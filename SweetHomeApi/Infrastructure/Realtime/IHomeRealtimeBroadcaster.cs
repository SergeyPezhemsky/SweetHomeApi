using System.Net.WebSockets;

namespace SweetHomeApi.Infrastructure.Realtime;

public interface IHomeRealtimeBroadcaster
{
    Task AddClientAsync(string userId, WebSocket webSocket, CancellationToken cancellationToken);

    Task BroadcastAsync(string userId, string type, object payload, CancellationToken cancellationToken);
}
