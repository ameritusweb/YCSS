using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YCSS.Core.Compilation;
using YCSS.Core.Compilation.Formatters;
using YCSS.Core.Models;
using YCSS.Core.Test.Providers;
using YCSS.Core.Utils;

namespace YCSS.Core.Test
{
    public class FormattersIntegrationTests
    {
        private readonly YamlParser _parser;
        private readonly IServiceProvider _serviceProvider;

        public FormattersIntegrationTests()
        {
            _parser = TestServiceProvider.GetService<YamlParser>();
            _serviceProvider = TestServiceProvider.Instance;
        }

        [Fact]
        public async Task CssFormatter_Format_GeneratesValidCSS()
        {
            // Arrange
            var logger = _serviceProvider.GetRequiredService<ILogger<CssFormatter>>();
            var formatter = new CssFormatter(logger);

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
            var result = formatter.Format(definition, options);

            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result);

            // Verify standard CSS structure
            Assert.Contains(":root {", result);
            Assert.Contains("--color-primary: #1f2937;", result);

            // Verify component CSS
            Assert.Contains(".button {", result);
            Assert.Contains("background-color: var(--color-primary);", result);

            // Verify states are properly formatted
            Assert.Contains(".button:hover {", result);
            Assert.Contains("transform: translateY(-1px);", result);

            // Verify no SCSS syntax
            Assert.DoesNotContain("$color-primary", result);
            Assert.DoesNotContain("@mixin", result);
            Assert.DoesNotContain("&:", result);
        }

        [Fact]
        public async Task ScssFormatter_Format_GeneratesValidSCSS()
        {
            // Arrange
            var logger = _serviceProvider.GetRequiredService<ILogger<ScssFormatter>>();
            var formatter = new ScssFormatter(logger);

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
                Format = OutputFormat.SCSS
            };

            // Act
            var result = formatter.Format(definition, options);

            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result);

            // Verify SCSS variables
            Assert.Contains("$color-primary: #1f2937;", result);
            Assert.Contains("$spacing-md: 1rem;", result);

            // Verify SCSS mixins
            Assert.Contains("@mixin theme(", result);

            // Verify nested syntax
            Assert.Contains("&:hover {", result);
            Assert.Contains("&--primary {", result);

            // Does not contain CSS syntax
            Assert.DoesNotContain(":root {", result);
            Assert.DoesNotContain("--color-primary:", result);
        }

        [Fact]
        public async Task TailwindFormatter_Format_GeneratesValidTailwindConfig()
        {
            // Arrange
            var logger = _serviceProvider.GetRequiredService<ILogger<TailwindFormatter>>();
            var formatter = new TailwindFormatter(logger);

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
                Format = OutputFormat.Tailwind
            };

            // Act
            var result = formatter.Format(definition, options);

            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result);

            // Verify Tailwind configuration structure
            Assert.Contains("/** @type {import('tailwindcss').Config} */", result);
            Assert.Contains("module.exports = {", result);
            Assert.Contains("theme: {", result);

            // Verify Tailwind theme extension
            Assert.Contains("colors: {", result);
            Assert.Contains("'primary': '#1f2937',", result);

            // Verify plugin for components
            Assert.Contains("plugins: [", result);
            Assert.Contains("plugin(function({ addComponents })", result);

            // Verify component classes
            Assert.Contains("'.button': {", result);
            Assert.Contains("backgroundColor: '#1f2937',", result);
        }
    }
}
