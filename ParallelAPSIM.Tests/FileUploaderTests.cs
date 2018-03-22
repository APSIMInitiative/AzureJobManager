using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using NUnit.Framework;

namespace ParallelAPSIM.Tests
{
    [TestFixture]
    public class FileUploaderTests
    {

        private string _account = "cbsapsimpoc";
        private string _key = "";

        [Test]
        public void TestNoRemoteFileUploadsFile()
        {
            var fileToUpload = "D:\\Temp\\file.txt";

            var creds = new StorageCredentials(_account, _key);
            var storageAccount = new CloudStorageAccount(creds, true);
            var fileUploader = new FileUploader(storageAccount);

            var sas = fileUploader.UploadFile(fileToUpload, "apsimbin", Path.GetFileName(fileToUpload), CancellationToken.None);

            Assert.NotNull(sas);
        }
    }
}
