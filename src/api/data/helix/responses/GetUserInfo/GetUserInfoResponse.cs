using Newtonsoft.Json;

namespace TwitchAPI.api.data.responses.GetUserInfo;

public class GetUserInfoResponse {
    [JsonProperty("data")]
    public UserInfo[] Data { get; private set; }
    
    
    [JsonConstructor]
    public GetUserInfoResponse(
        [JsonProperty("data")] UserInfo[] data) {
        Data = data;
    }
}