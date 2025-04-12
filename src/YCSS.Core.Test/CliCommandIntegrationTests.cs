using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YCSS.Cli.Commands;
using YCSS.Cli.Utils;
using YCSS.Core.Interfaces;
using YCSS.Core.Pipeline;
using YCSS.Core.Test.Renderers;
using YCSS.Core.Test.Writers;

namespace YCSS.Core.Test
{
    /// <summary>
    /// Tests for CLI command functionality.
    /// These tests verify that CLI commands work correctly with the actual services.
    /// </summary>
    public class CliCommandIntegrationTests : IDisposable
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly string _testOutputDir;
        private readonly IConsoleWriter _fakeConsole;

        public CliCommandIntegrationTests()
        {
            // Create a service collection with both Core and CLI services
            var services = new ServiceCollection();

            // Add logging
            services.AddLogging(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Debug);
            });

            // Add core YCSS services
            services.AddYCSS();

            // Add CLI-specific services
            services.AddSingleton<IConsoleWriter, TestConsoleWriter>();
            services.AddSingleton<IProgressRenderer, TestProgressRenderer>();
            services.AddSingleton<FileWatcher>();

            _serviceProvider = services.BuildServiceProvider();
            _fakeConsole = _serviceProvider.GetRequiredService<IConsoleWriter>();

            // Create a temporary directory for test outputs
            _testOutputDir = Path.Combine(Path.GetTempPath(), $"ycss_test_{Guid.NewGuid()}");
            Directory.CreateDirectory(_testOutputDir);
        }

        [Fact]
        public async Task BuildCommand_WithBasicTokens_GeneratesExpectedOutput()
        {
            // Arrange
            var buildCommand = new BuildCommand(_serviceProvider);
            var inputFile = new FileInfo("TestData/basic-tokens.yaml");
            var outputFile = new FileInfo(Path.Combine(_testOutputDir, "tokens.css"));

            // Act
            await buildCommand.HandleBuild(
                inputFile,
                outputFile,
                "css",
                false,
                false,
                null,
                false);

            // Assert
            Assert.True(File.Exists(outputFile.FullName));
            var content = await File.ReadAllTextAsync(outputFile.FullName);

            Assert.Contains(":root {", content);
            Assert.Contains("--color-primary: #1f2937;", content);
            Assert.Contains("--spacing-md: 1rem;", content);
            Assert.Contains("--radius-md: 0.5rem;", content);
        }

        [Fact]
        public async Task BuildCommand_WithSCSSFormat_GeneratesSCSSOutput()
        {
            // Arrange
            var buildCommand = new BuildCommand(_serviceProvider);
            var inputFile = new FileInfo("TestData/basic-tokens.yaml");
            var outputFile = new FileInfo(Path.Combine(_testOutputDir, "tokens.scss"));

            // Act
            await buildCommand.HandleBuild(
                inputFile,
                outputFile,
                "scss",
                false,
                false,
                null,
                false);

            // Assert
            Assert.True(File.Exists(outputFile.FullName));
            var content = await File.ReadAllTextAsync(outputFile.FullName);

            Assert.Contains("$color-primary: #1f2937;", content);
            Assert.Contains("$spacing-md: 1rem;", content);
            Assert.Contains("$radius-md: 0.5rem;", content);
        }

        [Fact]
        public async Task BuildCommand_WithMinifyOption_GeneratesMinifiedOutput()
        {
            // Arrange
            var buildCommand = new BuildCommand(_serviceProvider);
            var inputFile = new FileInfo("TestData/basic-component.yaml");
            var outputFile = new FileInfo(Path.Combine(_testOutputDir, "component.min.css"));

            // Act
            await buildCommand.HandleBuild(
                inputFile,
                outputFile,
                "css",
                false,
                true, // minify
                null,
                false);

            // Assert
            Assert.True(File.Exists(outputFile.FullName));
            var content = await File.ReadAllTextAsync(outputFile.FullName);

            // Check for minification characteristics
            Assert.DoesNotContain("  ", content); // No indentation
            Assert.Contains(".button{", content); // No space after selector
            Assert.Contains("background-color:var(--color-primary);", content); // No space around colon
        }

        [Fact]
        public async Task TokensCommand_ExportBasicTokens_GeneratesTokensOnly()
        {
            // Arrange
            var tokensCommand = new TokensCommand(_serviceProvider);
            var inputFiles = new[] { new FileInfo("TestData/basic-tokens.yaml") };
            var outputFile = new FileInfo(Path.Combine(_testOutputDir, "tokens-only.css"));

            // Act
            await tokensCommand.HandleTokens(
                inputFiles,
                "css",
                outputFile,
                "export",
                false);

            // Assert
            Assert.True(File.Exists(outputFile.FullName));
            var content = await File.ReadAllTextAsync(outputFile.FullName);

            Assert.Contains(":root {", content);
            Assert.Contains("--color-primary: #1f2937;", content);
            Assert.Contains("--spacing-md: 1rem;", content);
        }

        [Fact]
        public async Task AnalyzeCommand_WithDuplicationPatterns_GeneratesAnalysisOutput()
        {
            // Arrange
            var analyzeCommand = new AnalyzeCommand(_serviceProvider);
            var inputFile = new FileInfo("TestData/duplication-patterns.yaml");
            var outputFile = new FileInfo(Path.Combine(_testOutputDir, "analysis.json"));

            // Act
            await analyzeCommand.HandleAnalyze(
                inputFile,
                outputFile,
                "json",
                false,
                false,
                true); // no cache

            // Assert
            Assert.True(File.Exists(outputFile.FullName));
            var content = await File.ReadAllTextAsync(outputFile.FullName);

            // Basic validation of JSON output
            Assert.StartsWith("[", content.Trim());
            Assert.EndsWith("]", content.Trim());

            // Check for pattern analysis content
            Assert.Contains("\"Properties\":", content);
            Assert.Contains("\"Cohesion\":", content);
            Assert.Contains("\"Frequency\":", content);
        }

        [Fact]
        public async Task InitCommand_BasicTemplate_CreatesExpectedFiles()
        {
            // Arrange
            var initCommand = new InitCommand(_serviceProvider);
            var outputDir = new DirectoryInfo(Path.Combine(_testOutputDir, "init-test"));

            if (!outputDir.Exists)
            {
                outputDir.Create();
            }

            // Act
            await initCommand.HandleInit(
                outputDir,
                "basic",
                true); // force overwrite

            // Assert
            Assert.True(File.Exists(Path.Combine(outputDir.FullName, "ycss.config.yaml")));
            Assert.True(File.Exists(Path.Combine(outputDir.FullName, "styles/tokens.yaml")));
            Assert.True(File.Exists(Path.Combine(outputDir.FullName, "styles/components/button.yaml")));
            Assert.True(File.Exists(Path.Combine(outputDir.FullName, ".gitignore")));

            // Verify content of created files
            var tokensContent = await File.ReadAllTextAsync(Path.Combine(outputDir.FullName, "styles/tokens.yaml"));
            Assert.Contains("colors:", tokensContent);
            Assert.Contains("primary: \"#1f2937\"", tokensContent);
        }

        public void Dispose()
        {
            // Clean up the test output directory
            if (Directory.Exists(_testOutputDir))
            {
                Directory.Delete(_testOutputDir, recursive: true);
            }
        }
    }
}
