using CommandLine;
using CommandLine.Text;

namespace ProcessAsUser {
    public class Options {
        [Option('e', "path", Required = true, HelpText = "The process path")]
        public string ExePath { get; set; }

        [Option('a', "arguments", Required = true, HelpText = "The process arguments")]
        public string Arguments { get; set; }

        [Option('u', "username", Required = true, HelpText = "A remote desktop session will be created with this user")]
        public string UserName { get; set; }

        [Option('p', "password", Required = true, HelpText = "The user password")]
        public string Password { get; set; }

        [ParserState]
        public IParserState LastParserState { get; set; }

        [HelpOption]
        public string GetUsage() {
            return HelpText.AutoBuild(this,current => HelpText.DefaultParsingErrorsHandler(this, current));
        }
    }
}
