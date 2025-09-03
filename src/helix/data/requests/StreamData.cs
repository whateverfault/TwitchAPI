using Newtonsoft.Json;

namespace TwitchAPI.helix.data.requests;

public class StreamData {
    [JsonProperty("title")]
    public string Title { get; set; } = null!;
}