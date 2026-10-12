using System.Collections.Generic;
using Extensions;
using Localizor;
using UI.Notification;

namespace RealLifeOverhaul.Utilities
{
    /// <summary>The utilities' words: money as the game writes it, a text with its names filled, a label and its value, a toast.</summary>
    public static class UtilitiesText
    {
        public static string Money(float amount)
        {
            return amount.ToCurrencyFormat();
        }

        public static string T(string key)
        {
            return key.GetLocalization();
        }

        /// <summary>A text with its names filled: pairs of a name and what goes in its place.</summary>
        public static string T(string key, params string[] pairs)
        {
            string text = key.GetLocalization();
            for (int i = 0; i + 1 < pairs.Length; i += 2)
            {
                text = text.Replace("{" + pairs[i] + "}", pairs[i + 1] ?? string.Empty);
            }
            return text;
        }

        /// <summary>A label and its value, as the game's white box writes them.</summary>
        public static string Pair(string labelKey, string value)
        {
            return T(labelKey) + " <b>" + value + "</b>";
        }

        public static void Toast(NotificationType type, string text, float seconds = 8f)
        {
            // The game localises what it is given by key; a line already built goes through a key whose text is only {text}.
            Notifications.Show(type, "reallife_util_toast", new Dictionary<string, string>(1) { { "text", text } }, seconds);
        }

        public static string Service(Service service)
        {
            return T(service == Utilities.Service.Power ? "reallife_util_power" : "reallife_util_water");
        }

        public static string Provider(Service service)
        {
            return T(service == Utilities.Service.Power ? "reallife_util_provider_power" : "reallife_util_provider_water");
        }

        public static string Day(int day)
        {
            return T("reallife_util_day", "day", day.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        /// <summary>Where an account stands, in a few words.</summary>
        public static string State(Account a, int day)
        {
            if (a == null)
            {
                return T("reallife_util_state_none");
            }
            switch (a.State)
            {
                case SupplyState.Pending:
                    return T("reallife_util_state_pending", "day", Day(a.ConnectDay));
                case SupplyState.Temporary:
                    return T("reallife_util_state_temporary", "day", Day(a.TemporaryUntil));
                case SupplyState.Cut:
                    return T("reallife_util_state_cut");
                case SupplyState.Closed:
                    return T("reallife_util_state_closed");
                default:
                    return a.Owed > 0 && a.DueDay > 0 && day > a.DueDay ? T("reallife_util_state_late") : T("reallife_util_state_active");
            }
        }
    }
}
