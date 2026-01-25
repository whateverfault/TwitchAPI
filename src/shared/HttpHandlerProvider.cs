using System.Net;

namespace TwitchAPI.shared;

public static class HttpHandlerProvider {
    public static readonly SocketsHttpHandler SharedHandler = new SocketsHttpHandler
                                                              {
                                                                  PooledConnectionLifetime = TimeSpan.FromMinutes(1),
                                                                  PooledConnectionIdleTimeout = TimeSpan.FromSeconds(30),
                                                                  MaxConnectionsPerServer = 50,
                                                                  EnableMultipleHttp2Connections = false,
                                                                  UseCookies = false,
                                                                  AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                                                                  UseProxy = true,
                                                                  Proxy = WebRequest.DefaultWebProxy,
                                                              };
}