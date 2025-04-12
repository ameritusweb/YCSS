using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YCSS.Core.Compilation.Formatters
{
    public record FormatterContext
    {
        public bool Minify { get; init; }
        public string? Theme { get; init; }
        public bool IncludeSourceMap { get; init; }
        public bool IncludeComments { get; init; } = true;
        public Dictionary<string, string> Variables { get; init; } = new();
    }
}
