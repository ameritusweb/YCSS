using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YCSS.Core.Analysis.Analytics;
using YCSS.Core.Analysis.Clustering;
using YCSS.Core.Analysis.Patterns;
using YCSS.Core.Logging;
using YCSS.Core.Pipeline;

namespace YCSS.Core.Analysis
{
    public interface IStyleAnalyzer
    {
        Task<AnalysisResult> AnalyzeAsync(
            Dictionary<string, object> styles,
            AnalysisOptions? options = null,
            CancellationToken ct = default);
    }

    public class StyleAnalyzer : IStyleAnalyzer
    {
        private readonly ILogger<StyleAnalyzer> _logger;
        private readonly GeneralPatternDetector _generalPatternDetector;
        private readonly HierarchicalPatternDetector _hierarchicalPatternDetector;
        private readonly BEMAnalyzer _bemAnalyzer;
        private readonly IClusterAnalyzer _clusterAnalyzer;
        private readonly StyleMetrics _metrics;
        private readonly PerformanceAnalyzer _performance;

        public StyleAnalyzer(
            ILogger<StyleAnalyzer> logger,
            GeneralPatternDetector generalPatternDetector,
            HierarchicalPatternDetector hierarchicalPatternDetector,
            BEMAnalyzer bemAnalyzer,
            IClusterAnalyzer clusterAnalyzer,
            StyleMetrics metrics,
            PerformanceAnalyzer performance)
        {
            _logger = logger;
            _generalPatternDetector = generalPatternDetector;
            _hierarchicalPatternDetector = hierarchicalPatternDetector;
            _bemAnalyzer = bemAnalyzer;
            _clusterAnalyzer = clusterAnalyzer;
            _metrics = metrics;
            _performance = performance;
        }

        public async Task<AnalysisResult> AnalyzeAsync(
            Dictionary<string, object> styles,
            AnalysisOptions? options = null,
            CancellationToken ct = default)
        {
            using var scope = _logger.BeginScope("Style Analysis");
            options ??= new AnalysisOptions();

            try
            {
                _logger.LogInformation("Starting style analysis with {Count} rules", styles.Count);
                using var perfTracker = _performance.TrackOperation("StyleAnalysis");

                // Detect patterns using both detectors in parallel
                var patternTasks = new[]
                {
                    _generalPatternDetector.DetectPatternsAsync(styles, ct),
                    Task.Run(() => _hierarchicalPatternDetector.FindPatternHierarchy(styles), ct)
                };

                await Task.WhenAll(patternTasks);

                var generalPatterns = await patternTasks[0];
                var hierarchicalPatterns = await patternTasks[1];

                _logger.LogDebug(
                    "Found {GeneralCount} general patterns and {HierarchicalCount} hierarchical patterns",
                    generalPatterns.PropertyPatterns.Count,
                    hierarchicalPatterns.Count);

                // Merge pattern analysis results
                var mergedPatterns = MergePatternResults(generalPatterns, hierarchicalPatterns);

                // Perform cluster analysis
                var clusters = hierarchicalPatterns;
                _logger.LogDebug("Using {Count} pattern clusters", clusters.Count);

                // Collect metrics
                var metrics = _metrics.CalculateMetrics(styles, mergedPatterns, clusters);

                // Generate suggestions
                // Run BEM analysis
                var bemResult = await _bemAnalyzer.AnalyzeAsync(styles, ct);
                _logger.LogDebug("Completed BEM analysis with {Count} components", bemResult.Components.Count);

                // Generate suggestions from all analyzers
                var suggestions = GenerateSuggestions(mergedPatterns, clusters, bemResult);
                _logger.LogDebug("Generated {Count} suggestions", suggestions.Count);

                return new AnalysisResult(
                    Patterns: mergedPatterns,
                    Clusters: clusters,
                    BEMAnalysis: bemResult,
                    Suggestions: suggestions,
                    Metrics: metrics,
                    Performance: perfTracker.GetMetrics());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Style analysis failed");
                throw;
            }
        }

        private PatternAnalysis MergePatternResults(
            PatternAnalysis generalPatterns,
            List<StyleCluster> hierarchicalPatterns)
        {
            var mergedPropertyPatterns = new Dictionary<string, PropertyPattern>(
                generalPatterns.PropertyPatterns);

            // Convert hierarchical patterns to property patterns
            foreach (var cluster in hierarchicalPatterns)
            {
                var patternKey = $"hierarchical_pattern_{cluster.Id}";
                
                var properties = new Dictionary<string, HashSet<string>>();
                foreach (var prop in cluster.Properties)
                {
                    properties[prop] = cluster.Values;
                }

                mergedPropertyPatterns[patternKey] = new PropertyPattern(
                    properties,
                    cluster.Frequency,
                    cluster.Cohesion
                );
            }

            return new PatternAnalysis(
                mergedPropertyPatterns,
                generalPatterns.ValuePatterns,
                generalPatterns.PropertyCorrelations
            );
        }

