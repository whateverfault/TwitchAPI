using TwitchAPI.helix.data.responses.GetUserInfo;
using TwitchAPI.shared;

namespace TwitchAPI.helix.data;

public class HelixCache {
    public FixedSizeDictionary<string, UserInfo> UserInfoTable { get; private set; }


    public HelixCache(int maxSize) {
        UserInfoTable = new FixedSizeDictionary<string, UserInfo>(maxSize);
    }
}