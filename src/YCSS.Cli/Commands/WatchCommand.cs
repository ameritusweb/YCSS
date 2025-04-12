using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YCSS.Cli.Common;
using YCSS.Cli.Utils;
using YCSS.Core.Compilation;
using YCSS.Core.Pipeline;

namespace YCSS.Cli.Commands
{
    public class WatchCommand
    {
        private readonly IServiceProvider _services;
        private readonly ConcurrentDictionary<string, DateTime> _lastProcessed = new();
        private readonly SemaphoreSlim _processLock = new(1, 1);

        public WatchCommand(IServiceProvider services)
        {
            _services = services;
        }

        public Command Create()
        {
            var command = new Command("watch", "Watch for file changes and rebuild")
        {
            CommonOptions.InputFile,
            CommonOptions.OutputFile,
            CommonOptions.Format,
            new Option<string[]>(
                aliases: new[] { "--include" },
                description: "Additional files or patterns to watch"
            ),
            new Option<int>(
                aliases: new[] { "--debounce" },
                getDefaultValue: () => 300,
                description: "Debounce time in milliseconds"
            ),
            new Option<bool>(
                "--notify",
                "Show desktop notifications"
            )
        };

            command.SetHandler(HandleWatch);
            return command;
        }

        private async Task HandleWatch(
        FileInfo input,
        FileInfo? output,
            string format,
            string[] include,
            int debounce,
            bool notify)
        {
            var console = _services.GetRequiredService<IConsoleWriter>();
            var pipeline = _services.GetRequiredService<IStylePipeline>();
            var notifier = notify ? _services.GetRequiredService<INotificationService>() : null;

            try
            {
                if (!input.Exists)
                {
                    throw new FileNotFoundException("Input file not found", input.FullName);
                }

                // Create watcher for main input file
                var mainWatcher = new FileSystemWatcher
                {
                    Path = input.DirectoryName!,
                    Filter = input.Name,
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime
                };

                // Create watchers for additional patterns
                var additionalWatchers = include.Select(pattern =>
                {
                    var dir = Path.GetDirectoryName(pattern) ?? ".";
                    return new FileSystemWatcher
                    {
                        Path = dir,
                        Filter = Path.GetFileName(pattern),
                        NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime
                    };
                }).ToList();

                async Task ProcessChange(string file)
                {
                    try
                    {
                        await _processLock.WaitAsync();

                        // Debounce
                        var now = DateTime.UtcNow;
                        if (_lastProcessed.TryGetValue(file, out var lastTime))
                        {
                            if ((now - lastTime).TotalMilliseconds < debounce)
                            {
                                return;
                            }
                        }
                        _lastProcessed[file] = now;

                        console.Clear();
                        console.WriteInfo($"File changed: {Path.GetFileName(file)}");

                        // Wait for file to be released
                        await WaitForFileAccess(file);

                        var yaml = await File.ReadAllTextAsync(input.FullName);
                        var result = await pipeline.CompileAsync(yaml, new()
                        {
                            Format = ParseFormat(format)
                        });

                        if (output != null)
                        {
                            await File.WriteAllTextAsync(output.FullName, result.Output);
                            console.WriteSuccess($"Rebuilt successfully at {DateTime.Now:HH:mm:ss}");

                            if (notifier != null)
                            {
                                await notifier.NotifyAsync("YCSS", "Build completed successfully");
                            }
                        }
                        else
                        {
                            console.WriteLine(result.Output);
                        }

                        // Show statistics
                        var stats = new Panel(new Table()
                            .AddColumn("Metric")
                            .AddColumn("Value")
                            .AddRow("Tokens", result.Statistics.TokenCount.ToString())
                            .AddRow("Components", result.Statistics.ComponentCount.ToString())
                            .AddRow("Output Size", $"{result.Statistics.OutputSize:N0} bytes"))
                            .Header("Build Statistics");

                        console.WriteLine(stats);
                    }
                    catch (Exception ex)
                    {
                        console.WriteError($"Build failed: {ex.Message}");
                        if (notifier != null)
                        {
                            await notifier.NotifyAsync("YCSS", "Build failed", NotificationType.Error);
                        }
                    }
                    finally
                    {
                        _processLock.Release();
                    }
                }

                // Setup event handlers
                void OnChanged(object sender, FileSystemEventArgs e)
                {
                    if (e.ChangeType == WatcherChangeTypes.Changed)
                    {
                        _ = ProcessChange(e.FullPath);
                    }
                }

                mainWatcher.Changed += OnChanged;
                foreach (var watcher in additionalWatchers)
                {
                    watcher.Changed += OnChanged;
                }

                // Start watching
                mainWatcher.EnableRaisingEvents = true;
                foreach (var watcher in additionalWatchers)
                {
                    watcher.EnableRaisingEvents = true;
                }

                console.WriteInfo($"""
                Watching for changes...
                • Main file: {input.FullName}
                • Additional patterns: {string.Join(", ", include)}
                Press Ctrl+C to stop
                """);

                // Initial build
                await ProcessChange(input.FullName);

                // Wait for cancellation
                var tcs = new TaskCompletionSource<bool>();
                Console.CancelKeyPress += (s, e) =>
                {
                    e.Cancel = true;
                    tcs.SetResult(true);
                };

                await tcs.Task;

                // Cleanup
                mainWatcher.Dispose();
                foreach (var watcher in additionalWatchers)
                {
                    watcher.Dispose();
                }
            }
            catch (Exception ex)
            {
                console.WriteError($"Watch failed: {ex.Message}");
                throw;
            }
        }

        private static async Task WaitForFileAccess(string path, int retries = 3)
        {
            for (var i = 0; i < retries; i++)
            {
                try
                {
                    using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                    return;
                }
                catch (IOException)
                {
                    if (i == retries - 1) throw;
                    await Task.Delay(100 * (i + 1));
                }
            }
        }

        private static OutputFormat ParseFormat(string format) => format.ToLower() switch
        {
            "css" => OutputFormat.CSS,
            "scss" => OutputFormat.SCSS,
            "tailwind" => OutputFormat.Tailwind,
            "tokens" => OutputFormat.Tokens,
            _ => throw new ArgumentException($"Unsupported format: {format}")
        };
    }
}
