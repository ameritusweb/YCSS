using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace YCSS.Server.Http
{
    public class LiveReloadInjector
    {
        private static readonly Regex StylesheetRegex = new(
            @"<link[^>]*rel=[""']stylesheet[""'][^>]*>",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex HrefRegex = new(
            @"href=[""']([^""']*)[""']",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static string InjectReloadableStyles(string html)
        {
            return StylesheetRegex.Replace(html, match =>
            {
                var href = HrefRegex.Match(match.Value).Groups[1].Value;
                if (string.IsNullOrEmpty(href)) return match.Value;

                // Add timestamp to prevent caching
                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                href = href.Contains('?')
                    ? $"{href}&t={timestamp}"
                    : $"{href}?t={timestamp}";

                return match.Value.Replace(
                    HrefRegex.Match(match.Value).Value,
                    $"href='{href}'");
            });
        }

        public static string GenerateDevScript()
        {
            var sb = new StringBuilder();
            sb.AppendLine("<script>");
            sb.AppendLine("(function() {");
            sb.AppendLine("    var ws = new WebSocket('ws://' + location.host + '/ws');");
            sb.AppendLine("    ws.onmessage = function(msg) {");
            sb.AppendLine("        if (msg.data === 'reload') {");
            sb.AppendLine("            var links = document.getElementsByTagName('link');");
            sb.AppendLine("            for (var i = 0; i < links.length; i++) {");
            sb.AppendLine("                if (links[i].rel === 'stylesheet') {");
            sb.AppendLine("                    var href = links[i].href.split('?')[0];");
            sb.AppendLine("                    links[i].href = href + '?t=' + Date.now();");
            sb.AppendLine("                }");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine("    };");
            sb.AppendLine("    ws.onclose = function() {");
            sb.AppendLine("        console.log('Dev server connection closed. Retrying in 1s...');");
            sb.AppendLine("        setTimeout(function() {");
            sb.AppendLine("            window.location.reload();");
            sb.AppendLine("        }, 1000);");
            sb.AppendLine("    };");
            sb.AppendLine("})();");
            sb.AppendLine("</script>");
            return sb.ToString();
        }
    }
}
