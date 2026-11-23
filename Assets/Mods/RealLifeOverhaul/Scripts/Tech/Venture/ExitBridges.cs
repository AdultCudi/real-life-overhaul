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

        /// <summary>The key of the text that says why the business cannot be handed over now (the player is inside it, a vehicle or gift is kept in it, the game is busy),
        /// or null when it can. Default: none, so it can.</summary>
        public static Func<string, string> HandOverRefusal;

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
        /// <summary>The job lead an acqui-hire gives (Careers): the buyer, whether its own office may have the post, a salt; true with the post's title, employer and
        /// opening's id and whether it is at the buyer, else false with the key of why not. Default: none.</summary>
        public delegate bool JobAsk(string buyer, bool office, int salt, out bool atBuyer, out string title, out string employer, out int opening, out string why);

        public static JobAsk Job;

        /// <summary>A founder of a city startup taken on by the buyer of their acqui-hired firm in the population: the startup's id, and the buyer's office (a street and a
        /// number, or none). True once their employer record moved. Default: none.</summary>
        public static Func<int, string, int, bool> FounderHired;

        /// <summary>The market's price of a listed company's stock now, real cents; 0 when it cannot be bought (the market is off, the stock is gone, the player is a guest). Default: none.</summary>
        public static Func<int, int> ShareQuote;

        /// <summary>
        /// Shares of a listed company put in the player's portfolio as part payment of a sale: the stock, the shares, the price of one in real cents and the day they stay locked
        /// until (0: free). True once they are in the portfolio. Default: none.
        /// </summary>
        public static Func<int, long, int, int, bool> ShareGrant;

        /// <summary>The tax withheld from a dividend of whole money paid to the player, as the stock market's dividends are taxed (the council's
        /// business tax moves the rate). Default: none withheld.</summary>
        public static Func<long, long> DividendTax;

        /// <summary>True while a business could be handed to a buyer now as a sale ends one: the sales of businesses run and the player is not a guest. Default: none.</summary>
        public static Func<string, bool> HandOverReady;

        /// <summary>True while a place is still a business of the player's. Default: none, which reads as gone.</summary>
        public static Func<string, bool> StillHeld;

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

        /// <summary>Why a business cannot be handed over now (a text key), or null when it can. A bridge that throws refuses without a reason of its own.</summary>
        public static string WhyNotHand(string place)
        {
            try
            {
                return HandOverRefusal != null ? HandOverRefusal(place) : null;
            }
            catch (Exception)
            {
                return GenericRefusal;
            }
        }

        public const string GenericRefusal = "reallife_exit_refuse_hand_over";

        /// <summary>What the stock market's own model makes of a firm's books (the game's statements at its place), cents at the start's prices; 0 where it cannot say.</summary>
        public static Func<string, long> BooksWorth;

        public static void Reset()
        {
            BooksWorth = null;
            Banks = null;
            Listed = null;
            HandOver = null;
            HandOverRefusal = null;
            Resident = null;
            People = null;
            Practised = null;
            Sessions = null;
            Fixtures = null;
            Job = null;
            FounderHired = null;
            ShareQuote = null;
            ShareGrant = null;
            HandOverReady = null;
            StillHeld = null;
            DividendTax = null;
        }
    }
}
