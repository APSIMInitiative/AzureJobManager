using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml;
using Microsoft.Azure.Batch;
using ParallelAPSIM.Storage;
using ParallelAPSIM.Utils;

namespace ParallelAPSIM.Batch
{
    public class TaskProvider : ITaskProvider
    {
        private readonly string _inputFileOrFolder;
        private readonly StorageCredentials _storageCredentials;
        private readonly int _coresPerProcess;
        private StringBuilder output;
        public string Output { get { return output.ToString(); } }

        public TaskProvider(
            StorageCredentials storageCredentials,
            string inputZipFileOrFolder,
            int coresPerProcess)
        {
            _storageCredentials = storageCredentials;
            _inputFileOrFolder = inputZipFileOrFolder;
            _coresPerProcess = coresPerProcess;
            output = new StringBuilder();
        }

        public IEnumerable<CloudTask> GetTasks(Guid jobId)
        {
            var index = 0;
            var models = GetApsimModels();

            foreach (var model in models)
            {
                foreach (var simulationName in model.Simulations)
                {
                    var cmd = string.Format(
                        "cmd.exe /c %AZ_BATCH_NODE_SHARED_DIR%\\%AZ_BATCH_JOB_ID%\\runtask.cmd {0} {1}",
                        model.ApsimFilename,
                        simulationName);

                    var description = string.Format("{0} ({1})",
                        simulationName,
                        Path.GetFileNameWithoutExtension(model.ApsimFilename));

                    yield return CreateTask(jobId, index.ToString(), description, cmd);

                    index++;
                }
            }
        }

        private CloudTask CreateTask(Guid jobId, string taskId, string displayName, string command)
        {
            var task = new CloudTask(taskId, command);
            task.DisplayName = displayName;
            task.EnvironmentSettings = new[]
            {
                new EnvironmentSetting("APSIM_STORAGE_ACCOUNT", _storageCredentials.Account),
                new EnvironmentSetting("APSIM_STORAGE_KEY", _storageCredentials.Key),
                new EnvironmentSetting("APSIM_STORAGE_CONTAINER_URL", GetStorageContainerUrl(jobId)),
                new EnvironmentSetting("NUMBER_OF_PROCESSORS", _coresPerProcess.ToString()),
            };
            return task;
        }

        private string GetStorageContainerUrl(Guid jobId)
        {
            var container = StorageConstants.GetJobOutputContainer(jobId);
            return string.Format("https://{0}.blob.core.windows.net/{1}", _storageCredentials.Account, container);
        }

        private IEnumerable<string> GetSimulationsFromZip()
        {
            output.AppendLine("Getting simulations from zip...");
            using (ZipArchive zip = ZipFile.OpenRead(_inputFileOrFolder))
            {
                foreach (var zipArchiveEntry in zip.Entries)
                {
                    if (zipArchiveEntry.Name.EndsWith(".simulations"))
                    {
                        using (var reader = new StreamReader(zipArchiveEntry.Open(), Encoding.UTF8))
                        {
                            yield return
                                reader.ReadToEnd()
                                    .Split(new[] {'/'}, StringSplitOptions.RemoveEmptyEntries)
                                    .Last()
                                    .Trim();
                        }
                    }
                }
            }
        }

        private string GetApsimFileFromZip()
        {
            using (ZipArchive zip = ZipFile.OpenRead(_inputFileOrFolder))
            {
                foreach (var zipArchiveEntry in zip.Entries)
                {
                    if (zipArchiveEntry.Name.ToLower().EndsWith(".apsim") || zipArchiveEntry.Name.ToLower().EndsWith(".apsimx"))
                    {
                        return Path.GetFileName(zipArchiveEntry.Name);
                    }
                }
            }

            return null;
        }

        private IEnumerable<ApsimModel> GetApsimModels()
        {
            output.AppendLine("Getting models...");
            if (File.Exists(_inputFileOrFolder) && _inputFileOrFolder.ToLower().EndsWith("zip"))
            {
                output.AppendLine($"Searching in zip archive: {_inputFileOrFolder}");
                string apsimFilename =  GetApsimFileFromZip();
                
                output.AppendLine($"apsim file name: {apsimFilename}");
                var simulations = GetSimulationsFromZip();
                if (simulations == null || simulations.Count() < 1)
                    throw new Exception($"Failed to find any simulations in zip archive {_inputFileOrFolder}");
                return new[]
                {
                    new ApsimModel
                    {
                        ApsimFilename = apsimFilename,
                        Simulations = simulations,
                    }
                };
            }

            if (Directory.Exists(_inputFileOrFolder))
            {
                output.AppendLine($"Searching in directory: {_inputFileOrFolder}");
                var apsimFiles = ListApsimFiles(_inputFileOrFolder).ToList();

                if (apsimFiles.Any())
                {
                    var models = new List<ApsimModel>();

                    foreach (var apsimFile in apsimFiles)
                    {
                        /*
                        output.AppendLine($"Found file {apsimFile}");
                        using (var stream = File.OpenRead(Path.Combine(_inputFileOrFolder, apsimFile)))
                        {
                            output.AppendLine($"Getting simulations from file {apsimFile}");
                            var simulations = GetSimulationFromApsimFile(stream);
                            if (simulations == null || simulations.Count() < 1)
                                throw new Exception($"Error: failed to find any simulations in file {apsimFile}");
                            
                            output.AppendLine($"Simulations in {apsimFile}:");
                            foreach (var sim in simulations)
                                output.AppendLine($"    {sim}");

                            models.Add(new ApsimModel
                            {
                                ApsimFilename = apsimFile,
                                Simulations = simulations,
                            });
                        }
                        */
                        models.Add(new ApsimModel
                        {
                            ApsimFilename = apsimFile,
                            Simulations = new[] { apsimFile },
                        });
                    }

                    return models;
                }
                else
                    throw new Exception($"Error: failed to find any apsim files in directory {_inputFileOrFolder}");
            }

            throw new ArgumentException("Input isn't valid file or directory: " + _inputFileOrFolder);
        }

        private IEnumerable<string> ListApsimFiles(string path)
        {
            var files = Directory.EnumerateFiles(path, "*.apsim").ToList();

            if (files.Any())
            {
                return files.Select(Path.GetFileName);
            }

            files = Directory.EnumerateFiles(path, "*.apsimx").ToList();

            if (files.Any())
            {
                return files.Select(Path.GetFileName);
            }

            return Enumerable.Empty<string>();
        }

        private static IEnumerable<string> GetSimulationFromApsimFile(Stream stream)
        {
            var sims = new List<string>();

            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                using (XmlReader xmlReader = XmlReader.Create(reader))
                {
                    while (xmlReader.Read())
                    {
                        switch (xmlReader.NodeType)
                        {
                            case XmlNodeType.Element: // The node is an element.
                                if (string.Equals(xmlReader.Name, "simulation",
                                    StringComparison.InvariantCultureIgnoreCase))
                                {
                                    sims.Add(xmlReader.GetAttribute("name"));
                                }
                                break;
                        }
                    }
                }
            }

            return sims;
        }
    }
}
