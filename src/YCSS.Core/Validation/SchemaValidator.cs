using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using YamlDotNet.RepresentationModel;
using YCSS.Core.Exceptions;
using YCSS.Core.Models;

namespace YCSS.Core.Validation
{
    public interface ISchemaValidator : IYamlValidator
    {
        Task<SchemaValidationResult> ValidateSchemaAsync(
            YamlMappingNode root,
            CancellationToken ct = default);
    }

    public record SchemaValidationResult(
        bool IsValid,
        IReadOnlyList<ValidationError> Errors,
        SchemaVersion Version
    );

    public record SchemaVersion(int Major, int Minor, int Patch);

    public class SchemaValidator : ISchemaValidator
    {
        private readonly ILogger<SchemaValidator> _logger;
        private static readonly SchemaVersion CurrentVersion = new(1, 0, 0);

        private static readonly Dictionary<string, SchemaType> PropertyTypes = new()
        {
            // Colors
            ["color"] = new SchemaType(
                @"^#[0-9a-fA-F]{3,6}$|^rgb\(\d+,\s*\d+,\s*\d+\)$|^var\(--[\w-]+\)$",
                "Color value must be a hex code, RGB function, or CSS variable"
            ),
            ["background-color"] = new SchemaType(
                @"^#[0-9a-fA-F]{3,6}$|^rgb\(\d+,\s*\d+,\s*\d+\)$|^var\(--[\w-]+\)$",
                "Background color must be a hex code, RGB function, or CSS variable"
            ),

            // Spacing
            ["margin"] = new SchemaType(
                @"^\d+(\.\d+)?(px|rem|em|%)|^var\(--[\w-]+\)$",
                "Margin must be a number with unit (px, rem, em, %) or CSS variable"
            ),
            ["padding"] = new SchemaType(
                @"^\d+(\.\d+)?(px|rem|em|%)|^var\(--[\w-]+\)$",
                "Padding must be a number with unit (px, rem, em, %) or CSS variable"
            ),

            // Sizing
            ["width"] = new SchemaType(
                @"^\d+(\.\d+)?(px|rem|em|%|vw)|^var\(--[\w-]+\)$|^auto$",
                "Width must be a number with unit (px, rem, em, %, vw), 'auto', or CSS variable"
            ),
            ["height"] = new SchemaType(
                @"^\d+(\.\d+)?(px|rem|em|%|vh)|^var\(--[\w-]+\)$|^auto$",
                "Height must be a number with unit (px, rem, em, %, vh), 'auto', or CSS variable"
            ),

            // Typography
            ["font-size"] = new SchemaType(
                @"^\d+(\.\d+)?(px|rem|em)|^var\(--[\w-]+\)$",
                "Font size must be a number with unit (px, rem, em) or CSS variable"
            ),
            ["font-weight"] = new SchemaType(
                @"^(normal|bold|\d{3})|^var\(--[\w-]+\)$",
                "Font weight must be 'normal', 'bold', a multiple of 100 (100-900), or CSS variable"
            ),
            ["line-height"] = new SchemaType(
                @"^\d+(\.\d+)?|^var\(--[\w-]+\)$",
                "Line height must be a number or CSS variable"
            ),

            // Borders
            ["border"] = new SchemaType(
                @"^\d+(\.\d+)?px\s+(solid|dashed|dotted)\s+([#\w])|^var\(--[\w-]+\)$",
                "Border must be in format '<width> <style> <color>' or CSS variable"
            ),
            ["border-radius"] = new SchemaType(
                @"^\d+(\.\d+)?(px|rem|em|%)|^var\(--[\w-]+\)$",
                "Border radius must be a number with unit (px, rem, em, %) or CSS variable"
            ),

            // Display & Position
            ["display"] = new SchemaType(
                @"^(block|inline|flex|grid|none)$",
                "Display must be one of: block, inline, flex, grid, none"
            ),
            ["position"] = new SchemaType(
                @"^(static|relative|absolute|fixed|sticky)$",
                "Position must be one of: static, relative, absolute, fixed, sticky"
            ),

            // Flexbox
            ["flex-direction"] = new SchemaType(
                @"^(row|column|row-reverse|column-reverse)$",
                "Flex direction must be one of: row, column, row-reverse, column-reverse"
            ),
            ["justify-content"] = new SchemaType(
                @"^(flex-start|flex-end|center|space-between|space-around|space-evenly)$",
                "Justify content must be one of: flex-start, flex-end, center, space-between, space-around, space-evenly"
            ),
            ["align-items"] = new SchemaType(
                @"^(flex-start|flex-end|center|baseline|stretch)$",
                "Align items must be one of: flex-start, flex-end, center, baseline, stretch"
            ),

            // Grid
            ["grid-template-columns"] = new SchemaType(
                @"^((\d+(\.\d+)?(fr|px|rem|em|%)\s*)+|repeat\(\d+,\s*\d+(\.\d+)?(fr|px|rem|em|%)\))|^var\(--[\w-]+\)$",
                "Grid template columns must be space-separated values with units, repeat function, or CSS variable"
            ),
            ["gap"] = new SchemaType(
                @"^\d+(\.\d+)?(px|rem|em)|^var\(--[\w-]+\)$",
                "Gap must be a number with unit (px, rem, em) or CSS variable"
            )
        };

