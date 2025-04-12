using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YCSS.Core.Compilation.Formatters
{
    public class ScssFormatter : IStyleFormatter
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
                Variables: definition.Tokens,
                Minify: options.Optimize,
                Theme: options.Theme,
                IncludeSourceMap: options.GenerateSourceMap
            );

            try
            {
                // Write variables
                WriteVariables(sb, definition.Tokens, context);

                // Write mixins
                WriteMixins(sb, definition, context);

                // Write components
                foreach (var (name, component) in definition.Components)
                {
                    WriteComponent(sb, name, component, context);
                }

                // Write utilities if requested
                if (options.UseUtilities)
                {
                    WriteUtilities(sb, definition, context);
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error formatting SCSS");
                throw;
            }
        }

        private void WriteVariables(
            StringBuilder sb,
            Dictionary<string, string> tokens,
            FormatterContext context)
        {
            if (!tokens.Any()) return;

            foreach (var (name, value) in tokens)
            {
                sb.AppendLine($"${name}: {value};");
            }
            sb.AppendLine();
        }

        private void WriteMixins(
            StringBuilder sb,
            StyleDefinition definition,
            FormatterContext context)
        {
            // Create mixins for commonly used property combinations
            foreach (var (name, component) in definition.Components)
            {
                if (component.Variants.Any())
                {
                    sb.AppendLine($"@mixin {name}-variants {{");
                    _indentLevel++;

                    foreach (var (variantName, styles) in component.Variants)
                    {
                        var indent = new string(' ', _indentLevel * 2);
                        sb.AppendLine($"{indent}&--{variantName} {{");
                        WriteStyles(sb, styles, context);
                        sb.AppendLine($"{indent}}}");
                    }

                    _indentLevel--;
                    sb.AppendLine("}");
                    sb.AppendLine();
                }
            }
        }

        private void WriteComponent(
            StringBuilder sb,
            string name,
            ComponentDefinition component,
            FormatterContext context)
        {
            var className = component.Class ?? name;
            sb.AppendLine($".{className} {{");
            _indentLevel++;

            // Base styles
            WriteStyles(sb, component.Styles, context);

            // Child components using nesting
            foreach (var (childName, child) in component.Children)
            {
                var childClass = child.Class ?? childName;
                var indent = new string(' ', _indentLevel * 2);
                sb.AppendLine($"{indent}&__{childClass} {{");
                _indentLevel++;
                WriteStyles(sb, child.Styles, context);
                _indentLevel--;
                sb.AppendLine($"{indent}}}");
            }

            // Include variants mixin if it exists
            if (component.Variants.Any())
            {
                var indent = new string(' ', _indentLevel * 2);
                sb.AppendLine($"{indent}@include {name}-variants;");
            }

            _indentLevel--;
            sb.AppendLine("}");
            sb.AppendLine();
        }

        private void WriteStyles(
            StringBuilder sb,
            Dictionary<string, object> styles,
            FormatterContext context)
        {
            foreach (var (prop, value) in styles)
            {
                var indent = new string(' ', _indentLevel * 2);
                var formattedValue = FormatValue(value?.ToString() ?? "", context);
                sb.AppendLine($"{indent}{prop}: {formattedValue};");
            }
        }

        private void WriteUtilities(
            StringBuilder sb,
            StyleDefinition definition,
            FormatterContext context)
        {
            // Generate utility mixins
            sb.AppendLine("// Utility Mixins");
            foreach (var (name, value) in definition.Tokens)
            {
                if (name.StartsWith("color"))
                {
                    sb.AppendLine($"@mixin text-{name} {{");
                    sb.AppendLine($"  color: ${name};");
                    sb.AppendLine("}");

                    sb.AppendLine($"@mixin bg-{name} {{");
                    sb.AppendLine($"  background-color: ${name};");
                    sb.AppendLine("}");
                }
                else if (name.StartsWith("spacing"))
                {
                    sb.AppendLine($"@mixin p-{name} {{");
                    sb.AppendLine($"  padding: ${name};");
                    sb.AppendLine("}");

                    sb.AppendLine($"@mixin m-{name} {{");
                    sb.AppendLine($"  margin: ${name};");
                    sb.AppendLine("}");
                }
            }

            // Generate utility classes
            sb.AppendLine("\n// Utility Classes");
            foreach (var (name, _) in definition.Tokens)
            {
                if (name.StartsWith("color"))
                {
                    sb.AppendLine($".text-{name} {{ @include text-{name}; }}");
                    sb.AppendLine($".bg-{name} {{ @include bg-{name}; }}");
                }
                else if (name.StartsWith("spacing"))
                {
                    sb.AppendLine($".p-{name} {{ @include p-{name}; }}");
                    sb.AppendLine($".m-{name} {{ @include m-{name}; }}");
                }
            }
        }

        private string FormatValue(string value, FormatterContext context)
        {
            // Convert CSS var() to SCSS variables
            if (value.StartsWith("var(--"))
            {
                value = "$" + value.Substring(6, value.Length - 7);
            }

            // Handle theme values
            if (context.Theme != null && value.Contains("theme("))
            {
                value = value.Replace(
                    "theme(",
                    $"${context.Theme}-");
            }

            return value;
        }
    }
}
