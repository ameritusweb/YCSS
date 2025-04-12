using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YCSS.Core.Exceptions;
using YCSS.Core.Test.Providers;
using YCSS.Core.Validation;

namespace YCSS.Core.Test
{
    public class StyleValidatorIntegrationTests
    {
        private readonly IStyleValidator _validator;

        public StyleValidatorIntegrationTests()
        {
            _validator = TestServiceProvider.GetService<IStyleValidator>();
        }

        [Fact]
        public async Task ValidateAsync_ValidBasicTokens_ReturnsValidResult()
        {
            // Arrange
            var yaml = await File.ReadAllTextAsync("TestData/basic-tokens.yaml");

            // Act
            var result = await _validator.ValidateAsync(yaml);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsValid);
            Assert.NotNull(result.Definition);
            Assert.NotEmpty(result.Definition.Tokens);
            Assert.Equal(9, result.Definition.Tokens.Count);
        }

        [Fact]
        public async Task ValidateAsync_ValidComponent_ReturnsValidResult()
        {
            // Arrange
            var yaml = await File.ReadAllTextAsync("TestData/basic-component.yaml");

            // Act
            var result = await _validator.ValidateAsync(yaml);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsValid);
            Assert.NotNull(result.Definition);
            Assert.Empty(result.Definition.Tokens);
            Assert.Empty(result.Definition.Components);
            Assert.NotEmpty(result.Definition.StreetStyles);
            Assert.Single(result.Definition.StreetStyles);
        }

        [Fact]
        public async Task ValidateAsync_ComplexDesignSystem_ReturnsValidResult()
        {
            // Arrange
            var yaml = await File.ReadAllTextAsync("TestData/complex-design-system.yaml");

            // Act
            var result = await _validator.ValidateAsync(yaml);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsValid);
            Assert.NotNull(result.Definition);
            Assert.NotEmpty(result.Definition.Tokens);
            Assert.NotEmpty(result.Definition.Components);
            Assert.NotEmpty(result.Definition.StreetStyles);
            Assert.Equal(32, result.Definition.Tokens.Count);
            Assert.Equal(3, result.Definition.Components.Count);
            Assert.Equal(8, result.Definition.StreetStyles.Count);
        }

        [Fact]
        public async Task ValidateAsync_InvalidYAML_ReturnsInvalidResult()
        {
            // Arrange
            var invalidYaml = await File.ReadAllTextAsync("TestData/invalid-yaml.yaml");

            // Act & Assert
            await Assert.ThrowsAsync<YCSSValidationException>(() =>
                _validator.ValidateAsync(invalidYaml));
        }

        [Fact]
        public async Task ValidateAsync_StyleDefinition_ValidatesComplexStructure()
        {
            // Arrange
            var yaml = await File.ReadAllTextAsync("TestData/complex-design-system.yaml");
            var initialResult = await _validator.ValidateAsync(yaml);
            var definition = initialResult.Definition!;

            // Act
            var result = await _validator.ValidateAsync(definition);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsValid);
            Assert.NotNull(result.Definition);
            Assert.Equal(32, result.Definition.Tokens.Count);
            Assert.Equal(3, result.Definition.Components.Count);
            Assert.Equal(8, result.Definition.StreetStyles.Count);
        }
    }
}
