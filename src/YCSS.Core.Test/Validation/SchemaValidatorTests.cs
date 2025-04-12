using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Threading.Tasks;
using YamlDotNet.RepresentationModel;
using YCSS.Core.Validation;
using System.IO;

namespace YCSS.Core.Test.Validation
{
    [TestClass]
    public class SchemaValidatorTests
    {
        private SchemaValidator _validator;
        private Mock<ILogger<SchemaValidator>> _logger;

        [TestInitialize]
        public void Setup()
        {
            _logger = new Mock<ILogger<SchemaValidator>>();
            _validator = new SchemaValidator(_logger.Object);
        }

        [TestMethod]
        public async Task ValidateSchemaAsync_ValidYaml_ReturnsSuccess()
        {
            // Arrange
            var yaml = @"
version: 1.0.0
tokens:
  color-primary: '#1f2937'
  spacing-lg: '2rem'
components:
  button:
    class: button
    styles:
      - background-color: var(--color-primary)
      - padding: var(--spacing-lg)
";
            var yamlStream = new YamlStream();
            using var reader = new StringReader(yaml);
            yamlStream.Load(reader);

            // Act
            var result = await _validator.ValidateSchemaAsync(
                (YamlMappingNode)yamlStream.Documents[0].RootNode);

            // Assert
            Assert.IsTrue(result.IsValid);
            Assert.AreEqual(0, result.Errors.Count);
            Assert.AreEqual(1, result.Version.Major);
        }

        [TestMethod]
        public async Task ValidateSchemaAsync_InvalidTokenName_ReturnsError()
        {
            // Arrange
            var yaml = @"
tokens:
  123-invalid: '#1f2937'  # Token names must start with a letter
";
            var yamlStream = new YamlStream();
            using var reader = new StringReader(yaml);
            yamlStream.Load(reader);

            // Act
            var result = await _validator.ValidateSchemaAsync(
                (YamlMappingNode)yamlStream.Documents[0].RootNode);

            // Assert
            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.Errors.Count > 0);
            Assert.IsTrue(result.Errors[0].Message.Contains("Token name"));
        }

