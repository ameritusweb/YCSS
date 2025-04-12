using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;

namespace YCSS.Server.WebSockets
{
    public class WebSocketManager
    {
        private readonly ILogger _logger;
        private readonly Dictionary<string, WebSocketHandler> _handlers;
        private readonly object _lock = new();

        public WebSocketManager(ILogger logger)
        {
            _logger = logger;
            _handlers = new Dictionary<string, WebSocketHandler>();
        }

        public async Task HandleConnectionAsync(WebSocket webSocket, CancellationToken ct)
        {
            var connectionId = Guid.NewGuid().ToString();
            var handler = new WebSocketHandler(
                webSocket,
                _logger,
                message => HandleMessage(connectionId, message),
                () => RemoveConnection(connectionId),
                ct);

            AddConnection(connectionId, handler);
            await handler.HandleConnectionAsync();
        }

        public async Task BroadcastAsync(string message)
        {
            var failedConnections = new List<string>();

            foreach (var (id, handler) in GetAllHandlers())
            {
                try
                {
                    await handler.SendAsync(message);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to send message to client {Id}", id);
                    failedConnections.Add(id);
                }
            }

            // Cleanup failed connections
            foreach (var id in failedConnections)
            {
                RemoveConnection(id);
            }

            _logger.LogDebug("Broadcast message sent to {Count} clients",
                _handlers.Count - failedConnections.Count);
        }

        private void HandleMessage(string connectionId, string message)
        {
            _logger.LogDebug("Received message from {ConnectionId}: {Message}",
                connectionId, message);
            // Handle any client messages here
        }

        private void AddConnection(string connectionId, WebSocketHandler handler)
        {
            lock (_lock)
            {
                _handlers.Add(connectionId, handler);
            }
            _logger.LogInformation("WebSocket client {ConnectionId} connected. Total clients: {Count}",
                connectionId, _handlers.Count);
        }

        private void RemoveConnection(string connectionId)
        {
            lock (_lock)
            {
                _handlers.Remove(connectionId);
            }
            _logger.LogInformation("WebSocket client {ConnectionId} disconnected. Remaining clients: {Count}",
                connectionId, _handlers.Count);
        }

        private IEnumerable<KeyValuePair<string, WebSocketHandler>> GetAllHandlers()
        {
            lock (_lock)
            {
                return _handlers.ToList();
            }
        }
    }
}
