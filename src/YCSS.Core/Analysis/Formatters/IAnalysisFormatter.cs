using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YCSS.Core.Analysis.Clustering;

namespace YCSS.Core.Analysis.Formatters
{
    public interface IAnalysisFormatter
    {
        string Format(List<StyleCluster> clusters);
    }
}
