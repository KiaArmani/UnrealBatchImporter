using CommandLine;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace WoWUE4CmdImport
{
    class Options
    {
        [Option('u', "ue4", Required = true,
          HelpText = "Path to UE4 Win64 Binaries.")]
        public String UE4Path { get; set; }

        [Option('p', "project", Required = true,
          HelpText = "Path to UE4 project.")]
        public String ProjectPath { get; set; }

        [Option('s', "source", Required = true,
          HelpText = "Path to folder with source files.")]
        public String SourceFilesPath { get; set; }

        [Option('i', "importto", Required = true,
          HelpText = "UE4 path to where files should get imported to")]
        public String ImportedFilesPath { get; set; }

        [Option('e', "extension", Required = true,
          HelpText = "File extensions to import.")]
        public String FileExtensionToSearch { get; set; }        

        [Option('c', "count", Required = true,
          HelpText = "Amount of assets to process at once.")]
        public int AmountOfAssetsToProcess { get; set; }

        [Option('j', "json", Required = true,
          HelpText = "Path to Import Settings JSON file.")]
        public string PathToImportJSON { get; set; }
    }
    class Program
    {        
        static void Main(string[] args)
        {
            // Start Progress Time now
            DateTime startTime = DateTime.Now;

            // Get Command line args
            var result = CommandLine.Parser.Default.ParseArguments<Options>(args);

            // If no errors occured..
            if (!result.Errors.Any())
            {
                // ..parse the JSON file.
                JObject importSettingsJson = JObject.Parse(File.ReadAllText(result.Value.PathToImportJSON));

                // Get refs to values we need to adjust later
                JArray importGroupsJson = (JArray) importSettingsJson["ImportGroups"];
                JArray importFilesJson = (JArray) importSettingsJson.SelectToken("ImportGroups[0].FileNames");
                JValue importDestinationJson = (JValue)importSettingsJson.SelectToken("ImportGroups[0].DestinationPath");

                // Add Game prefix to path to bypass parsing bug with strings beginning with a forward slash
                importDestinationJson.Value = "/Game/" + result.Value.ImportedFilesPath;

                // Get files in source directory
                DirectoryInfo sourceDir = new DirectoryInfo(result.Value.SourceFilesPath);
                FileInfo[] sourceFiles = sourceDir.GetFiles("*." + result.Value.FileExtensionToSearch);

                // Get the file count and create a counter
                int fileMaxCount = sourceFiles.Count();
                int fileCount = 0;                

                // Get real folder name from project path
                string destiationFolder = Path.Combine(Path.GetDirectoryName(result.Value.ProjectPath), result.Value.ImportedFilesPath.Replace("Game", "Content")).Replace("/", "\\").Replace("/", @"\");

                // Create folders where processed files are being placed into
                Directory.CreateDirectory(Path.Combine(result.Value.SourceFilesPath, "__tempimport"));
                Directory.CreateDirectory(Path.Combine(result.Value.SourceFilesPath, "imported"));

                // Get the folder the application is currently running in
                var currentFolder = Environment.CurrentDirectory;

                // Create new Progress Bar
                using (var progress = new ProgressBar())
                {
                    // Make array in which we will hold the files we will process
                    List<FileInfo> processArray = new List<FileInfo>();

                    // For every file in the source folder..
                    foreach (FileInfo importFile in sourceFiles)
                    {
                        processArray.Add(importFile);
                        // If we have the amount of files we want to process once, proceed..
                        if (processArray.Count == result.Value.AmountOfAssetsToProcess)
                        {        
                            // Move files from array into temp processing folder and add it to the JSON
                            foreach (FileInfo tempImportFile in processArray)
                            {
                                string tempNewFilePath = Path.Combine(tempImportFile.DirectoryName, "__tempimport", tempImportFile.Name);
                                if (File.Exists(tempImportFile.FullName))
                                {
                                    File.Move(tempImportFile.FullName, tempNewFilePath);
                                    importFilesJson.Add(tempNewFilePath);
                                }
                            }

                            // Write temp JSON file for importer
                            File.WriteAllText(Path.Combine(currentFolder, "tempimport.json"), importSettingsJson.ToString());                          

                            // Start a new process
                            ProcessStartInfo psi = new ProcessStartInfo();
                            psi.FileName = Path.Combine(result.Value.UE4Path, "UE4Editor-Cmd.exe");
                            psi.WorkingDirectory = Path.GetDirectoryName(result.Value.UE4Path);
                            psi.Arguments = "\"" + result.Value.ProjectPath + "\" -run=ImportAssets -importSettings=\"" + Path.Combine(currentFolder, "tempimport.json").Replace(@"\\", @"\") + "\" -AllowCommandletRendering";
                            psi.CreateNoWindow = true;
                            psi.RedirectStandardError = true;
                            psi.RedirectStandardOutput = true;
                            psi.UseShellExecute = false;

                            // Get process output
                            var process = Process.Start(psi);
                            string processOutput = process.StandardOutput.ReadToEnd();

                            // Wait for the process to end. Not using Event to block code execution.
                            process.WaitForExit();

                            // If the processing did not go without errors, log it.
                            if (processOutput.Contains("Success - 0 error(s), 0 warning(s)") == false)
                            {
                                using (StreamWriter file = File.AppendText("error.txt"))
                                {
                                    string[] lines = processOutput.Split(
                                        new[] { "\r\n", "\r", "\n" },
                                        StringSplitOptions.None
                                    );

                                    foreach (string line in lines)
                                        file.WriteLine(line);

                                    file.WriteLine("---------------");                                    
                                }
                            }
                            
                            // Move imported files to imported folder.
                            foreach (FileInfo tempImportFile in processArray)
                            {
                                string tempOldFilePath = Path.Combine(tempImportFile.DirectoryName, "__tempimport", tempImportFile.Name);
                                string tempNewFilePath = Path.Combine(tempImportFile.DirectoryName, "imported", tempImportFile.Name);
                                if (File.Exists(tempOldFilePath))                                                                    
                                    File.Move(tempOldFilePath, tempNewFilePath);                                
                            }                                                        

                            // Update Progress
                            fileCount += result.Value.AmountOfAssetsToProcess;
                            TimeSpan timeRemaining = TimeSpan.FromTicks(DateTime.Now.Subtract(startTime).Ticks * (fileMaxCount - (fileCount + 1)) / (fileCount + 1));
                            progress.timeRemainingText = timeRemaining.ToString("G");
                            progress.Report((double)fileCount / fileMaxCount);

                            processArray.Clear();
                        }                                             
                    }
                }                
            }
        }
    }
}
