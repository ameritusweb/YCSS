using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace YCSS.Server.WebSockets
{
    public class WebSocketHandler
    {
        private readonly ILogger _logger;
        private readonly WebSocket _webSocket;
        private readonly CancellationToken _cancellationToken;
        private readonly Action<string> _messageHandler;
        private readonly Action _closeHandler;

        public WebSocketHandler(
            WebSocket webSocket,
            ILogger logger,
            Action<string> messageHandler,
            Action closeHandler,
            CancellationToken cancellationToken)
        {
            _webSocket = webSocket;
            _logger = logger;
            _messageHandler = messageHandler;
            _closeHandler = closeHandler;
            _cancellationToken = cancellationToken;
        }

        public async Task HandleConnectionAsync()
        {
            try
            {
                var buffer = new byte[4096];
                while (_webSocket.State == WebSocketState.Open && !_cancellationToken.IsCancellationRequested)
                {
                    var result = await _webSocket.ReceiveAsync(
                        new ArraySegment<byte>(buffer),
                        _cancellationToken);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await HandleCloseAsync();
                        break;
                    }

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        await HandleMessageAsync(message);
                    }
                }
            }
            catch (WebSocketException ex)
            {
                _logger.LogDebug(ex, "WebSocket connection terminated");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling WebSocket connection");
            }
            finally
            {
                await CleanupAsync();
            }
        }

        public async Task SendAsync(string message)
        {
            if (_webSocket.State != WebSocketState.Open)
            {
                throw new InvalidOperationException("WebSocket is not open");
            }

            var buffer = Encoding.UTF8.GetBytes(message);
            await _webSocket.SendAsync(
                new ArraySegment<byte>(buffer),
                WebSocketMessageType.Text,
                true,
                _cancellationToken);
        }

        private async Task HandleMessageAsync(string message)
        {
            try
            {
                _messageHandler(message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling WebSocket message: {Message}", message);
                await SendErrorAsync("Error processing message");
            }
        }

        private async Task HandleCloseAsync()
        {
            if (_webSocket.State == WebSocketState.Open)
            {
                await _webSocket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Closing connection",
                    _cancellationToken);
            }
            _closeHandler();
        }

        private async Task SendErrorAsync(string error)
        {
            try
            {
                var errorMessage = JsonSerializer.Serialize(new { error });
                await SendAsync(errorMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send error message");
            }
        }

        private async Task CleanupAsync()
        {
            try
            {
                if (_webSocket.State != WebSocketState.Closed)
                {
                    await _webSocket.CloseAsync(
                        WebSocketCloseStatus.EndpointUnavailable,
                        "Connection terminated",
                        CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error during WebSocket cleanup");
            }

            _closeHandler();
        }
    }
}
