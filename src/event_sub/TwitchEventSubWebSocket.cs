using System.Net.WebSockets;
using System.Text;
using Newtonsoft.Json;
using TwitchAPI.event_sub.subscription_data.events;
using TwitchAPI.event_sub.subscription_data.events.chat_message;
using TwitchAPI.event_sub.subscription_data.session;
using TwitchAPI.event_sub.subscription_data.session.reconnect;

namespace TwitchAPI.event_sub;

public class TwitchEventSubWebSocket {
    private const int RECONNECTION_DEADLINE = 25;
    
    private readonly object _socketLock = new object();
    
    private CancellationTokenSource _cts = new CancellationTokenSource();
    
    private ClientWebSocket? _webSocket;
    private ClientWebSocket? _oldWebSocket;
    
    private bool _reconnecting;
    private string? _pendingReconnectUrl;
    private DateTime? _reconnectDeadline;
    
    public event EventHandler<ChatMessageEvent?>? OnChatMessageReceived;
    public event EventHandler<string>? OnConnectionClosed;
    public event EventHandler<string>? OnWebSocketError;
    
    public event EventHandler<ReconnectInfo>? OnReconnectRequired;
    
    public string? SessionId { get; private set; }
    public string? SubscriptionId { get; private set; }


    public async Task ConnectAsync() {
        await ConnectInternalAsync("wss://eventsub.wss.twitch.tv/ws");
    }

