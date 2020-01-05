using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Batch;
using Microsoft.Azure.Batch.Auth;
using Microsoft.Azure.Batch.Common;

using System.Net;
using System.Net.Mail;
using System.IO;
using Microsoft.WindowsAzure.Storage.Blob;
using ParallelAPSIM.Storage;
using Microsoft.WindowsAzure.Storage;
using System.IO.Compression;
//using StorageCredentials = Microsoft.WindowsAzure.Storage.Auth.StorageCredentials;

namespace ParallelAPSIM.Batch.JobMgr
{
    public class JobManager : IJobManager
    {
        private readonly BatchClient _batchClient;
        private CloudBlobClient _blobClient;
        private readonly ITaskProvider _taskProvider;
        private CloudStorageAccount _storageAccount;
        private CloudBlobContainer containerRef;

        string summary = "";

        public JobManager(
            BatchCredentials batchCredentials,
            StorageCredentials storageCredentials,
            ITaskProvider taskProvider)
        {
            _batchClient = BatchClient.Open(
                new BatchSharedKeyCredentials(
                    batchCredentials.Url,
                    batchCredentials.Account,
                    batchCredentials.Key));

            _batchClient.CustomBehaviors.Add(
                RetryPolicyProvider.LinearRetryProvider(TimeSpan.FromSeconds(3), 10));

            _taskProvider = taskProvider;

            _storageAccount = new CloudStorageAccount(
                new Microsoft.WindowsAzure.Storage.Auth.StorageCredentials(
                    storageCredentials.Account,
                    storageCredentials.Key),
                    true);
        }


        private string getFileList(string path)
        {
            string result = "\n------------------------------\n" +  path  + "\n";
            try
            {
                string[] dirs = Directory.GetFiles(path, "*");
                
                foreach (string filename in dirs)
                {
                    result+=filename+"\n";
                }
            }
            catch (Exception e)
            {
                result += "getFileList Error:" + e.Message;
            }


            return (result);
        }

        private IEnumerable<CloudBlockBlob> ListJobOutputsFromStorage(Guid jobId, CancellationToken ct)
        {
            var containerRef = _blobClient.GetContainerReference(StorageConstants.GetJobOutputContainer(jobId));

            if (!containerRef.Exists())
            {
                return Enumerable.Empty<CloudBlockBlob>();
            }

            return containerRef.ListBlobs().Select(b => ((CloudBlockBlob)b));
        }

        private long GetTotalFreeSpace(string driveName)  // only for dos style paths
        {
            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                if (drive.IsReady && drive.Name == driveName)
                {
                    return drive.TotalFreeSpace;
                }
            }
            return -1;
        }

        private int zipResults(Guid jobId, string tempDir, CancellationToken ct)
        {
            // create a temp directory
         
            try
            {
                if (!Directory.Exists(tempDir))
                {
                    Directory.CreateDirectory(tempDir);
                }
            }
            catch
            {
                summary += "Failed to create temporary directory (" + tempDir + ")\n";
                return (1);
            }

            summary += "Downloading results from storage...\n";

            try
            {
                //containerRef.CreateIfNotExists();

                Parallel.ForEach(
                    ListJobOutputsFromStorage(jobId, ct),
                    new ParallelOptions { CancellationToken = ct, MaxDegreeOfParallelism = 8 },
                    blob =>
                    {
                        bool zip = true;
                        if (Path.GetExtension(Path.Combine(tempDir, blob.Name).ToLower()) == ".stdout") zip = false;
                        if (Path.GetExtension(Path.Combine(tempDir, blob.Name).ToLower()) == ".sum") zip = false;

                        if (zip)
                        {
                            blob.DownloadToFile(Path.Combine(tempDir, blob.Name), FileMode.Create);
                            //summary += "Downloaded job output" + blob.Name + "\n";
                        }

                    });
            }
            catch (Exception e)
            {
                summary += "Download Error:" + e.Message + "\n";
                return (1);
            }

            summary += "Zipping results...\n";

            string zipPath = Path.Combine(Directory.GetParent(tempDir).ToString(), "Results.zip");

            try
            {

                if (File.Exists(zipPath)) File.Delete(zipPath);

                summary += "Zip Path:" + zipPath + "\n";

                ZipFile.CreateFromDirectory(tempDir, zipPath, CompressionLevel.Optimal, false);

            }
            catch (Exception e)
            {
                summary += "Error zipping results:" + e.Message + "\n";
            }
            summary += "Uploading zip to job outputs...\n";
            try
            {
                var blobRef = containerRef.GetBlockBlobReference(Path.GetFileName(zipPath));
                if (!blobRef.Exists())
                {
                    blobRef.Properties.ContentType = "application/zip";
                    blobRef.UploadFromFile(zipPath, FileMode.Open);
                }
                File.Delete(zipPath);
                Directory.Delete(tempDir, true);
            }
            catch (Exception e)
            {
                summary += "Error uploading Zip:" + e.Message + "\n";
                return (1);
            }

            return (0);
        }

