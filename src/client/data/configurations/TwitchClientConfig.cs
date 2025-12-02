namespace TwitchAPI.client.data;

public class TwitchClientConfig {
    public readonly AutoReconnectConfig AutoReconnectConfig;
    public readonly char CommandIdentifier;
    
    
    public TwitchClientConfig(AutoReconnectConfig? autoReconnectConfig = null, char commandIdentifier = '!') {
        AutoReconnectConfig = autoReconnectConfig ?? new AutoReconnectConfig();
        CommandIdentifier = commandIdentifier;
    }
}