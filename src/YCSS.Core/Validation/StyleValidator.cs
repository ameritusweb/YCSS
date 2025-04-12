using Microsoft.Extensions.Logging;
using YamlDotNet.RepresentationModel;
using YCSS.Core.Exceptions;
using YCSS.Core.Models;
using YCSS.Core.Pipeline;
using YCSS.Core.Utils;

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
        private readonly YamlParser _yamlParser;
        private readonly IEnumerable<IYamlValidator> _validators;

        public StyleValidator(
            ILogger<StyleValidator> logger,
            YamlParser yamlParser,
            IEnumerable<IYamlValidator> validators)
        {
            _logger = logger;
            _yamlParser = yamlParser;
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

                // Parse YAML using our parser
                var (tokens, components, styles) = _yamlParser.Parse(yamlContent);

                // Create build context with parsed data
                var definition = new StyleDefinition
                {
                    Tokens = tokens,
                    Components = components,
                    StreetStyles = styles
                };

                _logger.LogInformation("YAML validation completed successfully");
                _logger.LogDebug("Parsed {TokenCount} tokens, {ComponentCount} components, {StyleCount} street styles",
                    tokens.Count, components.Count, styles.Count);

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
            foreach (var token in definition.Tokens.Values)
            {
                if (string.IsNullOrWhiteSpace(token.Name))
                {
                    errors.Add(new ValidationError("tokens", "Token name cannot be empty"));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(token.Value))
                {
                    errors.Add(new ValidationError($"tokens.{token.Name}", "Token value cannot be empty"));
                }
            }

            // Validate components
            foreach (var component in definition.Components.Values)
            {
                if (string.IsNullOrWhiteSpace(component.Name))
                {
                    errors.Add(new ValidationError("components", "Component name cannot be empty"));
                    continue;
                }

                ValidateComponentBase(component.Base, $"components.{component.Name}.base", errors);
                
                foreach (var (partName, part) in component.Parts)
                {
                    ValidateComponentBase(part, $"components.{component.Name}.parts.{partName}", errors);
                }

                foreach (var (variantName, variant) in component.Variants)
                {
                    ValidateComponentBase(variant, $"components.{component.Name}.variants.{variantName}", errors);
                }
            }

            // Validate street styles
            foreach (var (name, style) in definition.StreetStyles)
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    errors.Add(new ValidationError("styles", "Style name cannot be empty"));
                    continue;
                }

                ValidateComponentBase(style, name, errors);
            }

            return new ValidationResult(!errors.Any(e => e.Severity == ValidationSeverity.Error), errors, definition);
        }

        private void ValidateComponentBase(
            ComponentBaseDefinition component,
            string path,
            List<ValidationError> errors)
        {
            if (component == null)
            {
                errors.Add(new ValidationError(path, "Component definition cannot be null"));
                return;
            }

            // Validate class name if specified
            if (!string.IsNullOrEmpty(component.Class) && !IsValidClassName(component.Class))
            {
                errors.Add(new ValidationError(
                    $"{path}.class",
                    $"Invalid class name: {component.Class}"
                ));
            }

            // Validate styles
            foreach (var style in component.Styles)
            {
                if (string.IsNullOrWhiteSpace(style.Property))
                {
                    errors.Add(new ValidationError(
                        $"{path}.styles",
                        "Style property name cannot be empty"
                    ));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(style.Value))
                {
                    errors.Add(new ValidationError(
                        $"{path}.styles.{style.Property}",
                        "Style value cannot be empty"
                    ));
                }
            }

            // Validate media queries
            foreach (var (query, styles) in component.MediaQueries)
            {
                if (string.IsNullOrWhiteSpace(query))
                {
                    errors.Add(new ValidationError(
                        $"{path}.media",
                        "Media query cannot be empty"
                    ));
                }
            }

            // Validate states
            foreach (var (state, styles) in component.States)
            {
                if (string.IsNullOrWhiteSpace(state))
                {
                    errors.Add(new ValidationError(
                        $"{path}.states",
                        "State name cannot be empty"
                    ));
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
