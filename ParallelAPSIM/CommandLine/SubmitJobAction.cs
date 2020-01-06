using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Management.Batch;
using Microsoft.WindowsAzure.Storage;
using ParallelAPSIM.Storage;
using ParallelAPSIM.Utils;

namespace ParallelAPSIM.CommandLine
{
    public class SubmitJobAction : ICommandLineAction
    {
        public string GetActionName()
        {
            return "job-submit";
        }

        public string Output { get { return ""; } }

        public int Execute(string[] args, CancellationToken ct)
        {
            try
            {
                ValidateArgs(args);
            }
            catch (ArgumentException e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(GetUsage());
                return 1;
            }

            try
            {
                InitPackages(Storage.StorageCredentials.FromConfiguration());

                var jobParameters = GetJobParametersFromArgs(args);

                var apsim = new ParallelAPSIM(
                    Storage.StorageCredentials.FromConfiguration(),
                    Batch.BatchCredentials.FromConfiguration(),
                    Batch.PoolSettings.FromConfiguration());

                var jobId = apsim.SubmitJob(jobParameters, ct);

                if (!jobParameters.NoWait)
                {
                    var baseOutputPath = Path.Combine(Directory.GetCurrentDirectory(), "Outputs");
                    var jobOutput = new JobOutputMonitor();
                    jobOutput.Execute(jobId, baseOutputPath, ct);
                }
            }
            catch (OperationCanceledException)
            {
                return 1;
            }
            catch (AggregateException e)
            {
                var unwrapped = ExceptionHelper.UnwrapAggregateException(e);
                Console.WriteLine(unwrapped.Message);
                Console.WriteLine(unwrapped.StackTrace);
                return 1;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
                return 1;
            }

            return 0;
        }

        public string GetUsage()
        {
            return string.Format("{0} [-nowait] <JobName> <ModelPath>", GetActionName());
        }

        private void ValidateArgs(string [] args)
        {
            var argList = args.ToList();

            if (argList.Count < 2)
            {
                throw new ArgumentException("Invalid number of arguments");
            }

            if (argList.Contains("-nowait"))
            {
                argList = argList.Where(arg => arg != "-nowait").ToList();
            }

            if (string.IsNullOrWhiteSpace(argList[0]))
            {
                throw new ArgumentException("Invalid job name: " + argList[0]);
            }

            if (!File.Exists(argList[1]))
            {
                throw new ArgumentException("Invalid model path: " + argList[1]);
            }
        }

        private JobParameters GetJobParametersFromArgs(string[] args)
        {
            int cores;
            bool jobManagerShouldSubmitTasks;
            bool autoScale;
            bool noWait = false;

            if (args.Contains("-nowait"))
            {
                args = args.Where(arg => arg != "-nowait").ToArray();
                noWait = true;
            }

            return new JobParameters
            {
                JobDisplayName = args[0],
                ModelPath = args[1],
                ApplicationPackage = ConfigurationManager.AppSettings["DefaultApplicationPackage"],
                ApplicationPackageVersion = ConfigurationManager.AppSettings["DefaultApplicationPackageVersion"],
                CoresPerProcess = int.TryParse(ConfigurationManager.AppSettings["ApsimCoresPerProcess"], out cores) ? cores : 1,
                JobManagerShouldSubmitTasks = bool.TryParse(ConfigurationManager.AppSettings["JobManagerShouldSubmitTasks"], out jobManagerShouldSubmitTasks) ? jobManagerShouldSubmitTasks : false,
                AutoScale = bool.TryParse(ConfigurationManager.AppSettings["AutoScale"], out autoScale) ? autoScale : false,
                NoWait = noWait,
            };
        }

        private void InitPackages(StorageCredentials credentials)
        {
            string codeBase = Assembly.GetExecutingAssembly().CodeBase;
            UriBuilder uri = new UriBuilder(codeBase);
            string path = Uri.UnescapeDataString(uri.Path);
            var executableDirectory = Path.GetDirectoryName(path);
            var toolsDir = Path.Combine(executableDirectory, "tools");
            var apsimDIr = Path.Combine(executableDirectory, "apsim");

            if (!Directory.Exists(toolsDir))
            {
                throw new DirectoryNotFoundException("Tools directory not found: " + toolsDir);
            }

            if (!Directory.Exists(apsimDIr))
            {
                throw new DirectoryNotFoundException("Apsim directory not found: " + apsimDIr);
            }

            // Upload 7zip and AzCopy
            foreach (var filePath in Directory.EnumerateFiles(toolsDir))
            {
                UploadFileIfNeeded(credentials, "tools", filePath);
            }

            // Upload job manager
            UploadFileIfNeeded(credentials, "jobmanager", Path.Combine(executableDirectory, "azure-apsim.exe"));

            // Upload Apsim zips
            foreach (var filePath in Directory.EnumerateFiles(apsimDIr))
            {
                UploadFileIfNeeded(credentials, "apsim", filePath);
            }
        }

        private void UploadFileIfNeeded(StorageCredentials credentials, string containerName, string filePath)
        {
            var storageAccount = new CloudStorageAccount(
                new Microsoft.WindowsAzure.Storage.Auth.StorageCredentials(
                    credentials.Account,
                    credentials.Key), true);

            var blobClient = storageAccount.CreateCloudBlobClient();

            var containerRef = blobClient.GetContainerReference(containerName);
            containerRef.CreateIfNotExists();

            var blobRef = containerRef.GetBlockBlobReference(Path.GetFileName(filePath));
            if (!blobRef.Exists() || blobRef.Properties.Length != new FileInfo(filePath).Length)
            {
                blobRef.UploadFromFile(filePath, FileMode.Open);
            }
        }
    }
}
