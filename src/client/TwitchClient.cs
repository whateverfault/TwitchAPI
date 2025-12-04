using TwitchAPI.client.commands;
using TwitchAPI.client.commands.data;
using TwitchAPI.client.credentials;
using TwitchAPI.client.data;
using TwitchAPI.client.data.badges;
using TwitchAPI.client.data.badges.data.badge;
using TwitchAPI.event_sub;
using TwitchAPI.event_sub.subscription_data.events.chat_message;
using TwitchAPI.helix;
using TwitchAPI.helix.data.requests.chat_subscription;
using ChatMessage = TwitchAPI.client.data.ChatMessage;

namespace TwitchAPI.client;

public enum LogLevel {
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
    
    private readonly TimeSpan _sendMessageDelay = TimeSpan.FromMilliseconds(500);
    
    private readonly object _lock = new object();
    
    private bool _initializing;
    
    private CancellationTokenSource _sendMessagesTaskCts;
    
    private readonly Queue<QueuedMessage> _messageQueue;
    private TwitchEventSubWebSocket? _websocket;

    private Badge[]? _globalBadges;
    private Badge[]? _channelBadges;
    
    public FullCredentials? Credentials { get; private set; }

    public event EventHandler<ChatMessage>? OnMessageReceived;
    public event EventHandler<Command>? OnCommandReceived;
    public event EventHandler? OnConnected;
    public event EventHandler<string>? OnDisconnected;
    public event EventHandler<string>? OnError;
    
    
    public TwitchClient(TwitchClientConfig? config = null) {
        _config = config ?? new TwitchClientConfig();
        
        _commandParser = new CommandParser(_config.CommandIdentifier);
        _messageQueue = new Queue<QueuedMessage>();
        
        _sendMessagesTaskCts = new CancellationTokenSource();
        
        InitializeWebSocket();
    }

    private async Task Initialize(FullCredentials credentials) {
        await Initialize(new ConnectionCredentials(credentials.Channel, credentials.Oauth, credentials.ChannelOauth));
    }
    
    public async Task Initialize(ConnectionCredentials credentials) {
        try {
            lock (_lock) {
                if (_initializing) {
                    return;
                }

                _initializing = true;
            }
            
            if (_websocket == null) return;
            
            var validateResponse = await Helix.ValidateOauth(credentials.Oauth, OnError);
            if (validateResponse == null) return;

            var channelInfo = await Helix.GetUserInfoByUsername(
                                                                credentials.Channel,
                                                                credentials.Oauth,
                                                                validateResponse.ClientId,
                                                                OnError
                                                                );
            if (channelInfo == null) return;
            
            Credentials = new FullCredentials(
                                              validateResponse.Login,
                                              credentials.Channel,
                                              credentials.Oauth,
                                              credentials.ChannelOauth,
                                              validateResponse.ClientId,
                                              validateResponse.UserId,
                                              channelInfo.Id
                                             );
    
            _ = SendMessagesRoutine(_sendMessagesTaskCts.Token);
            
            await _websocket.ConnectAsync();
            await SubscribeToChat();
            
            _globalBadges = await Badges.ListGlobalBadges(Credentials, OnError);
            _channelBadges = await Badges.ListChannelBadges(Credentials, OnError);

            lock (_lock) {
                _initializing = false;
            }
        }
        catch (Exception e) {
            OnError?.Invoke(this, $"Error while initializing. {e.Message}");
        }
    }

    private async Task SubscribeToChat() {
        try {
            if (_websocket == null 
             || Credentials == null) {
                OnError?.Invoke(this, "Not initialized.");
                return;
            }
            
            EventSubPayload? result;
            var attempts = 0;
            
            do {
                result = await EventSub.SubscribeToChannelChat(_websocket.SessionId, Credentials);
                if (result != null) break;
                
                ++attempts;

                OnError?.Invoke(this, $"Trying to subscribe to {Credentials.Channel}'s chat. Attempt: {attempts}");
                
                await Task.Delay(_config.AutoReconnectConfig.Cooldown);
            } while (attempts < _config.AutoReconnectConfig.Tries);
            
            if (result?.Id == null) {
                OnError?.Invoke(this, $"Failed to subscribe to {Credentials.Channel}'s chat.");
                return;
            }
            
            _websocket.SetSubscriptionId(result.Id);
            OnConnected?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex) {
            OnError?.Invoke(this, $"Error while subscribing to a chat. {ex.Message}");
        }
    }
    
    public async Task Reconnect() {
        if (Credentials == null) return;
        
        UnSubscribe();
        await Disconnect(reconnect: true);

        InitializeWebSocket();
        await Initialize(Credentials);
    }

    public async Task Disconnect() {
        await Disconnect(reconnect: false);
    }
    
    private async Task Disconnect(bool reconnect) {
        if (_websocket?.SubscriptionId == null
         || Credentials == null) {
            return;
        }
        
        UnSubscribe();
        await EventSub.EventSubUnSubscribe(
                                           _websocket.SubscriptionId, 
                                           Credentials, 
                                           OnError
                                        );
        await _websocket.DisconnectAsync();
        
        await _sendMessagesTaskCts.CancelAsync();
        _sendMessagesTaskCts = new CancellationTokenSource();
        
        if (reconnect) return;
        OnDisconnected?.Invoke(this, "Disconnected.");
    }
    
    public Task SendMessage(string message, string? replyId = null) {
        if (Credentials == null) {
            OnError?.Invoke(this, "Couldn't send a message. Not initialized.");
            return Task.CompletedTask;
        }

        lock (_lock) {
            _messageQueue.Enqueue(new QueuedMessage(message, replyId));
        }
        return Task.CompletedTask;
    }

