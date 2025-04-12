using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YCSS.Core.Analysis.Clustering;

namespace YCSS.Core.Compilation.Formatters
{
    public interface IOutputFormatter
    {
        string Format(List<StyleCluster> clusters);
    }
}
