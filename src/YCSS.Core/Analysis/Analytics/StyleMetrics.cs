using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YCSS.Core.Analysis.Patterns;
using YCSS.Core.Analysis.Clustering;
using MathNet.Numerics.Statistics;

namespace YCSS.Core.Analysis.Analytics
{
    public record StyleMetricsResult(
        PropertyMetrics Properties,
        ValueMetrics Values,
        ComplexityMetrics Complexity,
        DuplicationMetrics Duplication,
        StatisticalMetrics Statistics
    );

    public record PropertyMetrics(
        Dictionary<string, int> Frequencies,
        double AveragePropertiesPerRule,
        Dictionary<string, double> PropertyCorrelations,
        List<string> MostUsedProperties,
        List<string> LeastUsedProperties
    );

    public record ValueMetrics(
        Dictionary<string, ValueDistribution> Distributions,
        Dictionary<string, List<string>> CommonValues,
        Dictionary<string, double> ValueEntropy,
        List<string> NonStandardValues
    );

    public record ComplexityMetrics(
        double OverallComplexity,
        Dictionary<string, double> RuleComplexity,
        double SpecificityScore,
        double MaintenabilityIndex
    );

    public record DuplicationMetrics(
        int TotalDuplicates,
        List<DuplicateGroup> DuplicateGroups,
        double DuplicationRatio,
        Dictionary<string, int> ValueRepetitions
    );

    public record StatisticalMetrics(
        Dictionary<string, double> ChiSquareTests,
        Dictionary<string, double> MutualInformation,
        Dictionary<string, List<double>> Distributions,
        Dictionary<string, double> Significance
    );

    public record ValueDistribution(
        double Mean,
        double Median,
        double StdDev,
        List<double> Quartiles,
        List<string> Outliers
    );

    public record DuplicateGroup(
        List<string> Properties,
        List<string> Values,
        int Occurrences,
        double Similarity
    );

    public class StyleMetrics
    {
        private readonly int _minFrequency;
        private readonly double _significanceThreshold;

        public StyleMetrics(int minFrequency = 2, double significanceThreshold = 0.05)
        {
            _minFrequency = minFrequency;
            _significanceThreshold = significanceThreshold;
        }

        public StyleMetricsResult CalculateMetrics(
            Dictionary<string, object> styles,
            PatternAnalysis patterns,
            IReadOnlyList<StyleCluster> clusters)
        {
            // Extract all style rules and properties
            var rules = ExtractStyleRules(styles);

            return new StyleMetricsResult(
                Properties: CalculatePropertyMetrics(rules, patterns),
                Values: CalculateValueMetrics(rules),
                Complexity: CalculateComplexityMetrics(rules, clusters),
                Duplication: CalculateDuplicationMetrics(rules),
                Statistics: CalculateStatisticalMetrics(rules, patterns)
            );
        }

        private PropertyMetrics CalculatePropertyMetrics(
            List<StyleRule> rules,
            PatternAnalysis patterns)
        {
            // Calculate property frequencies
            var frequencies = rules
                .SelectMany(r => r.Properties.Keys)
                .GroupBy(p => p)
                .ToDictionary(g => g.Key, g => g.Count());

            // Calculate average properties per rule
            var avgProperties = rules.Average(r => r.Properties.Count);

            // Get correlations from pattern analysis
            var correlations = patterns.PropertyCorrelations;

            // Find most/least used properties
            var propertyUsage = frequencies.OrderByDescending(kvp => kvp.Value);
            var mostUsed = propertyUsage.Take(5).Select(kvp => kvp.Key).ToList();
            var leastUsed = propertyUsage.TakeLast(5).Select(kvp => kvp.Key).ToList();

            return new PropertyMetrics(
                Frequencies: frequencies,
                AveragePropertiesPerRule: avgProperties,
                PropertyCorrelations: correlations,
                MostUsedProperties: mostUsed,
                LeastUsedProperties: leastUsed
            );
        }

        private ValueMetrics CalculateValueMetrics(List<StyleRule> rules)
        {
            var distributions = new Dictionary<string, ValueDistribution>();
            var commonValues = new Dictionary<string, List<string>>();
            var entropy = new Dictionary<string, double>();
            var nonStandard = new List<string>();

            // Group values by property
            var valuesByProperty = rules
                .SelectMany(r => r.Properties)
                .GroupBy(kvp => kvp.Key)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(kvp => kvp.Value).ToList());

