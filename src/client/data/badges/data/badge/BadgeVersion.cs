using Newtonsoft.Json;

namespace TwitchAPI.client.data.badges.data.badge;

public class BadgeVersion {
    [JsonProperty("title")]
    public string Title { get; private set; }
    
    [JsonProperty("id")]
    public string Id { get; private set; }
    
    [JsonProperty("image_url_1x")]
    public string ImageX1Url { get; private set; }
    
    [JsonProperty("image_url_2x")]
    public string ImageX2Url { get; private set; }
    
    [JsonProperty("image_url_4x")]
    public string ImageX4Url { get; private set; }
    
    
    public BadgeVersion(
        [JsonProperty("title")] string title,
        [JsonProperty("id")] string id,
        [JsonProperty("image_url_1x")] string imageX1Url,
        [JsonProperty("image_url_2x")] string imageX2Url,
        [JsonProperty("image_url_4x")] string imageX4Url) {
        Title = title;
        Id = id;
        ImageX1Url = imageX1Url;
        ImageX2Url = imageX2Url;
        ImageX4Url = imageX4Url;
    }
}