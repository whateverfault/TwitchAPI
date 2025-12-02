using TwitchAPI.helix.data.requests;

namespace TwitchAPI.helix.data.responses;

public class GameSearchResponse {
    public List<GameData>? Data { get; }
    
    
    public GameSearchResponse(List<GameData>? data) {
        Data = data;
    }
}