            foreach (var (prop, values) in valuesByProperty)
            {
                // Calculate numeric distributions where possible
                var numericValues = ExtractNumericValues(values);
                if (numericValues.Any())
                {
                    distributions[prop] = CalculateDistribution(numericValues);
                }

                // Find common values
                commonValues[prop] = values
                    .GroupBy(v => v)
                    .OrderByDescending(g => g.Count())
                    .Take(3)
                    .Select(g => g.Key)
                    .ToList();

                // Calculate value entropy
                entropy[prop] = CalculateEntropy(values);

                // Detect non-standard values
                var unusual = DetectNonStandardValues(values);
                nonStandard.AddRange(unusual);
            }

            return new ValueMetrics(
                Distributions: distributions,
                CommonValues: commonValues,
                ValueEntropy: entropy,
                NonStandardValues: nonStandard.Distinct().ToList()
            );
        }

        private ComplexityMetrics CalculateComplexityMetrics(
            List<StyleRule> rules,
            IReadOnlyList<StyleCluster> clusters)
        {
            var ruleComplexity = new Dictionary<string, double>();

            // Calculate complexity per rule
            foreach (var rule in rules)
            {
                var complexity = CalculateRuleComplexity(rule);
                ruleComplexity[rule.Selector] = complexity;
            }

            // Calculate overall metrics
            var overallComplexity = ruleComplexity.Values.Average();
            var specificityScore = CalculateSpecificityScore(rules);
            var maintainability = CalculateMaintainabilityIndex(
                rules, clusters, overallComplexity);

            return new ComplexityMetrics(
                OverallComplexity: overallComplexity,
                RuleComplexity: ruleComplexity,
                SpecificityScore: specificityScore,
                MaintenabilityIndex: maintainability
            );
        }

        private DuplicationMetrics CalculateDuplicationMetrics(List<StyleRule> rules)
        {
            var duplicateGroups = new List<DuplicateGroup>();
            var valueRepetitions = new Dictionary<string, int>();

            // Find duplicate property-value combinations
            var propertyGroups = rules
                .SelectMany(r => r.Properties)
                .GroupBy(kvp => $"{kvp.Key}:{kvp.Value}")
                .Where(g => g.Count() >= _minFrequency)
                .OrderByDescending(g => g.Count());

            foreach (var group in propertyGroups)
            {
                var parts = group.Key.Split(':');
                duplicateGroups.Add(new DuplicateGroup(
                    Properties: new List<string> { parts[0] },
                    Values: new List<string> { parts[1] },
                    Occurrences: group.Count(),
                    Similarity: 1.0
                ));

                valueRepetitions[parts[1]] = group.Count();
            }

            // Find similar property groups
            var similarGroups = FindSimilarPropertyGroups(rules);
            duplicateGroups.AddRange(similarGroups);

            var totalDuplicates = duplicateGroups.Sum(g => g.Occurrences);
            var duplicationRatio = totalDuplicates / (double)rules.Count;

            return new DuplicationMetrics(
                TotalDuplicates: totalDuplicates,
                DuplicateGroups: duplicateGroups,
                DuplicationRatio: duplicationRatio,
                ValueRepetitions: valueRepetitions
            );
        }

        private StatisticalMetrics CalculateStatisticalMetrics(
            List<StyleRule> rules,
            PatternAnalysis patterns)
        {
            var chiSquare = new Dictionary<string, double>();
            var mutualInfo = new Dictionary<string, double>();
            var distributions = new Dictionary<string, List<double>>();
            var significance = new Dictionary<string, double>();

            // Calculate chi-square test for property independence
            foreach (var (pair, correlation) in patterns.PropertyCorrelations)
            {
                var props = pair.Split('|');
                var chiSquareValue = CalculateChiSquare(rules, props[0], props[1]);
                chiSquare[pair] = chiSquareValue;

                // Calculate mutual information
                var mi = CalculateMutualInformation(rules, props[0], props[1]);
                mutualInfo[pair] = mi;

                // Calculate significance
                var pValue = CalculatePValue(chiSquareValue, 1); // df = 1 for 2x2
                significance[pair] = pValue;
            }

            // Calculate property value distributions
            foreach (var prop in rules.SelectMany(r => r.Properties.Keys).Distinct())
            {
                var values = rules
                    .Where(r => r.Properties.ContainsKey(prop))
                    .Select(r => r.Properties[prop])
                    .ToList();

                var numericValues = ExtractNumericValues(values);
                if (numericValues.Any())
                {
                    distributions[prop] = numericValues;
                }
            }

            return new StatisticalMetrics(
                ChiSquareTests: chiSquare,
                MutualInformation: mutualInfo,
                Distributions: distributions,
                Significance: significance
            );
        }

