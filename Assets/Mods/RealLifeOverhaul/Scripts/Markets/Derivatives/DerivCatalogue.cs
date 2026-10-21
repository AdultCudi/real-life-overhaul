using System;

namespace RealLifeOverhaul.Markets
{
    /// <summary>A group of raw materials that move together on the exchange. Saved as a number: never renumber.</summary>
    public enum MaterialGroup { Farm = 0, Food = 1, Drinks = 2, Metals = 3, Electronics = 4, Paper = 5, Other = 6, Count }

    /// <summary>
    /// Every figure of the raw materials exchange, its futures and the options on materials and shares. Game-free: the
    /// offline checks compile it and the Guide prints from it. Prices a unit are in micros (millionths of money), so a
    /// material at a hundredth of a unit keeps four figures; money held for margin is in cents; a market day is a game
    /// weekday as the stock market's (MarketCatalogue).
    /// </summary>
    public static class DerivCatalogue
    {
        public const long Micro = 1000000L;                // micros in one unit of money

        // The price process of a material: each market day's close moves the logarithm of its price against its own price
        // and the price level as an autoregression: x' = phi x + mu + sigma (rho zg + sqrt(1 - rho^2) zi), with mu set so
        // the price's long-run mean is exactly its own price at the price level. Normal draws only, so every forward and
        // option has an exact price.
        public static readonly int[] GroupSigmaBp = { 400, 300, 240, 320, 260, 220, 250 };   // a market day's move, bp of the price, by group
        public static readonly int[] GroupHalfLife = { 8, 10, 10, 14, 14, 12, 12 };          // market days for a move away from the mean to halve
        public const int GroupShareBp = 5000;              // of a day's move's variance shared with its group (rho squared)
        public const double XMost = 1.2;                   // ln: a guard only, ten long-run spreads and more away; never reached in play
        public const int ClosesKept = 30;                  // the last closes' prices kept for each material

        // The contracts: each settles at the close of a Friday, one to four weeks ahead. A material's future or option is for
        // the week after it: the price that close sets is what the importers charge until the next.
        public const int WeeksAhead = 4;
        public const int SettleWeekday = 4;                // Friday (0 is Monday)

        public static readonly long[] MaterialQuantities = { 500, 1000, 2500, 5000, 10000, 25000 };   // units on the ticket
        public static readonly long[] ShareQuantities = { 100, 250, 500, 1000, 2500, 5000 };          // shares on an option's ticket
        public static readonly int[] StrikeStepsBp = { -1000, -500, 0, 500, 1000 };                   // an option's strike against the price now

        public const long MaterialUnitsMost = 100000;      // units under contract on one material, every kind together
        public const int ContractsMost = 40;               // open at once
        public const int EndedKeptDays = 20;               // market days an ended contract stays on the tab

        // What a trade costs: the exchange's spread on either side of its fair price, and the broker's commission
        // (MarketCatalogue.CommissionBp, never less than its least) on a future's worth or an option's premium.
        public const int FutureHalfSpreadBp = 30;          // of the price
        public const int OptionMarkupBp = 800;             // of an option's fair price, on the ask...
        public const int OptionCutBp = 800;                // ...and off it on the bid, when one is sold back
        public const int OptionHalfSpreadBp = 15;          // of the material's price or the share's, on either side as well

        // A future's margin: held with the broker from the day it is bought; every close settles the day's move in it,
        // what is over the margin is paid back and a shortfall under the maintenance is called (MarketCatalogue's rules).
        public const int MarginSdTimes = 3;                // the margin covers this many spreads of a week's move...
        public const int MarginLeastPct = 10;              // ...never less than this of its worth...
        public const int MarginMostPct = 40;               // ...nor more
        public const int MaintenancePct = 75;              // of the margin: under this at a close, a call

        // Options on shares: only the city's own companies that trade enough and are not in play.
        public const int ShareListedDaysLeast = 20;        // market days on the market
        public const double ShareDistressMost = 0.15;      // MarketValuation.DistressFrom: no company that may fail
        public const int ShareOptionAdvPct = 50;           // shares under option on a company: at most this share of a day's usual volume...
        public const int ShareOptionHoldBp = 5000;         // ...and of the most a player may hold (MarketCatalogue.HoldingShareBp)
        public const double ShareVolSafety = 1.12;         // the spread of a share's moves as priced, against the market's own: its fat tails
        public const double ShareNewsPerDay = 0.008;       // more variance for each market day to its day, a share: the business's own news at the opens
        public const double ResultsExtraDays = 1.5;        // a results day's own variance counts as so many ordinary days' more
        public const double JumpSpread = 0.12;             // ln: how far a crisis or a windfall may land from its mean, beyond its own range
        public const int SettleSteps = 12;                 // a share option settles at the mean of the expiry day's last hour of steps
        public const int TestMovePct = 10;                 // the test tools: a futures' materials moved this much against them

