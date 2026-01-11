using Newtonsoft.Json;

namespace TwitchAPI.client.credentials;

public class ConnectionCredentials {
    [JsonProperty("channel")]
    public string Channel { get; }
    
    [JsonProperty("oauth")]
    public string Oauth { get; }
    
    [JsonProperty("broadcaster_oauth")]
    public string BroadcasterOauth { get; }


    public ConnectionCredentials() {
        Channel = string.Empty;
        Oauth = string.Empty;
        BroadcasterOauth = string.Empty;
    }
    
    [JsonConstructor]
    public ConnectionCredentials(
        [JsonProperty("channel")] string channel,
        [JsonProperty("oauth")] string oauth,
        [JsonProperty("broadcaster_oauth")] string broadcasterOauth) {
        Channel = channel;
        Oauth = oauth;
        BroadcasterOauth = broadcasterOauth;
    }

    public static ConnectionCredentials FromFullCredentials(FullCredentials credentials) {
        return new ConnectionCredentials(credentials.Broadcaster.DisplayName, credentials.Bot.Oauth, credentials.Broadcaster.Oauth);
    }
}