using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YCSS.Core.Interfaces
{
    public interface IConsoleWriter
    {
        void Clear();
        void Write(string text);
        void WriteLine(string text);
        void WriteLine();
        void WriteInfo(string text);
        void WriteSuccess(string text);
        void WriteWarning(string text);
        void WriteError(string text);
        void WriteError(Exception ex);
        void WriteException(Exception ex);
    }
}
