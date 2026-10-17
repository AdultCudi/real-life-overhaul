using System.Collections.Generic;
using Extensions;
using Localizor;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>Travel's locale lines with their {names} filled in. Every call builds a string, so never per frame.</summary>
    public static class TravelText
    {
        public static string T(string key) => key.GetLocalization();

        public static string T(string key, string name, string value)
        {
            return key.GetLocalization().Replace("{" + name + "}", value);
        }

        public static string T(string key, string name, string value, string name2, string value2)
        {
            return key.GetLocalization().Replace("{" + name + "}", value).Replace("{" + name2 + "}", value2);
        }

        public static string T(string key, string name, string value, string name2, string value2, string name3, string value3)
        {
            return T(key, name, value, name2, value2).Replace("{" + name3 + "}", value3);
        }

        public static string T(string key, string name, string value, string name2, string value2, string name3, string value3, string name4, string value4)
        {
            return T(key, name, value, name2, value2, name3, value3).Replace("{" + name4 + "}", value4);
        }

        public static string Money(float amount) => amount.ToCurrencyFormat();

        public static string Number(int value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>The hour as the game writes times of day.</summary>
        public static string Hour(int hour) => (hour % 24).GetFormattedTime(0f);

        public static string Pick(string[] keys) => keys[Random.Range(0, keys.Length)];

        public static void Toast(NotificationType type, string text, float seconds = 8f)
        {
            // The game localises by key: a line already built goes through a key whose text is only {text}.
            var data = new Dictionary<string, string>(1) { { "text", text } };
            Notifications.Show(type, "reallife_travel_toast", data, seconds);
        }
    }
}
