using System.Collections.Generic;
using Entities;
using Extensions;
using Helpers;
using Localizor;
using Localizor.LanguageChangeEvent;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Family
{
    /// <summary>What a line of family text may be filled with. Every text uses the same few names, so one set does for all.</summary>
    public struct Args
    {
        public string Name;      // {name}: the partner, unless said otherwise
        public string Child;     // {child}
        public string Amount;    // {amount}: money, already formatted
        public string Place;     // {place}
        public string Day;       // {day}
        public string Text;      // {text}: a line built elsewhere
        public string N;         // {n}: a number

        /// <summary>With the partner's name filled in.</summary>
        public static Args Us => new Args { Name = Partner.Name };

        public Args Kid(string child) { Child = child; return this; }
        public Args Cost(int amount) { Amount = FamilyText.Money(amount); return this; }
        public Args At(string place) { Place = place; return this; }
        public Args On(string day) { Day = day; return this; }
        public Args With(string text) { Text = text; return this; }
        public Args Count(int n) { N = n.ToString(); return this; }
    }

    /// <summary>A key and what fills it, as a line for a toast, a phone message, a conversation or the HUD. Called on events only.</summary>
    public static class FamilyText
    {
        public static string Money(int amount)
        {
            return ((float)amount).ToCurrencyFormat();
        }

        public static string T(string key)
        {
            return JobKeys.Fill(key.GetLocalization());
        }

        public static string T(string key, in Args a)
        {
            return Fill(T(key), a);
        }

        public static string Fill(string text, in Args a)
        {
            if (text == null || text.IndexOf('{') < 0)
            {
                return text;
            }
            return text.Replace("{name}", a.Name ?? string.Empty).Replace("{child}", a.Child ?? string.Empty)
                .Replace("{amount}", a.Amount ?? string.Empty).Replace("{place}", a.Place ?? string.Empty)
                .Replace("{day}", a.Day ?? string.Empty).Replace("{text}", a.Text ?? string.Empty).Replace("{n}", a.N ?? string.Empty);
        }

        /// <summary>The same as the text of a conversation entry.</summary>
        public static LanguageChangeEventDataHolder Say(string key, in Args a)
        {
            return key.Localize(new
            {
                name = a.Name ?? string.Empty,
                child = a.Child ?? string.Empty,
                amount = a.Amount ?? string.Empty,
                place = a.Place ?? string.Empty,
                day = a.Day ?? string.Empty,
                text = a.Text ?? string.Empty,
                n = a.N ?? string.Empty,
                key = JobKeys.Label
            });
        }

        public static LanguageChangeEventDataHolder Say(string key)
        {
            return Say(key, Args.Us);
        }

        /// <summary>A line built from several: shown through the one key whose text is just {text}.</summary>
        public static LanguageChangeEventDataHolder Built(string text)
        {
            return "reallife_family_text".Localize(new { text });
        }

        // A new dictionary each time: the game keeps hold of it for as long as the toast or the message is listed.
        public static Dictionary<string, string> Data(in Args a)
        {
            return new Dictionary<string, string>(7)
            {
                { "name", a.Name ?? string.Empty }, { "child", a.Child ?? string.Empty }, { "amount", a.Amount ?? string.Empty },
                { "place", a.Place ?? string.Empty }, { "day", a.Day ?? string.Empty }, { "text", a.Text ?? string.Empty }, { "n", a.N ?? string.Empty }
            };
        }

        public static void Toast(NotificationType type, string key, in Args a, float seconds = 9f)
        {
            Notifications.Show(type, key, Data(a), seconds);
        }


        public static void Good(string key, in Args a)
        {
            Notifications.Show(NotificationType.Success, key, Data(a), 9f);
        }

        /// <summary>
        /// Set by RLO Texts (Wiring/TextsPhone): given who a text is from, its key, its values and a way to send it, true when it
        /// holds the text for the model's words and sends it later; null or false and it goes now, as written.
        /// </summary>
        public static System.Func<string, string, Dictionary<string, string>, System.Action<string, Dictionary<string, string>>, bool> HoldText;

        /// <summary>A text message from one of the phone's contacts; without the contact, said on screen instead.</summary>
        public static void Message(Contact contact, string key, in Args a)
        {
            if (contact == null)
            {
                Toast(NotificationType.Info, key, a);
                return;
            }
            Dictionary<string, string> data = Data(a);
            string from = contact == FamilyPhone.PartnerContact ? "partner" : contact == PhoneContacts.Mom ? "mom" : contact == FamilyPhone.InLawContact ? "inlaws" : "family";
            if (HoldText == null || !HoldText(from, key, data, (k, d) => Send(contact, k, d)))
            {
                contact.SendMessage(new TextMessage(key, data));
            }
        }

        // A text held for its words (RLO Texts), sent now.
        private static void Send(Contact contact, string key, Dictionary<string, string> data)
        {
            try
            {
                contact.SendMessage(new TextMessage(key, data));
            }
            catch (System.Exception e)
            {
                FamilyDirector.Logger?.Error(e);
            }
        }

        /// <summary>One of several lines for the same moment, never the one said last time.</summary>
        public static string Pick(string[] keys, ref int last)
        {
            int index = Random.Range(0, keys.Length);
            if (index == last && keys.Length > 1)
            {
                index = (index + 1 + Random.Range(0, keys.Length - 1)) % keys.Length;
            }
            last = index;
            return keys[index];
        }

        /// <summary>"today", "tomorrow", "Saturday, in 4 days".</summary>
        public static string When(int day)
        {
            int ahead = day - TimeHelper.CurrentDay;
            if (ahead <= 0)
            {
                return T("reallife_family_when_today");
            }
            if (ahead == 1)
            {
                return T("reallife_family_when_tomorrow");
            }
            return T("reallife_family_when_later", new Args { Text = TimeHelper.GetDayOfWeek(day).GetLocalization(), N = ahead.ToString() });
        }

        /// <summary>A child's age in words: "3 months", "2 years".</summary>
        public static string Age(float years)
        {
            if (years < 1f)
            {
                int months = Mathf.Clamp(Mathf.FloorToInt(years * 12f), 0, 11);
                return T(months == 1 ? "reallife_family_age_month" : "reallife_family_age_months", new Args { N = months.ToString() });
            }
            int whole = Mathf.FloorToInt(years);
            return T(whole == 1 ? "reallife_family_age_year" : "reallife_family_age_years", new Args { N = whole.ToString() });
        }

        public static string BondWord(Bond bond)
        {
            switch (bond)
            {
                case Bond.Dating: return FamilyState.S.LivesIn ? "reallife_family_bond_living" : "reallife_family_bond_dating";
                case Bond.Engaged: return "reallife_family_bond_engaged";
                case Bond.Married: return "reallife_family_bond_married";
                case Bond.Separated: return "reallife_family_bond_separated";
                case Bond.Divorced: return "reallife_family_bond_divorced";
                default: return "reallife_family_bond_single";
            }
        }

        public static string HarmonyWord(float harmony)
        {
            return harmony >= 80f ? "reallife_family_harmony_great" : harmony >= 60f ? "reallife_family_harmony_good"
                : harmony >= 40f ? "reallife_family_harmony_tense" : harmony >= 20f ? "reallife_family_harmony_strained" : "reallife_family_harmony_breaking";
        }
    }
}
