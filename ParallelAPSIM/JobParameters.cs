using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ParallelAPSIM
{
    public class JobParameters
    {
        public string JobDisplayName { get; set; }
        public string ModelPath { get; set; }
        public string ApplicationPackage { get; set; }
        public string ApplicationPackageVersion { get; set; }
        public int CoresPerProcess { get; set; }
        public bool JobManagerShouldSubmitTasks { get; set; }
        public bool AutoScale { get; set; }
        public bool NoWait { get; set; }
    }
}
