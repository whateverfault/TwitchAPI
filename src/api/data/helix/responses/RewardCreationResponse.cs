using Newtonsoft.Json;
using TwitchAPI.api.data.requests;

namespace TwitchAPI.api.data.responses;

public class RewardCreationResponse {
    [JsonProperty("data")]
    public List<RewardData> Data { get; set; } = null!;
}
