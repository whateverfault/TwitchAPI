using Newtonsoft.Json;

namespace TwitchAPI.api.data.badges.data.badge;

public class Badge {
    [JsonProperty("set_id")]
    public string Name { get; private set; }
    
    [JsonProperty("versions")]
    public BadgeVersion[] Versions { get; private set; }
    
    
    public Badge(
        [JsonProperty("set_id")] string name,
        [JsonProperty("versions")] BadgeVersion[] versions) {
        Name = name;
        Versions = versions;
    }
}