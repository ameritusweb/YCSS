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
    /// Interface for YAML validators that validate specific aspects of YAML content.
    /// </summary>
    public interface IYamlValidator
    {
        /// <summary>
        /// Validates the YAML node and returns any validation errors found.
        /// </summary>
        /// <param name="rootNode">The root YAML node to validate</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>A collection of validation errors, or an empty collection if no errors</returns>
        Task<IEnumerable<ValidationError>> ValidateAsync(YamlNode rootNode, CancellationToken ct = default);
    }
}
