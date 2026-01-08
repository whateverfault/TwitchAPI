using Newtonsoft.Json;

namespace TwitchAPI.api.data.requests;

public class FollowData {
    [JsonProperty("followed_at")]
    public DateTime FollowedAt { get; set; }
}