        private void deleteResults(Guid jobId, CancellationToken ct)
        {
            try
            {
                //containerRef.CreateIfNotExists();

                Parallel.ForEach(
                    ListJobOutputsFromStorage(jobId, ct),
                    new ParallelOptions { CancellationToken = ct, MaxDegreeOfParallelism = 8 },
                    blob =>
                    {
                        if ((Path.GetExtension(blob.Name.ToLower()) != ".stdout") && (Path.GetExtension(blob.Name.ToLower()) != ".zip"))
                        {
                            blob.Delete();
                            //summary += "Downloaded job output" + blob.Name + "\n";
                        }

                    });
            }
            catch (Exception e)
            {
                summary += "Failed to delete blobs:" + e.Message + "\n";
                return;
            }
        }


        public string getFreeSpace()
        {
            string result = "";
            DriveInfo[] allDrives = DriveInfo.GetDrives();

            foreach (DriveInfo d in allDrives)
            {
                result+=string.Format("Drive {0}\n", d.Name);
                result += string.Format("  Drive type: {0}\n", d.DriveType);
                if (d.IsReady == true)
                {
                    result += string.Format("  Volume label: {0}\n", d.VolumeLabel);
                    result += string.Format("  File system: {0}\n", d.DriveFormat);
                    result += string.Format(
                        "  Available space to current user:{0, 15} MB\n",
                        d.AvailableFreeSpace/1024/1024);

                    result += string.Format(
                        "  Total available space:          {0, 15} MB\n",
                        d.TotalFreeSpace/1024/1024);

                    result += string.Format(
                        "  Total size of drive:            {0, 15} MB\n",
                        d.TotalSize/1024/1024);
                }
            }
            return (result);
        }
        

        public void emailNotify(string senderAddr, string recipientAddr, string pw, string body)
        {

            summary += "emailing from '" + senderAddr + "'\n";
            summary += "emailing to '" + recipientAddr + "'\n";

            string senderName = senderAddr.Substring(0, senderAddr.IndexOf('@'));
            senderName = senderName.Replace(".", " ");

            var fromAddress = new MailAddress(senderAddr, senderName);

            string recipientName = recipientAddr.Substring(0, recipientAddr.IndexOf('@'));
            recipientName = recipientName.Replace(".", " ");            

            var toAddress = new MailAddress(recipientAddr, recipientName);            

            const string subject = "Simulation Complete";

            var smtp = new SmtpClient
            {
                Host = "smtp.gmail.com",
                Port = 587,
                EnableSsl = true,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(fromAddress.Address, pw)
            };
            using (var message = new MailMessage(fromAddress, toAddress)
            {
                Subject = subject,
                Body = body
            })
            {
                smtp.Send(message);
            }

        }

