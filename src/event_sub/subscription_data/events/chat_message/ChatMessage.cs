using Newtonsoft.Json;

namespace TwitchAPI.event_sub.subscription_data.events.chat_message;

public class ChatMessage {
    [JsonProperty("text")]
    public string Text { get; set; }
    
    [JsonProperty("fragments")]
    public ChatMessageFragment[] Fragments { get; set; }
    
    
    [JsonConstructor]
    public ChatMessage(
        [JsonProperty("text")] string text,
        [JsonProperty("fragments")] ChatMessageFragment[] fragments) {
        Text = text;
        Fragments = fragments;
    }
}