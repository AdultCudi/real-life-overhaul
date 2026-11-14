using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// Which of the mod's notifications a kind's switch keeps from popping up (Options, "Notifications that pop up"). Never
    /// kept back: a notification that is not the mod's; a warning or an error; the kinds that have no switch, the police's
    /// (an arrest, a fine, a court and its texts, which come as a notification when the Legal contact is not on the phone, prison,
    /// a scheme's inquiry) and health's (an illness, the doctor, the hospital); what is
    /// owed, the end of a life and the morning line's news; what the mod says of itself in answer to the player (the options
    /// page's replies, the Guide, Settings and Staff apps when they cannot open at once, the bug report, the performance
    /// tools, the texts service, Going Public's word on the session, who has the camera, an app moved on the phone) and its
    /// welcome; the driving test's own words; a reminder the player set in the Planner (<see cref="Always"/>); a test's answer
    /// (a key with "_test_"), but for the samples of each kind, which go as their kind goes. One kept back still goes into
    /// the game's notification list, whatever its sender asked (<see cref="ListData"/>). Game-free (EconomyCheck --notice-mute).
    /// COST: with every kind on, one comparison a notification; with one off, the kind's table walk (NoticeKinds.Of) and a
    /// few dozen ordinal comparisons; nothing allocated; never per frame.
    /// </summary>
    public static class NoticeMuteRules
    {
        /// <summary>The kinds with a switch, in the order the options page lists them. Never changed.</summary>
        public static readonly NoticeKind[] Switchable =
        {
            NoticeKind.Road, NoticeKind.Money, NoticeKind.Property, NoticeKind.Work, NoticeKind.Business, NoticeKind.Tech, NoticeKind.News,
            NoticeKind.People, NoticeKind.Other
        };

        private const string Samples = NoticeKinds.ModPrefix + "notice_test_";   // the options page's samples, one of each kind: they go as their kind goes
        private const string TestWord = "_test_";                                // in every test's answer, and in the driving test's own words

        // The key after the mod's prefix, as far as it must go: what is owed and the end of a life pop up whatever their kind's
        // switch says; and the morning line with the night's news, which holds only kinds that pop up (MorningNews), so it goes as they do.
        // Then what the mod says of itself in answer to the player, which is no news of the city: the options page's replies, its
        // own apps', tools' and services' (each of the kind "anything else", whose switch would take the answer from the press that
        // asked for it), and its welcome, once a version, which opens what it names; the driving test's own words whose keys have no
        // "_test_"; and the player's own reminders. A kept one would still be in the list, but unseen at the press and without its click.
        private static readonly string[] AlwaysTable =
        {
            "util_toast",                // every power, water and internet toast (one key carries each text): the bills due, a supply cut off
            "econ_note_statement",       // the card's statement: what is due, the minimum and the day it is taken
            "econ_note_advice",          // debts long overdue, and the free advice
            "co_note_return_due",        // a company's tax return for the quarter, to be filed by a day
            "estate_note_trading_tax",   // the year's tax on the hotels and the car hire
            "civic_charge_",             // a fine or a parking ticket the city's policy changed
            "dynasty_hand_",             // a death or a retirement, and the estate passing on
            "dynasty_elder_died",        // the old founder's death, and what their fund leaves
            "home_morning_news",         // the morning line with the news held while the player slept
            "home_morning_more",         // its further parts, when the news does not fit in one
            "opt_text",                  // the options page's replies (OptionsNotice): a preset, an import, an export, a reset, the setup check, the guard's
                                         // status, the bug report done or failed, the page's own tests, and a test that needs a city
            "guide_not_here",            // the Guide, when it is not on the phone
            "guide_waits",               // ...and when it opens once a menu of the game's has closed
            "set_not_here",              // the Settings app, the same
            "set_waits",
            "staff_not_here",            // the Staff app, the same
            "staff_waits",
            "bug_waits",                 // the bug report, made once a menu of the game's has closed
            "perf_",                     // the performance tools: the benchmark and the probe, started, armed, refused, done
            "texts_",                    // the texts service: its state, its model and its tests
            "textsai_",                  // the texts' AI services: a key kept, a sign-in, a model, a test, a limit reached
            "gp_",                       // Going Public: what it changes in this session, whose city it is and what of it is shared, and why
                                         // something the player asked for waits while a session is on
            "camera_other_toast",        // which other mod has the camera, when the camera's key does nothing
            "phone_layout_reset_done",   // the phone's apps put back in their places (the options page)
            "phone_moved",               // an app moved on the phone's home screen
            "windows_reset_done",        // the windows put back where they were (the options page)
            "ui_apps_reset_done",        // the app bar and the favourites put back (the options page)
            "meta_welcome_note_",        // the welcome on the first run and after an update, which opens What's new
            "beta_note",                 // the same on the beta
            "ui_note_interface_off",     // the word, once, to a player whose RLO interface 1.0.3 switched off: their screens changed unasked
            "lic_toast",                 // the driving test's words on the way, and its result
            "lic_theory_",               // the theory test's marks and result
            "lic_car_",                  // the test car: the drive is over, or it is not the player's
            "lic_fee_returned",          // a test's fee given back
            "planner_reminder",          // a reminder the player set in the Planner, which has its own switch for them
            "planner_welcome"            // the Planner's own word, once, that it is on the phone, which opens it
        };

        /// <summary>A kind's bit in a mask of the kinds switched off.</summary>
        public static int Bit(NoticeKind kind)
        {
            return 1 << (int)kind;
        }

        /// <summary>True for a kind with a switch: neither the police's nor health's, nor a notification that is not the mod's.</summary>
        public static bool HasSwitch(NoticeKind kind)
        {
            return kind > NoticeKind.None && kind <= NoticeKind.Other && kind != NoticeKind.Police && kind != NoticeKind.Health;
        }

        /// <summary>The mask with a kind's bit set when its switch is off and cleared when it is on; a kind without a switch is never set.</summary>
        public static int With(int mask, NoticeKind kind, bool shown)
        {
            if (!HasSwitch(kind))
            {
                return mask;
            }
            return shown ? mask & ~Bit(kind) : mask | Bit(kind);
        }

        /// <summary>
        /// True for a key that pops up whatever its kind's switch says: what is owed, the end of a life, the morning line's news,
        /// the mod's words about itself in answer to the player, the driving test's own words and the player's own reminders.
        /// </summary>
        public static bool Always(string key)
        {
            int from = NoticeKinds.ModPrefix.Length;
            if (key == null || key.Length <= from || string.CompareOrdinal(key, 0, NoticeKinds.ModPrefix, 0, from) != 0)
            {
                return false;
            }
            for (int i = 0; i < AlwaysTable.Length; i++)
            {
                string prefix = AlwaysTable[i];
                if (prefix.Length <= key.Length - from && string.CompareOrdinal(key, from, prefix, 0, prefix.Length) == 0)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>True for a test's answer or the driving test's own words: a key with "_test_" that is not a kind's sample.</summary>
        public static bool TestAnswer(string key)
        {
            return key != null && key.IndexOf(TestWord, StringComparison.Ordinal) >= 0 && !key.StartsWith(Samples, StringComparison.Ordinal);
        }

        /// <summary>
        /// True when a notification is kept from popping up: its kind is off in <paramref name="mask"/> and it is none of those
        /// that always pop up. <paramref name="urgent"/>: a warning or an error. With every kind on, one comparison.
        /// </summary>
        public static bool Muted(string key, bool urgent, int mask)
        {
            if (mask == 0 || urgent)
            {
                return false;
            }
            NoticeKind kind = NoticeKinds.Of(key);
            if (!HasSwitch(kind) || (mask & Bit(kind)) == 0)
            {
                return false;
            }
            return !Always(key) && !TestAnswer(key);
        }

        /// <summary>
        /// The words a notification kept back is written into the game's list with. The list is where one that did not pop up is
        /// found, so it goes there whatever its sender asked: a sender that kept it out of the list did so because it popped up
        /// (an answer seen at once, a toast that opens an app), and may use its dictionary again, so the list gets a copy of it; one
        /// the sender asked the list for keeps its own, as the game keeps it. Null stays null. On a kept-back notification only.
        /// </summary>
        public static Dictionary<string, string> ListData(Dictionary<string, string> data, bool track)
        {
            return track || data == null ? data : new Dictionary<string, string>(data, data.Comparer);
        }

        /// <summary>A copy of what always pops up (the key after the mod's prefix), for the offline check.</summary>
        public static string[] AlwaysPrefixes()
        {
            return (string[])AlwaysTable.Clone();
        }
    }
}
