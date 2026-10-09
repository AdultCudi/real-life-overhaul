using System.Collections.Generic;
using Extensions;
using Localizor;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Text helpers for travel: a line from en.json with its {names} filled in. Every call
    /// builds a string, so these are for when something changes (a button press, an hour
    /// turning), never per frame.
    /// </summary>
    public static class TravelText
    {
        private static string _keyLabel;
        private static int _keyVersion = -1;

        public static string T(string key)
        {
            return key.GetLocalization();
        }

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

        /// <summary>A line that names the interaction key: {key} is whatever the player has bound.</summary>
        public static string WithKey(string key)
        {
            if (_keyVersion != Keys.Version || _keyLabel == null)
            {
                _keyVersion = Keys.Version;
                _keyLabel = Keys.Label(KeyAction.Interact);
            }
            return key.GetLocalization().Replace("{key}", _keyLabel);
        }

        public static string Money(float amount)
        {
            return amount.ToCurrencyFormat();
        }

        public static string Number(int value)
        {
            return value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>The hour as the game writes times of day.</summary>
        public static string Hour(int hour)
        {
            return (hour % 24).GetFormattedTime(0f);
        }

        public static string Pick(string[] keys)
        {
            return keys[Random.Range(0, keys.Length)];
        }

        public static void Toast(NotificationType type, string text, float seconds = 8f)
        {
            // The game localises what it is given by key; a line already built is passed through a key whose text is only {text}.
            var data = new Dictionary<string, string>(1) { { "text", text } };
            Notifications.Show(type, "reallife_travel_toast", data, seconds);
        }
    }
}
