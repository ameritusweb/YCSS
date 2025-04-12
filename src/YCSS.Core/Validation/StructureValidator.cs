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
    /// Performs general YAML structure validation.
    /// </summary>
    public class StructureValidator : IYamlValidator
    {
        private readonly ILogger<StructureValidator> _logger;

        public StructureValidator(ILogger<StructureValidator> logger)
        {
            _logger = logger;
        }

        public Task<IEnumerable<ValidationError>> ValidateAsync(YamlNode rootNode, CancellationToken ct = default)
        {
            var errors = new List<ValidationError>();

            if (rootNode == null)
            {
                errors.Add(new ValidationError("", "YAML content cannot be null or empty", ValidationSeverity.Error));
                return Task.FromResult<IEnumerable<ValidationError>>(errors);
            }

            if (rootNode is not YamlMappingNode mappingNode)
            {
                errors.Add(new ValidationError("", "Root YAML node must be a mapping", ValidationSeverity.Error));
                return Task.FromResult<IEnumerable<ValidationError>>(errors);
            }

            // Check for valid root nodes
            var validRootNodes = new[]
            {
                "tokens",
                "components"
            };

            var hasValidContent = false;
            foreach (var rootName in validRootNodes)
            {
                if (mappingNode.Children.ContainsKey(new YamlScalarNode(rootName)))
                {
                    hasValidContent = true;
                    break;
                }
            }

            // If no valid root nodes found, check if any "street styles" are defined
            if (!hasValidContent && mappingNode.Children.Count > 0)
            {
                // Check if any root nodes have a styles property
                foreach (var (keyNode, valueNode) in mappingNode.Children)
                {
                    if (keyNode is YamlScalarNode scalarKey &&
                        valueNode is YamlMappingNode valueMapping &&
                        valueMapping.Children.ContainsKey(new YamlScalarNode("styles")))
                    {
                        hasValidContent = true;
                        break;
                    }
                }
            }

            if (!hasValidContent)
            {
                errors.Add(new ValidationError("", "YAML must contain tokens, components, or street styles", ValidationSeverity.Error));
            }

            return Task.FromResult<IEnumerable<ValidationError>>(errors);
        }
    }
}
