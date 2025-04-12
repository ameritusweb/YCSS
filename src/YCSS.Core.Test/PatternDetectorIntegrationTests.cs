using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YCSS.Core.Analysis.Patterns;
using YCSS.Core.Models;
using YCSS.Core.Test.Providers;
using YCSS.Core.Utils;

namespace YCSS.Core.Test
{
    public class PatternDetectorIntegrationTests
    {
        private readonly IPatternDetector _patternDetector;
        private readonly YamlParser _parser;

        public PatternDetectorIntegrationTests()
        {
            _patternDetector = TestServiceProvider.GetService<IPatternDetector>();
            _parser = TestServiceProvider.GetService<YamlParser>();
        }

        [Fact]
        public async Task DetectPatternsAsync_WithDuplicationPatterns_DetectsPropertyPatterns()
        {
            // Arrange
            var yaml = await File.ReadAllTextAsync("TestData/duplication-patterns.yaml");
            var (_, _, styles) = _parser.Parse(yaml);

            // Convert to Dictionary<string, object> for pattern detector
            var styleDict = styles.ToDictionary<KeyValuePair<string, ComponentBaseDefinition>, string, object>(
                kvp => kvp.Key,
                kvp => (object)kvp.Value
            );

            // Act
            var result = await _patternDetector.DetectPatternsAsync(styleDict);

            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result.PropertyPatterns);

            // Verify property patterns
            Assert.True(result.PropertyPatterns.Count > 0);

            // Find the box pattern (padding, margin, border-radius)
            var boxPattern = result.PropertyPatterns.Values.FirstOrDefault(p =>
                p.Properties.ContainsKey("padding") &&
                p.Properties.ContainsKey("margin") &&
                p.Properties.ContainsKey("border-radius"));

            Assert.NotNull(boxPattern);
            Assert.Equal(3, boxPattern.Frequency); // Should occur in all 3 box styles
            Assert.True(boxPattern.Cohesion > 0.5); // Should have high cohesion

            // Find the card pattern (display, flex-direction, gap)
            var cardPattern = result.PropertyPatterns.Values.FirstOrDefault(p =>
                p.Properties.ContainsKey("display") &&
                p.Properties.ContainsKey("flex-direction") &&
                p.Properties.ContainsKey("gap"));

            Assert.NotNull(cardPattern);
            Assert.Equal(2, cardPattern.Frequency); // Should occur in both card styles
            Assert.True(cardPattern.Cohesion > 0.5); // Should have high cohesion

            // Find the text pattern (font-size, line-height, color)
            var textPattern = result.PropertyPatterns.Values.FirstOrDefault(p =>
                p.Properties.ContainsKey("font-size") &&
                p.Properties.ContainsKey("line-height") &&
                p.Properties.ContainsKey("color"));

            Assert.NotNull(textPattern);
            Assert.Equal(2, textPattern.Frequency); // Should occur in both text styles
            Assert.True(textPattern.Cohesion > 0.5); // Should have high cohesion
        }

        [Fact]
        public async Task DetectPatternsAsync_WithDuplicationPatterns_DetectsValuePatterns()
        {
            // Arrange
            var yaml = await File.ReadAllTextAsync("TestData/duplication-patterns.yaml");
            var (_, _, styles) = _parser.Parse(yaml);

            // Convert to Dictionary<string, object> for pattern detector
            var styleDict = styles.ToDictionary<KeyValuePair<string, ComponentBaseDefinition>, string, object>(
                kvp => kvp.Key,
                kvp => (object)kvp.Value
            );

            // Act
            var result = await _patternDetector.DetectPatternsAsync(styleDict);

            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result.ValuePatterns);

            // Find common values
            var paddingPattern = result.ValuePatterns.Values.FirstOrDefault(p =>
                p.Value == "1rem" && p.Properties.Contains("padding"));

            Assert.NotNull(paddingPattern);
            Assert.True(paddingPattern.Frequency >= 5); // Should occur in multiple styles

            var lineHeightPattern = result.ValuePatterns.Values.FirstOrDefault(p =>
                p.Value == "1.5" && p.Properties.Contains("line-height"));

            Assert.NotNull(lineHeightPattern);
            Assert.Equal(2, lineHeightPattern.Frequency); // Should occur in both text styles
        }

        [Fact]
        public async Task DetectPatternsAsync_WithComplexDesignSystem_DetectsComponentPatterns()
        {
            // Arrange
            var yaml = await File.ReadAllTextAsync("TestData/complex-design-system.yaml");
            var (_, components, _) = _parser.Parse(yaml);

            // Convert to Dictionary<string, object> for pattern detector
            var componentDict = components.ToDictionary<KeyValuePair<string, ComponentDefinition>, string, object>(
                kvp => kvp.Key,
                kvp => (object)kvp.Value
            );

            // Act
            var result = await _patternDetector.DetectPatternsAsync(componentDict);

            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result.PropertyPatterns);
            Assert.NotEmpty(result.ValuePatterns);
            Assert.NotEmpty(result.PropertyCorrelations);

            // Verify property correlations
            Assert.True(result.PropertyCorrelations.Count > 0);

            // Verify value patterns
            var commonValues = result.ValuePatterns.Values
                .Where(p => p.Frequency > 1)
                .ToList();

            Assert.NotEmpty(commonValues);
        }
    }
}
