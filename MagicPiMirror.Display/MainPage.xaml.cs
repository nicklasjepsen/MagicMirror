using System;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Threading.Tasks;
using Windows.Globalization;
using Windows.System.Threading;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Microsoft.ApplicationInsights;
using Newtonsoft.Json;
using DayOfWeek = System.DayOfWeek;

namespace SystemOut.MagicPiMirror
{
    public sealed partial class MainPage : Page
    {
        private const double ClockTickIntervalMs = 10;
        private const int ClockSyncIntervalMs = 50000;
        private readonly TelemetryClient aiClient = new TelemetryClient();

        public MainPage()
        {
            InitializeComponent();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            ApplicationLanguages.PrimaryLanguageOverride = ApplicationDataController.GetValue(KeyNames.Language, string.Empty);
            StartClock();
            aiClient.TrackEvent("PageLoaded");
        }
        
        private void LogError(string message)
        {
            aiClient.TrackException(new Exception(message));
            Debug.WriteLine(message);
        }

        private void LogException(Exception e)
        {
            if (e == null)
            {
                LogError("Unable to log exception - exception is null.");
            }
            else
            {
                aiClient.TrackException(e);
                Debug.WriteLine(e.Message);
            }
        }

        private async Task RefreshUiControls()
        {
            await SetTime();
        }

        private void StartClock()
        {
            ThreadPoolTimer.CreatePeriodicTimer(ClockTimer_Tick, TimeSpan.FromMilliseconds(ClockTickIntervalMs));
        }

        private void StartClockSync()
        {
            ThreadPoolTimer.CreatePeriodicTimer(ClockSync_Tick, TimeSpan.FromMilliseconds(ClockSyncIntervalMs));
        }

        private async void ClockTimer_Tick(ThreadPoolTimer timer)
        {
            await SetTime();
        }

        private async void ClockSync_Tick(ThreadPoolTimer timer)
        {
            await SyncTime();
        }

        private async Task RunOnDispatch(Action a)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                a();
            });
        }

        private async Task SetTime()
        {
            await RunOnDispatch(() =>
            {
                DayTxt.Text = $"{GetDayOfWeek(TimeManager.Today.DayOfWeek)} {TimeManager.Now.ToString(Strings.DateFormatString)}";
                ClockHoursLabel.Text = TimeManager.Now.ToString(Strings.ClockHourFormatString);
                ClockMinutesLabel.Text = TimeManager.Now.ToString(Strings.ClockMinFormatString);
            });
        }

        private async Task SyncTime()
        {
            string timeStr;
            var http = new HttpClient();
            http.DefaultRequestHeaders.Add("Accept", "text/json");
            try
            {
                timeStr = await http.GetStringAsync("http://appservices.systemout.net/services/api/time");
            }
            catch (HttpRequestException httpException)
            {
                LogException(httpException);

                return;
            }
            
            var dtUtc = JsonConvert.DeserializeObject<DateTime>(timeStr);

            // If more than 1 min off, alert and 
            var offTime = DateTime.UtcNow - dtUtc;
            if (offTime > new TimeSpan(0, 1, 0) ||
                offTime < new TimeSpan(0, -1, 0))
            {
                TimeManager.UpdateOffset(dtUtc);
                aiClient.TrackTrace($"Warning: Mirror time is off by {offTime}.");
            }
        }


        private static string GetDayOfWeek(DayOfWeek dayOfWeek)
        {
            return DateTimeFormatInfo.CurrentInfo.GetDayName(dayOfWeek);
        }
    }
}
