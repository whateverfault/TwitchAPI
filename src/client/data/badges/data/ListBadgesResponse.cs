﻿using Newtonsoft.Json;
using TwitchAPI.client.data.badges.data.badge;

namespace TwitchAPI.client.data.badges.data;

public class ListBadgesResponse {
    [JsonProperty("data")]
    public Badge[] Data { get; private set; }
    
    
    public ListBadgesResponse(
        [JsonProperty("data")] Badge[] data) {
        Data = data;
    }
}