using System.Net.WebSockets;
using System.Text;
using Newtonsoft.Json;
using TwitchAPI.client.credentials;
using TwitchAPI.event_sub.data;
using TwitchAPI.event_sub.data.subscription_data.events;
using TwitchAPI.event_sub.data.subscription_data.events.channel_points;
using TwitchAPI.event_sub.data.subscription_data.events.chat_message;
using TwitchAPI.event_sub.data.subscription_data.session;
using TwitchAPI.event_sub.data.subscription_data.session.reconnect;

namespace TwitchAPI.event_sub;

public sealed class TwitchEventSubWebSocket {
    private static readonly TimeSpan _receiveTimeout = TimeSpan.FromSeconds(70);
    private static readonly TimeSpan _reconnectWindow = TimeSpan.FromSeconds(25);

    private readonly object _sync = new object();

    private ClientWebSocket? _activeSocket;
    private CancellationTokenSource? _activeCts;
    private Task? _activeReceiveTask;

    private ClientWebSocket? _reconnectSocket;
    private CancellationTokenSource? _reconnectCts;
    private Task? _reconnectReceiveTask;
    private DateTime _reconnectDeadline;

    private readonly bool _broadcaster;
    
    private bool _reconnecting;
    private TaskCompletionSource<bool>? _initialWelcomeTcs;
    private FullCredentials? _credentials;

    private readonly List<EventSubSubscription> _subscriptions = new List<EventSubSubscription>();

    public string? SessionId { get; private set; }
    
    public event EventHandler<ChatMessageEvent?>? OnChatMessageReceived;
    public event EventHandler<ChannelPointsRedemptionEvent?>? OnRewardRedeemed;
    
    public event EventHandler? OnConnected;
    public event EventHandler<string>? OnConnectionClosed;
    public event EventHandler<string>? OnWebSocketError;
    public event EventHandler<ReconnectInfo>? OnReconnectRequired;

    
    public TwitchEventSubWebSocket(
        HttpClient? httpClient = null, 
        bool broadcaster = false) {
        var eventSub = new EventSub(httpClient);
        _broadcaster = broadcaster;
        
        RegisterCoreSubscriptions(eventSub);
    }

    public void SetCredentials(FullCredentials creds) {
        _credentials = creds;
    }

    public void RaiseChatMessage(ChatMessageEvent e) {
        OnChatMessageReceived?.Invoke(this, e);
    }
    
    public void RaiseRewardRedeemed(ChannelPointsRedemptionEvent e) {
        OnRewardRedeemed?.Invoke(this, e);
    }
    
    public async Task<bool> ConnectAsync() {
        try {
            _initialWelcomeTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            await ConnectActiveAsync("wss://eventsub.wss.twitch.tv/ws");
            return await _initialWelcomeTcs.Task;
        }
        catch {
            return false;
        }
    }

    public async Task DisconnectAsync() {
        SessionId = null;
        
        ClientWebSocket? socket;
        CancellationTokenSource? cts;
        Task? receiveTask;

        lock (_sync) {
            socket = _activeSocket;
            cts = _activeCts;
            receiveTask = _activeReceiveTask;
            _activeSocket = null;
            _activeCts = null;
            _activeReceiveTask = null;
        }

        cts?.Cancel();

        if (socket != null)
            await CloseSocketAsync(socket, "Client disconnect");

        if (receiveTask != null) {
            try {
                await receiveTask;
            }
            catch (Exception ex) {
                OnWebSocketError?.Invoke(this, ex.Message);
            }
        }

        try {
            socket?.Dispose();
        }
        catch (Exception ex) {
            OnWebSocketError?.Invoke(this, ex.Message);
        }

        try {
            cts?.Dispose();
        }
        catch (Exception ex) {
            OnWebSocketError?.Invoke(this, ex.Message);
        }
    }

    private async Task ConnectActiveAsync(string url) {
        var socket = new ClientWebSocket();
        var cts = new CancellationTokenSource();

        lock (_sync) {
            _activeSocket = socket;
            _activeCts = cts;
        }

        try {
            await socket.ConnectAsync(new Uri(url), CancellationToken.None);
            _activeReceiveTask = Task.Run(() => ReceiveLoopAsync(socket, cts.Token, true), CancellationToken.None);
        }
        catch (Exception ex) {
            Cleanup(socket, cts);
            OnWebSocketError?.Invoke(this, ex.Message);
        }
    }

