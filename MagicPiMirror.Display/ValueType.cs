using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace SystemOut.MagicPiMirror
{
    public class KeyNames
    {
        public const string
            SpecialNote = nameof(SpecialNote),
            ListNote = nameof(ListNote),
            SpecialNoteOn = nameof(SpecialNoteOn),
            DebugModeOn = nameof(DebugModeOn),
            ListNoteOn = nameof(ListNoteOn),
            ListNoteHeading = nameof(ListNoteHeading),
            LoadSettingsFromFile = nameof(LoadSettingsFromFile);
    }

    public class ApplicationDataController
    {
        public static T GetValue<T>(string key, T defaultValue)
        {
            object returnVal;
            var value = ApplicationData.Current.LocalSettings.Values[key];
            if (value == null)
                return defaultValue;
            if (typeof (T) == typeof (bool))
                returnVal = bool.Parse((string) value);
            else returnVal = value;
            return (T) returnVal;
        }

        public static void SetValue(string key, string value)
        {
            ApplicationData.Current.LocalSettings.Values[key] = value;
        }
    }
}
