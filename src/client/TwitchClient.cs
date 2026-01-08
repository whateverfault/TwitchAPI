using TwitchAPI.client.commands;
using TwitchAPI.client.commands.data;
using TwitchAPI.client.credentials;
using TwitchAPI.client.data;
using TwitchAPI.api.data.badges.data.badge;
using TwitchAPI.event_sub;
using TwitchAPI.event_sub.subscription_data.events.chat_message;
using TwitchAPI.api;
using ChatMessage = TwitchAPI.client.data.ChatMessage;
using System.Threading.Channels;

namespace TwitchAPI.client;

public enum LogLevel {
    Debug,
    Info,
    Warning,
    Error,
}

internal struct QueuedMessage {
    public readonly string Message;
    public readonly string? ReplyId;
    public readonly bool IsWhisper;

    public QueuedMessage(string message, string? replyId = null, bool isWhisper = false) {
        Message = message;
        ReplyId = replyId;
        IsWhisper = isWhisper;
    }
}

public class TwitchClient : ITwitchClient {
    private readonly CommandParser _commandParser;
    private readonly TwitchClientConfig _config;

    private readonly TimeSpan _sendMessageDelay = TimeSpan.FromMilliseconds(1000);
    private readonly object _lock = new object();

    private bool _initializing;
    private bool _reconnecting;

    private CancellationTokenSource _sendMessagesTaskCts;
    private Task? _sendMessagesTask;

    private TwitchEventSubWebSocket? _websocket;

    private Badge[]? _globalBadges;
    private Badge[]? _channelBadges;

    private event EventHandler<ChatMessage>? MessagePipeline;

    private TwitchApi Api => _config.Api;

    public FullCredentials? Credentials { get; private set; }

    private readonly Channel<QueuedMessage> _messageChannel;

    public event EventHandler<ChatMessage>? OnMessageReceived;
    public event EventHandler<Command>? OnCommandReceived;
    public event EventHandler? OnConnected;
    public event EventHandler<string>? OnDisconnected;
    public event EventHandler<string>? OnError;

    public TwitchClient(TwitchClientConfig? config = null) {
        _config = config ?? new TwitchClientConfig();
        _commandParser = new CommandParser(_config.CommandIdentifier);

        _messageChannel = Channel.CreateUnbounded<QueuedMessage>();
        _sendMessagesTaskCts = new CancellationTokenSource();

        InitializeWebSocket();
        StartSendLoop();
    }

    private void StartSendLoop() {
        _sendMessagesTask = Task.Run(() => SendMessagesRoutine(_sendMessagesTaskCts.Token), CancellationToken.None);
    }

    private async Task Initialize(FullCredentials credentials) {
        await Initialize(new ConnectionCredentials(credentials.Channel, credentials.Oauth, credentials.ChannelOauth));
    }

    public async Task Initialize(ConnectionCredentials credentials) {
        lock (_lock) {
            if (_initializing)
                return;

            _initializing = true;
        }

        try {
            if (_websocket == null)
                return;

            var validate = await Api.ValidateOauth(credentials.Oauth, RaiseError);
            if (validate == null)
                return;

            var channelInfo = await Api.GetUserInfoByUsername(
                credentials.Channel,
                credentials.Oauth,
                validate.ClientId,
                RaiseError
            );

            if (channelInfo == null)
                return;

            Credentials = new FullCredentials(
                validate.Login,
                credentials.Channel,
                credentials.Oauth,
                credentials.ChannelOauth,
                validate.ClientId,
                validate.UserId,
                channelInfo.Id
            );

            var connected = await _websocket.ConnectAsync();
            if (!connected) {
                RaiseError(this, "WebSocket connection failed.");
                return;
            }

            await SubscribeToChat();

            _globalBadges = await Api.ListGlobalBadges(Credentials, RaiseError);
            _channelBadges = await Api.ListChannelBadges(Credentials, RaiseError);
        }
        finally {
            lock (_lock) {
                _initializing = false;
            }
        }
    }

    private void RaiseMessage(ChatMessage message) {
        var handler = OnMessageReceived;
        if (handler != null)
            handler(this, message);
    }

    private void RaiseCommand(Command command) {
        var handler = OnCommandReceived;
        if (handler != null)
            handler(this, command);
    }

    private void RaiseConnected() {
        var handler = OnConnected;
        if (handler != null)
            handler(this, EventArgs.Empty);
    }

    private void RaiseDisconnected(string message) {
        var handler = OnDisconnected;
        if (handler != null)
            handler(this, message);
    }

    private void RaiseError(object? sender, string message) {
        var handler = OnError;
        if (handler != null)
            handler(sender ?? this, message);
    }

    private void HandleChatMessage(object? sender, ChatMessageEvent? e) {
        if (e == null || Credentials == null)
            return;

        if (e.UserId == Credentials.UserId)
            return;

        var message = ChatMessage.Create(e, _globalBadges, _channelBadges);

        RaiseMessage(message);

        var pipeline = MessagePipeline;
        if (pipeline != null)
            pipeline(this, message);
    }

    private void HandleChatCommand(object? sender, ChatMessage message) {
        var command = _commandParser.Parse(message);
        if (command != null)
            RaiseCommand(command);
    }

