using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ParallelAPSIM.Tests
{
    public abstract class TestBase
    {
        protected string _storageAccount = "";
        protected string _storageKey = "";
        protected string _batchUrl = "";
        protected string _batchAccount = "";
        protected string _batchKey = "";

        protected readonly string _sevenZipFile = "C:\\temp\\APSIM_Test_Small\\Bin\\7za.exe";
        protected readonly string _binFolder = "C:\\temp\\APSIM_Test_Small\\Bin";
        protected readonly string _simsFolder = "C:\\temp\\APSIM_Test_Small\\Sims";
        protected readonly string _modelFolder = "C:\\temp\\APSIM_Test_Small\\Model";
        protected readonly string _inputZip = "C:\\temp\\APSIM_Test_Small\\Common\\APSIM_Input.zip";
        protected readonly string _inputFolder = "C:\\temp\\APSIM_Test_Small\\Input";

        protected JobParameters GetJobParameters()
        {
            return new JobParameters
            {
                JobDisplayName = "my job",
                ModelPath = _modelFolder,
            };
        }
    }
}
