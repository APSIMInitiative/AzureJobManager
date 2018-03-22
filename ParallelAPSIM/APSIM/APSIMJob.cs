using ParallelAPSIM.Batch;
using ParallelAPSIM.Storage;

namespace ParallelAPSIM.APSIM
{
    public class APSIMJob
    {
        public string DisplayName { get; set; }

        public string ModelZipFileSas { get; set; }

        public string ApsimApplicationPackage { get; set; }

        public string ApsimApplicationPackageVersion { get; set; }

        public string SevenZipApplicationPackage { get; set; }

        public BatchCredentials BatchCredentials { get; set; }

        public StorageCredentials StorageCredentials { get; set; }

        public PoolSettings PoolSettings { get; set; }
    }
}
