using Newtonsoft.Json;

namespace TwitchAPI.event_sub.subscription_data.events.chat_message;

public class BadgeInfo {
    [JsonProperty("set_id")]
    public string Name { get; set; }
    
    [JsonProperty("id")]
    public string Version { get; set; }
    
    
    [JsonConstructor]
    public BadgeInfo(
        [JsonProperty("set_id")] string name,
        [JsonProperty("id")] string version) {
        Name = name;
        Version = version;
    }
}