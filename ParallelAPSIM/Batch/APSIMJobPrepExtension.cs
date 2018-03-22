using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Batch;
using Microsoft.WindowsAzure.Storage.Blob;
using ParallelAPSIM.APSIM;

namespace ParallelAPSIM.Batch
{
    public static class APSIMJobPrepExtension
    {
        public static JobPreparationTask ToJobPreparationTask(this APSIMJob job, Guid jobId, CloudBlobClient blobClient)
        {
            return new JobPreparationTask
            {
                CommandLine = "cmd.exe /c jobprep.cmd",
                ResourceFiles = GetResourceFiles(job, blobClient).ToList(),
                WaitForSuccess = true,
            };
        }

        public static JobReleaseTask ToJobReleaseTask(this APSIMJob job, Guid jobId)
        {
            return new JobReleaseTask
            {
                //CommandLine = "cmd.exe /c rmdir /s /q " + BatchConstants.GetJobInputPath(jobId),
                //CommandLine = "cmd.exe /c echo test > " + BatchConstants.GetJobInputPath(jobId) + "\\test.txt",
                //CommandLine = "cmd.exe /c jobrelease.cmd " + BatchConstants.GetJobInputPath(jobId),
                //CommandLine = "cmd.exe /c jobrelease.cmd",
                CommandLine = "cmd.exe /c jobrelease.cmd " + job.StorageCredentials.Key,
                //CommandLine = "cmd.exe /c md c:\temp && echo test > c:\temp\test.stdout",

            };
        }

        /// <summary>
        /// Returns the APSIM ZIP file and helpers like AzCopy and 7zip.
        /// </summary>
        private static IEnumerable<ResourceFile> GetResourceFiles(APSIMJob job, CloudBlobClient blobClient)
        {
            yield return new ResourceFile(job.ModelZipFileSas, BatchConstants.MODEL_ZIPFILE_NAME);

            var toolsRef = blobClient.GetContainerReference("tools");
            foreach (CloudBlockBlob listBlobItem in toolsRef.ListBlobs())
            {
                var sas = listBlobItem.GetSharedAccessSignature(new SharedAccessBlobPolicy
                {
                    SharedAccessStartTime = DateTime.UtcNow.AddHours(-1),
                    SharedAccessExpiryTime = DateTime.UtcNow.AddMonths(2),
                    Permissions = SharedAccessBlobPermissions.Read,
                });
                yield return new ResourceFile(listBlobItem.Uri.AbsoluteUri + sas, listBlobItem.Name);
            }

            var apsimRef = blobClient.GetContainerReference("apsim");
            foreach (CloudBlockBlob listBlobItem in apsimRef.ListBlobs())
            {
                if (listBlobItem.Name.ToLower().Contains(job.ApsimApplicationPackageVersion.ToLower()))
                {
                    var sas = listBlobItem.GetSharedAccessSignature(new SharedAccessBlobPolicy
                    {
                        SharedAccessStartTime = DateTime.UtcNow.AddHours(-1),
                        SharedAccessExpiryTime = DateTime.UtcNow.AddMonths(2),
                        Permissions = SharedAccessBlobPermissions.Read,
                    });
                    yield return new ResourceFile(listBlobItem.Uri.AbsoluteUri + sas, listBlobItem.Name);
                }
            }
        }
    }
}
