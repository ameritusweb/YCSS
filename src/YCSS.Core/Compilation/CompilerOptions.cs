using System;

namespace YCSS.Core.Compilation
{
    public enum OutputFormat
    {
        CSS,
        SCSS,
        Tailwind,
        Tokens
    }

    public record CompilerOptions
    {
        public OutputFormat Format { get; init; } = OutputFormat.CSS;
        public bool TokensOnly { get; init; }
        public bool Optimize { get; init; }
        public string? Theme { get; init; }
        public bool UseUtilities { get; init; }
    }
}
