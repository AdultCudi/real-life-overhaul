using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Economy
{
    /// <summary>The city's inspection of a building, as the last one came out. Saved as a number.</summary>
    public enum InspectResult : byte
    {
        None = 0,
        Passed = 1,
        Failed = 2
    }

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

        /// <summary>What came of answering a tenant's request.</summary>
        public enum AnswerResult
        {
            Done,       // paid for or refused, and the unit's record changed
            Nothing,    // no request open, no unit, or the landlord's duties are off
            Short       // the money was not there: the request stands
        }

        /// <summary>
        /// A tenant's request for an improvement is met (<paramref name="pay"/> takes the money, true when it was there) or refused. The same
        /// function the director's answer and the check's pressed button call.
        /// </summary>
        public static AnswerResult Answer(EstateHolding h, EstateUnit u, bool accept, bool duties, Func<int, bool> pay)
        {
            if (h == null || u == null || !duties || u.Improve <= 0)
            {
                return AnswerResult.Nothing;
            }
            if (accept)
            {
                int cost = u.ImproveCost;
                if (!pay(cost))
                {
                    return AnswerResult.Short;
                }
                h.CostYear += cost;
                h.CallsYear += cost;
                ImproveRules.Accept(h, u);
            }
            else
            {
                ImproveRules.Refuse(u);
            }
            return AnswerResult.Done;
        }

        /// <summary>A service is booked for a kind of fault: the earliest day, the city's contractor by a roll, the fee of a visit. False when it cannot be.</summary>
        public static bool BookService(EstateHolding h, int kind, int day, int seed, string[] firms, int index, bool duties)
        {
            if (h == null || !duties)
            {
                return false;
            }
            string who = ServiceRules.Contractor(firms, seed, EstateRules.Hash(h.Place), day + kind);
            return ServiceRules.Book(h, kind, day, ServiceRules.Earliest(day), who, ServiceRules.Fee(h, kind, index));
        }

        /// <summary>The booking for a kind of fault is cancelled (by the kind, so a morning's re-sorting of the list never cancels another).</summary>
        public static bool CancelService(EstateHolding h, int kind)
        {
            return h != null && ServiceRules.CancelKind(h, kind);
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
    /// <summary>
    /// The city's rents as the day's read holds them: the last read and the one before it (its room), the day of the read in hand. A city
    /// unloaded or another save loaded resets it, so the first city's rents never show in the second. Game-free.
    /// </summary>
    public sealed class RentSlot
    {
        public RentMapData Now = new RentMapData(), Next = new RentMapData();
        public int Day = -1;

        /// <summary>Whether the read of a day has yet to be begun.</summary>
        public bool Due(int day) => Day != day;

        /// <summary>A read of a day is begun; the data goes into the room the read before it left.</summary>
        public RentMapData Begin(int day)
        {
            Day = day;
            return Next;
        }

        /// <summary>A read is done: it becomes the rents, and the ones before it become the next read's room.</summary>
        public void Finish(RentMapData done)
        {
            Next = Now;
            Now = done;
        }

        public void Reset()
        {
            Now = new RentMapData();
            Next = new RentMapData();
            Day = -1;
        }
    }
}
