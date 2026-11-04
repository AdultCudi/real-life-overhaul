using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The property business's morning as the director runs it, for what the interface conversion added: who the tenants are, what a month's
    /// servicing does, the rent record, and the city's rent ring. Game-free: the director calls these on its day turn with the game's money and
    /// notices as callbacks, and the run checks call the same methods over a hundred days. Nothing here allocates a list a day.
    /// </summary>
    public static class EstateDay
    {
        /// <summary>
        /// A managed building's tenants of the morning: every let unit has a person of the people pool (named once, the same on every machine, for
        /// units of an older save too), and their strain on the rent (a lost job, a wage the rent is too much of) is read.
        /// </summary>
        public static void People(EstateState s, EstateHolding h, int day, ITenantPool pool)
        {
            for (int n = 0; n < h.Units.Count; n++)
            {
                EstateUnit u = h.Units[n];
                if (!u.Let)
                {
                    u.Strain = 0;
                    continue;
                }
                if (u.PersonId <= 0 && pool != null)
                {
                    TenantRules.Name(h, n, pool, s.Seed, day, id => Taken(s, id));
                }
                u.Strain = pool != null ? TenantRules.Strain(pool, u.PersonId, u.Rent) : 0;
            }
        }

        /// <summary>A lease has just started: the tenant is named at once, so the page shows who it is.</summary>
        public static void Lease(EstateState s, EstateHolding h, int unit, int day, ITenantPool pool)
        {
            if (pool != null)
            {
                TenantRules.Name(h, unit, pool, s.Seed, day, id => Taken(s, id));
            }
        }

        /// <summary>Whether a person of the pool is the tenant of a unit of any building already.</summary>
        public static bool Taken(EstateState s, int id)
        {
            for (int i = 0; i < s.Holdings.Count; i++)
            {
                List<EstateUnit> units = s.Holdings[i].Units;
                for (int n = 0; n < units.Count; n++)
                {
                    if (units[n].PersonId == id)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// The visits booked for today: each is paid (<paramref name="pay"/> says whether the money was there) and done, and told
        /// (<paramref name="told"/>, with whether it was done). A visit the money does not cover waits a day. Returns the visits done.
        /// </summary>
        public static int Services(EstateHolding h, int day, int daysPerYear, Func<ServiceBooking, bool> pay, Action<ServiceBooking, bool> told)
        {
            int done = 0;
            ServiceBooking b = ServiceRules.NextDue(h, day);
            for (int guard = 0; b != null && guard < EstateCatalogue.ServicesMost; guard++)
            {
                if (!pay(b))
                {
                    b.Day = day + 1;
                    told(b, false);
                    break;
                }
                h.CallsYear += b.Fee;
                h.CostYear += b.Fee;
                ServiceRules.Complete(h, b, day, daysPerYear);
                told(b, true);
                done++;
                b = ServiceRules.NextDue(h, day);
            }
            return done;
        }

        /// <summary>The city's inspection of a let building whose day has come (or at once with <paramref name="now"/>): scheduled, judged, and the day and result kept on the building.</summary>
        public static Inspection Inspect(EstateState s, EstateHolding h, int day, EstateClimate c, bool now)
        {
            EstateRules.Schedule(h, s.Seed, day);
            Inspection r = EstateRules.Inspect(h, day, c, now);
            if (r.Done)
            {
                h.LastInspectDay = day;
                h.LastInspectScore = r.Score;
                h.LastInspect = (int)(r.Passed ? InspectResult.Passed : InspectResult.Failed);
            }
            return r;
        }

        /// <summary>One unit's day: the tenancy's rules (rent, arrears, the tenant's mood, an improvement asked for, quitting), and the payment into its record.</summary>
        public static TenancyDay Unit(EstateState s, EstateHolding h, int unit, int day, EstateClimate c, bool duties)
        {
            TenancyDay d = EstateRules.Day(h, unit, s.Seed, day, c, duties);
            RentRecordRules.Day(h.Units[unit], d.Missed, d.Lump, day, MacroCatalogue.MonthDays(c.DaysPerYear));
            return d;
        }

        /// <summary>The city's rents as read today: the month's mean flat rent into the ring, and the landlords' rents marked against the month before. True when a new month was written.</summary>
        public static bool Ring(EstateState s, RentMapData done)
        {
            int month = done.Day / Math.Max(1, done.MonthDays);
            if (RentRingRules.Month(s, month, Math.Max(0, done.MeanUnit((int)EstateUse.Homes))))
            {
                RentRingRules.Mark(s, done);
                return true;
            }
            return false;
        }
    }
}
