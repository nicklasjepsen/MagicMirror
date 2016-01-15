using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using SystemOut.WeatherApi.Core;
using Windows.ApplicationModel.Background;
using Windows.Data.Xml.Dom;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.System.Threading;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace SystemOut.MagicPiMirror
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private const int ClockTickIntervalMs = 100;
        private const int CalendarTickIntervalMs = 180000;
        private const int WeatherTickIntervalMs = 30000;
        private const int ClockBlinkyTickInterval = 1000;
        private readonly SpecialDayCalendar specialDayCalendar;
        private readonly MirrorWebServer webServer;

        public MainPage()
        {
            InitializeComponent();

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
            await RefreshUiControls();
            StartClockBlinky();
            StartClock();
            await webServer.InitializeWebServer();
            await RefreshWeatherData();
            await RefreshCalendar();
            StartWeatherRefresher();
            StartCalendarRefresher();
        }

        private async Task RefreshCalendar()
        {
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
                {DateTime.Today, new List<Appointment>()},
                {DateTime.Today.AddDays(1), new List<Appointment>()},
                {DateTime.Today.AddDays(2), new List<Appointment>()},
                {DateTime.Today.AddDays(3), new List<Appointment>()},
                {DateTime.Today.AddDays(4), new List<Appointment>()},
                {DateTime.Today.AddDays(5), new List<Appointment>()},
                {DateTime.Today.AddDays(6), new List<Appointment>()},
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
                for (var i = 0; i < days.Count; i++)
                {
                    var currentDay = DateTime.Today.AddDays(i);
                    var appointmentsForCurrentDay = days[currentDay];
                    if (i == 0)
                    {
                        // Today is treated special - we show todays schedule in the upper left side of the screen

                    }
                    else
                    {
                        var heading = (TextBlock)FindName($"Day{i}Txb");
                        if (heading == null)
                        {
                            Debug.WriteLine("Unable to find the heading textblock for the date " + currentDay);
                        }
                        else
                        {
                            heading.Text = currentDay.DayOfWeek.ToString().ToLower();
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
                                .GroupBy(a => a.StartTime.ToLocalTime().ToString("HH", new CultureInfo("da-dk")))
                                .OrderBy(ag => ag.Key))
                            {
                                // Group by hour
                                var hourSp = new StackPanel();
                                hourSp.Children.Add(new TextBlock
                                {
                                    Style = (Style)Resources["AppointmentHourStyle"],
                                    Text = appointmentGrouping.Key + ":00",
                                });

                                foreach (var appointment in appointmentGrouping)
                                {
                                    var entry = new TextBlock
                                    {
                                        TextTrimming = TextTrimming.WordEllipsis,
                                        Style = (Style)Resources["AppointmentEntryStyle"],
                                        Text =appointment.Subject
                                    };
                                    hourSp.Children.Add(entry);
                                }

                                daySp.Children.Add(hourSp);
                            }
                        }
                    }
                }
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
                var specials = specialDayCalendar.GetSpecials(DateTime.Today).ToList();
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
            var weather = new WeatherService(ApplicationDataController.GetValue(KeyNames.OpenWeatherMapApiKey, string.Empty));
            //var weatherData = await weather.GetWeatherDataForCity(ApplicationDataController.GetValue(KeyNames.WeatherZip, string.Empty), ApplicationDataController.GetValue(KeyNames.WeatherCountry, string.Empty));
            var weatherData = await weather.GetWeatherDataForCity(ApplicationDataController.GetValue(KeyNames.WeatherCityName, string.Empty));

            if (weatherData == null)
            {
                try
                {
                    var messageDialog = new MessageDialog("Unable to connect to the Weather Service.", "Error");
                    await messageDialog.ShowAsync();
                }
                // Windows 10 Core do not support message dialogs (I think, this is thrown on Windows 10 Core)
                catch (NotImplementedException)
                {
                    await RunOnDispatch(() =>
                    {
                        WeatherIcon.Source = null;
                        LocationTxb.Text = "Unable to get weather information.";
                        TemperatureTxb.Text = "?";
                        WeatherDescirptionTxb.Text = string.Empty;
                    });
                }
            }
            else
            {
                await RunOnDispatch(() =>
                {
                    WeatherIcon.Source = GetImageSourceFromUri(weatherData.WeatherIconUri.AbsolutePath);
                    LocationTxb.Text = weatherData.Location;
                    TemperatureTxb.Text = Math.Round(weatherData.Temp) + "°";
                    WeatherDescirptionTxb.Text = weatherData.Description;
                });
            }
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
                case "50n": resName = "RainNight"; break;
            }

            if (string.IsNullOrEmpty(resName))
                return null;
            return (ImageSource)Resources[resName];
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
                DayTxt.Text = GetDanishDayOfWeek();
                DateTxb.Text = DateTime.Now.ToString("d. MMMM yyyy", new CultureInfo("da-dk"));
                ClockHoursLabel.Text = DateTime.Now.ToString("HH");
                ClockMinutesLabel.Text = DateTime.Now.ToString("mm");
            });
        }

        private static string GetDanishDayOfWeek()
        {
            // TODO: Add localization support
            switch (DateTime.Now.DayOfWeek)
            {
                case DayOfWeek.Friday:
                    return "Fredag";
                case DayOfWeek.Monday:
                    return "Mandag";
                case DayOfWeek.Saturday:
                    return "Lørdag";
                case DayOfWeek.Sunday:
                    return "Søndag";
                case DayOfWeek.Thursday:
                    return "Torsdag";
                case DayOfWeek.Tuesday:
                    return "Tirsdag";
                case DayOfWeek.Wednesday:
                    return "Onsdag";
                default:
                    return "Pludselig var der to mandage på en uge...";
            }
        }
    }
}
