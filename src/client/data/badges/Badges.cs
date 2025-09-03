using System.Net;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using TwitchAPI.client.credentials;
using TwitchAPI.client.data.badges.data;
using TwitchAPI.client.data.badges.data.badge;

namespace TwitchAPI.client.data.badges;

public class Badges {
    private static readonly SocketsHttpHandler _httpHandler = new SocketsHttpHandler
                                                              {
                                                                  PooledConnectionLifetime  = TimeSpan.FromMinutes(2),
                                                                  MaxConnectionsPerServer = 50,
                                                                  EnableMultipleHttp2Connections = true,
                                                                  PooledConnectionIdleTimeout = TimeSpan.FromMinutes(1),
                                                                  UseCookies = false,
                                                                  AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                                                              };
    private static readonly HttpClient _httpClient = new HttpClient(_httpHandler);
    private static readonly BadgesCache _cache = new BadgesCache();

    private static string? _channelId;
    
    
    public static async Task<Badge[]?> ListGlobalBadges(FullCredentials credentials, EventHandler<string>? callback = null) {
        try {
            if (_cache.GlobalBadges != null) {
                return _cache.GlobalBadges;
            }
            
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.twitch.tv/helix/chat/badges/global");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credentials.Oauth);
            request.Headers.Add("Client-Id", credentials.ClientId);

            var response = await _httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode) {
                var deserialized = JsonConvert.DeserializeObject<ListBadgesResponse>(responseContent);
                if (deserialized == null) return null;
                
                _cache.CacheGlobalEmotes(deserialized.Data);
                return deserialized.Data;
            }

            callback?.Invoke(null, $"Failed to list global emotes. Status: {response.StatusCode}. Content: {responseContent}");
            return null;

        }
        catch (Exception e) {
            callback?.Invoke(null, $"Exception while listing global emotes. {e.Message}");
            return null;
        }
    }
    
    public static async Task<Badge[]?> ListChannelBadges(FullCredentials credentials, EventHandler<string>? callback = null) {
        try {
            if (_cache.ChannelBadges != null 
             && (_channelId == null || _channelId.Equals(credentials.ChannelId))) {
                return _cache.ChannelBadges;
            }
            
            using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.twitch.tv/helix/chat/badges?broadcaster_id={credentials.ChannelId}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credentials.Oauth);
            request.Headers.Add("Client-Id", credentials.ClientId);

            var response = await _httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode) {
                var deserialized = JsonConvert.DeserializeObject<ListBadgesResponse>(responseContent);
                if (deserialized == null) return null;
                
                _cache.CacheChannelEmotes(deserialized.Data);
                _channelId = credentials.ChannelId;
                
                return deserialized.Data;
            }

            callback?.Invoke(null, $"Failed to list global emotes. Status: {response.StatusCode}. Content: {responseContent}");
            return null;
        }
        catch (Exception e) {
            callback?.Invoke(null, $"Exception while listing global emotes. {e.Message}");
            return null;
        }
    }
}