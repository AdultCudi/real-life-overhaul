using System;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// Which of the mod's notifications a kind's switch keeps from popping up (Options, "Notifications that pop up"). Never
    /// kept back: a notification that is not the mod's; a warning or an error; the kinds that have no switch, the police's
    /// (an arrest, a fine, a court, prison, a scheme's inquiry) and health's (an illness, the doctor, the hospital); what is
    /// owed, the end of a life and the morning line's news (<see cref="Always"/>); a test's answer and the driving test's
    /// own words (a key with "_test_"), but for the samples of each kind, which go as their kind goes. Game-free
    /// (EconomyCheck --notice-mute).
    /// COST: with every kind on, one comparison a notification; with one off, the kind's table walk (NoticeKinds.Of) and a
    /// dozen ordinal comparisons; nothing allocated; never per frame.
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
            "home_morning_more"          // its further parts, when the news does not fit in one
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

        /// <summary>True for a key that pops up whatever its kind's switch says: what is owed, the end of a life, the morning line's news.</summary>
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

        /// <summary>A copy of what always pops up (the key after the mod's prefix), for the offline check.</summary>
        public static string[] AlwaysPrefixes()
        {
            return (string[])AlwaysTable.Clone();
        }
    }
}
