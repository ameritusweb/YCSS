using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YCSS.Core.Compilation;
using YCSS.Core.Exceptions;
using YCSS.Core.Pipeline;
using YCSS.Core.Test.Providers;

namespace YCSS.Core.Test
{
    public class PipelineIntegrationTests
    {
        private readonly IStylePipeline _pipeline;

        public PipelineIntegrationTests()
        {
            _pipeline = TestServiceProvider.GetService<IStylePipeline>();
        }

        [Fact]
        public async Task CompileAsync_BasicTokens_GeneratesCSSVariables()
        {
            // Arrange
            var yaml = await File.ReadAllTextAsync("TestData/basic-tokens.yaml");
            var options = new CompilerOptions
            {
                Format = OutputFormat.CSS,
                TokensOnly = true
            };

            // Act
            var result = await _pipeline.CompileAsync(yaml, options);

            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result.Output);
            Assert.Contains(":root {", result.Output);
            Assert.Contains("--color-primary: #1f2937;", result.Output);
            Assert.Contains("--spacing-md: 1rem;", result.Output);
            Assert.Contains("--radius-md: 0.5rem;", result.Output);
            Assert.Equal(9, result.Statistics.TokenCount); // Verify all tokens were processed
        }

        [Fact]
        public async Task CompileAsync_BasicComponent_GeneratesComponentCSS()
        {
            // Arrange
            var yaml = await File.ReadAllTextAsync("TestData/basic-component.yaml");
            var options = new CompilerOptions
            {
                Format = OutputFormat.CSS
            };

            // Act
            var result = await _pipeline.CompileAsync(yaml, options);

            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result.Output);
            Assert.Contains(".button {", result.Output);
            Assert.Contains("background-color: var(--color-primary);", result.Output);
            Assert.Contains(".button--primary {", result.Output);
            Assert.Contains(".button--secondary {", result.Output);
        }

        [Fact]
        public async Task CompileAsync_ComplexDesignSystem_GeneratesFullCSS()
        {
            // Arrange
            var yaml = await File.ReadAllTextAsync("TestData/complex-design-system.yaml");
            var options = new CompilerOptions
            {
                Format = OutputFormat.CSS
            };

            // Act
            var result = await _pipeline.CompileAsync(yaml, options);

            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result.Output);

            // Verify tokens were processed
            Assert.Contains(":root {", result.Output);
            Assert.Contains("--color-primary: #1f2937;", result.Output);

            // Verify components were processed
            Assert.Contains(".button {", result.Output);
            Assert.Contains(".card {", result.Output);
            Assert.Contains(".alert {", result.Output);

            // Verify component variants
            Assert.Contains(".button--primary {", result.Output);
            Assert.Contains(".button--secondary {", result.Output);
            Assert.Contains(".button--outline {", result.Output);

            // Verify component parts
            Assert.Contains(".card__header {", result.Output);
            Assert.Contains(".card__body {", result.Output);
            Assert.Contains(".card__footer {", result.Output);

            // Verify street styles
            Assert.Contains(".text-primary {", result.Output);
            Assert.Contains(".bg-primary {", result.Output);
            Assert.Contains(".flex-row {", result.Output);

            // Verify statistics
            Assert.Equal(32, result.Statistics.TokenCount);
            Assert.Equal(3, result.Statistics.ComponentCount);
        }

        [Fact]
        public async Task CompileAsync_WithSCSSFormat_GeneratesSCSSOutput()
        {
            // Arrange
            var yaml = await File.ReadAllTextAsync("TestData/basic-tokens.yaml");
            var options = new CompilerOptions
            {
                Format = OutputFormat.SCSS,
                TokensOnly = true
            };

            // Act
            var result = await _pipeline.CompileAsync(yaml, options);

            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result.Output);

            // Verify SCSS variables format
            Assert.Contains("$color-primary: #1f2937;", result.Output);
            Assert.Contains("$spacing-md: 1rem;", result.Output);
            Assert.Contains("$radius-md: 0.5rem;", result.Output);

            // Does not contain CSS format
            Assert.DoesNotContain(":root {", result.Output);
            Assert.DoesNotContain("--color-primary", result.Output);
        }

        [Fact]
        public async Task CompileAsync_WithOptimize_MinifiesOutput()
        {
            // Arrange
            var yaml = await File.ReadAllTextAsync("TestData/basic-component.yaml");
            var options = new CompilerOptions
            {
                Format = OutputFormat.CSS,
                Optimize = true
            };

            // Act
            var result = await _pipeline.CompileAsync(yaml, options);

            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result.Output);

            // Check for minified output (no extra whitespace)
            Assert.DoesNotContain("  ", result.Output); // No indentation
            Assert.DoesNotContain("\n\n", result.Output); // No double line breaks

            // Basic syntax validation
            Assert.Contains(".button{", result.Output);
            Assert.Contains("background-color:var(--color-primary);", result.Output);
        }

        [Fact]
        public async Task CompileAsync_InvalidYAML_ThrowsYCSSValidationException()
        {
            // Arrange
            var invalidYaml = @"
        tokens:
          color-primary: ""#1f2937
          - This is invalid YAML syntax
        ";

            var options = new CompilerOptions
            {
                Format = OutputFormat.CSS
            };

            // Act & Assert
            await Assert.ThrowsAsync<YCSSValidationException>(() =>
                _pipeline.CompileAsync(invalidYaml, options));
        }

        [Fact]
        public async Task AnalyzeAsync_WithDuplicationPatterns_DetectsPatterns()
        {
            // Arrange
            var yaml = await File.ReadAllTextAsync("TestData/duplication-patterns.yaml");

            // Act
            var result = await _pipeline.AnalyzeAsync(yaml);

            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result.Patterns);

            // Verify pattern detection
            Assert.True(result.Patterns.Count > 0);

            // Verify statistics
            Assert.True(result.Statistics.PatternCount > 0);
            Assert.True(result.Statistics.AverageCohesion > 0);

            // Verify suggestions
            Assert.NotEmpty(result.Suggestions);
        }

        [Fact]
        public async Task AnalyzeAsync_WithComplexDesignSystem_DetectsAdvancedPatterns()
        {
            // Arrange
            var yaml = await File.ReadAllTextAsync("TestData/complex-design-system.yaml");

            // Act
            var result = await _pipeline.AnalyzeAsync(yaml);

            // Assert
            Assert.NotNull(result);

            // Verify pattern detection
            Assert.True(result.Patterns.Count > 0);

            // Verify statistics
            Assert.Equal(32, result.Statistics.TokenCount);
            Assert.Equal(3, result.Statistics.ComponentCount);
            Assert.True(result.Statistics.PatternCount > 0);
        }
    }
}
