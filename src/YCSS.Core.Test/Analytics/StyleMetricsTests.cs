using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using YCSS.Core.Analysis.Analytics;
using YCSS.Core.Analysis.Patterns;
using YCSS.Core.Analysis.Clustering;

namespace YCSS.Core.Test.Analytics
{
    [TestClass]
    public class StyleMetricsTests
    {
        private StyleMetrics _metrics;
        private Dictionary<string, object> _testStyles;
        private PatternAnalysis _testPatterns;
        private List<StyleCluster> _testClusters;

        [TestInitialize]
        public void Setup()
        {
            _metrics = new StyleMetrics(minFrequency: 2);

            // Setup test data
            _testStyles = new Dictionary<string, object>
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
                    ["padding"] = "2rem",
                    ["box-shadow"] = "0 1px 3px rgba(0,0,0,0.1)"
                },
                ["input"] = new Dictionary<object, object>
                {
                    ["border"] = "1px solid #ccc",
                    ["padding"] = "1rem",
                    ["border-radius"] = "4px"
                }
            };

            // Setup patterns
            var propertyPatterns = new Dictionary<string, PropertyPattern>
            {
                ["pattern1"] = new PropertyPattern(
                    new Dictionary<string, HashSet<string>>
                    {
                        ["padding"] = new HashSet<string> { "1rem", "2rem" },
                        ["border-radius"] = new HashSet<string> { "4px" }
                    },
                    Frequency: 2,
                    Cohesion: 0.8
                )
            };

            var valuePatterns = new Dictionary<string, ValuePattern>
            {
                ["value1"] = new ValuePattern(
                    "4px",
                    Frequency: 2,
                    new HashSet<string> { "border-radius" }
                )
            };

            var correlations = new Dictionary<string, double>
            {
                ["padding|border-radius"] = 0.75
            };

            _testPatterns = new PatternAnalysis(
                propertyPatterns,
                valuePatterns,
                correlations
            );

            // Setup clusters
            _testClusters = new List<StyleCluster>
            {
                new StyleCluster
                {
                    Properties = new HashSet<string> { "padding", "border-radius" },
                    Values = new HashSet<string> { "1rem", "4px" },
                    Cohesion = 0.8,
                    Frequency = 2
                }
            };
        }

        [TestMethod]
        public void CalculateMetrics_ShouldCalculatePropertyMetrics()
        {
            // Act
            var result = _metrics.CalculateMetrics(_testStyles, _testPatterns, _testClusters);

            // Assert
            Assert.IsNotNull(result.Properties);
            Assert.AreEqual(6, result.Properties.Frequencies.Count);
            Assert.AreEqual(3, result.Properties.AveragePropertiesPerRule);
            Assert.IsTrue(result.Properties.MostUsedProperties.Contains("padding"));
        }

        [TestMethod]
        public void CalculateMetrics_ShouldCalculateValueMetrics()
        {
            // Act
            var result = _metrics.CalculateMetrics(_testStyles, _testPatterns, _testClusters);

            // Assert
            Assert.IsNotNull(result.Values);
            Assert.IsTrue(result.Values.Distributions.ContainsKey("border-radius"));
            Assert.IsTrue(result.Values.CommonValues["padding"].Contains("1rem"));
            Assert.IsTrue(result.Values.ValueEntropy.ContainsKey("padding"));
        }

        [TestMethod]
        public void CalculateMetrics_ShouldCalculateComplexityMetrics()
        {
            // Act
            var result = _metrics.CalculateMetrics(_testStyles, _testPatterns, _testClusters);

            // Assert
            Assert.IsNotNull(result.Complexity);
            Assert.IsTrue(result.Complexity.OverallComplexity > 0);
            Assert.AreEqual(3, result.Complexity.RuleComplexity.Count);
            Assert.IsTrue(result.Complexity.MaintenabilityIndex > 0);
        }

        [TestMethod]
        public void CalculateMetrics_ShouldCalculateDuplicationMetrics()
        {
            // Act
            var result = _metrics.CalculateMetrics(_testStyles, _testPatterns, _testClusters);

            // Assert
            Assert.IsNotNull(result.Duplication);
            Assert.IsTrue(result.Duplication.TotalDuplicates >= 2); // padding and border-radius
            Assert.IsTrue(result.Duplication.DuplicateGroups.Count > 0);
            Assert.IsTrue(result.Duplication.DuplicationRatio > 0);
        }

        [TestMethod]
        public void CalculateMetrics_ShouldCalculateStatisticalMetrics()
        {
            // Act
            var result = _metrics.CalculateMetrics(_testStyles, _testPatterns, _testClusters);

            // Assert
            Assert.IsNotNull(result.Statistics);
            Assert.IsTrue(result.Statistics.ChiSquareTests.ContainsKey("padding|border-radius"));
            Assert.IsTrue(result.Statistics.MutualInformation.ContainsKey("padding|border-radius"));
            Assert.IsTrue(result.Statistics.Significance.ContainsKey("padding|border-radius"));
        }

        [TestMethod]
        public void CalculateMetrics_ShouldHandleEmptyStyles()
        {
            // Arrange
            var emptyStyles = new Dictionary<string, object>();

            // Act
            var result = _metrics.CalculateMetrics(emptyStyles, _testPatterns, _testClusters);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Properties.Frequencies.Count);
            Assert.AreEqual(0, result.Values.CommonValues.Count);
            Assert.AreEqual(0, result.Complexity.RuleComplexity.Count);
        }

        [TestMethod]
        public void CalculateMetrics_ShouldDetectNonStandardValues()
        {
            // Arrange
            _testStyles["custom"] = new Dictionary<object, object>
            {
                ["margin"] = "weird-value",
                ["color"] = "not-a-color"
            };

            // Act
            var result = _metrics.CalculateMetrics(_testStyles, _testPatterns, _testClusters);

            // Assert
            Assert.IsTrue(result.Values.NonStandardValues.Contains("weird-value"));
            Assert.IsTrue(result.Values.NonStandardValues.Contains("not-a-color"));
        }

        [TestMethod]
        public void CalculateMetrics_ShouldCalculateNumericDistributions()
        {
            // Arrange
            _testStyles["spacing1"] = new Dictionary<object, object> { ["margin"] = "10px" };
            _testStyles["spacing2"] = new Dictionary<object, object> { ["margin"] = "20px" };
            _testStyles["spacing3"] = new Dictionary<object, object> { ["margin"] = "30px" };

            // Act
            var result = _metrics.CalculateMetrics(_testStyles, _testPatterns, _testClusters);

            // Assert
            Assert.IsTrue(result.Values.Distributions.ContainsKey("margin"));
            var distribution = result.Values.Distributions["margin"];
            Assert.AreEqual(20, distribution.Mean);
            Assert.AreEqual(20, distribution.Median);
            Assert.AreEqual(10, distribution.StdDev, 0.01);
        }
    }
}