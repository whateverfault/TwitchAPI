using Newtonsoft.Json;
using TwitchAPI.helix.data.requests.chat_subscription;

namespace TwitchAPI.event_sub.subscription_data.subscription;

public class EventSubData {
    [JsonProperty("data")]
    public EventSubPayload[] Data { get; set; } 
    
    
    [JsonConstructor]
    public EventSubData(
        [JsonProperty("data")] EventSubPayload[] data) {
        Data = data;
    }
}