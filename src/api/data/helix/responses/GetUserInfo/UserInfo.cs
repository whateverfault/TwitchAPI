using Newtonsoft.Json;

namespace TwitchAPI.api.data.responses.GetUserInfo;

public class UserInfo {
    [JsonProperty("id")]
    public string UserId { get; private set; }
    
    [JsonProperty("login")]
    public string Login { get; private set; }
    
    [JsonProperty("display_name")]
    public string DisplayName { get; private set; }
    
    [JsonProperty("type")]
    public string Type { get; private set; }
    
    [JsonProperty("broadcaster_type")]
    public string BroadcasterType { get; private set; }
    
    [JsonProperty("description")]
    public string Description { get; private set; }
    
    
    public UserInfo(
        [JsonProperty("id")] string userId,
        [JsonProperty("login")] string login,
        [JsonProperty("display_name")] string displayName,
        [JsonProperty("type")] string type,
        [JsonProperty("broadcaster_type")] string broadcasterType,
        [JsonProperty("description")] string description) {
        UserId = userId;
        Login = login;
        DisplayName = displayName;
        Type = type;
        BroadcasterType = broadcasterType;
        Description = description;
    }

    public string GetDisplayName() {
        return DisplayName;
    }

    public void SetDisplayName(string value) {
        DisplayName = value;
    }
}