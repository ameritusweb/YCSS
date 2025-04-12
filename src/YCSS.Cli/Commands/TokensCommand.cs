using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Serialization;
using YCSS.Cli.Common;
using YCSS.Cli.Utils;

namespace YCSS.Cli.Commands
{
    public class TokensCommand
    {
        private readonly IServiceProvider _services;

        public TokensCommand(IServiceProvider services)
        {
            _services = services;
        }

        public Command Create()
        {
            var command = new Command("tokens", "Manage design tokens")
        {
            CommonOptions.Format,
            CommonOptions.OutputFile,
            CommonOptions.Verbose,
            new Option<FileInfo[]>(
                aliases: new[] { "--files", "-f" },
                description: "Input token files"
            ) { AllowMultipleArgumentsPerToken = true },
            new Option<string>(
                "--action",
                getDefaultValue: () => "export",
                description: "Action to perform (export, validate, merge)"
            )
        };

            command.SetHandler(HandleTokens);
            return command;
        }

        private async Task HandleTokens(
            FileInfo[] files,
        string format,
        FileInfo? output,
            string action,
            bool verbose)
        {
            var console = _services.GetRequiredService<IConsoleWriter>();
            var progress = _services.GetRequiredService<IProgressRenderer>();

            try
            {
                await progress.RunWithProgressAsync("Processing tokens", async ctx =>
                {
                    // Read and merge token files
                    ctx.AddTask("Reading token files");
                    var tokens = new Dictionary<string, object>();

                    foreach (var file in files)
                    {
                        if (!file.Exists)
                        {
                            throw new FileNotFoundException($"Token file not found: {file.Name}");
                        }

                        var yaml = await File.ReadAllTextAsync(file.FullName);
                        var deserializer = new DeserializerBuilder().Build();
                        var fileTokens = deserializer.Deserialize<Dictionary<string, object>>(yaml);

                        // Merge tokens
                        foreach (var (key, value) in fileTokens)
                        {
                            tokens[key] = value;
                        }
                    }

                    switch (action.ToLower())
                    {
                        case "export":
                            await ExportTokens(tokens, format, output, console);
                            break;

                        case "validate":
                            await ValidateTokens(tokens, console);
                            break;

                        case "merge":
                            await MergeTokens(tokens, format, output, console);
                            break;

                        default:
                            throw new ArgumentException($"Unknown action: {action}");
                    }

                    if (verbose)
                    {
                        // Display token statistics
                        var stats = new Dictionary<string, int>();
                        CountTokens(tokens, "", stats);

                        var table = new Table()
                            .AddColumn("Category")
                            .AddColumn("Count");

                        foreach (var (category, count) in stats)
                        {
                            table.AddRow(category, count.ToString());
                        }

                        console.WriteInfo(new Panel(table)
                            .Header("Token Statistics")
                            .Expand());
                    }
                });
            }
            catch (Exception ex)
            {
                console.WriteError($"Token processing failed: {ex.Message}");
                throw;
            }
        }

        private static async Task ExportTokens(
            Dictionary<string, object> tokens,
            string format,
            FileInfo? output,
            IConsoleWriter console)
        {
            var result = format.ToLower() switch
            {
                "css" => GenerateCSSVariables(tokens),
                "scss" => GenerateScssVariables(tokens),
                "json" => GenerateJsonVariables(tokens),
                _ => throw new ArgumentException($"Unsupported format: {format}")
            };

            if (output != null)
            {
                await File.WriteAllTextAsync(output.FullName, result);
                console.WriteSuccess($"Tokens exported to {output.FullName}");
            }
            else
            {
                console.WriteLine(result);
            }
        }

        private static async Task ValidateTokens(
            Dictionary<string, object> tokens,
            IConsoleWriter console)
        {
            var errors = new List<string>();
            ValidateTokensRecursive(tokens, "", errors);

            if (errors.Any())
            {
                console.WriteError("Token validation failed:");
                foreach (var error in errors)
                {
                    console.WriteLine($"  • {error}");
                }
            }
            else
            {
                console.WriteSuccess("All tokens are valid");
            }
        }

