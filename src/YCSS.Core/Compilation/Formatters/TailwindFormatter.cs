using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using YCSS.Core.Models;

namespace YCSS.Core.Compilation.Formatters
{
    public class TailwindFormatter : IOutputFormatter
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
                Minify: options.Optimize,
                Theme: options.Theme,
                IncludeSourceMap: false,
                IncludeComments: !options.Optimize
            );

            try
            {
                if (!options.TokensOnly)
                {
                    // Write Tailwind configuration
                    WriteConfig(sb, definition, context);

                    // Write component classes
                    foreach (var (name, component) in definition.Components)
                    {
                        WriteComponent(sb, name, component, context);
                    }

                    // Write street styles
                    foreach (var (name, style) in definition.StreetStyles)
                    {
                        WriteStreetStyle(sb, name, style, context);
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
                _logger.LogError(ex, "Error formatting Tailwind CSS");
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

            WriteConfig(sb, definition, context);
            return sb.ToString();
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

            foreach (var token in definition.Tokens.Values)
            {
                var strValue = token.Value ?? "";

                if (token.Name.StartsWith("color") && ColorRegex.IsMatch(strValue))
                {
                    var name = token.Name.Replace("color-", "");
                    colorTokens[name] = strValue;
                }
                else if (token.Name.StartsWith("spacing") && SpacingRegex.IsMatch(strValue))
                {
                    var name = token.Name.Replace("spacing-", "");
                    spacingTokens[name] = strValue;
                }
                else
                {
                    var category = token.Name.Split('-')[0];
                    var name = token.Name.Substring(category.Length + 1);

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

            // Write structured components
            foreach (var (name, component) in definition.Components)
            {
                WriteComponentStyle(sb, name, component, context);
            }

            // Write street styles
            foreach (var (name, style) in definition.StreetStyles)
            {
                WriteStreetStyle(sb, name, style, context);
            }

            sb.AppendLine("      });");
            sb.AppendLine("    }),");
        }

        private void WriteStreetStyle(
            StringBuilder sb,
            string name,
            ComponentBaseDefinition style,
            FormatterContext context)
        {
            sb.AppendLine($"        '.{style.Class}': {{");
            WriteComponentBase(sb, style, "          ");
            sb.AppendLine("        },");
        }

        private void WriteComponent(
    StringBuilder sb,
    string name,
    ComponentDefinition component,
    FormatterContext context)
        {
            var className = component.Base?.Class ?? name;

            if (context.IncludeComments)
            {
                sb.AppendLine($"/* Component: {className} */");
            }

            sb.AppendLine($"@layer components {{");
            sb.AppendLine($"  .{className} {{");

            // Write base styles
            if (component.Base != null)
            {
                WriteStyles(sb, component.Base.Styles, "    ", context);
            }

            // Write variants
            foreach (var (variantName, variant) in component.Variants)
            {
                sb.AppendLine($"    &--{variantName} {{");
                WriteStyles(sb, variant.Styles, "      ", context);
                sb.AppendLine("    }");
            }

            // Write parts (child components)
            foreach (var (partName, part) in component.Parts)
            {
                var partClass = part.Class ?? $"{className}__{partName}";
                sb.AppendLine($"    .{partClass} {{");
                WriteComponentBase(sb, part, "      ", context);
                sb.AppendLine("    }");
            }

            sb.AppendLine("  }");
            sb.AppendLine("}");

            if (!context.Minify)
            {
                sb.AppendLine();
            }
        }

        private void WriteComponentBase(
            StringBuilder sb,
            ComponentBaseDefinition component,
            string indent,
            FormatterContext context)
        {
            WriteStyles(sb, component.Styles, indent, context);

            // Write media queries
            foreach (var (query, styles) in component.MediaQueries)
            {
                sb.AppendLine($"{indent}['@media {query}']: {{");
                foreach (var (prop, value) in styles)
                {
                    var tailwindProp = ConvertToTailwindProperty(prop);
                    var tailwindValue = ConvertToTailwindValue(value);
                    sb.AppendLine(context.Minify
                        ? $"{indent}  {tailwindProp}:'{tailwindValue}',"
                        : $"{indent}  {tailwindProp}: '{tailwindValue}',");
                }
                sb.AppendLine($"{indent}}},");
            }

            // Write states
            foreach (var (state, styles) in component.States)
            {
                sb.AppendLine($"{indent}['&:{state}']: {{");
                foreach (var (prop, value) in styles)
                {
                    var tailwindProp = ConvertToTailwindProperty(prop);
                    var tailwindValue = ConvertToTailwindValue(value);
                    sb.AppendLine(context.Minify
                        ? $"{indent}  {tailwindProp}:'{tailwindValue}',"
                        : $"{indent}  {tailwindProp}: '{tailwindValue}',");
                }
                sb.AppendLine($"{indent}}},");
            }
        }

        private void WriteComponentStyle(
            StringBuilder sb,
            string name,
            ComponentDefinition component,
            FormatterContext context)
        {
            var className = component.Base?.Class ?? name;
            sb.AppendLine($"        '.{className}': {{");

            if (component.Base != null)
            {
                WriteComponentBase(sb, component.Base, "          ", context);
            }

            sb.AppendLine("        },");
        }

        private void WriteStyles(
    StringBuilder sb,
    List<StylePropertyDefinition> styles,
    string indent,
    FormatterContext context)  // Added context parameter
        {
            foreach (var style in styles)
            {
                var tailwindProp = ConvertToTailwindProperty(style.Property);
                var tailwindValue = ConvertToTailwindValue(style.Value);
                var important = style.Important ? " !important" : "";

                if (!string.IsNullOrEmpty(style.Comment) && context.IncludeComments)
                {
                    sb.AppendLine($"{indent}/* {style.Comment} */");
                }

                if (context.Minify)
                {
                    sb.AppendLine($"{indent}{tailwindProp}:'{tailwindValue}{important}';");
                }
                else
                {
                    sb.AppendLine($"{indent}{tailwindProp}: '{tailwindValue}{important}';");
                }
            }
        }

        private void WriteComponentBase(
            StringBuilder sb,
            ComponentBaseDefinition component,
            string indent)
        {
            foreach (var style in component.Styles)
            {
                var tailwindProp = ConvertToTailwindProperty(style.Property);
                var tailwindValue = ConvertToTailwindValue(style.Value);
                var important = style.Important ? " !important" : "";
                sb.AppendLine($"{indent}{tailwindProp}: '{tailwindValue}{important}',");
            }

            // Write media queries
            foreach (var (query, styles) in component.MediaQueries)
            {
                sb.AppendLine($"{indent}['@media {query}']: {{");
                foreach (var (prop, value) in styles)
                {
                    var tailwindProp = ConvertToTailwindProperty(prop);
                    var tailwindValue = ConvertToTailwindValue(value);
                    sb.AppendLine($"{indent}  {tailwindProp}: '{tailwindValue}',");
                }
                sb.AppendLine($"{indent}}},");
            }

            // Write states
            foreach (var (state, styles) in component.States)
            {
                sb.AppendLine($"{indent}['&:{state}']: {{");
                foreach (var (prop, value) in styles)
                {
                    var tailwindProp = ConvertToTailwindProperty(prop);
                    var tailwindValue = ConvertToTailwindValue(value);
                    sb.AppendLine($"{indent}  {tailwindProp}: '{tailwindValue}',");
                }
                sb.AppendLine($"{indent}}},");
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