        private static readonly Dictionary<string, string[]> RequiredProperties = new()
        {
            ["button"] = new[] { "background-color", "padding" },
            ["input"] = new[] { "border", "padding" },
            ["card"] = new[] { "padding", "background-color" },
            ["modal"] = new[] { "position", "background-color" },
            ["grid"] = new[] { "display", "grid-template-columns" },
            ["flex"] = new[] { "display", "flex-direction" }
        };

        private static readonly Dictionary<string, string[]> RecommendedProperties = new()
        {
            ["button"] = new[] { "border-radius", "font-weight" },
            ["input"] = new[] { "border-radius", "width" },
            ["card"] = new[] { "border-radius", "box-shadow" },
            ["modal"] = new[] { "width", "height", "z-index" },
            ["grid"] = new[] { "gap", "width" },
            ["flex"] = new[] { "justify-content", "align-items" }
        };

        public SchemaValidator(ILogger<SchemaValidator> logger)
        {
            _logger = logger;
        }

        public async Task<IReadOnlyList<ValidationError>> ValidateAsync(
            YamlNode node,
            CancellationToken ct = default)
        {
            var result = await ValidateSchemaAsync((YamlMappingNode)node, ct);
            return result.Errors;
        }

        public async Task<SchemaValidationResult> ValidateSchemaAsync(
            YamlMappingNode root,
            CancellationToken ct = default)
        {
            var errors = new List<ValidationError>();

            try
            {
                // Validate schema version
                var version = ValidateVersion(root, errors);

                // Validate root structure
                if (!root.Children.ContainsKey("tokens") && !root.Children.ContainsKey("components"))
                {
                    errors.Add(new ValidationError(
                        "",
                        "YAML must contain at least 'tokens' or 'components' section"
                    ));
                    return new SchemaValidationResult(false, errors, version);
                }

                // Validate tokens if present
                if (root.Children.ContainsKey("tokens"))
                {
                    await ValidateTokens((YamlMappingNode)root.Children["tokens"], errors, ct);
                }

                // Validate components if present
                if (root.Children.ContainsKey("components"))
                {
                    await ValidateComponents((YamlMappingNode)root.Children["components"], errors, ct);
                }

                return new SchemaValidationResult(
                    !errors.Any(e => e.Severity == ValidationSeverity.Error),
                    errors,
                    version
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Schema validation failed");
                errors.Add(new ValidationError("", $"Schema validation failed: {ex.Message}"));
                return new SchemaValidationResult(false, errors, CurrentVersion);
            }
        }

        private SchemaVersion ValidateVersion(YamlMappingNode root, List<ValidationError> errors)
        {
            if (!root.Children.ContainsKey("version"))
            {
                return CurrentVersion;
            }

            var versionStr = root.Children["version"].ToString();
            var parts = versionStr.Split('.');

            if (parts.Length != 3 ||
                !int.TryParse(parts[0], out var major) ||
                !int.TryParse(parts[1], out var minor) ||
                !int.TryParse(parts[2], out var patch))
            {
                errors.Add(new ValidationError(
                    "version",
                    "Version must be in format 'major.minor.patch'"
                ));
                return CurrentVersion;
            }

            return new SchemaVersion(major, minor, patch);
        }

        private async Task ValidateTokens(
            YamlMappingNode tokens,
            List<ValidationError> errors,
            CancellationToken ct)
        {
            foreach (var (key, value) in tokens.Children)
            {
                if (ct.IsCancellationRequested) return;

                var tokenName = key.ToString();
                var tokenValue = value.ToString();

                // Validate token name
                if (!IsValidTokenName(tokenName))
                {
                    errors.Add(new ValidationError(
                        $"tokens.{tokenName}",
                        "Token name must be kebab-case and start with a letter"
                    ));
                }

                // Validate token value
                if (string.IsNullOrWhiteSpace(tokenValue))
                {
                    errors.Add(new ValidationError(
                        $"tokens.{tokenName}",
                        "Token value cannot be empty"
                    ));
                }

                // Type-specific validation
                var inferredType = InferTokenType(tokenName);
                if (inferredType != null && !ValidateTokenValue(tokenValue, inferredType))
                {
                    errors.Add(new ValidationError(
                        $"tokens.{tokenName}",
                        $"Invalid value for {inferredType} token"
                    ));
                }
            }
        }

        private async Task ValidateComponents(
            YamlMappingNode components,
            List<ValidationError> errors,
            CancellationToken ct)
        {
            foreach (var (key, value) in components.Children)
            {
                if (ct.IsCancellationRequested) return;

                var componentName = key.ToString();
                if (!(value is YamlMappingNode componentNode))
                {
                    errors.Add(new ValidationError(
                        $"components.{componentName}",
                        "Component definition must be a mapping"
                    ));
                    continue;
                }

                // Validate component name
                if (!IsValidComponentName(componentName))
                {
                    errors.Add(new ValidationError(
                        $"components.{componentName}",
                        "Component name must be kebab-case and start with a letter"
                    ));
                }

                // Validate base component
                await ValidateComponentBase(
                    componentName,
                    componentNode,
                    $"components.{componentName}",
                    errors,
                    ct);

                // Validate variants if present
                if (componentNode.Children.ContainsKey("variants"))
                {
                    await ValidateVariants(
                        componentName,
                        (YamlMappingNode)componentNode.Children["variants"],
                        $"components.{componentName}.variants",
                        errors,
                        ct);
                }

                // Check required properties
                if (RequiredProperties.ContainsKey(componentName))
                {
                    foreach (var prop in RequiredProperties[componentName])
                    {
                        if (!HasProperty(componentNode, prop))
                        {
                            errors.Add(new ValidationError(
                                $"components.{componentName}",
                                $"Required property '{prop}' is missing",
                                ValidationSeverity.Error
                            ));
                        }
                    }
                }

                // Check recommended properties
                if (RecommendedProperties.ContainsKey(componentName))
                {
                    foreach (var prop in RecommendedProperties[componentName])
                    {
                        if (!HasProperty(componentNode, prop))
                        {
                            errors.Add(new ValidationError(
                                $"components.{componentName}",
                                $"Recommended property '{prop}' is missing",
                                ValidationSeverity.Warning
                            ));
                        }
                    }
                }
            }
        }

        private async Task ValidateComponentBase(
            string componentName,
            YamlMappingNode node,
            string path,
            List<ValidationError> errors,
            CancellationToken ct)
        {
            // Validate class name if present
            if (node.Children.ContainsKey("class"))
            {
                var className = node.Children["class"].ToString();
                if (!IsValidClassName(className))
                {
                    errors.Add(new ValidationError(
                        $"{path}.class",
                        "Class name must be kebab-case or BEM format"
                    ));
                }
            }

            // Validate styles
            if (!node.Children.ContainsKey("styles"))
            {
                errors.Add(new ValidationError(
                    path,
                    "Component must have a 'styles' section"
                ));
                return;
            }

            if (!(node.Children["styles"] is YamlSequenceNode styles))
            {
                errors.Add(new ValidationError(
                    $"{path}.styles",
                    "Styles must be a sequence"
                ));
                return;
            }

            foreach (var style in styles)
            {
                if (ct.IsCancellationRequested) return;

                if (!(style is YamlMappingNode styleMap))
                {
                    errors.Add(new ValidationError(
                        $"{path}.styles",
                        "Each style must be a property-value mapping"
                    ));
                    continue;
                }

                foreach (var (propKey, propValue) in styleMap.Children)
                {
                    var property = propKey.ToString();
                    var value = propValue.ToString();

                    // Validate property name
                    if (!IsValidPropertyName(property))
                    {
                        errors.Add(new ValidationError(
                            $"{path}.styles.{property}",
                            "Invalid property name"
                        ));
                    }

                    // Validate property value
                    if (PropertyTypes.ContainsKey(property))
                    {
                        var type = PropertyTypes[property];
                        if (!Regex.IsMatch(value, type.Pattern))
                        {
                            errors.Add(new ValidationError(
                                $"{path}.styles.{property}",
                                type.ErrorMessage
                            ));
                        }
                    }
                }
            }
        }

        private async Task ValidateVariants(
            string componentName,
            YamlMappingNode variants,
            string path,
            List<ValidationError> errors,
            CancellationToken ct)
        {
            foreach (var (key, value) in variants.Children)
            {
                if (ct.IsCancellationRequested) return;

                var variantName = key.ToString();
                if (!(value is YamlMappingNode variantNode))
                {
                    errors.Add(new ValidationError(
                        $"{path}.{variantName}",
                        "Variant definition must be a mapping"
                    ));
                    continue;
                }

                // Validate variant name
                if (!IsValidVariantName(variantName))
                {
                    errors.Add(new ValidationError(
                        $"{path}.{variantName}",
                        "Variant name must be kebab-case"
                    ));
                }

                // Validate variant component
                await ValidateComponentBase(
                    componentName,
                    variantNode,
                    $"{path}.{variantName}",
                    errors,
                    ct);
            }
        }

        private bool IsValidTokenName(string name)
        {
            return Regex.IsMatch(name, @"^[a-z][a-z0-9]*(-[a-z0-9]+)*$");
        }

        private bool IsValidComponentName(string name)
        {
            return Regex.IsMatch(name, @"^[a-z][a-z0-9]*(-[a-z0-9]+)*$");
        }

        private bool IsValidClassName(string name)
        {
            // Allow kebab-case or BEM format
            return Regex.IsMatch(name, @"^[a-z][a-z0-9]*(-[a-z0-9]+)*(__[a-z0-9]+)*(-{2}[a-z0-9]+)*$");
        }

        private bool IsValidPropertyName(string name)
        {
            return Regex.IsMatch(name, @"^[a-z][a-z0-9]*(-[a-z0-9]+)*$");
        }

        private bool IsValidVariantName(string name)
        {
            return Regex.IsMatch(name, @"^[a-z][a-z0-9]*(-[a-z0-9]+)*$");
        }

        private string? InferTokenType(string name)
        {
            if (name.StartsWith("color")) return "color";
            if (name.StartsWith("spacing")) return "spacing";
            if (name.StartsWith("font")) return "typography";
            if (name.StartsWith("border")) return "border";
            return null;
        }

        private bool ValidateTokenValue(string value, string type)
        {
            return type switch
            {
                "color" => Regex.IsMatch(value, @"^#[0-9a-fA-F]{3,6}$|^rgb\(\d+,\s*\d+,\s*\d+\)$"),
                "spacing" => Regex.IsMatch(value, @"^\d+(\.\d+)?(px|rem|em)$"),
                "typography" => Regex.IsMatch(value, @"^\d+(\.\d+)?(px|rem|em)$|^(normal|bold|\d{3})$"),
                "border" => Regex.IsMatch(value, @"^\d+(\.\d+)?px\s+(solid|dashed|dotted)\s+([#\w])$"),
                _ => true
            };
        }

        private bool HasProperty(YamlMappingNode node, string property)
        {
            if (!node.Children.ContainsKey("styles")) return false;
            var styles = (YamlSequenceNode)node.Children["styles"];
            return styles.Children.Any(style =>
            {
                var styleMap = (YamlMappingNode)style;
                return styleMap.Children.Keys.Any(k => k.ToString() == property);
            });
        }

        private record SchemaType(string Pattern, string ErrorMessage);
    }
}