        private static async Task MergeTokens(
            Dictionary<string, object> tokens,
            string format,
            FileInfo? output,
            IConsoleWriter console)
        {
            var serializer = new SerializerBuilder()
                .WithQuotingNecessaryStrings()
                .Build();

            var merged = serializer.Serialize(tokens);

            if (output != null)
            {
                await File.WriteAllTextAsync(output.FullName, merged);
                console.WriteSuccess($"Merged tokens written to {output.FullName}");
            }
            else
            {
                console.WriteLine(merged);
            }
        }

        private static string GenerateCSSVariables(
            Dictionary<string, object> tokens,
            string prefix = "")
        {
            var sb = new StringBuilder();
            sb.AppendLine(":root {");

            void AddVariables(Dictionary<string, object> dict, string currentPrefix)
            {
                foreach (var (key, value) in dict)
                {
                    var name = string.IsNullOrEmpty(currentPrefix) ? key : $"{currentPrefix}-{key}";

                    if (value is Dictionary<object, object> nested)
                    {
                        AddVariables(
                            nested.ToDictionary(
                                kvp => kvp.Key.ToString()!,
                                kvp => kvp.Value),
                            name);
                    }
                    else
                    {
                        sb.AppendLine($"  --{name}: {value};");
                    }
                }
            }

            AddVariables(tokens, prefix);
            sb.AppendLine("}");

            return sb.ToString();
        }

        private static string GenerateScssVariables(
            Dictionary<string, object> tokens,
            string prefix = "")
        {
            var sb = new StringBuilder();

            void AddVariables(Dictionary<string, object> dict, string currentPrefix)
            {
                foreach (var (key, value) in dict)
                {
                    var name = string.IsNullOrEmpty(currentPrefix) ? key : $"{currentPrefix}-{key}";

                    if (value is Dictionary<object, object> nested)
                    {
                        AddVariables(
                            nested.ToDictionary(
                                kvp => kvp.Key.ToString()!,
                                kvp => kvp.Value),
                            name);
                    }
                    else
                    {
                        sb.AppendLine($"${name}: {value};");
                    }
                }
            }

            AddVariables(tokens, prefix);
            return sb.ToString();
        }

        private static string GenerateJsonVariables(Dictionary<string, object> tokens)
        {
            return System.Text.Json.JsonSerializer.Serialize(
                tokens,
                new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                });
        }

        private static void ValidateTokensRecursive(
            Dictionary<string, object> tokens,
            string path,
            List<string> errors)
        {
            foreach (var (key, value) in tokens)
            {
                var currentPath = string.IsNullOrEmpty(path) ? key : $"{path}.{key}";

                if (string.IsNullOrWhiteSpace(key))
                {
                    errors.Add($"Empty key at {path}");
                    continue;
                }

                if (value == null)
                {
                    errors.Add($"Null value at {currentPath}");
                    continue;
                }

                if (value is Dictionary<object, object> nested)
                {
                    ValidateTokensRecursive(
                        nested.ToDictionary(
                            kvp => kvp.Key.ToString()!,
                            kvp => kvp.Value),
                        currentPath,
                        errors);
                }
            }
        }

        private static void CountTokens(
            Dictionary<string, object> tokens,
            string path,
            Dictionary<string, int> stats)
        {
            foreach (var (key, value) in tokens)
            {
                var category = string.IsNullOrEmpty(path) ? key : path;

                if (value is Dictionary<object, object> nested)
                {
                    CountTokens(
                        nested.ToDictionary(
                            kvp => kvp.Key.ToString()!,
                            kvp => kvp.Value),
                        category,
                        stats);
                }
                else
                {
                    if (!stats.ContainsKey(category))
                    {
                        stats[category] = 0;
                    }
                    stats[category]++;
                }
            }
        }
    }
}
