using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ParallelAPSIM.CommandLine
{
    public interface ICommandLineAction
    {
        string GetActionName();

        int Execute(string[] args, CancellationToken ct);

        string GetUsage();
    }
}
