using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Population
{
    /// <summary>
    /// What people hear of a business (POPULATION.md section 6; the brief's sixth step): an interface every feature that draws attention to a business
    /// feeds (Flare, campaigns, Standing, brands), each a source with a name that answers, for a business of the game's, thousandths more (or fewer) of
    /// the business's pull on its pools. The population asks every source once a game day as the day's turn begins and adds the answers; a pool is only
    /// ever shared out, so attention moves people from rivals to the business and never makes money from nowhere. Today's levers are its first sources
    /// (Wiring/PopulationLinks: the game's own promotion since the anchor, and the per cents the mod's business features wrote into it before).
    /// A source that throws is taken off with one log line. Main thread only. COST: nothing per frame; a call a source a business once a game day.
    /// </summary>
    public static class PopulationAttention
    {
        private struct Source
        {
            public string Name;
            public Func<BuildingRegistration, int> PerMille;
        }

        private static readonly List<Source> Sources = new List<Source>(8);

        /// <summary>A source of attention: thousandths of pull more for a business (a source of the same name takes the old one's place).</summary>
        public static void Register(string name, Func<BuildingRegistration, int> perMille)
        {
            if (string.IsNullOrEmpty(name) || perMille == null)
            {
                return;
            }
            Unregister(name);
            Sources.Add(new Source { Name = name, PerMille = perMille });
        }

        /// <summary>A source taken off (its feature stood down).</summary>
        public static void Unregister(string name)
        {
            for (int i = Sources.Count - 1; i >= 0; i--)
            {
                if (string.Equals(Sources[i].Name, name, StringComparison.Ordinal))
                {
                    Sources.RemoveAt(i);
                }
            }
        }

        public static int Count => Sources.Count;

        /// <summary>A business's attention from every source, thousandths; a source that throws is taken off and named in <paramref name="fault"/>.</summary>
        public static int Of(BuildingRegistration reg, Action<string, Exception> fault)
        {
            int sum = 0;
            for (int i = Sources.Count - 1; i >= 0; i--)
            {
                try
                {
                    sum += Sources[i].PerMille(reg);
                }
                catch (Exception e)
                {
                    string name = Sources[i].Name;
                    Sources.RemoveAt(i);
                    fault?.Invoke(name, e);
                }
            }
            return PopulationCatalogue.Clamp(sum, PopulationCatalogue.AttentionLeast, PopulationCatalogue.AttentionMost);
        }

        /// <summary>Every source taken off (the city unloads).</summary>
        public static void Clear()
        {
            Sources.Clear();
        }
    }
}
