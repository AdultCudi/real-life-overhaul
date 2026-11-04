using System;

namespace RealLifeOverhaul.Economy
{
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
}
