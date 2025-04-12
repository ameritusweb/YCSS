using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.RepresentationModel;
using YCSS.Core.Exceptions;

namespace YCSS.Core.Validation
{
    /// <summary>
    /// Validates component definitions in YAML.
    /// </summary>
    public class ComponentValidator : IYamlValidator
    {
        private readonly ILogger<ComponentValidator> _logger;

        public ComponentValidator(ILogger<ComponentValidator> logger)
        {
            _logger = logger;
        }

        public Task<IEnumerable<ValidationError>> ValidateAsync(YamlNode rootNode, CancellationToken ct = default)
        {
            var errors = new List<ValidationError>();

            if (rootNode is not YamlMappingNode mappingNode)
            {
                errors.Add(new ValidationError("", "Root YAML node must be a mapping node", ValidationSeverity.Error));
                return Task.FromResult<IEnumerable<ValidationError>>(errors);
            }

            // Check for components node
            if (mappingNode.Children.TryGetValue(new YamlScalarNode("components"), out var componentsNode))
            {
                if (componentsNode is YamlMappingNode componentsMappingNode)
                {
                    ValidateComponents(componentsMappingNode, errors);
                }
                else
                {
                    errors.Add(new ValidationError("components", "Components must be a mapping", ValidationSeverity.Error));
                }
            }

            // Also validate "street styles" (components defined at root level)
            foreach (var (keyNode, valueNode) in mappingNode.Children)
            {
                if (keyNode is YamlScalarNode scalarKey)
                {
                    // Skip special nodes like "tokens" or "components"
                    if (scalarKey.Value == "tokens" || scalarKey.Value == "components")
                    {
                        continue;
                    }

                    // Treat other root nodes as street styles
                    if (valueNode is YamlMappingNode streetStyleNode)
                    {
                        ValidateStreetStyle(scalarKey.Value, streetStyleNode, errors);
                    }
                }
            }

            return Task.FromResult<IEnumerable<ValidationError>>(errors);
        }

        private void ValidateComponents(YamlMappingNode componentsNode, List<ValidationError> errors)
        {
            foreach (var (keyNode, valueNode) in componentsNode.Children)
            {
                if (keyNode is not YamlScalarNode componentKeyNode)
                {
                    errors.Add(new ValidationError("components", "Component key must be a scalar", ValidationSeverity.Error));
                    continue;
                }

                var componentName = componentKeyNode.Value;

                if (string.IsNullOrWhiteSpace(componentName))
                {
                    errors.Add(new ValidationError("components", "Component name cannot be empty", ValidationSeverity.Error));
                    continue;
                }

                if (valueNode is not YamlMappingNode componentMappingNode)
                {
                    errors.Add(new ValidationError($"components.{componentName}", "Component must be a mapping", ValidationSeverity.Error));
                    continue;
                }

                // Validate base component structure
                if (componentMappingNode.Children.TryGetValue(new YamlScalarNode("base"), out var baseNode))
                {
                    if (baseNode is YamlMappingNode baseMappingNode)
                    {
                        ValidateComponentBase($"components.{componentName}.base", baseMappingNode, errors);
                    }
                    else
                    {
                        errors.Add(new ValidationError($"components.{componentName}.base", "Component base must be a mapping", ValidationSeverity.Error));
                    }
                }

                // Validate parts
                if (componentMappingNode.Children.TryGetValue(new YamlScalarNode("parts"), out var partsNode))
                {
                    if (partsNode is YamlMappingNode partsMappingNode)
                    {
                        ValidateComponentParts($"components.{componentName}.parts", partsMappingNode, errors);
                    }
                    else
                    {
                        errors.Add(new ValidationError($"components.{componentName}.parts", "Component parts must be a mapping", ValidationSeverity.Error));
                    }
                }

                // Validate variants
                if (componentMappingNode.Children.TryGetValue(new YamlScalarNode("variants"), out var variantsNode))
                {
                    if (variantsNode is YamlMappingNode variantsMappingNode)
                    {
                        ValidateComponentVariants($"components.{componentName}.variants", variantsMappingNode, errors);
                    }
                    else
                    {
                        errors.Add(new ValidationError($"components.{componentName}.variants", "Component variants must be a mapping", ValidationSeverity.Error));
                    }
                }
            }
        }

