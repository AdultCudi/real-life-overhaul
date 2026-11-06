using System;

namespace RealLifeOverhaul.Construction
{
    /// <summary>Where a trade in land stands (the land market, docs/features/construction.md).</summary>
    public enum LandOfferState
    {
        Waiting = 0,    // made, to be answered on its day
        Accepted = 1,   // the land has changed hands
        Refused = 2,    // the other side said no
        Lapsed = 3,     // it came to nothing: the works began, the land was used, the money was not there, or no one answered in time
        Withdrawn = 4   // the player took it back
    }

    /// <summary>Whose offer it is.</summary>
    public enum LandOfferSide
    {
        Mine = 0,       // the player's offer to buy land a rival developer holds
        Theirs = 1      // a rival developer's offer to buy land of the player's
    }

    /// <summary>What a rival developer wants the player's land for.</summary>
    public enum LandReason
    {
        Blocks = 0,     // it stands in the way of a site the developer wants: free land lies beside it
        Joins = 1       // it lies beside a site the developer already holds or has built
    }

    /// <summary>How a rival developer answered an offer for its land.</summary>
    public enum LandAnswer
    {
        Accept = 0,
        Refuse = 1,
        Lapse = 2       // its works had begun or its plans were dropped by the day it was answered
    }

    /// <summary>
    /// The land market (docs/features/construction.md, "Land market"), game-free and checked offline (EconomyCheck/LandMarketChecks.cs):
    /// what the city pays for land the player sells back, what a rival developer asks for land it holds (what it paid and a premium
    /// by how far its plans went, its only site and its means), how it answers an offer, how often the player may offer, what a rival
    /// pays for the player's land, and how often the rivals trade at all. Every draw is from the save's seed and the day, so the same
    /// on every machine. COST: nothing per frame; each is a few multiplications on a press or the day's pass.
    /// </summary>
    public static class LandMarketRules
    {
        // ---- the city buys land back
        public const int BackPermille = 850;           // of what was paid: the least the city gives for land sold back (the market value is the other bound)

        // ---- what a rival developer asks
        public const int PremiumLeast = 150;           // thousandths over what it paid: when its plans have only begun
        public const int PremiumMost = 350;            // ... when its application is about to be decided
        public const int OnlySiteMore = 50;            // thousandths more when this is the only site it holds
        public const int ShortLess = 60;               // thousandths less when it is short of cash
        public const int PremiumFloor = 100, PremiumCeil = 450;   // thousandths: the premium stays within these whatever else is said
        public const int RangeLeast = 40, RangeSpread = 70;       // thousandths: the range shown starts this far under the ask and this far over it, drawn from the site
        public const int PricePoints = 7;              // prices to choose between in the offer's selector

        // ---- the player's offers
        public const int GapDays = 4;                  // days between two offers for one lot
        public const int RaisesMost = 3;               // times a refused offer is raised straight away
        public const int RestDays = 6;                 // days the developer will not talk after the third raise is refused
        public const int KeptMost = 40;                // offers kept in the save: those waiting, then the newest answered

        // ---- a rival developer's means (it holds no purse of its own in the save: its means come from its record)
        public const int MeansBase = 2000000;          // money at the start's prices
        public const int MeansPerReputation = 40000;   // ... for each point of reputation
        public const int MeansPerBuilt = 300000;       // ... for each building it has put up
        public const int MeansPerWon = 150000;         // ... for each tender it has won

        // ---- the rivals' side
        public const int OfferDays = 7;                // days the player has to answer a rival's offer
        public const int OfferRest = 20;               // days after a refusal before a rival offers on the same land again
        public const int UnusedDays = 14;              // days the player's land must have stood with nothing on it before a rival offers
        public const int BlocksLeast = 200, BlocksMost = 300;   // thousandths over the land's market value for land that stands in the way
        public const int JoinsLeast = 100, JoinsMost = 200;     // ... for land beside a site of its own
        public const int TradePremium = 100;           // thousandths over what it paid: a rival buying land another rival must sell
        public const int GapLeast = 8, GapMost = 12;   // days between two moves of the rivals' land trading, all kinds together
        public const int MonthMost = 3;                // moves in any thirty days, at the very most
        public const int MonthDays = 30;
        public const int FirstDay = 5;                 // days after a save first meets the market before its first move

        /// <summary>
        /// What the city pays for land sold back: the higher of 85% of what was paid and the land's market value now (its district,
        /// spaces and rows), nothing taken off. Whole money.
        /// </summary>
        public static long CityPrice(long paid, long marketValue)
        {
            long floor = paid > 0L ? paid * BackPermille / 1000L : 0L;
            return Math.Max(floor, Math.Max(0L, marketValue));
        }

