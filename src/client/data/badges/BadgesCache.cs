using TwitchAPI.client.data.badges.data.badge;

namespace TwitchAPI.client.data.badges;

public class BadgesCache {
    public Badge[]? GlobalBadges { get; private set; }
    public Badge[]? ChannelBadges { get; private set; }


    public void CacheGlobalEmotes(Badge[] badges) {
        GlobalBadges = badges;
    }

    public void CacheChannelEmotes(Badge[] badges) {
        ChannelBadges = badges;
    }
}