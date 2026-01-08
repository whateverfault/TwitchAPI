using Newtonsoft.Json;

namespace TwitchAPI.api.data.responses;

public class ValidateResponse {
    [JsonProperty("client_id")]
    public string ClientId { get; private set; }
    
    [JsonProperty("login")]
    public string Login { get; private set; }
    
    [JsonProperty("scopes")]
    public string[] Scopes { get; private set; }
    
    [JsonProperty("user_id")]
    public string UserId { get; private set; }
    
    [JsonProperty("expires_in")]
    public long ExpiresIn { get; private set; }
    
    
    public ValidateResponse(
        [JsonProperty("client_id")] string clientId,
        [JsonProperty("login")] string login,
        [JsonProperty("scopes")] string[] scopes,
        [JsonProperty("user_id")] string userId,
        [JsonProperty("expires_in")] long expiresIn) {
        ClientId = clientId;
        Login = login;
        Scopes = scopes;
        UserId = userId;
        ExpiresIn = expiresIn;
    }
}