using System.Collections.Generic;
using Newtonsoft.Json;

namespace K6TeamCityRunner
{
    public class JsDataset
    {
        public JsDataset(string label, string backgroundColour, bool fill, List<double> data, string yAxisID)
        {
            Label = label;
            BackgroundColour = backgroundColour;
            Fill = fill;
            BorderColor = backgroundColour;
            Data = data;
            YAxisID = yAxisID;
        }

        [JsonProperty("label")]
        public string Label {get; set;}

        [JsonProperty("backgroundColor")]
        public string BackgroundColour {get; set;}

        [JsonProperty("fill")]
        public bool Fill {get; set;}

        [JsonProperty("borderColor")]
        public string BorderColor {get; set;}

        [JsonProperty("data")]
        public List<double> Data {get; set;}
        
        [JsonProperty("yAxisID")]
        public string YAxisID {get; set;}
    }
}