    private async Task ReceiveLoopAsync(ClientWebSocket socket, CancellationToken token, bool canBecomeActive) {
        var buffer = new byte[4096];
        var messageBuffer = new List<byte>();
        var lastReceiveUtc = DateTime.UtcNow;

        try {
            while (!token.IsCancellationRequested && socket.State == WebSocketState.Open) {
                using var receiveCts = CancellationTokenSource.CreateLinkedTokenSource(token);
                var receiveTask = socket.ReceiveAsync(buffer, receiveCts.Token);
                var timeoutTask = Task.Delay(_receiveTimeout, CancellationToken.None);

                var completed = await Task.WhenAny(receiveTask, timeoutTask);
                if (completed == timeoutTask) {
                    receiveCts.Cancel();
                    
                    if (DateTime.UtcNow - lastReceiveUtc >= _receiveTimeout) {
                        await CloseSocketAsync(socket, "Receive timeout");
                        break;
                    }
                    continue;
                }

                var result = await receiveTask;
                lastReceiveUtc = DateTime.UtcNow;

                if (result.MessageType == WebSocketMessageType.Close) {
                    OnConnectionClosed?.Invoke(this, "Closed by server");
                    break;
                }

                messageBuffer.AddRange(buffer[..result.Count]);
                if (!result.EndOfMessage)
                    continue;

                var message = Encoding.UTF8.GetString(messageBuffer.ToArray());
                messageBuffer.Clear();

                await HandleMessageAsync(message, socket, canBecomeActive);
            }
        }
        catch (OperationCanceledException) {
            OnConnectionClosed?.Invoke(this, "Connection canceled");
        }
        catch (WebSocketException ex) {
            OnConnectionClosed?.Invoke(this, ex.Message);
        }
        catch (Exception ex) {
            OnWebSocketError?.Invoke(this, ex.Message);
        }
        finally {
            bool shouldDispose;

            lock (_sync) {
                shouldDispose = socket != _activeSocket && socket != _reconnectSocket;
            }

            if (shouldDispose)
                Cleanup(socket, null);
        }
    }

    private async Task HandleMessageAsync(string message, ClientWebSocket source, bool canBecomeActive) {
        EventSubMessage<SessionMetadata, object>? baseMessage;

        try {
            baseMessage = JsonConvert.DeserializeObject<EventSubMessage<SessionMetadata, object>>(message);
        }
        catch { return; }

        if (baseMessage == null)
            return;

        switch (baseMessage.Metadata.MessageType) {
            case "session_welcome":
                await HandleWelcomeAsync(message, source, canBecomeActive);
                break;

            case "session_keepalive":
                break;

            case "session_reconnect":
                await HandleReconnectAsync(message);
                break;

            case "notification":
                DispatchNotification(baseMessage.Metadata.SubscriptionType, message);
                break;
        }
    }

    private async Task HandleWelcomeAsync(string message, ClientWebSocket source, bool canBecomeActive) {
        EventSubMessage<SessionMetadata, SessionWelcomePayload>? welcome;

        try {
            welcome = JsonConvert.DeserializeObject<EventSubMessage<SessionMetadata, SessionWelcomePayload>>(message);
        }
        catch { return; }

        if (welcome == null)
            return;

        SessionId = welcome.Payload.Session.Id;
        _initialWelcomeTcs?.TrySetResult(true);

        if (_credentials != null)
            _ = SubscribeAllInternal();

        if (canBecomeActive)
            await PromoteReconnectSocketAsync(source);
    }

    private async Task HandleReconnectAsync(string message) {
        EventSubMessage<SessionMetadata, SessionReconnectPayload>? reconnect;

        try {
            reconnect = JsonConvert.DeserializeObject<EventSubMessage<SessionMetadata, SessionReconnectPayload>>(message);
        }
        catch { return; }

        if (reconnect == null)
            return;

        var url = reconnect.Payload.Session.ReconnectUrl;
        var deadline = DateTime.UtcNow.Add(_reconnectWindow);

        OnReconnectRequired?.Invoke(this, new ReconnectInfo(url, deadline));
        await BeginReconnectAsync(url, deadline);
    }

    private void DispatchNotification(string? subscriptionType, string rawJson) {
        if (subscriptionType == null)
            return;

        var sub = _subscriptions.FirstOrDefault(s => s.Type == subscriptionType);
        sub?.Dispatch(rawJson);
    }

    private void RegisterCoreSubscriptions(EventSub eventSub) {
        switch (_broadcaster) {
            case true: {
                _subscriptions.Add(EventSubSubscription.CreateRedemptions(this, eventSub));
                break;
            }

            case false: {
                _subscriptions.Add(EventSubSubscription.CreateChat(this, eventSub));
                break;
            }
        }
    }

