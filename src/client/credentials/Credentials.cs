using Newtonsoft.Json;

namespace TwitchAPI.client.credentials;

public class Credentials {
    [JsonProperty("username")]
    public string DisplayName { get; private set; }
    
    [JsonProperty("login")]
    public string Login { get; private set; }

    [JsonProperty("user_id")]
    public string UserId { get; private set; }
    
    [JsonProperty("oauth")]
    public string Oauth { get; private set; }
    
    [JsonProperty("client_id")]
    public string ClientId { get; private set; }
    
    
    public Credentials() {
        DisplayName = string.Empty;
        Login = string.Empty;
        UserId = string.Empty;
        Oauth = string.Empty;
        ClientId = string.Empty;
    }
    
    public Credentials(
        [JsonProperty("username")] string displayName,
        [JsonProperty("login")] string login,
        [JsonProperty("user_id")] string userId,
        [JsonProperty("oauth")] string oauth,
        [JsonProperty("client_id")] string clientId) {
        DisplayName = displayName;
        Login = login;
        Oauth = oauth;
        ClientId = clientId;
        UserId = userId;
    }
    
    public void UpdateDisplayName(string displayName) {
        DisplayName = displayName;
    }
    
    public void UpdateLogin(string login) {
        Login = login;
    }
    
    public void UpdateUserId(string userId) {
        UserId = userId;
    }
    
    public void UpdateOauth(string oauth) {
        Oauth = oauth;
    }
    
    public void UpdateClientId(string clientId) {
        ClientId = clientId;
    }
}