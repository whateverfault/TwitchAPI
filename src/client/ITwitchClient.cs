using TwitchAPI.client.commands.data;
using TwitchAPI.client.credentials;
using TwitchAPI.client.data;

namespace TwitchAPI.client;

public interface ITwitchClient {
    public FullCredentials? Credentials { get; }

    public event EventHandler<RewardRedemption>? OnRewardRedeemed;
    public event EventHandler<ChatMessage>? OnMessageReceived;
    public event EventHandler<Command>? OnCommandReceived;
    
    public event EventHandler? OnConnected;
    public event EventHandler<string>? OnDisconnected;
    public event EventHandler<string>? OnError;


    public Task<FullCredentials?> GetFullCredentials(ConnectionCredentials credentials);

    public Task Initialize(FullCredentials credentials);
    public Task Initialize(ConnectionCredentials credentials);

    public Task Reconnect();
    
    public Task Disconnect();
    
    public Task SendMessage(string message, string? replyId = null);
    public Task SendWhisper(string message, string userId);

    public bool SetCommandIdentifier(char identifier);

    public Task UpdateBroadcaster(string username);

    public Task UpdateOauth(string oauth);

    public Task UpdateBroadcasterOauth(string oauth);
}