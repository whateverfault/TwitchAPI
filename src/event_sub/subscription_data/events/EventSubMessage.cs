using Newtonsoft.Json;

namespace TwitchAPI.event_sub.subscription_data.events;

public class EventSubMessage<TMetadata, TPayload>  {
    [JsonProperty("metadata")]
    public TMetadata Metadata { get; set; }

    [JsonProperty("payload")]
    public TPayload Payload { get; set; }
    
    
    [JsonConstructor]
    public EventSubMessage(
        [JsonProperty("metadata")] TMetadata metadata,
        [JsonProperty("payload")] TPayload payload) {
        Metadata = metadata;
        Payload = payload;
    }
}