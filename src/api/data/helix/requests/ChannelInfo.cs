using Newtonsoft.Json;

namespace TwitchAPI.api.data.requests;

public class ChannelInfo {
    [JsonProperty("broadcaster_id")]
    public string BroadcasterId { get; set; }
    [JsonProperty("broadcaster_name")]
    public string BroadcasterName { get; set; }
    [JsonProperty("title")]
    public string Title { get; set; }
    [JsonProperty("game_id")]
    public string GameId { get; set; }
    [JsonProperty("game_name")]
    public string GameName { get; set; }
    [JsonProperty(PropertyName = "delay")]
    public int Delay { get; set; }
    
    
    public ChannelInfo(
        [JsonProperty("broadcaster_id")] string broadcasterId,
        [JsonProperty("broadcaster_name")] string broadcasterName,
        [JsonProperty("title")] string title,
        [JsonProperty("game_id")] string gameId,
        [JsonProperty("game_name")] string gameName,
        [JsonProperty(PropertyName = "delay")] int delay
        ) {
        BroadcasterId = broadcasterId;
        BroadcasterName = broadcasterName;
        Title = title;
        GameId = gameId;
        GameName = gameName;
        Delay = delay;
    }
}