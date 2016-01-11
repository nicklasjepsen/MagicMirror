using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace SystemOut.MagicPiMirror
{
    public class CalendarService
    {
        private readonly string url;

        public CalendarService(string url)
        {
            this.url = url;
        }

        public async Task<CalendarModel> GetCalendar(string id)
        {
            try
            {
                var webClient = new HttpClient();
                var json = await webClient.GetStringAsync(url + id);
                return JsonConvert.DeserializeObject<CalendarModel>(json);
            }
            catch (HttpRequestException)
            {
                return null;
            }
        }
    }


    public class CalendarModel
    {
        public string Owner { get; set; }
        public Appointment[] Appointments { get; set; }
    }

    public class Appointment
    {
        public string Subject { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string Duration { get; set; }
        public bool IsPrivate { get; set; }
    }

}
