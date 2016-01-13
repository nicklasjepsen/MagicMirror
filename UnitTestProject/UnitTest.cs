using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;

namespace UnitTestProject
{
    //[TestClass]
    //public class WeatherServiceUnitTest
    //{
    //    [TestMethod]
    //    public async Task GetWeatherDataUnitTest()
    //    {
    //        var service = new WeatherService(new HttpClientMock());
    //        var data = await service.GetWeatherDataForCity("copenhagen");
    //        Assert.AreEqual(1.56f, data.Temp);
    //    }
    //}

    //public class HttpClientMock : IHttpClient
    //{
    //    public Task<string> GetStringAsync(string uri)
    //    {
    //        if (uri.Contains("q=copenhagen"))
    //            return
    //                Task.FromResult(
    //                    "{\"coord\":{\"lon\":12.57,\"lat\":55.68},\"weather\":[{\"id\":741,\"main\":\"Fog\",\"description\":\"fog\",\"icon\":\"50d\"},{\"id\":520,\"main\":\"Rain\",\"description\":\"light intensity shower rain\",\"icon\":\"09d\"}],\"base\":\"cmc stations\",\"main\":{\"temp\":1.56,\"pressure\":992,\"humidity\":100,\"temp_min\":1,\"temp_max\":2.2},\"wind\":{\"speed\":2.6,\"deg\":140},\"clouds\":{\"all\":90},\"dt\":1452517491,\"sys\":{\"type\":1,\"id\":5245,\"message\":0.0032,\"country\":\"DK\",\"sunrise\":1452497590,\"sunset\":1452524550},\"id\":2618425,\"name\":\"Copenhagen\",\"cod\":200}");
    //        else return null;
    //    }
    //}
}
