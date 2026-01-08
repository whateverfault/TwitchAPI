using TwitchAPI.api.data.requests;

namespace TwitchAPI.api.data.responses;

public class FollowResponse {
    public List<FollowData>? Data { get; }
    
    
    public FollowResponse(List<FollowData>? data) {
        Data = data;
    }
}