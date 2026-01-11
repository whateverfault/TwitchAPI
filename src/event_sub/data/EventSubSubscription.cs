using Newtonsoft.Json;
using TwitchAPI.client.credentials;
using TwitchAPI.event_sub.data.subscription_data.events;
using TwitchAPI.event_sub.data.subscription_data.events.channel_points;
using TwitchAPI.event_sub.data.subscription_data.events.chat_message;
using TwitchAPI.event_sub.data.subscription_data.session;

namespace TwitchAPI.event_sub.data;

public sealed class EventSubSubscription {
    public required string Type { get; init; }
    public required Func<string, FullCredentials, EventHandler<string>?, Task> SubscribeAsync { get; init; }
    public required Action<string> Dispatch { get; init; }

    
    public static EventSubSubscription CreateChat(
        TwitchEventSubWebSocket ws,
        EventSub eventSub) {
        return new EventSubSubscription {
                                            Type = "channel.chat.message",
                                            SubscribeAsync = async (sessionId, creds, errorCallback) => {
                                                                 await eventSub.SubscribeToChannelChat(sessionId, creds, errorCallback);
                                                             },
                                            Dispatch = json => {
                                                           try {
                                                               var msg = JsonConvert
                                                                  .DeserializeObject<EventSubMessage<SessionMetadata,
                                                                       EventSubMessagePayload<ChatMessageEvent>>>(json);
                                                               if (msg == null)
                                                                   return;

                                                               ws.RaiseChatMessage(msg.Payload.Event);
                                                           }
                                                           catch { return; }
                                                       },
                                        };
    }
    
    public static EventSubSubscription CreateRedemptions(
        TwitchEventSubWebSocket ws, 
        EventSub eventSub) {
        return new EventSubSubscription {
                                            Type = "channel.channel_points_custom_reward_redemption.add",
                                            SubscribeAsync = async (sessionId, creds, errorCallback) => {
                                                                 await eventSub.SubscribeToChannelRedemptions(sessionId, creds, errorCallback);
                                                             },
                                            Dispatch = json => {
                                                           try {
                                                               var msg = JsonConvert.DeserializeObject<
                                                                   EventSubMessage<SessionMetadata, EventSubMessagePayload<ChannelPointsRedemptionEvent>>>(json);

                                                               if (msg == null)
                                                                   return;
                                                               
                                                               ws.RaiseRewardRedeemed(msg.Payload.Event);
                                                           }
                                                           catch { return; }
                                                       },
                                        };
    }
}