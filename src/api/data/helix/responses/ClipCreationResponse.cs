using Newtonsoft.Json;
using TwitchAPI.api.data.requests;

namespace TwitchAPI.api.data.responses;

public class ClipCreationResponse {
    [JsonProperty("data")]
    public List<ClipData>? Data { get; set; }
}