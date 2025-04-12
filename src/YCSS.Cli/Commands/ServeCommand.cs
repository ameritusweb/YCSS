using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using System.Net.WebSockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using YCSS.Cli.Common;
using YCSS.Cli.Utils;
using YCSS.Core.Pipeline;
using YCSS.Core.Interfaces;

namespace YCSS.Cli.Commands
{
    public class ServeCommand
    {
        private readonly IServiceProvider _services;
        private readonly ConcurrentDictionary<WebSocket, bool> _connectedClients = new();
        private readonly CancellationTokenSource _cts = new();

        public ServeCommand(IServiceProvider services)
        {
            _services = services;
        }

        public Command Create()
        {
            var command = new Command("serve", "Start development server with live reload")
        {
            CommonOptions.InputFile,
            new Option<int>(
                aliases: new[] { "--port", "-p" },
                getDefaultValue: () => 3000,
                description: "Port to listen on"
            ),
            new Option<string>(
                aliases: new[] { "--root", "-r" },
                getDefaultValue: () => ".",
                description: "Root directory to serve"
            ),
            new Option<bool>(
                "--open",
                "Open browser automatically"
            ),
            new Option<string[]>(
                "--include",
                description: "Additional files or patterns to watch"
            )
        };

            command.SetHandler(HandleServe);
            return command;
        }
        private async Task HandleServe(
            FileInfo input,
            int port,
            string root,
            bool open,
            string[] include)
        {
            var console = _services.GetRequiredService<IConsoleWriter>();
            var pipeline = _services.GetRequiredService<IStylePipeline>();

            try
            {
                if (!input.Exists)
                {
                    throw new FileNotFoundException("Input file not found", input.FullName);
                }

                var listener = new HttpListener();
                listener.Prefixes.Add($"http://localhost:{port}/");
                listener.Start();

                console.WriteSuccess($"""
                Development server started:
                • URL: http://localhost:{port}
                • Root: {Path.GetFullPath(root)}
                • Watching: {input.Name}, {string.Join(", ", include)}
                Press Ctrl+C to stop
                """);

                if (open)
                {
                    ProcessUtil.OpenBrowser($"http://localhost:{port}");
                }

                // Start file watcher
                var watcher = new FileSystemWatcher
                {
                    Path = input.DirectoryName!,
                    Filter = input.Name,
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size
                };

                watcher.Changed += async (_, e) =>
                {
                    if (e.ChangeType == WatcherChangeTypes.Changed)
                    {
                        try
                        {
                            // Recompile
                            var yaml = await File.ReadAllTextAsync(input.FullName);
                            var result = await pipeline.CompileAsync(yaml, new());

                            // Notify all connected clients
                            var message = Encoding.UTF8.GetBytes("reload");
                            foreach (var client in _connectedClients.Keys)
                            {
                                try
                                {
                                    await client.SendAsync(
                                        message,
                                        WebSocketMessageType.Text,
                                        true,
                                        _cts.Token);
                                }
                                catch
                                {
                                    bool res;
                                    _connectedClients.TryRemove(client, out res);
                                }
                            }

                            console.WriteSuccess($"Rebuilt and notified {_connectedClients.Count} clients");
                        }
                        catch (Exception ex)
                        {
                            console.WriteError($"Rebuild failed: {ex.Message}");
                        }
                    }
                };

                watcher.EnableRaisingEvents = true;

                // Handle requests
                _ = Task.Run(async () =>
                {
                    while (!_cts.Token.IsCancellationRequested)
                    {
                        try
                        {
                            var context = await listener.GetContextAsync();

                            if (context.Request.IsWebSocketRequest)
                            {
                                await HandleWebSocketRequest(context);
                            }
                            else
                            {
                                await HandleHttpRequest(context, root);
                            }
                        }
                        catch (Exception ex) when (!_cts.Token.IsCancellationRequested)
                        {
                            console.WriteError($"Request handling failed: {ex.Message}");
                        }
                    }
                });

                // Wait for cancellation
                var tcs = new TaskCompletionSource<bool>();
                Console.CancelKeyPress += (s, e) =>
                {
                    e.Cancel = true;
                    tcs.SetResult(true);
                };

                await tcs.Task;

                // Cleanup
                _cts.Cancel();
                watcher.Dispose();
                listener.Stop();

                foreach (var client in _connectedClients.Keys)
                {
                    await client.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Server shutting down",
                        CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                console.WriteError($"Server failed: {ex.Message}");
                throw;
            }
        }

        private async Task HandleWebSocketRequest(HttpListenerContext context)
        {
            var webSocketContext = await context.AcceptWebSocketAsync(null);
            var webSocket = webSocketContext.WebSocket;

            _connectedClients.TryAdd(webSocket, true);

            try
            {
                var buffer = new byte[1024];
                while (webSocket.State == WebSocketState.Open)
                {
                    var result = await webSocket.ReceiveAsync(
                        new ArraySegment<byte>(buffer),
                        _cts.Token);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await webSocket.CloseAsync(
                            WebSocketCloseStatus.NormalClosure,
                            "Client requested close",
                            _cts.Token);
                    }
                }
            }
            catch
            {
                // Connection closed
            }
            finally
            {
                _connectedClients.TryRemove(webSocket, out _);
            }
        }

        private async Task HandleHttpRequest(HttpListenerContext context, string root)
        {
            var response = context.Response;
            var path = context.Request.Url!.AbsolutePath.TrimStart('/');

            if (string.IsNullOrEmpty(path))
            {
                path = "index.html";
            }

            var fullPath = Path.GetFullPath(Path.Combine(root, path));

            // Security check
            if (!fullPath.StartsWith(Path.GetFullPath(root)))
            {
                response.StatusCode = 403;
                return;
            }

            try
            {
                if (File.Exists(fullPath))
                {
                    response.ContentType = GetContentType(Path.GetExtension(fullPath));
                    var content = await File.ReadAllBytesAsync(fullPath);

                    // Inject live reload script for HTML files
                    if (Path.GetExtension(fullPath).Equals(".html", StringComparison.OrdinalIgnoreCase))
                    {
                        var html = Encoding.UTF8.GetString(content);
                        html = InjectLiveReloadScript(html);
                        content = Encoding.UTF8.GetBytes(html);
                    }

                    await response.OutputStream.WriteAsync(content);
                }
                else
                {
                    response.StatusCode = 404;
                }
            }
            catch
            {
                response.StatusCode = 500;
            }
            finally
            {
                response.Close();
            }
        }

        private static string GetContentType(string extension) => extension.ToLower() switch
        {
            ".html" => "text/html",
            ".css" => "text/css",
            ".js" => "application/javascript",
            ".json" => "application/json",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".svg" => "image/svg+xml",
            _ => "application/octet-stream"
        };

        private static string InjectLiveReloadScript(string html)
        {
            const string script = """
            <script>
            (function() {
                var socket = new WebSocket('ws://' + location.host + '/ws');
                socket.onmessage = function(msg) {
                    if (msg.data === 'reload') window.location.reload();
                };
                socket.onclose = function() {
                    console.log('Live reload connection closed. Retrying in 1s...');
                    setTimeout(function() { window.location.reload(); }, 1000);
                };
            })();
            </script>
            </body>
            """;

            return html.Replace("</body>", script);
        }
    }
}
