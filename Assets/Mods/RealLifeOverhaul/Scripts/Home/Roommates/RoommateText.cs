using System.Collections.Generic;
using Entities;
using Extensions;
using Helpers;
using Localizor;
using Localizor.LanguageChangeEvent;
using UI.Notification;

namespace RealLifeOverhaul.Roommates
{
    /// <summary>What a line of roommate text may be filled with. Every text uses the same few names, so one set does for all.</summary>
    public struct Fill
    {
        public string Name;      // {name}: the roommate, or the applicant
        public string Amount;    // {amount}: money, already formatted
        public string Day;       // {day}
        public string Text;      // {text}: a line built elsewhere
        public string N;         // {n}: a number
        public string Other;     // {other}: somebody or something else (the partner, what was taken)

        public static Fill For(string name) => new Fill { Name = name };

        public Fill Cost(int amount) { Amount = RoommateText.Money(amount); return this; }
        public Fill On(string day) { Day = day; return this; }
        public Fill With(string text) { Text = text; return this; }
        public Fill Count(int n) { N = n.ToString(); return this; }
        public Fill And(string other) { Other = other; return this; }
    }

    /// <summary>
    /// Text for the roommate: a key and what fills it become a line for a notice, a phone message or a conversation.
    /// Everything here builds strings and is called when something happens or a page is opened, never on a timer.
    /// Money, weekdays and the hours of a day are written as the game writes them.
    /// </summary>
    public static class RoommateText
    {
        private static readonly RoomTrait[] TraitOrder =
        {
            RoomTrait.Tidy, RoomTrait.Messy, RoomTrait.Quiet, RoomTrait.Noisy, RoomTrait.EarlyBird, RoomTrait.NightOwl, RoomTrait.Generous, RoomTrait.Tight,
            RoomTrait.Reliable, RoomTrait.LateWithRent, RoomTrait.Cooks, RoomTrait.RaidsFridge, RoomTrait.Sociable, RoomTrait.Private
        };

        // Written out in full for the locale check: one word for each trait, in the order above.
        private static readonly string[] TraitKeys =
        {
            "reallife_roommate_trait_tidy", "reallife_roommate_trait_messy", "reallife_roommate_trait_quiet", "reallife_roommate_trait_noisy",
            "reallife_roommate_trait_early", "reallife_roommate_trait_owl", "reallife_roommate_trait_generous", "reallife_roommate_trait_tight",
            "reallife_roommate_trait_reliable", "reallife_roommate_trait_late", "reallife_roommate_trait_cooks", "reallife_roommate_trait_raids",
            "reallife_roommate_trait_sociable", "reallife_roommate_trait_private"
        };

        public static string Money(int amount)
        {
            return ((float)amount).ToCurrencyFormat();
        }

        public static string T(string key)
        {
            return key.GetLocalization();
        }

        public static string T(string key, in Fill f)
        {
            string text = key.GetLocalization();
            if (text == null || text.IndexOf('{') < 0)
            {
                return text;
            }
            return text.Replace("{name}", f.Name ?? string.Empty).Replace("{amount}", f.Amount ?? string.Empty).Replace("{day}", f.Day ?? string.Empty)
                .Replace("{text}", f.Text ?? string.Empty).Replace("{n}", f.N ?? string.Empty).Replace("{other}", f.Other ?? string.Empty);
        }

        /// <summary>The same as the text of a conversation entry.</summary>
        public static LanguageChangeEventDataHolder Say(string key, in Fill f)
        {
            return key.Localize(new
            {
                name = f.Name ?? string.Empty,
                amount = f.Amount ?? string.Empty,
                day = f.Day ?? string.Empty,
                text = f.Text ?? string.Empty,
                n = f.N ?? string.Empty,
                other = f.Other ?? string.Empty
            });
        }

        // A new dictionary each time: the game keeps hold of it for as long as the notice or the message is listed.
        private static Dictionary<string, string> Data(in Fill f)
        {
            return new Dictionary<string, string>(6)
            {
                { "name", f.Name ?? string.Empty }, { "amount", f.Amount ?? string.Empty }, { "day", f.Day ?? string.Empty },
                { "text", f.Text ?? string.Empty }, { "n", f.N ?? string.Empty }, { "other", f.Other ?? string.Empty }
            };
        }

