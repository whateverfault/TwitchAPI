namespace TwitchAPI.client.data;

public class TwitchClientConfig {
    public AutoReconnectConfig AutoReconnectConfig;
    public char CommandIdentifier;
    
    
    public TwitchClientConfig(AutoReconnectConfig? autoReconnectConfig = null, char commandIdentifier = '!') {
        AutoReconnectConfig = autoReconnectConfig ?? new AutoReconnectConfig();
        CommandIdentifier = commandIdentifier;
    }
}