using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using ParallelAPSIM.CommandLine;

namespace ParallelAPSIM
{
    public class Program
    {
        private static Dictionary<string, ICommandLineAction> _actions;

        public static void Main(string[] args)
        {
            StringBuilder summary = new StringBuilder();
            int result;
            try
            {
                summary.AppendLine("Registering actions...");
                RegisterActions();

                summary.AppendLine("Validating arguments...");
                ValidateArgs(args);

                var cts = new CancellationTokenSource();

                summary.AppendLine("Registering cancellation callback...");
                Console.CancelKeyPress += delegate (object sender, ConsoleCancelEventArgs e)
                {
                    Console.WriteLine("Cancelling");
                    cts.Cancel();
                    e.Cancel = true;
                };

                summary.AppendLine("Calculating which job to run...");
                var action = _actions[args[0]];
                summary.AppendLine("Executing " + action.GetActionName() + " job...");
                result = action.Execute(args.Skip(1).ToArray(), cts.Token);
            }
            catch (Exception err)
            {
                summary.AppendLine("Error:");
                summary.AppendLine(err.ToString());
                result = 1;
            }
            finally
            {
                storeSummary(summary.ToString(), args);
            }
            Environment.Exit(result);
        }

        private static void storeSummary(string summary, string[] args)
        {
            Guid jobId = Guid.Parse(args[6]);
            try
            {
                string summaryPath = Path.Combine(Path.Combine(Environment.GetEnvironmentVariable("TEMP")), "azure-apsim.stdout");
                if (File.Exists(summaryPath))
                    File.Delete(summaryPath);

                File.WriteAllText(summaryPath, summary);
                var storageCredentials = new Storage.StorageCredentials
                {
                    Account = args[4],
                    Key = args[5],
                };
                var credentials = new StorageCredentials(storageCredentials.Account, storageCredentials.Key);
                var _storageAccount = new CloudStorageAccount(credentials, true);
                var _blobClient = _storageAccount.CreateCloudBlobClient();
                var containerRef = _blobClient.GetContainerReference("job-" + jobId + "-outputs");
                containerRef.CreateIfNotExists();
                var blobRef = containerRef.GetBlockBlobReference(Path.GetFileName(summaryPath));
                if (!blobRef.Exists())
                    blobRef.UploadFromFile(summaryPath, FileMode.Open);
                File.Delete(summaryPath);
            }
            catch (Exception e)
            {
                summary += "Error storing job manager summary:" + e.ToString() + "\n";
            }

            string dir = Path.Combine("C:", "User", "tasks", "shared", jobId.ToString());
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            string file = Path.Combine(dir, "azure-apsim-fallback.stdout");
            File.WriteAllText(file, summary);
        }

        private static void RegisterActions()
        {
            _actions = new Dictionary<string, ICommandLineAction>();

            ICommandLineAction action = new SubmitJobAction();
            _actions[action.GetActionName()] = action;
            action = new TerminateJobAction();
            _actions[action.GetActionName()] = action;
            action = new DeleteJobAction();
            _actions[action.GetActionName()] = action;
            action = new ListJobsAction();
            _actions[action.GetActionName()] = action;
            action = new ListTasksAction();
            _actions[action.GetActionName()] = action;
            action = new JobOutputDownloadAction();
            _actions[action.GetActionName()] = action;
            action = new JobManagerAction();
            _actions[action.GetActionName()] = action;
        }

        private static void ValidateArgs(string[] args)
        {
            if (args.Length == 0)
            {
                Usage();
            }

            if (!_actions.ContainsKey(args[0]))
            {
                Console.WriteLine("Unknown command {0}", args[0]);
                Usage();
            }
        }

        private static void Usage()
        {
            StringBuilder message = new StringBuilder();
            message.AppendLine("Usage:");

            foreach (var action in _actions.Values)
                message.AppendLine($"    azure-apsim.exe {action.GetUsage()}");

            throw new Exception(message.ToString());
        }
    }
}
