using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using YCSS.Core.Analysis.Clustering;

namespace YCSS.Core.Analysis.Formatters
{
    public class JsonFormatter : BaseAnalysisFormatter
    {
        public override string Format(List<StyleCluster> clusters)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            return JsonSerializer.Serialize(clusters, options);
        }
    }
}
