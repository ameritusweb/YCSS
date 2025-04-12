using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YCSS.Core.Exceptions
{
    public class YCSSException : Exception
    {
        public string Code { get; }

        public YCSSException(string message, string code = "YCSS001")
            : base(message)
        {
            Code = code;
        }

        public YCSSException(string message, Exception inner, string code = "YCSS001")
            : base(message, inner)
        {
            Code = code;
        }
    }

    public class YCSSValidationException : YCSSException
    {
        public IReadOnlyList<ValidationError> Errors { get; }

        public YCSSValidationException(IEnumerable<ValidationError> errors)
            : base("YAML validation failed", "YCSS100")
        {
            Errors = errors.ToList().AsReadOnly();
        }
    }

    public class YCSSCompilationException : YCSSException
    {
        public string? SourceFile { get; }
        public int? Line { get; }
        public int? Column { get; }

        public YCSSCompilationException(
            string message,
            string? sourceFile = null,
            int? line = null,
            int? column = null)
            : base(message, "YCSS200")
        {
            SourceFile = sourceFile;
            Line = line;
            Column = column;
        }
    }

    public record ValidationError(
        string Property,
        string Message,
        ValidationSeverity Severity = ValidationSeverity.Error
    );

    public enum ValidationSeverity
    {
        Warning,
        Error
    }
}
