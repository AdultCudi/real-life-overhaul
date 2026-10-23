using System.Collections.Generic;
using Extensions;
using Localizor;
using UI.Notification;

namespace RealLifeOverhaul.Utilities
{
    /// <summary>The utilities' words: money as the game writes it, texts with their names filled, a label and value, a notice.</summary>
    public static class UtilitiesText
    {
        public static string Money(float amount)
        {
            return amount.ToCurrencyFormat();
        }

        /// <summary>The game's culture, for counts written as the game writes them (12,500).</summary>
        public static System.Globalization.CultureInfo Culture => CultureHelper.CultureInfo ?? System.Globalization.CultureInfo.InvariantCulture;

        /// <summary>Thousandths as a percentage (231 as 23.1).</summary>
        public static string Percent(int thousandths)
        {
            return (thousandths / 10f).ToString("0.#", Culture);
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
            // the game localises by key: a line already built goes through a key whose text is only {text}
            Notifications.Show(type, "reallife_util_toast", new Dictionary<string, string>(1) { { "text", text } }, seconds);
        }

        public static string Service(Service service)
        {
            return T(service == Utilities.Service.Power ? "reallife_util_power" : service == Utilities.Service.Water ? "reallife_util_water" : "reallife_util_internet");
        }

        /// <summary>Who supplies an account: the city's for power and water, its own provider for the internet.</summary>
        public static string ProviderOf(Account a)
        {
            if (a.Service == Utilities.Service.Internet)
            {
                Provider p = InternetRules.Get(a.Provider);
                return p != null ? T(p.NameKey) : T("reallife_net_none");
            }
            return T(a.Service == Utilities.Service.Power ? "reallife_util_provider_power" : "reallife_util_provider_water");
        }

        /// <summary>The providers for the Guide: each by name and kind, and where it reaches if not everywhere in the city.</summary>
        public static string Providers()
        {
            var text = new System.Text.StringBuilder(200);
            for (int i = 0; i < InternetCatalogue.Providers.Length; i++)
            {
                Provider p = InternetCatalogue.Providers[i];
                text.Append(i == 0 ? string.Empty : i == InternetCatalogue.Providers.Length - 1 ? T("reallife_net_list_last") : ", ");
                text.Append(T(p.NameKey)).Append(" (").Append(T(p.KindKey));
                if (p.Districts.Length < 6)
                {
                    text.Append(", ");
                    for (int d = 0; d < p.Districts.Length; d++)
                    {
                        text.Append(d == 0 ? string.Empty : d == p.Districts.Length - 1 ? T("reallife_net_list_last") : ", ").Append(p.Districts[d].GetLocalization());
                    }
                }
                text.Append(')');
            }
            return text.ToString();
        }

        /// <summary>An hour of the day as the game writes it (10:00).</summary>
        public static string Hour(int hour)
        {
            return (((hour % 24) + 24) % 24).ToString("00", System.Globalization.CultureInfo.InvariantCulture) + ":00";
        }

        public static string Day(int day)
        {
            return T("reallife_util_day", "day", day.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

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
