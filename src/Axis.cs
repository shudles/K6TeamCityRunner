using Newtonsoft.Json;

namespace K6TeamCityRunner
{
    public class Axis
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("type")]
        public string Type {get; set;}

        [JsonProperty("position")]
        public string Position {get; set;}

        [JsonProperty("ticks")]
        public Ticks Ticks {get; set;}
    }
}