        /// <summary>Which group each of the game's raw materials is in, by the game's item name; any other is Other.</summary>
        public static readonly string[][] GroupNames =
        {
            new[]
            {
                "ba:itemname_appleseeds", "ba:itemname_bananaseeds", "ba:itemname_carrotseeds", "ba:itemname_lettuceseeds", "ba:itemname_pearseeds", "ba:itemname_tomatoseeds",
                "ba:itemname_flowercheapseeds", "ba:itemname_flowerexpensiveseeds", "ba:itemname_barley", "ba:itemname_hops", "ba:itemname_grapes", "ba:itemname_blueagave",
                "ba:itemname_juniperberries", "ba:itemname_tealeaves", "ba:itemname_groundcoffeebeans", "ba:itemname_tobacco", "ba:itemname_onion", "ba:itemname_pepper",
                "ba:itemname_russetpotatoes", "ba:itemname_cornkernels"
            },
            new[]
            {
                "ba:itemname_milk", "ba:itemname_butter", "ba:itemname_cheese", "ba:itemname_chickenbreast", "ba:itemname_groundbeef", "ba:itemname_rawsausage", "ba:itemname_dough",
                "ba:itemname_bakingmix", "ba:itemname_yeast", "ba:itemname_sugar", "ba:itemname_vinaigrette"
            },
            new[]
            {
                "ba:itemname_water", "ba:itemname_carbondioxide", "ba:itemname_colaflavoring", "ba:itemname_flavorsyrup", "ba:itemname_limejuice", "ba:itemname_vermouth",
                "ba:itemname_caffeineextract"
            },
            new[] { "ba:itemname_metalwire", "ba:itemname_metalband", "ba:itemname_glass", "ba:itemname_clay", "ba:itemname_uncutgemscheap", "ba:itemname_uncutgemsexpensive" },
            new[]
            {
                "ba:itemname_battery", "ba:itemname_capacitors", "ba:itemname_resistors", "ba:itemname_transistors", "ba:itemname_integratedcircuits",
                "ba:itemname_coppercladlaminate", "ba:itemname_microphone", "ba:itemname_speaker"
            },
            new[]
            {
                "ba:itemname_paper", "ba:itemname_cigarettepaper", "ba:itemname_cigarpaper", "ba:itemname_fabriccheap", "ba:itemname_fabricexpensive", "ba:itemname_plastic",
                "ba:itemname_haircareformula"
            }
        };

        private const string P = "reallife";   // apart, so a key made of parts is not taken for a whole key

        public static readonly string[] GroupKeys =
        {
            P + "_mkt_drv_group_farm", P + "_mkt_drv_group_food", P + "_mkt_drv_group_drinks", P + "_mkt_drv_group_metals", P + "_mkt_drv_group_electronics",
            P + "_mkt_drv_group_paper", P + "_mkt_drv_group_other"
        };

        /// <summary>A material's group by its name; Other for a name the catalogue does not know.</summary>
        public static MaterialGroup GroupOf(string name)
        {
            for (int g = 0; g < GroupNames.Length; g++)
            {
                string[] names = GroupNames[g];
                for (int i = 0; i < names.Length; i++)
                {
                    if (string.Equals(names[i], name, StringComparison.Ordinal))
                    {
                        return (MaterialGroup)g;
                    }
                }
            }
            return MaterialGroup.Other;
        }

        private static readonly double[] Phis = PhiOfEach();

        private static double[] PhiOfEach()
        {
            var phis = new double[(int)MaterialGroup.Count];
            for (int g = 0; g < phis.Length; g++)
            {
                phis[g] = Math.Pow(0.5, 1.0 / Math.Max(1, GroupHalfLife[g]));
            }
            return phis;
        }

        /// <summary>A day's move of a group's materials, a share of the price.</summary>
        public static double Sigma(MaterialGroup group)
        {
            int g = Math.Max(0, Math.Min((int)MaterialGroup.Count - 1, (int)group));
            return GroupSigmaBp[g] / 10000.0;
        }

        /// <summary>How much of a move away from the mean is left after one close.</summary>
        public static double Phi(MaterialGroup group)
        {
            return Phis[Math.Max(0, Math.Min((int)MaterialGroup.Count - 1, (int)group))];
        }
    }
}
