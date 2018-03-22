using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using ParallelAPSIM.Batch;
using ParallelAPSIM.Batch.JobMgr;
using ParallelAPSIM.Storage;

namespace ParallelAPSIM.Tests
{
    [TestFixture]
    public class JobManagerTests : TestBase
    {
        [Test]
        public void TestCanSubmitTasks()
        {
            var apsim = new ParallelAPSIM(
                Storage.StorageCredentials.FromConfiguration(),
                Batch.BatchCredentials.FromConfiguration(),
                Batch.PoolSettings.FromConfiguration());

            var jobId = apsim.SubmitJob(GetJobParameters(), CancellationToken.None);

            var taskProvider = new TaskProvider(
                new StorageCredentials{ Account = _storageAccount, Key = _storageKey},
                _inputZip,
                1);

            var jobManager = new JobManager(
                new BatchCredentials{ Url = _batchUrl, Account = _batchAccount, Key = _batchKey},
                taskProvider);

            jobManager.Execute(jobId, true, false, CancellationToken.None);
        }
    }
}
