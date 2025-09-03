using Newtonsoft.Json;
using TwitchAPI.helix.data.requests;

namespace TwitchAPI.helix.data.responses;

public class ClipCreationResponse {
    [JsonProperty("data")]
    public List<ClipData>? Data { get; set; }
}