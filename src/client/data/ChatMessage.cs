using System.Drawing;
using System.Text;
using TwitchAPI.api.data.badges.data.badge;
using TwitchAPI.event_sub.subscription_data.events.chat_message;

namespace TwitchAPI.client.data;

public class ChatMessage {
    public string ChannelId { get; }

    public string Channel { get; }

    public string UserId { get; }

    public string Username { get; }

    public string Id { get; }

    public string Text { get; }
    
    public Color Color { get; }
    
    public List<BadgeVersion>? Badges { get; } 
    
    public ChatReply? Reply { get; }
    
    public string? RewardId { get; }
    
    public bool IsBroadcaster { get; }
    
    public bool IsModerator { get; }
    
    public bool IsVip { get; }
    
    public bool IsSubscriber { get; }
    
    
    private ChatMessage(
        string channelId,
        string channel,
        string userId,
        string username,
        string id,
        string text,
        Color color,
        List<BadgeVersion>? badges,
        ChatReply? reply,
        string? rewardId, 
        bool isBroadcaster,
        bool isModerator,
        bool isVip,
        bool isSubscriber) {
        ChannelId = channelId;
        Channel = channel;
        UserId = userId;
        Username = username;
        Id = id;
        Text = text;
        Color = color;
        Badges = badges;
        Reply = reply;
        RewardId = rewardId;
        IsBroadcaster = isBroadcaster;
        IsModerator = isModerator;
        IsVip = isVip;
        IsSubscriber = isSubscriber;
    }
    
    public static ChatMessage Create(
        ChatMessageEvent e,
        Badge[]? globalBadges = null,
        Badge[]? channelBadges = null) {
        var message = new StringBuilder();

        for (var i = 0; i < e.Message.Fragments.Length; i++) {
            var fragment = e.Message.Fragments[i];
            if (e.Reply != null && i == 0 && fragment.Type == "mention") {
                continue;
            }

            var processed = Sanitize(fragment.Text);

            message.Append($"{processed} ");
        }

        List<BadgeVersion>? badges = null;
        if (globalBadges != null && channelBadges != null) {
            badges = [];
            foreach (var badge in e.Badges) {
                var badgeVersion = FindBadge(globalBadges, badge.Name, badge.Version);
                if (badgeVersion != null) {
                    badges.Add(badgeVersion);
                    continue;
                }
            
                badgeVersion = FindBadge(channelBadges, badge.Name, badge.Version);
                if (badgeVersion == null) continue;
                badges.Add(badgeVersion);
            }
        
        }
        
        ParseBadges(e.Badges, out var isBroadcaster, out var isModerator, out var isVip, out var isSubscriber);
        var args = new ChatMessage(
                                   e.ChannelId,
                                   e.Channel,
                                   e.UserId,
                                   e.User, 
                                   e.MessageId,
                                   message.ToString(),
                                   ColorTranslator.FromHtml(e.Color),
                                   badges,
                                   e.Reply,
                                   e.RewardId,
                                   isBroadcaster,
                                   isModerator,
                                   isVip,
                                   isSubscriber
                                   );
        return args;
    }
    
    private static BadgeVersion? FindBadge(Badge[] sets, string name, string version) {
        return (from set in sets
                where set.Name.Equals(name)
                from ver in set.Versions
                where ver.Id.Equals(version)
                select ver).FirstOrDefault();
    }
    
    private static void ParseBadges(BadgeInfo[] badges, out bool isBroadcaster, out bool isModerator, out bool isVip, out bool isSubscriber) {
        isBroadcaster = false;
        isModerator = false;
        isVip = false;
        isSubscriber = false;

        foreach (var badge in badges) {
            switch (badge.Name) {
                case "broadcaster": {
                    isBroadcaster = true;
                    break;
                }
                case "moderator": {
                    isModerator = true;
                    break;
                }
                case "vip": {
                    isVip = true;
                    break;
                }
                case "subscriber": {
                    isSubscriber = true;
                    break;
                }
            }
        }
    }

    private static string Sanitize(string message) {
        var sb = new StringBuilder();

        foreach (var c in message
                         .Replace($"{(char)56128}", "")
                         .Replace($"{(char)56320}", "")
                         .Replace($"{(char)847}", "")
                         .Trim()) {
            if (!char.IsControl(c)) {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }
}