using System;
using System.IO;

namespace K6TeamCityRunner
{
    public static class Helpers
    {
        public static DateTime UnixTimeStampToDateTime(long unixTimeStamp)
        {
            // Unix timestamp is seconds past epoch
            System.DateTime dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc);
            dtDateTime = dtDateTime.AddSeconds(unixTimeStamp).ToLocalTime();
            return dtDateTime;
        }

        public static double Percentile(double[] sequence, double excelPercentile)
        {
            Array.Sort(sequence);
            int N = sequence.Length;
            if (N == 0)
                return double.NaN;
            double n = (N - 1) * excelPercentile + 1;
            // Another method: double n = (N + 1) * excelPercentile;
            if (n == 1d) return sequence[0];
            else if (n == N) return sequence[N - 1];
            else
            {
                int k = (int)n;
                double d = n - k;
                return sequence[k - 1] + d * (sequence[k] - sequence[k - 1]);
            }
        }

        public static bool IsGraphable(this StatisticType statisticType)
        {
            return statisticType != StatisticType.Values;
        }

        public static void TeamCityTestStart(string testName) => Console.WriteLine($"##teamcity[testStarted name='{testName}']");

        public static void TeamCityTestFinished(string testName) => Console.WriteLine($"##teamcity[testFinished name='{testName}']");

        public static void TeamCityTestFail(string testName, string reason) => Console.WriteLine($"##teamcity[testFailed name='{testName}' message='{reason}']");
        public static void TeamCityPublishArtifact(string filePath)
        {
            Console.WriteLine($"##teamcity[publishArtifacts '{filePath}']");
        }

        public static (int, int, int) GenerateColour(string label)
        {
            var random = new Random(label.GetHashCode());
            return (random.Next(1, 255), random.Next(1, 255), random.Next(1, 255));
        } 
    }
}