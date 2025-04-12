using System.CommandLine.Builder;
using System.CommandLine.Invocation;
using System.CommandLine;
using YCSS.Cli.Commands;
using YCSS.Cli.Common;
using YCSS.Cli.Utils;
using System.CommandLine.Parsing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace YCSS.CLI
{
    class Program
    {
        public static async Task<int> Main(string[] args)
        {
            try
            {
                // Setup dependency injection
                var services = new ServiceCollection();
                ConfigureServices(services);

                var serviceProvider = services.BuildServiceProvider();

                // Build command line interface
                var rootCommand = BuildRootCommand(serviceProvider);
                var parser = new CommandLineBuilder(rootCommand)
                    .UseDefaults()
                    .UseExceptionHandler(HandleException)
                    .Build();

                return await parser.InvokeAsync(args);
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine("[red]Fatal error:[/]");
                AnsiConsole.WriteException(ex, ExceptionFormats.ShortenPaths);
                return 1;
            }
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            // Add logging
            services.AddLogging(builder =>
            {
                builder.AddConsole();
                builder.AddDebug();
                builder.SetMinimumLevel(LogLevel.Information);
            });

            // Add YCSS core services
            services.AddYCSS(builder =>
            {
                builder.ConfigureAnalysis(options =>
                {
                    options.MinimumCohesion = 0.5;
                    options.MinimumFrequency = 2;
                    options.MaxDepth = 3;
                });
            });

            // Add CLI services
            services.AddSingleton<IConsoleWriter, SpectreConsoleWriter>();
            services.AddSingleton<IProgressRenderer, SpectreProgressRenderer>();
            services.AddSingleton<FileWatcher>();
        }

        private static RootCommand BuildRootCommand(IServiceProvider services)
        {
            var rootCommand = new RootCommand("YCSS - YAML CSS Compiler and Analyzer")
        {
            // Common options
            CommonOptions.Verbose,
            CommonOptions.NoColor,
            CommonOptions.ConfigFile
        };

            // Add commands
            rootCommand.AddCommand(new BuildCommand(services).Create());
            rootCommand.AddCommand(new AnalyzeCommand(services).Create());
            rootCommand.AddCommand(new WatchCommand(services).Create());
            rootCommand.AddCommand(new InitCommand(services).Create());
            rootCommand.AddCommand(new TokensCommand(services).Create());
            return rootCommand;
        }

        private static void HandleException(Exception exception, InvocationContext context)
        {
            var console = context.Console.GetService<IConsoleWriter>();
            console.WriteError(exception);
            context.ExitCode = 1;
        }
    }
}