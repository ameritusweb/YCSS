using Spectre.Console;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YCSS.Core.Interfaces;

namespace YCSS.Cli.Utils
{
    /// <summary>
    /// Implementation of IConsoleWriter using Spectre.Console for rich output
    /// </summary>
    public class SpectreConsoleWriter : IConsoleWriter
    {
        public void Clear()
        {
            AnsiConsole.Clear();
        }

        public void Write(string text)
        {
            AnsiConsole.Write(text);
        }

        public void WriteLine(string text)
        {
            AnsiConsole.WriteLine(text);
        }

        public void WriteLine()
        {
            AnsiConsole.WriteLine();
        }

        public void WriteInfo(string text)
        {
            AnsiConsole.MarkupLine($"[blue]ℹ [/][white]{EscapeMarkup(text)}[/]");
        }

        public void WriteSuccess(string text)
        {
            AnsiConsole.MarkupLine($"[green]✓ [/][white]{EscapeMarkup(text)}[/]");
        }

        public void WriteWarning(string text)
        {
            AnsiConsole.MarkupLine($"[yellow]⚠ [/][white]{EscapeMarkup(text)}[/]");
        }

        public void WriteError(string text)
        {
            AnsiConsole.MarkupLine($"[red]✗ [/][white]{EscapeMarkup(text)}[/]");
        }

        public void WriteError(Exception ex)
        {
            var panel = new Panel(new Markup($"[red]{EscapeMarkup(ex.Message)}[/]"))
                .Header("[red]Error[/]")
                .Border(BoxBorder.Rounded)
                .Expand();

            AnsiConsole.Write(panel);
        }

        public void WriteException(Exception ex)
        {
            var exceptionTree = new Tree("Exception");
            BuildExceptionTree(exceptionTree.AddNode($"[red]{EscapeMarkup(ex.Message)}[/]"), ex);

            AnsiConsole.Write(exceptionTree);
        }

        private void BuildExceptionTree(TreeNode node, Exception ex)
        {
            if (!string.IsNullOrEmpty(ex.StackTrace))
            {
                var stackTraceNode = node.AddNode("[gray]Stack Trace[/]");
                foreach (var frame in ex.StackTrace.Split('\n'))
                {
                    if (!string.IsNullOrWhiteSpace(frame))
                    {
                        stackTraceNode.AddNode($"[gray]{EscapeMarkup(frame.Trim())}[/]");
                    }
                }
            }

            if (ex.InnerException != null)
            {
                var innerNode = node.AddNode($"[red]Inner Exception: {EscapeMarkup(ex.InnerException.Message)}[/]");
                BuildExceptionTree(innerNode, ex.InnerException);
            }
        }

        private static string EscapeMarkup(string text)
        {
            return text?.Replace("[", "[[").Replace("]", "]]") ?? string.Empty;
        }
    }
}
