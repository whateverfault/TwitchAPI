using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;
using TwitchAPI.client.credentials;
using TwitchAPI.event_sub.data.subscription_data.subscription;
using TwitchAPI.api.data.requests.chat_subscription;
using TwitchAPI.shared;

namespace TwitchAPI.event_sub.data;

public class EventSub {
    private readonly HttpClient _httpClient;


    public EventSub(HttpClient? client = null) {
        _httpClient = client ?? new HttpClient(HttpHandlerProvider.SharedHandler, disposeHandler: false);
    }
    
    public async Task<EventSubPayload?> SubscribeToChannelChat(
        string? sessionId,
        FullCredentials credentials,
        EventHandler<string>? callback = null) {
        try {
            var subscription = new EventSubPayload(
                                                   "channel.chat.message",
                                                   "1",
                                                   new Condition(credentials.Broadcaster.UserId, credentials.Bot.UserId),
                                                   new Transport("websocket", sessionId)
                                                  );
            
            var json = JsonConvert.SerializeObject(subscription);
            
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.twitch.tv/helix/eventsub/subscriptions");
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credentials.Bot.Oauth);
            request.Headers.Add("Client-Id", credentials.Bot.ClientId);
            
            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();
            
            if (!response.IsSuccessStatusCode) {
                callback?.Invoke(null, $"Couldn't subscribe to chat. Status: {response.StatusCode}. Content: {content}");
                return null;
            }
            
            var deserialized = JsonConvert.DeserializeObject<EventSubData>(content);
            return deserialized?.Data.FirstOrDefault();
        }
        catch (Exception e) {
            callback?.Invoke(null, $"Error while subscribing to a chat. {e.Message}");
            return null;
        }
    }
    
    public async Task<EventSubPayload?> SubscribeToChannelRedemptions(
        string? sessionId,
        FullCredentials credentials,
        EventHandler<string>? callback = null) {
        try {
            var subscription = new EventSubPayload(
                                                   "channel.channel_points_custom_reward_redemption.add",
                                                   "1",
                                                   new Condition(credentials.Broadcaster.UserId),
                                                   new Transport("websocket", sessionId)
                                                  );

            var json = JsonConvert.SerializeObject(subscription);

            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.twitch.tv/helix/eventsub/subscriptions");
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credentials.Broadcaster.Oauth);
            request.Headers.Add("Client-Id", credentials.Broadcaster.ClientId);

            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode) {
                callback?.Invoke(null, $"Couldn't subscribe to reward redeems. Status: {response.StatusCode}. Content: {content}");
                return null;
            }

            var deserialized = JsonConvert.DeserializeObject<EventSubData>(content);
            return deserialized?.Data.FirstOrDefault();
        }
        catch (Exception e) {
            callback?.Invoke(null, $"Error while subscribing to reward redeems. {e.Message}");
            return null;
        }
    }
}