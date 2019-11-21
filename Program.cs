using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using CommandLine;
using Newtonsoft.Json.Linq;

namespace UnrealBatchImporter
{
    internal class Options
    {
        [Option('u', "ue4", Required = true,
            HelpText = "Path to UE4 Win64 Binaries.")]
        public string UE4Path { get; set; }

        [Option('p', "project", Required = true,
            HelpText = "Path to UE4 project.")]
        public string ProjectPath { get; set; }

        [Option('s', "source", Required = true,
            HelpText = "Path to folder with source files.")]
        public string SourceFilesPath { get; set; }

        [Option('i', "importto", Required = true,
            HelpText = "UE4 path to where files should get imported to")]
        public string ImportedFilesPath { get; set; }

        [Option('e', "extension", Required = true,
            HelpText = "File extensions to import.")]
        public string FileExtensionToSearch { get; set; }

        [Option('c', "count", Required = true,
            HelpText = "Amount of assets to process at once.")]
        public int AmountOfAssetsToProcess { get; set; }

        [Option('j', "json", Required = true,
            HelpText = "Path to Import Settings JSON file.")]
        public string PathToImportJSON { get; set; }

        [Option('t', "tilesubfolder", Required = false,
            HelpText = "If true, make subfolders for tiles.")]
        public bool MakeSubfolders { get; set; }
    }

    internal class Program
    {
        private static void Main(string[] args)
        {
            // Start Progress Time now
            var startTime = DateTime.Now;

            // Get Command line args
            var result = Parser.Default.ParseArguments<Options>(args);

            // If no errors occured..
            if (result.Errors.Any()) return;

            // ..parse the JSON file.
            var importSettingsJson = JObject.Parse(File.ReadAllText(result.Value.PathToImportJSON));

            // Get refs to values we need to adjust later
            var importFilesJson = (JArray) importSettingsJson.SelectToken("ImportGroups[0].FileNames");
            var importDestinationJson = (JValue) importSettingsJson.SelectToken("ImportGroups[0].DestinationPath");

            // Add Game prefix to path to bypass parsing bug with strings beginning with a forward slash
            var finalDestination = "/Game/" + result.Value.ImportedFilesPath;
            importDestinationJson.Value = finalDestination;

            // Get files in source directory
            var sourceDir = new DirectoryInfo(result.Value.SourceFilesPath);
            var sourceFiles = sourceDir.GetFiles("*." + result.Value.FileExtensionToSearch);

            // Get the file count and create a counter
            var fileMaxCount = sourceFiles.Count();
            var fileCount = 0;

            // Create folders where processed files are being placed into
            Directory.CreateDirectory(Path.Combine(result.Value.SourceFilesPath, "__tempimport"));
            Directory.CreateDirectory(Path.Combine(result.Value.SourceFilesPath, "imported"));

            // Get the folder the application is currently running in
            var currentFolder = Environment.CurrentDirectory;

            // Create new Progress Bar
            using (var progress = new ProgressBar())
            {
                // Make array in which we will hold the files we will process
                var processArray = new List<FileInfo>();

                // For every file in the source folder..
                foreach (var importFile in sourceFiles)
                {
                    processArray.Add(importFile);
                    // If we have the amount of files we want to process once, proceed..
                    if (processArray.Count != result.Value.AmountOfAssetsToProcess) continue;

                    // Move files from array into temp processing folder and add it to the JSON
                    foreach (var tempImportFile in processArray)
                    {
                        var tempNewFilePath = Path.Combine(tempImportFile.DirectoryName, "__tempimport",
                            tempImportFile.Name);
                        if (!File.Exists(tempImportFile.FullName)) continue;

                        File.Move(tempImportFile.FullName, tempNewFilePath);
                        importFilesJson.Add(tempNewFilePath);
                    }

                    if (result.Value.MakeSubfolders)
                    {
                        var tileName = Path.GetFileNameWithoutExtension(importFile.FullName)
                            .Split(new[] {"d_"}, StringSplitOptions.None)[1];
                        importDestinationJson.Value = finalDestination + "/" + tileName + "/";
                    }

                    // Write temp JSON file for importer
                    File.WriteAllText(Path.Combine(currentFolder, "tempimport.json"),
                        importSettingsJson.ToString());

                    // Start a new process
                    var psi = new ProcessStartInfo
                    {
                        FileName = Path.Combine(result.Value.UE4Path, "UE4Editor-Cmd.exe"),
                        WorkingDirectory = Path.GetDirectoryName(result.Value.UE4Path) ?? throw new InvalidOperationException(),
                        Arguments = "\"" + result.Value.ProjectPath +
                                    "\" -run=ImportAssets -importSettings=\"" +
                                    Path.Combine(currentFolder, "tempimport.json").Replace(@"\\", @"\") +
                                    "\" -AllowCommandletRendering",
                        CreateNoWindow = true,
                        RedirectStandardError = true,
                        RedirectStandardOutput = true,
                        UseShellExecute = false
                    };

                    // Get process output
                    var process = Process.Start(psi);
                    var processOutput = process.StandardOutput.ReadToEnd();

                    // Wait for the process to end. Not using Event to block code execution.
                    process.WaitForExit();

                    // If the processing did not go without errors, log it.
                    if (processOutput.Contains("Success - 0 error(s), 0 warning(s)") == false)
                        using (var file = File.AppendText("error.txt"))
                        {
                            var lines = processOutput.Split(
                                new[] {"\r\n", "\r", "\n"},
                                StringSplitOptions.None
                            );

                            foreach (var line in lines)
                                file.WriteLine(line);

                            file.WriteLine("---------------");
                        }

                    // Move imported files to imported folder.
                    foreach (var tempImportFile in processArray)
                    {
                        var tempOldFilePath = Path.Combine(tempImportFile.DirectoryName, "__tempimport",
                            tempImportFile.Name);
                        var tempNewFilePath = Path.Combine(tempImportFile.DirectoryName, "imported",
                            tempImportFile.Name);
                        if (File.Exists(tempOldFilePath))
                            File.Move(tempOldFilePath, tempNewFilePath);
                    }

                    // Update Progress
                    fileCount += result.Value.AmountOfAssetsToProcess;
                    var timeRemaining = TimeSpan.FromTicks(
                        DateTime.Now.Subtract(startTime).Ticks * (fileMaxCount - (fileCount + 1)) /
                        (fileCount + 1));
                    progress.TimeRemainingText = timeRemaining.ToString("G");
                    progress.Report((double) fileCount / fileMaxCount);

                    processArray.Clear();
                    importFilesJson.Clear();
                }
            }
        }
    }
}