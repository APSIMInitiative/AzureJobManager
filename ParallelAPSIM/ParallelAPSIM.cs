using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.Azure.Batch;
using Microsoft.Azure.Batch.Auth;
using Microsoft.Azure.Batch.Common;
using Microsoft.WindowsAzure.Storage;
using ParallelAPSIM.APSIM;
using ParallelAPSIM.Batch;
using ParallelAPSIM.Batch.JobMgr;
using ParallelAPSIM.Storage;
using ParallelAPSIM.Utils;
using ParallelAPSIM.Zip;
using LinearRetry = Microsoft.WindowsAzure.Storage.RetryPolicies.LinearRetry;
using StorageCredentials = Microsoft.WindowsAzure.Storage.Auth.StorageCredentials;

namespace ParallelAPSIM
{
    public class ParallelAPSIM
    {
        private readonly CloudStorageAccount _storageAccount;
        private readonly BatchClient _batchClient;
        private readonly Storage.StorageCredentials _storageCredentials;
        private readonly Batch.BatchCredentials _batchCredentials;
        private readonly IFileUploader _fileUploader;
        private readonly PoolSettings _poolSettings;

        public ParallelAPSIM(
            Storage.StorageCredentials storageCredentials,
            Batch.BatchCredentials batchCredentials,
            PoolSettings poolSettings)
        {
            _storageAccount = new CloudStorageAccount(
                new StorageCredentials(
                    storageCredentials.Account,
                    storageCredentials.Key),
                    true);

            _batchClient = BatchClient.Open(
                new BatchSharedKeyCredentials(
                    batchCredentials.Url,
                    batchCredentials.Account,
                    batchCredentials.Key));

            _batchClient.CustomBehaviors.Add(
                RetryPolicyProvider.LinearRetryProvider(TimeSpan.FromSeconds(3), 10));

            _fileUploader = new FileUploader(_storageAccount);
            _storageCredentials = storageCredentials;
            _batchCredentials = batchCredentials;
            _poolSettings = poolSettings;
        }

        public Guid SubmitJob(
            JobParameters jobParameters,
            CancellationToken ct)
        {
            ValidateInputs(jobParameters);

            var jobId = Guid.NewGuid();

            var job = BuildAPSIMJobAndStageFiles(
                jobId,
                jobParameters.ApplicationPackageVersion,
                jobParameters.JobDisplayName,
                jobParameters.ModelPath,
                ct);

            SubmitJob(jobId, job, jobParameters.JobManagerShouldSubmitTasks, jobParameters.AutoScale);

            if (!jobParameters.JobManagerShouldSubmitTasks)
            {
                // Client side submit
                var taskProvider = new TaskProvider(
                    _storageCredentials,
                    jobParameters.ModelPath,
                    jobParameters.CoresPerProcess);

                var tasks = taskProvider.GetTasks(jobId).ToList();
                _batchClient.JobOperations.AddTask(jobId.ToString(), tasks);
            }

            Console.WriteLine("Submitted Job {0}", jobId);

            return jobId;
        }

