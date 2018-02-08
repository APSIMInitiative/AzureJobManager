using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ParallelAPSIM.Utils;

namespace ParallelAPSIM.CommandLine
{
    public class ListJobsAction : ICommandLineAction
    {
        public string GetActionName()
        {
            return "job-list";
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

                var jobs = apsim.ListJobs(ct);

                foreach (var job in jobs)
                {
                    var duration = job.Duration.HasValue ? Convert.ToInt32(job.Duration.Value.TotalMinutes) + " minute(s)" : "";

                    Console.WriteLine("JobId: {0}", job.Id);
                    Console.WriteLine("    Job Info");
                    Console.WriteLine("        Description: {0}", job.DisplayName);
                    Console.WriteLine("        State: {0}", job.State);
                    Console.WriteLine("        StartTime: {0}", job.StartTime != null ? job.StartTime.Value.ToString() : "");
                    Console.WriteLine("        EndTime: {0}", job.EndTime != null ? job.EndTime.Value.ToString() : "");
                    Console.WriteLine("        Duration: {0}", duration);

                    Console.WriteLine("    Pool Info");
                    if (job.PoolSettings != null)
                    {
                        Console.WriteLine("        VM Count: {0}", job.PoolSettings.VMCount);
                        Console.WriteLine("        VM Size: {0}", job.PoolSettings.VMSize);
                        Console.WriteLine("        Allocation State: {0}", job.PoolSettings.State);
                    }
                    else
                    {
                        Console.WriteLine("        Not available");
                    }

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
            return string.Format("{0}", GetActionName());
        }

        private void ValidateArgs(string[] args)
        {
            if (args.Length != 0)
            {
                throw new ArgumentException("Invalid number of arguments");
            }
        }
    }
}
