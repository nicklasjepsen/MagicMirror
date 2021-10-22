using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Windows.Globalization;
using Windows.System.Threading;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Microsoft.Graph;
using Microsoft.Graph.Extensions;
using Microsoft.Identity.Client;
using SystemOut.WeatherApi.Core;
using SystemOut.WeatherApi.Core.Models.OpenWeatherMap;
using DayOfWeek = System.DayOfWeek;

namespace MagicMirror
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private readonly SpecialDayCalendar _specialDayCalendar;
        private const int ClockTickIntervalMs = 100;
        private const int ClockSyncIntervalMs = 50000;
        private const int CalendarTickIntervalMs = 30000;
        private const int WeatherTickIntervalMs = 30000;
        private const int ClockBlinkyTickInterval = 1000;

        private GraphServiceClient _graphClient;
        private readonly string[] _scopes = {"user.read", "calendars.read"};
        private readonly string _clientId;
        private const string Tenant = "common"; // Alternatively "[Enter your tenant, as obtained from the Azure portal, e.g. kko365.onmicrosoft.com]"
        private const string Authority = "https://login.microsoftonline.com/" + Tenant;
        private IPublicClientApplication _publicClientApp;
        private const string MsGraphUrl = "https://graph.microsoft.com/v1.0/";
        private AuthenticationResult _authResult;

        private readonly string _calendarIdentifier;

        public MainPage()
        {
            InitializeComponent();            
            _specialDayCalendar = new SpecialDayCalendar();
            var settings = System.IO.File.ReadAllLines("MagicPiMirrorSettings.txt");
            ApplicationDataController.LoadDefaultSettings(settings, true);
            _clientId = ApplicationDataController.GetValue("MsGraphClientId", string.Empty);
            _calendarIdentifier = ApplicationDataController.GetValue("CalendarIdentifier", string.Empty);
            //ApplicationLanguages.PrimaryLanguageOverride = ApplicationDataController.GetValue(nameof(Language), string.Empty);
            //Windows.ApplicationModel.Resources.Core.ResourceContext.GetForCurrentView().Reset();
            //Windows.ApplicationModel.Resources.Core.ResourceContext.GetForViewIndependentUse().Reset();
            SpecialNote2.Text = GetAppVersion();
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            await RefreshUiControls();
            // StartClockSync();
            StartClockBlinky();
            StartClock();

            _graphClient = await SignInAndInitializeGraphServiceClient(_scopes);

            await GetCalendar();
            await RefreshWeatherData();
            StartWeatherRefresher();
            StartCalendarRefresher();
        }
        public static string GetAppVersion()
        {
            var package = Windows.ApplicationModel.Package.Current;
            var packageId = package.Id;
            var version = packageId.Version;

            return $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
        }
        private async Task RefreshUiControls()
        {
            await SetTime();
            await RefreshSpecialDayView();
            await RefreshSpecialNote();
            await RefreshSpecialNoteVisible();
        }

        private void StartCalendarRefresher()
        {
            ThreadPoolTimer.CreatePeriodicTimer(CalendarTimer_Tick, TimeSpan.FromMilliseconds(CalendarTickIntervalMs));
        }

        private async void CalendarTimer_Tick(ThreadPoolTimer timer)
        {
            await GetCalendar();
        }

        private async void WeatherTimer_Tick(ThreadPoolTimer timer)
        {
            await RefreshWeatherData();
        }

        private void StartWeatherRefresher()
        {
            ThreadPoolTimer.CreatePeriodicTimer(WeatherTimer_Tick, TimeSpan.FromMilliseconds(WeatherTickIntervalMs));
        }

        private async Task GetCalendar()
        {
            try
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                {
                    var queryOptions = new List<QueryOption>()
                    {
                        new QueryOption("startDateTime", DateTime.Today.ToString("s")),
                        new QueryOption("endDateTime", DateTime.Today.AddDays(7).ToString("s"))
                    };
                    var calResponse = await
                        _graphClient.Me.Calendars[_calendarIdentifier].CalendarView.Request(queryOptions)
                            .Select(e => new { e.Subject, e.Start, e.End, e.Sensitivity, e.IsCancelled})
                            .Top(999)
                            .GetAsync();

                    await RefreshCalendar(calResponse.Select(x => new Appointment
                    {
                        Subject = x.Subject,
                        StartTime = x.Start.ToDateTime(),
                        EndTime = x.End.ToDateTime(),
                        IsPrivate =
                            x.Sensitivity == Sensitivity.Private || x.Sensitivity == Sensitivity.Confidential
                    }).ToList());
                });
            }
            catch (MsalException msalEx)
            {
                await DisplayMessageAsync($"Error Acquiring Token:{Environment.NewLine}{msalEx}");
            }
            catch (Exception ex)
            {
                await DisplayMessageAsync($"Error Acquiring Token Silently:{Environment.NewLine}{ex}");
            }
        }

        private async Task DisplayMessageAsync(string message)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                () => { SpecialNote.Text = message; });
        }

        private async Task<GraphServiceClient> SignInAndInitializeGraphServiceClient(string[] scopes)
        {
            new GraphServiceClient(new HttpClient());
            var graphClient = new GraphServiceClient(MsGraphUrl,
                new DelegateAuthenticationProvider(async (requestMessage) =>
                {
                    requestMessage.Headers.Authorization =
                        new AuthenticationHeaderValue("bearer", await SignInUserAndGetTokenUsingMsal(scopes));
                }));

            return await Task.FromResult(graphClient);
        }

        private async Task<string> SignInUserAndGetTokenUsingMsal(string[] scopes)
        {
            _publicClientApp = PublicClientApplicationBuilder.Create(_clientId)
                .WithAuthority(Authority)
                .WithUseCorporateNetwork(false)
                .WithRedirectUri(DefaultRedirectUri.Value)
                .WithLogging((level, message, containsPii) => { Debug.WriteLine($"MSAL: {level} {message} "); },
                    LogLevel.Warning, false, true)
                .Build();
            
            var accounts = await _publicClientApp.GetAccountsAsync();
            var firstAccount = accounts.FirstOrDefault();

            try
            {
                _authResult = await _publicClientApp.AcquireTokenSilent(scopes, firstAccount)
                    .ExecuteAsync();
            }
            catch (MsalUiRequiredException ex)
            {
                Debug.WriteLine($"MsalUiRequiredException: {ex.Message}");

                _authResult = await _publicClientApp.AcquireTokenInteractive(scopes)
                    .ExecuteAsync()
                    .ConfigureAwait(false);
            }

            return _authResult.AccessToken;
        }

        private async Task RefreshCalendar(List<Appointment> allAppointments)
        {
            // We are displaying appointments for the next 7 days.
            var days = new Dictionary<DateTime, List<Appointment>>
            {
                {TimeManager.Today, new List<Appointment>()},
                {TimeManager.Today.AddDays(1), new List<Appointment>()},
                {TimeManager.Today.AddDays(2), new List<Appointment>()},
                {TimeManager.Today.AddDays(3), new List<Appointment>()},
                {TimeManager.Today.AddDays(4), new List<Appointment>()},
                {TimeManager.Today.AddDays(5), new List<Appointment>()},
                {TimeManager.Today.AddDays(6), new List<Appointment>()}
            };

            foreach (var appointment in allAppointments)
                if (days.ContainsKey(appointment.StartTime.Date))
                {
                    days[appointment.StartTime.Date].Add(appointment);
                }
                else
                {
                    //LogError("Appointment occurring on day that we can't display. Appointment StartDate=" +
                    //          appointment.StartTime);
                }

            await RunOnDispatch(() =>
            {
                for (var i = 0; i < days.Count; i++)
                {
                    var currentDay = TimeManager.Today.AddDays(i);
                    var appointmentsForCurrentDay = days[currentDay];
                    var heading = (TextBlock) FindName($"Day{i}Txb");
                    Style appointmentHourStyle = null;
                    Style appointmentEntryStyle = null;
                    if (heading == null)
                    {
                        //LogError("Unable to find the heading textblock for the date " + currentDay);
                    }
                    else
                    {
                        if (currentDay.Date == TimeManager.Today)
                        {
                            heading.Text = Strings.TodaysAgendaHeading;
                            appointmentHourStyle = (Style) Resources["SmallTextStyle"];
                            appointmentEntryStyle = (Style) Resources["AppointmentEntryStyleMedium"];
                        }
                        else if (currentDay.Date == TimeManager.Today.AddDays(1))
                        {
                            appointmentHourStyle = (Style) Resources["AppointmentHourStyle"];
                            appointmentEntryStyle = (Style) Resources["AppointmentEntryStyle"];
                            heading.Text = Strings.TomorrowHeading;
                        }
                        else
                        {
                            appointmentHourStyle = (Style) Resources["AppointmentHourStyle"];
                            appointmentEntryStyle = (Style) Resources["AppointmentEntryStyle"];
                            heading.Text = GetDayOfWeek(currentDay.DayOfWeek).ToLower();
                        }
                    }

                    // Set appointments
                    var daySp = (StackPanel) FindName($"Day{i}Sp");
                    if (daySp == null)
                    {
                        //LogError("Unable to find the calendar stack panel for the date " + currentDay);
                    }
                    else
                    {
                        daySp.Children.Clear();
                        var appointmentGrouping = appointmentsForCurrentDay
                            .GroupBy(a => a.StartTime.ToLocalTime().ToString(Strings.CalendarHourGroupByFormatString))
                            .OrderBy(ag => ag.Key);
                        if (!appointmentGrouping.Any())
                            daySp.Children.Add(new TextBlock
                            {
                                TextTrimming = TextTrimming.WordEllipsis,
                                Style = appointmentEntryStyle,
                                Text = Strings.NoAppointments
                            });
                        else
                            foreach (var ag in appointmentGrouping)
                            {
                                // Group by hour
                                var hourSp = new StackPanel();
                                var hourHeadning = ag.Key;
                                if (hourHeadning.Length < 3)
                                    hourHeadning = hourHeadning + ":00";
                                hourSp.Children.Add(new TextBlock
                                {
                                    Style = appointmentHourStyle,
                                    Text = hourHeadning
                                });

                                foreach (var appointment in ag)
                                {
                                    var entry = new TextBlock
                                    {
                                        TextTrimming = TextTrimming.WordEllipsis,
                                        Style = appointmentEntryStyle,
                                        Text = appointment?.Subject
                                    };
                                    hourSp.Children.Add(entry);
                                }

                                daySp.Children.Add(hourSp);
                            }
                    }
                }
            });
        }

        private async Task RefreshSpecialDayView()
        {
            await RunOnDispatch(() =>
            {
                var specials = _specialDayCalendar.GetSpecials(TimeManager.Today).ToList();
                if (specials.Any()) SpecialNote.Text = specials.First().DisplayText;
            });
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

        private async Task RefreshWeatherData()
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                var weather =
                    new WeatherService(ApplicationDataController.GetValue(KeyNames.OpenWeatherMapApiKey, string.Empty),
                        new CultureInfo(ApplicationLanguages.PrimaryLanguageOverride));
                WeatherData weatherData = null;
                try
                {
                    weatherData = await weather.GetWeatherDataForCity(
                            ApplicationDataController.GetValue(KeyNames.WeatherCityName, string.Empty));
                }
                catch (WeatherServiceException weatherServiceException)
                {
                    await DisplayMessageAsync(weatherServiceException.Message);
                }

                if (weatherData == null)
                {
                    await DisplayMessageAsync(Strings.UnableToConnectToWeatherService);
                }
                else
                {
                    WeatherIcon.Source = GetImageSourceFromUri(weatherData.WeatherIconUri.AbsolutePath);
                    LocationTxb.Text = Strings.Get(weatherData.Location);
                    if (string.IsNullOrEmpty(LocationTxb.Text))
                        LocationTxb.Text = weatherData.Location;
                    TemperatureTxb.Text = Math.Round(weatherData.Temp) + "°";
                    WeatherDescirptionTxb.Text = string.IsNullOrEmpty(weatherData.Description)
                        ? string.Empty
                        : weatherData.Description.ToLower();
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
                case "01d":
                    resName = "ClearDay";
                    break;
                case "01n":
                    resName = "ClearNight";
                    break;
                case "02d":
                    resName = "FewCloudsDay";
                    break;
                case "02n":
                    resName = "FewCloudsNight";
                    break;
                case "03d":
                    resName = "ScatteredCloudsDay";
                    break;
                case "03n":
                    resName = "ScatteredCloudsNight";
                    break;
                case "04d":
                    resName = "BrokenCloudsDay";
                    break;
                case "04n":
                    resName = "BrokenCloudsNight";
                    break;
                case "09d":
                    resName = "RainDay";
                    break;
                case "09n":
                    resName = "RainNight";
                    break;
                case "10d":
                    resName = "RainDay";
                    break;
                case "10n":
                    resName = "RainNight";
                    break;
                case "11d":
                    resName = "ThunderStormDay";
                    break;
                case "11n":
                    resName = "ThunderStormNight";
                    break;
                case "13d":
                    resName = "SnowDay";
                    break;
                case "13n":
                    resName = "SnowNight";
                    break;
                case "50d":
                    resName = "MistDay";
                    break;
                case "50n":
                    resName = "MistNight";
                    break;
            }

            if (string.IsNullOrEmpty(resName))
                return null;
            return (ImageSource) Resources[resName];
        }

        private void StartClock()
        {
            ThreadPoolTimer.CreatePeriodicTimer(ClockTimer_Tick, TimeSpan.FromMilliseconds(ClockTickIntervalMs));
        }

        private async void ClockTimer_Tick(ThreadPoolTimer timer)
        {
            await SetTime();
        }

        private async void ClockBlinky_Tick(ThreadPoolTimer timer)
        {
            await ToggleClockBlinky();
        }

        private async Task ToggleClockBlinky()
        {
            await RunOnDispatch(() =>
            {
                if (string.IsNullOrEmpty(ClockSeparatorLabel.Text))
                    ClockSeparatorLabel.Text = ":";
                else ClockSeparatorLabel.Text = "";
            });
        }

        private void StartClockBlinky()
        {
            ThreadPoolTimer.CreatePeriodicTimer(ClockBlinky_Tick, TimeSpan.FromMilliseconds(ClockBlinkyTickInterval));
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

        private async Task RunOnDispatch(Action a)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => { a(); });
        }

        private static string GetDayOfWeek(DayOfWeek dayOfWeek)
        {
            return DateTimeFormatInfo.CurrentInfo?.GetDayName(dayOfWeek);
        }
    }
}