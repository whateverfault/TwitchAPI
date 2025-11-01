namespace TwitchAPI.client.data;

public class AutoReconnectConfig {
    public bool AutoReconnect;
    public TimeSpan Cooldown;


    public AutoReconnectConfig(bool autoReconnect = false, TimeSpan? cooldown = null) {
        AutoReconnect = autoReconnect;
        Cooldown = cooldown ?? TimeSpan.FromMinutes(5);
    }
}