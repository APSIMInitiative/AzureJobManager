using System.IO;
using System.Reflection;
using NUnit.Framework;
using ParallelAPSIM.Zip;

namespace ParallelAPSIM.Tests.ZipHelperTests
{
    [TestFixture]
    public class ZipHelperTests
    {
        [Test]
        public void TestIdenticalFolderAndZipReturnsTrue()
        {
            var zipFile = Path.Combine(GetBaseFolder(), "Same.zip");
            var folder = Path.Combine(GetBaseFolder(), "Same");

            Assert.IsTrue(ZipHelper.CompareZipFileWithFolder(zipFile, folder));
        }

        [Test]
        public void TestDifferentFolderAndZipReturnsFalse()
        {
            var zipFile = Path.Combine(GetBaseFolder(), "Different.zip");
            var folder = Path.Combine(GetBaseFolder(), "Different");

            Assert.IsFalse(ZipHelper.CompareZipFileWithFolder(zipFile, folder));
        }

        private string GetBaseFolder()
        {
            return Path.Combine(
                Directory.GetParent(new System.Uri(Assembly.GetExecutingAssembly().CodeBase).AbsolutePath).ToString(), 
                "ZipHelperTests");
        }
    }
}
