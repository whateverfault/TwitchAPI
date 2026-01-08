using Newtonsoft.Json;

namespace TwitchAPI.api.data.requests;

public class ClipData {
    [JsonProperty("id")]
    public string? Id { get; set; }
    
    [JsonProperty("edit_url")]
    public string? EditUrl { get; set; }
}