    private async Task ConnectInternalAsync(string url) {
        try {
            var socket = new ClientWebSocket();

            lock (_socketLock) {
                _webSocket = socket;
            }
            
            await _webSocket.ConnectAsync(new Uri(url), _cts.Token);
            await WaitForWelcomeMessage(_webSocket, _cts.Token);
            
            _ = ReceiveMessagesAsync(socket, _cts.Token);
        }
        catch (Exception ex) {
            OnWebSocketError?.Invoke(this, $"Connection failed: {ex.Message}");
        }
    }

    
    // TODO: refactor this big-ass ReceiveMessagesAsync method copy.
    private async Task WaitForWelcomeMessage(ClientWebSocket socket, CancellationToken cancellationToken = default) {
        var buffer = new byte[4096];
        var messageBuffer = new List<byte>();
        
        while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested) {
            try {
                var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
                
                if (result.MessageType == WebSocketMessageType.Close) {
                    if (result.CloseStatus == (WebSocketCloseStatus)4004) {
                        OnWebSocketError?.Invoke(this, "Reconnect timeout: Failed to establish new connection within timeframe");
                    }
                    OnConnectionClosed?.Invoke(this, "WebSocket closed by server");
                    break;
                }

                messageBuffer.AddRange(buffer[..result.Count]);
                if (!result.EndOfMessage) continue;

                var message = Encoding.UTF8.GetString(messageBuffer.ToArray());
                messageBuffer.Clear();

                var baseMessage =
                    JsonConvert.DeserializeObject<EventSubMessage<SessionMetadata, SessionWelcomePayload>>(message);

                if (baseMessage == null) {
                    OnWebSocketError?.Invoke(this, "Couldn't deserialize the message.");
                    return;
                }

                if (baseMessage.Metadata.MessageType.Equals("session_welcome")) {
                    var welcomeMessage =
                        JsonConvert
                           .DeserializeObject<EventSubMessage<SessionMetadata, SessionWelcomePayload>>(message);

                    if (welcomeMessage == null) {
                        OnWebSocketError?.Invoke(this, "Couldn't deserialize the welcome message");
                        return;
                    }

                    SessionId = welcomeMessage.Payload.Session.Id;
                    
                    if (_reconnecting
                     && _webSocket == socket 
                     && _oldWebSocket != null) {
                        await CloseOldConnection();
                    }

                    return;
                }
            }
            catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely) {
                if (!_reconnecting) {
                    OnConnectionClosed?.Invoke(this, $"WebSocket connection closed prematurely: {ex.Message}");
                }
                break;
            }
            catch (Exception ex) {
                OnConnectionClosed?.Invoke(this, $"Error: {ex.Message}");
                break;
            }
        }
    }
    
    private async Task ReceiveMessagesAsync(ClientWebSocket socket, CancellationToken cancellationToken = default) {
        var buffer = new byte[4096];
        var messageBuffer = new List<byte>();
        
        while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested) {
            try {
                var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
                
                if (result.MessageType == WebSocketMessageType.Close) {
                    if (result.CloseStatus == (WebSocketCloseStatus)4004) {
                        OnWebSocketError?.Invoke(this, "Reconnect timeout: Failed to establish new connection within timeframe");
                    }
                    OnConnectionClosed?.Invoke(this, "WebSocket closed by server");
                    break;
                }

                messageBuffer.AddRange(buffer[..result.Count]);
                if (!result.EndOfMessage) continue;

                var message = Encoding.UTF8.GetString(messageBuffer.ToArray());
                messageBuffer.Clear();

                await HandleMessage(message, socket);
            }
            catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely) {
                if (!_reconnecting) {
                    OnConnectionClosed?.Invoke(this, $"WebSocket connection closed prematurely: {ex.Message}");
                }
                break;
            }
            catch (Exception ex) {
                OnConnectionClosed?.Invoke(this, $"Error: {ex.Message}");
                break;
            }
        }
    }

    private async Task HandleMessage(string message, ClientWebSocket sourceSocket) {
        try {
            var baseMessage =
                JsonConvert.DeserializeObject<EventSubMessage<SessionMetadata, SessionWelcomePayload>>(message);

            if (baseMessage == null) {
                OnWebSocketError?.Invoke(this, "Couldn't deserialize the message.");
                return;
            }

            switch (baseMessage.Metadata.MessageType) {
                case "session_welcome": {
                    var welcomeMessage =
                        JsonConvert
                           .DeserializeObject<EventSubMessage<SessionMetadata, SessionWelcomePayload>>(message);

                    if (welcomeMessage == null) {
                        OnWebSocketError?.Invoke(this, "Couldn't deserialize the welcome message");
                        return;
                    }

                    SessionId = welcomeMessage.Payload.Session.Id;
                    
                    if (_reconnecting
                     && _webSocket == sourceSocket 
                     && _oldWebSocket != null) {
                        await CloseOldConnection();
                    }
                    break;
                }

                case "session_reconnect": {
                    if (sourceSocket == _oldWebSocket) break;
                    await HandleReconnectMessage(message);
                    break;
                }

                case "notification" when baseMessage.Metadata.SubscriptionType == "channel.chat.message": {
                    var chatMessage =
                        JsonConvert
                           .DeserializeObject<EventSubMessage<SessionMetadata, EventSubMessagePayload<ChatMessageEvent>>>(message);
                    OnChatMessageReceived?.Invoke(this, chatMessage?.Payload.Event);
                    break;
                }

                case "session_keepalive":
                    break;

                default: {
                    OnWebSocketError?.Invoke(this, $"Unhandled message type: {baseMessage.Metadata.MessageType}");
                    break;
                }
            }
        }
        catch (Exception ex) {
            OnWebSocketError?.Invoke(this, $"Error handling message: {ex.Message}");
        }
    }

    public async Task DisconnectAsync() {
        if (_webSocket?.State == WebSocketState.Open) {
            await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client closing", CancellationToken.None);
        }
        if (_oldWebSocket?.State == WebSocketState.Open) {
            await _oldWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client closing", CancellationToken.None);
        }
        
        await _cts.CancelAsync();
        _cts = new CancellationTokenSource();
    }

    public void SetSubscriptionId(string id) {
        SubscriptionId = id;
    }
    
    private async Task HandleReconnectMessage(string message) {
        try {
            var reconnectMessage = JsonConvert.DeserializeObject<EventSubMessage<SessionMetadata, SessionReconnectPayload>>(message);
            if (reconnectMessage == null) {
                OnWebSocketError?.Invoke(this, "Couldn't deserialize reconnect message");
                return;
            }

            _pendingReconnectUrl = reconnectMessage.Payload.Session.ReconnectUrl;
            _reconnectDeadline = DateTime.UtcNow.AddSeconds(RECONNECTION_DEADLINE);

            var reconnectInfo = new ReconnectInfo(_pendingReconnectUrl, _reconnectDeadline.Value);
            OnReconnectRequired?.Invoke(this, reconnectInfo);
            _reconnecting = true;
            
            var newWebSocket = new ClientWebSocket();
            await newWebSocket.ConnectAsync(new Uri(_pendingReconnectUrl), _cts.Token);

            lock (_socketLock) {
                _oldWebSocket = _webSocket;
                _webSocket = newWebSocket;
            }
            
            _ = Task.Run(() => ReceiveMessagesAsync(newWebSocket), _cts.Token);
            
            _ = Task.Run(async () => {
                             await Task.Delay(TimeSpan.FromSeconds(RECONNECTION_DEADLINE), _cts.Token);
                             if (_reconnecting && _reconnectDeadline <= DateTime.UtcNow) {
                                 OnWebSocketError?.Invoke(this, "Reconnect deadline passed, forcing old connection closed.");
                                 await CloseOldConnection();
                             }
                         });
        }
        catch (Exception ex) {
            OnWebSocketError?.Invoke(this, $"Error during reconnect: {ex.Message}");
            _reconnecting = false;
            _pendingReconnectUrl = null;
            _reconnectDeadline = null;
        }
    }
    
    
    private async Task CloseOldConnection() {
        try {
            if (_oldWebSocket?.State == WebSocketState.Open) {
                await _oldWebSocket.CloseAsync(
                                               WebSocketCloseStatus.NormalClosure, 
                                               "Replaced by new connection", 
                                               CancellationToken.None
                                               );
            }
        }
        catch (Exception ex) {
            OnWebSocketError?.Invoke(this, $"Error closing old connection: {ex.Message}");
        }
        finally {
            _oldWebSocket?.Dispose();
            _oldWebSocket = null;
            _reconnecting = false;
            _pendingReconnectUrl = null;
            _reconnectDeadline = null;
        }
    }
}