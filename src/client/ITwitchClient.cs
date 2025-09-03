using TwitchAPI.client.commands.data;
using TwitchAPI.client.credentials;
using TwitchAPI.client.data;

namespace TwitchAPI.client;

public interface ITwitchClient {
    public FullCredentials? Credentials { get; }

    public event EventHandler<ChatMessage>? OnMessageReceived;
    public event EventHandler<Command>? OnCommandReceived;
    public event EventHandler? OnConnected;
    public event EventHandler<string>? OnDisconnected;
    public event EventHandler<string>? OnError;

    
    public Task Initialize(ConnectionCredentials credentials);

    public Task Reconnect();
    
    public Task Disconnect();
    
    public Task SendMessage(string message);
    
    public Task SendReply(string replyId, string message);

    public bool SetCommandIdentifier(char identifier);

    public Task UpdateChannel(string username);

    public Task UpdateOauth(string oauth);

    public Task UpdateChannelOauth(string oauth);
}