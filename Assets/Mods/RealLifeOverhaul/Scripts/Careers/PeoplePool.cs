using System;
using RealLifeOverhaul.Economy.Dealership;
using RealLifeOverhaul.Population;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul.Careers
{
    /// <summary>A person of the city's one people pool: their id there and their name.</summary>
    public struct PoolPerson
    {
        public int Id;           // 0: none, the name came from the mod's own catalogue (the living population is off)
        public string Name;
    }

    /// <summary>
    /// Where the work apps take the people they name from: the city's one people pool (Population's people, each with an id and a name made from its
    /// seed), the same one the property business takes its tenants from. A student, a tutor, a colleague, a rival applicant and a seller are each a
    /// person of the pool, picked by a hash of the save's seed and what they are for, so the same one is picked every time; the id is saved beside
    /// the name, and a page can open that person. While the living population is off (or in the offline checks) the pick is empty and the mod's own
    /// catalogue of names stands in, as it always has. Game-free: the game side installs <see cref="Pick"/> and the wait
    /// (<see cref="InstallWait"/>, Careers/PeoplePoolGame.cs). Cost: a few array reads a call; nothing per frame.
    /// </summary>
    public static class PeoplePool
    {
        /// <summary>A grown person of the pool picked by a hash (the same hash, the same person); default when there is no pool.</summary>
        public static Func<long, PoolPerson> Pick;

        /// <summary>True when the living population is meant to run (the setting is on, and this is not a guest following the host's city).</summary>
        public static Func<bool> Wanted;

        /// <summary>True when the pool has people to pick from now.</summary>
        public static Func<bool> Ready;

        /// <summary>
        /// Puts the wait in place: whether the population is meant to run and whether it is up. Called once when the mod starts (PeoplePoolGame.Install), before
        /// anything names a person, because a wait that is not installed answers "not waiting" and the first people of a session are named from the catalogue for good.
        /// </summary>
        public static void InstallWait(Func<bool> livingOn)
        {
            Wanted = () => livingOn() && !CityPopulation.Following;
            Ready = () => !CityPopulation.Following && CityPopulation.Live != null && CityPopulation.Live.People.Count > 0;
        }

        /// <summary>
        /// True while the population is meant to run but has not come up yet (it loads on a cycle of its own, seconds after the save): a person made now
        /// would get a name of the catalogue and no id for good, so whoever names people checks this and fills them in later (<see cref="Ready"/>).
        /// </summary>
        public static bool Waiting => Wanted != null && Wanted() && (Ready == null || !Ready());

        /// <summary>
        /// <see cref="Waiting"/>, for at most the given seconds from the first time it is asked (the clock is kept in <paramref name="since"/>, which starts at 0):
        /// a population that never comes up must not hold the work apps back for good.
        /// </summary>
        public static bool WaitingFor(ref long since, int seconds)
        {
            if (!Waiting)
            {
                return false;
            }
            if (since == 0)
            {
                since = System.Diagnostics.Stopwatch.GetTimestamp();
            }
            return System.Diagnostics.Stopwatch.GetTimestamp() - since < seconds * System.Diagnostics.Stopwatch.Frequency;
        }

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
    }
}