        private List<StyleSuggestion> GenerateSuggestions(
            PatternAnalysis patterns,
            List<StyleCluster> clusters,
            BEMAnalysis bemAnalysis)
        {
            var suggestions = new List<StyleSuggestion>();

            // Add suggestions from general patterns
            foreach (var (name, pattern) in patterns.PropertyPatterns)
            {
                if (pattern.Cohesion >= 0.8 && pattern.Frequency >= 3)
                {
                    suggestions.Add(new StyleSuggestion(
                        SuggestionType.UtilityClass,
                        $"Create utility class for frequently co-occurring properties",
                        pattern.Properties.Keys.ToHashSet(),
                        pattern.Cohesion
                    ));
                }
            }

            // Add suggestions from value patterns
            foreach (var (value, pattern) in patterns.ValuePatterns)
            {
                if (pattern.Frequency >= 3)
                {
                    suggestions.Add(new StyleSuggestion(
                        SuggestionType.CSSVariable,
                        $"Extract frequently used value into CSS variable",
                        pattern.Properties,
                        pattern.Frequency / 10.0 // Normalize frequency to 0-1 range
                    ));
                }
            }

            // Add suggestions from hierarchical patterns
            foreach (var cluster in clusters)
            {
                if (cluster.Cohesion >= 0.8 && cluster.Children.Any())
                {
                    suggestions.Add(new StyleSuggestion(
                        SuggestionType.Mixin,
                        $"Create mixin for hierarchical pattern with variants",
                        cluster.Properties,
                        cluster.Cohesion
                    ));
                }
            }

            // Add BEM-specific suggestions
            foreach (var suggestion in bemAnalysis.Suggestions)
            {
                suggestions.Add(new StyleSuggestion(
                    suggestion.Type,
                    suggestion.Description,
                    new HashSet<string> { suggestion.Current, suggestion.Suggested },
                    suggestion.Confidence
                ));
            }

            // Add relationship-based suggestions
            foreach (var relationship in bemAnalysis.Relationships.Where(r => r.Type == RelationType.Extension))
            {
                if (relationship.Confidence >= 0.8)
                {
                    suggestions.Add(new StyleSuggestion(
                        SuggestionType.Relationship,
                        $"Components share styles and could be related through BEM",
                        new HashSet<string> { relationship.SourceComponent, relationship.TargetComponent },
                        relationship.Confidence
                    ));
                }
            }

            // Look for opportunities to extract shared styles within BEM blocks
            var blockGroups = bemAnalysis.Components
                .GroupBy(c => c.Block)
                .Where(g => g.Count() >= 3);

            foreach (var group in blockGroups)
            {
                var sharedProperties = FindSharedProperties(group.Select(c => c.Styles));
                if (sharedProperties.Any())
                {
                    suggestions.Add(new StyleSuggestion(
                        SuggestionType.SharedStyles,
                        $"Extract shared styles for {group.Key} components",
                        sharedProperties,
                        0.9
                    ));
                }
            }

            return suggestions
                .OrderByDescending(s => s.Confidence)
                .ThenBy(s => s.Type)
                .ToList();
        }

        private HashSet<string> FindSharedProperties(IEnumerable<Dictionary<string, object>> stylesList)
        {
            var allStyles = stylesList.ToList();
            if (!allStyles.Any()) return new HashSet<string>();

            // Start with all properties from the first style
            var shared = new HashSet<string>(
                allStyles.First()
                    .Where(kvp => IsValueEqual(kvp.Value, allStyles.Skip(1)))
                    .Select(kvp => kvp.Key));

            return shared;
        }

        private bool IsValueEqual(object value, IEnumerable<Dictionary<string, object>> otherStyles)
        {
            var stringValue = value?.ToString();
            return otherStyles.All(style =>
                style.Any(kvp => kvp.Value?.ToString() == stringValue));
        }
        }
    }

    public record AnalysisOptions
    {
        public double MinimumCohesion { get; init; } = 0.5;
        public int MinimumFrequency { get; init; } = 2;
        public int MaxDepth { get; init; } = 3;
        public bool IncludeMetrics { get; init; } = true;
        public bool TrackPerformance { get; init; } = true;
    }

    public record AnalysisResult(
        PatternAnalysis Patterns,
        IReadOnlyList<StyleCluster> Clusters,
        BEMAnalysis BEMAnalysis,
        IReadOnlyList<StyleSuggestion> Suggestions,
        StyleMetricsResult Metrics,
        PerformanceMetrics Performance
    );
}
