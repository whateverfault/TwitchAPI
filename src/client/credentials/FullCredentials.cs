using Newtonsoft.Json;

namespace TwitchAPI.client.credentials;

public class FullCredentials {
    [JsonProperty("username")]
    public string Username { get; private set; }

    [JsonProperty("channel")]
    public string Channel { get; private set; }

    [JsonProperty("oauth")]
    public string Oauth { get; private set; }
    
    [JsonProperty("channel_oauth")]
    public string? ChannelOauth { get; private set; }
    
    [JsonProperty("client_id")]
    public string ClientId { get; private set; }
    
    [JsonProperty("user_id")]
    public string UserId { get; private set; }

    [JsonProperty("channel_id")]
    public string ChannelId { get; private set; }


    public FullCredentials() {
        Username = string.Empty;
        Channel = string.Empty;
        Oauth = string.Empty;
        ChannelOauth = string.Empty;
        ClientId = string.Empty;
        UserId = string.Empty;
        ChannelId = string.Empty;
    }
    
    public FullCredentials(
        [JsonProperty("username")] string username,
        [JsonProperty("channel")] string channel,
        [JsonProperty("oauth")] string oauth,
        [JsonProperty("channel_oauth")] string? channelOauth,
        [JsonProperty("client_id")] string clientId,
        [JsonProperty("user_id")] string userId,
        [JsonProperty("channel_id")] string channelId) {
        Username = username;
        Channel = channel;
        Oauth = oauth;
        ChannelOauth = channelOauth;
        ClientId = clientId;
        UserId = userId;
        ChannelId = channelId;
    }

    public void UpdateUsername(string username) {
        Username = username;
    }
    
    public void UpdateChannel(string channel) {
        Channel = channel;
    }
    
    public void UpdateOauth(string oauth) {
        Oauth = oauth;
    }
    
    public void UpdateChannelOauth(string oauth) {
        ChannelOauth = oauth;
    }
    
    public void UpdateClientId(string clientId) {
        ClientId = clientId;
    }
    
    public void UpdateUserId(string userId) {
        UserId = userId;
    }
    
    public void UpdateChannelId(string channelId) {
        ChannelId = channelId;
    }
}