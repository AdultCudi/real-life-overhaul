using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// What exits ask of the rest of the mod, filled in by Wiring/ExitLinks (the stock market, the sales of businesses, the population, the careers and the
    /// desk sessions). Each is optional: without it the part that needs it says so and does nothing, and the rest works.
    /// </summary>
    public static class ExitBridges
    {
        /// <summary>The city's largest banks, the largest first (the stock market's own list). Default: none.</summary>
        public static Action<List<string>> Banks;

        /// <summary>The listed companies that could buy a tech firm, as a stock id and a name each. Default: none.</summary>
        public static Action<List<KeyValuePair<int, string>>> Listed;

        /// <summary>A business of the player's handed to its buyer as the game's sale of a business ends one (its place, the buyer's name); true once it is not the player's. Default: none.</summary>
        public static Func<string, string, bool> HandOver;

        /// <summary>True while the business could be handed over now (the player is not inside it, no vehicle or gift in it). Default: true.</summary>
        public static Func<string, bool> CanHandOver;

        /// <summary>The population's record of a founder, by the key they were linked under; false while none is linked or the population is off. Default: none.</summary>
        public delegate bool ResidentAsk(string key, out ResidentInfo info);

        public static ResidentAsk Resident;

        /// <summary>True while the living population runs, so a founder may be one of its people. Default: false.</summary>
        public static Func<bool> People;

        /// <summary>The player learns a skill by doing (the careers' own learning at work): the skill and the hours. Default: nothing.</summary>
        public static Action<int, int> Practised;

        /// <summary>The sessions of exits put in (true) or taken out of the registry as the venture part starts and stops. Default: nothing.</summary>
        public static Action<bool> Sessions;

        /// <summary>A business's stock and furniture at the game's resale price, in dollars (a sale of a business's cost); 0 when unreadable. Default: none.</summary>
        public static Func<string, int> Fixtures;

        public static string KeyOf(int startup) => "founder:" + startup.ToString(System.Globalization.CultureInfo.InvariantCulture);

        public static bool ResidentOf(int startup, out ResidentInfo info)
        {
            info = default(ResidentInfo);
            try
            {
                return Resident != null && Resident(KeyOf(startup), out info);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static bool PeopleRun()
        {
            try
            {
                return People != null && People();
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static bool CanHand(string place)
        {
            try
            {
                return CanHandOver == null || CanHandOver(place);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static void Reset()
        {
            Banks = null;
            Listed = null;
            HandOver = null;
            CanHandOver = null;
            Resident = null;
            People = null;
            Practised = null;
            Sessions = null;
            Fixtures = null;
        }
    }
}
