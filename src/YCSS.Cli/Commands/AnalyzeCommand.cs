using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YCSS.Cli.Common;
using YCSS.Cli.Utils;
using YCSS.Core.Pipeline;

namespace YCSS.Cli.Commands
{
    public class AnalyzeCommand
    {
        private readonly IServiceProvider _services;

        public AnalyzeCommand(IServiceProvider services)
        {
            _services = services;
        }

        public Command Create()
        {
            var command = new Command("analyze", "Analyze styles for patterns")
        {
            CommonOptions.InputFile,
            CommonOptions.OutputFile,
            CommonOptions.Format,
            CommonOptions.Watch,
            CommonOptions.Verbose,
            new Option<bool>(
                "--no-cache",
                "Disable analysis caching"
            )
        };

            command.SetHandler(HandleAnalyze);
            return command;
        }

        private async Task HandleAnalyze(
            FileInfo input,
            FileInfo? output,
            string format,
            bool watch,
            bool verbose,
            bool noCache)
        {
            var pipeline = _services.GetRequiredService<IStylePipeline>();
            var console = _services.GetRequiredService<IConsoleWriter>();
            var progress = _services.GetRequiredService<IProgressRenderer>();

            try
            {
                if (!input.Exists)
                {
                    throw new FileNotFoundException("Input file not found", input.FullName);
                }

                async Task AnalyzeFile()
                {
                    await progress.RunWithProgressAsync("Analyzing styles", async ctx =>
                    {
                        ctx.AddTask("Reading input file");
                        var yaml = await File.ReadAllTextAsync(input.FullName);

                        ctx.AddTask("Analyzing patterns");
                        var result = await pipeline.AnalyzeAsync(yaml, !noCache);

                        // Process results
                        var tree = new Tree("Analysis Results");

                        // Add pattern clusters
                        var patternNode = tree.AddNode("[blue]Pattern Clusters[/]");
                        foreach (var pattern in result.Patterns)
                        {
                            var node = patternNode.AddNode($"[green]Cluster (Cohesion: {pattern.Cohesion:P0})[/]");
                            node.AddNode($"Properties: {string.Join(", ", pattern.Properties)}");

                            if (pattern.Children.Any())
                            {
                                var childNode = node.AddNode("Sub-patterns");
                                foreach (var child in pattern.Children)
                                {
                                    childNode.AddNode($"[grey]Cohesion: {child.Cohesion:P0}, Properties: {string.Join(", ", child.Properties)}[/]");
                                }
                            }
                        }

                        // Add suggestions
                        var suggestionsNode = tree.AddNode("[blue]Suggestions[/]");
                        foreach (var suggestion in result.Suggestions)
                        {
                            suggestionsNode.AddNode(
                                $"[yellow]{suggestion.Type}[/]: {suggestion.Description} " +
                                $"(Confidence: {suggestion.Confidence:P0})");
                        }

                        // Add statistics
                        var statsNode = tree.AddNode("[blue]Statistics[/]");
                        statsNode.AddNode($"Tokens: {result.Statistics.TokenCount}");
                        statsNode.AddNode($"Components: {result.Statistics.ComponentCount}");
                        statsNode.AddNode($"Patterns: {result.Statistics.PatternCount}");
                        statsNode.AddNode($"Average Cohesion: {result.Statistics.AverageCohesion:P0}");

                        if (output != null)
                        {
                            ctx.AddTask("Writing output");
                            var formatter = _services.GetRequiredService<IOutputFormatter>();
                            var formatted = formatter.Format(result.Patterns);
                            await File.WriteAllTextAsync(output.FullName, formatted);
                            console.WriteSuccess($"Analysis written to {output.FullName}");
                        }
                        else
                        {
                            AnsiConsole.Write(tree);
                        }
                    });
                }

                await AnalyzeFile();

                if (watch)
                {
                    var watcher = _services.GetRequiredService<FileWatcher>();
                    console.WriteInfo("Watching for changes...");

                    await watcher.WatchAsync(input, async () =>
                    {
                        console.WriteLine();
                        console.WriteInfo("File changed, reanalyzing...");
                        await AnalyzeFile();
                    });
                }
            }
            catch (Exception ex)
            {
                console.WriteError($"Analysis failed: {ex.Message}");
                if (verbose)
                {
                    console.WriteException(ex);
                }
                throw;
            }
        }
    }
}
