using System;

namespace RealLifeOverhaul.Economy
{
    /// <summary>How one month of a unit's rent ended. Saved as a number.</summary>
    public enum PaymentState : byte
    {
        OnTime = 0,
        Late = 1,       // met, but not on the day
        Missed = 2      // still owed when the month ended
    }

    /// <summary>The city's inspection of a building, as the last one came out. Saved as a number.</summary>
    public enum InspectResult : byte
    {
        None = 0,
        Passed = 1,
        Failed = 2
    }

    /// <summary>What a tenant asks the landlord to improve. Saved as a number.</summary>
    public enum EstateImprove : byte
    {
        None = 0,
        Boiler = 1,
        Windows = 2,
        Kitchen = 3,
        Decorate = 4
    }

    /// <summary>One month of a unit's rent record: the day the month's rent was complete, the day it was met, what it was and how it ended.</summary>
    public struct RentPayment
    {
        public int Due, Paid, Amount;
        public byte State;
    }

    /// <summary>A planned visit by a contractor: what is serviced, the day, who comes (a business of the city, by its address key) and the fee.</summary>
    public sealed class ServiceBooking
    {
        public int Kind;            // a call kind, 1 to EstateCatalogue.CallKinds: the fault it keeps down
        public int Day;
        public string Contractor = string.Empty;
        public int Fee;
    }

    /// <summary>One person of the shared people pool, as a landlord needs to know them to let them a unit.</summary>
    public struct TenantFact
    {
        public int Id;
        public int Age;
        public bool Female;
        public bool Working;
        public int PayADay;         // dollars a day in a job; 0 out of one
        public bool Employee;       // one of the player's own staff
    }

    /// <summary>The people a unit can be let to: the living population's, behind a seam so the rules stay game-free.</summary>
    public interface ITenantPool
    {
        int Count { get; }
        TenantFact At(int index);
        bool Find(int id, out TenantFact fact);
    }

    /// <summary>
    /// Who lives in a let unit, and what comes of their job: a person of the people pool is drawn for each lease (the same on every
    /// machine), rent trouble follows a lost job or a wage the rent is too much of, and the unit's rent record keeps the last months of
    /// it. Every draw is a <see cref="EstateRules.Roll"/>. Game-free. COST: a scan of at most <see cref="ScanMost"/> people when a
    /// lease starts; a few integer sums a unit a morning.
    /// </summary>
    public static class TenantRules
    {
        public const int ScanMost = 96;
        private const int ForTenant = 71;

        /// <summary>
        /// The person for a new lease: an adult nobody else of the player's units is let to, in a job where they can afford the rent
        /// (the rent a day against their pay a day), else any such adult. 0 when the pool has nobody.
        /// </summary>
        public static int Pick(ITenantPool pool, int seed, int key, int day, int rentADay, Func<int, bool> taken)
        {
            int n = pool != null ? pool.Count : 0;
            if (n <= 0)
            {
                return 0;
            }
            int start = (int)((uint)EstateRules.Roll(seed, day, ForTenant, key) * 997u % (uint)n);
            int step = 1 + EstateRules.Roll(seed, key, ForTenant, day) % 7 * 2;   // odd: the walk skips about the pool, so neighbours in it are not always neighbours here
            int fallback = 0;
            int look = Math.Min(n, ScanMost);
            for (int i = 0; i < look; i++)
            {
                TenantFact f = pool.At((int)(((long)start + (long)i * step) % n));
                if (f.Id <= 0 || f.Age < EstateCatalogue.AdultAge || f.Age > EstateCatalogue.OldAge || (taken != null && taken(f.Id)))
                {
                    continue;
                }
                if (f.Working && Affordable(rentADay, f.PayADay))
                {
                    return f.Id;
                }
                fallback = fallback == 0 ? f.Id : fallback;
            }
            return fallback;
        }

        /// <summary>True when the rent a day is no more than the share of the person's pay a day that rent may take.</summary>
        public static bool Affordable(int rentADay, int payADay)
        {
            return payADay > 0 && (long)rentADay * 1000 <= (long)payADay * EstateCatalogue.TenantAffordMost;
        }

