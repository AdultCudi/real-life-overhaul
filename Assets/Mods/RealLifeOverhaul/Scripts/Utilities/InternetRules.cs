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
    /// The rules of the city's internet (InternetCatalogue), game-free and checked by EconomyCheck: where a provider
    /// reaches, what a plan costs this month, a plan bought and installed, the fee for leaving a contract early, the
    /// month's price moving, and the outages. Every draw comes from the save's seed, the day and the provider (Draw):
    /// the same day of the same save always has the same outages and prices, so reloading changes nothing.
    ///
    /// The grace period: a save that meets the rule that some businesses cannot open without a connection for the first
    /// time (the feature newly in it: no utilities line yet, or one from before the internet) gives the businesses it
    /// already has that need one GraceDays of the game's clock to get connected. Only those places, listed when it
    /// begins; a business started after that needs a connection from its first day.
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

        /// <summary>True if a provider reaches a neighbourhood.</summary>
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
            return false;
        }

        /// <summary>What a plan costs a month now, cents: its list price for a home or a business, on a contract or month by month, at the provider's month and today's prices.</summary>
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

        /// <summary>
        /// A plan bought: installed after the catalogue's days, its price held for the contract (or set month by month),
        /// whatever was owed on the place's connection still owed.
        /// </summary>
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

        /// <summary>The fee for leaving a contract early: half of each month left (a month begun counts), money; nothing out of contract.</summary>
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

        /// <summary>A day of a connection added to the month: its share of the price, or nothing on a day the provider failed it (its service credit).</summary>
        public static void AddDay(Account a, int monthDays, bool outage)
        {
            if (a == null || a.State != SupplyState.Active || outage)
            {
                return;
            }
            a.Accrued = (int)Math.Min((long)a.Accrued + DayCents(a, monthDays), int.MaxValue / 4);
        }

        /// <summary>True while an outage is on (hours counted from the save's first day: day * 24 + hour).</summary>
        public static bool InOutage(Account a, int hour)
        {
            return a != null && a.OutageUntil > a.OutageFrom && hour >= a.OutageFrom && hour < a.OutageUntil;
        }

        /// <summary>
        /// True if an outage was on at any minute from <paramref name="fromMinute"/> to <paramref name="toMinute"/>
        /// (minutes from the save's first day: day * 1440 + minute). It reads the connection's last outage, which stands
        /// until the provider's next one is drawn on a later morning, so a span of the same day is always answered.
        /// </summary>
        public static bool OutageBetween(Account a, int fromMinute, int toMinute)
        {
            if (a == null || a.OutageUntil <= a.OutageFrom)
            {
                return false;
            }
            long from = (long)a.OutageFrom * 60, until = (long)a.OutageUntil * 60;
            return toMinute >= from && fromMinute < until;
        }

        /// <summary>
        /// Why a place's internet was down over some minutes, or that it was up: what other features are told (Wiring).
        /// Only <see cref="NetDown.Outage"/> is the provider's doing; the rest are the player's own.
        /// </summary>
        public static NetDown Down(Account a, int fromMinute, int toMinute)
        {
            if (a == null || a.State == SupplyState.Closed)
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

        /// <summary>Whether a provider fails on a day, and when: from an hour of the day for so many hours (half as long on a business plan).</summary>
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

        /// <summary>A provider's next month: a step up or down within the bounds, now and then a promotion that cuts the price.</summary>
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

        // ---------------------------------------------------------------- the grace period

        /// <summary>
        /// True for a save meeting the internet for the first time: the state read from it is none (the utilities newly in
        /// it), or a line written before the internet, which had no seed (every line since has one: it is drawn on the first
        /// load and kept).
        /// </summary>
        public static bool FirstMeeting(UtilitiesState loaded)
        {
            return loaded == null || loaded.Seed == 0;
        }

        /// <summary>The grace begun today for the places that need a connection now (each once); none if there are none. The number of places it covers.</summary>
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

        /// <summary>True if a place may open without a connection today: one the grace covers, before it ends.</summary>
        public static bool InGrace(UtilitiesState s, string place, int today)
        {
            return s != null && place != null && today < s.GraceUntil && s.Grace.Contains(place);
        }

        /// <summary>The days of the grace left, today counted (GraceDays on the day it begins, 1 on its last); 0 with none.</summary>
        public static int GraceLeft(UtilitiesState s, int today)
        {
            return s == null || s.Grace.Count == 0 ? 0 : Math.Max(0, s.GraceUntil - today);
        }

        /// <summary>
        /// The clock moved from the day last seen to today without those days being played: a step back, or, where the
        /// clock is a session's (Going Public: a guest's clock is set to the host's as they join), a leap of more than a
        /// day. The grace moves with it and keeps the days it had left. True if it moved.
        /// </summary>
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

        /// <summary>The grace over by today: ended and forgotten. True on the day it ends (to tell the player once).</summary>
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
