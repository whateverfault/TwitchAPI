using System.Net.WebSockets;
using System.Text;
using Newtonsoft.Json;
using TwitchAPI.event_sub.subscription_data.events;
using TwitchAPI.event_sub.subscription_data.events.chat_message;
using TwitchAPI.event_sub.subscription_data.session;
using TwitchAPI.event_sub.subscription_data.session.reconnect;

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

    private bool _reconnecting;

    private TaskCompletionSource<bool>? _initialWelcomeTcs;

    public event EventHandler<ChatMessageEvent?>? OnChatMessageReceived;
    public event EventHandler<string>? OnConnectionClosed;
    public event EventHandler<string>? OnWebSocketError;
    public event EventHandler<ReconnectInfo>? OnReconnectRequired;

    public string? SessionId { get; private set; }
    public string? SubscriptionId { get; private set; }

    public async Task<bool> ConnectAsync() {
        try {
            _initialWelcomeTcs =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            await ConnectActiveAsync("wss://eventsub.wss.twitch.tv/ws");
            return await _initialWelcomeTcs.Task;
        }
        catch {
            return false;
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
                var receiveTask = socket.ReceiveAsync(buffer, token);
                var timeoutTask = Task.Delay(_receiveTimeout, CancellationToken.None);

                var completed = await Task.WhenAny(receiveTask, timeoutTask);
                if (completed == timeoutTask) {
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
                shouldDispose =
                    socket != _activeSocket &&
                    socket != _reconnectSocket;
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
        catch {
            return;
        }

        if (baseMessage == null)
            return;

        switch (baseMessage.Metadata.MessageType) {
            case "session_welcome": {
                EventSubMessage<SessionMetadata, SessionWelcomePayload>? welcome;
                try {
                    welcome =
                        JsonConvert.DeserializeObject<EventSubMessage<SessionMetadata, SessionWelcomePayload>>(message);
                }
                catch {
                    return;
                }

                if (welcome == null)
                    return;

                SessionId = welcome.Payload.Session.Id;
                _initialWelcomeTcs?.TrySetResult(true);

                if (canBecomeActive)
                    await PromoteReconnectSocketAsync(source);

                break;
            }

            case "session_keepalive":
                break;

            case "session_reconnect": {
                EventSubMessage<SessionMetadata, SessionReconnectPayload>? reconnect;
                try {
                    reconnect =
                        JsonConvert.DeserializeObject<EventSubMessage<SessionMetadata, SessionReconnectPayload>>(message);
                }
                catch {
                    return;
                }

                if (reconnect == null)
                    return;

                var url = reconnect.Payload.Session.ReconnectUrl;
                var deadline = DateTime.UtcNow.Add(_reconnectWindow);

                OnReconnectRequired?.Invoke(this, new ReconnectInfo(url, deadline));
                await BeginReconnectAsync(url, deadline);
                break;
            }

            case "notification" when baseMessage.Metadata.SubscriptionType == "channel.chat.message": {
                EventSubMessage<SessionMetadata, EventSubMessagePayload<ChatMessageEvent>>? chat;
                try {
                    chat =
                        JsonConvert.DeserializeObject<EventSubMessage<SessionMetadata, EventSubMessagePayload<ChatMessageEvent>>>(message);
                }
                catch {
                    return;
                }

                OnChatMessageReceived?.Invoke(this, chat?.Payload.Event);
                break;
            }
        }
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
                if (DateTime.UtcNow >= _reconnectDeadline) {
                    await AbortReconnectAsync(); 
                } 
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
                await oldSocket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Reconnected",
                    CancellationToken.None);
            }
            catch {
                try { oldSocket.Abort(); } catch { }
            }
            finally {
                oldCts?.Cancel();
                oldSocket.Dispose();
                oldCts?.Dispose();
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

        if (socket != null) {
            try { socket.Abort(); } catch { }
            socket.Dispose();
        }

        if (cts != null) {
            cts.Cancel();
            cts.Dispose();
        }

        await Task.CompletedTask;
    }

    public async Task DisconnectAsync() {
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

        if (cts != null)
            cts.Cancel();

        if (socket != null)
            await CloseSocketAsync(socket, "Client disconnect");

        if (receiveTask != null) {
            try { await receiveTask; } catch { }
        }

        if (socket != null)
            socket.Dispose();

        if (cts != null)
            cts.Dispose();
    }

    private static async Task CloseSocketAsync(ClientWebSocket socket, string reason) {
        if (socket.State == WebSocketState.Open) {
            try {
                await socket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    reason,
                    CancellationToken.None);
            }
            catch {
                try { socket.Abort(); } catch { }
            }
        }
    }

    private static void Cleanup(ClientWebSocket socket, CancellationTokenSource? cts) {
        try { cts?.Cancel(); } catch { }
        try { socket.Dispose(); } catch { }
        try { cts?.Dispose(); } catch { }
    }

    public void SetSubscriptionId(string id) {
        SubscriptionId = id;
    }
}
