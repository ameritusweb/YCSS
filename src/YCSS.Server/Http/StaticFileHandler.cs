using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace YCSS.Server.Http
{
    public static class StaticFileHandler
    {
        public static string RootDirectory { get; set; } = ".";

        private static readonly Dictionary<string, string> ContentTypes = new()
        {
            [".html"] = "text/html",
            [".css"] = "text/css",
            [".js"] = "application/javascript",
            [".json"] = "application/json",
            [".png"] = "image/png",
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".gif"] = "image/gif",
            [".svg"] = "image/svg+xml",
            [".woff2"] = "font/woff2",
            [".woff"] = "font/woff",
            [".ttf"] = "font/ttf",
            [".eot"] = "application/vnd.ms-fontobject",
            [".ico"] = "image/x-icon",
        };

        public static async Task HandleRequestAsync(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            try
            {
                var path = request.Url?.LocalPath.TrimStart('/');
                if (string.IsNullOrEmpty(path))
                {
                    path = "index.html";
                }

                var fullPath = Path.GetFullPath(Path.Combine(RootDirectory, path));

                // Security check - prevent directory traversal
                if (!fullPath.StartsWith(Path.GetFullPath(RootDirectory)))
                {
                    response.StatusCode = 403;
                    return;
                }

                if (!File.Exists(fullPath))
                {
                    response.StatusCode = 404;
                    return;
                }

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
            catch (Exception)
            {
                response.StatusCode = 500;
            }
            finally
            {
                response.Close();
            }
        }

        private static string GetContentType(string extension)
        {
            return ContentTypes.TryGetValue(extension.ToLower(), out var contentType)
                ? contentType
                : "application/octet-stream";
        }

        private static string InjectLiveReloadScript(string html)
        {
            const string script = """
            <script>
            (function() {
                var ws = new WebSocket('ws://' + location.host + '/ws');
                ws.onmessage = function(msg) {
                    if (msg.data === 'reload') window.location.reload();
                };
                ws.onclose = function() {
                    console.log('Dev server connection closed. Retrying in 1s...');
                    setTimeout(function() {
                        window.location.reload();
                    }, 1000);
                };
            })();
            </script>
            </body>
            """;

            return html.Replace("</body>", script);
        }
    }
}
