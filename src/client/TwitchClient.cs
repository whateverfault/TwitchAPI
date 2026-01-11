using TwitchAPI.client.commands;
using TwitchAPI.client.commands.data;
using TwitchAPI.client.credentials;
using TwitchAPI.client.data;
using TwitchAPI.api.data.badges.data.badge;
using TwitchAPI.event_sub;
using TwitchAPI.event_sub.data.subscription_data.events.chat_message;
using TwitchAPI.api;
using ChatMessage = TwitchAPI.client.data.ChatMessage;
using System.Threading.Channels;
using TwitchAPI.api.data.responses.GetUserInfo;
using TwitchAPI.event_sub.data.subscription_data.events.channel_points;
using TwitchAPI.shared;

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

    private readonly CancellationTokenSource _sendMessagesTaskCts;

    private readonly HttpClient _httpClient;
    
    private TwitchEventSubWebSocket? _botWebSocket;
    private TwitchEventSubWebSocket? _broadcasterWebSocket;
    
    private Badge[]? _globalBadges;
    private Badge[]? _channelBadges;

    private event EventHandler<ChatMessage>? MessagePipeline;

    private TwitchApi Api => _config.Api;

    public FullCredentials? Credentials { get; private set; }

    private readonly Channel<QueuedMessage> _messageChannel;

    public event EventHandler<RewardRedemption>? OnRewardRedeemed;
    public event EventHandler<ChatMessage>? OnMessageReceived;
    public event EventHandler<Command>? OnCommandReceived;
    
    public event EventHandler? OnConnected;
    public event EventHandler<string>? OnDisconnected;
    public event EventHandler<string>? OnError;

    
    public TwitchClient(TwitchClientConfig? config = null, HttpClient? httpClient = null) {
        _config = config ?? new TwitchClientConfig();
        _httpClient = httpClient ?? new HttpClient(HttpHandlerProvider.SharedHandler, disposeHandler: false);
        _commandParser = new CommandParser(_config.CommandIdentifier);

        _messageChannel = Channel.CreateUnbounded<QueuedMessage>();
        _sendMessagesTaskCts = new CancellationTokenSource();

        InitializeWebSocket(_httpClient);
        StartSendLoop();
    }

    private void StartSendLoop() {
        Task.Run(() => SendMessagesRoutine(_sendMessagesTaskCts.Token), CancellationToken.None);
    }

    public async Task<FullCredentials?> GetFullCredentials(ConnectionCredentials credentials) {
        var validate = await Api.ValidateOauth(
                                               credentials.Oauth,
                                               RaiseError
                                               );
        if (validate == null)
            return null;

        var broadcasterClientId = string.Empty;

        if (!string.IsNullOrEmpty(credentials.BroadcasterOauth)) {
            var validateBroadcaster = await Api.ValidateOauth(
                                               credentials.BroadcasterOauth,
                                               RaiseError
                                              );

            if (validateBroadcaster == null)
                return null;
            
            broadcasterClientId = validate.ClientId;
        }
        
        var bot = await Api.GetUserInfoByUserName(
                                                  validate.Login, 
                                                  credentials.Oauth, 
                                                  validate.ClientId, 
                                                  RaiseError
                                                  );

        if (bot == null)
            return null;
        
        var broadcaster = await Api.GetUserInfoByUserName(
                                                          credentials.Channel,
                                                          credentials.Oauth,
                                                          validate.ClientId,
                                                          RaiseError
                                                         );

        if (broadcaster == null)
            return null;

        return new FullCredentials(
                                   new Credentials(
                                                   bot.DisplayName,
                                                   validate.Login,
                                                   validate.UserId,
                                                   credentials.Oauth,
                                                   validate.ClientId
                                                  ),
                                   new Credentials(
                                                   broadcaster.DisplayName,
                                                   broadcaster.Login,
                                                   broadcaster.UserId,
                                                   credentials.BroadcasterOauth,
                                                   broadcasterClientId
                                                  )
                                  );
    }
    
    public async Task Initialize(FullCredentials credentials) {
        lock (_lock) {
            if (_initializing)
                return;

            _initializing = true;
        }

        try {
            if (_botWebSocket == null
             || _broadcasterWebSocket == null)
                return;

            Credentials = credentials;
            
            _botWebSocket.SetCredentials(Credentials);
            _broadcasterWebSocket.SetCredentials(Credentials);

            var connected = await _botWebSocket.ConnectAsync();
            connected &= await _broadcasterWebSocket.ConnectAsync();
            
            if (!connected) {
                RaiseError(this, "WebSocket connection failed.");
                return;
            }

            _globalBadges = await Api.ListGlobalBadges(Credentials, RaiseError);
            _channelBadges = await Api.ListChannelBadges(Credentials, RaiseError);
        }
        finally {
            lock (_lock) {
                _initializing = false;
            }
        }
    }

    public async Task Initialize(ConnectionCredentials credentials) {
        var fullCredentials = await GetFullCredentials(credentials);
        if (fullCredentials == null) {
            return;
        }
        
        await Initialize(fullCredentials);
    }

    private void RaiseRewardRedemption(RewardRedemption redemption) {
        var handler = OnRewardRedeemed;
        if (handler != null)
            handler(this, redemption);
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

    private void RaiseConnected(object? sender, EventArgs args) {
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

    private void HandleRewardRedemption(object? sender, ChannelPointsRedemptionEvent? e) {
        if (e == null || Credentials == null)
            return;

        if (e.UserId == Credentials.Bot.UserId)
            return;
        
        var redemption = RewardRedemption.Create(e);
        
        RaiseRewardRedemption(redemption);
    }
    
    private void HandleChatMessage(object? sender, ChatMessageEvent? e) {
        if (e == null || Credentials == null)
            return;

        if (e.UserId == Credentials.Bot.UserId)
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

    private void InitializeWebSocket(HttpClient? httpClient = null) {
        if (_botWebSocket != null) {
            _botWebSocket.OnConnected -= RaiseConnected;
            _botWebSocket.OnChatMessageReceived -= HandleChatMessage;
            _botWebSocket.OnWebSocketError -= HandleWebSocketError;
            _botWebSocket.OnConnectionClosed -= HandleConnectionClosed;
        }

        if (_broadcasterWebSocket != null) {
            _broadcasterWebSocket.OnRewardRedeemed -= HandleRewardRedemption;
            _broadcasterWebSocket.OnWebSocketError -= HandleWebSocketError;
            _broadcasterWebSocket.OnConnectionClosed -= HandleConnectionClosed;
        }

        _botWebSocket = new TwitchEventSubWebSocket(httpClient, broadcaster: false);
        _broadcasterWebSocket = new TwitchEventSubWebSocket(httpClient, broadcaster: true);

        _botWebSocket.OnConnected += RaiseConnected;
        _botWebSocket.OnChatMessageReceived += HandleChatMessage;
        _botWebSocket.OnWebSocketError += HandleWebSocketError;
        _botWebSocket.OnConnectionClosed += HandleConnectionClosed;
        
        _broadcasterWebSocket.OnRewardRedeemed += HandleRewardRedemption;
        _broadcasterWebSocket.OnWebSocketError += HandleWebSocketError;
        _broadcasterWebSocket.OnConnectionClosed += HandleConnectionClosed;
        
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

            InitializeWebSocket(_httpClient);

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
        if (_botWebSocket != null) 
            await _botWebSocket.DisconnectAsync();

        if (_broadcasterWebSocket != null)
            await _broadcasterWebSocket.DisconnectAsync();
        
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

    public async Task UpdateBroadcaster(string username) {
        if (Credentials == null) 
            return;
        
        var userInfo = await ValidateUserName(username, OnError);
        if (userInfo == null)
            return;

        
        Credentials.Broadcaster.UpdateDisplayName(userInfo.DisplayName);
        Credentials.Broadcaster.UpdateLogin(userInfo.Login);
        Credentials.Broadcaster.UpdateUserId(userInfo.UserId);
        
        _channelBadges = await Api.ListChannelBadges(Credentials, OnError);
    }

    public async Task UpdateOauth(string oauth) {
        if (Credentials == null) 
            return;
        
        var response = await Api.ValidateOauth(oauth, OnError);
        if (response == null)
            return;

        var userInfo = await ValidateUserName(response.Login, OnError);
        if (userInfo == null)
            return;
        
        Credentials.Bot.UpdateOauth(oauth);
        Credentials.Bot.UpdateLogin(response.Login);
        Credentials.Bot.UpdateUserId(response.UserId);
        Credentials.Bot.UpdateClientId(response.ClientId);
        Credentials.Bot.UpdateDisplayName(userInfo.DisplayName);
    }

    public async Task UpdateBroadcasterOauth(string oauth) {
        if (Credentials == null) 
            return;
        
        var response = await Api.ValidateOauth(oauth, OnError);
        if (response == null)
            return;

        var userInfo = await ValidateUserName(response.Login, OnError);
        if (userInfo == null)
            return;
        
        Credentials.Broadcaster.UpdateOauth(oauth);
        Credentials.Broadcaster.UpdateLogin(response.Login);
        Credentials.Broadcaster.UpdateUserId(response.UserId);
        Credentials.Broadcaster.UpdateClientId(response.ClientId);
        Credentials.Broadcaster.UpdateDisplayName(userInfo.DisplayName);
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
            catch {
                RaiseError(this, "Failed to send a message.");
            }

            try {
                await Task.Delay(_sendMessageDelay, token);
            }
            catch {
                break;
            }
        }
    }

    private async Task<UserInfo?> ValidateUserName(string username, EventHandler<string>? callback = null) {
        if (Credentials == null) {
            callback?.Invoke(this, "Cannot update a username before initializing a client.");
            return null;
        }

        var userInfo = await Api.GetUserInfoByUserName(username, Credentials.Bot.Oauth, Credentials.Bot.ClientId, RaiseError);
        return userInfo;
    }
}
