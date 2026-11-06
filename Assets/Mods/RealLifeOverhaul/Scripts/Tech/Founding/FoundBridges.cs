using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// What founding asks of the rest of the mod, filled in by Wiring/Wiring.FoundingLinks. Each is optional with a default, and a fault in the other feature reads as the default.
    /// The city's people for co-founders and the businesses' staff and rents come from the population; the player's skills grow through the study's own route; a failed firm
    /// lets a partner down through family life. Game-free.
    /// </summary>
    public static class FoundBridges
    {
        /// <summary>
        /// People of the city's population who could found a firm, from its records: at most so many into the list (cleared); how many. Default: none, and the population is not running.
        /// </summary>
        public delegate int PeopleAsk(int seed, int day, List<CofounderOffer> into, int most);

        public static PeopleAsk People;

        /// <summary>The population's part of a niche census: the jobs at each niche's businesses, the rents and the buildings that let flats. Default: nothing added.</summary>
        public static Action<NicheCensus> Census;

        /// <summary>The player's skill grows (a TalentCatalogue skill, thousandths of a point), through the study's own route. Default: nothing.</summary>
        public static Action<int, int> Learn;

        /// <summary>A partner let down by a firm that lost the family's money: points of trust and of affection lost. Default: nothing.</summary>
        public static Action<int, int> LetDown;

        /// <summary>The go to market sessions put in (true) or taken out of the sessions' registry as the venture part starts and stops. Default: nothing.</summary>
        public static Action<bool> Sessions;

        /// <summary>True while the city's population runs and its records can be read. Default: not running.</summary>
        public static Func<bool> PopulationOn;

        public static bool PopulationRuns
        {
            get
            {
                try
                {
                    return PopulationOn != null && PopulationOn();
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        public static int PeopleNow(int seed, int day, List<CofounderOffer> into, int most)
        {
            try
            {
                return People != null ? Math.Max(0, People(seed, day, into, most)) : 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        public static void CensusNow(NicheCensus c)
        {
            try
            {
                Census?.Invoke(c);
            }
            catch (Exception)
            {
                // the population's fault: the game's own businesses stand
            }
        }

        public static void LearnNow(int skill, int thousandths)
        {
            try
            {
                Learn?.Invoke(skill, thousandths);
            }
            catch (Exception)
            {
                // the study's fault: the session still counts
            }
        }

        public static void LetDownNow(int trust, int affection)
        {
            try
            {
                LetDown?.Invoke(trust, affection);
            }
            catch (Exception)
            {
                // family life's fault: no strain is added
            }
        }

        public static void SessionsNow(bool on)
        {
            try
            {
                Sessions?.Invoke(on);
            }
            catch (Exception)
            {
                // the sessions' fault: going to market goes on from the firm's page
            }
        }

        public static void Reset()
        {
            People = null;
            Census = null;
            Learn = null;
            LetDown = null;
            Sessions = null;
            PopulationOn = null;
        }
    }
}
