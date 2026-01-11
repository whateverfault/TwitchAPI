using Newtonsoft.Json;

namespace TwitchAPI.event_sub.data.subscription_data.events.channel_points;

public sealed class Reward {
    [JsonProperty("id")]
    public string Id { get; set; } = null!;

    [JsonProperty("title")]
    public string Title { get; set; } = null!;

    [JsonProperty("prompt")]
    public string Prompt { get; set; } = null!;

    [JsonProperty("cost")]
    public int Cost { get; set; }
}
