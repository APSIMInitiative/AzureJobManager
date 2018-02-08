using System.Configuration;

namespace ParallelAPSIM.Batch
{
    public class BatchCredentials
    {
        public string Url { get; set; }
        public string Account { get; set; }
        public string Key { get; set; }

        public static BatchCredentials FromConfiguration()
        {
            return new BatchCredentials
            {
                Url = ConfigurationManager.AppSettings["BatchUrl"],
                Account = ConfigurationManager.AppSettings["BatchAccount"],
                Key = ConfigurationManager.AppSettings["BatchKey"]
            };
        }
    }
}
