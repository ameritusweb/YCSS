using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Threading.Tasks;
using YCSS.Core.Analysis;
using YCSS.Core.Analysis.Analytics;
using YCSS.Core.Analysis.Clustering;
using YCSS.Core.Analysis.Patterns;

namespace YCSS.Core.Test.Analysis
{
    [TestClass]
    public class StyleAnalyzerTests
    {
        private StyleAnalyzer _analyzer;
        private Mock<ILogger<StyleAnalyzer>> _logger;
        private GeneralPatternDetector _generalDetector;
        private HierarchicalPatternDetector _hierarchicalDetector;
        private Mock<IClusterAnalyzer> _clusterAnalyzer;
        private StyleMetrics _metrics;
        private Mock<PerformanceAnalyzer> _performance;

        [TestInitialize]
        public void Setup()
        {
            _logger = new Mock<ILogger<StyleAnalyzer>>();
            _generalDetector = new GeneralPatternDetector(Mock.Of<ILogger<GeneralPatternDetector>>());
            _hierarchicalDetector = new HierarchicalPatternDetector();
            _clusterAnalyzer = new Mock<IClusterAnalyzer>();
            _metrics = new StyleMetrics();
            _performance = new Mock<PerformanceAnalyzer>();

            _analyzer = new StyleAnalyzer(
                _logger.Object,
                _generalDetector,
                _hierarchicalDetector,
                _clusterAnalyzer.Object,
                _metrics,
                _performance.Object);
        }

        [TestMethod]
        public async Task AnalyzeAsync_WithSimpleStyles_DetectsPatternsFromBothDetectors()
        {
            // Arrange
            var styles = new Dictionary<string, object>
            {
                ["button"] = new Dictionary<object, object>
                {
                    ["background-color"] = "#1f2937",
                    ["padding"] = "1rem",
                    ["border-radius"] = "4px"
                },
                ["card"] = new Dictionary<object, object>
                {
                    ["background-color"] = "#ffffff",
                    ["padding"] = "1rem",
                    ["border-radius"] = "4px"
                }
            };

            // Act
            var result = await _analyzer.AnalyzeAsync(styles);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Patterns);
            
            // Verify general patterns
            Assert.IsTrue(result.Patterns.PropertyPatterns.Any(p => 
                p.Value.Properties.ContainsKey("padding") && 
                p.Value.Properties.ContainsKey("border-radius")));

            // Verify hierarchical patterns
            Assert.IsTrue(result.Clusters.Any(c => 
                c.Properties.Contains("padding") && 
                c.Properties.Contains("border-radius")));
        }

        [TestMethod]
        public async Task AnalyzeAsync_WithComplexStyles_GeneratesAppropriateMetrics()
        {
            // Arrange
            var styles = new Dictionary<string, object>
            {
                ["button"] = new Dictionary<object, object>
                {
                    ["display"] = "flex",
                    ["justify-content"] = "center",
                    ["align-items"] = "center",
                    ["padding"] = "1rem 2rem",
                    ["background-color"] = "#1f2937",
                    ["border-radius"] = "4px"
                },
                ["card"] = new Dictionary<object, object>
                {
                    ["display"] = "flex",
                    ["flex-direction"] = "column",
                    ["padding"] = "2rem",
                    ["background-color"] = "#ffffff",
                    ["border-radius"] = "8px",
                    ["box-shadow"] = "0 1px 3px rgba(0,0,0,0.1)"
                },
                ["nav-item"] = new Dictionary<object, object>
                {
                    ["display"] = "flex",
                    ["align-items"] = "center",
                    ["padding"] = "0.5rem 1rem",
                    ["color"] = "#1f2937"
                }
            };

            // Act
            var result = await _analyzer.AnalyzeAsync(styles);

            // Assert
            Assert.IsNotNull(result.Metrics);

            // Check for flex pattern detection
            var hasFlexPattern = result.Patterns.PropertyPatterns.Any(p =>
                p.Value.Properties.ContainsKey("display") &&
                p.Value.Properties["display"].Contains("flex"));
            Assert.IsTrue(hasFlexPattern);

            // Check for hierarchical pattern with flex-related properties
            var hasFlexCluster = result.Clusters.Any(c =>
                c.Properties.Contains("display") &&
                c.Properties.Contains("align-items"));
            Assert.IsTrue(hasFlexCluster);

            // Verify suggestions were generated
            Assert.IsTrue(result.Suggestions.Any());
            Assert.IsTrue(result.Suggestions.Any(s => s.Type == SuggestionType.UtilityClass));
        }

        [TestMethod]
        public async Task AnalyzeAsync_WithDuplicateValues_SuggestsCSSVariables()
        {
            // Arrange
            var styles = new Dictionary<string, object>
            {
                ["button"] = new Dictionary<object, object>
                {
                    ["background-color"] = "#1f2937",
                    ["color"] = "#ffffff"
                },
                ["header"] = new Dictionary<object, object>
                {
                    ["background-color"] = "#1f2937",
                    ["padding"] = "1rem"
                },
                ["nav"] = new Dictionary<object, object>
                {
                    ["background-color"] = "#1f2937",
                    ["margin-bottom"] = "1rem"
                }
            };

            // Act
            var result = await _analyzer.AnalyzeAsync(styles);

            // Assert
            Assert.IsNotNull(result);

            // Verify value patterns were detected
            Assert.IsTrue(result.Patterns.ValuePatterns.Any(p => 
                p.Value.Value == "#1f2937" && 
                p.Value.Frequency >= 3));

            // Verify CSS variable suggestions
            var cssVarSuggestions = result.Suggestions
                .Where(s => s.Type == SuggestionType.CSSVariable)
                .ToList();

            Assert.IsTrue(cssVarSuggestions.Any());
            Assert.IsTrue(cssVarSuggestions.Any(s => 
                s.Properties.Contains("background-color")));
        }

        [TestMethod]
        public async Task AnalyzeAsync_WithNestedPatterns_DetectsHierarchy()
        {
            // Arrange
            var styles = new Dictionary<string, object>
            {
                ["card"] = new Dictionary<object, object>
                {
                    ["display"] = "flex",
                    ["flex-direction"] = "column",
                    ["background-color"] = "#ffffff",
                    ["border-radius"] = "8px",
                    ["padding"] = "1rem"
                },
                ["card-header"] = new Dictionary<object, object>
                {
                    ["display"] = "flex",
                    ["justify-content"] = "space-between",
                    ["align-items"] = "center",
                    ["padding"] = "1rem",
                    ["border-bottom"] = "1px solid #e5e7eb"
                },
                ["card-body"] = new Dictionary<object, object>
                {
                    ["display"] = "flex",
                    ["flex-direction"] = "column",
                    ["padding"] = "1rem"
                }
            };

            // Act
            var result = await _analyzer.AnalyzeAsync(styles);

            // Assert
            Assert.IsNotNull(result);

            // Verify hierarchical patterns
            var hasParentChildRelation = result.Clusters.Any(c =>
                c.Properties.Contains("display") &&
                c.Properties.Contains("padding") &&
                c.Children.Any(child =>
                    child.Properties.Contains("flex-direction") ||
                    child.Properties.Contains("justify-content")));

            Assert.IsTrue(hasParentChildRelation);

            // Verify mixin suggestions for hierarchical patterns
            var mixinSuggestions = result.Suggestions
                .Where(s => s.Type == SuggestionType.Mixin)
                .ToList();

            Assert.IsTrue(mixinSuggestions.Any());
            Assert.IsTrue(mixinSuggestions.Any(s =>
                s.Properties.Contains("display") &&
                s.Properties.Contains("padding")));
        }
    }
}