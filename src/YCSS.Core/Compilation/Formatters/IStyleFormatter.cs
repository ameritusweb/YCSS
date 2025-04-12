using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YCSS.Core.Models;

namespace YCSS.Core.Compilation.Formatters
{
    public interface IStyleFormatter
    {
        string Format(StyleDefinition definition, CompilerOptions options);
        string FormatTokens(StyleDefinition definition, CompilerOptions options);
    }
}
