using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Utilities
{
    /// <summary>Why a connection was down over a span, or that it was up (<see cref="InternetRules.Down"/>). Never saved.</summary>
    public enum NetDown
    {
        Up = 0,
        Outage = 1,       // the provider's outage: not the player's doing
        NoPlan = 2,       // no plan at the place, or one closed
        Installing = 3,   // bought, not yet connected
        Cut = 4           // cut off for a bill not paid
    }

    /// <summary>
    /// The rules of the city's internet; every draw from the save's seed, the day and the provider, so a reload changes
    /// nothing. Checked offline (EconomyCheck).
    /// </summary>
    public static class InternetRules
    {
        /// <summary>The providers: the rivals, and the player's own company where its network is open.</summary>
        public static int Count => InternetCatalogue.Providers.Length + (OwnReaches ? 1 : 0);

        public static Provider Get(int provider)
        {
            if (provider >= 0 && provider < InternetCatalogue.Providers.Length)
            {
                return InternetCatalogue.Providers[provider];
            }
            return provider == InternetCatalogue.OwnIndex && OwnReaches ? InternetCatalogue.Own : null;
        }

        private static bool OwnReaches => InternetCatalogue.Own.Districts.Length > 0;

        public static bool Reaches(int provider, string district)
        {
            Provider p = Get(provider);
            if (p == null || district == null)
            {
                return false;
            }
            for (int i = 0; i < p.Districts.Length; i++)
            {
                if (p.Districts[i] == district)
                {
                    return true;
                }
            }
            return !CityDistricts.Known(district) && InternetCatalogue.ReachesNewDistricts(p);   // a district a free update adds
        }

        /// <summary>A plan's price a month now, cents: home or business, contract or not, at the provider's month and today's prices.</summary>
        public static int MonthCents(int provider, int plan, bool business, bool contract, int level, int promo)
        {
            Provider p = Get(provider);
            if (p == null)
            {
                return 0;
            }
            int k = Math.Max(0, Math.Min(p.MonthCents.Length - 1, plan));
            long cents = p.MonthCents[k];
            cents = cents * (business ? InternetCatalogue.BusinessShare : 1000) / 1000;
            cents = cents * (contract ? 1000 : InternetCatalogue.MonthlyShare) / 1000;
            cents = cents * Math.Max(InternetCatalogue.PromoLeast, Math.Min(InternetCatalogue.PromoMost, promo)) / 1000;
            cents = cents * Math.Max(PriceRules.Least, Math.Min(PriceRules.Most, level)) / PriceRules.Start;
            return (int)Math.Min(cents, int.MaxValue / 4);
        }

        /// <summary>A plan bought: installed after InstallDays, its price held for a contract; anything owed stays owed.</summary>
        public static void SignUp(Account a, int provider, int plan, bool business, bool contract, int day, int monthDays, int monthCents)
        {
            a.Service = Service.Internet;
            a.Provider = provider;
            a.Plan = plan;
            a.BusinessPlan = business;
            a.State = SupplyState.Pending;
            a.OpenedDay = day;
            a.ConnectDay = day + InternetCatalogue.InstallDays;
            a.TemporaryUntil = 0;
            a.CutForDebt = false;
            a.ContractUntil = contract ? day + InternetCatalogue.ContractMonths * Math.Max(1, monthDays) : 0;
            a.PriceCents = Math.Max(0, monthCents);
            a.OutageFrom = a.OutageUntil = 0;
        }

        /// <summary>Leaving a contract early: half of each month left, a month begun counting, money; nothing out of contract.</summary>
        public static int ExitFee(Account a, int day, int monthDays)
        {
            if (a == null || a.ContractUntil <= day)
            {
                return 0;
            }
            int monthDaysSafe = Math.Max(1, monthDays);
            int monthsLeft = (a.ContractUntil - day + monthDaysSafe - 1) / monthDaysSafe;
            return UtilitiesRules.Money((long)monthsLeft * a.PriceCents * InternetCatalogue.ExitShare / 1000);
        }

        /// <summary>A day's share of the month's price, cents.</summary>
        public static int DayCents(Account a, int monthDays)
        {
            return a == null ? 0 : (int)(((long)a.PriceCents + Math.Max(1, monthDays) / 2) / Math.Max(1, monthDays));
        }

        /// <summary>A day of a connection added to the month; nothing on a day the provider failed it (its service credit).</summary>
        public static void AddDay(Account a, int monthDays, bool outage)
        {
            if (a == null || a.State != SupplyState.Active || outage)
            {
                return;
            }
            a.Accrued = (int)Math.Min((long)a.Accrued + DayCents(a, monthDays), int.MaxValue / 4);
        }

        /// <summary>An outage is on (hours from the save's first day: day * 24 + hour).</summary>
        public static bool InOutage(Account a, int hour)
        {
            return a != null && a.OutageUntil > a.OutageFrom && hour >= a.OutageFrom && hour < a.OutageUntil;
        }

        /// <summary>An outage was on at any minute of the span (minutes from the save's first day), by the last outage drawn.</summary>
        public static bool OutageBetween(Account a, int fromMinute, int toMinute)
        {
            if (a == null || a.OutageUntil <= a.OutageFrom)
            {
                return false;
            }
            long from = (long)a.OutageFrom * 60, until = (long)a.OutageUntil * 60;
            return toMinute >= from && fromMinute < until;
        }

        /// <summary>Why a place's internet was down over some minutes, or up; only <see cref="NetDown.Outage"/> is the provider's doing.</summary>
        public static NetDown Down(Account a, int fromMinute, int toMinute)
        {
            if (a == null)
            {
                return NetDown.NoPlan;
            }
            switch (a.State)
            {
                case SupplyState.Pending: return NetDown.Installing;
                case SupplyState.Cut: return NetDown.Cut;
                case SupplyState.Active: return OutageBetween(a, fromMinute, toMinute) ? NetDown.Outage : NetDown.Up;
                default: return NetDown.NoPlan;
            }
        }

        /// <summary>Whether a provider fails on a day, from an hour for so many hours (half as long on a business plan).</summary>
        public static bool Outage(int seed, int day, int provider, bool business, out int fromHour, out int hours)
        {
            fromHour = hours = 0;
            Provider p = Get(provider);
            if (p == null || Draw(seed, day, provider, 1, 1000) >= p.OutagePerMille)
            {
                return false;
            }
            fromHour = InternetCatalogue.OutageFirstHour + Draw(seed, day, provider, 2, InternetCatalogue.OutageLastHour - InternetCatalogue.OutageFirstHour + 1);
            hours = InternetCatalogue.OutageLeastHours + Draw(seed, day, provider, 3, InternetCatalogue.OutageMostHours - InternetCatalogue.OutageLeastHours + 1);
            if (business)
            {
                hours = Math.Max(1, (hours + 1) / 2);
            }
            return true;
        }

        /// <summary>A provider's next month: a step within the bounds, now and then a promotion's cut.</summary>
        public static int NextPromo(int seed, int month, int provider, int promo)
        {
            int step = Draw(seed, month, provider, 4, 3) - 1;   // down, the same or up
            int next = promo + step * InternetCatalogue.PromoStep;
            if (Draw(seed, month, provider, 5, 1000) < InternetCatalogue.PromoChancePerMille)
            {
                next -= InternetCatalogue.PromoCut;
            }
            return Math.Max(InternetCatalogue.PromoLeast, Math.Min(InternetCatalogue.PromoMost, next));
        }

        /// <summary>A save meeting the internet for the first time: no state, or a line from before it, with no seed.</summary>
        public static bool FirstMeeting(UtilitiesState loaded)
        {
            return loaded == null || loaded.Seed == 0;
        }

        /// <summary>The grace begun today for the places that need a connection now; how many it covers.</summary>
        public static int BeginGrace(UtilitiesState s, IList<string> places, int today)
        {
            s.Grace.Clear();
            for (int i = 0; places != null && i < places.Count; i++)
            {
                if (!string.IsNullOrEmpty(places[i]) && !s.Grace.Contains(places[i]))
                {
                    s.Grace.Add(places[i]);
                }
            }
            s.GraceUntil = s.Grace.Count > 0 ? today + InternetCatalogue.GraceDays : 0;
            return s.Grace.Count;
        }

        /// <summary>A place the grace covers may open without a connection before it ends.</summary>
        public static bool InGrace(UtilitiesState s, string place, int today)
        {
            return s != null && place != null && today < s.GraceUntil && s.Grace.Contains(place);
        }

        /// <summary>The grace's days left, today counted; 0 with none.</summary>
        public static int GraceLeft(UtilitiesState s, int today)
        {
            return s == null || s.Grace.Count == 0 ? 0 : Math.Max(0, s.GraceUntil - today);
        }

        /// <summary>The clock moved without the days played (a step back, a guest joining a host): the grace keeps its days left.</summary>
        public static bool MoveGrace(UtilitiesState s, int seen, int today, bool sessionClock)
        {
            int step = today - seen;
            if (s == null || s.GraceUntil <= 0 || seen < 0 || !(step < 0 || (sessionClock && step > 1)))
            {
                return false;
            }
            s.GraceUntil = Math.Max(1, s.GraceUntil + step);
            return true;
        }

        /// <summary>The grace over by today, ended and forgotten; true on the day it ends, to tell the player once.</summary>
        public static bool EndGrace(UtilitiesState s, int today)
        {
            if (s == null || s.GraceUntil <= 0 || today < s.GraceUntil)
            {
                return false;
            }
            s.GraceUntil = 0;
            s.Grace.Clear();
            return true;
        }

        /// <summary>A number from 0 to range-1, the same for the same seed, day, provider and purpose.</summary>
        public static int Draw(int seed, int day, int provider, int purpose, int range)
        {
            unchecked
            {
                uint h = 2166136261u;
                h = (h ^ (uint)seed) * 16777619u;
                h = (h ^ (uint)day) * 16777619u;
                h = (h ^ (uint)provider) * 16777619u;
                h = (h ^ (uint)purpose) * 16777619u;
                h ^= h >> 13;
                h *= 0x5bd1e995u;
                h ^= h >> 15;
                return range <= 1 ? 0 : (int)(h % (uint)range);
            }
        }
    }
}
