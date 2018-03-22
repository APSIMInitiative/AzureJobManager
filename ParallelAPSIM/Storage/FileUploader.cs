using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.RetryPolicies;

namespace ParallelAPSIM
{
    public class FileUploader : IFileUploader
    {
        private CloudStorageAccount _storageAccount;

        public FileUploader(CloudStorageAccount storageAccount)
        {
            _storageAccount = storageAccount;
        }

        public string UploadFile(string filePath, string container, string remoteFileName, CancellationToken ct)
        {

            var blobClient = _storageAccount.CreateCloudBlobClient();
            blobClient.DefaultRequestOptions.RetryPolicy = new LinearRetry(TimeSpan.FromSeconds(3), 10);

            var containerRef = blobClient.GetContainerReference(container);

            containerRef.CreateIfNotExists();

            var blob = containerRef.GetBlockBlobReference(remoteFileName);

            if (BlobNeedsUploading(blob, filePath))
            {
                Console.WriteLine("Uploading file: " + filePath);

                blob.UploadFromFileAsync(filePath, FileMode.Open,
                    new AccessCondition(), new BlobRequestOptions{ParallelOperationThreadCount = 8, StoreBlobContentMD5 = true}, null, ct).Wait();
            }

            var policy = new SharedAccessBlobPolicy
            {
                Permissions = SharedAccessBlobPermissions.Read,
                SharedAccessStartTime = DateTime.UtcNow.AddMinutes(-15),
                SharedAccessExpiryTime = DateTime.UtcNow.AddMonths(12),
            };

            return blob.Uri.AbsoluteUri + blob.GetSharedAccessSignature(policy);
        }

        private static bool BlobNeedsUploading(CloudBlockBlob blob, string filePath)
        {
            if (blob.Exists())
            {
                blob.FetchAttributes();

                if (blob.Properties.ContentMD5 != null)
                {
                    var localMd5 = GetMD5(filePath);

                    if (blob.Properties.ContentMD5 == localMd5)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static string GetMD5(string filePath)
        {
            using (var md5 = MD5.Create())
            {
                using (var stream = File.OpenRead(filePath))
                {
                    return Convert.ToBase64String(md5.ComputeHash(stream));
                }
            }
        }
    }
}
