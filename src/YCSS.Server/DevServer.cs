using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using YCSS.Core.Pipeline;
using YCSS.Server.Http;
using YCSS.Server.WebSockets;

namespace YCSS.Server
{
    public class DevServer : IAsyncDisposable
    {
        private readonly ILogger<DevServer> _logger;
        private readonly IStylePipeline _pipeline;
        private readonly WebSocketManager _webSocketManager;
        private readonly HttpListener _listener;
        private readonly CancellationTokenSource _cts;
        private Task? _serverTask;

        public DevServer(
            ILogger<DevServer> logger,
            IStylePipeline pipeline,
            string rootDirectory,
            int port = 3000)
        {
            _logger = logger;
            _pipeline = pipeline;
            _webSocketManager = new WebSocketManager(logger);
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://localhost:{port}/");
            _cts = new CancellationTokenSource();

            StaticFileHandler.RootDirectory = rootDirectory;
        }

        public async Task StartAsync()
        {
            _listener.Start();
            _logger.LogInformation("Development server started on http://localhost:{Port}",
                _listener.Prefixes.First().Replace("http://localhost:", "").TrimEnd('/'));

            _serverTask = HandleConnectionsAsync();
        }

        public async Task StopAsync()
        {
            _cts.Cancel();
            if (_serverTask != null)
            {
                await _serverTask;
            }
            _listener.Stop();
            _logger.LogInformation("Development server stopped");
        }

        public async Task NotifyReloadAsync()
        {
            await _webSocketManager.BroadcastAsync("reload");
        }

        private async Task HandleConnectionsAsync()
        {
            try
            {
                while (!_cts.Token.IsCancellationRequested)
                {
                    var context = await _listener.GetContextAsync();
                    _ = HandleRequestAsync(context);
                }
            }
            catch (Exception ex) when (!_cts.Token.IsCancellationRequested)
            {
                _logger.LogError(ex, "Error handling connections");
            }
        }

        private async Task HandleRequestAsync(HttpListenerContext context)
        {
            try
            {
                if (context.Request.IsWebSocketRequest)
                {
                    var webSocketContext = await context.AcceptWebSocketAsync(null);
                    await _webSocketManager.HandleConnectionAsync(webSocketContext.WebSocket, _cts.Token);
                }
                else
                {
                    await StaticFileHandler.HandleRequestAsync(context);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling request");
                context.Response.StatusCode = 500;
                context.Response.Close();
            }
        }

        public async ValueTask DisposeAsync()
        {
            await StopAsync();
            _cts.Dispose();
            _listener.Close();
        }
    }
}