        /// <summary>Why the city will not buy a site now (a text key), or null. Land is bought back only while nothing is planned, permitted or under way on it.</summary>
        public static string SellWhy(bool inSession, bool found, bool planned, bool built, long price)
        {
            if (inSession)
            {
                return "reallife_bld_answer_session_land";
            }
            if (!found)
            {
                return "reallife_bld_answer_no_lot";
            }
            if (planned)
            {
                return "reallife_land_answer_sell_busy";
            }
            if (built)
            {
                return "reallife_land_answer_sell_built";
            }
            return price <= 0L ? "reallife_bld_answer_no_price" : null;
        }

        /// <summary>
        /// How far a rival's plans have gone, 0 to 1000, from the day its step ends: its architect's days first (0 to 400), then City Hall's
        /// wait (400 to 1000). Read from the end day alone, so it is the same whatever the designs have read.
        /// </summary>
        public static int Progress(bool planning, int day, int until)
        {
            int span = planning ? BuildCatalogue.PlanningDaysMost * 7 / 5 + 1 : GrowRules.PlansDaysLeast + GrowRules.PlansDaysSpread;
            int share = BuildCatalogue.Clamp(1000 - Math.Max(0, until - day) * 1000 / Math.Max(1, span), 0, 1000);
            return planning ? 400 + share * 600 / 1000 : share * 400 / 1000;
        }

        /// <summary>
        /// The premium a rival asks over what it paid, in thousandths: from 15% to 35% as its plans go from begun to about to be decided,
        /// 5 points more when it is its only site, 6 less when it is short of cash, never under 10% or over 45%.
        /// </summary>
        public static int Premium(int progress, bool onlySite, bool shortOfCash)
        {
            int p = PremiumLeast + (PremiumMost - PremiumLeast) * BuildCatalogue.Clamp(progress, 0, 1000) / 1000;
            p += onlySite ? OnlySiteMore : 0;
            p -= shortOfCash ? ShortLess : 0;
            return BuildCatalogue.Clamp(p, PremiumFloor, PremiumCeil);
        }

        /// <summary>The rival's ask: what it paid and the premium, in whole thousands rounded up (at least a thousand).</summary>
        public static long Ask(long paid, int premium)
        {
            long raw = Math.Max(1L, paid) * (1000L + premium) / 1000L;
            long round = BuildCatalogue.LandRound;
            return Math.Max(round, (raw + round - 1) / round * round);
        }

        /// <summary>
        /// The range of the ask the player is shown: the ask lies inside it, but not in its middle. The low end is 4% to 11% under the ask,
        /// the high end 4% to 11% over it, drawn from the site. Whole thousands.
        /// </summary>
        public static void Range(long ask, int seed, int site, out long low, out long high)
        {
            BuildRandom r = BuildRandom.Of(seed, site, 0x3A51);
            int under = RangeLeast + r.Range(0, RangeSpread + 1), over = RangeLeast + r.Range(0, RangeSpread + 1);
            long round = BuildCatalogue.LandRound;
            low = Math.Max(round, ask * (1000L - under) / 1000L / round * round);
            high = Math.Max(low + round, (ask * (1000L + over) / 1000L + round - 1) / round * round);
        }

        /// <summary>The prices the offer's selector steps through: a tenth under the range to a tenth over it, whole thousands, ascending.</summary>
        public static long PriceAt(long low, long high, int step)
        {
            int n = BuildCatalogue.Clamp(step, 0, PricePoints - 1);
            long from = low * 900L / 1000L, to = high * 1100L / 1000L;
            long round = BuildCatalogue.LandRound;
            long price = from + (to - from) * n / (PricePoints - 1);
            return Math.Max(round, price / round * round);
        }

        /// <summary>The step whose price is nearest above the range's middle, where the selector starts.</summary>
        public static int StartStep(long low, long high)
        {
            long mid = (low + high) / 2L;
            for (int i = 0; i < PricePoints; i++)
            {
                if (PriceAt(low, high, i) >= mid)
                {
                    return i;
                }
            }
            return PricePoints - 1;
        }

        /// <summary>
        /// A rival's means: what it can put into land, from its reputation and what it has built and won, at today's prices
        /// (<paramref name="prices"/> in thousandths of the start's).
        /// </summary>
        public static long Means(int reputation, int built, int won, int prices)
        {
            long means = MeansBase + (long)MeansPerReputation * Math.Max(0, reputation) + (long)MeansPerBuilt * Math.Max(0, built) + (long)MeansPerWon * Math.Max(0, won);
            return means * Math.Max(1, prices) / 1000L;
        }

