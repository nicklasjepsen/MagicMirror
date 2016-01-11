using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
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
        private readonly SpecialDayCalendar specialDayCalendar;
        private readonly MirrorWebServer webServer;
        private string timeFormatString;

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
            await ApplicationDataController.LoadDefaultSettings(File.ReadAllLines("DefaultSettings.txt"));
#endif
            RefreshDebugMode();
            await RefreshUiControls();

            StartClock();
            await webServer.InitializeWebServer();
            await StartWeatherService();

            var calendarService =
                new CalendarService(ApplicationDataController.GetValue(KeyNames.CalendarServiceUrl, string.Empty));
            var calendar = await calendarService.GetCalendar(ApplicationDataController.GetValue(KeyNames.OneCalendar, string.Empty));
            ApplicationDataController.SetValue(KeyNames.SpecialNote, calendar.Appointments.First().Subject);
            ApplicationDataController.SetValue(KeyNames.SpecialNoteOn, "True");
            await RefreshSpecialNote();
            await RefreshSpecialNoteVisible();
        }

        private async Task RefreshUiControls()
        {
            await SetTime();
            await RefreshSpecialDayView();
            await RefreshListNoteVisibility();
            await RefreshListNoteHeading();
            await RefreshListNote();
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
            ThreadPoolTimer.CreatePeriodicTimer(ClockTimer_Tick, TimeSpan.FromMilliseconds(100));
        }

        private async Task StartWeatherService()
        {
            var weather = new WeatherService(ApplicationDataController.GetValue(KeyNames.OpenWeatherMapApiKey, string.Empty));
            //var weatherData = await weather.GetWeatherDataForCity(ApplicationDataController.GetValue(KeyNames.WeatherZip, string.Empty), ApplicationDataController.GetValue(KeyNames.WeatherCountry, string.Empty));
            var weatherData = await weather.GetWeatherDataForCity(ApplicationDataController.GetValue(KeyNames.WeatherCityName, string.Empty));

            if (weatherData == null)
            {
                var messageDialog = new MessageDialog("Unable to connect to the Weather Service.");
                await messageDialog.ShowAsync();
            }
            else
            {
                await RunOnDispatch(() =>
                {
                    //WeatherIcon.Source = new BitmapImage(weatherData.WeatherIconUri);
                    LocationTxb.Text = weatherData.Location;
                    TemperatureTxb.Text = Math.Round(weatherData.Temp) + "°";
                    WeatherDescirptionTxb.Text = weatherData.Description;
                });
            }
        }

        private void RefreshDebugMode()
        {
            SetTimeStampFormat();
        }

        private async void WebserverEventProxy_ValueChanged(object sender, ValueChangedEventArg e)
        {
            switch (e.Key)
            {
                case KeyNames.SpecialNote:
                    await RefreshSpecialNote(); break;
                case KeyNames.ListNote:
                    await RefreshListNote(); break;
                case KeyNames.SpecialNoteOn:
                    await RefreshSpecialNoteVisible(); break;
                case KeyNames.DebugModeOn:
                    SetTimeStampFormat(); break;
                case KeyNames.ListNoteOn:
                    await RefreshListNoteVisibility(); break;
                case KeyNames.ListNoteHeading:
                    await RefreshListNoteHeading(); break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private async Task RefreshListNoteHeading()
        {
            await RunOnDispatch(() =>
            {
                ListNoteHeading.Text = ApplicationDataController.GetValue(KeyNames.ListNoteHeading, string.Empty);
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

        private async Task RefreshListNote()
        {
            await RunOnDispatch(() =>
            {
                ListNoteContent.Text = ApplicationDataController.GetValue(KeyNames.ListNote, string.Empty);
            });
        }

        private async Task RefreshSpecialNote()
        {
            await RunOnDispatch(() =>
            {
                SpecialNote.Text = ApplicationDataController.GetValue(KeyNames.SpecialNote, string.Empty);
            });
        }

        private async Task RefreshListNoteVisibility()
        {
            await RunOnDispatch(() =>
            {
                ListNoteHeading.Visibility = ApplicationDataController.GetValue(KeyNames.ListNoteOn, false)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
                ListNoteContent.Visibility = ApplicationDataController.GetValue(KeyNames.ListNoteOn, false)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            });
        }

        private async void SetTimeStampFormat()
        {
            if (ApplicationDataController.GetValue(KeyNames.DebugModeOn, false))
                timeFormatString = "T";
            else timeFormatString = "t";
            await SetTime();
        }

        private async void ClockTimer_Tick(ThreadPoolTimer timer)
        {
            await SetTime();
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
                ClockLabel.Text = DateTime.Now.ToString(timeFormatString, new CultureInfo("da-dk"));
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
