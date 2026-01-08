using TwitchAPI.api.data.requests;

namespace TwitchAPI.api.data.responses;

public class GameSearchResponse {
    public List<GameData>? Data { get; }
    
    
    public GameSearchResponse(List<GameData>? data) {
        Data = data;
    }
}