        /// <summary>
        /// Thousandths added to the chance of a missed day: with no job, a flat sum; in a job whose wage the rent takes too much of, more
        /// the further over. A person the pool no longer holds strains nothing: the lease keeps its record.
        /// </summary>
        public static int Strain(ITenantPool pool, int personId, int rentADay)
        {
            TenantFact f;
            if (pool == null || personId <= 0 || !pool.Find(personId, out f))
            {
                return 0;
            }
            if (!f.Working || f.PayADay <= 0)
            {
                return EstateCatalogue.TenantStrainJob;
            }
            long share = (long)rentADay * 1000 / f.PayADay;
            return share <= EstateCatalogue.TenantAffordMost ? 0
                : (int)Math.Min(EstateCatalogue.TenantStrainMost, (share - EstateCatalogue.TenantAffordMost) / 2);
        }

        /// <summary>The miss chance with a tenant's strain on it, in thousandths, never past a certain miss.</summary>
        public static int Strained(int chance, int strain)
        {
            return strain <= 0 ? chance : Math.Min(1000, chance + strain);
        }

        /// <summary>A lease starts: the person is chosen and written on the unit. True when one was found.</summary>
        public static bool Name(EstateHolding h, int unit, ITenantPool pool, int seed, int day, Func<int, bool> taken)
        {
            EstateUnit u = h.Unit(unit);
            if (u == null || !u.Let || u.PersonId > 0)
            {
                return false;
            }
            int id = Pick(pool, seed, EstateRules.UnitKey(h, unit), day, u.Rent, taken);
            if (id <= 0)
            {
                return false;
            }
            u.PersonId = id;
            u.PersonSince = day;
            return true;
        }

        /// <summary>The person a lease is let to is one of the player's own staff.</summary>
        public static bool IsEmployee(ITenantPool pool, int personId)
        {
            TenantFact f;
            return pool != null && personId > 0 && pool.Find(personId, out f) && f.Employee;
        }
    }

    /// <summary>A unit's rent record: the last months of rent, each with the day it was complete, the day it was met and how it ended. Game-free.</summary>
    public static class RentRecordRules
    {
        /// <summary>
        /// One morning of a let unit, after its tenancy's day: a missed day makes the running month late, arrears met (a lump) say on which
        /// day, and when the month is up it is written to the record, newest first, the oldest dropped.
        /// </summary>
        public static void Day(EstateUnit u, bool missed, int lump, int day, int monthDays)
        {
            if (u == null || !u.Let)
            {
                return;
            }
            monthDays = Math.Max(1, monthDays);
            if (u.PeriodFrom <= 0)
            {
                u.PeriodFrom = day;
            }
            if (missed)
            {
                u.PeriodLate++;
            }
            if (lump > 0 && u.PeriodLate > 0 && u.Arrears == 0)
            {
                u.PeriodPaid = day;
            }
            if (day - u.PeriodFrom + 1 < monthDays)
            {
                return;
            }
            PaymentState state = u.PeriodLate == 0 ? PaymentState.OnTime : u.Arrears > 0 ? PaymentState.Missed : PaymentState.Late;
            Push(u, new RentPayment
            {
                Due = day,
                Paid = state == PaymentState.Missed ? 0 : state == PaymentState.OnTime ? day : u.PeriodPaid > 0 ? u.PeriodPaid : day,
                Amount = Math.Max(0, u.Rent) * monthDays,
                State = (byte)state
            });
            u.PeriodFrom = day + 1;
            u.PeriodLate = 0;
            u.PeriodPaid = 0;
        }

        public static void Push(EstateUnit u, RentPayment p)
        {
            int keep = Math.Min(u.Payments.Length, u.PaymentCount + 1);
            for (int i = keep - 1; i > 0; i--)
            {
                u.Payments[i] = u.Payments[i - 1];
            }
            u.Payments[0] = p;
            u.PaymentCount = keep;
        }
    }

    /// <summary>
    /// Booked servicing: a planned visit that keeps a kind of fault down for a season (the matching call is not made on most days
    /// it would be). Game-free; the director pays the fee on the day and the contractor is a business of the city.
    /// </summary>
    public static class ServiceRules
    {
        private const int ForService = 72, ForContractor = 73;

        /// <summary>The fee of a visit for a kind of fault: a share of what a call for it would be quoted.</summary>
        public static int Fee(EstateHolding h, int kind, int index)
        {
            return Math.Max(1, EstateCatalogue.Share(EstateRules.CallCost(h, kind, index), EstateCatalogue.ServiceShare));
        }

        /// <summary>How long a service holds: a season of the game's year, never under a fortnight.</summary>
        public static int Season(int daysPerYear) => Math.Max(14, daysPerYear / 4);

        /// <summary>The earliest day a service can be booked for.</summary>
        public static int Earliest(int day) => day + EstateCatalogue.ServiceDaysAhead;

