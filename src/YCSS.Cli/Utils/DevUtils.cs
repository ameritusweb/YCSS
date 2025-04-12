using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace YCSS.Cli.Utils
{
    public interface INotificationService
    {
        Task NotifyAsync(string title, string message, NotificationType type = NotificationType.Info);
    }

    public enum NotificationType
    {
        Info,
        Success,
        Warning,
        Error
    }

    public class DesktopNotificationService : INotificationService
    {
        public async Task NotifyAsync(string title, string message, NotificationType type = NotificationType.Info)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                await ShowWindowsNotificationAsync(title, message, type);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                await ShowMacNotificationAsync(title, message, type);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                await ShowLinuxNotificationAsync(title, message, type);
            }
        }

        private async Task ShowWindowsNotificationAsync(string title, string message, NotificationType type)
        {
            var icon = type switch
            {
                NotificationType.Success => "✓",
                NotificationType.Warning => "⚠",
                NotificationType.Error => "❌",
                _ => "ℹ"
            };

            var script = $"""
            [Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime] | Out-Null
            [Windows.Data.Xml.Dom.XmlDocument, Windows.Data.Xml.Dom.XmlDocument, ContentType = WindowsRuntime] | Out-Null

            $template = @"
            <toast>
                <visual>
                    <binding template="ToastText02">
                        <text id="1">{icon} {title}</text>
                        <text id="2">{message}</text>
                    </binding>
                </visual>
            </toast>
            "@

            $xml = New-Object Windows.Data.Xml.Dom.XmlDocument
            $xml.LoadXml($template)
            $toast = [Windows.UI.Notifications.ToastNotification]::new($xml)
            $notifier = [Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier("YCSS")
            $notifier.Show($toast)
            """;

            await ProcessUtil.RunAsync("powershell", new[] { "-Command", script });
        }

        private async Task ShowMacNotificationAsync(string title, string message, NotificationType type)
        {
            var script = $"""
            display notification "{message}" with title "{title}"
            """;

            await ProcessUtil.RunAsync("osascript", new[] { "-e", script });
        }

        private async Task ShowLinuxNotificationAsync(string title, string message, NotificationType type)
        {
            var urgency = type switch
            {
                NotificationType.Error => "critical",
                NotificationType.Warning => "normal",
                _ => "low"
            };

            await ProcessUtil.RunAsync("notify-send", new[]
            {
            "--urgency", urgency,
            "--app-name", "YCSS",
            title,
            message
        });
        }
    }

    public static class ProcessUtil
    {
        public static async Task<string> RunAsync(
            string command,
            string[] args,
            string? workingDir = null)
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = command,
                    Arguments = string.Join(" ", args),
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = workingDir ?? Directory.GetCurrentDirectory()
                }
            };

            var output = new StringBuilder();
            var error = new StringBuilder();

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null)
                {
                    output.AppendLine(e.Data);
                }
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null)
                {
                    error.AppendLine(e.Data);
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                throw new Exception($"Process failed with exit code {process.ExitCode}: {error}");
            }

            return output.ToString().Trim();
        }

        public static void OpenBrowser(string url)
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Process.Start("open", url);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    Process.Start("xdg-open", url);
                }
            }
            catch
            {
                // Ignore browser launch failures
            }
        }
    }
}
