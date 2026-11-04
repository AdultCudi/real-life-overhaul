using System;
using RealLifeOverhaul.Economy.Dealership;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul.Careers
{
    /// <summary>A person of the city's one people pool: their id there, their name, and whether the game's name maker gave them a woman's.</summary>
    public struct PoolPerson
    {
        public int Id;           // 0: none, the name came from the mod's own catalogue (the living population is off)
        public string Name;
        public bool Female;
    }

    /// <summary>
    /// Where the work apps take the people they name from: the city's one people pool (Population's people, each with an id and a name made from its
    /// seed), the same one the property business takes its tenants from. A student, a tutor, a colleague, a rival applicant and a seller are each a
    /// person of the pool, picked by a hash of the save's seed and what they are for, so the same one is picked every time; the id is saved beside
    /// the name, and a page can open that person. While the living population is off (or in the offline checks) the pick is empty and the mod's own
    /// catalogue of names stands in, as it always has. Game-free: the game side installs <see cref="Pick"/> and <see cref="Find"/>
    /// (Careers/PeoplePoolGame.cs). Cost: a few array reads a call; nothing per frame.
    /// </summary>
    public static class PeoplePool
    {
        /// <summary>A grown person of the pool picked by a hash (the same hash, the same person); default when there is no pool.</summary>
        public static Func<long, PoolPerson> Pick;

        /// <summary>A person of the pool by their id; false when they are gone or there is no pool.</summary>
        public static Func<int, PoolPerson> Find;

        /// <summary>A person for a purpose: the pool's pick, else a name from the catalogue by the same hash.</summary>
        public static PoolPerson For(long hash)
        {
            PoolPerson p = Pick != null ? Pick(hash) : default(PoolPerson);
            if (p.Id == 0 || string.IsNullOrEmpty(p.Name))
            {
                int firsts = DealerCatalogue.FirstNames.Length, lasts = DealerCatalogue.LastNames.Length, total = firsts * lasts;
                int at = (int)((hash & long.MaxValue) % total);
                p = new PoolPerson { Id = 0, Name = DealerCatalogue.FirstNames[at % firsts] + " " + DealerCatalogue.LastNames[at / firsts % lasts] };
            }
            return p;
        }

        /// <summary>A person's name by id, or the name given when they are not in the pool.</summary>
        public static string NameOf(int id, string saved)
        {
            if (id > 0 && Find != null)
            {
                PoolPerson p = Find(id);
                if (p.Id == id && !string.IsNullOrEmpty(p.Name))
                {
                    return p.Name;
                }
            }
            return saved;
        }
    }
}
