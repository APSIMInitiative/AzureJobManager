using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using ParallelAPSIM.Batch;
using ParallelAPSIM.Storage;

namespace ParallelAPSIM.Tests
{
    [TestFixture]
    public class TaskProviderTests : TestBase
    {
        [Test]
        public void TestInputZipFile()
        {
            var taskProvider = new TaskProvider(
                new StorageCredentials { Account = _storageAccount, Key = _storageKey },
                Path.Combine(Directory.GetCurrentDirectory(), "Input.zip"),
                1);

            Assert.AreEqual(2, taskProvider.GetTasks(Guid.NewGuid()).Count());
        }

        [Test]
        public void TestInputFolder()
        {
            var taskProvider = new TaskProvider(
                new StorageCredentials { Account = _storageAccount, Key = _storageKey },
                "D:\\APSIM_Test_Small\\Input",
                1);
            var tasks = taskProvider.GetTasks(Guid.NewGuid());
            Assert.AreEqual(4, tasks.Count());
        }
    }
}
