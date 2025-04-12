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
    /// Validates token definitions in YAML.
    /// </summary>
    public class TokenValidator : IYamlValidator
    {
        private readonly ILogger<TokenValidator> _logger;

        public TokenValidator(ILogger<TokenValidator> logger)
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

            // Check for tokens node
            if (mappingNode.Children.TryGetValue(new YamlScalarNode("tokens"), out var tokensNode))
            {
                if (tokensNode is YamlMappingNode tokensMappingNode)
                {
                    ValidateTokens(tokensMappingNode, errors);
                }
                else
                {
                    errors.Add(new ValidationError("tokens", "Tokens must be a mapping", ValidationSeverity.Error));
                }
            }

            return Task.FromResult<IEnumerable<ValidationError>>(errors);
        }

        private void ValidateTokens(YamlMappingNode tokensNode, List<ValidationError> errors)
        {
            foreach (var (keyNode, valueNode) in tokensNode.Children)
            {
                if (keyNode is not YamlScalarNode tokenKeyNode)
                {
                    errors.Add(new ValidationError("tokens", "Token key must be a scalar", ValidationSeverity.Error));
                    continue;
                }

                var tokenName = tokenKeyNode.Value;

                // Validate token name format
                if (string.IsNullOrWhiteSpace(tokenName))
                {
                    errors.Add(new ValidationError("tokens", "Token name cannot be empty", ValidationSeverity.Error));
                    continue;
                }

                // Validate token value
                if (valueNode is YamlScalarNode tokenValueNode)
                {
                    if (string.IsNullOrWhiteSpace(tokenValueNode.Value))
                    {
                        errors.Add(new ValidationError($"tokens.{tokenName}", "Token value cannot be empty", ValidationSeverity.Error));
                    }
                }
                else if (valueNode is YamlMappingNode tokenMappingNode)
                {
                    ValidateTokenObjectStructure(tokenName, tokenMappingNode, errors);
                }
                else
                {
                    errors.Add(new ValidationError($"tokens.{tokenName}", "Token value must be a scalar or mapping", ValidationSeverity.Error));
                }
            }
        }

        private void ValidateTokenObjectStructure(string tokenName, YamlMappingNode tokenNode, List<ValidationError> errors)
        {
            // Check required fields for token object
            if (!tokenNode.Children.ContainsKey(new YamlScalarNode("value")))
            {
                errors.Add(new ValidationError($"tokens.{tokenName}", "Token object must have a 'value' property", ValidationSeverity.Error));
            }
            else if (tokenNode.Children[new YamlScalarNode("value")] is not YamlScalarNode valueNode)
            {
                errors.Add(new ValidationError($"tokens.{tokenName}.value", "Token value must be a scalar", ValidationSeverity.Error));
            }
            else if (string.IsNullOrWhiteSpace(valueNode.Value))
            {
                errors.Add(new ValidationError($"tokens.{tokenName}.value", "Token value cannot be empty", ValidationSeverity.Error));
            }

            // Validate theme overrides if present
            if (tokenNode.Children.TryGetValue(new YamlScalarNode("themeOverrides"), out var overridesNode))
            {
                if (overridesNode is YamlMappingNode overridesMappingNode)
                {
                    foreach (var (themeKeyNode, themeValueNode) in overridesMappingNode.Children)
                    {
                        if (themeKeyNode is not YamlScalarNode themeNameNode)
                        {
                            errors.Add(new ValidationError($"tokens.{tokenName}.themeOverrides", "Theme name must be a scalar", ValidationSeverity.Error));
                            continue;
                        }

                        var themeName = themeNameNode.Value;

                        if (string.IsNullOrWhiteSpace(themeName))
                        {
                            errors.Add(new ValidationError($"tokens.{tokenName}.themeOverrides", "Theme name cannot be empty", ValidationSeverity.Error));
                            continue;
                        }

                        if (themeValueNode is not YamlScalarNode themeValueScalarNode)
                        {
                            errors.Add(new ValidationError($"tokens.{tokenName}.themeOverrides.{themeName}", "Theme value must be a scalar", ValidationSeverity.Error));
                            continue;
                        }

                        if (string.IsNullOrWhiteSpace(themeValueScalarNode.Value))
                        {
                            errors.Add(new ValidationError($"tokens.{tokenName}.themeOverrides.{themeName}", "Theme value cannot be empty", ValidationSeverity.Error));
                        }
                    }
                }
                else
                {
                    errors.Add(new ValidationError($"tokens.{tokenName}.themeOverrides", "Theme overrides must be a mapping", ValidationSeverity.Error));
                }
            }
        }
    }
}