        private record StyleRule(
            string Selector,
            Dictionary<string, string> Properties
        );

        private List<StyleRule> ExtractStyleRules(Dictionary<string, object> styles)
        {
            var rules = new List<StyleRule>();

            foreach (var (selector, value) in styles)
            {
                if (value is not Dictionary<object, object> styleDict) continue;

                var properties = new Dictionary<string, string>();
                foreach (var (prop, val) in styleDict)
                {
                    properties[prop.ToString()!] = val?.ToString() ?? "";
                }

                rules.Add(new StyleRule(selector, properties));
            }

            return rules;
        }

        private List<double> ExtractNumericValues(List<string> values)
        {
            var numbers = new List<double>();
            foreach (var value in values)
            {
                // Extract numeric part and unit
                var numeric = value.TrimEnd("px%, rem, em, vh, vw".Split(", "));
                if (double.TryParse(numeric, out var number))
                {
                    numbers.Add(number);
                }
            }
            return numbers;
        }

        private ValueDistribution CalculateDistribution(List<double> values)
        {
            if (!values.Any()) throw new ArgumentException("Values cannot be empty");

            var stats = new DescriptiveStatistics(values);
            var quartiles = new List<double>
            {
                values.Quantile(0.25),
                values.Quantile(0.5),
                values.Quantile(0.75)
            };

            // Find outliers using IQR method
            var iqr = quartiles[2] - quartiles[0];
            var lowerBound = quartiles[0] - 1.5 * iqr;
            var upperBound = quartiles[2] + 1.5 * iqr;

            var outliers = values
                .Select(v => v.ToString())
                .Where(v => v < lowerBound || v > upperBound)
                .ToList();

            return new ValueDistribution(
                Mean: stats.Mean,
                Median: stats.Median,
                StdDev: stats.StandardDeviation,
                Quartiles: quartiles,
                Outliers: outliers
            );
        }

        private double CalculateEntropy(List<string> values)
        {
            var frequencies = values
                .GroupBy(v => v)
                .Select(g => g.Count() / (double)values.Count);

            return -frequencies.Sum(p => p * Math.Log(p, 2));
        }

        private List<string> DetectNonStandardValues(List<string> values)
        {
            // Simple heuristic: values that don't follow common patterns
            var nonStandard = new List<string>();

            foreach (var value in values)
            {
                // Check for unusual units or formats
                if (!IsStandardValue(value))
                {
                    nonStandard.Add(value);
                }
            }

            return nonStandard;
        }

        private bool IsStandardValue(string value)
        {
            // Common CSS value patterns
            var standardPatterns = new[]
            {
                @"^\d+px$",
                @"^\d+%$",
                @"^\d+rem$",
                @"^\d+em$",
                @"^#[0-9a-fA-F]{3,6}$",
                @"^rgb\(\d+,\s*\d+,\s*\d+\)$",
                @"^rgba\(\d+,\s*\d+,\s*\d+,\s*[\d.]+\)$",
                @"^(solid|dashed|dotted)$",
                @"^(bold|normal|\d+)$",
                @"^(flex|block|inline|grid)$"
            };

            return standardPatterns.Any(p => System.Text.RegularExpressions.Regex.IsMatch(value, p));
        }

        private double CalculateRuleComplexity(StyleRule rule)
        {
            var complexity = 0.0;

            // Base complexity from number of properties
            complexity += rule.Properties.Count;

            // Additional complexity for each non-standard value
            complexity += rule.Properties.Values.Count(v => !IsStandardValue(v)) * 0.5;

            // Complexity from selector (simplified)
            complexity += rule.Selector.Count(c => c == ' ' || c == '>' || c == '+') * 0.5;

            return complexity;
        }

        private double CalculateSpecificityScore(List<StyleRule> rules)
        {
            var total = 0.0;

            foreach (var rule in rules)
            {
                // Simple specificity calculation
                var score = 0.0;
                score += rule.Selector.Count(c => c == '#') * 100;    // ID
                score += rule.Selector.Count(c => c == '.') * 10;     // Class
                score += rule.Selector.Count(c => c == ':') * 10;     // Pseudo
                score += rule.Selector.Count(c => c == '[') * 10;     // Attribute

                total += score;
            }

            return total / rules.Count;
        }

