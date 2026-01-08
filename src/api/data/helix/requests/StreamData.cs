using Newtonsoft.Json;

namespace TwitchAPI.api.data.requests;

public class StreamData {
    [JsonProperty("title")]
    public string Title { get; set; } = null!;
}