using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YCSS.Cli.Common;
using YCSS.Cli.Utils;
using YCSS.Core.Exceptions;
using YCSS.Core.Validation;

namespace YCSS.Cli.Commands
{
    public class ValidateCommand
    {
        private readonly IServiceProvider _services;

        public ValidateCommand(IServiceProvider services)
        {
            _services = services;
        }

        public Command Create()
        {
            var command = new Command("validate", "Validate YCSS files")
        {
            CommonOptions.InputFile,
            new Option<bool>(
                "--strict",
                "Enable strict validation"
            ),
            new Option<bool>(
                "--fix",
                "Try to fix common issues"
            )
            };

            command.SetHandler(HandleValidate);
            return command;
        }

        private async Task HandleValidate(FileInfo input, bool strict, bool fix)
        {
            var console = _services.GetRequiredService<IConsoleWriter>();
            var validator = _services.GetRequiredService<IStyleValidator>();

            try
            {
                if (!input.Exists)
                {
                    throw new FileNotFoundException("Input file not found", input.FullName);
                }

                var yaml = await File.ReadAllTextAsync(input.FullName);
                var result = await validator.ValidateAsync(yaml);

                if (result.IsValid && !result.Errors.Any())
                {
                    console.WriteSuccess("Validation passed! No issues found.");
                    return;
                }

                // Group issues by severity
                var grouped = result.Errors
                    .GroupBy(e => e.Severity)
                    .ToDictionary(g => g.Key, g => g.ToList());

                var table = new Table()
                    .AddColumn("Severity")
                    .AddColumn("Property")
                    .AddColumn("Message");

                var hasErrors = false;

                if (grouped.TryGetValue(ValidationSeverity.Error, out var errors))
                {
                    hasErrors = true;
                    foreach (var error in errors)
                    {
                        table.AddRow(
                            "[red]Error[/]",
                            error.Property,
                            error.Message);
                    }
                }

                if (grouped.TryGetValue(ValidationSeverity.Warning, out var warnings))
                {
                    foreach (var warning in warnings)
                    {
                        table.AddRow(
                            "[yellow]Warning[/]",
                            warning.Property,
                            warning.Message);
                    }
                }

                console.WriteLine();
                console.WriteLine(new Panel(table)
                    .Header("Validation Results")
                    .Expand());

                if (fix && (errors?.Any() == true || warnings?.Any() == true))
                {
                    console.WriteLine();
                    console.WriteInfo("Attempting to fix issues...");

                    var fixes = new List<(string Original, string Fixed)>();
                    var yaml = await File.ReadAllTextAsync(input.FullName);
                    var fixed1 = false;

                    // Apply fixes
                    foreach (var error in result.Errors)
                    {
                        if (TryFixIssue(error, yaml, out var fixedYaml))
                        {
                            yaml = fixedYaml;
                            fixes.Add((error.Message, "Fixed"));
                            fixed1 = true;
                        }
                        else
                        {
                            fixes.Add((error.Message, "Could not fix automatically"));
                        }
                    }

                    if (fixed1)
                    {
                        var backup = input.FullName + ".bak";
                        await File.WriteAllTextAsync(backup, yaml);
                        await File.WriteAllTextAsync(input.FullName, yaml);

                        console.WriteSuccess($"""
                        Fixed some issues automatically:
                        • Backup saved to {backup}
                        • Updated {input.FullName}
                        """);

                        foreach (var (original, status) in fixes)
                        {
                            console.WriteLine($"  • {original}: {status}");
                        }
                    }
                    else
                    {
                        console.WriteWarning("Could not fix issues automatically");
                    }
                }

                if (hasErrors && strict)
                {
                    throw new ValidationException("Validation failed");
                }
            }
            catch (Exception ex)
            {
                console.WriteError($"Validation failed: {ex.Message}");
                throw;
            }
        }

        private bool TryFixIssue(ValidationError error, string yaml, out string fixed1)
        {
            fixed1 = yaml;

            // Add common fixes here
            // For example:
            // - Remove trailing whitespace
            // - Fix indentation
            // - Add missing quotes around values
            // - Normalize color values
            // - etc.

            return false;
        }
    }
}