        private double CalculateMaintainabilityIndex(
            List<StyleRule> rules,
            IReadOnlyList<StyleCluster> clusters,
            double complexity)
        {
            // Factors that improve maintainability
            var positiveFactors = new List<double>
            {
                clusters.Average(c => c.Cohesion),                     // Pattern cohesion
                1 - (rules.Count(HasImportant()) / (double)rules.Count), // Lack of !important
                1 - complexity / 100                                   // Inverse complexity
            };

            // Calculate weighted average (equal weights for now)
            return positiveFactors.Average() * 100;
        }

        private Func<StyleRule, bool> HasImportant()
        {
            return r => r.Properties.Values.Any(v => v.Contains("!important"));
        }

        private List<DuplicateGroup> FindSimilarPropertyGroups(List<StyleRule> rules)
        {
            var groups = new List<DuplicateGroup>();

            // Group rules by property sets
            var propertyGroups = rules
                .Select(r => r.Properties)
                .GroupBy(p => string.Join(",", p.Keys.OrderBy(k => k)));

            foreach (var group in propertyGroups.Where(g => g.Count() >= _minFrequency))
            {
                var properties = group.First().Keys.ToList();
                var values = group.Select(d => string.Join(",", properties.Select(p => d[p])))
                    .GroupBy(v => v)
                    .Where(g => g.Count() >= _minFrequency);

                foreach (var valueGroup in values)
                {
                    var valueList = valueGroup.Key.Split(',').ToList();
                    groups.Add(new DuplicateGroup(
                        Properties: properties,
                        Values: valueList,
                        Occurrences: valueGroup.Count(),
                        Similarity: 1.0
                    ));
                }
            }

            return groups;
        }

        private double CalculateChiSquare(
            List<StyleRule> rules,
            string prop1,
            string prop2)
        {
            // Create 2x2 contingency table
            var n11 = rules.Count(r => r.Properties.ContainsKey(prop1) && r.Properties.ContainsKey(prop2));
            var n12 = rules.Count(r => r.Properties.ContainsKey(prop1) && !r.Properties.ContainsKey(prop2));
            var n21 = rules.Count(r => !r.Properties.ContainsKey(prop1) && r.Properties.ContainsKey(prop2));
            var n22 = rules.Count(r => !r.Properties.ContainsKey(prop1) && !r.Properties.ContainsKey(prop2));

            var n = rules.Count;
            var r1 = n11 + n12;
            var r2 = n21 + n22;
            var c1 = n11 + n21;
            var c2 = n12 + n22;

            var e11 = (r1 * c1) / (double)n;
            var e12 = (r1 * c2) / (double)n;
            var e21 = (r2 * c1) / (double)n;
            var e22 = (r2 * c2) / (double)n;

            return
                Math.Pow(n11 - e11, 2) / e11 +
                Math.Pow(n12 - e12, 2) / e12 +
                Math.Pow(n21 - e21, 2) / e21 +
                Math.Pow(n22 - e22, 2) / e22;
        }

        private double CalculateMutualInformation(
            List<StyleRule> rules,
            string prop1,
            string prop2)
        {
            var n = rules.Count;
            
            // Joint probability
            var p11 = rules.Count(r => r.Properties.ContainsKey(prop1) && r.Properties.ContainsKey(prop2)) / (double)n;
            var p10 = rules.Count(r => r.Properties.ContainsKey(prop1) && !r.Properties.ContainsKey(prop2)) / (double)n;
            var p01 = rules.Count(r => !r.Properties.ContainsKey(prop1) && r.Properties.ContainsKey(prop2)) / (double)n;
            var p00 = rules.Count(r => !r.Properties.ContainsKey(prop1) && !r.Properties.ContainsKey(prop2)) / (double)n;

            // Marginal probabilities
            var p1_ = p11 + p10;
            var p0_ = p01 + p00;
            var p_1 = p11 + p01;
            var p_0 = p10 + p00;

            var mi = 0.0;

            // Add non-zero terms
            if (p11 > 0) mi += p11 * Math.Log(p11 / (p1_ * p_1), 2);
            if (p10 > 0) mi += p10 * Math.Log(p10 / (p1_ * p_0), 2);
            if (p01 > 0) mi += p01 * Math.Log(p01 / (p0_ * p_1), 2);
            if (p00 > 0) mi += p00 * Math.Log(p00 / (p0_ * p_0), 2);

            return mi;
        }

        private double CalculatePValue(double chiSquare, int degreesOfFreedom)
        {
            // Using chi-square distribution function from MathNet.Numerics
            return 1 - MathNet.Numerics.Distributions.ChiSquared.CDF(degreesOfFreedom, chiSquare);
        }
    }
}
