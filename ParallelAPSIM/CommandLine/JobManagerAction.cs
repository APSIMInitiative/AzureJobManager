using System;
using System.Configuration;
using System.IO;
using System.Threading;
using ParallelAPSIM.Batch;
using ParallelAPSIM.Utils;

namespace ParallelAPSIM.CommandLine
{
    public class JobManagerAction : ICommandLineAction
    {
        public string GetActionName()
        {
            return "job-manager";
        }

        public int Execute(string[] args, CancellationToken ct)
        {
            ValidateArgs(args);

            var jobId = Guid.Parse(args[5]);
            var inputZipOrFolder = args[6];

            bool submitTasks = true;
            bool.TryParse(args[7], out submitTasks);

            bool autoScaleEnabled = false;
            bool.TryParse(args[8], out autoScaleEnabled);

            int coresPerProcess = 1;

            var jobManager = new Batch.JobMgr.JobManager(
                    GetBatchCredentialsFromArgs(args),
                    GetStorageCredentialsFromArgs(args),
                    new TaskProvider(
                            GetStorageCredentialsFromArgs(args),
                            inputZipOrFolder,
                            coresPerProcess));

            jobManager.Execute(jobId, submitTasks, autoScaleEnabled, ct);

            return 0;
        }

        public string GetUsage()
        {
            return string.Format("{0} <BatchUrl> <BatchAccount> <BatchKey> <StorageAccount> <StorageKey> <JobId> <InputFolderOrZipFile> <AutoScale>", GetActionName());
        }

        private void ValidateArgs(string[] args)
        {
            if (args.Length != 9)
            {
                throw new ArgumentException("Invalid number of arguments");
            }

            Guid jobId;
            if (!Guid.TryParse(args[5], out jobId))
            {
                throw new ArgumentException("Invalid job Id: " + args[5]);
            }

            var inputFileOrDir = args[6];

            if (!File.Exists(inputFileOrDir))
            {
                if (!Directory.Exists(inputFileOrDir))
                {
                    throw new ArgumentException("Input file is not a valid file or directory: " + inputFileOrDir);
                }
            }
        }

        private static Batch.BatchCredentials GetBatchCredentialsFromArgs(string[] args)
        {
            return new BatchCredentials
            {
                Url = args[0],
                Account = args[1],
                Key = args[2],
            };
        }

        private static Storage.StorageCredentials GetStorageCredentialsFromArgs(string[] args)
        {
            return new Storage.StorageCredentials
            {
                Account = args[3],
                Key = args[4],
            };
        }
    }
}
