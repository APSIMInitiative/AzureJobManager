using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ParallelAPSIM.Batch;

namespace ParallelAPSIM
{
    public class JobDetails
    {
        public string Id { get; set; }

        public string DisplayName { get; set; }

        public string State { get; set; }

        public DateTime? StartTime { get; set; }

        public DateTime? EndTime { get; set; }

        public TimeSpan? Duration
        {
            get
            {
                if (StartTime == null)
                {
                    return null;
                }

                return (EndTime != null ? EndTime.Value : DateTime.UtcNow) - StartTime.Value;
            }
        }

        public PoolSettings PoolSettings 
        {
            get;
            set;
        }
    }
}
