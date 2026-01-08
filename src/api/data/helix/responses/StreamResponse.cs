using Newtonsoft.Json;
using TwitchAPI.api.data.requests;

namespace TwitchAPI.api.data.responses;

public class StreamResponse {
    [JsonProperty("data")]
    public List<StreamData?> Data { get; set; } = null!;
}