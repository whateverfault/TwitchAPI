namespace TwitchAPI.event_sub.subscription_data.session.reconnect;

public class ReconnectInfo {
    public string NewWebSocketUrl { get; set; }
    public DateTimeOffset ReconnectDeadline { get; set; }
    
    
    public ReconnectInfo(string newWebSocketUrl, DateTimeOffset deadline) {
        NewWebSocketUrl = newWebSocketUrl;
        ReconnectDeadline = deadline;
    }
}