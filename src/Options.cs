using CommandLine;

namespace K6TeamCityRunner
{
    public class Options
    {
        [Option('o', "output", Required = true, HelpText = "The output file to write to.")]
        public string Output { get; set; }

        [Option('r', "reportFile", Required = true, HelpText = "The html file to write the report to.")]
        public string ReportFile { get; set; }

        [Option('t', "templateFile", Required = true, HelpText = "The html template file to base the report off.")]
        public string TemplateFile { get; set; }

        [Option('k', "k6", Required = true, HelpText = "The k6 process location.")]
        public string K6Location { get; set; }
        
        [Option('t', "testFile", Required = true, HelpText = "The k6 script file to run.")]
        public string TestFile { get; set; }

        [Option('c', "config", Required = true, HelpText = "The config file for metrics to capture.")]
        public string ConfigFile { get; set; }

        [Option('v', "virtualUsers", Required = true, HelpText = "The amount of virtual users that was used in K6.")]
        public int VirtualUsers { get; set; }
    }
}