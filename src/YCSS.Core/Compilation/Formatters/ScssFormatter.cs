using Microsoft.Extensions.Logging;
using System.Text;
using YCSS.Core.Models;

namespace YCSS.Core.Compilation.Formatters
{
    public class ScssFormatter : IOutputFormatter
    {
        private readonly ILogger<ScssFormatter> _logger;
        private int _indentLevel;

        public ScssFormatter(ILogger<ScssFormatter> logger)
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
                    // Write SCSS variables
                    WriteVariables(sb, definition.Tokens, context);

                    // Write mixins and functions if needed
                    WriteMixins(sb, context);

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

                return sb.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error formatting SCSS");
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
                sb.AppendLine("// Design Tokens");
            }

            foreach (var token in tokens.Values)
            {
                // Write token description if available
                if (context.IncludeComments && !string.IsNullOrEmpty(token.Description))
                {
                    sb.AppendLine($"// {token.Description}");
                }

                sb.AppendLine($"${token.Name}: {token.Value};");

                // Write theme overrides if available
                if (token.ThemeOverrides.Any())
                {
                    foreach (var (theme, value) in token.ThemeOverrides)
                    {
                        sb.AppendLine($"${theme}-{token.Name}: {value};");
                    }
                }
            }
            sb.AppendLine();
        }

        private void WriteMixins(StringBuilder sb, FormatterContext context)
        {
            if (context.IncludeComments)
            {
                sb.AppendLine("// Common Mixins");
            }

            // Add common mixins
            sb.AppendLine(@"@mixin theme($name) {
  [data-theme=""#{$name}""] & {
    @content;
  }
}");
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
                sb.AppendLine($"// {component.Description}");
            }

            // Write base component styles
            if (component.Base != null)
            {
                WriteComponentBase(sb, component.Base, context);
            }

            // Write parts using nesting
            foreach (var (partName, part) in component.Parts)
            {
                WriteComponentBase(sb, part, context, true);
            }

            // Write variants using nesting
            foreach (var (variantName, variant) in component.Variants)
            {
                WriteComponentBase(sb, variant, context, true);
            }

            if (!context.Minify)
            {
                sb.AppendLine();
            }
        }

        private void WriteComponentBase(
            StringBuilder sb,
            ComponentBaseDefinition component,
            FormatterContext context,
            bool nested = false)
        {
            var selector = $".{component.Class}";
            WriteStyles(sb, selector, component, context, nested);

            // Write media queries using SCSS nesting
            foreach (var (query, styles) in component.MediaQueries)
            {
                var indent = nested ? "  " : "";
                sb.AppendLine($"{indent}@media {query} {{");
                foreach (var (prop, value) in styles)
                {
                    sb.AppendLine(context.Minify
                        ? $"{prop}:{value};"
                        : $"{indent}  {prop}: {value};");
                }
                sb.AppendLine($"{indent}}}");
            }

            // Write states using SCSS nesting
            foreach (var (state, styles) in component.States)
            {
                var indent = nested ? "  " : "";
                sb.AppendLine($"{indent}&:{state} {{");
                foreach (var (prop, value) in styles)
                {
                    sb.AppendLine(context.Minify
                        ? $"{prop}:{value};"
                        : $"{indent}  {prop}: {value};");
                }
                sb.AppendLine($"{indent}}}");
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
            FormatterContext context,
            bool nested = false)
        {
            if (!component.Styles.Any()) return;

            var indent = nested ? "  " : "";
            sb.AppendLine(nested ? $"{indent}&{selector} {{" : $"{selector} {{");

            foreach (var style in component.Styles)
            {
                var formattedValue = FormatValue(style.Value, context);
                var important = style.Important ? " !important" : "";
                
                if (context.IncludeComments && !string.IsNullOrEmpty(style.Comment))
                {
                    sb.AppendLine($"{indent}  // {style.Comment}");
                }

                sb.AppendLine(context.Minify
                    ? $"{style.Property}:{formattedValue}{important};"
                    : $"{indent}  {style.Property}: {formattedValue}{important};");
            }

            sb.AppendLine($"{indent}}}");
        }

        private void WriteUtilities(
            StringBuilder sb,
            StyleDefinition definition,
            FormatterContext context)
        {
            if (context.IncludeComments)
            {
                sb.AppendLine("// Utility Classes");
            }

            foreach (var token in definition.Tokens.Values)
            {
                if (token.Name.StartsWith("color"))
                {
                    sb.AppendLine($".text-{token.Name} {{");
                    sb.AppendLine($"  color: ${token.Name};");
                    sb.AppendLine("}");

                    sb.AppendLine($".bg-{token.Name} {{");
                    sb.AppendLine($"  background-color: ${token.Name};");
                    sb.AppendLine("}");
                }
                else if (token.Name.StartsWith("spacing"))
                {
                    sb.AppendLine($".p-{token.Name} {{");
                    sb.AppendLine($"  padding: ${token.Name};");
                    sb.AppendLine("}");

                    sb.AppendLine($".m-{token.Name} {{");
                    sb.AppendLine($"  margin: ${token.Name};");
                    sb.AppendLine("}");
                }
            }
        }

        private string FormatValue(string value, FormatterContext context)
        {
            // Replace CSS variables with SCSS variables
            if (value.Contains("var(--"))
            {
                value = value.Replace("var(--", "$");
                value = value.Replace(")", "");
            }

            // Replace theme function
            if (context.Theme != null && value.Contains("theme("))
            {
                value = value.Replace("theme(", $"${context.Theme}-");
                value = value.Replace(")", "");
            }

            return value;
        }
    }
}
