namespace TwitchAPI.client.data;

public class AutoReconnectConfig {
    public bool AutoReconnect;
    public int Tries;
    public TimeSpan Cooldown;


    public AutoReconnectConfig(bool autoReconnect = false, int tries = 3, TimeSpan? cooldown = null) {
        AutoReconnect = autoReconnect;
        Tries = tries;
        Cooldown = cooldown ?? TimeSpan.FromSeconds(5);
    }
}