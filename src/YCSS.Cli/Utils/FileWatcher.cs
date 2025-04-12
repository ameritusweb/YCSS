using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YCSS.Cli.Utils
{
    public class FileWatcher
    {
        private readonly ILogger<FileWatcher> _logger;
        private readonly SemaphoreSlim _processingLock;
        private readonly object _lastProcessedLock;
        private DateTime _lastProcessed;
        private bool _isProcessing;

        public FileWatcher(ILogger<FileWatcher> logger)
        {
            _logger = logger;
            _processingLock = new SemaphoreSlim(1, 1);
            _lastProcessedLock = new object();
            _lastProcessed = DateTime.MinValue;
        }

        public async Task WatchAsync(
            FileInfo file,
            Func<Task> onChange,
            CancellationToken ct = default)
        {
            var watcher = new FileSystemWatcher
            {
                Path = file.DirectoryName!,
                Filter = file.Name,
                EnableRaisingEvents = true,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size
            };

            var tcs = new TaskCompletionSource();
            ct.Register(() => tcs.TrySetCanceled());

            watcher.Changed += async (_, e) =>
            {
                try
                {
                    await HandleChangeAsync(e.FullPath, onChange);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error handling file change");
                }
            };

            // Handle Ctrl+C
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                tcs.TrySetCanceled();
            };

            try
            {
                await tcs.Task;
            }
            finally
            {
                watcher.Dispose();
            }
        }

        private async Task HandleChangeAsync(string path, Func<Task> onChange)
        {
            // Debounce frequent changes
            lock (_lastProcessedLock)
            {
                var now = DateTime.UtcNow;
                if ((now - _lastProcessed).TotalMilliseconds < 500)
                {
                    return;
                }
                _lastProcessed = now;
            }

            if (_isProcessing)
            {
                return;
            }

            try
            {
                await _processingLock.WaitAsync();
                _isProcessing = true;

                // Wait for file to be released
                await WaitForFileAccess(path);

                await onChange();
            }
            finally
            {
                _isProcessing = false;
                _processingLock.Release();
            }
        }

        private static async Task WaitForFileAccess(string path)
        {
            var retries = 0;
            const int maxRetries = 3;

            while (retries < maxRetries)
            {
                try
                {
                    using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                    return;
                }
                catch (IOException)
                {
                    retries++;
                    if (retries == maxRetries)
                    {
                        throw;
                    }
                    await Task.Delay(100 * retries);
                }
            }
        }
    }
}
