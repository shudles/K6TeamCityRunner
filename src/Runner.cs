using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using static K6TeamCityRunner.Helpers;

namespace K6TeamCityRunner
{
    public class Runner
    {
        private string _k6OutputFile;
        private string _csvArtifact;
        private string _htmlReportArtifact;
        private string _templateFile;
        private string _k6ProcessLocation;
        private string _k6TestFile;
        private int _numberOfVus;
        private MetricConfiguration _configuration;

        private Dictionary<string, List<double>> _metrics;

        private Dictionary<Statistic, List<double>> _graphingMetrics;

        public Runner(int teamCityBuildNumber, string k6ProcessLocation, string configurationFile, string testFile, string outputFile, string reportFile, string templateFile, int numberOfVus)
        {
            _k6ProcessLocation = k6ProcessLocation;
            var configContents = File.ReadAllText(configurationFile);
            _configuration = JsonConvert.DeserializeObject<MetricConfiguration>(configContents);

            _metrics = new Dictionary<string, List<double>>();
            _graphingMetrics = new Dictionary<Statistic, List<double>>();
            foreach (var metric in _configuration.Metrics)
            {
                _metrics[metric.Name] = new List<double>();
                foreach (var statistic in metric.Statistics)
                    if (statistic.IsGraphable)
                        _graphingMetrics[statistic] = new List<double>();
            }

            _numberOfVus = numberOfVus;
            _k6TestFile = testFile;
            _k6OutputFile = Path.Combine(Path.GetDirectoryName(Directory.GetCurrentDirectory()), "k6Output.csv");
            _csvArtifact = outputFile;
            _htmlReportArtifact = reportFile;
            _templateFile = templateFile;
        }
        public async Task StartAsync()
        {
            if (File.Exists(_k6OutputFile))
                File.Delete(_k6OutputFile);
            if (File.Exists(_csvArtifact))
                File.Delete(_csvArtifact);
            if (File.Exists(_htmlReportArtifact))
                File.Delete(_htmlReportArtifact);

            // todo: cancel token should also be triggered when user cancels build from TC (query TC API?)
            // we'll kill k6 if it runs past 250% of the time it was specifed to run for
            var cancellationTokenSource = new CancellationTokenSource((int)Math.Round(10 * 60 * 1000 * 2.5, 0));

            var minVus = Math.Max(1, _numberOfVus - 250);
            var maxVus = Math.Max(minVus, _numberOfVus);

            var k6Args = $"run {_k6TestFile} -e minNumberOfVus={minVus} -e maxNumberOfVus={maxVus} --out csv={_k6OutputFile} --quiet --no-color";


            var k6ProcessStartInfo = new ProcessStartInfo(_k6ProcessLocation, k6Args);
            var k6StdError = new List<string>();
            var k6StdOut = new List<string>();
            var k6ProcessTask = RunProcessAsTask.ProcessEx.RunAsync(k6ProcessStartInfo, k6StdOut, k6StdError, cancellationTokenSource.Token);

            // start the k6 process, every 10 seconds we'll:
            // print any std out or std error k6 has creatted
            // read the k6 out CSV and parse it into our TC artifact && report tab
            var artifactGenerationTask = ProduceArtifact(cancellationTokenSource.Token);
            try
            {
                await k6ProcessTask;
                Console.WriteLine("K6 finished executing.");
            }
            catch (TaskCanceledException)
            {
                Console.WriteLine("K6 was cancelled.");
            }

            cancellationTokenSource.Cancel();

            await artifactGenerationTask;

            foreach (var stdOut in k6StdOut)
                Console.WriteLine(stdOut);
            foreach (var stdError in k6StdError)
                Console.WriteLine(stdError);
        }

        private async Task PublishArtifacts(CancellationToken cancellationToken)
        {
            while (true)
            {
                await BuildChartAsync();
                TeamCityPublishArtifact(_htmlReportArtifact);
                TeamCityPublishArtifact(_csvArtifact);
                if (cancellationToken.IsCancellationRequested)
                    return;
                await Task.Delay(20000);
            }
        }

