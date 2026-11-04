using System;
using RealLifeOverhaul.Population;

namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// The game side of <see cref="PeoplePool"/>: the city's people (Population's person table) read for their id and the name the game's own name
    /// maker gives from their seed. Installed once when the mod starts (RealLifeOverhaulMod), before any director names a person; answers empty while the living population does not run.
    /// Cost: a walk of at most a few dozen slots for a pick; the name maker once per pick.
    /// </summary>
    internal static class PeoplePoolGame
    {
        private const int Reach = 48;       // slots looked at for a grown person from the slot a hash gives
        private const int Grown = 18;       // years

        internal static void Install()
        {
            PeoplePool.Pick = Pick;
            PeoplePool.InstallWait(() => Settings.LivingPopulation);
        }

        // A guest following the host has the host's copy, not people of its own.
        private static PopulationState Live => Settings.LivingPopulation && !CityPopulation.Following ? CityPopulation.Live : null;

        private static PoolPerson Pick(long hash)
        {
            try
            {
                PopulationState s = Live;
                if (s == null || s.People.Count == 0)
                {
                    return default(PoolPerson);
                }
                int count = s.People.Count;
                int at = (int)((hash & long.MaxValue) % count);
                for (int k = 0; k < Reach; k++)
                {
                    int slot = (at + k) % count;
                    if (s.People.Id[slot] != 0 && s.Age(slot, s.Day) >= Grown)
                    {
                        return Make(s, slot);
                    }
                }
                return default(PoolPerson);
            }
            catch (Exception e) when (Guard.Missing(e))
            {
                return default(PoolPerson);
            }
        }

        private static PoolPerson Make(PopulationState s, int slot)
        {
            return new PoolPerson { Id = s.People.Id[slot], Name = PopulationGame.NameOf(PopulationViews.NameSeed(s, slot), s.People.Female[slot] == 1) };
        }

        /// <summary>The names of the player's hired staff, at most a number of them (the employees who may follow a course).</summary>
        internal static void EmployeeNames(System.Collections.Generic.IList<string> into, int most)
        {
            var hired = new System.Collections.Generic.List<global::Entities.EmployeeInstance>(16);
            Tech.StaffNamesGame.Hired(hired, new System.Collections.Generic.List<bool>(16));
            for (int i = 0; i < hired.Count && into.Count < most; i++)
            {
                into.Add(Tech.StaffNamesGame.Name(hired[i]));
            }
        }

        /// <summary>Opens the people page on a person of the pool; false when there is no pool or they are gone.</summary>
        internal static bool Open(int id)
        {
            PopulationState s = Live;
            if (s == null || id <= 0)
            {
                return false;
            }
            for (int i = 0; i < s.People.Count; i++)
            {
                if (s.People.Id[i] == id)
                {
                    Ui.PopulationScreen.Want(ViewTarget.To(ViewKind.Person, i, id));
                    return Ui.ShellPages.OpenPeople();
                }
            }
            return false;
        }
    }
}