        /// <summary>True when the land a rival holds, at what it paid, is more than its means.</summary>
        public static bool Short(long held, long means) => held > means;

        /// <summary>
        /// How a rival answers an offer: it accepts a price at or over its ask, refuses a lower one, and the offer lapses when its works
        /// have begun or its plans were dropped by the day it is answered.
        /// </summary>
        public static LandAnswer Decide(long price, long ask, bool started, bool gone)
        {
            if (started || gone)
            {
                return LandAnswer.Lapse;
            }
            return price >= ask ? LandAnswer.Accept : LandAnswer.Refuse;
        }

        /// <summary>The day a rival answers an offer made on a day: the next working day.</summary>
        public static int AnswerDay(int day) => PlanningRules.AfterWorkingDays(day, 1);

        /// <summary>
        /// Why the player cannot offer for a rival's land now (a text key), or null. Never in a Going Public session; only while the rival
        /// has not begun works (its plans may be at any step); one offer waits at a time; one offer for a lot every few days, but a refused
        /// offer may be raised at once, three times; the money must be there and a lot to hold the land.
        /// </summary>
        public static string OfferWhy(bool inSession, bool started, bool gone, bool waiting, int day, int lastDay, bool lastRefused, long lastPrice, int lastRaised, long price, long cash, int lots)
        {
            if (inSession)
            {
                return "reallife_bld_answer_session_land";
            }
            if (started || gone)
            {
                return "reallife_land_answer_started";
            }
            if (waiting)
            {
                return "reallife_land_answer_waiting";
            }
            if (lots >= BuildCatalogue.LotsMost)
            {
                return "reallife_bld_answer_too_many_lots";
            }
            if (price <= 0L)
            {
                return "reallife_land_answer_price";
            }
            if (lastDay > 0)
            {
                bool raise = lastRefused && price > lastPrice && lastRaised < RaisesMost;
                int gap = lastRefused && lastRaised >= RaisesMost ? RestDays : GapDays;
                if (!raise && day - lastDay < gap)
                {
                    return "reallife_land_answer_rest";
                }
            }
            return cash < price ? "reallife_bld_answer_no_money" : null;
        }

        /// <summary>What a rival pays for land of the player's: its market value and a premium by why it wants it, drawn from the offer's number. Whole thousands.</summary>
        public static long OfferPrice(long marketValue, LandReason reason, int seed, int offer)
        {
            int least = reason == LandReason.Blocks ? BlocksLeast : JoinsLeast, most = reason == LandReason.Blocks ? BlocksMost : JoinsMost;
            int premium = least + BuildRandom.Of(seed, offer, 0x3A52).Range(0, most - least + 1);
            long round = BuildCatalogue.LandRound;
            return Math.Max(round, (Math.Max(1L, marketValue) * (1000L + premium) / 1000L + round / 2) / round * round);
        }

        /// <summary>True when the player's land has stood unused long enough for a rival to offer on it.</summary>
        public static bool Unused(int emptySince, int day) => emptySince > 0 && day - emptySince >= UnusedDays;

        /// <summary>The last day the player may answer an offer made on a day.</summary>
        public static int Due(int day) => day + OfferDays;

        /// <summary>True while a rival leaves a lot alone after a refusal.</summary>
        public static bool Resting(int refusedDay, int day) => refusedDay > 0 && day - refusedDay < OfferRest;

        /// <summary>What a rival pays another rival for a site it must sell: what the seller paid and a tenth.</summary>
        public static long TradePrice(long paid)
        {
            return Ask(paid, TradePremium);
        }

        /// <summary>The day of the next move of the rivals' land trading, from the save's seed and the day.</summary>
        public static int NextMove(int seed, int day)
        {
            return day + GapLeast + BuildRandom.Of(seed, day, 0x3A53).Range(0, GapMost - GapLeast + 1);
        }

        /// <summary>True when the moves of the last thirty days already number the most there may be.</summary>
        public static bool MonthFull(int[] moveDays, int count, int day)
        {
            int n = 0;
            for (int i = 0; moveDays != null && i < count && i < moveDays.Length; i++)
            {
                n += moveDays[i] > 0 && day - moveDays[i] < MonthDays ? 1 : 0;
            }
            return n >= MonthMost;
        }

        /// <summary>Which kind of move the rivals make on a day: 0 sell a site they cannot afford, 1 offer on the player's land.</summary>
        public static int MoveKind(int seed, int day)
        {
            return BuildRandom.Of(seed, day, 0x3A54).Range(0, 2);
        }
    }
}
