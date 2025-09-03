using Newtonsoft.Json;

namespace TwitchAPI.helix.data.requests;

public class FollowData {
    [JsonProperty("followed_at")]
    public DateTime FollowedAt { get; set; }
}