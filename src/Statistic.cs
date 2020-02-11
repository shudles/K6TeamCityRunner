namespace K6TeamCityRunner
{
    public class Statistic
    {
        public string GraphName {get; set;}
        public bool IsGraphable => !string.IsNullOrEmpty(GraphName)&& !string.IsNullOrEmpty(AxisId) && Type.IsGraphable();
        public StatisticType Type {get; set;}

        public string AxisId {get; set;}

        public double Threshold {get; set;}
    }
}