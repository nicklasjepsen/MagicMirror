using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace SystemOut.MagicPiMirror
{
    /// <summary>
    /// This class uses the OpenWeatherMap to get weather data.
    /// See: http://openweathermap.org/current
    /// </summary>

    public class WeatherService
    {
        public async Task<float> GetWeatherData()
        {
            //http://api.openweathermap.org/data/2.5/weather?q={copenhagen}&APPID=5b92e266d1435f9ddac2cc47419c4d9e&units=metric
            var webClient = new HttpClient();
            var json = await webClient.GetStringAsync(
                "http://api.openweathermap.org/data/2.5/weather?q={copenhagen}&APPID=5b92e266d1435f9ddac2cc47419c4d9e&units=metric");

            var jsonObject = JsonConvert.DeserializeObject<Rootobject>(json);
            return jsonObject.main.temp;
        }
    }

    public class Rootobject
    {
        public Coord coord { get; set; }
        public Weather[] weather { get; set; }
        public string _base { get; set; }
        public Main main { get; set; }
        public Wind wind { get; set; }
        public Clouds clouds { get; set; }
        public int dt { get; set; }
        public Sys sys { get; set; }
        public int id { get; set; }
        public string name { get; set; }
        public int cod { get; set; }
    }

    public class Coord
    {
        public float lon { get; set; }
        public float lat { get; set; }
    }

    public class Main
    {
        public float temp { get; set; }
        public int pressure { get; set; }
        public int humidity { get; set; }
        public int temp_max { get; set; }
    }

    public class Wind
    {
        public float speed { get; set; }
        public int deg { get; set; }
    }

    public class Clouds
    {
        public int all { get; set; }
    }

    public class Rain
    {
        public int volume { get; set; }
    }

    public class Sys
    {
        public int type { get; set; }
        public int id { get; set; }
        public float message { get; set; }
        public string country { get; set; }
        public int sunrise { get; set; }
        public int sunset { get; set; }
    }

    public class Weather
    {
        public int id { get; set; }
        public string main { get; set; }
        public string description { get; set; }
        public string icon { get; set; }
    }

}
