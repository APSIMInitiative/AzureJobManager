using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ParallelAPSIM.Zip
{
    public static class ZipHelper
    {
        public static string ZipFolder(string folderPath, string zipFilePath, bool overwrite = true)
        {
            if (File.Exists(zipFilePath) && !overwrite)
            {
                File.Delete(zipFilePath);
            }

            ZipFile.CreateFromDirectory(folderPath, zipFilePath);

            return zipFilePath;
        }

        public static bool CompareZipFileWithFolder(string zipFile, string folder)
        {
            if (!folder.EndsWith("\\"))
            {
                folder += "\\";
            }

            using (var archive = ZipFile.OpenRead(zipFile))
            {
                var zipFiles = archive.Entries.ToDictionary(e => e.FullName);

                var switchSep = zipFiles.Keys.FirstOrDefault(k => k.Contains("/")) != null ;

                foreach (string file in Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories))
                {
                    // Get relative path and replace backslashes with forward slashes
                    var relativePath = file.Replace(folder, string.Empty);

                    if (switchSep)
                    {
                        relativePath = relativePath.Replace("\\", "/");
                    }

                    var contains = zipFiles.ContainsKey(relativePath);

                    if (!contains)
                    {
                        return false;
                    }

                    var e = zipFiles[relativePath];

                    var fileInfo = new FileInfo(file);

                    if (fileInfo.Length != e.Length)
                    {
                        return false;
                    }
                }

                return true;
            }
        }
    }
}
