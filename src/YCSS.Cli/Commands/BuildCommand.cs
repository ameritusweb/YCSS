using Microsoft.Extensions.DependencyInjection;
using System;
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
    public class BuildCommand
    {
        private readonly IServiceProvider _services;

        public BuildCommand(IServiceProvider services)
        {
            _services = services;
        }

        public Command Create()
        {
            var command = new Command("build", "Compile YAML to CSS/SCSS")
        {
            CommonOptions.InputFile,
            CommonOptions.OutputFile,
            CommonOptions.Format,
            CommonOptions.Watch,
            CommonOptions.Minify,
            CommonOptions.Theme,
            CommonOptions.Verbose
        };

            command.SetHandler(HandleBuild);
            return command;
        }

        private async Task HandleBuild(
            FileInfo input,
            FileInfo? output,
            string format,
            bool watch,
            bool minify,
            string? theme,
            bool verbose)
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

                async Task CompileFile()
                {
                    await progress.RunWithProgressAsync("Compiling styles", async ctx =>
                    {
                        ctx.AddTask("Reading input file");
                        var yaml = await File.ReadAllTextAsync(input.FullName);

                        ctx.AddTask("Compiling styles");
                        var options = new CompilerOptions
                        {
                            Format = ParseFormat(format),
                            Optimize = minify,
                            Theme = theme
                        };

                        var result = await pipeline.CompileAsync(yaml, options);

                        ctx.AddTask("Writing output");
                        if (output != null)
                        {
                            await File.WriteAllTextAsync(output.FullName, result.Output);
                            console.WriteSuccess($"Output written to {output.FullName}");
                        }
                        else
                        {
                            console.WriteLine(result.Output);
                        }

                        if (verbose)
                        {
                            console.WriteInfo(new Panel(
                                $"""
                            Compilation Statistics:
                            - Tokens: {result.Statistics.TokenCount}
                            - Components: {result.Statistics.ComponentCount}
                            - Output Size: {result.Statistics.OutputSize:N0} bytes
                            """)
                                .Header("Statistics")
                                .Expand());
                        }
                    });
                }

                await CompileFile();

                if (watch)
                {
                    var watcher = _services.GetRequiredService<FileWatcher>();
                    console.WriteInfo("Watching for changes...");

                    await watcher.WatchAsync(input, async () =>
                    {
                        console.WriteLine();
                        console.WriteInfo("File changed, recompiling...");
                        await CompileFile();
                    });
                }
            }
            catch (Exception ex)
            {
                console.WriteError($"Build failed: {ex.Message}");
                if (verbose)
                {
                    console.WriteException(ex);
                }
                throw;
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
