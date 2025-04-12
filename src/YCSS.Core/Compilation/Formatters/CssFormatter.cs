using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YCSS.Core.Compilation.Formatters
{
    public class CssFormatter : IStyleFormatter
    {
        private readonly ILogger<CssFormatter> _logger;

        public CssFormatter(ILogger<CssFormatter> logger)
        {
            _logger = logger;
        }

        public string Format(StyleDefinition definition, CompilerOptions options)
        {
            var sb = new StringBuilder();
            var context = new FormatterContext(
                Variables: definition.Tokens,
                Minify: options.Optimize,
                Theme: options.Theme,
                IncludeSourceMap: options.GenerateSourceMap
            );

            try
            {
                // Write CSS variables
                WriteVariables(sb, definition.Tokens, context);

                // Write component styles
                foreach (var (name, component) in definition.Components)
                {
                    WriteComponent(sb, name, component, context);
                }

                // Write utilities if requested
                if (options.UseUtilities)
                {
                    WriteUtilities(sb, definition, context);
                }

                var css = sb.ToString();

                // Apply minification if requested
                if (context.Minify)
                {
                    css = MinifyCss(css);
                }

                return css;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error formatting CSS");
                throw;
            }
        }

        private void WriteVariables(
            StringBuilder sb,
            Dictionary<string, string> tokens,
            FormatterContext context)
        {
            if (!tokens.Any()) return;

            sb.AppendLine(":root {");
            foreach (var (name, value) in tokens)
            {
                sb.AppendLine(context.Minify
                    ? $"--{name}:{value};"
                    : $"  --{name}: {value};");
            }
            sb.AppendLine("}");
            sb.AppendLine();
        }

        private void WriteComponent(
            StringBuilder sb,
            string name,
            ComponentDefinition component,
            FormatterContext context)
        {
            var className = component.Class ?? name;

            // Base styles
            if (component.Styles.Any())
            {
                WriteRuleSet(sb, $".{className}", component.Styles, context);
            }

            // Child components
            foreach (var (childName, child) in component.Children)
            {
                if (child.Styles.Any())
                {
                    var childClass = child.Class ?? $"{className}__{childName}";
                    WriteRuleSet(sb, $".{childClass}", child.Styles, context);
                }
            }

            // Variants
            foreach (var (variantName, styles) in component.Variants)
            {
                if (styles.Any())
                {
                    WriteRuleSet(sb, $".{className}--{variantName}", styles, context);
                }
            }

            if (!context.Minify)
            {
                sb.AppendLine();
            }
        }

        private void WriteRuleSet(
            StringBuilder sb,
            string selector,
            Dictionary<string, object> styles,
            FormatterContext context)
        {
            sb.Append(selector);
            sb.AppendLine(context.Minify ? "{" : " {");

            foreach (var (prop, value) in styles)
            {
                var formattedValue = FormatValue(value?.ToString() ?? "", context);
                sb.AppendLine(context.Minify
                    ? $"{prop}:{formattedValue};"
                    : $"  {prop}: {formattedValue};");
            }

            sb.AppendLine("}");
        }

        private void WriteUtilities(
            StringBuilder sb,
            StyleDefinition definition,
            FormatterContext context)
        {
            foreach (var (name, value) in definition.Tokens)
            {
                if (name.StartsWith("color"))
                {
                    WriteRuleSet(sb, $".text-{name}", new Dictionary<string, object>
                    {
                        ["color"] = $"var(--{name})"
                    }, context);

                    WriteRuleSet(sb, $".bg-{name}", new Dictionary<string, object>
                    {
                        ["background-color"] = $"var(--{name})"
                    }, context);
                }
                else if (name.StartsWith("spacing"))
                {
                    WriteRuleSet(sb, $".p-{name}", new Dictionary<string, object>
                    {
                        ["padding"] = $"var(--{name})"
                    }, context);

                    WriteRuleSet(sb, $".m-{name}", new Dictionary<string, object>
                    {
                        ["margin"] = $"var(--{name})"
                    }, context);
                }
            }
        }

        private string FormatValue(string value, FormatterContext context)
        {
            // Replace theme-specific values if theme is specified
            if (context.Theme != null && value.Contains("theme("))
            {
                value = value.Replace(
                    "theme(",
                    $"var(--{context.Theme}-");
            }

            return value;
        }

        private string MinifyCss(string css)
        {
            // Basic CSS minification
            return css
                .Replace(Environment.NewLine, "")
                .Replace("  ", "")
                .Replace(" {", "{")
                .Replace(" :", ":")
                .Replace(": ", ":")
                .Replace("{ ", "{")
                .Replace(" }", "}")
                .Replace("; ", ";")
                .Replace(" ;", ";");
        }
    }
}
