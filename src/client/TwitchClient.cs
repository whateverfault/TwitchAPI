using TwitchAPI.client.commands;
using TwitchAPI.client.commands.data;
using TwitchAPI.client.credentials;
using TwitchAPI.client.data.badges;
using TwitchAPI.client.data.badges.data.badge;
using TwitchAPI.event_sub;
using TwitchAPI.event_sub.subscription_data.events.chat_message;
using TwitchAPI.helix;
using ChatMessage = TwitchAPI.client.data.ChatMessage;

namespace TwitchAPI.client;

public enum LogLevel {
    Info,
    Error,
    Warning,
}

public class TwitchClient : ITwitchClient {
    private readonly CommandParser _commandParser;
    private TwitchEventSubWebSocket? _websocket;

    private Badge[]? _globalBadges;
    private Badge[]? _channelBadges;
    
    public FullCredentials? Credentials { get; private set; }

    public event EventHandler<ChatMessage>? OnMessageReceived;
    public event EventHandler<Command>? OnCommandReceived;
    public event EventHandler? OnConnected;
    public event EventHandler<string>? OnDisconnected;
    public event EventHandler<string>? OnError;
    
    
    public TwitchClient(char? commandIdentifier = null) {
        _commandParser = new CommandParser(commandIdentifier);
        InitializeWebSocket();
    }

    private async Task Initialize(FullCredentials credentials) {
        await Initialize(new ConnectionCredentials(credentials.Channel, credentials.Oauth, credentials.ChannelOauth));
    }
    
    public async Task Initialize(ConnectionCredentials credentials) {
        try {
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
    
            await _websocket.ConnectAsync();
            _websocket.OnSessionStarted += SubscribeToChat;
            
            _globalBadges = await Badges.ListGlobalBadges(Credentials, OnError);
            _channelBadges = await Badges.ListChannelBadges(Credentials, OnError);
            
            return;
        
            async void SubscribeToChat(object? sender, EventArgs e) {
                try {
                    var result = await EventSub.SubscribeToChannelChat(_websocket.SessionId, Credentials, OnError);
                    if (result?.Id == null) return;
                
                    _websocket.SetSubscriptionId(result.Id);
                    _websocket.OnSessionStarted -= SubscribeToChat;
                    OnConnected?.Invoke(this, EventArgs.Empty);
                }
                catch (Exception ex) {
                    OnError?.Invoke(this, $"Error while subscribing to a chat. {ex.Message}");
                }
            }
        }
        catch (Exception e) {
            OnError?.Invoke(this, $"Error while initializing. {e.Message}");
        }
    }

    public async Task Reconnect() {
        if (Credentials == null) return;
        
        UnSubscribe();
        await Disconnect(true);

        InitializeWebSocket();
        await Initialize(Credentials);
    }

    public async Task Disconnect() {
        await Disconnect(false);
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
        
        if (reconnect) return;
        OnDisconnected?.Invoke(this, "Disconnected.");
    }
    
    public async Task SendMessage(string message) {
        if (Credentials == null) {
            OnError?.Invoke(this, "Couldn't send a message. Not initialized.");
            return;
        }
        
        await Helix.SendMessage(
                                message, 
                                Credentials, 
                                OnError
                                );
    }
    
    public async Task SendReply(string replyId, string message) {
        if (Credentials == null) {
            OnError?.Invoke(this, "Couldn't send a message. Not initialized.");
            return;
        }
        
        await Helix.SendReply(
                                 message,
                                 replyId,
                                 Credentials,
                                 OnError
                                 );
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
        
        OnMessageReceived += HandleChatCommand;
    }

    private void UnSubscribe() {
        if (_websocket == null) return;
        
        _websocket.OnWebSocketError -= OnWebSocketError;
        _websocket.OnChatMessageReceived -= HandleChatMessage;
        _websocket.OnConnectionClosed -= OnConnectionClosed;
        
        OnMessageReceived -= HandleChatCommand;
    }

    private void OnWebSocketError(object? sender, string message) {
        OnError?.Invoke(sender, message);
    }
    
    private void OnConnectionClosed(object? sender, string message) {
        OnDisconnected?.Invoke(sender, message);
    }
}