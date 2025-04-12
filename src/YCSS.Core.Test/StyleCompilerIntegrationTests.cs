using YCSS.Core.Compilation;
using YCSS.Core.Models;
using YCSS.Core.Test.Providers;
using YCSS.Core.Utils;

namespace YCSS.Core.Test;

public class StyleCompilerIntegrationTests
{
    private readonly IStyleCompiler _compiler;
    private readonly YamlParser _parser;

    public StyleCompilerIntegrationTests()
    {
        _compiler = TestServiceProvider.GetService<IStyleCompiler>();
        _parser = TestServiceProvider.GetService<YamlParser>();
    }

    [Fact]
    public async Task CompileStyles_WithTokens_GeneratesCSSVariables()
    {
        // Arrange
        var yaml = await File.ReadAllTextAsync("TestData/basic-tokens.yaml");
        var (tokens, _, _) = _parser.Parse(yaml);

        var definition = new StyleDefinition
        {
            Tokens = tokens
        };

        var options = new CompilerOptions
        {
            Format = OutputFormat.CSS,
            TokensOnly = true
        };

        // Act
        var result = _compiler.CompileStyles(definition, options);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.Contains(":root {", result);
        Assert.Contains("--color-primary: #1f2937;", result);
        Assert.Contains("--spacing-md: 1rem;", result);
        Assert.Contains("--radius-md: 0.5rem;", result);
    }

    [Fact]
    public async Task CompileStyles_WithSCSSFormat_GeneratesSCSSVariables()
    {
        // Arrange
        var yaml = await File.ReadAllTextAsync("TestData/basic-tokens.yaml");
        var (tokens, _, _) = _parser.Parse(yaml);

        var definition = new StyleDefinition
        {
            Tokens = tokens
        };

        var options = new CompilerOptions
        {
            Format = OutputFormat.SCSS,
            TokensOnly = true
        };

        // Act
        var result = _compiler.CompileStyles(definition, options);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.Contains("$color-primary: #1f2937;", result);
        Assert.Contains("$spacing-md: 1rem;", result);
        Assert.Contains("$radius-md: 0.5rem;", result);
    }

    [Fact]
    public async Task CompileStyles_WithFullDesignSystem_GeneratesCompleteCSS()
    {
        // Arrange
        var yaml = await File.ReadAllTextAsync("TestData/complex-design-system.yaml");
        var (tokens, components, styles) = _parser.Parse(yaml);

        var definition = new StyleDefinition
        {
            Tokens = tokens,
            Components = components,
            StreetStyles = styles
        };

        var options = new CompilerOptions
        {
            Format = OutputFormat.CSS
        };

        // Act
        var result = _compiler.CompileStyles(definition, options);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);

        // Verify tokens
        Assert.Contains(":root {", result);
        Assert.Contains("--color-primary: #1f2937;", result);

        // Verify components
        Assert.Contains(".button {", result);
        Assert.Contains(".card {", result);
        Assert.Contains(".alert {", result);

        // Verify variants
        Assert.Contains(".button--primary {", result);
        Assert.Contains(".card--bordered {", result);
        Assert.Contains(".alert--success {", result);

        // Verify parts
        Assert.Contains(".card__header {", result);
        Assert.Contains(".card__body {", result);

        // Verify street styles
        Assert.Contains(".text-primary {", result);
        Assert.Contains(".bg-primary {", result);
        Assert.Contains(".flex-row {", result);
    }

    [Fact]
    public async Task CompileStyles_WithThemeOption_AppliesThemeCorrectly()
    {
        // Arrange
        var yaml = await File.ReadAllTextAsync("TestData/complex-design-system.yaml");
        var (tokens, components, styles) = _parser.Parse(yaml);

        // Add theme overrides manually to test theme compilation
        tokens["color-primary"].ThemeOverrides["dark"] = "#93c5fd";
        tokens["color-secondary"].ThemeOverrides["dark"] = "#9ca3af";

        var definition = new StyleDefinition
        {
            Tokens = tokens,
            Components = components,
            StreetStyles = styles
        };

        var options = new CompilerOptions
        {
            Format = OutputFormat.CSS,
            Theme = "dark"
        };

        // Act
        var result = _compiler.CompileStyles(definition, options);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);

        // Verify theme overrides are included
        Assert.Contains("--color-primary: #1f2937;", result);
        Assert.Contains("--dark-color-primary: #93c5fd;", result);
        Assert.Contains("--color-secondary: #4b5563;", result);
        Assert.Contains("--dark-color-secondary: #9ca3af;", result);
    }

    [Fact]
    public async Task CompileStyles_WithTokensOnly_GeneratesOnlyTokens()
    {
        // Arrange
        var yaml = await File.ReadAllTextAsync("TestData/complex-design-system.yaml");
        var (tokens, components, styles) = _parser.Parse(yaml);

        var definition = new StyleDefinition
        {
            Tokens = tokens,
            Components = components,
            StreetStyles = styles
        };

        var options = new CompilerOptions
        {
            Format = OutputFormat.CSS,
            TokensOnly = true
        };

        // Act
        var result = _compiler.CompileStyles(definition, options);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);

        // Verify tokens are included
        Assert.Contains(":root {", result);
        Assert.Contains("--color-primary: #1f2937;", result);

        // Verify components and styles are not included
        Assert.DoesNotContain(".button {", result);
        Assert.DoesNotContain(".card {", result);
        Assert.DoesNotContain(".text-primary {", result);
    }
}