using Newtonsoft.Json;

namespace TwitchAPI.event_sub.subscription_data.session.reconnect;

public class ReconnectSession {
    [JsonProperty("id")]
    public string Id { get; set; }
    
    [JsonProperty("status")]
    public string Status { get; set; }
    
    [JsonProperty("keepalive_timeout_seconds")]
    public int? KeepaliveTimeoutSeconds { get; set; }
    
    [JsonProperty("reconnect_url")]
    public string ReconnectUrl { get; set; }
    
    [JsonProperty("connected_at")]
    public DateTime ConnectedAt { get; set; }


    public ReconnectSession() {
        Id = string.Empty;
        Status = string.Empty;
        ReconnectUrl = string.Empty;
    }
    
    public ReconnectSession(
        string id,
        string status,
        int? keepaliveTimeoutSeconds,
        string reconnectUrl,
        DateTime connectedAt) {
        Id = id;
        Status = status;
        KeepaliveTimeoutSeconds = keepaliveTimeoutSeconds;
        ReconnectUrl = reconnectUrl;
        ConnectedAt = connectedAt;
    }
}