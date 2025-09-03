using Newtonsoft.Json;

namespace TwitchAPI.helix.data.responses.SendMessage;

public class SendMessageResponse {
    [JsonProperty("data")]
    public SentMessage[] Data { get; private set; }
    
    
    [JsonConstructor]
    public SendMessageResponse(
        [JsonProperty("data")] SentMessage[] data) {
        Data = data;
    }
}