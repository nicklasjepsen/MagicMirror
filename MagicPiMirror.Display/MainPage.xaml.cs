using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using Windows.Data.Xml.Dom;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.System.Threading;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

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
            LoadSettings();
            ListNoteHeading.Text = ApplicationDataController.GetValue(KeyNames.ListNoteHeading, "Notes");
            ListNoteContent.Text = ApplicationDataController.GetValue(KeyNames.ListNoteHeading, "Notes");
            SpecialNote.Text = ApplicationDataController.GetValue(KeyNames.ListNoteHeading, "Notes");
            if (ApplicationDataController.GetValue(KeyNames.DebugModeOn, false))
            {
                timeFormatString = "T";
            }
            else
                timeFormatString = "t";
            if (ApplicationDataController.GetValue(KeyNames.SpecialNoteOn, true))
            {
                SpecialNote.Visibility = Visibility.Visible;
            }
            else
                SpecialNote.Visibility = Visibility.Collapsed;
            UpdateListNoteViewVisibility();
            webserverEventProxy = WebServerEventProxy.Instance;
            webserverEventProxy.ValueChanged += WebserverEventProxy_ValueChanged;
            specialDayCalendar = new SpecialDayCalendar();
            ThreadPoolTimer.CreatePeriodicTimer(ClockTimer_Tick, TimeSpan.FromMilliseconds(1000));
            webServer = new MirrorWebServer();
        }

        private void LoadSettings()
        {
            var lines = File.ReadAllLines("DefaultSettings.txt");
            var settings = new Dictionary<string, string>();
            foreach (var line in lines)
            {
                if (string.IsNullOrEmpty(line))
                    continue;
                if (line.Contains("[") && line.Contains("]"))
                {
                    var key = line.Substring(0, line.IndexOf('['));
                    var value = line.Substring(line.IndexOf('[') + 1, line.IndexOf(']') - line.IndexOf('['));
                    settings.Add(key, value);
                }

                if (settings.ContainsKey(KeyNames.LoadSettingsFromFile))
                {
                    if (bool.Parse(settings[KeyNames.LoadSettingsFromFile]))
                    {
                        foreach (var setting in settings)
                        {
                            ApplicationDataController.SetValue(setting.Key, setting.Value);
                        }
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
                ClockLabel.Text = DateTime.Now.ToString(timeFormatString, new CultureInfo("da-dk"));
            });
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            await webServer.InitializeWebServer();
            var weather = new WeatherService();
            var temp = await weather.GetWeatherData();
            await RunOnDispatch(() => { SpecialNote.Text = temp + string.Empty; });
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            ClockLabel.Text = DateTime.Now.ToString("t", new CultureInfo("da-dk"));
            CalendarHeading.Text = GetDanishDayOfWeek();

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
