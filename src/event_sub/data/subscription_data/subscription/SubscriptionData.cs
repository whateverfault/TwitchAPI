using Newtonsoft.Json;

namespace TwitchAPI.event_sub.data.subscription_data.subscription;

public class SubscriptionData {
    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonProperty("status")]
    public string Status { get; set; }

    [JsonProperty("type")]
    public string Type { get; set; }
    
    
    [JsonConstructor]
    public SubscriptionData(
        [JsonProperty("id")] string id,
        [JsonProperty("status")] string status,
        [JsonProperty("type")]string type) {
        Id = id;
        Status = status;
        Type = type;
    }
}