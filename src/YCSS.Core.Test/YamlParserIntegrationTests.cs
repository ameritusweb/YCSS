using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YCSS.Core.Test.Providers;
using YCSS.Core.Utils;

namespace YCSS.Core.Test
{
    public class YamlParserIntegrationTests
    {
        private readonly YamlParser _parser;

        public YamlParserIntegrationTests()
        {
            _parser = TestServiceProvider.GetService<YamlParser>();
        }

        [Fact]
        public async Task Parse_BasicTokens_ParsesTokensCorrectly()
        {
            // Arrange
            var yaml = await File.ReadAllTextAsync("TestData/basic-tokens.yaml");

            // Act
            var (tokens, components, styles) = _parser.Parse(yaml);

            // Assert
            Assert.NotNull(tokens);
            Assert.NotEmpty(tokens);
            Assert.Equal(9, tokens.Count);

            // Verify specific tokens
            Assert.True(tokens.ContainsKey("color-primary"));
            Assert.Equal("#1f2937", tokens["color-primary"].Value);

            Assert.True(tokens.ContainsKey("spacing-md"));
            Assert.Equal("1rem", tokens["spacing-md"].Value);

            Assert.True(tokens.ContainsKey("radius-md"));
            Assert.Equal("0.5rem", tokens["radius-md"].Value);

            // Verify no components or street styles
            Assert.Empty(components);
            Assert.Empty(styles);
        }

        [Fact]
        public async Task Parse_BasicComponent_ParsesComponentCorrectly()
        {
            // Arrange
            var yaml = await File.ReadAllTextAsync("TestData/basic-component.yaml");

            // Act
            var (tokens, components, styles) = _parser.Parse(yaml);

            // Assert
            Assert.Empty(tokens);
            Assert.Empty(components);
            Assert.NotEmpty(styles);

            // Verify button style
            Assert.True(styles.ContainsKey("button"));
            Assert.Equal("button", styles["button"].Class);
            Assert.NotEmpty(styles["button"].Styles);

            // Verify style properties
            var buttonStyles = styles["button"].Styles;
            Assert.Contains(buttonStyles, s => s.Property == "background-color" && s.Value == "var(--color-primary)");
            Assert.Contains(buttonStyles, s => s.Property == "padding" && s.Value == "var(--spacing-md)");
            Assert.Contains(buttonStyles, s => s.Property == "border-radius" && s.Value == "var(--radius-md)");
        }

        [Fact]
        public async Task Parse_ComplexDesignSystem_ParsesAllElementsCorrectly()
        {
            // Arrange
            var yaml = await File.ReadAllTextAsync("TestData/complex-design-system.yaml");

            // Act
            var (tokens, components, styles) = _parser.Parse(yaml);

            // Assert
            Assert.NotNull(tokens);
            Assert.NotEmpty(tokens);
            Assert.Equal(32, tokens.Count);

            Assert.NotNull(components);
            Assert.NotEmpty(components);
            Assert.Equal(3, components.Count);

            Assert.NotNull(styles);
            Assert.NotEmpty(styles);
            Assert.Equal(8, styles.Count);

            // Verify specific components
            Assert.True(components.ContainsKey("button"));
            Assert.True(components.ContainsKey("card"));
            Assert.True(components.ContainsKey("alert"));

            // Verify component structure
            var button = components["button"];
            Assert.NotNull(button.Base);
            Assert.NotEmpty(button.Variants);
            Assert.Equal(8, button.Variants.Count);

            var card = components["card"];
            Assert.NotNull(card.Base);
            Assert.NotEmpty(card.Parts);
            Assert.Equal(3, card.Parts.Count);
            Assert.NotEmpty(card.Variants);
            Assert.Equal(2, card.Variants.Count);

            // Verify street styles
            Assert.True(styles.ContainsKey("text-primary"));
            Assert.True(styles.ContainsKey("bg-primary"));
            Assert.True(styles.ContainsKey("flex-row"));
        }
    }
}