        /// <summary>The contractor for a visit: one of the city's businesses that do the trade, by a roll; empty when there are none.</summary>
        public static string Contractor(string[] firms, int seed, int key, int day)
        {
            return firms == null || firms.Length == 0 ? string.Empty : firms[EstateRules.Roll(seed, day, ForContractor, key) * firms.Length / 1000];
        }

        /// <summary>A visit is booked. False when the kind is none, the day is too soon, the building has the most booked or the kind is already booked.</summary>
        public static bool Book(EstateHolding h, int kind, int day, int bookDay, string contractor, int fee)
        {
            if (h == null || kind < 1 || kind > EstateCatalogue.CallKinds || bookDay < Earliest(day) || h.Services.Count >= EstateCatalogue.ServicesMost || fee < 1)
            {
                return false;
            }
            for (int i = 0; i < h.Services.Count; i++)
            {
                if (h.Services[i].Kind == kind)
                {
                    return false;
                }
            }
            h.Services.Add(new ServiceBooking { Kind = kind, Day = bookDay, Contractor = contractor ?? string.Empty, Fee = fee });
            h.Services.Sort((a, b) => a.Day.CompareTo(b.Day));
            return true;
        }

        public static bool Cancel(EstateHolding h, int index)
        {
            if (h == null || index < 0 || index >= h.Services.Count)
            {
                return false;
            }
            h.Services.RemoveAt(index);
            return true;
        }

        /// <summary>The first booked visit whose day has come, or null; nothing changed.</summary>
        public static ServiceBooking NextDue(EstateHolding h, int day)
        {
            for (int i = 0; h != null && i < h.Services.Count; i++)
            {
                if (h.Services[i].Day <= day)
                {
                    return h.Services[i];
                }
            }
            return null;
        }

        /// <summary>The visit is done and paid for: it is taken off the list and its kind held down for a season from the day.</summary>
        public static void Complete(EstateHolding h, ServiceBooking b, int day, int daysPerYear)
        {
            if (h == null || b == null || !h.Services.Remove(b))
            {
                return;
            }
            h.ServiceUntil[EstateCatalogue.Clamp(b.Kind, 0, EstateCatalogue.CallKinds)] = day + Season(daysPerYear);
        }

        /// <summary>True while a service holds the kind down.</summary>
        public static bool Holds(EstateHolding h, int kind, int day)
        {
            return h != null && kind >= 1 && kind <= EstateCatalogue.CallKinds && h.ServiceUntil[kind] >= day;
        }

        /// <summary>True when a call of this kind that would be made today is not: a service holds and the roll is under the cut.</summary>
        public static bool Averts(EstateHolding h, int kind, int seed, int day, int key)
        {
            return Holds(h, kind, day) && EstateRules.Roll(seed, day, ForService, key) < EstateCatalogue.ServiceCut;
        }
    }

    /// <summary>What a tenant asks for to stay content, and what answering does. Game-free.</summary>
    public static class ImproveRules
    {
        private const int ForAsk = 74, ForKind = 75;

        /// <summary>The quote for an improvement: a multiple of the quote for a call, by kind.</summary>
        public static int Quote(EstateHolding h, int kind, int index)
        {
            return Math.Max(1, EstateCatalogue.Share(EstateRules.CallCost(h, 1, index), EstateCatalogue.ImproveKindCost[EstateCatalogue.Clamp(kind, 0, EstateCatalogue.ImproveKinds)]));
        }

        /// <summary>A tenant who is not content asks for something, on a few mornings: nothing open, the unit let. True when they did.</summary>
        public static bool Ask(EstateHolding h, int unit, int seed, int day, EstateClimate c)
        {
            EstateUnit u = h.Unit(unit);
            if (u == null || !u.Let || u.Improve > 0 || u.Happy < 0 || u.Happy >= EstateCatalogue.ImproveBelow || u.Call > 0 || u.Arrears > 0 || h.Busy)
            {
                return false;
            }
            int key = EstateRules.UnitKey(h, unit);
            if (EstateRules.Roll(seed, day, ForAsk, key) >= EstateCatalogue.ImproveChance)
            {
                return false;
            }
            u.Improve = EstateRules.Between(EstateRules.Roll(seed, day, ForKind, key), 1, EstateCatalogue.ImproveKinds);
            u.ImproveDay = day;
            u.ImproveCost = Quote(h, u.Improve, c.Index);
            return true;
        }

