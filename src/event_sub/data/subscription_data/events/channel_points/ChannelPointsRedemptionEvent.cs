using Newtonsoft.Json;

namespace TwitchAPI.event_sub.data.subscription_data.events.channel_points;

public sealed class ChannelPointsRedemptionEvent {
    [JsonProperty("id")]
    public string Id { get; set; } = null!;

    [JsonProperty("broadcaster_user_id")]
    public string BroadcasterUserId { get; set; } = null!;

    [JsonProperty("broadcaster_user_login")]
    public string BroadcasterUserLogin { get; set; } = null!;

    [JsonProperty("broadcaster_user_name")]
    public string BroadcasterUserName { get; set; } = null!;

    [JsonProperty("user_id")]
    public string UserId { get; set; } = null!;

    [JsonProperty("user_login")]
    public string UserLogin { get; set; } = null!;

    [JsonProperty("user_name")]
    public string UserName { get; set; } = null!;

    [JsonProperty("user_input")]
    public string UserInput { get; set; } = string.Empty;

    [JsonProperty("status")]
    public string Status { get; set; } = null!;

    [JsonProperty("redeemed_at")]
    public DateTime RedeemedAt { get; set; }

    [JsonProperty("reward")]
    public Reward Reward { get; set; } = null!;
}