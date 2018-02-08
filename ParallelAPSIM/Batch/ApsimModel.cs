using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ParallelAPSIM.Batch
{
    public class ApsimModel
    {
        public string ApsimFilename { get; set; }

        public IEnumerable<string> Simulations { get; set; }
    }
}