    private async Task SubscribeAllInternal() {
        if (_credentials == null || SessionId == null)
            return;

        foreach (var sub in _subscriptions) {
            try {
                await sub.SubscribeAsync(SessionId, _credentials, OnWebSocketError);
            }
            catch (Exception ex) {
                OnWebSocketError?.Invoke(this, ex.Message);
            }
        }

        OnConnected?.Invoke(this, EventArgs.Empty);
    }

    private async Task BeginReconnectAsync(string url, DateTime deadline) {
        lock (_sync) {
            if (_reconnecting)
                return;

            _reconnecting = true;
            _reconnectDeadline = deadline;
        }

        var socket = new ClientWebSocket();
        var cts = new CancellationTokenSource();

        try {
            await socket.ConnectAsync(new Uri(url), cts.Token);

            lock (_sync) {
                _reconnectSocket = socket;
                _reconnectCts = cts;
            }

            _reconnectReceiveTask = Task.Run(() => ReceiveLoopAsync(socket, cts.Token, true), CancellationToken.None);

            _ = Task.Run(async () => {
                await Task.Delay(_reconnectWindow, CancellationToken.None); 
                if (DateTime.UtcNow >= _reconnectDeadline)
                    await AbortReconnectAsync();
                         }, CancellationToken.None);
        }
        catch (Exception ex) {
            Cleanup(socket, cts);
            OnWebSocketError?.Invoke(this, ex.Message);
        }
    }

    private async Task PromoteReconnectSocketAsync(ClientWebSocket newSocket) {
        ClientWebSocket? oldSocket;
        CancellationTokenSource? oldCts;

        lock (_sync) {
            if (_reconnectSocket != newSocket)
                return;

            oldSocket = _activeSocket;
            oldCts = _activeCts;

            _activeSocket = newSocket;
            _activeCts = _reconnectCts;

            _reconnectSocket = null;
            _reconnectCts = null;
            _reconnecting = false;
        }

        if (oldSocket != null) {
            try {
                await oldSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Reconnected", CancellationToken.None);
            }
            catch {
                try {
                    oldSocket.Abort();
                }
                catch (Exception ex) {
                    OnWebSocketError?.Invoke(this, ex.Message);
                }
            }
            finally {
                oldCts?.Cancel();
                
                try {
                    oldSocket.Dispose();
                } 
                catch (Exception ex) {
                    OnWebSocketError?.Invoke(this, ex.Message);
                }

                try {
                    oldCts?.Dispose();
                } 
                catch (Exception ex) {
                    OnWebSocketError?.Invoke(this, ex.Message);
                }
            }
        }
    }

    private async Task AbortReconnectAsync() {
        ClientWebSocket? socket;
        CancellationTokenSource? cts;

        lock (_sync) {
            socket = _reconnectSocket;
            cts = _reconnectCts;
            _reconnectSocket = null;
            _reconnectCts = null;
            _reconnecting = false;
        }

        try {
            socket?.Abort();
        } 
        catch (Exception ex) {
            OnWebSocketError?.Invoke(this, ex.Message);
        }

        try {
            socket?.Dispose();
        } 
        catch (Exception ex) {
            OnWebSocketError?.Invoke(this, ex.Message);
        }

        try {
            cts?.Cancel();
        } 
        catch (Exception ex) {
            OnWebSocketError?.Invoke(this, ex.Message);
        }

        try {
            cts?.Dispose();
        } 
        catch (Exception ex) {
            OnWebSocketError?.Invoke(this, ex.Message);
        }

        await Task.CompletedTask;
    }

    private async Task CloseSocketAsync(ClientWebSocket socket, string reason) {
        if (socket.State == WebSocketState.Open) {
            try {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, reason, CancellationToken.None);
            }
            catch {
                try {
                    socket.Abort();
                } 
                catch (Exception ex) {
                    OnWebSocketError?.Invoke(this, ex.Message);
                }
            }
        }
    }

    private void Cleanup(ClientWebSocket socket, CancellationTokenSource? cts) {
        try {
            cts?.Cancel();
        } 
        catch (Exception ex) {
            OnWebSocketError?.Invoke(this, ex.Message);
        }

        try {
            socket.Dispose();
        } 
        catch (Exception ex) {
            OnWebSocketError?.Invoke(this, ex.Message);
        }

        try {
            cts?.Dispose();
        } 
        catch (Exception ex) {
            OnWebSocketError?.Invoke(this, ex.Message);
        }
    }
}
