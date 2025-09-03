using Newtonsoft.Json;
using TwitchAPI.helix.data.requests;

namespace TwitchAPI.helix.data.responses;

public class RewardCreationResponse {
    [JsonProperty("data")]
    public List<RewardData> Data { get; set; } = null!;
}
