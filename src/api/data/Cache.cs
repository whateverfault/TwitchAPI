using TwitchAPI.api.data.badges;
using TwitchAPI.api.data.responses.GetUserInfo;
using TwitchAPI.shared;

namespace TwitchAPI.api.data;

public class Cache {
    public FixedSizeDictionary<string, UserInfo> UserInfoTable { get; private set; }
    
    public BadgesCache Badges { get; private set; }
    

    public Cache(int capacity) {
        UserInfoTable = new FixedSizeDictionary<string, UserInfo>(capacity);
        Badges = new BadgesCache();
    }
}