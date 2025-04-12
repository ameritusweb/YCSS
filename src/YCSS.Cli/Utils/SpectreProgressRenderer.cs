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
    /// Implementation of IProgressRenderer using Spectre.Console for rich output
    /// </summary>
    public class SpectreProgressRenderer : IProgressRenderer
    {
        public async Task RunWithProgressAsync(string title, Func<IProgressContext, Task> action)
        {
            await AnsiConsole.Progress()
                .Columns(new ProgressColumn[]
                {
                    new TaskDescriptionColumn(),
                    new ProgressBarColumn(),
                    new PercentageColumn(),
                    new SpinnerColumn()
                })
                .StartAsync(async ctx =>
                {
                    var progressContext = new SpectreProgressContext(ctx, title);
                    await action(progressContext);
                });
        }

        private class SpectreProgressContext : IProgressContext
        {
            private readonly ProgressContext _context;
            private readonly string _mainTaskDescription;
            private readonly List<ProgressTask> _tasks = new();

            public SpectreProgressContext(ProgressContext context, string mainTaskDescription)
            {
                _context = context;
                _mainTaskDescription = mainTaskDescription;

                // Create main task
                var mainTask = _context.AddTask(_mainTaskDescription);
                _tasks.Add(mainTask);
            }

            public void AddTask(string description)
            {
                var task = _context.AddTask(description);
                _tasks.Add(task);

                // Update the main task progress based on subtasks
                UpdateMainTaskProgress();
            }

            private void UpdateMainTaskProgress()
            {
                if (_tasks.Count <= 1)
                {
                    return;
                }

                // Skip the main task (first item) when calculating progress
                var subTasks = _tasks.Skip(1).ToList();

                if (subTasks.Any())
                {
                    var totalProgress = subTasks.Sum(t => t.Value) / subTasks.Count;
                    _tasks[0].Value = totalProgress;
                }

                // Mark completed tasks
                foreach (var task in subTasks.Where(t => t.Value >= 100))
                {
                    task.StopTask();
                }
            }
        }
    }
}
