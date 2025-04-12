using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YCSS.Core.Analysis.Clustering;

namespace YCSS.Core.Analysis.Formatters
{
    public class MarkdownFormatter : BaseAnalysisFormatter
    {
        public override string Format(List<StyleCluster> clusters)
        {
            var writer = new StringWriter();
            writer.WriteLine("# Style Pattern Analysis\n");
            FormatClusters(clusters, writer);
            return writer.ToString();
        }

        private void FormatClusters(List<StyleCluster> clusters, TextWriter writer, int level = 0)
        {
            foreach (var cluster in clusters)
            {
                var prefix = new string('#', level + 2);
                writer.WriteLine($"{prefix} Pattern (Cohesion: {cluster.Cohesion:F2})\n");

                writer.WriteLine("**Properties:**\n");
                writer.WriteLine(FormatClusterProperties(cluster));
                writer.WriteLine();

                writer.WriteLine("**Common Values:**\n");
                writer.WriteLine(FormatClusterValues(cluster));
                writer.WriteLine();

                if (cluster.Children.Any())
                {
                    writer.WriteLine("**Sub-patterns:**\n");
                    FormatClusters(cluster.Children, writer, level + 1);
                }

                writer.WriteLine("---\n");
            }
        }
    }
}
