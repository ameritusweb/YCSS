using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YCSS.Core.Interfaces
{
    public interface IProgressRenderer
    {
        Task RunWithProgressAsync(string title, Func<IProgressContext, Task> action);
    }
}
