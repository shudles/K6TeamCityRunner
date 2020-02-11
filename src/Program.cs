using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommandLine;
using static K6TeamCityRunner.Helpers;

namespace K6TeamCityRunner
{
    public class Program
    {
        public static string TestName;

        static async Task Main(string[] args)
        {
            Options options = null;
            Parser.Default.ParseArguments<Options>(args).WithParsed<Options>(o => options = o);

            TestName = $"VUs-{options.VirtualUsers}";

            TeamCityTestStart(TestName);

            try
            {
                await new Runner(117, options.K6Location, options.ConfigFile, options.TestFile, options.Output, options.ReportFile, options.TemplateFile, options.VirtualUsers).StartAsync();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.StackTrace);
                TeamCityTestFail(TestName, $"Test crashed from exception rather than threshold fail: {e.Message} | {e.StackTrace.Replace('\n', ' ').Replace('[', '(').Replace(']', ')')}");
            }
            finally
            {
                TeamCityTestFinished(TestName);
            }
        }
    }
}