        private void ValidateComponentBase(string path, YamlMappingNode baseNode, List<ValidationError> errors)
        {
            // Validate class field
            if (baseNode.Children.TryGetValue(new YamlScalarNode("class"), out var classNode))
            {
                if (classNode is not YamlScalarNode classScalarNode)
                {
                    errors.Add(new ValidationError($"{path}.class", "Class must be a scalar", ValidationSeverity.Error));
                }
                else if (string.IsNullOrWhiteSpace(classScalarNode.Value))
                {
                    errors.Add(new ValidationError($"{path}.class", "Class cannot be empty", ValidationSeverity.Error));
                }
                else if (!IsValidClassName(classScalarNode.Value))
                {
                    errors.Add(new ValidationError($"{path}.class", $"Invalid class name: {classScalarNode.Value}", ValidationSeverity.Error));
                }
            }

            // Validate styles
            if (baseNode.Children.TryGetValue(new YamlScalarNode("styles"), out var stylesNode))
            {
                if (stylesNode is YamlSequenceNode stylesSequenceNode)
                {
                    ValidateStylesSequence($"{path}.styles", stylesSequenceNode, errors);
                }
                else
                {
                    errors.Add(new ValidationError($"{path}.styles", "Styles must be a sequence", ValidationSeverity.Error));
                }
            }

            // Validate media queries
            if (baseNode.Children.TryGetValue(new YamlScalarNode("media"), out var mediaNode))
            {
                if (mediaNode is YamlMappingNode mediaMappingNode)
                {
                    ValidateMediaQueries($"{path}.media", mediaMappingNode, errors);
                }
                else
                {
                    errors.Add(new ValidationError($"{path}.media", "Media queries must be a mapping", ValidationSeverity.Error));
                }
            }

            // Validate states
            if (baseNode.Children.TryGetValue(new YamlScalarNode("states"), out var statesNode))
            {
                if (statesNode is YamlMappingNode statesMappingNode)
                {
                    ValidateStates($"{path}.states", statesMappingNode, errors);
                }
                else
                {
                    errors.Add(new ValidationError($"{path}.states", "States must be a mapping", ValidationSeverity.Error));
                }
            }
        }

        private void ValidateComponentParts(string path, YamlMappingNode partsNode, List<ValidationError> errors)
        {
            foreach (var (keyNode, valueNode) in partsNode.Children)
            {
                if (keyNode is not YamlScalarNode partKeyNode)
                {
                    errors.Add(new ValidationError(path, "Part key must be a scalar", ValidationSeverity.Error));
                    continue;
                }

                var partName = partKeyNode.Value;

                if (string.IsNullOrWhiteSpace(partName))
                {
                    errors.Add(new ValidationError(path, "Part name cannot be empty", ValidationSeverity.Error));
                    continue;
                }

                if (valueNode is YamlMappingNode partMappingNode)
                {
                    ValidateComponentBase($"{path}.{partName}", partMappingNode, errors);
                }
                else
                {
                    errors.Add(new ValidationError($"{path}.{partName}", "Part must be a mapping", ValidationSeverity.Error));
                }
            }
        }

        private void ValidateComponentVariants(string path, YamlMappingNode variantsNode, List<ValidationError> errors)
        {
            foreach (var (keyNode, valueNode) in variantsNode.Children)
            {
                if (keyNode is not YamlScalarNode variantKeyNode)
                {
                    errors.Add(new ValidationError(path, "Variant key must be a scalar", ValidationSeverity.Error));
                    continue;
                }

                var variantName = variantKeyNode.Value;

                if (string.IsNullOrWhiteSpace(variantName))
                {
                    errors.Add(new ValidationError(path, "Variant name cannot be empty", ValidationSeverity.Error));
                    continue;
                }

                if (valueNode is YamlMappingNode variantMappingNode)
                {
                    ValidateComponentBase($"{path}.{variantName}", variantMappingNode, errors);
                }
                else
                {
                    errors.Add(new ValidationError($"{path}.{variantName}", "Variant must be a mapping", ValidationSeverity.Error));
                }
            }
        }

        private void ValidateStylesSequence(string path, YamlSequenceNode stylesNode, List<ValidationError> errors)
        {
            int index = 0;
            foreach (var styleNode in stylesNode)
            {
                if (styleNode is YamlMappingNode styleMappingNode)
                {
                    if (styleMappingNode.Children.Count != 1)
                    {
                        errors.Add(new ValidationError($"{path}[{index}]", "Style entry should have exactly one property", ValidationSeverity.Warning));
                    }

                    foreach (var (propKeyNode, propValueNode) in styleMappingNode.Children)
                    {
                        if (propKeyNode is not YamlScalarNode propKeyScalarNode)
                        {
                            errors.Add(new ValidationError($"{path}[{index}]", "Style property name must be a scalar", ValidationSeverity.Error));
                            continue;
                        }

                        var propName = propKeyScalarNode.Value;

                        if (string.IsNullOrWhiteSpace(propName))
                        {
                            errors.Add(new ValidationError($"{path}[{index}]", "Style property name cannot be empty", ValidationSeverity.Error));
                            continue;
                        }

                        if (propValueNode is not YamlScalarNode propValueScalarNode)
                        {
                            errors.Add(new ValidationError($"{path}[{index}].{propName}", "Style property value must be a scalar", ValidationSeverity.Error));
                            continue;
                        }

                        if (string.IsNullOrWhiteSpace(propValueScalarNode.Value))
                        {
                            errors.Add(new ValidationError($"{path}[{index}].{propName}", "Style property value cannot be empty", ValidationSeverity.Error));
                        }
                    }
                }
                else
                {
                    errors.Add(new ValidationError($"{path}[{index}]", "Style must be a mapping", ValidationSeverity.Error));
                }

                index++;
            }
        }

