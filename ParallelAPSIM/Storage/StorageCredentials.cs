using System.Configuration;

namespace ParallelAPSIM.Storage
{
    public class StorageCredentials
    {
        public string Account { get; set; }
        public string Key { get; set; }

        public static StorageCredentials FromConfiguration()
        {
            return new StorageCredentials
            {
                Account = ConfigurationManager.AppSettings["StorageAccount"],
                Key = ConfigurationManager.AppSettings["StorageKey"]
            };
        }
    }
}
