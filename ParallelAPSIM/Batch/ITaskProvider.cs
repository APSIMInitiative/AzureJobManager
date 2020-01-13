using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Batch;

namespace ParallelAPSIM.Batch
{
    public interface ITaskProvider
    {
        IEnumerable<CloudTask> GetTasks(Guid jobId);

        string Output { get; }
    }
}
