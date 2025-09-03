using Newtonsoft.Json;

namespace TwitchAPI.event_sub.subscription_data.session;

public class SessionData {
    [JsonProperty("id")]
    public string? Id { get; set; }

    [JsonProperty("status")]
    public string Status { get; set; }

    [JsonProperty("keepalive_timeout_seconds")]
    public int? KeepaliveTimeoutSeconds { get; set; }
    
    
    [JsonConstructor]
    public SessionData(
        [JsonProperty("status")] string status,
        [JsonProperty("id")] string? id = null,
        [JsonProperty("keepalive_timeout_seconds")] int? timeout = null) {
        Id = id;
        Status = status;
        KeepaliveTimeoutSeconds = timeout;
    }
}