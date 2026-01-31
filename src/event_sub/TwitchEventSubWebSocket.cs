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
    private readonly object _sync = new object();

    private ClientWebSocket? _socket;
    private CancellationTokenSource? _cts;
    private Task? _loopTask;

    private TaskCompletionSource<bool>? _welcomeTcs;

    private DateTime _lastMessageUtc;
    private TimeSpan _keepAliveTimeout = TimeSpan.FromSeconds(600);

    private string _nextUrl = "wss://eventsub.wss.twitch.tv/ws";
    private ReconnectKind _reconnectKind = ReconnectKind.None;
    private bool _running;

    private readonly bool _broadcaster;
    private readonly List<EventSubSubscription> _subscriptions = [];

    private FullCredentials? _credentials;
    private string? SessionId { get; set; }

    private enum ReconnectKind {
        None,
        Twitch,
        KeepAlive,
        Failure
    }

    public event EventHandler<ChatMessageEvent?>? OnChatMessageReceived;
    public event EventHandler<ChannelPointsRedemptionEvent?>? OnRewardRedeemed;
    public event EventHandler? OnConnected;
    public event EventHandler<string>? OnConnectionClosed;
    public event EventHandler<string>? OnWebSocketError;
    public event EventHandler<ReconnectInfo>? OnReconnectRequired;

    public TwitchEventSubWebSocket(HttpClient? httpClient = null, bool broadcaster = false) {
        var eventSub = new EventSub(httpClient);
        _broadcaster = broadcaster;
        RegisterCoreSubscriptions(eventSub);
    }

    public void SetCredentials(FullCredentials creds) {
        _credentials = creds;
    }

    public async Task ConnectAsync() {
        TaskCompletionSource<bool> tcs;

        lock (_sync) {
            if (_running)
                return;

            _running = true;
            _welcomeTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            tcs = _welcomeTcs;
            _loopTask = Task.Run(MainLoopAsync);
        }

        await tcs.Task;
    }

    public async Task DisconnectAsync() {
        lock (_sync) {
            _running = false;
        }

        await ForceCloseSocketAsync();

        if (_loopTask != null)
            await _loopTask;
    }

    private async Task MainLoopAsync() {
        while (true) {
            string url;

            lock (_sync) {
                if (!_running)
                    return;

                url = _nextUrl;
                _reconnectKind = ReconnectKind.None;
            }

            try {
                await RunConnectionAsync(url);
            }
            catch (Exception ex) {
                OnWebSocketError?.Invoke(this, ex.Message);
                lock (_sync) {
                    _reconnectKind = ReconnectKind.Failure;
                    _nextUrl = "wss://eventsub.wss.twitch.tv/ws";
                }
                await Task.Delay(2000);
            }
        }
    }

    private async Task RunConnectionAsync(string url) {
        var socket = new ClientWebSocket();
        var cts = new CancellationTokenSource();

        lock (_sync) {
            _socket = socket;
            _cts = cts;
        }

        await socket.ConnectAsync(new Uri(url), cts.Token);
        _lastMessageUtc = DateTime.UtcNow;

        var receiveTask = ReceiveLoopAsync(socket, cts.Token);
        await receiveTask;

        await ForceCloseSocketAsync();
        OnConnectionClosed?.Invoke(this, "Socket closed");
    }

    private async Task ReceiveLoopAsync(ClientWebSocket socket, CancellationToken token) {
        var buffer = new byte[8192];
        var messageBuffer = new List<byte>();

        while (!token.IsCancellationRequested && socket.State == WebSocketState.Open) {
            if (DateTime.UtcNow - _lastMessageUtc > _keepAliveTimeout) {
                lock (_sync) {
                    _reconnectKind = ReconnectKind.KeepAlive;
                    _nextUrl = "wss://eventsub.wss.twitch.tv/ws";
                }
                return;
            }

            var result = await socket.ReceiveAsync(buffer, token);

            if (result.MessageType == WebSocketMessageType.Close)
                return;

            messageBuffer.AddRange(buffer[..result.Count]);

            if (!result.EndOfMessage)
                continue;

            _lastMessageUtc = DateTime.UtcNow;

            var message = Encoding.UTF8.GetString(messageBuffer.ToArray());
            messageBuffer.Clear();

            await HandleMessageAsync(message);
        }
    }

    private async Task HandleMessageAsync(string message) {
        var baseMsg = JsonConvert.DeserializeObject<EventSubMessage<SessionMetadata, object>>(message);
        if (baseMsg == null)
            return;

        switch (baseMsg.Metadata.MessageType) {
            case "session_welcome":
                await HandleWelcomeAsync(message);
                break;

            case "session_keepalive":
                _lastMessageUtc = DateTime.UtcNow;
                break;

            case "session_reconnect":
                HandleReconnect(message);
                break;

            case "notification":
                DispatchNotification(baseMsg.Metadata.SubscriptionType, message);
                break;
        }
    }

    private async Task HandleWelcomeAsync(string message) {
        var welcome = JsonConvert.DeserializeObject<EventSubMessage<SessionMetadata, SessionWelcomePayload>>(message);
        if (welcome == null)
            return;

        SessionId = welcome.Payload.Session.Id;

        if (welcome.Payload.Session.KeepaliveTimeoutSeconds is var s)
            _keepAliveTimeout = TimeSpan.FromSeconds(s);

        if (_credentials != null && _reconnectKind != ReconnectKind.Twitch)
            await SubscribeAllInternal();

        _welcomeTcs?.TrySetResult(true);
        OnConnected?.Invoke(this, EventArgs.Empty);
    }

    private void HandleReconnect(string message) {
        var reconnect = JsonConvert.DeserializeObject<EventSubMessage<SessionMetadata, SessionReconnectPayload>>(message);
        if (reconnect == null)
            return;

        lock (_sync) {
            _reconnectKind = ReconnectKind.Twitch;
            _nextUrl = reconnect.Payload.Session.ReconnectUrl;
        }

        OnReconnectRequired?.Invoke(this,
                                    new ReconnectInfo(_nextUrl, DateTime.UtcNow.AddSeconds(30)));

        _cts?.Cancel();
    }

    private void DispatchNotification(string? type, string rawJson) {
        if (type == null)
            return;

        var sub = _subscriptions.FirstOrDefault(s => s.Type == type);
        sub?.Dispatch(rawJson);
    }

    private void RegisterCoreSubscriptions(EventSub eventSub) {
        _subscriptions.Add(_broadcaster
                               ? EventSubSubscription.CreateRedemptions(this, eventSub)
                               : EventSubSubscription.CreateChat(this, eventSub));
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
    }

    private async Task ForceCloseSocketAsync() {
        ClientWebSocket? socket;
        CancellationTokenSource? cts;

        lock (_sync) {
            socket = _socket;
            cts = _cts;
            _socket = null;
            _cts = null;
        }

        try { cts?.Cancel(); } catch { }
        try { socket?.Abort(); } catch { }
        try { socket?.Dispose(); } catch { }
        try { cts?.Dispose(); } catch { }

        await Task.CompletedTask;
    }

    public void RaiseChatMessage(ChatMessageEvent e) {
        OnChatMessageReceived?.Invoke(this, e);
    }

    public void RaiseRewardRedeemed(ChannelPointsRedemptionEvent e) {
        OnRewardRedeemed?.Invoke(this, e);
    }
}
