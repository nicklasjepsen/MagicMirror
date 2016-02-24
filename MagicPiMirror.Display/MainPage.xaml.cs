using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using SystemOut.WeatherApi.Core;
using SystemOut.WeatherApi.Core.Models.OpenWeatherMap;
using Windows.ApplicationModel.Background;
using Windows.ApplicationModel.Resources;
using Windows.Data.Xml.Dom;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Globalization;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.System.Threading;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;
using Microsoft.ApplicationInsights;
using Newtonsoft.Json;
using DayOfWeek = System.DayOfWeek;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace SystemOut.MagicPiMirror
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private const int ClockTickIntervalMs = 100;
        private const int ClockSyncIntervalMs = 50000;
        private const int CalendarTickIntervalMs = 30000;
        private const int WeatherTickIntervalMs = 30000;
        private const int ClockBlinkyTickInterval = 1000;
        private readonly SpecialDayCalendar specialDayCalendar;
        private readonly MirrorWebServer webServer;
        private TelemetryClient aiClient = new TelemetryClient();

        public MainPage()
        {
            InitializeComponent();

            // Toggle background pic on/off
#if ARM
            ParentGrid.Background = new ImageBrush
            {
                ImageSource = (ImageSource)Resources["BackgroundImg"],
                Stretch = Stretch.None,
            };
            #else
                        ParentGrid.Background = new SolidColorBrush(Colors.Black);
            #endif
            // Set all design time text entries to nothing
            TemperatureTxb.Text = string.Empty;
            WeatherIcon.Source = null;
            WeatherDescirptionTxb.Text = string.Empty;
            LocationTxb.Text = Strings.LoadingWeatherData;

            SpecialNote.Text = string.Empty;

            Day0Txb.Text = Strings.LoadingCalendarEvents;
            Day0Sp.Children.Clear();
            Day1Sp.Children.Clear();
            Day1Txb.Text = string.Empty;
            Day2Txb.Text = string.Empty;
            Day3Txb.Text = string.Empty;
            Day4Txb.Text = string.Empty;
            Day5Txb.Text = string.Empty;
            Day6Txb.Text = string.Empty;

            var webserverEventProxy = WebServerEventProxy.Instance;
            webserverEventProxy.ValueChanged += WebserverEventProxy_ValueChanged;
            specialDayCalendar = new SpecialDayCalendar();
            webServer = new MirrorWebServer();
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
#if DEBUG
            // TODO: Insert path to own settings file where settings are stored in a key/value format like:
            // Key1 [value]
            // Key2 [value2]
            // Key3Array [value3,value4]
            await ApplicationDataController.LoadDefaultSettings(File.ReadAllLines("DefaultSettings.txt"));
#endif
            //ApplicationLanguages.PrimaryLanguageOverride = "da-DK";
            ApplicationLanguages.PrimaryLanguageOverride = ApplicationDataController.GetValue(KeyNames.Language, string.Empty);

            await RefreshUiControls();
            StartClockBlinky();
            StartClock();
            await webServer.InitializeWebServer();
            await RefreshWeatherData();
            await RefreshCalendar();
            StartWeatherRefresher();
            StartCalendarRefresher();

            
            aiClient.TrackEvent("PageLoaded");
        }

        private async Task RefreshCalendar()
        {
            var sw = Stopwatch.StartNew();

            var calendarService = new CalendarService(
                ApplicationDataController.GetValue(KeyNames.CalendarServiceUrl, string.Empty));

            var allAppointments = new List<Appointment>();
            foreach (var calendarId in ApplicationDataController.GetValue(KeyNames.CalendarIdentifiers, new string[] { }))
            {
                var calendar = await calendarService.GetCalendar(calendarId);
                if (calendar == null)
                    continue;
                allAppointments.AddRange(calendar.Appointments);
            }

            // We are displaying appointments for the next 7 days.
            var days = new Dictionary<DateTime, List<Appointment>>
            {
                {TimeManager.Today, new List<Appointment>()},
                {TimeManager.Today.AddDays(1), new List<Appointment>()},
                {TimeManager.Today.AddDays(2), new List<Appointment>()},
                {TimeManager.Today.AddDays(3), new List<Appointment>()},
                {TimeManager.Today.AddDays(4), new List<Appointment>()},
                {TimeManager.Today.AddDays(5), new List<Appointment>()},
                {TimeManager.Today.AddDays(6), new List<Appointment>()},
            };

            foreach (var appointment in allAppointments)
            {
                if (days.ContainsKey(appointment.StartTime.Date))
                {
                    days[appointment.StartTime.Date].Add(appointment);
                }
                else
                {
                    Debug.WriteLine("Appointment occurring on day that we can't display. Appointment StartDate=" + appointment.StartTime);
                }
            }

            await RunOnDispatch(() =>
            {
                var appointmentHourStyle = (Style)Resources["AppointmentHourStyle"];
                var appointmentEntryStyle = (Style)Resources["AppointmentEntryStyle"];
                for (var i = 0; i < days.Count; i++)
                {
                    var currentDay = TimeManager.Today.AddDays(i);
                    var appointmentsForCurrentDay = days[currentDay];
                    var heading = (TextBlock)FindName($"Day{i}Txb");
                    if (heading == null)
                    {
                        Debug.WriteLine("Unable to find the heading textblock for the date " + currentDay);
                    }
                    else
                    {
                        if (currentDay.Date == TimeManager.Today)
                            heading.Text = Strings.TodaysAgendaHeading;
                        else if (currentDay.Date == TimeManager.Today.AddDays(1))
                            heading.Text = Strings.TomorrowHeading;
                        else
                            heading.Text = GetDayOfWeek(currentDay.DayOfWeek).ToLower();
                    }

                    // Set appointments
                    var daySp = (StackPanel)FindName($"Day{i}Sp");
                    if (daySp == null)
                    {
                        Debug.WriteLine("Unable to find the calendar stack panel for the date " + currentDay);
                    }
                    else
                    {
                        daySp.Children.Clear();
                        foreach (var appointmentGrouping in appointmentsForCurrentDay
                            .GroupBy(a => a.StartTime.ToLocalTime().ToString(Strings.CalendarHourGroupByFormatString))
                            .OrderBy(ag => ag.Key))
                        {
                            // Group by hour
                            var hourSp = new StackPanel();
                            var hourHeadning = appointmentGrouping.Key;
                            if (hourHeadning.Length < 3)
                                hourHeadning = hourHeadning + ":00";
                            hourSp.Children.Add(new TextBlock
                            {
                                Style = appointmentHourStyle,
                                Text = hourHeadning,
                            });

                            foreach (var appointment in appointmentGrouping)
                            {
                                var entry = new TextBlock
                                {
                                    TextTrimming = TextTrimming.WordEllipsis,
                                    Style = appointmentEntryStyle,
                                    Text = appointment.Subject
                                };
                                hourSp.Children.Add(entry);
                            }

                            daySp.Children.Add(hourSp);
                        }
                    }
                }
                var tc = new TelemetryClient();
                tc.TrackMetric("Refresh Calendar Time Ms", sw.Elapsed.TotalMilliseconds);
            });

        }

        private async Task RefreshUiControls()
        {
            await SetTime();
            await RefreshSpecialDayView();
            await RefreshSpecialNote();
            await RefreshSpecialNoteVisible();
        }

        private async Task RefreshSpecialDayView()
        {
            await RunOnDispatch(() =>
            {
                var specials = specialDayCalendar.GetSpecials(TimeManager.Today).ToList();
                if (specials.Any())
                {
                    SpecialNote.Text = specials.First().DisplayText;
                }
            });
        }

        private void StartClock()
        {
            ThreadPoolTimer.CreatePeriodicTimer(ClockTimer_Tick, TimeSpan.FromMilliseconds(ClockTickIntervalMs));
        }

        private void StartClockSync()
        {
            ThreadPoolTimer.CreatePeriodicTimer(ClockSync_Tick, TimeSpan.FromMilliseconds(ClockSyncIntervalMs));
        }

        private void StartCalendarRefresher()
        {
            ThreadPoolTimer.CreatePeriodicTimer(CalendarTimer_Tick, TimeSpan.FromMilliseconds(CalendarTickIntervalMs));
        }

        private void StartWeatherRefresher()
        {
            ThreadPoolTimer.CreatePeriodicTimer(WeatherTimer_Tick, TimeSpan.FromMilliseconds(WeatherTickIntervalMs));
        }

        private void StartClockBlinky()
        {
            ThreadPoolTimer.CreatePeriodicTimer(ClockBlinky_Tick, TimeSpan.FromMilliseconds(ClockBlinkyTickInterval));
        }

        private async Task RefreshWeatherData()
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () => 
            {
                var weather = new WeatherService(ApplicationDataController.GetValue(KeyNames.OpenWeatherMapApiKey, string.Empty), new CultureInfo(ApplicationLanguages.PrimaryLanguageOverride));
                WeatherData weatherData = null;
                try
                {
                    //weatherData = await weather.GetWeatherDataForCity(ApplicationDataController.GetValue(KeyNames.WeatherZip, string.Empty), ApplicationDataController.GetValue(KeyNames.WeatherCountry, string.Empty));
                    weatherData = await weather.GetWeatherDataForCity(ApplicationDataController.GetValue(KeyNames.WeatherCityName, string.Empty));
                }
                catch (WeatherServiceException weatherServiceException)
                {
                    await ShowMessageDialogIfSupported(weatherServiceException.Message, "Error");
                }

                if (weatherData == null)
                {
                    await ShowMessageDialogIfSupported(Strings.UnableToConnectToWeatherService, Strings.Error);
                }
                else
                {
                    WeatherIcon.Source = GetImageSourceFromUri(weatherData.WeatherIconUri.AbsolutePath);
                    LocationTxb.Text = Strings.Get(weatherData.Location);
                    if (string.IsNullOrEmpty(LocationTxb.Text))
                        LocationTxb.Text = weatherData.Location;
                    TemperatureTxb.Text = Math.Round(weatherData.Temp) + "°";
                    WeatherDescirptionTxb.Text = weatherData.Description;
                }
            });
        }

        private ImageSource GetImageSourceFromUri(string uri)
        {
            // http://openweathermap.org/img/w/01d.png
            var iconName = uri.Substring(uri.LastIndexOf(@"/", StringComparison.Ordinal) + 1,
                uri.LastIndexOf(".", StringComparison.Ordinal) - uri.LastIndexOf(@"/", StringComparison.Ordinal) - 1);
            var resName = string.Empty;
            switch (iconName)
            {
                case "01d": resName = "ClearDay"; break;
                case "01n": resName = "ClearNight"; break;
                case "02d": resName = "FewCloudsDay"; break;
                case "02n": resName = "FewCloudsNight"; break;
                case "03d": resName = "ScatteredCloudsDay"; break;
                case "03n": resName = "ScatteredCloudsNight"; break;
                case "04d": resName = "BrokenCloudsDay"; break;
                case "04n": resName = "BrokenCloudsNight"; break;
                case "09d": resName = "RainDay"; break;
                case "09n": resName = "RainNight"; break;
                case "10d": resName = "RainDay"; break;
                case "10n": resName = "RainNight"; break;
                case "11d": resName = "ThunderStormDay"; break;
                case "11n": resName = "ThunderStormNight"; break;
                case "13d": resName = "SnowDay"; break;
                case "13n": resName = "SnowNight"; break;
                case "50d": resName = "MistDay"; break;
                case "50n": resName = "MistNight"; break;
            }

            if (string.IsNullOrEmpty(resName))
                return null;
            return (ImageSource)Resources[resName];
        }

        private async Task ShowMessageDialogIfSupported(string message, string title)
        {
            try
            {
                var messageDialog = new MessageDialog(message, title);
                await messageDialog.ShowAsync();
            }
            // Windows 10 Core do not support message dialogs (I think, this is thrown on Windows 10 Core)
            catch (NotImplementedException)
            {
                await RunOnDispatch(() =>
                {
                    WeatherIcon.Source = null;
                    LocationTxb.Text = Strings.UnableToGetWeatherInformation;
                    TemperatureTxb.Text = "?";
                    WeatherDescirptionTxb.Text = string.Empty;
                });
            }
        }

        private async void WebserverEventProxy_ValueChanged(object sender, ValueChangedEventArg e)
        {
            switch (e.Key)
            {
                case KeyNames.SpecialNote:
                    await RefreshSpecialNote(); break;
                case KeyNames.SpecialNoteOn:
                    await RefreshSpecialNoteVisible(); break;
            }
        }

        private async Task RefreshSpecialNoteVisible()
        {
            await RunOnDispatch(() =>
            {
                if (ApplicationDataController.GetValue(KeyNames.SpecialNoteOn, false))
                    SpecialNote.Visibility = Visibility.Visible;
                else SpecialNote.Visibility = Visibility.Collapsed;
            });
        }


        private async Task RefreshSpecialNote()
        {
            await RunOnDispatch(() =>
            {
                SpecialNote.Text = ApplicationDataController.GetValue(KeyNames.SpecialNote, string.Empty);
            });
        }

        private async void CalendarTimer_Tick(ThreadPoolTimer timer)
        {
            await RefreshCalendar();
        }

        private async void ClockTimer_Tick(ThreadPoolTimer timer)
        {
            await SetTime();
        }

        private async void ClockSync_Tick(ThreadPoolTimer timer)
        {
            await SyncTime();
        }

        private async void WeatherTimer_Tick(ThreadPoolTimer timer)
        {
            await RefreshWeatherData();
        }

        private async void ClockBlinky_Tick(ThreadPoolTimer timer)
        {
            await ToggleClockBlinky();
        }

        private async Task ToggleClockBlinky()
        {
            await RunOnDispatch(() =>
            {
                if (string.IsNullOrEmpty(ClockSeperatorLabel.Text))
                    ClockSeperatorLabel.Text = ":";
                else ClockSeperatorLabel.Text = "";
            });
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
                DayTxt.Text = GetDayOfWeek(TimeManager.Today.DayOfWeek);
                DateTxb.Text = TimeManager.Now.ToString(Strings.DateFormatString);
                ClockHoursLabel.Text = TimeManager.Now.ToString(Strings.ClockHourFormatString);
                ClockMinutesLabel.Text = TimeManager.Now.ToString(Strings.ClockMinFormatString);
            });
        }

        private async Task SyncTime()
        {
            var http = new HttpClient();
            http.DefaultRequestHeaders.Add("Accept", "text/json");
            var timeStr = await http.GetStringAsync("http://appservices.systemout.net/services/api/time");
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
