using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static YCSS.Core.Compilation.StyleCompiler;

namespace YCSS.Core.Compilation
{
    public record CompilerOptions
    {
        public OutputFormat Format { get; init; } = OutputFormat.CSS;
        public bool TokensOnly { get; init; }
        public bool Optimize { get; init; }
        public string? Theme { get; init; }
        public bool UseUtilities { get; init; }
    }
}
