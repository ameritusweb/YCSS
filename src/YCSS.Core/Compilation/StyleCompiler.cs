using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YCSS.Core.Compilation
{
    public class StyleCompiler
    {
        public enum OutputFormat
        {
            CSS,
            SCSS,
            Tailwind,
            Tokens
        }

        public record StyleDefinition
        {
            public Dictionary<string, object> Tokens { get; init; } = new();
            public Dictionary<string, ComponentDefinition> Components { get; init; } = new();
        }

        public record ComponentDefinition
        {
            public string? Class { get; init; }
            public Dictionary<string, object> Styles { get; init; } = new();
            public Dictionary<string, ComponentDefinition> Children { get; init; } = new();
            public Dictionary<string, Dictionary<string, object>> Variants { get; init; } = new();
        }

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

                // Generate utility classes if requested
                if (options.UseUtilities)
                {
                    WriteUtilities(writer, styles, options);
                }
            }

            return writer.ToString();
        }

        private void WriteTokens(TextWriter writer, Dictionary<string, object> tokens, CompilerOptions options)
        {
            switch (options.Format)
            {
                case OutputFormat.CSS:
                    writer.WriteLine(":root {");
                    foreach (var (name, value) in tokens)
                    {
                        writer.WriteLine($"  --{name}: {value};");
                    }
                    writer.WriteLine("}\n");
                    break;

                case OutputFormat.SCSS:
                    foreach (var (name, value) in tokens)
                    {
                        writer.WriteLine($"${name}: {value};");
                    }
                    writer.WriteLine();
                    break;

                case OutputFormat.Tokens:
                    foreach (var (name, value) in tokens)
                    {
                        writer.WriteLine($"{name}: {value}");
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
            var className = component.Class ?? name;
            var selector = FormatSelector(className, options);

            writer.WriteLine($"{selector} {{");
            WriteStyles(writer, component.Styles, options);
            writer.WriteLine("}");

            // Write child components
            foreach (var (childName, child) in component.Children)
            {
                var childSelector = FormatSelector($"{className}__{childName}", options);
                writer.WriteLine($"{childSelector} {{");
                WriteStyles(writer, child.Styles, options);
                writer.WriteLine("}");
            }

            // Write variants
            foreach (var (variantName, styles) in component.Variants)
            {
                var variantSelector = FormatSelector($"{className}--{variantName}", options);
                writer.WriteLine($"{variantSelector} {{");
                WriteStyles(writer, styles, options);
                writer.WriteLine("}");
            }

            writer.WriteLine();
        }

        private void WriteStyles(
            TextWriter writer,
            Dictionary<string, object> styles,
            CompilerOptions options)
        {
            foreach (var (prop, value) in styles)
            {
                var formattedValue = FormatValue(value.ToString()!, options);
                writer.WriteLine($"  {prop}: {formattedValue};");
            }
        }

        private void WriteUtilities(
            TextWriter writer,
            StyleDefinition styles,
            CompilerOptions options)
        {
            // Generate utility classes based on tokens
            foreach (var (name, value) in styles.Tokens)
            {
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
