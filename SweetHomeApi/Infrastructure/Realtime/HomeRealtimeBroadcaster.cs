using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace SweetHomeApi.Infrastructure.Realtime;

public class HomeRealtimeBroadcaster : IHomeRealtimeBroadcaster
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<Guid, WebSocket>> clientsByUser = new();

    public async Task AddClientAsync(string userId, WebSocket webSocket, CancellationToken cancellationToken)
    {
        var clientId = Guid.NewGuid();
        var clients = clientsByUser.GetOrAdd(userId, _ => new ConcurrentDictionary<Guid, WebSocket>());
        clients[clientId] = webSocket;

        var buffer = new byte[1024 * 4];
        try
        {
            while (webSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                var result = await webSocket.ReceiveAsync(buffer, cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close)
                    break;
            }
        }
        finally
        {
            clients.TryRemove(clientId, out _);
            if (webSocket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                await webSocket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Closed",
                    CancellationToken.None);
            }
        }
    }

    public async Task BroadcastAsync(string userId, string type, object payload, CancellationToken cancellationToken)
    {
        if (!clientsByUser.TryGetValue(userId, out var clients) || clients.IsEmpty)
            return;

        var message = JsonSerializer.Serialize(new
        {
            type,
            occurredAt = DateTime.UtcNow,
            payload
        }, SerializerOptions);

        var bytes = Encoding.UTF8.GetBytes(message);
        var segment = new ArraySegment<byte>(bytes);

        foreach (var (clientId, client) in clients)
        {
            if (client.State != WebSocketState.Open)
            {
                clients.TryRemove(clientId, out _);
                continue;
            }

            await client.SendAsync(segment, WebSocketMessageType.Text, true, cancellationToken);
        }
    }
}
