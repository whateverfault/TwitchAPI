using Newtonsoft.Json;

namespace TwitchAPI.api.data.requests;

public class SendReplyPayload {
    [JsonProperty("broadcaster_id")]
    public string BroadcasterId { get; }
    
    [JsonProperty("sender_id")]
    public string SenderId { get; }
    
    [JsonProperty("message")]
    public string Message { get; }
    
    [JsonProperty("reply_parent_message_id")]
    public string ReplyId { get; }


    public SendReplyPayload(
        string broadcasterId,
        string senderId,
        string message,
        string replyId) {
        BroadcasterId = broadcasterId;
        SenderId = senderId;
        Message = message;
        ReplyId = replyId;
    }
}