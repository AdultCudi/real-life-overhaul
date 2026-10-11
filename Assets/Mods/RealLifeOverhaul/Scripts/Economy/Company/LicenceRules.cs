using System;

namespace RealLifeOverhaul.Economy
{
    /// <summary>What a day brought for one licence.</summary>
    public enum LicenceNote
    {
        None = 0,
        Remind = 1,     // it runs out within three days
        Renew = 2,      // it ran out yesterday and renews by itself: the director takes the fee
        Lapsed = 3      // it ran out yesterday
    }

    /// <summary>Licences and permits by rule: which a business needs, what each costs, how long it lasts, and what trading without one costs. Game-free.</summary>
    public static class LicenceRules
    {
        /// <summary>The licences a business needs, a bit each: food hygiene, alcohol, late opening, and signage for all.</summary>
        public static int Needs(bool food, bool alcohol, bool late)
        {
            return (food ? 1 << (int)LicenceKind.Hygiene : 0) | (alcohol ? 1 << (int)LicenceKind.Alcohol : 0) | (late ? 1 << (int)LicenceKind.Late : 0) | 1 << (int)LicenceKind.Signage;
        }

        public static bool Needed(int needs, int kind)
        {
            return (needs & (1 << kind)) != 0;
        }

        /// <summary>Opening hours that need the late opening permit: open at or past ten at night, or before five in the morning.</summary>
        public static bool IsLate(int opens, int closes)
        {
            return closes > opens && (closes > CompanyCatalogue.LateHour || opens < CompanyCatalogue.EarlyHour);
        }

        public static int Fee(int kind, int index)
        {
            return CompanyCatalogue.AtPrices(CompanyCatalogue.LicenceFee[kind], index);
        }

        public static int Fine(int kind, int index)
        {
            return Fee(kind, index) * CompanyCatalogue.LicenceFineMultiple;
        }

        public static bool Valid(Licence l, int day)
        {
            return l.Until >= day;
        }

        /// <summary>A term is bought. Renewed before it runs out, the new term is added on at its end.</summary>
        public static void Grant(Licence l, int kind, int day)
        {
            l.Until = Math.Max(l.Until, day - 1) + CompanyCatalogue.LicenceTerm[kind];
            l.Reminded = false;
        }

        /// <summary>
        /// A business is seen for the first time. One that already ran when the rule appeared gets every licence it
        /// needs for a first term, free. One opened later may trade for a few days before it needs them.
        /// </summary>
        public static void First(ShopFile s, int needs, int day, bool ranBefore)
        {
            for (int k = 0; k < s.Permits.Length; k++)
            {
                if (Needed(needs, k))
                {
                    s.Permits[k].Until = day - 1 + (ranBefore ? CompanyCatalogue.LicenceTerm[k] : CompanyCatalogue.LicenceGraceDays);
                    s.Permits[k].Reminded = false;
                }
            }
        }

        /// <summary>The licences a business needs and does not hold today, a bit each.</summary>
        public static int Missing(ShopFile s, int needs, int day)
        {
            int missing = 0;
            for (int k = 0; k < s.Permits.Length; k++)
            {
                if (Needed(needs, k) && !Valid(s.Permits[k], day))
                {
                    missing |= 1 << k;
                }
            }
            return missing;
        }

        public static LicenceNote Day(Licence l, int day)
        {
            if (l.Until <= 0)
            {
                return LicenceNote.None;
            }
            if (day == l.Until + 1)
            {
                return l.Auto ? LicenceNote.Renew : LicenceNote.Lapsed;
            }
            if (!l.Reminded && !l.Auto && l.Until >= day && l.Until - day < CompanyCatalogue.LicenceRemindDays)
            {
                l.Reminded = true;
                return LicenceNote.Remind;
            }
            return LicenceNote.None;
        }

        /// <summary>Whether trading without this one shuts the doors for some hours as well as the fine.</summary>
        public static bool Shuts(int kind)
        {
            return kind == (int)LicenceKind.Hygiene || kind == (int)LicenceKind.Alcohol;
        }
    }
}
