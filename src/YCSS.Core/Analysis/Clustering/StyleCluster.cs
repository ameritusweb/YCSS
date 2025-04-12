using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YCSS.Core.Analysis.Clustering
{
    public class StyleCluster
    {
        public string Id { get; init; } = Guid.NewGuid().ToString();
        public HashSet<string> Properties { get; init; } = new();
        public HashSet<string> Values { get; init; } = new();
        public List<StyleCluster> Children { get; init; } = new();
        public double Cohesion { get; init; }
        public int Frequency { get; init; }

        public override string ToString() =>
            $"Cluster({Properties.Count} props, {Children.Count} children, cohesion: {Cohesion:F2})";
    }
}
