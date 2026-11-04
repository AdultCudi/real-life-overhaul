using System;

namespace RealLifeOverhaul.Economy
{
    /// <summary>What a day brought for one licence.</summary>
    public enum LicenceNote
    {
        None = 0,
        Remind = 1,     // it runs out within the reminder's days
        Renew = 2,      // it ran out yesterday and renews by itself: the director takes the fee
        Lapsed = 3,     // it ran out yesterday
        Granted = 4     // an application was decided today: the permit runs from now
    }

    /// <summary>What a business is, as far as its licences go: read from the game once a day, game-free here.</summary>
    public struct LicenceSite
    {
        public bool Food, Alcohol, Late;
        public bool Crowd;          // a nightclub, a cinema, a theatre, a casino: many people in one room
        public bool Seating;        // tables for food and drink
        public bool Drivers;        // a delivery driver on its staff
        public bool Cellar;         // spirits or goods held in bulk
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

        /// <summary>
        /// The licences a business needs by what it is: the four the first version asked, a fire safety certificate where food is served at tables or a crowd gathers,
        /// an outdoor seating permit where there are tables, a delivery vehicle permit with a driver on the staff, and bonded storage in a warehouse, where
        /// goods are held in bulk. A bit each.
        /// </summary>
        public static int Needs(LicenceSite s)
        {
            return Needs(s.Food, s.Alcohol, s.Late)
                   | (s.Seating || s.Crowd ? 1 << (int)LicenceKind.FireSafety : 0) | (s.Seating ? 1 << (int)LicenceKind.OutdoorSeating : 0)
                   | (s.Drivers ? 1 << (int)LicenceKind.DeliveryVehicle : 0) | (s.Cellar ? 1 << (int)LicenceKind.BondedStorage : 0);
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

        /// <summary>Held, or applied for: an inspector counts an application in hand as the permit it will become.</summary>
        public static bool Covered(Licence l, int day)
        {
            return Valid(l, day) || l.Pending;
        }

        /// <summary>A term is bought; renewed early, it is added on at the end of the one running.</summary>
        public static void Grant(Licence l, int kind, int day)
        {
            l.Until = Math.Max(l.Until, day - 1) + CompanyCatalogue.LicenceTerm[kind];
            l.Reminded = false;
            l.Decide = 0;
        }

        /// <summary>The days the city takes over an application for a permit not held.</summary>
        public static int Decision(int kind)
        {
            return CompanyCatalogue.LicenceDecide[kind];
        }

        /// <summary>
        /// A permit bought (its fee paid). One still running, or one the city grants at once, has a term added now. Any other waits for the city's
        /// decision: true if it waits. A second application while one waits changes nothing (false, and nothing is to be paid: see <see cref="CanApply"/>).
        /// </summary>
        public static bool Apply(Licence l, int kind, int day)
        {
            if (Valid(l, day) || Decision(kind) <= 0)
            {
                Grant(l, kind, day);
                return false;
            }
            l.Decide = day + Decision(kind);
            return true;
        }

        /// <summary>Whether a purchase may be made today: not while an application for the same permit waits.</summary>
        public static bool CanApply(Licence l)
        {
            return !l.Pending;
        }

        /// <summary>The days left before the city decides; 0 with no application.</summary>
        public static int DaysToDecision(Licence l, int day)
        {
            return l.Pending ? Math.Max(1, l.Decide - day) : 0;
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

        /// <summary>The licences a business needs and does not hold today, a bit each; one applied for counts as held.</summary>
        public static int Missing(ShopFile s, int needs, int day)
        {
            int missing = 0;
            for (int k = 0; k < s.Permits.Length; k++)
            {
                if (Needed(needs, k) && !Covered(s.Permits[k], day))
                {
                    missing |= 1 << k;
                }
            }
            return missing;
        }

        public static LicenceNote Day(Licence l, int day)
        {
            if (l.Pending && day >= l.Decide)
            {
                return LicenceNote.Granted;
            }
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
                if (Needed(needs, k) && s.Permits[k].Until <= 0 && !s.Permits[k].Pending)
                {
                    // The four kinds added later come with a first term free: a business that ran before they were asked for has not been told.
                    s.Permits[k].Until = day - 1 + (k >= CompanyCatalogue.LicenceOwnKinds ? CompanyCatalogue.LicenceTerm[k] : CompanyCatalogue.LicenceGraceDays);
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
