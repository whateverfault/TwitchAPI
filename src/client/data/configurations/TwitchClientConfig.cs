using TwitchAPI.api;

namespace TwitchAPI.client.data;

public class TwitchClientConfig {
    public readonly AutoReconnectConfig AutoReconnectConfig;
    public readonly TwitchApi Api;
    public readonly char CommandIdentifier;
    
    
    public TwitchClientConfig(AutoReconnectConfig? autoReconnectConfig = null, TwitchApi? helixApi = null, char commandIdentifier = '!') {
        AutoReconnectConfig = autoReconnectConfig ?? new AutoReconnectConfig();
        Api = helixApi ?? new TwitchApi();
        CommandIdentifier = commandIdentifier;
    }
}