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
        private readonly IPatternDetector _patternDetector;
        private readonly ClusterAnalyzer _clusterAnalyzer;
        private readonly StyleMetrics _metrics;
        private readonly PerformanceAnalyzer _performance;

        public StyleAnalyzer(
            ILogger<StyleAnalyzer> logger,
            IPatternDetector patternDetector,
            ClusterAnalyzer clusterAnalyzer,
            StyleMetrics metrics,
            PerformanceAnalyzer performance)
        {
            _logger = logger;
            _patternDetector = patternDetector;
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

                // Detect basic patterns
                var patterns = await _patternDetector.DetectPatternsAsync(styles, ct);
                _logger.LogDebug("Found {Count} basic patterns", patterns.PropertyPatterns.Count);

                // Perform cluster analysis
                var clusters = await _clusterAnalyzer.AnalyzeClustersAsync(patterns, options, ct);
                _logger.LogDebug("Generated {Count} pattern clusters", clusters.Count);

                // Collect metrics
                var metrics = _metrics.CalculateMetrics(styles, patterns, clusters);

                // Generate suggestions
                var suggestions = _clusterAnalyzer.GenerateSuggestions(clusters);
                _logger.LogDebug("Generated {Count} suggestions", suggestions.Count);

                return new AnalysisResult(
                    Patterns: patterns,
                    Clusters: clusters,
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
        IReadOnlyList<StyleSuggestion> Suggestions,
        StyleMetricsResult Metrics,
        PerformanceMetrics Performance
    );
}
