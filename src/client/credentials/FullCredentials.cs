using Newtonsoft.Json;

namespace TwitchAPI.client.credentials;

public class FullCredentials {
    [JsonProperty("user")]
    public Credentials Bot;

    [JsonProperty("broadcaster")]
    public Credentials Broadcaster;
    

    public FullCredentials() {
        Bot = new Credentials();
        Broadcaster = new Credentials();
    }
    
    public FullCredentials(
        [JsonProperty("bot")] Credentials bot,
        [JsonProperty("broadcaster")] Credentials broadcaster) {
        Bot = bot;
        Broadcaster = broadcaster;
    }
}