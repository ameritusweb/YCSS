using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YCSS.Core.Analysis.Patterns
{
    public interface IPatternDetector
    {
        Task<PatternAnalysis> DetectPatternsAsync(
            Dictionary<string, object> styles,
            CancellationToken ct = default);
    }

    public class PatternAnalysis
    {
        public PatternAnalysis(Dictionary<string, PropertyPattern> propertyPatterns, Dictionary<string, ValuePattern> valuePatterns, Dictionary<string, double> propertyCorrelations)
        {
            this.PropertyPatterns = propertyPatterns;
            this.ValuePatterns = valuePatterns;
            this.PropertyCorrelations = propertyCorrelations;
        }

        public Dictionary<string, PropertyPattern> PropertyPatterns { get; init; } = new();
        public Dictionary<string, ValuePattern> ValuePatterns { get; init; } = new();
        public Dictionary<string, double> PropertyCorrelations { get; init; } = new();
    }

    public record PropertyPattern(
        Dictionary<string, HashSet<string>> Properties,
        int Frequency,
        double Cohesion
    );

    public record ValuePattern(
        string Value,
        int Frequency,
        HashSet<string> Properties
    );
}