        private void storeSummary(Guid jobId)
        {
            try
            {
                string summaryPath = Path.Combine(Path.Combine(Environment.GetEnvironmentVariable("TEMP")), "JobManagerResults.stdout");

                if (File.Exists(summaryPath)) File.Delete(summaryPath);

                System.IO.File.WriteAllText(summaryPath, summary);
                var blobRef = containerRef.GetBlockBlobReference(Path.GetFileName(summaryPath));
                if (!blobRef.Exists())
                {
                    blobRef.UploadFromFile(summaryPath, FileMode.Open);
                }
                File.Delete(summaryPath);
            }
            catch (Exception e)
            {
                summary += "Error storing job manager summary:" + e.Message + "\n";
            }

            try
            {
                string dir = Path.Combine("C:", "User", "tasks", "shared", jobId.ToString());
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                string file = Path.Combine(dir, "job-manager-fallback.stdout");
                File.WriteAllText(file, summary);
            }
            catch (Exception err)
            {
                throw new Exception("Error storing job manager fallback output file", err);
            }
        }

        public string getConfigSetting(string config, string name)
        {
            config=config.Replace("\r", ""); // remove \r
            string[] lines = config.Split('\n');
            foreach (string line in lines)
            {                
                string[] pairs = line.Split('=');
                if (pairs[0].ToLower() == name.ToLower()) return (pairs[1]);
            }
            return ("");
        }

        public void Execute(Guid jobId, bool submitTasks, bool autoScalePool, CancellationToken ct)
        {
            try
            {
                summary += "Submitting tasks\n";
                if (submitTasks)
                {
                    SubmitTasks(jobId, ct);
                }

                WaitForTasksToComplete(jobId, autoScalePool, ct);


                string recipient = "";
                string from = "";
                string pw = "";

                summary += "Tasks complete\n";

                _blobClient = _storageAccount.CreateCloudBlobClient();
                containerRef = _blobClient.GetContainerReference("job-" + jobId + "-outputs");
                containerRef.CreateIfNotExists();
                //string tempDir = Path.Combine(Environment.GetEnvironmentVariable("TEMP"), jobId.ToString());
                string tempDir = Path.Combine(Environment.GetEnvironmentVariable("WATASK_TVM_SHARED_DIR"), jobId.ToString(), "-tmpZip");

                summary += "Config settings...\n";
                // Download the settings file from blob storage
                var settingsContainerRef = _blobClient.GetContainerReference("job-" + jobId);
                summary += "Get blob ref...\n";
                var blob = settingsContainerRef.GetBlobReference("settings.txt");

                string tmpConfig = Path.Combine(Path.GetTempPath(), "settings.txt");
                summary += "Downloading settings...\n";
                blob.DownloadToFile(tmpConfig, FileMode.Create);

                summary += "Reading settings...\n";
                string config = File.ReadAllText(tmpConfig);

                recipient = getConfigSetting(config, "EmailRecipient");
                from = getConfigSetting(config, "EmailSender");
                pw = getConfigSetting(config, "EmailPW");

                summary += "Recipient='" + recipient + "'\n";
                summary += "Sender='" + from + "'\n";
                //summary += "pw=" + pw+ "\n";

                summary += "Deleting tmp settings file...\n";
                File.Delete(tmpConfig);

                int result = zipResults(jobId, tempDir, ct);
                if (result == 0)
                {
                    summary += "Deleting individual results...\n";
                    deleteResults(jobId, ct);
                }

                summary += "------------------------\nEnvironment:\n";

                foreach (System.Collections.DictionaryEntry env in Environment.GetEnvironmentVariables())
                {
                    string name = (string)env.Key;
                    string value = (string)env.Value;
                    summary += name + "=" + value + "\n";
                }

                summary += getFreeSpace();

                // fixme - this doesn't work
                //emailNotify(from, recipient, pw, summary);
            }
            catch (Exception err)
            {
                summary += err.ToString();
                throw;
            }
            finally
            {
                summary += "Storing summary...\n";
                storeSummary(jobId);
            }
        }

        private bool AnyRunningTasks(Guid jobId)
        {
            var detailLevel = new ODATADetailLevel { SelectClause = "id,state" };
            var tasks = _batchClient.JobOperations.ListTasks(jobId.ToString(), detailLevel)
                .Where(t => !string.Equals(t.Id, BatchConstants.JOB_MANAGER_NAME));
            return tasks.Any(t => t.State.HasValue && t.State.Value != TaskState.Completed);
        }