        public static void Toast(NotificationType type, string key, in Fill f, float seconds = 9f)
        {
            Notifications.Show(type, key, Data(f), seconds);
        }

        /// <summary>A text message from the Roommate contact; with no contact (the phone's contacts are off) it is said on screen instead, so nothing is lost.</summary>
        public static void Message(string key, in Fill f)
        {
            Contact contact = RoommatePhone.Contact;
            if (contact == null)
            {
                Toast(NotificationType.Info, key, f);
                return;
            }
            contact.SendMessage(new TextMessage(key, Data(f)));
        }

        /// <summary>"today", "tomorrow", or the weekday as the game names it.</summary>
        public static string When(int day)
        {
            int ahead = day - TimeHelper.CurrentDay;
            return ahead <= 0 ? T("reallife_roommate_when_today") : ahead == 1 ? T("reallife_roommate_when_tomorrow") : TimeHelper.GetDayOfWeek(day).GetLocalization();
        }

        /// <summary>What they do for a living, as it is said after "is": the social cast's own words.</summary>
        public static string Job(SocialPerson person)
        {
            int job = person.Job < 0 ? 0 : person.Job >= SocialCatalogue.Jobs.Length ? SocialCatalogue.Jobs.Length - 1 : person.Job;
            return T(SocialCatalogue.Jobs[job]);
        }

        /// <summary>The traits of a set in words, divided by commas; "nothing yet" for none.</summary>
        public static string Traits(RoomTrait set)
        {
            string text = null;
            for (int i = 0; i < TraitOrder.Length; i++)
            {
                if ((set & TraitOrder[i]) != 0)
                {
                    text = text == null ? T(TraitKeys[i]) : text + ", " + T(TraitKeys[i]);
                }
            }
            return text ?? T("reallife_roommate_trait_none");
        }

        /// <summary>The one word for a single trait.</summary>
        public static string Trait(RoomTrait trait)
        {
            for (int i = 0; i < TraitOrder.Length; i++)
            {
                if (TraitOrder[i] == trait)
                {
                    return T(TraitKeys[i]);
                }
            }
            return string.Empty;
        }

        /// <summary>The hours their work keeps them out, in a few words.</summary>
        public static string Hours(SocialPerson person)
        {
            int job = person.Job;
            // Tried on a Tuesday and a Saturday at three hours: enough to tell the patterns apart without a table of words per job.
            bool shifts = job == 1 || job == 7;
            bool evenings = RoommateRules.AtWork(job, 2, 1, 20) && !RoommateRules.AtWork(job, 2, 1, 10);
            bool early = RoommateRules.AtWork(job, 2, 1, 7);
            return T(shifts ? "reallife_roommate_hours_shifts" : evenings ? "reallife_roommate_hours_evenings" : early ? "reallife_roommate_hours_early" : "reallife_roommate_hours_days");
        }

        public static string LevelWord(int level)
        {
            return level >= RoommateRules.FriendLevel ? "reallife_roommate_level_close" : level >= RoommateRules.GoodLevel ? "reallife_roommate_level_good"
                : level >= RoommateRules.LowLevel ? "reallife_roommate_level_civil" : "reallife_roommate_level_strained";
        }

        public static string WhyKey(EndWhy why)
        {
            switch (why)
            {
                case EndWhy.PlayerNotice: return "reallife_roommate_why_player";
                case EndWhy.NewJob: return "reallife_roommate_why_job";
                case EndWhy.NewPartner: return "reallife_roommate_why_partner";
                case EndWhy.Row: return "reallife_roommate_why_row";
                case EndWhy.PartnerMovedIn: return "reallife_roommate_why_couple";
                case EndWhy.HomeGivenUp: return "reallife_roommate_why_home";
                case EndWhy.NoRoomAfterMove: return "reallife_roommate_why_no_room";
                case EndWhy.NotCloseEnoughToMove: return "reallife_roommate_why_not_close";
                default: return "reallife_roommate_why_together";
            }
        }
    }
}
