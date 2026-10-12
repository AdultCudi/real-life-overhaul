using System;

namespace RealLifeOverhaul.Utilities
{
    /// <summary>
    /// The rules of the city's internet (InternetCatalogue), game-free and checked by EconomyCheck: where a provider
    /// reaches, what a plan costs this month, a plan bought and installed, the fee for leaving a contract early, the
    /// month's price moving, and the outages. Every draw comes from the save's seed, the day and the provider (Draw):
    /// the same day of the same save always has the same outages and prices, so reloading changes nothing.
    /// </summary>
    public static class InternetRules
    {
        public static int Count => InternetCatalogue.Providers.Length;

        public static Provider Get(int provider)
        {
            return provider >= 0 && provider < InternetCatalogue.Providers.Length ? InternetCatalogue.Providers[provider] : null;
        }

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
