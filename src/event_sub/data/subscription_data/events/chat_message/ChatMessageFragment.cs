using Newtonsoft.Json;

namespace TwitchAPI.event_sub.data.subscription_data.events.chat_message;

public class ChatMessageFragment {
    [JsonProperty("type")]
    public string Type { get; set; }
    
    [JsonProperty("text")]
    public string Text { get; set; }
    
    
    [JsonConstructor]
    public ChatMessageFragment(
        [JsonProperty("type")] string type,
        [JsonProperty("text")] string text) {
        Type = type;
        Text = text;
    }
}