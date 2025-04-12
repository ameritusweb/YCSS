using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YCSS.Core.Analysis.Clustering;
using YCSS.Core.Caching;
using YCSS.Core.Compilation;
using YCSS.Core.Exceptions;
using YCSS.Core.Logging;
using YCSS.Core.Validation;

namespace YCSS.Core.Pipeline
{
    public interface IStylePipeline
    {
        Task<CompilationResult> CompileAsync(
            string yamlContent,
            CompilerOptions options,
            CancellationToken ct = default);

        Task<AnalysisResult> AnalyzeAsync(
            string yamlContent,
            bool useCache = true,
            CancellationToken ct = default);
    }

    public class StylePipeline : IStylePipeline
    {
        private readonly ILogger<StylePipeline> _logger;
        private readonly IStyleValidator _validator;
        private readonly IStyleCompiler _compiler;
        private readonly IPatternDetector _patternDetector;
        private readonly IAnalysisCache _cache;
        private readonly PerformanceLogger _performanceLogger;

        public StylePipeline(
            ILogger<StylePipeline> logger,
            IStyleValidator validator,
            IStyleCompiler compiler,
            IPatternDetector patternDetector,
            IAnalysisCache cache)
        {
            _logger = logger;
            _validator = validator;
            _compiler = compiler;
            _patternDetector = patternDetector;
            _cache = cache;
            _performanceLogger = new PerformanceLogger();
        }

        public async Task<CompilationResult> CompileAsync(
            string yamlContent,
            CompilerOptions options,
            CancellationToken ct = default)
        {
            using var scope = _logger.BeginStyleOperation("Style Compilation");
            var sw = Stopwatch.StartNew();

            try
            {
                // Validate YAML
                var validationResult = await _validator.ValidateAsync(yamlContent, ct);
                if (!validationResult.IsValid)
                {
                    throw new YCSSValidationException(validationResult.Errors);
                }

                var definition = validationResult.Definition!;

                // Compile styles
                var output = await Task.Run(() =>
                    _compiler.CompileStyles(definition, options), ct);

                sw.Stop();
                _performanceLogger.RecordMetric("Compilation", sw.Elapsed);

                return new CompilationResult(
                    Output: output,
                    SourceMap: null, // TODO: Implement source mapping
                    Statistics: new CompilationStats(
                        TokenCount: definition.Tokens.Count,
                        ComponentCount: definition.Components.Count,
                        OutputSize: output.Length
                    )
                );
            }
            catch (Exception ex) when (ex is not YCSSException)
            {
                _logger.LogError(ex, "Unexpected error during compilation");
                throw new YCSSCompilationException(
                    "An unexpected error occurred during compilation",
                    inner: ex);
            }
        }

        public async Task<AnalysisResult> AnalyzeAsync(
            string yamlContent,
            bool useCache = true,
            CancellationToken ct = default)
        {
            using var scope = _logger.BeginStyleOperation("Style Analysis");
            var sw = Stopwatch.StartNew();

            try
            {
                // Generate cache key if using cache
                string? cacheKey = null;
                if (useCache)
                {
                    using var hash = SHA256.Create();
                    var inputBytes = Encoding.UTF8.GetBytes(yamlContent);
                    var hashBytes = hash.ComputeHash(inputBytes);
                    cacheKey = Convert.ToBase64String(hashBytes);
                }

                // Try to get from cache
                if (cacheKey != null)
                {
                    var cached = await _cache.GetAsync<AnalysisResult>(cacheKey, ct);
                    if (cached != null)
                    {
                        _logger.LogInformation("Retrieved analysis result from cache");
                        return cached;
                    }
                }

                // Validate YAML
                var validationResult = await _validator.ValidateAsync(yamlContent, ct);
                if (!validationResult.IsValid)
                {
                    throw new YCSSValidationException(validationResult.Errors);
                }

                var definition = validationResult.Definition!;

                // Analyze patterns
                var patterns = await Task.Run(() =>
                    _patternDetector.FindPatternHierarchy(definition.Components), ct);

                // Create analysis result
                var result = new AnalysisResult(
                    Patterns: patterns,
                    Statistics: new AnalysisStats(
                        TokenCount: definition.Tokens.Count,
                        ComponentCount: definition.Components.Count,
                        PatternCount: patterns.Count,
                        AverageCohesion: patterns.Average(p => p.Cohesion)
                    ),
                    Suggestions: GenerateSuggestions(patterns)
                );

                // Cache the result
                if (cacheKey != null)
                {
                    await _cache.SetAsync(cacheKey, result, TimeSpan.FromHours(1), ct);
                }

                sw.Stop();
                _performanceLogger.RecordMetric("Analysis", sw.Elapsed);

                return result;
            }
            catch (Exception ex) when (ex is not YCSSException)
            {
                _logger.LogError(ex, "Unexpected error during analysis");
                throw new YCSSException(
                    "An unexpected error occurred during analysis",
                    ex);
            }
        }

        private IReadOnlyList<StyleSuggestion> GenerateSuggestions(
            IReadOnlyList<StyleCluster> patterns)
        {
            var suggestions = new List<StyleSuggestion>();

            foreach (var pattern in patterns.Where(p => p.Cohesion >= 0.8))
            {
                suggestions.Add(new StyleSuggestion(
                    Type: SuggestionType.UtilityClass,
                    Description: $"Consider creating a utility class for these highly cohesive properties",
                    Properties: pattern.Properties,
                    Confidence: pattern.Cohesion
                ));
            }

            // Add more suggestion types here...

            return suggestions;
        }
    }

    public record CompilationResult(
        string Output,
        string? SourceMap,
        CompilationStats Statistics
    );

    public record CompilationStats(
        int TokenCount,
        int ComponentCount,
        int OutputSize
    );

    public record AnalysisResult(
        IReadOnlyList<StyleCluster> Patterns,
        AnalysisStats Statistics,
        IReadOnlyList<StyleSuggestion> Suggestions
    );

    public record AnalysisStats(
        int TokenCount,
        int ComponentCount,
        int PatternCount,
        double AverageCohesion
    );

    public record StyleSuggestion(
        SuggestionType Type,
        string Description,
        IReadOnlySet<string> Properties,
        double Confidence
    );

    public enum SuggestionType
    {
        UtilityClass,
        CSSVariable,
        Mixin,
        ComponentRefactor
    }
}
