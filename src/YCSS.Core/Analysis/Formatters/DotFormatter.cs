using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YCSS.Core.Analysis.Clustering;

namespace YCSS.Core.Analysis.Formatters
{
    public class DotFormatter : BaseAnalysisFormatter
    {
        public override string Format(List<StyleCluster> clusters)
        {
            var writer = new StringWriter();
            writer.WriteLine("digraph StylePatterns {");
            writer.WriteLine("  node [shape=box];");

            foreach (var cluster in clusters)
            {
                FormatCluster(cluster, writer);
            }

            writer.WriteLine("}");
            return writer.ToString();
        }

        private void FormatCluster(StyleCluster cluster, TextWriter writer)
        {
            var id = cluster.Id;
            var label = string.Join("\\n",
                $"Pattern ({cluster.Cohesion:F2})",
                ...cluster.Properties.Take(3).Select(p => p));

            writer.WriteLine($"  {id} [label=\"{label}\"];");

            foreach (var child in cluster.Children)
            {
                writer.WriteLine($"  {id} -> {child.Id};");
                FormatCluster(child, writer);
            }
        }
    }
}
