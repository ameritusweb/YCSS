using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YCSS.Cli.Utils;
using YCSS.Core.Interfaces;

namespace YCSS.Core.Test.Writers
{
    /// <summary>
    /// Test implementation of IConsoleWriter for test purposes.
    /// </summary>
    public class TestConsoleWriter : IConsoleWriter
    {
        public List<string> Messages { get; } = new();

        public void Clear() => Messages.Clear();
        public void Write(string text) => Messages.Add(text);
        public void WriteLine(string text) => Messages.Add(text);
        public void WriteLine() => Messages.Add(string.Empty);
        public void WriteInfo(string text) => Messages.Add($"[INFO] {text}");
        public void WriteSuccess(string text) => Messages.Add($"[SUCCESS] {text}");
        public void WriteWarning(string text) => Messages.Add($"[WARNING] {text}");
        public void WriteError(string text) => Messages.Add($"[ERROR] {text}");
        public void WriteError(Exception ex) => Messages.Add($"[ERROR] {ex.Message}");
        public void WriteException(Exception ex) => Messages.Add($"[EXCEPTION] {ex}");
    }
}
