using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YCSS.Core.Analysis.Clustering
{
    public interface IClusterAnalyzer
    {
        void AnalyzeClusters(List<StyleCluster> clusters, int indent = 0);
    }
}
