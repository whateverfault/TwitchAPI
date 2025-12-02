using TwitchAPI.helix.data.requests;

namespace TwitchAPI.helix.data.responses;

public class FollowResponse {
    public List<FollowData>? Data { get; }
    
    
    public FollowResponse(List<FollowData>? data) {
        Data = data;
    }
}