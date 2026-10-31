using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Extensions;
using Localizor;
using UI.Notification;

namespace RealLifeOverhaul.Population
{
    /// <summary>The population's words in the game's language: its keys, money as the game writes it, an address, and a screen's view model as one line for the log.</summary>
    internal static class PopulationText
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static string T(string key)
        {
            return string.IsNullOrEmpty(key) ? string.Empty : key.GetLocalization();
        }

        /// <summary>Whole money as the game writes it.</summary>
        public static string Money(long amount)
        {
            return ((float)amount).ToShortCurrencyFormat();
        }

        /// <summary>A building's address as the game writes it: its street's name and its number.</summary>
        public static string Address(PopulationState s, int b)
        {
            if (b < 0 || b >= s.Buildings.Count)
            {
                return string.Empty;
            }
            CityBuilding cb = s.Buildings[b];
            string street = LocalizorManager.IsLocalizedKey(cb.Street) ? cb.Street.GetLocalization() : cb.Street;
            return cb.Number.ToString(Inv) + " " + street;
        }

        /// <summary>A notice through the game's own notifications; a new dictionary each time (the game keeps it while the notice is listed).</summary>
        public static void Toast(NotificationType type, string key, Dictionary<string, string> data)
        {
            Notifications.Show(type, key, data, 10f);
        }

        /// <summary>A screen's view model as one line for the log, in English: its title, figures, rows and action.</summary>
        public static string Describe(PopulationView v)
        {
            var t = new StringBuilder(256);
            t.Append(v.Kind).Append(' ').Append(v.TitleKey ? v.Title : v.Title ?? v.NameSeed.ToString(Inv)).Append(':');
            foreach (ViewFigure f in v.Hero)
            {
                t.Append(' ').Append(Short(f.Label)).Append(' ').Append(f.Format == ViewFormat.Key ? f.Text : f.Value.ToString(Inv)).Append(';');
            }
            t.Append(' ').Append(v.Rows.Count).Append(" row(s)");
            foreach (ViewFigure f in v.Details)
            {
                t.Append("; ").Append(Short(f.Label)).Append(' ').Append(f.Format == ViewFormat.Key ? f.Text : f.Value.ToString(Inv));
            }
            if (v.Primary != null)
            {
                t.Append("; action ").Append(Short(v.Primary)).Append(" to ").Append(v.PrimaryTarget.Kind).Append(' ').Append(v.PrimaryTarget.Index);
            }
            return t.ToString();
        }

        // A key without its first three parts (the mod's, the population's and the kind): "moved_in" of a figure's.
        private static string Short(string key)
        {
            int at = 0;
            for (int i = 0; i < 3 && key != null; i++)
            {
                int next = key.IndexOf('_', at);
                at = next < 0 ? at : next + 1;
            }
            return key == null ? string.Empty : key.Substring(at);
        }
    }
}
