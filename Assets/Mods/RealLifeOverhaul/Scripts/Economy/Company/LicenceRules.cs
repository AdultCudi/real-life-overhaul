using System;

namespace RealLifeOverhaul.Economy
{
    /// <summary>What a day brought for one licence.</summary>
    public enum LicenceNote
    {
        None = 0,
        Remind = 1,     // it runs out within the reminder's days
        Renew = 2,      // it ran out yesterday and renews by itself: the director takes the fee
        Lapsed = 3      // it ran out yesterday
    }

    /// <summary>Licences and permits: which a business needs, their terms, fees and fines. Game-free.</summary>
    public static class LicenceRules
    {
        private const int PurposeCheck = 81;

        /// <summary>Whether an inspection is asked for where a licence is missing; fixed by seed and day.</summary>
        public static bool Checked(int seed, int age, int shop)
        {
            return CityModel.Draw(seed, PurposeCheck, age * CompanyCatalogue.ShopsMost + shop, 1000) < CompanyCatalogue.LicenceCheckChance;
        }

        /// <summary>The licences a business needs, a bit each; signage for all.</summary>
        public static int Needs(bool food, bool alcohol, bool late)
        {
            return (food ? 1 << (int)LicenceKind.Hygiene : 0) | (alcohol ? 1 << (int)LicenceKind.Alcohol : 0) | (late ? 1 << (int)LicenceKind.Late : 0)
                   | 1 << (int)LicenceKind.Signage;
        }

        public static bool Needed(int needs, int kind)
        {
            return (needs & (1 << kind)) != 0;
        }

        /// <summary>Opening hours that need the late opening permit: late at night or early in the morning.</summary>
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

        /// <summary>A term is bought; renewed early, it is added on at the end of the one running.</summary>
        public static void Grant(Licence l, int kind, int day)
        {
            l.Until = Math.Max(l.Until, day - 1) + CompanyCatalogue.LicenceTerm[kind];
            l.Reminded = false;
        }

        /// <summary>
        /// A business seen first: a free first term of each licence if it ran before the feature began, else a few
        /// days' grace.
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
            if (l.Auto && day > l.Until)
            {
                return LicenceNote.Renew;   // every day until its fee can be paid, a day the pass did not run included
            }
            if (day == l.Until + 1)
            {
                return LicenceNote.Lapsed;
            }
            if (!l.Reminded && !l.Auto && l.Until >= day && l.Until - day < CompanyCatalogue.LicenceRemindDays)
            {
                l.Reminded = true;
                return LicenceNote.Remind;
            }
            return LicenceNote.None;
        }

        /// <summary>
        /// A licence that renews by itself and whose fee could not be paid: true when that is to be said, on the day it ran
        /// out or the first day the pass found it so; afterwards it is only tried again each day.
        /// </summary>
        public static bool Unpaid(Licence l, int day)
        {
            bool say = day == l.Until + 1 || !l.Reminded;
            l.Reminded = true;
            return say;
        }

        /// <summary>
        /// The licences a business has come to need and never held (it opens late now, or its type changed): the same few
        /// days' grace as a business opened later, so that it is not fined the day it first needs one. A bit each.
        /// </summary>
        public static int Newly(ShopFile s, int needs, int day)
        {
            int granted = 0;
            for (int k = 0; k < s.Permits.Length; k++)
            {
                if (Needed(needs, k) && s.Permits[k].Until <= 0)
                {
                    s.Permits[k].Until = day - 1 + CompanyCatalogue.LicenceGraceDays;
                    s.Permits[k].Reminded = false;
                    granted |= 1 << k;
                }
            }
            return granted;
        }

        /// <summary>Whether trading without this one also shuts the doors for some hours.</summary>
        public static bool Shuts(int kind)
        {
            return kind == (int)LicenceKind.Hygiene || kind == (int)LicenceKind.Alcohol;
        }
    }
}
