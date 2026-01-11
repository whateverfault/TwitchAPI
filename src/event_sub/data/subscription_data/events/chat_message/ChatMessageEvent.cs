using Newtonsoft.Json;

namespace TwitchAPI.event_sub.data.subscription_data.events.chat_message;

public class ChatMessageEvent {
    [JsonProperty("broadcaster_user_id")]
    public string ChannelId { get; private set; }
    
    [JsonProperty("broadcaster_user_name")]
    public string Channel { get; private set; }

    [JsonProperty("chatter_user_id")]
    public string UserId { get; private set; }
    
    [JsonProperty("chatter_user_name")]
    public string User { get; private set; }
    
    [JsonProperty("message_id")]
    public string MessageId { get; private set; }
    
    [JsonProperty("reply")]
    public ChatReply? Reply { get; private set; }
    
    [JsonProperty("channel_points_custom_reward_id")]
    public string? RewardId { get; private set; }
    
    [JsonProperty("message")]
    public ChatMessage Message { get; private set; }
    
    [JsonProperty("color")]
    public string Color { get; private set; }
    
    [JsonProperty("badges")]
    public BadgeInfo[] Badges { get; private set; }
    
    
    public ChatMessageEvent(
        [JsonProperty("broadcaster_user_id")] string channelId,
        [JsonProperty("broadcaster_user_name")] string channel,
        [JsonProperty("chatter_user_id")] string userId,
        [JsonProperty("chatter_user_name")] string user,
        [JsonProperty("message_id")] string messageId,
        [JsonProperty("reply")] ChatReply? reply,
        [JsonProperty("channel_points_custom_reward_id")] string? rewardId,
        [JsonProperty("message")] ChatMessage message,
        [JsonProperty("color")] string color,
        [JsonProperty("badges")] BadgeInfo[] badges) {
        ChannelId = channelId;
        Channel = channel;
        UserId = userId;
        User = user;
        MessageId = messageId;
        Reply = reply;
        RewardId = rewardId;
        Message = message;
        Color = color;
        Badges = badges;
    }
}