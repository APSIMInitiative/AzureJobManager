using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ParallelAPSIM.Batch
{
    public class PoolSettings
    {
        public string VMSize { get; set; }

        public int VMCount { get; set; }

        public int MaxTasksPerVM { get; set; }

        public string PoolName { get; set; }

        public string State { get; set; }

        public static PoolSettings FromConfiguration()
        {
            return new PoolSettings
            {
                MaxTasksPerVM = int.Parse(ConfigurationManager.AppSettings["PoolMaxTasksPerVM"]),
                PoolName = ConfigurationManager.AppSettings["PoolName"],
                VMCount = int.Parse(ConfigurationManager.AppSettings["PoolVMCount"]),
                VMSize = ConfigurationManager.AppSettings["PoolVMSize"],
            };
        }
    }
}
