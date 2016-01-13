using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Storage;

namespace SystemOut.MagicPiMirror
{
    public class ApplicationDataController
    {
        public static T GetValue<T>(string key, T defaultValue)
        {
            object returnVal;
            var value = ApplicationData.Current.LocalSettings.Values[key];
            if (value == null)
                return defaultValue;
            if (typeof(T) == typeof(bool))
                returnVal = bool.Parse((string)value);
            else if (typeof (T) == typeof (string[]))
                returnVal = ((string) value).Split(',');
            else returnVal = value;
            return (T)returnVal;
        }

        public static void SetValue(string key, string value)
        {
            ApplicationData.Current.LocalSettings.Values[key] = value;
        }

        public static async Task LoadDefaultSettings(string[] settingsRaw)
        {
            var settings = new Dictionary<string, string>();
            foreach (var line in settingsRaw)
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

            await ApplicationData.Current.ClearAsync();
            foreach (var setting in settings)
            {
                SetValue(setting.Key, setting.Value);
            }
        }
    }
}