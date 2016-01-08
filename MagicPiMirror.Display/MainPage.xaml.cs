using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
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
        private ThreadPoolTimer _clockTimer = null;
        private MirrorWebServer webServer;
        private string TimeFormatString = "t";
        private WebServerEventProxy webserverEventProxy;

        public MainPage()
        {
            this.InitializeComponent();
            webserverEventProxy = WebServerEventProxy.Instance;
            webserverEventProxy.ValueChanged += WebserverEventProxy_ValueChanged;
            specialDayCalendar = new SpecialDayCalendar();
            _clockTimer = ThreadPoolTimer.CreatePeriodicTimer(_clockTimer_Tick, TimeSpan.FromMilliseconds(1000));
            webServer = new MirrorWebServer();
            SpecialNote.Text = ApplicationDataController.GetValue(ValueType.SpecialNote, "");
            SetTimeStampFormat();
        }

        private async void WebserverEventProxy_ValueChanged(object sender, ValueChangedEventArg e)
        {
            switch (e.Key)
            {
                case ValueType.SpecialNote:
                    await RunOnDispatch(() =>
                    {
                        SpecialNote.Text = e.Value;
                    });
                    break;
                case ValueType.ListNote:
                    await RunOnDispatch(() =>
                    {
                        NotesContent.Text = e.Value;
                    });
                    break;
                case ValueType.SpecialNoteOn:
                    await RunOnDispatch(() =>
                    {
                        SpecialNote.Visibility = ApplicationDataController.GetValue(ValueType.SpecialNoteOn, false) ?
                        Visibility.Collapsed :
                        Visibility.Visible;
                    }); break;
                case ValueType.DebugModeOn:
                    SetTimeStampFormat();
                    break;
                case ValueType.ListNoteOn:
                    await RunOnDispatch(() =>
                    {
                        NotestHeading.Visibility = ApplicationDataController.GetValue(ValueType.ListNoteOn, false)
                            ? Visibility.Visible
                            : Visibility.Collapsed;
                        NotesContent.Visibility = ApplicationDataController.GetValue(ValueType.ListNoteOn, false)
                            ? Visibility.Visible
                            : Visibility.Collapsed;
                    }); break;
                case ValueType.ListNoteHeading:
                    await RunOnDispatch(() =>
                    {
                        NotestHeading.Text = e.Value;
                    });
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private async void SetTimeStampFormat()
        {
            if (ApplicationDataController.GetValue(ValueType.DebugModeOn, false))
                TimeFormatString = "T";
            else TimeFormatString = "t";
            await SetTime();
        }

        private async void _clockTimer_Tick(ThreadPoolTimer timer)
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
                ClockLabel.Text = DateTime.Now.ToString(TimeFormatString, new CultureInfo("da-dk"));
            });
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            await webServer.InitializeWebServer();
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
