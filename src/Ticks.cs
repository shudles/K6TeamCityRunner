using Newtonsoft.Json;

namespace K6TeamCityRunner
{
    public class Ticks
    {
        [JsonProperty("min")]
        public int Min => 0;

        [JsonProperty("max")]
        public int Max {get; set;}
    }
}