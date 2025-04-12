using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YCSS.Core.Compilation.Formatters
{
    public interface IStyleFormatter
    {
        string Format(StyleDefinition definition, CompilerOptions options);
    }

    public record FormatterContext(
        Dictionary<string, string> Variables,
        bool Minify,
        string? Theme,
        bool IncludeSourceMap
    );
}
