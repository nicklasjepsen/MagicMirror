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
        public async Task<WeatherData> GetWeatherData()
        {
            // This will always use the most precise method of city look up. If coords are provided we use those,
            // then city id and lastly city plain text search string.
            var uri = $"http://api.openweathermap.org/data/2.5/weather?APPID={ApplicationDataController.GetValue(KeyNames.OpenWeatherMapApiKey, string.Empty)}&units=metric&";
            var coordsString = ApplicationDataController.GetValue(KeyNames.WeatherCityGeoCoordinates, string.Empty);
            if (!string.IsNullOrEmpty(coordsString))
            {
                var lon = coordsString.Split(',')[0];
                var lat = coordsString.Split(',')[1];
                uri = $"{uri}lat={lat}&lon={lon}";
            }
            else if (!string.IsNullOrEmpty(ApplicationDataController.GetValue(KeyNames.WeatherZip, string.Empty)) &&
                !string.IsNullOrEmpty(ApplicationDataController.GetValue(KeyNames.WeatherCountry, string.Empty)))
            {
                uri = $"{uri}zip={ApplicationDataController.GetValue(KeyNames.WeatherZip, string.Empty)},{ApplicationDataController.GetValue(KeyNames.WeatherCountry, string.Empty)}";
            }
            else if (!string.IsNullOrEmpty(ApplicationDataController.GetValue(KeyNames.WeatherCityId, string.Empty)))
            {
                uri = $"{uri}id={ApplicationDataController.GetValue(KeyNames.WeatherCityId, string.Empty)}";
            }
            else
            {
                uri = $"{uri}q={ApplicationDataController.GetValue(KeyNames.WeatherCityName, string.Empty)}";
            }
            try
            {
                var webClient = new HttpClient();
                var json = await webClient.GetStringAsync(uri);
                var jsonObject = JsonConvert.DeserializeObject<Rootobject>(json);
                return new WeatherData
                {
                    Description = jsonObject.weather.First().main,
                    Location = jsonObject.name,
                    Temp = jsonObject.main.temp,
                    WeatherIconUri = new Uri($"http://openweathermap.org/img/w/{jsonObject.weather.First().icon}.png")
                };
            }
            catch (HttpRequestException)
            {
                return null;
            }
        }
    }

    public class WeatherData
    {
        public float Temp { get; set; }
        public string Description { get; set; }
        public string Location { get; set; }
        public Uri WeatherIconUri { get; set; }
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
