using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YCSS.Cli.Utils;

namespace YCSS.Cli.Commands
{
    public class InitCommand
    {
        private readonly IServiceProvider _services;

        public InitCommand(IServiceProvider services)
        {
            _services = services;
        }

        public Command Create()
        {
            var command = new Command("init", "Initialize a new YCSS project")
        {
            new Option<DirectoryInfo>(
                aliases: new[] { "--dir", "-d" },
                getDefaultValue: () => new DirectoryInfo(Directory.GetCurrentDirectory()),
                description: "Directory to initialize"
            ),
            new Option<string>(
                aliases: new[] { "--template", "-t" },
                getDefaultValue: () => "basic",
                description: "Template to use (basic, component-library, design-system)"
            ),
            new Option<bool>(
                "--force",
                "Overwrite existing files"
            )
        };

            command.SetHandler(HandleInit);
            return command;
        }

        private async Task HandleInit(DirectoryInfo dir, string template, bool force)
        {
            var console = _services.GetRequiredService<IConsoleWriter>();
            var progress = _services.GetRequiredService<IProgressRenderer>();

            try
            {
                await progress.RunWithProgressAsync("Initializing project", async ctx =>
                {
                    // Create project structure
                    ctx.AddTask("Creating project structure");

                    var projectFiles = template.ToLower() switch
                    {
                        "basic" => ProjectTemplates.Basic,
                        "component-library" => ProjectTemplates.ComponentLibrary,
                        "design-system" => ProjectTemplates.DesignSystem,
                        _ => throw new ArgumentException($"Unknown template: {template}")
                    };

                    foreach (var (path, content) in projectFiles)
                    {
                        var fullPath = Path.Combine(dir.FullName, path);
                        var fileDir = Path.GetDirectoryName(fullPath)!;

                        if (!Directory.Exists(fileDir))
                        {
                            Directory.CreateDirectory(fileDir);
                        }

                        if (File.Exists(fullPath) && !force)
                        {
                            throw new IOException($"File already exists: {path}. Use --force to overwrite.");
                        }

                        await File.WriteAllTextAsync(fullPath, content);
                    }

                    // Create gitignore if it doesn't exist
                    var gitignorePath = Path.Combine(dir.FullName, ".gitignore");
                    if (!File.Exists(gitignorePath) || force)
                    {
                        await File.WriteAllTextAsync(gitignorePath, ProjectTemplates.GitIgnore);
                    }

                    ctx.AddTask("Initializing git repository");
                    if (!Directory.Exists(Path.Combine(dir.FullName, ".git")))
                    {
                        await ProcessUtil.RunAsync("git", new[] { "init" }, dir.FullName);
                    }

                    console.WriteSuccess($"""
                    Project initialized successfully!
                    
                    Next steps:
                    1. Review the generated files in {dir.FullName}
                    2. Edit styles/tokens.yaml to define your design tokens
                    3. Run 'ycss build' to compile your styles
                    """);
                });
            }
            catch (Exception ex)
            {
                console.WriteError($"Initialization failed: {ex.Message}");
                throw;
            }
        }
    }
}
