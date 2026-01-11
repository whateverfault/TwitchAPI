using System.Text;
using TwitchAPI.event_sub.data.subscription_data.events.channel_points;

namespace TwitchAPI.client.data;

public class RewardRedemption {
    public string Id { get; set; }
    
    public string BroadcasterUserId { get; set; }
    
    public string BroadcasterUserLogin { get; set; }
    
    public string BroadcasterUserName { get; set; }
    
    public string UserId { get; set; }
    
    public string UserLogin { get; set; }
    
    public string UserName { get; set; }
    
    public string Text { get; set; } = string.Empty;
    
    public string Status { get; set; }
    
    public DateTime RedeemedAt { get; set; }
    
    public Reward Reward { get; set; }


    public RewardRedemption(
        string id,
        string broadcasterUserId,
        string broadcasterUserLogin,
        string broadcasterUserName,
        string userId,
        string userLogin,
        string username,
        string text,
        string status,
        DateTime redeemedAt,
        Reward reward) {
        Id = id;
        BroadcasterUserId = broadcasterUserId;
        BroadcasterUserLogin = broadcasterUserLogin;
        BroadcasterUserName = broadcasterUserName;
        UserId = userId;
        UserLogin = userLogin;
        UserName = username;
        Text = text;
        Status = status;
        RedeemedAt = redeemedAt;
        Reward = reward;
    }

    public static RewardRedemption Create(ChannelPointsRedemptionEvent redemption) {
        return new RewardRedemption(
                                    redemption.Id,
                                    redemption.BroadcasterUserId,
                                    redemption.BroadcasterUserLogin,
                                    redemption.BroadcasterUserName,
                                    redemption.UserId,
                                    redemption.UserLogin,
                                    redemption.UserName,
                                    Sanitize(redemption.UserInput),
                                    redemption.Status,
                                    redemption.RedeemedAt,
                                    redemption.Reward
                                    );
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