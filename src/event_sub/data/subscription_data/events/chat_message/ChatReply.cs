using Newtonsoft.Json;

namespace TwitchAPI.event_sub.data.subscription_data.events.chat_message;

public class ChatReply {
    [JsonProperty("parent_message_id")]
    public string MessageId { get; set; }
    
    [JsonProperty("parent_message_body")]
    public string Text { get; set; }
    
    [JsonProperty("parent_user_id")]
    public string UserId { get; set; }
    
    [JsonProperty("parent_user_login")]
    public string Username { get; set; }
    
    
    [JsonConstructor]
    public ChatReply(
        [JsonProperty("parent_message_id")] string messageId,
        [JsonProperty("parent_message_body")] string text,
        [JsonProperty("parent_user_id")] string userId,
        [JsonProperty("parent_user_login")] string username) {
        MessageId = messageId;
        Text = text;
        UserId = userId;
        Username = username;
    }
}