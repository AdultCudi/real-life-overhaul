using System;
using System.Collections.Generic;
using Extensions;
using Helpers;
using Localizor;
using UI.Notification;

namespace RealLifeOverhaul
{
    /// <summary>Texts and toasts for civic life: every string comes from the locales, and {names} are filled here.</summary>
    public static class CivicText
    {
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
            return key.GetLocalization().Replace("{" + name + "}", value).Replace("{" + name2 + "}", value2).Replace("{" + name3 + "}", value3);
        }

        public static string Money(float amount)
        {
            return amount.ToCurrencyFormat();
        }

        /// <summary>What a policy does, in words, with its amounts at today's prices.</summary>
        public static string Effect(PolicyDef policy)
        {
            if (policy == null)
            {
                return string.Empty;
            }
            string text = T(policy.Effect);
            if (text.IndexOf('{') < 0)
            {
                return text;
            }
            return text.Replace("{levy}", Money(Math.Abs(CivicCatalogue.LevyNow(policy))))
                .Replace("{grant}", Money(Math.Abs(CivicCatalogue.GrantNow(policy.Dir))))
                .Replace("{ticket}", Money(Math.Abs(CivicCatalogue.TicketExtraNow(policy.Dir))))
                .Replace("{fare}", Money(Math.Abs(CivicCatalogue.FareExtraNow(policy.Dir))))
                .Replace("{floor}", Money(CivicCatalogue.WageFloorNow(policy.Dir)));
        }

        public static string Name(Candidate candidate)
        {
            if (candidate.Player)
            {
                return PlayerName();
            }
            return CivicCatalogue.FirstNames[candidate.First].GetLocalization() + " " + CivicCatalogue.LastNames[candidate.Last].GetLocalization();
        }

        /// <summary>The player's own name, as the game has it; "you" if it has none.</summary>
        public static string PlayerName()
        {
            try
            {
                CharacterData data = PlayerHelper.CharacterData;
                if (data != null && !string.IsNullOrEmpty(data.name))
                {
                    return data.name;
                }
            }
            catch (Exception)
            {
                // the player's character is not there yet: the word below
            }
            return T("reallife_civic_office_you");
        }

        public static string Party(Candidate candidate)
        {
            return CivicCatalogue.PartyKey(candidate.Party).GetLocalization();
        }

        /// <summary>Set while the host of a Going Public session runs the city's hours: every city toast is heard too, to be shown to the guests (Core/SharedCity, part N).</summary>
        public static Action<NotificationType, string, string[], float> Heard;

        /// <summary>
        /// A toast with no names in it. Not kept in the save: the key would mean nothing without the mod. Every city toast
        /// is held for the morning line while the player sleeps (Core/MorningNews): the city's hours catch up at the wake.
        /// </summary>
        public static void Toast(NotificationType type, string key, float seconds = 9f)
        {
            Heard?.Invoke(type, key, null, seconds);
            MorningNews.Show(type, key, null, seconds, null, false);
        }

        public static void Toast(NotificationType type, string key, string name, string value, float seconds = 9f)
        {
            if (Heard != null)
            {
                Heard(type, key, new[] { name, value }, seconds);
            }
            // A new dictionary each time: the game keeps hold of it for as long as the toast is listed.
            var data = new Dictionary<string, string>(1) { { name, value } };
            MorningNews.Show(type, key, data, seconds, null, false);
        }

        public static void Toast(NotificationType type, string key, string name, string value, string name2, string value2, float seconds = 9f)
        {
            if (Heard != null)
            {
                Heard(type, key, new[] { name, value, name2, value2 }, seconds);
            }
            var data = new Dictionary<string, string>(2) { { name, value }, { name2, value2 } };
            MorningNews.Show(type, key, data, seconds, null, false);
        }

        public static void Toast(NotificationType type, string key, string name, string value, string name2, string value2, string name3, string value3, float seconds = 9f)
        {
            if (Heard != null)
            {
                Heard(type, key, new[] { name, value, name2, value2, name3, value3 }, seconds);
            }
            var data = new Dictionary<string, string>(3) { { name, value }, { name2, value2 }, { name3, value3 } };
            MorningNews.Show(type, key, data, seconds, null, false);
        }

        /// <summary>A toast of the host's, shown on a guest as the host's player saw it (names and values in pairs); held like the guest's own.</summary>
        public static void Show(NotificationType type, string key, string[] pairs, float seconds)
        {
            Dictionary<string, string> data = null;
            if (pairs != null && pairs.Length > 1)
            {
                data = new Dictionary<string, string>(pairs.Length / 2);
                for (int i = 0; i + 1 < pairs.Length; i += 2)
                {
                    data[pairs[i]] = pairs[i + 1] ?? string.Empty;
                }
            }
            MorningNews.Show(type, key, data, seconds, null, false);
        }
    }
}
