using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YCSS.Core.Analysis.Clustering
{
    public class HierarchicalPatternDetector
    {
        private readonly double _minCohesion;
        private readonly int _minFrequency;
        private readonly int _maxDepth;

        public HierarchicalPatternDetector(
            double minCohesion = 0.5,
            int minFrequency = 2,
            int maxDepth = 3)
        {
            _minCohesion = minCohesion;
            _minFrequency = minFrequency;
            _maxDepth = maxDepth;
        }

        public List<StyleCluster> FindPatternHierarchy(Dictionary<string, object> styles)
        {
            // First, extract all property-value pairs
            var styleRules = ExtractStyleRules(styles);

            // Start with the most frequent, highly cohesive patterns
            return BuildClusterHierarchy(styleRules);
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

                rules.Add(new StyleRule(selector.ToString()!, properties));
            }

            return rules;
        }

        private List<StyleCluster> BuildClusterHierarchy(List<StyleRule> rules)
        {
            var rootClusters = new List<StyleCluster>();
            var processedProperties = new HashSet<string>();

            // Calculate property co-occurrence matrix
            var coOccurrenceMatrix = BuildCoOccurrenceMatrix(rules);

            // Start with the most frequent property combinations
            var frequentSets = FindFrequentPropertySets(rules, _minFrequency);

            foreach (var set in frequentSets.OrderByDescending(s => s.Properties.Count))
            {
                // Skip if all properties in this set are already part of a cluster
                if (set.Properties.All(p => processedProperties.Contains(p))) continue;

                var cluster = BuildCluster(set.Properties, rules, coOccurrenceMatrix, 0);
                if (cluster != null)
                {
                    rootClusters.Add(cluster);
                    processedProperties.UnionWith(cluster.Properties);
                }
            }

            return rootClusters;
        }

        private Dictionary<string, Dictionary<string, int>> BuildCoOccurrenceMatrix(List<StyleRule> rules)
        {
            var matrix = new Dictionary<string, Dictionary<string, int>>();

            foreach (var rule in rules)
            {
                var properties = rule.Properties.Keys.ToList();

                foreach (var prop1 in properties)
                {
                    if (!matrix.ContainsKey(prop1))
                    {
                        matrix[prop1] = new Dictionary<string, int>();
                    }

                    foreach (var prop2 in properties)
                    {
                        if (prop1 != prop2)
                        {
                            matrix[prop1][prop2] = matrix[prop1].GetValueOrDefault(prop2) + 1;
                        }
                    }
                }
            }

            return matrix;
        }

        private record FrequentSet(HashSet<string> Properties, int Frequency);

        private List<FrequentSet> FindFrequentPropertySets(List<StyleRule> rules, int minSupport)
        {
            var sets = new List<FrequentSet>();
            var candidates = rules
                .SelectMany(r => r.Properties.Keys)
                .Distinct()
                .Select(p => new HashSet<string> { p })
                .ToList();

            // Start with single properties
            foreach (var prop in candidates)
            {
                var frequency = rules.Count(r => prop.All(p => r.Properties.ContainsKey(p)));
                if (frequency >= minSupport)
                {
                    sets.Add(new FrequentSet(prop, frequency));
                }
            }

            // Iteratively build larger sets
            var k = 1;
            while (candidates.Any() && k < 5) // Limit to reasonable size
            {
                var newCandidates = new List<HashSet<string>>();

                // Generate k+1 sized candidates
                for (var i = 0; i < candidates.Count; i++)
                {
                    for (var j = i + 1; j < candidates.Count; j++)
                    {
                        var union = new HashSet<string>(candidates[i]);
                        union.UnionWith(candidates[j]);

                        if (union.Count == k + 1)
                        {
                            var frequency = rules.Count(r =>
                                union.All(p => r.Properties.ContainsKey(p)));

                            if (frequency >= minSupport)
                            {
                                newCandidates.Add(union);
                                sets.Add(new FrequentSet(union, frequency));
                            }
                        }
                    }
                }

                candidates = newCandidates;
                k++;
            }

            return sets;
        }

        private StyleCluster? BuildCluster(
            HashSet<string> properties,
            List<StyleRule> rules,
            Dictionary<string, Dictionary<string, int>> coOccurrenceMatrix,
            int depth)
        {
            if (depth >= _maxDepth || properties.Count < 2) return null;

            // Calculate cluster cohesion
            var cohesion = CalculateClusterCohesion(properties, coOccurrenceMatrix);
            if (cohesion < _minCohesion) return null;

            // Find rules that contain all properties in this cluster
            var matchingRules = rules
                .Where(r => properties.All(p => r.Properties.ContainsKey(p)))
                .ToList();

            if (matchingRules.Count < _minFrequency) return null;

            // Collect all values for these properties
            var values = new HashSet<string>();
            foreach (var rule in matchingRules)
            {
                foreach (var prop in properties)
                {
                    values.Add(rule.Properties[prop]);
                }
            }

            // Find sub-patterns within this cluster
            var children = new List<StyleCluster>();
            var remainingProps = matchingRules
                .SelectMany(r => r.Properties.Keys)
                .Except(properties)
                .ToHashSet();

            if (remainingProps.Any())
            {
                var subClusters = FindSubClusters(
                    remainingProps,
                    matchingRules,
                    coOccurrenceMatrix,
                    depth + 1);

                children.AddRange(subClusters);
            }

            return new StyleCluster
            {
                Properties = properties,
                Values = values,
                Children = children,
                Cohesion = cohesion,
                Frequency = matchingRules.Count
            };
        }

        private List<StyleCluster> FindSubClusters(
            HashSet<string> properties,
            List<StyleRule> rules,
            Dictionary<string, Dictionary<string, int>> coOccurrenceMatrix,
            int depth)
        {
            var subClusters = new List<StyleCluster>();
            var processedProps = new HashSet<string>();

            // Try different property combinations
            foreach (var prop in properties)
            {
                if (processedProps.Contains(prop)) continue;

                var relatedProps = FindRelatedProperties(
                    prop,
                    properties.Except(processedProps),
                    coOccurrenceMatrix);

                if (relatedProps.Count > 1)
                {
                    var cluster = BuildCluster(
                        relatedProps,
                        rules,
                        coOccurrenceMatrix,
                        depth);

                    if (cluster != null)
                    {
                        subClusters.Add(cluster);
                        processedProps.UnionWith(relatedProps);
                    }
                }
            }

            return subClusters;
        }

        private HashSet<string> FindRelatedProperties(
            string prop,
            IEnumerable<string> candidates,
            Dictionary<string, Dictionary<string, int>> coOccurrenceMatrix)
        {
            var related = new HashSet<string> { prop };

            foreach (var candidate in candidates)
            {
                if (candidate == prop) continue;

                var coOccurrences = coOccurrenceMatrix[prop].GetValueOrDefault(candidate);
                var totalOccurrences = Math.Max(
                    coOccurrenceMatrix[prop].Values.Sum(),
                    coOccurrenceMatrix[candidate].Values.Sum());

                var correlation = coOccurrences / (double)totalOccurrences;
                if (correlation >= _minCohesion)
                {
                    related.Add(candidate);
                }
            }

            return related;
        }

        private double CalculateClusterCohesion(
            HashSet<string> properties,
            Dictionary<string, Dictionary<string, int>> coOccurrenceMatrix)
        {
            var totalCorrelations = 0.0;
            var correlationCount = 0;

            foreach (var prop1 in properties)
            {
                foreach (var prop2 in properties)
                {
                    if (prop1 == prop2) continue;

                    var coOccurrences = coOccurrenceMatrix[prop1].GetValueOrDefault(prop2);
                    var totalOccurrences = Math.Max(
                        coOccurrenceMatrix[prop1].Values.Sum(),
                        coOccurrenceMatrix[prop2].Values.Sum());

                    totalCorrelations += coOccurrences / (double)totalOccurrences;
                    correlationCount++;
                }
            }

            return correlationCount > 0 ? totalCorrelations / correlationCount : 0;
        }
    }
}
