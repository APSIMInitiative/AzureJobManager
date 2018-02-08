using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace ParallelAPSIM.Tests
{
    [TestFixture]
    public class JobOutputMonitorTests : TestBase
    {
        [Test]
        public void Test()
        {
            var jobOutputMonitor = new JobOutputMonitor();
            jobOutputMonitor.Execute(Guid.Parse("9ffdb1e6-070b-46cd-a84f-2c65b1a89e3c"), "D:\\temp\\outputs", CancellationToken.None);
        }
    }
}
