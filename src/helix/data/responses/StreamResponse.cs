using Newtonsoft.Json;
using TwitchAPI.helix.data.requests;

namespace TwitchAPI.helix.data.responses;

public class StreamResponse {
    [JsonProperty("data")]
    public List<StreamData?> Data { get; set; } = null!;
}