    public Task SendWhisper(string message, string userId) {
        if (Credentials == null) {
            OnError?.Invoke(this, "Couldn't send a message. Not initialized.");
            return Task.CompletedTask;
        }

        lock (_lock) {
            _messageQueue.Enqueue(new QueuedMessage(message, userId, isWhisper: true));
        }
        return Task.CompletedTask;
    }
    
    public bool SetCommandIdentifier(char identifier) {
        return _commandParser.SetCommandIdentifier(identifier);
    }

    public async Task UpdateChannel(string username) {
        if (Credentials == null) return;
        
        var userId = await ValidateUser(username, OnError);
        if (userId == null) return;
        
        Credentials.UpdateChannel(username);
        Credentials.UpdateChannelId(userId);
        
        _channelBadges = await Badges.ListChannelBadges(Credentials, OnError);
    }
    
    public async Task UpdateOauth(string oauth) {
        var response = await Helix.ValidateOauth(oauth, OnError);
        if (response == null) return;
        
        Credentials?.UpdateOauth(oauth);
        Credentials?.UpdateUsername(response.Login);
        Credentials?.UpdateUserId(response.UserId);
        Credentials?.UpdateClientId(response.ClientId);
    }

    public async Task UpdateChannelOauth(string oauth) {
        var response = await Helix.ValidateOauth(oauth, OnError);
        if (response == null) return;
        
        Credentials?.UpdateChannelOauth(oauth);
        Credentials?.UpdateChannel(response.Login);
        Credentials?.UpdateChannelId(response.UserId);
    }

    private void AutoReconnect(object? sender, string message) {
        lock (_lock) {
            if (!_config.AutoReconnectConfig.AutoReconnect) {
                return;
            }
            
            Reconnect().Wait();
        }
    }
    
    private void InitializeWebSocket() {
        _websocket = new TwitchEventSubWebSocket();
        Subscribe();
    }
    
    private async Task<string?> ValidateUser(string username, EventHandler<string>? callback = null) {
        if (Credentials == null) {
            callback?.Invoke(this, "Cannot update a username before initializing a client.");
            return null;
        }
        
        var userId = await Helix.GetUserId(username, Credentials);
        if (userId == null) {
            callback?.Invoke(this, "User doesn't exist.");
        }

        return userId;
    }
    
    private void HandleChatMessage(object? sender, ChatMessageEvent? chatMessageEvent) {
        lock (_lock) {
            if (chatMessageEvent == null
             || Credentials == null
             || chatMessageEvent.UserId.Equals(Credentials.UserId)) return;

            OnMessageReceived?.Invoke(
                                      this,
                                      ChatMessage.Create(
                                                         chatMessageEvent,
                                                         _globalBadges,
                                                         _channelBadges
                                                        )
                                     );
        }
    }

    private void HandleChatCommand(object? sender, ChatMessage chatMessage) {
        var command = _commandParser.Parse(chatMessage);
        if (command == null) return;
        
        OnCommandReceived?.Invoke(
                                  this,
                                  command
                                  );
    }
    
    private void Subscribe() {
        if (_websocket == null) return;
        
        _websocket.OnWebSocketError += OnWebSocketError;
        
        _websocket.OnChatMessageReceived += HandleChatMessage;
        _websocket.OnConnectionClosed += OnConnectionClosed;
        
        OnError += AutoReconnect;
        OnMessageReceived += HandleChatCommand;
    }

    private void UnSubscribe() {
        if (_websocket == null) return;
        
        _websocket.OnWebSocketError -= OnWebSocketError;
        _websocket.OnChatMessageReceived -= HandleChatMessage;
        _websocket.OnConnectionClosed -= OnConnectionClosed;
        
        OnError -= AutoReconnect;
        OnMessageReceived -= HandleChatCommand;
    }

    private void OnWebSocketError(object? sender, string message) {
        lock (_lock) {
            OnError?.Invoke(sender, message);
        }
    }
    
    private void OnConnectionClosed(object? sender, string message) {
        lock (_lock) {
            OnDisconnected?.Invoke(sender, message);
        }
    }

    private Task SendMessagesRoutine(CancellationToken cancellationToken = default) {
        if (Credentials == null) return Task.CompletedTask;
        
        return Task.Run(async () => {
                            try {
                                while (true) {
                                    if (cancellationToken.IsCancellationRequested) {
                                        return;
                                    }

                                    await Task.Delay(_sendMessageDelay, cancellationToken);

                                    QueuedMessage message;
                                    lock (_lock) {
                                        if (!_messageQueue.TryDequeue(out message)) {
                                            continue;
                                        }
                                    }

                                    if (string.IsNullOrEmpty(message.ReplyId)) {
                                        await Helix.SendMessage(
                                                                message.Message,
                                                                Credentials,
                                                                OnError
                                                               );
                                    }
                                    else {
                                        if (message.IsWhisper) {
                                            await Helix.SendWhisper(
                                                                    message.ReplyId,
                                                                    message.Message,
                                                                    Credentials,
                                                                    OnError
                                                                   );
                                        }
                                        else {
                                            await Helix.SendReply(
                                                                  message.Message,
                                                                  message.ReplyId,
                                                                  Credentials,
                                                                  OnError
                                                                 );
                                        }
                                    }
                                }
                            }
                            catch (TaskCanceledException) { }
                            catch (Exception e) {
                                OnError?.Invoke(this, $"Exception while sending chat messages. {e.Message}");
                            }
                        }, cancellationToken);
    }
}