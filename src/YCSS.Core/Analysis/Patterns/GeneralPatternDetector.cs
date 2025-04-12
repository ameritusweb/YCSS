using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YCSS.Core.Analysis.Patterns
{
    public class GeneralPatternDetector : IPatternDetector
    {
        private readonly ILogger<GeneralPatternDetector> _logger;

        public GeneralPatternDetector(ILogger<GeneralPatternDetector> logger)
        {
            _logger = logger;
        }

        public async Task<PatternAnalysis> DetectPatternsAsync(
            Dictionary<string, object> styles,
            CancellationToken ct = default)
        {
            _logger.LogDebug("Starting pattern detection for {Count} styles", styles.Count);

            // Track property co-occurrences
            var coOccurrenceMatrix = new Dictionary<string, Dictionary<string, int>>();
            var propertyFrequencies = new Dictionary<string, int>();
            var valueFrequencies = new Dictionary<string, HashSet<string>>();

            // First pass: Collect raw statistics
            foreach (var (_, value) in styles)
            {
                if (ct.IsCancellationRequested) break;
                if (value is not Dictionary<object, object> styleDict) continue;

                var properties = styleDict.Keys.Select(k => k.ToString()!).ToList();

                // Track property frequencies
                foreach (var prop in properties)
                {
                    propertyFrequencies[prop] = propertyFrequencies.GetValueOrDefault(prop) + 1;
                }

                // Track property co-occurrences
                foreach (var prop1 in properties)
                {
                    if (!coOccurrenceMatrix.ContainsKey(prop1))
                    {
                        coOccurrenceMatrix[prop1] = new Dictionary<string, int>();
                    }

                    foreach (var prop2 in properties)
                    {
                        if (prop1 != prop2)
                        {
                            coOccurrenceMatrix[prop1][prop2] =
                                coOccurrenceMatrix[prop1].GetValueOrDefault(prop2) + 1;
                        }
                    }
                }

                // Track value patterns
                foreach (var (prop, val) in styleDict)
                {
                    var value = val?.ToString() ?? "";
                    if (!valueFrequencies.ContainsKey(value))
                    {
                        valueFrequencies[value] = new HashSet<string>();
                    }
                    valueFrequencies[value].Add(prop.ToString()!);
                }
            }

            if (ct.IsCancellationRequested)
            {
                _logger.LogInformation("Pattern detection cancelled");
                return new PatternAnalysis(
                    new Dictionary<string, PropertyPattern>(),
                    new Dictionary<string, ValuePattern>(),
                    new Dictionary<string, double>());
            }

            // Calculate correlations and find patterns
            var correlations = CalculateCorrelations(coOccurrenceMatrix, propertyFrequencies);
            var propertyPatterns = FindPropertyPatterns(correlations, styles);
            var valuePatterns = FindValuePatterns(valueFrequencies, styles);

            _logger.LogInformation(
                "Pattern detection complete. Found {PropertyCount} property patterns and {ValueCount} value patterns",
                propertyPatterns.Count,
                valuePatterns.Count);

            return new PatternAnalysis(
                propertyPatterns,
                valuePatterns,
                correlations);
        }

        public record ValuePattern(
            string Value,
            int Frequency,
            HashSet<string> Properties  // Properties where this value appears
        );

        public PatternAnalysis AnalyzeStyles(Dictionary<string, object> styles)
        {
            // Track property co-occurrences
            var coOccurrenceMatrix = new Dictionary<string, Dictionary<string, int>>();
            var propertyFrequencies = new Dictionary<string, int>();
            var valueFrequencies = new Dictionary<string, HashSet<string>>();

            // First pass: Collect raw statistics
            foreach (var (_, value) in styles)
            {
                if (value is not Dictionary<object, object> styleDict) continue;

                var properties = styleDict.Keys.Select(k => k.ToString()!).ToList();

                // Track property frequencies
                foreach (var prop in properties)
                {
                    propertyFrequencies[prop] = propertyFrequencies.GetValueOrDefault(prop) + 1;
                }

                // Track property co-occurrences
                foreach (var prop1 in properties)
                {
                    if (!coOccurrenceMatrix.ContainsKey(prop1))
                    {
                        coOccurrenceMatrix[prop1] = new Dictionary<string, int>();
                    }

                    foreach (var prop2 in properties)
                    {
                        if (prop1 != prop2)
                        {
                            coOccurrenceMatrix[prop1][prop2] =
                                coOccurrenceMatrix[prop1].GetValueOrDefault(prop2) + 1;
                        }
                    }
                }

                // Track value patterns
                foreach (var (prop, val) in styleDict)
                {
                    var value = val?.ToString() ?? "";
                    if (!valueFrequencies.ContainsKey(value))
                    {
                        valueFrequencies[value] = new HashSet<string>();
                    }
                    valueFrequencies[value].Add(prop.ToString()!);
                }
            }

            // Calculate property correlations
            var correlations = CalculateCorrelations(coOccurrenceMatrix, propertyFrequencies);

            // Group highly correlated properties into patterns
            var propertyPatterns = FindPropertyPatterns(correlations, styles);

            // Analyze value patterns
            var valuePatterns = FindValuePatterns(valueFrequencies, styles);

            return new PatternAnalysis
            {
                PropertyPatterns = propertyPatterns,
                ValuePatterns = valuePatterns,
                PropertyCorrelations = correlations
            };
        }

        private Dictionary<string, double> CalculateCorrelations(
            Dictionary<string, Dictionary<string, int>> coOccurrences,
            Dictionary<string, int> frequencies)
        {
            var correlations = new Dictionary<string, double>();

            foreach (var (prop1, coProps) in coOccurrences)
            {
                foreach (var (prop2, count) in coProps)
                {
                    // Calculate Jaccard similarity coefficient
                    var union = Math.Max(frequencies[prop1], frequencies[prop2]);
                    var correlation = count / (double)union;

                    var key = $"{prop1}|{prop2}";
                    correlations[key] = correlation;
                }
            }

            return correlations;
        }

        private Dictionary<string, PropertyPattern> FindPropertyPatterns(
            Dictionary<string, double> correlations,
            Dictionary<string, object> styles)
        {
            var patterns = new Dictionary<string, PropertyPattern>();
            var processed = new HashSet<string>();

            // Group properties by correlation strength
            foreach (var (pair, correlation) in correlations.OrderByDescending(c => c.Value))
            {
                if (correlation < 0.5) continue; // Correlation threshold

                var props = pair.Split('|');
                if (processed.Contains(props[0]) || processed.Contains(props[1])) continue;

                // Find all styles where these properties co-occur
                var coOccurringProps = new Dictionary<string, HashSet<string>>();
                var frequency = 0;

                foreach (var (_, value) in styles)
                {
                    if (value is not Dictionary<object, object> styleDict) continue;

                    if (styleDict.ContainsKey(props[0]) && styleDict.ContainsKey(props[1]))
                    {
                        frequency++;
                        foreach (var (prop, val) in styleDict)
                        {
                            var propName = prop.ToString()!;
                            if (!coOccurringProps.ContainsKey(propName))
                            {
                                coOccurringProps[propName] = new HashSet<string>();
                            }
                            coOccurringProps[propName].Add(val?.ToString() ?? "");
                        }
                    }
                }

                if (frequency >= 2) // Frequency threshold
                {
                    var patternName = $"pattern_{patterns.Count + 1}";
                    patterns[patternName] = new PropertyPattern(
                        coOccurringProps,
                        frequency,
                        correlation
                    );

                    processed.Add(props[0]);
                    processed.Add(props[1]);
                }
            }

            return patterns;
        }

        private Dictionary<string, ValuePattern> FindValuePatterns(
            Dictionary<string, HashSet<string>> valueFrequencies,
            Dictionary<string, object> styles)
        {
            var patterns = new Dictionary<string, ValuePattern>();

            foreach (var (value, props) in valueFrequencies)
            {
                if (string.IsNullOrWhiteSpace(value)) continue;

                var frequency = 0;
                foreach (var (_, styleDict) in styles)
                {
                    if (styleDict is not Dictionary<object, object> dict) continue;

                    if (dict.Values.Any(v => v?.ToString() == value))
                    {
                        frequency++;
                    }
                }

                if (frequency >= 2 && props.Count >= 1) // Thresholds
                {
                    var patternName = $"value_{patterns.Count + 1}";
                    patterns[patternName] = new ValuePattern(
                        value,
                        frequency,
                        props
                    );
                }
            }

            return patterns;
        }
    }
}
