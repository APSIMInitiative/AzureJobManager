using System.Threading;
using NUnit.Framework;

namespace ParallelAPSIM.Tests.JobManagerTest
{
    [TestFixture]
    class JobManagerCliTests : TestBase
    {
        [Test]
        public void Test()
        {
            var apsim = new ParallelAPSIM(
                Storage.StorageCredentials.FromConfiguration(),
                Batch.BatchCredentials.FromConfiguration(),
                Batch.PoolSettings.FromConfiguration()); 
            
            var jobId = apsim.SubmitJob(GetJobParameters(), CancellationToken.None);

            var args = new[]
            {
                "job-manager",
                _batchUrl,
                _batchAccount,
                _batchKey,
                _storageAccount,
                _storageKey,
                jobId.ToString(),
                "D:\\APSIM_Test_Small\\Input"
            };

            Program.Main(args);
        }

        [Test]
        public void Test2()
        {
            // "Usage: parallelapsim.exe job-submit <BinariesFolder> <ModelFolder> <SimsFolder> <InputFolderOrZipFile>"
            var args = new[]
            {
                "job-submit",
                "my-job",
                _binFolder,
                _modelFolder,
                _simsFolder,
                _inputZip
            };

            Program.Main(args);
        }

        [Test]
        public void Test3()
        {
            // "Usage: parallelapsim.exe job-submit <BinariesFolder> <ModelFolder> <SimsFolder> <InputFolderOrZipFile>"
            var args = new[]
            {
                "job-monitor",
                "id",
                "D:\\temp\\outputs"
            };

            Program.Main(args);
        }
    }
}
