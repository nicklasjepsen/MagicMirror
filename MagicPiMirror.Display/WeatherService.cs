using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Appointments;
using Newtonsoft.Json;

namespace SystemOut.MagicPiMirror
{
    public interface IWeatherServiceProvider
    {
        Task<WeatherData> ExecuteAsync(string uri);
    }

    public interface IHttpClient
    {
        Task<string> GetStringAsync(string uri);
    }

    internal class WeatherServiceProvider : IWeatherServiceProvider
    {
        private readonly IHttpClient httpClient;
        public WeatherServiceProvider()
        {
            httpClient = new HttpClientImp();
        }

        public WeatherServiceProvider(IHttpClient mock)
        {
            httpClient = mock;
        }

        public async Task<WeatherData> ExecuteAsync(string uri)
        {
            try
            {
                var json = await httpClient.GetStringAsync(uri);
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

        class HttpClientImp : IHttpClient
        {
            public async Task<string> GetStringAsync(string uri)
            {
                var webClient = new HttpClient();
                return await webClient.GetStringAsync(uri);
            }
        }
    }

    /// <summary>
    /// This class uses the OpenWeatherMap to get weather data.
    /// See: http://openweathermap.org/current
    /// </summary>

    public class WeatherService
    {
        private readonly IWeatherServiceProvider weatherServiceProvider;
        public string AppId { get; }
        public string Uri
        {
            get
            {
                return $"http://api.openweathermap.org/data/2.5/weather?APPID={AppId}&units=metric&";
            }
        }

        public WeatherService(string appId)
        {
            AppId = appId;
            weatherServiceProvider = new WeatherServiceProvider();
        }

        public WeatherService(IHttpClient mock)
        {
            AppId = "InvalidAppId";
            weatherServiceProvider = new WeatherServiceProvider(mock);
        }

        public async Task<WeatherData> GetWeatherDataForCoordinates(string lon, string lat)
        {
            return await weatherServiceProvider.ExecuteAsync($"{Uri}lat={lat}&lon={lon}");
        }
        public async Task<WeatherData> GetWeatherDataForCity(string cityName)
        {
            return await weatherServiceProvider.ExecuteAsync($"{Uri}q={cityName}");
        }

        public async Task<WeatherData> GetWeatherDataForCity(string zip, string country)
        {
            return await weatherServiceProvider.ExecuteAsync($"{Uri}zip={zip},{country}");
        }

        public async Task<WeatherData> GetWeatherDataForCityId(string cityId)
        {
            return await weatherServiceProvider.ExecuteAsync($"{Uri}id={cityId}");
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
        public float temp_min { get; set; }
        public float temp_max { get; set; }
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
