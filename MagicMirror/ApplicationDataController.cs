using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;

namespace MagicMirror
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
            else if (typeof(T) == typeof(string[]))
                returnVal = (value as string)?.Split(',');
            else returnVal = value;
            return (T)returnVal;
        }

        public static void LoadDefaultSettings(string[] settingsRaw, bool overrideIfExists)
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

            foreach (var (key, value) in settings)
            {
                if (overrideIfExists || !ApplicationData.Current.LocalSettings.Values.Any(s => s.Key.Equals(key)))
                    ApplicationData.Current.LocalSettings.Values[key] = value;
            }
        }
    }
}
