using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace YCSS.Core.Compilation.Formatters
{
    public class TailwindFormatter : IStyleFormatter
    {
        private readonly ILogger<TailwindFormatter> _logger;
        private static readonly Regex ColorRegex = new(@"^#([A-Fa-f0-9]{6}|[A-Fa-f0-9]{3})$");
        private static readonly Regex SpacingRegex = new(@"^-?\d*\.?\d+(rem|em|px|vh|vw)$");

        public TailwindFormatter(ILogger<TailwindFormatter> logger)
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
                // Write Tailwind configuration
                WriteConfig(sb, definition, context);

                // Write component classes
                foreach (var (name, component) in definition.Components)
                {
                    WriteComponent(sb, name, component, context);
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error formatting Tailwind CSS");
                throw;
            }
        }

        private void WriteConfig(
            StringBuilder sb,
            StyleDefinition definition,
            FormatterContext context)
        {
            sb.AppendLine("/** @type {import('tailwindcss').Config} */");
            sb.AppendLine("module.exports = {");
            sb.AppendLine("  theme: {");
            sb.AppendLine("    extend: {");

            // Convert tokens to Tailwind config
            var colorTokens = new Dictionary<string, string>();
            var spacingTokens = new Dictionary<string, string>();
            var otherTokens = new Dictionary<string, Dictionary<string, string>>();

            foreach (var (key, value) in definition.Tokens)
            {
                var strValue = value.ToString() ?? "";

                if (key.StartsWith("color") && ColorRegex.IsMatch(strValue))
                {
                    var name = key.Replace("color-", "");
                    colorTokens[name] = strValue;
                }
                else if (key.StartsWith("spacing") && SpacingRegex.IsMatch(strValue))
                {
                    var name = key.Replace("spacing-", "");
                    spacingTokens[name] = strValue;
                }
                else
                {
                    var category = key.Split('-')[0];
                    var name = key.Substring(category.Length + 1);

                    if (!otherTokens.ContainsKey(category))
                    {
                        otherTokens[category] = new Dictionary<string, string>();
                    }
                    otherTokens[category][name] = strValue;
                }
            }

            // Write colors
            if (colorTokens.Any())
            {
                sb.AppendLine("      colors: {");
                foreach (var (name, value) in colorTokens)
                {
                    sb.AppendLine($"        '{name}': '{value}',");
                }
                sb.AppendLine("      },");
            }

            // Write spacing
            if (spacingTokens.Any())
            {
                sb.AppendLine("      spacing: {");
                foreach (var (name, value) in spacingTokens)
                {
                    sb.AppendLine($"        '{name}': '{value}',");
                }
                sb.AppendLine("      },");
            }

            // Write other token categories
            foreach (var (category, values) in otherTokens)
            {
                sb.AppendLine($"      {category}: {{");
                foreach (var (name, value) in values)
                {
                    sb.AppendLine($"        '{name}': '{value}',");
                }
                sb.AppendLine("      },");
            }

            sb.AppendLine("    },");
            sb.AppendLine("  },");

            // Add variants configuration
            sb.AppendLine("  variants: {");
            sb.AppendLine("    extend: {},");
            sb.AppendLine("  },");

            sb.AppendLine("  plugins: [");
            WritePlugins(sb, definition, context);
            sb.AppendLine("  ],");
            sb.AppendLine("};");
            sb.AppendLine();
        }

        private void WritePlugins(
            StringBuilder sb,
            StyleDefinition definition,
            FormatterContext context)
        {
            // Add plugin to generate component classes
            sb.AppendLine("    plugin(function({ addComponents }) {");
            sb.AppendLine("      addComponents({");

            foreach (var (name, component) in definition.Components)
            {
                WriteComponentStyle(sb, name, component, context);
            }

            sb.AppendLine("      });");
            sb.AppendLine("    }),");
        }

        private void WriteComponent(
            StringBuilder sb,
            string name,
            ComponentDefinition component,
            FormatterContext context)
        {
            var className = component.Class ?? name;

            sb.AppendLine($"/* Component: {className} */");
            sb.AppendLine($"@layer components {{");
            sb.AppendLine($"  .{className} {{");
            WriteStyles(sb, component.Styles, "    ");

            // Write variants
            foreach (var (variantName, styles) in component.Variants)
            {
                sb.AppendLine($"    &--{variantName} {{");
                WriteStyles(sb, styles, "      ");
                sb.AppendLine("    }");
            }

            // Write child components
            foreach (var (childName, child) in component.Children)
            {
                var childClass = child.Class ?? $"{className}__{childName}";
                sb.AppendLine($"    .{childClass} {{");
                WriteStyles(sb, child.Styles, "      ");
                sb.AppendLine("    }");
            }

            sb.AppendLine("  }");
            sb.AppendLine("}");
            sb.AppendLine();
        }

        private void WriteComponentStyle(
            StringBuilder sb,
            string name,
            ComponentDefinition component,
            FormatterContext context)
        {
            var className = component.Class ?? name;
            sb.AppendLine($"        '.{className}': {{");
            WriteStyles(sb, component.Styles, "          ");
            sb.AppendLine("        },");
        }

        private void WriteStyles(
            StringBuilder sb,
            Dictionary<string, object> styles,
            string indent)
        {
            foreach (var (prop, value) in styles)
            {
                var tailwindProp = ConvertToTailwindProperty(prop);
                var tailwindValue = ConvertToTailwindValue(value?.ToString() ?? "");
                sb.AppendLine($"{indent}{tailwindProp}: '{tailwindValue}',");
            }
        }

        private string ConvertToTailwindProperty(string cssProperty)
        {
            // Convert kebab-case to camelCase
            return Regex.Replace(cssProperty, @"-([a-z])", m => m.Groups[1].Value.ToUpper());
        }

        private string ConvertToTailwindValue(string value)
        {
            if (value.StartsWith("var(--"))
            {
                // Convert CSS variable to Tailwind theme reference
                var varName = value.Substring(6, value.Length - 7);
                var parts = varName.Split('-', 2);
                if (parts.Length == 2)
                {
                    return $"theme('{parts[0]}.{parts[1]}')";
                }
            }

            return value;
        }
    }
}
