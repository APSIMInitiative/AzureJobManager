using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ParallelAPSIM.Utils;

namespace ParallelAPSIM.CommandLine
{
    public class ListTasksAction : ICommandLineAction
    {
        public string GetActionName()
        {
            return "job-list-tasks";
        }

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
                var apsim = new ParallelAPSIM(
                    Storage.StorageCredentials.FromConfiguration(),
                    Batch.BatchCredentials.FromConfiguration(),
                    Batch.PoolSettings.FromConfiguration());

                var tasks = apsim.ListTasks(Guid.Parse(args[0]), ct);

                foreach (var task in tasks)
                {
                    var duration = task.Duration.HasValue ? Convert.ToInt32(task.Duration.Value.TotalMinutes) + " minute(s)" : "";

                    Console.WriteLine("TaskId: {0}", task.Id);
                    Console.WriteLine("    Task Info");
                    Console.WriteLine("        Description: {0}", task.DisplayName);
                    Console.WriteLine("        State: {0}", task.State);
                    Console.WriteLine("        StartTime: {0}", task.StartTime != null ? task.StartTime.Value.ToString() : "");
                    Console.WriteLine("        EndTime: {0}", task.EndTime != null ? task.EndTime.Value.ToString() : "");
                    Console.WriteLine("        Duration: {0}", duration);
                    Console.WriteLine("");
                }
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
            return string.Format("{0} <JobId>", GetActionName());
        }

        private void ValidateArgs(string[] args)
        {
            if (args == null || args.Length != 1)
            {
                throw new ArgumentException("Invalid number of arguments");
            }

            Guid jobId;
            if (!Guid.TryParse(args[0], out jobId))
            {
                throw new ArgumentException("Invalid job Id");
            }
        }
    }
}
