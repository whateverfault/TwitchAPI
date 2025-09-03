using Newtonsoft.Json;

namespace TwitchAPI.helix.data.responses.SendMessage;

public class SentMessage {
    [JsonProperty("message_id")]
    public string MessageId { get; private set; }
    
    [JsonProperty("is_sent")]
    public bool IsSent { get; private set; }
    
    [JsonProperty("drop_reason")]
    public DropReason? DropReason { get; private set; }
    
    
    public SentMessage(
        [JsonProperty("message_id")] string messageId,
        [JsonProperty("is_sent")] bool isSent,
        [JsonProperty("drop_reason")] DropReason? dropReason) {
        MessageId = messageId;
        IsSent = isSent;
        DropReason = dropReason;
    }
}