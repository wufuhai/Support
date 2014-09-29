using CommandLine;
using CommandLine.Text;

namespace PDBLinker {
    public class Options {
        [Option("sourcedir", Required = true, HelpText = @"The source code directory. eg. C:\DevExpress\Sources\")]
        public string SourceDir { get; set; }

        [Option("pdbdir", Required = true, HelpText = "The symbols folder")]
        public string PDBDir { get; set; }

        [Option("BuildConfiguration", DefaultValue = "Release")]
        public string BuildConfiguration { get; set; }

        [Option("DbgToolsPath", DefaultValue = @"..\..\..\Tool\srcsrv\")]
        public string DbgToolsPath { get; set; }

        [HelpOption]
        public string GetUsage() {
            return HelpText.AutoBuild(this,current => HelpText.DefaultParsingErrorsHandler(this, current));
        }
    }
}