        private void ValidateMediaQueries(string path, YamlMappingNode mediaNode, List<ValidationError> errors)
        {
            foreach (var (queryKeyNode, queryValueNode) in mediaNode.Children)
            {
                if (queryKeyNode is not YamlScalarNode queryKeyScalarNode)
                {
                    errors.Add(new ValidationError(path, "Media query must be a scalar", ValidationSeverity.Error));
                    continue;
                }

                var queryExpression = queryKeyScalarNode.Value;

                if (string.IsNullOrWhiteSpace(queryExpression))
                {
                    errors.Add(new ValidationError(path, "Media query cannot be empty", ValidationSeverity.Error));
                    continue;
                }

                if (queryValueNode is YamlMappingNode queryStylesNode)
                {
                    ValidateStylesMapping($"{path}.{queryExpression}", queryStylesNode, errors);
                }
                else
                {
                    errors.Add(new ValidationError($"{path}.{queryExpression}", "Media query styles must be a mapping", ValidationSeverity.Error));
                }
            }
        }

        private void ValidateStates(string path, YamlMappingNode statesNode, List<ValidationError> errors)
        {
            foreach (var (stateKeyNode, stateValueNode) in statesNode.Children)
            {
                if (stateKeyNode is not YamlScalarNode stateKeyScalarNode)
                {
                    errors.Add(new ValidationError(path, "State name must be a scalar", ValidationSeverity.Error));
                    continue;
                }

                var stateName = stateKeyScalarNode.Value;

                if (string.IsNullOrWhiteSpace(stateName))
                {
                    errors.Add(new ValidationError(path, "State name cannot be empty", ValidationSeverity.Error));
                    continue;
                }

                if (stateValueNode is YamlMappingNode stateStylesNode)
                {
                    ValidateStylesMapping($"{path}.{stateName}", stateStylesNode, errors);
                }
                else
                {
                    errors.Add(new ValidationError($"{path}.{stateName}", "State styles must be a mapping", ValidationSeverity.Error));
                }
            }
        }

        private void ValidateStylesMapping(string path, YamlMappingNode stylesNode, List<ValidationError> errors)
        {
            foreach (var (propKeyNode, propValueNode) in stylesNode.Children)
            {
                if (propKeyNode is not YamlScalarNode propKeyScalarNode)
                {
                    errors.Add(new ValidationError(path, "Style property name must be a scalar", ValidationSeverity.Error));
                    continue;
                }

                var propName = propKeyScalarNode.Value;

                if (string.IsNullOrWhiteSpace(propName))
                {
                    errors.Add(new ValidationError(path, "Style property name cannot be empty", ValidationSeverity.Error));
                    continue;
                }

                if (propValueNode is not YamlScalarNode propValueScalarNode)
                {
                    errors.Add(new ValidationError($"{path}.{propName}", "Style property value must be a scalar", ValidationSeverity.Error));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(propValueScalarNode.Value))
                {
                    errors.Add(new ValidationError($"{path}.{propName}", "Style property value cannot be empty", ValidationSeverity.Error));
                }
            }
        }

        private void ValidateStreetStyle(string name, YamlMappingNode styleNode, List<ValidationError> errors)
        {
            // Check for styles node
            if (styleNode.Children.TryGetValue(new YamlScalarNode("styles"), out var stylesNode))
            {
                if (stylesNode is YamlSequenceNode stylesSequenceNode)
                {
                    ValidateStylesSequence($"{name}.styles", stylesSequenceNode, errors);
                }
                else
                {
                    errors.Add(new ValidationError($"{name}.styles", "Styles must be a sequence", ValidationSeverity.Error));
                }
            }
            else
            {
                // No styles node found - warning only since other properties might be valid
                errors.Add(new ValidationError(name, "Street style missing 'styles' property", ValidationSeverity.Warning));
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