        /// <summary>The request is met: the landlord pays; the tenant is glad and the building is better kept. Returns the satisfaction gained; the caller takes the money.</summary>
        public static int Accept(EstateHolding h, EstateUnit u)
        {
            if (h == null || u == null || u.Improve <= 0)
            {
                return 0;
            }
            if (u.Happy >= 0)
            {
                u.Happy = EstateCatalogue.Clamp(u.Happy + EstateCatalogue.ImproveGain, 0, 1000);
            }
            h.Cond = Math.Min(1000, h.Cond + (u.Improve == (int)EstateImprove.Decorate ? 30 : 60));
            u.Improve = u.ImproveDay = u.ImproveCost = 0;
            return EstateCatalogue.ImproveGain;
        }

        /// <summary>The request is refused: satisfaction falls and they are likelier to go.</summary>
        public static void Refuse(EstateUnit u)
        {
            if (u == null || u.Improve <= 0)
            {
                return;
            }
            if (u.Happy >= 0)
            {
                u.Happy = EstateCatalogue.Clamp(u.Happy - EstateCatalogue.ImproveRefusedLoss, 0, 1000);
            }
            u.Refused++;
            u.Improve = u.ImproveDay = u.ImproveCost = 0;
        }

        /// <summary>A request left too long lapses, with a small loss. True on the day it does.</summary>
        public static bool Lapse(EstateUnit u, int day)
        {
            if (u == null || u.Improve <= 0 || day - u.ImproveDay < EstateCatalogue.ImproveDays)
            {
                return false;
            }
            if (u.Happy >= 0)
            {
                u.Happy = EstateCatalogue.Clamp(u.Happy - EstateCatalogue.ImproveLapseLoss, 0, 1000);
            }
            u.Improve = u.ImproveDay = u.ImproveCost = 0;
            return true;
        }

        /// <summary>The thousandths a day added to a tenant's chance of giving notice by the requests they were refused.</summary>
        public static int QuitMore(EstateUnit u) => u == null ? 0 : u.Refused * EstateCatalogue.ImproveRefusedQuit;
    }

    /// <summary>
    /// What stands behind the four meters of a tenant's satisfaction on a unit's page: repairs (an open call), heating (a heating call
    /// open or the boiler not served), value for rent (the rent against the market) and condition (the building). Each is 0 to 100 and
    /// agrees with <see cref="EstateRules.HappyTarget"/>'s own terms. Game-free.
    /// </summary>
    public static class HappyTerms
    {
        public const int Heating = 1;   // the call kind for a heating fault

        public static void Of(EstateHolding h, int unit, int day, EstateClimate c, out int repairs, out int heating, out int value, out int condition)
        {
            EstateUnit u = h.Unit(unit);
            repairs = heating = value = condition = 0;
            if (u == null || !u.Let)
            {
                return;
            }
            int weight = EstateRules.CallWeight(u, day);
            repairs = EstateCatalogue.Clamp(100 - weight * 100 / Math.Max(1, EstateCatalogue.HappyCallMost), 0, 100);
            heating = u.Call == Heating ? Math.Min(repairs, 35) : ServiceRules.Holds(h, Heating, day) ? 100 : repairs < 100 ? Math.Max(repairs, 60) : 85;
            int market = EstateRules.UnitRent(h, c);
            if (market > 0)
            {
                long share = (long)u.Rent * 1000 / market;
                int term = EstateCatalogue.Clamp((int)((1000 - share) * EstateCatalogue.HappyRentSlope / 1000), EstateCatalogue.HappyRentLeast, EstateCatalogue.HappyRentMost);
                value = EstateCatalogue.Clamp((term - EstateCatalogue.HappyRentLeast) * 100 / (EstateCatalogue.HappyRentMost - EstateCatalogue.HappyRentLeast), 0, 100);
            }
            else
            {
                value = 50;
            }
            condition = EstateCatalogue.Clamp((EstateCatalogue.Clamp(h.Cond, 0, 1000) + h.Level * 60) / 10, 0, 100);
        }
    }
}

namespace RealLifeOverhaul.Economy
{
    /// <summary>What a unit is, from the game's floor area: its size, and for a home its bedrooms. Game-free.</summary>
    public static class UnitRules
    {
        /// <summary>Bedrooms of a home from its floor area in square metres: a studio under 35, then one for about every 20 over 30, to five.</summary>
        public static int Bedrooms(int sqm)
        {
            return sqm < 35 ? 0 : Math.Min(5, Math.Max(1, (sqm - 15) / 20));
        }

        /// <summary>The floor area of one unit of a building: the building's over its units, at least a square metre.</summary>
        public static int UnitSqm(int totalSqm, int units)
        {
            return Math.Max(1, totalSqm / Math.Max(1, units));
        }
    }
}
