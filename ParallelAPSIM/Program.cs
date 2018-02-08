using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ParallelAPSIM.CommandLine;

namespace ParallelAPSIM
{
    public class Program
    {
        private static Dictionary<string, ICommandLineAction> _actions;

        public static void Main(string[] args)
        {

            RegisterActions();

            ValidateArgs(args);

            var cts = new CancellationTokenSource();

            Console.CancelKeyPress += delegate(object sender, ConsoleCancelEventArgs e)
            {
                Console.WriteLine("Cancelling");
                cts.Cancel();
                e.Cancel = true;
            };

            try
            {
                var action = _actions[args[0]];
                var result = action.Execute(args.Skip(1).ToArray(), cts.Token);
                Environment.Exit(result);
            }
            catch (ArgumentException e)
            {
                Console.WriteLine(e.Message);
                Environment.Exit(1);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
                Environment.Exit(1);
            }
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
            Console.WriteLine("");
            Console.WriteLine("Usage:");

            foreach (var action in _actions.Values)
            {
                Console.WriteLine("    parallelapsim.exe {0}", action.GetUsage());
            }

            Environment.Exit(1);
        }
    }
}
