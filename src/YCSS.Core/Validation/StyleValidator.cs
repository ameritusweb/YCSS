using Microsoft.Extensions.Logging;
using YCSS.Core.Exceptions;
using YCSS.Core.Models;

namespace YCSS.Core.Validation
{
    public interface IStyleValidator
    {
        Task<ValidationResult> ValidateAsync(string yamlContent, CancellationToken ct = default);
        Task<ValidationResult> ValidateAsync(StyleDefinition definition, CancellationToken ct = default);
    }

    public record ValidationResult(
        bool IsValid,
        IReadOnlyList<ValidationError> Errors,
        StyleDefinition? Definition = null
    );

    public class StyleValidator : IStyleValidator
    {
        private readonly ILogger<StyleValidator> _logger;
        private readonly IEnumerable<IYamlValidator> _validators;

        public StyleValidator(
            ILogger<StyleValidator> logger,
            IEnumerable<IYamlValidator> validators)
        {
            _logger = logger;
            _validators = validators;
        }

        public async Task<ValidationResult> ValidateAsync(
            string yamlContent,
            CancellationToken ct = default)
        {
            try
            {
                _logger.LogDebug("Starting YAML validation");

                var yaml = new YamlStream();
                using var reader = new StringReader(yamlContent);
                yaml.Load(reader);

                var errors = new List<ValidationError>();

                // Validate YAML structure
                foreach (var validator in _validators)
                {
                    var validationErrors = await validator.ValidateAsync(yaml.Documents[0].RootNode, ct);
                    errors.AddRange(validationErrors);
                }

                if (errors.Any(e => e.Severity == ValidationSeverity.Error))
                {
                    _logger.LogWarning("YAML validation failed with {ErrorCount} errors",
                        errors.Count(e => e.Severity == ValidationSeverity.Error));
                    return new ValidationResult(false, errors);
                }

                // Parse into StyleDefinition if valid
                var deserializer = new YamlDotNet.Serialization.DeserializerBuilder()
                    .IgnoreUnmatchedProperties()
                    .Build();

                var definition = deserializer.Deserialize<StyleDefinition>(yamlContent);

                _logger.LogInformation("YAML validation completed successfully");
                return new ValidationResult(true, errors, definition);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "YAML validation failed with exception");
                throw new YCSSValidationException(new[]
                {
                new ValidationError("", ex.Message)
            });
            }
        }

        public async Task<ValidationResult> ValidateAsync(
            StyleDefinition definition,
            CancellationToken ct = default)
        {
            var errors = new List<ValidationError>();

            // Validate tokens
            foreach (var (key, value) in definition.Tokens)
            {
                if (string.IsNullOrWhiteSpace(key))
                {
                    errors.Add(new ValidationError("tokens", "Token key cannot be empty"));
                    continue;
                }

                if (value == null)
                {
                    errors.Add(new ValidationError($"tokens.{key}", "Token value cannot be null"));
                }
            }

            // Validate components
            foreach (var (name, component) in definition.Components)
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    errors.Add(new ValidationError("components", "Component name cannot be empty"));
                    continue;
                }

                ValidateComponent(component, $"components.{name}", errors);
            }

            return new ValidationResult(!errors.Any(e => e.Severity == ValidationSeverity.Error), errors, definition);
        }

        private void ValidateComponent(
            ComponentDefinition component,
            string path,
            List<ValidationError> errors)
        {
            // Validate class name if specified
            if (component.Class != null && !IsValidClassName(component.Class))
            {
                errors.Add(new ValidationError(
                    $"{path}.class",
                    $"Invalid class name: {component.Class}"
                ));
            }

            // Validate styles
            foreach (var (prop, value) in component.Styles)
            {
                if (string.IsNullOrWhiteSpace(prop))
                {
                    errors.Add(new ValidationError(
                        $"{path}.styles",
                        "Style property name cannot be empty"
                    ));
                    continue;
                }

                if (value == null)
                {
                    errors.Add(new ValidationError(
                        $"{path}.styles.{prop}",
                        "Style value cannot be null"
                    ));
                }
            }

            // Validate children
            foreach (var (childName, child) in component.Children)
            {
                if (string.IsNullOrWhiteSpace(childName))
                {
                    errors.Add(new ValidationError(
                        $"{path}.children",
                        "Child component name cannot be empty"
                    ));
                    continue;
                }

                ValidateComponent(child, $"{path}.children.{childName}", errors);
            }

            // Validate variants
            foreach (var (variantName, styles) in component.Variants)
            {
                if (string.IsNullOrWhiteSpace(variantName))
                {
                    errors.Add(new ValidationError(
                        $"{path}.variants",
                        "Variant name cannot be empty"
                    ));
                    continue;
                }

                foreach (var (prop, value) in styles)
                {
                    if (string.IsNullOrWhiteSpace(prop))
                    {
                        errors.Add(new ValidationError(
                            $"{path}.variants.{variantName}",
                            "Style property name cannot be empty"
                        ));
                        continue;
                    }

                    if (value == null)
                    {
                        errors.Add(new ValidationError(
                            $"{path}.variants.{variantName}.{prop}",
                            "Style value cannot be null"
                        ));
                    }
                }
            }
        }

        private static bool IsValidClassName(string className)
        {
            // Basic CSS class name validation
            return !string.IsNullOrWhiteSpace(className) &&
                   char.IsLetter(className[0]) &&
                   className.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_');
        }
    }
}
