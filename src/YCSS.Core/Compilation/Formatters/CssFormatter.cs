using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YCSS.Core.Models;

namespace YCSS.Core.Compilation.Formatters
{
    public class CssFormatter : IOutputFormatter
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
                Minify: options.Optimize,
                Theme: options.Theme,
                IncludeSourceMap: false,
                IncludeComments: !options.Optimize
            );

            try
            {
                if (!options.TokensOnly)
                {
                    // Write CSS variables
                    WriteVariables(sb, definition.Tokens, context);

                    // Write component styles
                    foreach (var (name, component) in definition.Components)
                    {
                        WriteComponent(sb, name, component, context);
                    }

                    // Write street styles
                    foreach (var (name, style) in definition.StreetStyles)
                    {
                        WriteStreetStyle(sb, name, style, context);
                    }

                    // Write utilities if requested
                    if (options.UseUtilities)
                    {
                        WriteUtilities(sb, definition, context);
                    }
                }
                else
                {
                    // Tokens only
                    sb.Append(FormatTokens(definition, options));
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

        public string FormatTokens(StyleDefinition definition, CompilerOptions options)
        {
            var sb = new StringBuilder();
            var context = new FormatterContext(
                Minify: options.Optimize,
                Theme: options.Theme,
                IncludeSourceMap: false,
                IncludeComments: !options.Optimize
            );

            WriteVariables(sb, definition.Tokens, context);
            return sb.ToString();
        }

        private void WriteVariables(
            StringBuilder sb,
            Dictionary<string, TokenDefinition> tokens,
            FormatterContext context)
        {
            if (!tokens.Any()) return;

            if (context.IncludeComments)
            {
                sb.AppendLine("/* Design Tokens */");
            }

            sb.AppendLine(":root {");
            foreach (var token in tokens.Values)
            {
                // Write token description if available
                if (context.IncludeComments && !string.IsNullOrEmpty(token.Description))
                {
                    sb.AppendLine($"  /* {token.Description} */");
                }

                sb.AppendLine(context.Minify
                    ? $"--{token.Name}:{token.Value};"
                    : $"  --{token.Name}: {token.Value};");

                // Write theme overrides if available
                if (token.ThemeOverrides.Any())
                {
                    foreach (var (theme, value) in token.ThemeOverrides)
                    {
                        sb.AppendLine(context.Minify
                            ? $"--{theme}-{token.Name}:{value};"
                            : $"  --{theme}-{token.Name}: {value};");
                    }
                }
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
            // Write component description if available
            if (context.IncludeComments && !string.IsNullOrEmpty(component.Description))
            {
                sb.AppendLine($"/* {component.Description} */");
            }

            // Write base styles
            if (component.Base != null)
            {
                WriteComponentBase(sb, component.Base, context);
            }

            // Write parts
            foreach (var (partName, part) in component.Parts)
            {
                WriteComponentBase(sb, part, context);
            }

            // Write variants
            foreach (var (variantName, variant) in component.Variants)
            {
                WriteComponentBase(sb, variant, context);
            }

            if (!context.Minify)
            {
                sb.AppendLine();
            }
        }

        private void WriteComponentBase(
            StringBuilder sb,
            ComponentBaseDefinition component,
            FormatterContext context)
        {
            var selector = $".{component.Class}";
            WriteStyles(sb, selector, component, context);

            // Write media queries
            foreach (var (query, styles) in component.MediaQueries)
            {
                sb.AppendLine($"@media {query} {{");
                foreach (var (prop, value) in styles)
                {
                    sb.AppendLine(context.Minify
                        ? $"{selector}{{${prop}:{value};}}"
                        : $"  {selector} {{\n    {prop}: {value};\n  }}");
                }
                sb.AppendLine("}");
            }

            // Write states
            foreach (var (state, styles) in component.States)
            {
                WriteRuleSet(sb, $"{selector}:{state}", styles, context);
            }
        }

        private void WriteStreetStyle(
            StringBuilder sb,
            string name,
            ComponentBaseDefinition style,
            FormatterContext context)
        {
            WriteComponentBase(sb, style, context);
        }

        private void WriteStyles(
            StringBuilder sb,
            string selector,
            ComponentBaseDefinition component,
            FormatterContext context)
        {
            if (!component.Styles.Any()) return;

            sb.Append(selector);
            sb.AppendLine(context.Minify ? "{" : " {");

            foreach (var style in component.Styles)
            {
                var formattedValue = FormatValue(style.Value, context);
                var important = style.Important ? " !important" : "";
                
                if (context.IncludeComments && !string.IsNullOrEmpty(style.Comment))
                {
                    sb.AppendLine($"  /* {style.Comment} */");
                }

                sb.AppendLine(context.Minify
                    ? $"{style.Property}:{formattedValue}{important};"
                    : $"  {style.Property}: {formattedValue}{important};");
            }

            sb.AppendLine("}");
        }

        private void WriteRuleSet(
            StringBuilder sb,
            string selector,
            Dictionary<string, string> styles,
            FormatterContext context)
        {
            if (!styles.Any()) return;

            sb.Append(selector);
            sb.AppendLine(context.Minify ? "{" : " {");

            foreach (var (prop, value) in styles)
            {
                var formattedValue = FormatValue(value, context);
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
            if (context.IncludeComments)
            {
                sb.AppendLine("/* Utility Classes */");
            }

            foreach (var token in definition.Tokens.Values)
            {
                if (token.Name.StartsWith("color"))
                {
                    WriteRuleSet(sb, $".text-{token.Name}", new Dictionary<string, string>
                    {
                        ["color"] = $"var(--{token.Name})"
                    }, context);

                    WriteRuleSet(sb, $".bg-{token.Name}", new Dictionary<string, string>
                    {
                        ["background-color"] = $"var(--{token.Name})"
                    }, context);
                }
                else if (token.Name.StartsWith("spacing"))
                {
                    WriteRuleSet(sb, $".p-{token.Name}", new Dictionary<string, string>
                    {
                        ["padding"] = $"var(--{token.Name})"
                    }, context);

                    WriteRuleSet(sb, $".m-{token.Name}", new Dictionary<string, string>
                    {
                        ["margin"] = $"var(--{token.Name})"
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
