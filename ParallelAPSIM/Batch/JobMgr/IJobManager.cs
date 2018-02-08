using System;
using System.Threading;

namespace ParallelAPSIM.Batch.JobMgr
{
    public interface IJobManager
    {
        void Execute(Guid jobId, bool submitTasks, bool autoScalePool, CancellationToken ct);
    }
}