    private void InitializeWebSocket() {
        if (_websocket != null) {
            _websocket.OnChatMessageReceived -= HandleChatMessage;
            _websocket.OnWebSocketError -= HandleWebSocketError;
            _websocket.OnConnectionClosed -= HandleConnectionClosed;
        }

        _websocket = new TwitchEventSubWebSocket();

        _websocket.OnChatMessageReceived += HandleChatMessage;
        _websocket.OnWebSocketError += HandleWebSocketError;
        _websocket.OnConnectionClosed += HandleConnectionClosed;

        MessagePipeline -= HandleChatCommand;
        MessagePipeline += HandleChatCommand;

        OnError -= HandleAutoReconnect;
        OnError += HandleAutoReconnect;
    }

    private void HandleWebSocketError(object? sender, string message) {
        RaiseError(this, message);
    }

    private void HandleConnectionClosed(object? sender, string message) {
        RaiseDisconnected(message);
    }

    private async Task SubscribeToChat() {
        if (_websocket == null || Credentials == null)
            return;

        var payload = await EventSub.SubscribeToChannelChat(
            _websocket.SessionId,
            Credentials
        );

        if (payload?.Id == null) {
            RaiseError(this, "Failed to subscribe to chat.");
            return;
        }

        _websocket.SetSubscriptionId(payload.Id);
        RaiseConnected();
    }

    private void HandleAutoReconnect(object? sender, string message) {
        if (!_config.AutoReconnectConfig.AutoReconnect)
            return;

        if (message.Contains("Reconnect", StringComparison.OrdinalIgnoreCase))
            return;

        _ = Reconnect();
    }

    public async Task Reconnect() {
        lock (_lock) {
            if (_reconnecting)
                return;

            _reconnecting = true;
        }

        try {
            await DisconnectInternal(true);

            InitializeWebSocket();

            if (Credentials != null)
                await Initialize(Credentials);
        }
        finally {
            lock (_lock) {
                _reconnecting = false;
            }
        }
    }

    public async Task Disconnect() {
        await DisconnectInternal(false);
    }

    private async Task DisconnectInternal(bool reconnect) {
        if (_websocket?.SubscriptionId != null && Credentials != null) {
            await EventSub.EventSubUnSubscribe(
                _websocket.SubscriptionId,
                Credentials,
                RaiseError
            );
        }

        if (_websocket != null)
            await _websocket.DisconnectAsync();

        if (!reconnect)
            RaiseDisconnected("Disconnected.");
    }

    public Task SendMessage(string message, string? replyId = null) {
        if (Credentials == null) {
            OnError?.Invoke(this, "Couldn't send a message. Not initialized.");
            return Task.CompletedTask;
        }

        _messageChannel.Writer.TryWrite(new QueuedMessage(message, replyId));
        return Task.CompletedTask;
    }

    public Task SendWhisper(string message, string userId) {
        if (Credentials == null) {
            OnError?.Invoke(this, "Couldn't send a message. Not initialized.");
            return Task.CompletedTask;
        }

        _messageChannel.Writer.TryWrite(new QueuedMessage(message, userId, isWhisper: true));
        return Task.CompletedTask;
    }

    public bool SetCommandIdentifier(char identifier) {
        return _commandParser.SetCommandIdentifier(identifier);
    }

    public async Task UpdateChannel(string username) {
        if (Credentials == null) return;
        var userId = await ValidateUser(username, OnError);
        if (userId == null)
            return;

        Credentials.UpdateChannel(username);
        Credentials.UpdateChannelId(userId);
        _channelBadges = await Api.ListChannelBadges(Credentials, OnError);
    }

    public async Task UpdateOauth(string oauth) {
        var response = await Api.ValidateOauth(oauth, OnError);
        if (response == null)
            return;

        Credentials?.UpdateOauth(oauth);
        Credentials?.UpdateUsername(response.Login);
        Credentials?.UpdateUserId(response.UserId);
        Credentials?.UpdateClientId(response.ClientId);
    }

    public async Task UpdateChannelOauth(string oauth) {
        var response = await Api.ValidateOauth(oauth, OnError);
        if (response == null)
            return;

        Credentials?.UpdateChannelOauth(oauth);
        Credentials?.UpdateChannel(response.Login);
        Credentials?.UpdateChannelId(response.UserId);
    }

    private async Task SendMessagesRoutine(CancellationToken token) {
        var reader = _messageChannel.Reader;

        while (!token.IsCancellationRequested) {
            QueuedMessage message;

            try {
                message = await reader.ReadAsync(token);
            }
            catch {
                break;
            }

            if (Credentials == null)
                continue;

            try {
                if (message.IsWhisper) {
                    await Api.SendWhisper(
                        message.ReplyId!,
                        message.Message,
                        Credentials,
                        RaiseError
                    );
                }
                else if (message.ReplyId != null) {
                    await Api.SendReply(
                        message.Message,
                        message.ReplyId,
                        Credentials,
                        RaiseError
                    );
                }
                else {
                    await Api.SendMessage(
                        message.Message,
                        Credentials,
                        RaiseError
                    );
                }
            }
            catch { }

            try {
                await Task.Delay(_sendMessageDelay, token);
            }
            catch {
                break;
            }
        }
    }

    private async Task<string?> ValidateUser(string username, EventHandler<string>? callback = null) {
        if (Credentials == null) {
            callback?.Invoke(this, "Cannot update a username before initializing a client.");
            return null;
        }

        var userId = await Api.GetUserId(username, Credentials);
        if (userId == null)
            callback?.Invoke(this, "User doesn't exist.");

        return userId;
    }
}