        [TestMethod]
        public async Task ValidateSchemaAsync_InvalidColorValue_ReturnsError()
        {
            // Arrange
            var yaml = @"
tokens:
  color-primary: 'not-a-color'  # Invalid color format
";
            var yamlStream = new YamlStream();
            using var reader = new StringReader(yaml);
            yamlStream.Load(reader);

            // Act
            var result = await _validator.ValidateSchemaAsync(
                (YamlMappingNode)yamlStream.Documents[0].RootNode);

            // Assert
            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.Errors.Count > 0);
            Assert.IsTrue(result.Errors[0].Message.Contains("color"));
        }

        [TestMethod]
        public async Task ValidateSchemaAsync_MissingRequiredProperties_ReturnsError()
        {
            // Arrange
            var yaml = @"
components:
  button:  # Missing required background-color and padding
    class: button
    styles:
      - border: 1px solid black
";
            var yamlStream = new YamlStream();
            using var reader = new StringReader(yaml);
            yamlStream.Load(reader);

            // Act
            var result = await _validator.ValidateSchemaAsync(
                (YamlMappingNode)yamlStream.Documents[0].RootNode);

            // Assert
            Assert.IsFalse(result.IsValid);
            var hasBackgroundColorError = result.Errors.Any(e => 
                e.Message.Contains("background-color") && 
                e.Severity == ValidationSeverity.Error);
            var hasPaddingError = result.Errors.Any(e => 
                e.Message.Contains("padding") && 
                e.Severity == ValidationSeverity.Error);
            Assert.IsTrue(hasBackgroundColorError);
            Assert.IsTrue(hasPaddingError);
        }

        [TestMethod]
        public async Task ValidateSchemaAsync_MissingRecommendedProperties_ReturnsWarning()
        {
            // Arrange
            var yaml = @"
components:
  button:
    class: button
    styles:
      - background-color: '#000'
      - padding: 1rem
";
            var yamlStream = new YamlStream();
            using var reader = new StringReader(yaml);
            yamlStream.Load(reader);

            // Act
            var result = await _validator.ValidateSchemaAsync(
                (YamlMappingNode)yamlStream.Documents[0].RootNode);

            // Assert
            Assert.IsTrue(result.IsValid); // Warnings don't make it invalid
            var hasWarnings = result.Errors.Any(e => 
                e.Severity == ValidationSeverity.Warning);
            Assert.IsTrue(hasWarnings);
        }

        [TestMethod]
        public async Task ValidateSchemaAsync_InvalidBemClassName_ReturnsError()
        {
            // Arrange
            var yaml = @"
components:
  button:
    class: button__primary--invalid--extra  # Invalid BEM format
    styles:
      - background-color: '#000'
      - padding: 1rem
";
            var yamlStream = new YamlStream();
            using var reader = new StringReader(yaml);
            yamlStream.Load(reader);

            // Act
            var result = await _validator.ValidateSchemaAsync(
                (YamlMappingNode)yamlStream.Documents[0].RootNode);

            // Assert
            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.Errors.Any(e => 
                e.Message.Contains("BEM format")));
        }

        [TestMethod]
        public async Task ValidateSchemaAsync_ValidVariants_ReturnsSuccess()
        {
            // Arrange
            var yaml = @"
components:
  button:
    class: button
    styles:
      - background-color: '#000'
      - padding: 1rem
    variants:
      primary:
        class: button--primary
        styles:
          - background-color: '#007bff'
      secondary:
        class: button--secondary
        styles:
          - background-color: '#6c757d'
";
            var yamlStream = new YamlStream();
            using var reader = new StringReader(yaml);
            yamlStream.Load(reader);

            // Act
            var result = await _validator.ValidateSchemaAsync(
                (YamlMappingNode)yamlStream.Documents[0].RootNode);

            // Assert
            Assert.IsTrue(result.IsValid);
            Assert.AreEqual(0, result.Errors.Count);
        }

        [TestMethod]
        public async Task ValidateSchemaAsync_InvalidVariantName_ReturnsError()
        {
            // Arrange
            var yaml = @"
components:
  button:
    class: button
    styles:
      - background-color: '#000'
      - padding: 1rem
    variants:
      123-invalid:  # Invalid variant name
        class: button--primary
        styles:
          - background-color: '#007bff'
";
            var yamlStream = new YamlStream();
            using var reader = new StringReader(yaml);
            yamlStream.Load(reader);

            // Act
            var result = await _validator.ValidateSchemaAsync(
                (YamlMappingNode)yamlStream.Documents[0].RootNode);

            // Assert
            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.Errors.Any(e => 
                e.Message.Contains("Variant name")));
        }

        [TestMethod]
        public async Task ValidateSchemaAsync_ValidCssProperties_ReturnsSuccess()
        {
            // Arrange
            var yaml = @"
components:
  container:
    class: container
    styles:
      - display: flex
      - flex-direction: row
      - justify-content: space-between
      - align-items: center
      - padding: 1rem
      - gap: 10px
      - width: 100%
      - height: auto
      - border: 1px solid #000
      - border-radius: 4px
      - font-size: 16px
      - font-weight: bold
      - line-height: 1.5
";
            var yamlStream = new YamlStream();
            using var reader = new StringReader(yaml);
            yamlStream.Load(reader);

            // Act
            var result = await _validator.ValidateSchemaAsync(
                (YamlMappingNode)yamlStream.Documents[0].RootNode);

            // Assert
            Assert.IsTrue(result.IsValid);
            Assert.AreEqual(0, result.Errors.Count);
        }

        [TestMethod]
        public async Task ValidateSchemaAsync_InvalidCssValues_ReturnsErrors()
        {
            // Arrange
            var yaml = @"
components:
  container:
    class: container
    styles:
      - display: invalid
      - flex-direction: wrong
      - padding: 1rem 2rem 3rem 4rem 5rem  # Too many values
      - border: thick-border  # Invalid format
      - font-weight: 123  # Not a multiple of 100
";
            var yamlStream = new YamlStream();
            using var reader = new StringReader(yaml);
            yamlStream.Load(reader);

            // Act
            var result = await _validator.ValidateSchemaAsync(
                (YamlMappingNode)yamlStream.Documents[0].RootNode);

            // Assert
            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.Errors.Count >= 5);
        }
    }
}