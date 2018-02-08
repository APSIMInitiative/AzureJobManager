using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ParallelAPSIM
{
    public interface IFileUploader
    {
        /// <summary>
        /// Uploads the given file to storage and returns a SAS URL to it.
        /// 
        /// If the remote file exists, the MD5 hash is checked with the local version.  If the 
        /// file has changed a new version is uploaded.
        /// </summary>
        /// <returns></returns>
        string UploadFile(string filePath, string container, string remoteFileName, CancellationToken ct);
    }
}
