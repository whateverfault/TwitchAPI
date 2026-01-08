using Newtonsoft.Json;

namespace TwitchAPI.api.data.responses.SendMessage;

public class DropReason {
    [JsonProperty("code")]
    public string Code { get; private set; }
    
    [JsonProperty("message")]
    public string Message { get; private set; }
    
    
    [JsonConstructor]
    public DropReason(
        [JsonProperty("code")] string code,
        [JsonProperty("message")] string message) {
        Code = code;
        Message = message;
    }
}