using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Batch;
using Microsoft.Azure.Batch.Auth;
using Microsoft.Azure.Batch.Common;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.RetryPolicies;
using ParallelAPSIM.Batch;
using ParallelAPSIM.Storage;
using LinearRetry = Microsoft.WindowsAzure.Storage.RetryPolicies.LinearRetry;
using StorageCredentials = Microsoft.WindowsAzure.Storage.Auth.StorageCredentials;

namespace ParallelAPSIM
{
    public class JobOutputMonitor
    {
        private readonly CloudStorageAccount _storageAccount;
        private readonly CloudBlobClient _blobClient;
        private readonly BatchClient _batchClient;

        public JobOutputMonitor() : this(
            new Storage.StorageCredentials
            {
                Account = "", 
                Key = ""
            },
            new Batch.BatchCredentials
            {
                Url = "",
                Account = "",
                Key = ""
            })
        { }

        public JobOutputMonitor(
            Storage.StorageCredentials storageCredentials,
            Batch.BatchCredentials batchCredentials)
        {
            _storageAccount = new CloudStorageAccount(
                new StorageCredentials(
                    storageCredentials.Account,
                    storageCredentials.Key), 
                    true);

            _blobClient = _storageAccount.CreateCloudBlobClient();
            _blobClient.DefaultRequestOptions.RetryPolicy = new LinearRetry(TimeSpan.FromSeconds(3), 10);

            _batchClient = BatchClient.Open(
                new BatchSharedKeyCredentials(
                    batchCredentials.Url,
                    batchCredentials.Account,
                    batchCredentials.Key));

            _batchClient.CustomBehaviors.Add(
                RetryPolicyProvider.LinearRetryProvider(TimeSpan.FromSeconds(3), 10));
        }

        public void Execute(Guid jobId, string baseOutputPath, CancellationToken ct)
        {
            Console.WriteLine("Waiting for job outputs...");

            var jobOutputDir = GetJobOutputDirectory(jobId, baseOutputPath);

            var outputHashLock = new object();
            var downloadedOutputs = GetDownloadedOutputFiles(jobOutputDir);

            while (true)
            {
                try
                {
                    if (ct.IsCancellationRequested)
                    {
                        return;
                    }

                    var complete = IsJobComplete(jobId);

                    Parallel.ForEach(
                        ListJobOutputsFromStorage(jobId, ct),
                        new ParallelOptions { CancellationToken = ct, MaxDegreeOfParallelism = 8 },
                        blob =>
                        {
                            if (!downloadedOutputs.Contains(blob.Name))
                            {
                                blob.DownloadToFile(Path.Combine(jobOutputDir, blob.Name), FileMode.Create);

                                lock (outputHashLock)
                                {
                                    downloadedOutputs.Add(blob.Name);
                                }

                                Console.WriteLine("Downloaded job output {0}", blob.Name);
                            }
                        });

                    if (complete)
                    {
                        Console.WriteLine("Job {0} completed", jobId);
                        break;
                    }
                }
                catch (AggregateException e)
                {
                    Console.WriteLine(e.InnerException.Message);
                    Console.WriteLine(e.InnerException.StackTrace);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    Console.WriteLine(e.StackTrace);
                }

                ct.WaitHandle.WaitOne(TimeSpan.FromSeconds(15));
            }
        }

        private bool IsJobComplete(Guid jobId)
        {
            var job = GetJob(jobId);

            if (job == null)
            {
                return true;
            }

            return job.State == JobState.Completed || job.State == JobState.Disabled;
        }

        private CloudJob GetJob(Guid jobId)
        {
            var detailLevel = new ODATADetailLevel { SelectClause = "id" };
            var job =
                _batchClient.JobOperations.ListJobs(detailLevel)
                    .FirstOrDefault(j => string.Equals(jobId.ToString(), j.Id));

            if (job == null)
            {
                return null;
            }

            return _batchClient.JobOperations.GetJob(jobId.ToString());
        }

        private string GetJobOutputDirectory(Guid jobId, string outputPath)
        {
            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
            }

            var jobDir = Path.Combine(outputPath, StorageConstants.GetJobOutputContainer(jobId));

            if (!Directory.Exists(jobDir))
            {
                Directory.CreateDirectory(jobDir);
            }

            return jobDir;
        }

        private HashSet<string> GetDownloadedOutputFiles(string jobOutputPath)
        {
            return new HashSet<string>(Directory.EnumerateFiles(jobOutputPath).Select(f => Path.GetFileName(f)));
        }

        private IEnumerable<CloudBlockBlob> ListJobOutputsFromStorage(Guid jobId, CancellationToken ct)
        {
            var containerRef = _blobClient.GetContainerReference(StorageConstants.GetJobOutputContainer(jobId));

            if (!containerRef.Exists())
            {
                return Enumerable.Empty<CloudBlockBlob>();
            }

            return containerRef.ListBlobs().Select(b => ((CloudBlockBlob) b));
        } 
    }
}
