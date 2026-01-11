using Newtonsoft.Json;

namespace TwitchAPI.event_sub.data.subscription_data.session;

public class SessionWelcomePayload {
    [JsonProperty("session")]
    public SessionData Session { get; set; }
    
    
    [JsonConstructor]
    public SessionWelcomePayload(
        [JsonProperty("session")] SessionData session) {
        Session = session;
    }
}