        public void SubmitJob(Guid jobId, APSIMJob job, bool shouldSubmitTasks, bool autoScale)
        {
            try
            {
                var cloudJob = _batchClient.JobOperations.CreateJob(jobId.ToString(), GetPoolInfo(job.PoolSettings));
                cloudJob.DisplayName = job.DisplayName;
                cloudJob.JobPreparationTask = job.ToJobPreparationTask(jobId, _storageAccount.CreateCloudBlobClient());
                cloudJob.JobReleaseTask = job.ToJobReleaseTask(jobId);
                cloudJob.JobManagerTask = job.ToJobManagerTask(jobId, _storageAccount.CreateCloudBlobClient(), shouldSubmitTasks, autoScale);

                cloudJob.Commit();
            }
            catch (AggregateException e)
            {
                throw ExceptionHelper.UnwrapAggregateException(e);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        private PoolInformation GetPoolInfo(PoolSettings poolSettings)
        {
            if (string.IsNullOrEmpty(poolSettings.PoolName))
            {
                return new PoolInformation
                {
                    AutoPoolSpecification = new AutoPoolSpecification
                    {
                        PoolLifetimeOption = PoolLifetimeOption.Job,
                        PoolSpecification = new PoolSpecification
                        {
                            MaxTasksPerComputeNode = poolSettings.MaxTasksPerVM,
                            CloudServiceConfiguration = new CloudServiceConfiguration("4"),
                            ResizeTimeout = TimeSpan.FromMinutes(15),
                            TargetDedicated = poolSettings.VMCount,
                            VirtualMachineSize = poolSettings.VMSize,
                            TaskSchedulingPolicy = new TaskSchedulingPolicy(ComputeNodeFillType.Spread),
                        }
                    }
                };
            }

            return new PoolInformation
            {
                PoolId = poolSettings.PoolName
            };
        }

        public IEnumerable<JobDetails> ListJobs(CancellationToken ct)
        {
            var pools = _batchClient.PoolOperations.ListPools();

            var jobDetailLevel = new ODATADetailLevel {SelectClause = "id,displayName,state,executionInfo"};

            foreach (var cloudJob in _batchClient.JobOperations.ListJobs(jobDetailLevel))
            {
                if (ct.IsCancellationRequested)
                {
                    break;
                }

                var job = new JobDetails
                {
                    Id = cloudJob.Id,
                    DisplayName = cloudJob.DisplayName,
                    State = cloudJob.State.ToString(),
                };

                if (cloudJob.ExecutionInformation != null)
                {
                    job.StartTime = cloudJob.ExecutionInformation.StartTime;
                    job.EndTime = cloudJob.ExecutionInformation.EndTime;

                    if (cloudJob.ExecutionInformation.PoolId != null)
                    {
                        var pool = pools.FirstOrDefault(p => string.Equals(cloudJob.ExecutionInformation.PoolId, p.Id));

                        if (pool != null)
                        {
                            job.PoolSettings = new PoolSettings
                            {
                                MaxTasksPerVM = pool.MaxTasksPerComputeNode.GetValueOrDefault(1),
                                State = pool.AllocationState.GetValueOrDefault(AllocationState.Resizing).ToString(),
                                VMCount = pool.CurrentDedicated.GetValueOrDefault(0),
                                VMSize = pool.VirtualMachineSize,
                            };
                        }
                    }
                }

                yield return job;
            }
        }

        public void TerminateJob(Guid jobId)
        {
            var job = GetJob(jobId);

            if (job != null)
            {
                _batchClient.JobOperations.TerminateJob(jobId.ToString());
            }
        }

        public IEnumerable<TaskDetails> ListTasks(Guid jobId, CancellationToken ct)
        {
            var job = GetJob(jobId);

            if (job != null)
            {
                var detailLevel = new ODATADetailLevel { SelectClause = "id,displayName,state,executionInfo" };

                foreach (var cloudTask in _batchClient.JobOperations.ListTasks(jobId.ToString(), detailLevel))
                {
                    if (ct.IsCancellationRequested)
                    {
                        break;
                    }

                    yield return new TaskDetails
                    {
                        Id = cloudTask.Id,
                        DisplayName = cloudTask.DisplayName,
                        State = cloudTask.State.ToString(),
                        StartTime = cloudTask.ExecutionInformation == null ? null : cloudTask.ExecutionInformation.StartTime,
                        EndTime = cloudTask.ExecutionInformation == null ? null : cloudTask.ExecutionInformation.EndTime,
                    };
                }
            }
        }

        public void DeleteJob(Guid jobId)
        {
            var blobClient = _storageAccount.CreateCloudBlobClient();
            blobClient.DefaultRequestOptions.RetryPolicy = new LinearRetry(TimeSpan.FromSeconds(3), 10);
            var containerRef = blobClient.GetContainerReference(StorageConstants.GetJobOutputContainer(jobId));
            if (containerRef.Exists())
            {
                containerRef.Delete();
            }

            var job = GetJob(jobId);

            if (job != null)
            {
                _batchClient.JobOperations.DeleteJob(jobId.ToString());
            }
        }

        private CloudJob GetJob(Guid jobId)
        {
            var detailLevel = new ODATADetailLevel { SelectClause = "id" };
            var job =
                _batchClient.JobOperations.ListJobs(detailLevel)
                    .FirstOrDefault(j => string.Equals(jobId.ToString(), j.Id));

            if (job == null)
            {
                Console.WriteLine("Job {0} not found", jobId);
                return null;
            }

            return _batchClient.JobOperations.GetJob(jobId.ToString());
        }

        private APSIMJob BuildAPSIMJobAndStageFiles(
            Guid jobId,
            string apsimAppPkgVersion,
            string displayName,
            string modelZipFile,
            CancellationToken ct)
        {
            var job = new APSIMJob
            {
                DisplayName = displayName,
                StorageCredentials = _storageCredentials,
                BatchCredentials = _batchCredentials,
                PoolSettings = _poolSettings,
                ApsimApplicationPackageVersion = apsimAppPkgVersion,
            };

            ct.ThrowIfCancellationRequested();

            job.ModelZipFileSas = _fileUploader.UploadFile(
                modelZipFile,
                jobId.ToString(),
                Path.GetFileName(modelZipFile), ct);

            ct.ThrowIfCancellationRequested();

            return job;
        }

        private void ValidateInputs(JobParameters jobParameters)
        {
            if (!File.Exists(jobParameters.ModelPath))
            {
                throw new ArgumentException("Model zip does not exist", "ModelPath");
            }
        }
    }
}
