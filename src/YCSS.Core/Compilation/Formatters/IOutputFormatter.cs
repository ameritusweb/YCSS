using YCSS.Core.Models;

namespace YCSS.Core.Compilation.Formatters
{
    public interface IOutputFormatter
    {
        string Format(StyleDefinition definition, CompilerOptions options);
        string FormatTokens(StyleDefinition definition, CompilerOptions options);
    }
}
