using Spectre.Console;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YCSS.Cli.Utils;
using YCSS.Core.Interfaces;

namespace YCSS.Core.Test.Renderers
{
    /// <summary>
    /// Test implementation of IProgressRenderer for test purposes.
    /// </summary>
    public class TestProgressRenderer : IProgressRenderer
    {
        public async Task RunWithProgressAsync(string title, Func<IProgressContext, Task> action)
        {
            await action(new TestProgressContext());
        }

        private class TestProgressContext : IProgressContext
        {
            public List<string> Tasks { get; } = new();

            public void AddTask(string description)
            {
                Tasks.Add(description);
            }
        }
    }
}