        private async Task ProduceArtifact(CancellationToken cancellationToken)
        {
            while (true)
            {
                if (cancellationToken.IsCancellationRequested)
                    return; // k6 stopped before it produced any output, just return
                // wait until k6 output file appears
                if (File.Exists(_k6OutputFile))
                    break;
                await Task.Delay(1000);
            }
            var publishTask = PublishArtifacts(cancellationToken);
            // important that we open with read access and readwrite sharing otherwise k6 won't give us the read handle
            using (FileStream fileStream = File.Open(_k6OutputFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var inputReader = new StreamReader(fileStream))
            using (var outputWritter = new StreamWriter(_csvArtifact))
            {
                inputReader.ReadLine(); // consume the input header
                await outputWritter.WriteLineAsync(BuildHeaderLine());
                long previousTime = default;
                while (true)
                {
                    if (inputReader.EndOfStream)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        { // k6 is done, no more to read
                            await publishTask;
                            await PublishArtifacts(cancellationToken); // publish one more time to make sure we have the latest
                            return; 
                        }
                        await Task.Delay(10000); // currently at EOF but k6 isn't done, wait until there's more to read or we're cancelled.

                    }

                    else
                    { // process the line
                        var line = await inputReader.ReadLineAsync();
                        // metric_name,timestamp,metric_value
                        var split = line.Split(',');
                        var currentTime = long.Parse(split[1]);
                        if (previousTime == default)
                            previousTime = currentTime; // should get called once on the first line

                        if (previousTime != currentTime)
                        {
                            // we've entered a new time block, calculate the stats for the block
                            previousTime = currentTime;
                            string outLine = CalculateMetricsLine(previousTime);
                            await outputWritter.WriteLineAsync(outLine);
                            await outputWritter.FlushAsync(); // force the writter to write to the file
                            ClearMetrics();
                        }

                        var metricName = split[0];
                        if (!_configuration.Metrics.Any(m => m.Name == metricName))
                            continue; // not a metric we're interesting in

                        var metricValue = double.Parse(split[2]);
                        _metrics[metricName].Add(metricValue);
                    }
                }
            }
        }

        private string CalculateMetricsLine(long time)
        {
            var stringBuilder = new StringBuilder(time.ToString());
            stringBuilder.Append('\t');
            foreach (var metric in _configuration.Metrics)
            {
                var values = _metrics[metric.Name];
                foreach (var statistic in metric.Statistics)
                {
                    double value;
                    string stringValue;
                    switch (statistic.Type)
                    {
                        case StatisticType.Avg:
                            value = values.Count == 0 ? double.NaN : values.Average();
                            stringValue = values.Count == 0 ? "-" : values.Average().ToString();
                            break;
                        case StatisticType.Min:
                            value = values.Count == 0 ? double.NaN : values.Min();
                            stringValue = values.Count == 0 ? "-" : values.Min().ToString();
                            break;
                        case StatisticType.Max:
                            value = values.Count == 0 ? double.NaN : values.Max();
                            stringValue = values.Count == 0 ? "-" : values.Max().ToString();
                            break;
                        case StatisticType.Total:
                            value = values.Count;
                            stringValue = value.ToString();
                            break;
                        case StatisticType.Values:
                            value = double.NaN;
                            stringValue = $"[{string.Join(',', values)}]";
                            break;
                        case StatisticType.P95:
                            value = Percentile(values.ToArray(), 0.95);
                            stringValue = value.ToString();
                            break;
                        default:
                            throw new NotImplementedException($"Statistic type {statistic.Type.ToString()} is not implimented :(");
                    }
                    if (statistic.IsGraphable)
                        _graphingMetrics[statistic].Add(value);
                    if (value != double.NaN && statistic.Threshold != default && value > statistic.Threshold)
                        TeamCityTestFail(Program.TestName, $"Metric: {metric.Name}_{statistic.Type.ToString()} went over threshold {statistic.Threshold} with value {value} at {time}");
                    stringBuilder.Append(stringValue);
                    stringBuilder.Append('\t');
                }
            }
            return stringBuilder.ToString();
        }

        private async Task BuildChartAsync()
        {
            if (_graphingMetrics.Count != 0 && _graphingMetrics.First().Value.Count == 0)
                return; // nothing to graph yet
            var template = await File.ReadAllTextAsync(_templateFile);
            var writtenLabels = false;
            var jsDataSets = new List<JsDataset>();
            foreach (var statistic in _graphingMetrics)
            {
                var rgb = GenerateColour(statistic.Key.GraphName);
                var jsDataset = new JsDataset(statistic.Key.GraphName, $"rgb({rgb.Item1}, {rgb.Item2}, {rgb.Item3})", false, statistic.Value.ToList(), statistic.Key.AxisId);
                if (!writtenLabels)
                    template = template.Replace("{{labels}}", $"[{string.Join(',', Enumerable.Range(1, statistic.Value.Count))}]");
                jsDataSets.Add(jsDataset);
            }
            template = template.Replace("{{axes}}", JsonConvert.SerializeObject(_configuration.Axes, new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore // ticks property might not be set on some axes
            }));
            template = template.Replace("{{datasets}}", JsonConvert.SerializeObject(jsDataSets));
            await File.WriteAllTextAsync(_htmlReportArtifact, template);
        }

        private void ClearMetrics()
        {
            foreach (var kvp in _metrics)
                kvp.Value.Clear();
        }

        private string BuildHeaderLine()
        {
            var stringBuilder = new StringBuilder("epoch time\t");
            foreach (var metric in _configuration.Metrics)
                foreach (var statistic in metric.Statistics)
                {
                    stringBuilder.Append(metric.Name);
                    stringBuilder.Append('_');
                    stringBuilder.Append(statistic.Type.ToString());
                    stringBuilder.Append('\t');
                }
            return stringBuilder.ToString();
        }
    }
}