using System;
using System.Collections.Generic;
using System.IO;
using YCSS.Core.Models;

namespace YCSS.Core.Compilation
{
    public interface IStyleCompiler
    {
        string CompileStyles(StyleDefinition styles, CompilerOptions options);
    }

    public class StyleCompiler : IStyleCompiler
    {
        public string CompileStyles(StyleDefinition styles, CompilerOptions options)
        {
            var writer = new StringWriter();

            // Generate tokens/variables
            if (!options.TokensOnly || options.Format == OutputFormat.Tokens)
            {
                WriteTokens(writer, styles.Tokens, options);
            }

            if (!options.TokensOnly)
            {
                // Generate component styles
                foreach (var (name, component) in styles.Components)
                {
                    WriteComponent(writer, name, component, options);
                }

                // Generate street styles
                foreach (var (name, style) in styles.StreetStyles)
                {
                    WriteStreetStyle(writer, name, style, options);
                }

                // Generate utility classes if requested
                if (options.UseUtilities)
                {
                    WriteUtilities(writer, styles, options);
                }
            }

            return writer.ToString();
        }

        private void WriteTokens(TextWriter writer, Dictionary<string, TokenDefinition> tokens, CompilerOptions options)
        {
            switch (options.Format)
            {
                case OutputFormat.CSS:
                    writer.WriteLine(":root {");
                    foreach (var token in tokens.Values)
                    {
                        writer.WriteLine($"  --{token.Name}: {token.Value};");
                    }
                    writer.WriteLine("}\n");
                    break;

                case OutputFormat.SCSS:
                    foreach (var token in tokens.Values)
                    {
                        writer.WriteLine($"${token.Name}: {token.Value};");
                    }
                    writer.WriteLine();
                    break;

                case OutputFormat.Tokens:
                    foreach (var token in tokens.Values)
                    {
                        writer.WriteLine($"{token.Name}: {token.Value}");
                    }
                    break;
            }
        }

        private void WriteComponent(
            TextWriter writer,
            string name,
            ComponentDefinition component,
            CompilerOptions options)
        {
            // Write base component
            if (component.Base != null)
            {
                WriteComponentBase(writer, component.Base, options);
            }

            // Write parts
            foreach (var (partName, part) in component.Parts)
            {
                WriteComponentBase(writer, part, options);
            }

            // Write variants
            foreach (var (variantName, variant) in component.Variants)
            {
                WriteComponentBase(writer, variant, options);
            }

            writer.WriteLine();
        }

        private void WriteComponentBase(
            TextWriter writer,
            ComponentBaseDefinition component,
            CompilerOptions options)
        {
            var selector = FormatSelector(component.Class, options);
            writer.WriteLine($"{selector} {{");
            WriteStyles(writer, component.Styles, options);
            writer.WriteLine("}");

            // Write media queries
            foreach (var (query, styles) in component.MediaQueries)
            {
                writer.WriteLine($"@media {query} {{");
                writer.WriteLine($"  {selector} {{");
                foreach (var (prop, value) in styles)
                {
                    writer.WriteLine($"    {prop}: {value};");
                }
                writer.WriteLine("  }");
                writer.WriteLine("}");
            }

            // Write states
            foreach (var (state, styles) in component.States)
            {
                writer.WriteLine($"{selector}:{state} {{");
                foreach (var (prop, value) in styles)
                {
                    writer.WriteLine($"  {prop}: {value};");
                }
                writer.WriteLine("}");
            }
        }

        private void WriteStreetStyle(
            TextWriter writer,
            string name,
            ComponentBaseDefinition style,
            CompilerOptions options)
        {
            WriteComponentBase(writer, style, options);
            writer.WriteLine();
        }

        private void WriteStyles(
            TextWriter writer,
            List<StylePropertyDefinition> styles,
            CompilerOptions options)
        {
            foreach (var style in styles)
            {
                var formattedValue = FormatValue(style.Value, options);
                var important = style.Important ? " !important" : "";
                writer.WriteLine($"  {style.Property}: {formattedValue}{important};");
            }
        }

        private void WriteUtilities(
            TextWriter writer,
            StyleDefinition styles,
            CompilerOptions options)
        {
            // Generate utility classes based on tokens
            foreach (var token in styles.Tokens.Values)
            {
                var name = token.Name;
                if (name.StartsWith("color"))
                {
                    writer.WriteLine($".text-{name} {{ color: var(--{name}); }}");
                    writer.WriteLine($".bg-{name} {{ background-color: var(--{name}); }}");
                }
                else if (name.StartsWith("spacing"))
                {
                    writer.WriteLine($".p-{name} {{ padding: var(--{name}); }}");
                    writer.WriteLine($".m-{name} {{ margin: var(--{name}); }}");
                }
            }
        }

        private string FormatSelector(string name, CompilerOptions options)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Class name cannot be empty", nameof(name));
                
            return options.Format switch
            {
                OutputFormat.Tailwind => $"@layer components {{ .{name} }}",
                _ => $".{name}"
            };
        }

        private string FormatValue(string value, CompilerOptions options)
        {
            return options.Format switch
            {
                OutputFormat.SCSS => value.Replace("var(--", "$"),
                _ => value
            };
        }
    }
}
