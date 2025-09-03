using Newtonsoft.Json;

namespace TwitchAPI.client.credentials;

public class ConnectionCredentials {
    [JsonProperty("channel")]
    public string Channel { get; }
    
    [JsonProperty("oauth")]
    public string Oauth { get; }
    
    [JsonProperty("channel_oauth")]
    public string? ChannelOauth { get; }


    public ConnectionCredentials() {
        Channel = string.Empty;
        Oauth = string.Empty;
        ChannelOauth = string.Empty;
    }
    
    [JsonConstructor]
    public ConnectionCredentials(
        [JsonProperty("channel")] string channel,
        [JsonProperty("oauth")] string oauth,
        [JsonProperty("channel_oauth")] string? channelOauth = null) {
        Channel = channel;
        Oauth = oauth;
        ChannelOauth = channelOauth;
    }

    public static ConnectionCredentials FromFullCredentials(FullCredentials credentials) {
        return new ConnectionCredentials(credentials.Channel, credentials.Oauth, credentials.ChannelOauth);
    }
}