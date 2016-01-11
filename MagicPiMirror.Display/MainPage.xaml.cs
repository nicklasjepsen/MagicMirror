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
        private readonly WebServerEventProxy webserverEventProxy;

        public MainPage()
        {
            this.InitializeComponent();

            webserverEventProxy = WebServerEventProxy.Instance;
            webserverEventProxy.ValueChanged += WebserverEventProxy_ValueChanged;
            specialDayCalendar = new SpecialDayCalendar();
            ThreadPoolTimer.CreatePeriodicTimer(ClockTimer_Tick, TimeSpan.FromMilliseconds(100));
            webServer = new MirrorWebServer();
        }

        private async Task LoadSettings()
        {
            string[] lines = { };
            if (File.Exists("PrivateSettings.txt"))
                lines = File.ReadAllLines("PrivateSettings.txt");
            else File.ReadAllLines("DefaultSettings.txt");
            var settings = new Dictionary<string, string>();
            foreach (var line in lines)
            {
                if (string.IsNullOrEmpty(line))
                    continue;
                if (line.Contains("[") && line.Contains("]"))
                {
                    var key = line.Substring(0, line.IndexOf('[')).Trim();
                    var value = line.Substring(line.IndexOf('[') + 1, line.IndexOf(']') - line.IndexOf('[') - 1);
                    settings.Add(key, value);
                }

            }
            if (settings.ContainsKey(KeyNames.LoadSettingsFromFile))
            {
                if (bool.Parse(settings[KeyNames.LoadSettingsFromFile]))
                {
                    await ApplicationData.Current.ClearAsync();
                    foreach (var setting in settings)
                    {
                        ApplicationDataController.SetValue(setting.Key, setting.Value);
                    }
                }
            }
        }

        private async void WebserverEventProxy_ValueChanged(object sender, ValueChangedEventArg e)
        {
            switch (e.Key)
            {
                case KeyNames.SpecialNote:
                    await RunOnDispatch(() =>
                    {
                        SpecialNote.Text = e.Value;
                    });
                    break;
                case KeyNames.ListNote:
                    await RunOnDispatch(() =>
                    {
                        ListNoteContent.Text = e.Value;
                    });
                    break;
                case KeyNames.SpecialNoteOn:
                    await RunOnDispatch(() =>
                    {
                        SpecialNote.Visibility = ApplicationDataController.GetValue(KeyNames.SpecialNoteOn, false) ?
                        Visibility.Collapsed :
                        Visibility.Visible;
                    }); break;
                case KeyNames.DebugModeOn:
                    SetTimeStampFormat();
                    break;
                case KeyNames.ListNoteOn:
                    await RunOnDispatch(UpdateListNoteViewVisibility); break;
                case KeyNames.ListNoteHeading:
                    await RunOnDispatch(() =>
                    {
                        ListNoteHeading.Text = e.Value;
                    });
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void UpdateListNoteViewVisibility()
        {
            ListNoteHeading.Visibility = ApplicationDataController.GetValue(KeyNames.ListNoteOn, false)
                ? Visibility.Visible
                : Visibility.Collapsed;
            ListNoteContent.Visibility = ApplicationDataController.GetValue(KeyNames.ListNoteOn, false)
                ? Visibility.Visible
                : Visibility.Collapsed;
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

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadSettings();
            if (ApplicationDataController.GetValue(KeyNames.DebugModeOn, false))
            {
                timeFormatString = "T";
            }
            else
                timeFormatString = "t";
            await SetTime();
            ListNoteHeading.Text = ApplicationDataController.GetValue(KeyNames.ListNoteHeading, "ListNoteHeadning");
            ListNoteContent.Text = ApplicationDataController.GetValue(KeyNames.ListNoteHeading, "ListNoteContent");
            SpecialNote.Text = ApplicationDataController.GetValue(KeyNames.SpecialNote, "SpecialNote");
            if (ApplicationDataController.GetValue(KeyNames.SpecialNoteOn, true))
            {
                SpecialNote.Visibility = Visibility.Visible;
            }
            else
                SpecialNote.Visibility = Visibility.Collapsed;
            UpdateListNoteViewVisibility();
            await webServer.InitializeWebServer();
            var weather = new WeatherService();
            var weatherData = await weather.GetWeatherData();
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

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            var specials = specialDayCalendar.GetSpecials(DateTime.Today);
            if (specials.Any())
            {
                SpecialNote.Text = specials.First().DisplayText;
            }

            base.OnNavigatedTo(e);
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