        private void WaitForTasksToComplete(Guid jobId, bool autoScalePool, CancellationToken ct)
        {
            var errors = 0;

            string poolId = null;

            while (true)
            {
                if (ct.IsCancellationRequested)
                {
                    return;
                }

                if (errors > 20)
                {
                    Console.WriteLine("Too many errors, exiting");
                    throw new Exception("Too many errors waiting for tasks to complete");
                }

                try
                {
                    if (!AnyRunningTasks(jobId))
                    {
                        return;
                    }

                    if (autoScalePool)
                    {
                        if (poolId == null)
                        {
                            poolId = _batchClient.JobOperations.GetJob(jobId.ToString()).ExecutionInformation.PoolId;
                        }

                        ScalePoolIfNeeded(jobId, poolId);
                    }
                }
                catch (Exception e)
                {
                    errors++;
                    Console.WriteLine("Error waiting for tasks to complete: {0}", e);
                }

                ct.WaitHandle.WaitOne(TimeSpan.FromSeconds(30));
            }
        }

        private void SubmitTasks(Guid jobId, CancellationToken ct)
        {

            summary += $"Starting to submit tasks - {DateTime.UtcNow}\n";

            var attempts = 0;

            //while (attempts++ < 20)
            //{
                if (ct.IsCancellationRequested)
                {
                    return;
                }

                //try
                //{
                    BatchClientParallelOptions parallelOptions = new BatchClientParallelOptions()
                    {
                        CancellationToken = ct,
                        MaxDegreeOfParallelism = 4,
                    };
                    summary += "Fetchign tasks to submit...";
                    _batchClient.JobOperations.AddTaskAsync(jobId.ToString(), GetTasksToSubmit(jobId), parallelOptions).Wait(ct);
                    summary += _taskProvider.Output;
                    summary += $"Starting to submit tasks - {DateTime.UtcNow}";

                //    break;
                //}
                //catch (AggregateException e)
                //{
                //    Console.WriteLine("An error occurred submitting tasks: {0}", e.InnerException);
                //}
                //catch (Exception e)
                //{
                //    Console.WriteLine("An error occurred submitting tasks: {0}", e);
                //}

                ct.WaitHandle.WaitOne(TimeSpan.FromSeconds(5));
            //}
        }

        private void ScalePoolIfNeeded(Guid jobId, string poolId)
        {
            var pool = _batchClient.PoolOperations.GetPool(poolId);

            if (pool == null)
            {
                Console.WriteLine("Pool is null, skipping auto scale.");
                return;
            }

            if (pool.State != PoolState.Active)
            {
                Console.WriteLine("Pool not active, skipping auto scale.");
                return;
            }

            if (pool.AllocationState != AllocationState.Steady)
            {
                Console.WriteLine("Pool not steady, skipping auto scale.");
                return;
            }

            var tasks = _batchClient.JobOperations.ListTasks(jobId.ToString(),
                new ODATADetailLevel(selectClause: "state"));

            if (tasks.Any(task => task.State == TaskState.Active))
            {
                Console.WriteLine("There are still active tasks, will not scale.");
                return;
            }

            var idleComputeNodes =
                _batchClient.PoolOperations.ListComputeNodes(poolId).Count(cn => cn.State == ComputeNodeState.Idle);

            Console.WriteLine("Pool has {0} idle nodes.", idleComputeNodes);

            pool.Resize(pool.CurrentDedicated.Value - idleComputeNodes);
        }

        private HashSet<string> GetExistingTaskIds(Guid jobId)
        {
            var detailLevel = new ODATADetailLevel {SelectClause = "id"};
            return new HashSet<string>(_batchClient.JobOperations.ListTasks(jobId.ToString(), detailLevel).Select(t => t.Id));
        }

        private IEnumerable<CloudTask> GetTasksToSubmit(Guid jobId)
        {
            try
            {
                var existingTaskIds = GetExistingTaskIds(jobId);
                return _taskProvider.GetTasks(jobId).ToList().Where(t => !existingTaskIds.Contains(t.Id));
            }
            finally
            {
                summary += _taskProvider.Output;
            }
        }
    }
}
