using Newtonsoft.Json;

namespace TwitchAPI.event_sub.subscription_data.session;

public class SessionMetadata {
    [JsonProperty("message_id")]
    public string MessageId { get; set; }

    [JsonProperty("message_type")]
    public string MessageType { get; set; }

    [JsonProperty("subscription_type")]
    public string SubscriptionType { get; set; }
    
    
    [JsonConstructor]
    public SessionMetadata(
        [JsonProperty("message_id")] string messageId,
        [JsonProperty("message_type")] string messageType,
        [JsonProperty("subscription_type")] string subscriptionType) {
        MessageId = messageId;
        MessageType = messageType;
        SubscriptionType = subscriptionType;
    }
}