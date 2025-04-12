using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YCSS.Core.Analysis.Clustering;

namespace YCSS.Core.Analysis.Formatters
{
    public abstract class BaseAnalysisFormatter : IAnalysisFormatter
    {
        public abstract string Format(List<StyleCluster> clusters);

        protected string FormatClusterProperties(StyleCluster cluster, string indent = "")
        {
            return string.Join("\n", cluster.Properties.Select(p => $"{indent}- {p}"));
        }

        protected string FormatClusterValues(StyleCluster cluster, string indent = "", int maxValues = 5)
        {
            return string.Join("\n", cluster.Values.Take(maxValues).Select(v => $"{indent}- {v}"));
        }
    }
}
