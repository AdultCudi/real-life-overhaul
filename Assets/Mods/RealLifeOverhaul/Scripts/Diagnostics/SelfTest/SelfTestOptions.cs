using System.Collections.Generic;
using System.Globalization;
using RealLifeOverhaul.Meta;
using UnityEngine;

namespace RealLifeOverhaul.SelfTest
{
    /// <summary>
    /// The mod's options a plan changed, with what each was, so the plan's end puts them back and a developer's own settings
    /// are never left changed. Goes through Meta/SettingsTable as the presets do. COST: a few PlayerPrefs writes on a step.
    /// </summary>
    internal static class SelfTestOptions
    {
        private static readonly Dictionary<string, int> Original = new Dictionary<string, int>();

        public static bool Set(string id, string text, out string words)
        {
            OptionInfo info = SettingsTable.Get(id);
            if (info == null)
            {
                words = "no option has the id '" + id + "'";
                return false;
            }
            if (!Read(text, out int value))
            {
                words = "'" + text + "' is not a value (use 1 or 0, true or false, or a number)";
                return false;
            }
            if (!Original.ContainsKey(id))
            {
                Original[id] = SettingsTable.Current(info);
            }
            int now = SettingsTable.Clamp(info, value);
            SettingsTable.Write(info, now);
            UnityEngine.PlayerPrefs.Save();
            Settings.Reload();
            AppliedDev(id);
            words = id + " is " + SettingsTable.Current(info) + " (it was " + Original[id] + ")";
            return SettingsTable.Current(info) == now;
        }

        // The diagnostics switch changes what the log gets, so the mod's own reading of it is brought in line as the options page does.
        private static void AppliedDev(string id)
        {
            if (id == Settings.DevDiagnosticsId)
            {
                Meta.Dev.Apply();
            }
        }

        public static bool Value(string id, out int value)
        {
            OptionInfo info = SettingsTable.Get(id);
            value = info != null ? SettingsTable.Current(info) : 0;
            return info != null;
        }

        public static bool Read(string text, out int value)
        {
            value = 0;
            if (text == "true" || text == "on")
            {
                value = 1;
                return true;
            }
            if (text == "false" || text == "off")
            {
                return true;
            }
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        /// <summary>Every option changed is written back as it was; the count put back.</summary>
        public static int RestoreAll()
        {
            int n = 0;
            foreach (KeyValuePair<string, int> pair in Original)
            {
                OptionInfo info = SettingsTable.Get(pair.Key);
                if (info != null)
                {
                    SettingsTable.Write(info, pair.Value);
                    n++;
                }
            }
            Original.Clear();
            if (n > 0)
            {
                UnityEngine.PlayerPrefs.Save();
                Settings.Reload();
                Meta.Dev.Apply();
            }
            return n;
        }
    }
}
