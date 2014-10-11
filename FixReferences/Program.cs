using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;
using CommandLine;


namespace FixReferences {
    class Program {
        static readonly HashSet<string> _excludedDirs=new HashSet<string>{"DXBuildGenerator"};
        private static Options _options;

        static void Main(string[] args) {
            Environment.CurrentDirectory = AppDomain.CurrentDomain.SetupInformation.ApplicationBase;
            var rootDir = Path.GetFullPath(@"..\..\..\..");
            _options = new Options();
            bool arguments = Parser.Default.ParseArguments(args, _options);
            if (arguments){
                Execute(rootDir);
            }
        }

        public static Options Options{
            get { return _options; }
        }

        public static bool Execute(string rootDir){
            DeleteBackupFolders(rootDir);
            var version = GetVersion(rootDir);
            var documentHelper = new DocumentHelper();
            var projectFiles = GetProjects(rootDir).ToArray();
            foreach (var file in projectFiles) {
                var projectReferencesUpdater = new ProjectUpdater(documentHelper,rootDir,version);
                projectReferencesUpdater.Update(file);
            }

            var nuspecs = Directory.GetFiles(Path.Combine(rootDir, @"Support\Nuspec"), "*.nuspec");
            foreach (var file in nuspecs) {
                var projectReferencesUpdater = new NugetUpdater(documentHelper, rootDir, version, projectFiles,nuspecs);
                projectReferencesUpdater.Update(file);
            }
            return true;
        }

        private static IEnumerable<string> GetProjects(string rootDir){
            var files = Directory.GetFiles(rootDir, "*.csproj", SearchOption.AllDirectories);
            return files.Where(s => !_excludedDirs.Any(dir => s.IndexOf(dir, StringComparison.Ordinal) > -1));
        }

        private static void DeleteBackupFolders(string rootDir){
            var directories = Directory.EnumerateDirectories(rootDir,"Backup*",SearchOption.AllDirectories).ToList();
            foreach (var directory in directories){
                Directory.Delete(directory,true);
            }
        }

        static string GetVersion(string rootDir) {
            using (var fileStream = File.OpenRead(Path.Combine(rootDir, @"Xpand\Xpand.Utils\Properties\AssemblyInfo.cs"))) {
                using (var streamReader = new StreamReader(fileStream)) {
                    return Regex.Match(streamReader.ReadToEnd(), "Version = \"(?<version>[^\"]*)", RegexOptions.Singleline | RegexOptions.IgnoreCase)
                                .Groups["version"].Value;
                }
            }
        }
    }
}
