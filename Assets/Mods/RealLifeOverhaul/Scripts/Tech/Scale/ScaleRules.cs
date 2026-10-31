using System;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// The tech sector's money scale as rules (<see cref="ScaleCatalogue"/>): a product's budget and its scope, the audience
    /// it reaches for its marketing, how many of them buy at a quality and a price, what the stores, refunds and the engine
    /// take, and the band its return falls in; one market a kind shared by every product in it; a sale's day by day shape;
    /// the payback targets. The studio workstream calls <see cref="Units"/> with the population's audience. Whole numbers
    /// only, so every machine gets the same; nothing is kept. Game-free: EconomyCheck's TechScaleChecks holds it.
    /// </summary>
    public static class ScaleRules
    {
        /// <summary>A budget as a product's team, licences, marketing and launch make it: the team's pay a day over its days and the rest, cents.</summary>
        public static long Budget(long teamCentsADay, int days, long licences, long marketing, long launch)
        {
            return Math.Max(0L, teamCentsADay) * Math.Max(0, days) + Math.Max(0L, licences) + Math.Max(0L, marketing) + Math.Max(0L, launch);
        }

        /// <summary>The scope a budget is of: big from the big floor up, mid from the mid floor, indie below; one under the indie floor is still indie.</summary>
        public static Scope ScopeOf(long budget)
        {
            if (budget >= ScaleCatalogue.BudgetLeast[(int)Scope.Big])
            {
                return Scope.Big;
            }
            return budget >= ScaleCatalogue.BudgetLeast[(int)Scope.Mid] ? Scope.Mid : Scope.Indie;
        }

        /// <summary>
        /// The people a product of a budget usually reaches in the city and the world: its scope's people per thousand of
        /// budget. The studio workstream gives the population's own audience instead; this is the world the bands are set for.
        /// </summary>
        public static long TypicalAudience(long budget)
        {
            Scope s = ScopeOf(budget);
            return Math.Max(0L, budget) / 100 * ScaleCatalogue.PeoplePerMoney[(int)s] / 1000;
        }

        /// <summary>
        /// Thousandths of an audience a product is known to after a marketing spend (cents): the floor with none, rising
        /// with diminishing returns to the most; half way at <see cref="ScaleCatalogue.ReachHalfCents"/> a person.
        /// </summary>
        public static int Reach(long spend, long audience)
        {
            if (audience <= 0)
            {
                return 0;
            }
            long s = Math.Max(0L, spend);
            long half = audience * ScaleCatalogue.ReachHalfCents;
            int span = ScaleCatalogue.ReachMost - ScaleCatalogue.ReachFloor;
            return ScaleCatalogue.ReachFloor + (int)(span * s / (s + half));   // whole numbers: an audience of a billion at 60 cents and a spend of 150 million stay inside a long
        }

        /// <summary>The marketing spend (cents) that reaches a share of the audience (thousandths); nothing at or under the floor, and the most's spend can never be met.</summary>
        public static long SpendFor(int reach, long audience)
        {
            int span = ScaleCatalogue.ReachMost - ScaleCatalogue.ReachFloor;
            int over = reach - ScaleCatalogue.ReachFloor;
            if (audience <= 0 || over <= 0)
            {
                return 0;
            }
            if (over >= span)
            {
                return long.MaxValue / 4;
            }
            long half = audience * ScaleCatalogue.ReachHalfCents;
            return (half * over + span - over - 1) / (span - over);   // rounded up, so the spend found reaches the share asked
        }

        /// <summary>The spend of a step of a marketing plan (0 none to <see cref="ScaleCatalogue.MarketingStepsMost"/>) for an audience.</summary>
        public static long StepSpend(int step, long audience)
        {
            int s = Clamp(step, 0, ScaleCatalogue.MarketingStepsMost);
            int span = ScaleCatalogue.ReachMost - ScaleCatalogue.ReachFloor;
            return SpendFor(ScaleCatalogue.ReachFloor + span * ScaleCatalogue.MarketingReachStep[s] / 1000, audience);
        }

        /// <summary>A figure of a table by quality's tens (0, 10, ... 100), between two tens in a straight line.</summary>
        public static int ByQuality(int[] table, int quality)
        {
            int q = Clamp(quality, 0, 100), i = q / 10;
            return i >= 10 ? table[10] : table[i] + (table[i + 1] - table[i]) * (q - i * 10) / 10;
        }

        /// <summary>
        /// What a price does to the share who buy, thousandths, against the scope's usual price: about one and a half times
        /// as many lost as the price rises (a dearer product loses more than its price gains), held within the least and most.
        /// </summary>
        public static int PriceAppeal(long price, long usual)
        {
            if (price <= 0 || usual <= 0)
            {
                return 1000;
            }
            long f = usual * 1000 / price;                    // the plain ratio, thousandths
            long blend = (f + f * f / 1000) / 2;              // between the ratio and its square: an elasticity of about one and a half
            return (int)Math.Max(ScaleCatalogue.PriceAppealLeast, Math.Min(ScaleCatalogue.PriceAppealMost, blend));
        }

        /// <summary>
        /// The copies a product sells over its whole life: its audience, the share reached, the share of those who buy at its
        /// quality, its tail, how well it fits the audience's tastes (thousandths; the population's genre and platform
        /// shares, 1000 for the world's usual) and its price. Never more than the audience: the one cap there is.
        /// </summary>
        public static long Units(long audience, int reach, int quality, int fit, int priceAppeal)
        {
            if (audience <= 0)
            {
                return 0;
            }
            long u = audience * Clamp(reach, 0, 1000) / 1000;
            u = u * ByQuality(ScaleCatalogue.Conversion, quality) / 1000;
            u = u * ByQuality(ScaleCatalogue.Tail, quality) / 1000;
            u = u * Math.Max(0, fit) / 1000;
            u = u * Math.Max(0, priceAppeal) / 1000;
            return Math.Min(audience, u);
        }

        /// <summary>The copies sold in the launch window of a whole life's: the life over the tail.</summary>
        public static long LaunchUnits(long units, int quality)
        {
            return Math.Max(0L, units) * 1000 / ByQuality(ScaleCatalogue.Tail, quality);
        }

        /// <summary>The days for sales after the launch window to halve: longer for a better product.</summary>
        public static int HalfLife(int quality)
        {
            return ScaleCatalogue.HalfLifeBase + ScaleCatalogue.HalfLifePerPoint * Clamp(quality, 0, 100);
        }

        /// <summary>The money of the sales still to come that is sold today, as a share of what is left that halves over the half-life; one cent at least of what is left.</summary>
        public static long TailDay(long left, int halfLife)
        {
            if (left <= 0)
            {
                return 0;
            }
            long today = left * 693 / (Math.Max(1, halfLife) * 1000L);
            return Math.Min(left, Math.Max(1L, today));
        }

        /// <summary>What a firm keeps of its sales (cents): less the store's fee, the refunds, and a licensed engine's royalty past its free first million.</summary>
        public static long Net(long gross, Platform platform, bool licensedEngine)
        {
            long g = Math.Max(0L, gross);
            long kept = g * (1000 - ScaleCatalogue.PlatformFee[Clamp((int)platform, 0, (int)Platform.Count - 1)]) / 1000;
            kept -= g * ScaleCatalogue.Refunds / 1000;
            if (licensedEngine && g > ScaleCatalogue.EngineRoyaltyFree)
            {
                kept -= (g - ScaleCatalogue.EngineRoyaltyFree) * ScaleCatalogue.EngineRoyalty / 1000;
            }
            return Math.Max(0L, kept);
        }

        /// <summary>A month of an online product's servers for so many players still playing, cents.</summary>
        public static long Servers(long players)
        {
            return Math.Max(0L, players) * ScaleCatalogue.ServerCentsPerPlayerMonth;
        }

        /// <summary>A product's return, thousandths of its budget: what it kept over what it cost.</summary>
        public static int Return(long net, long budget)
        {
            return budget <= 0 ? 0 : (int)Math.Min(int.MaxValue / 4, Math.Max(0L, net) * 1000 / budget);
        }

        /// <summary>The band a return (thousandths of the budget) falls in.</summary>
        public static ReturnBand BandOf(int ret)
        {
            if (ret > ScaleCatalogue.HitMost)
            {
                return ReturnBand.Masterpiece;
            }
            if (ret >= ScaleCatalogue.HitLeast)
            {
                return ReturnBand.Hit;
            }
            if (ret > ScaleCatalogue.AverageMost)
            {
                return ReturnBand.Good;
            }
            if (ret >= ScaleCatalogue.AverageLeast)
            {
                return ReturnBand.Average;
            }
            return ret > ScaleCatalogue.FlopMost ? ReturnBand.Poor : ReturnBand.Flop;
        }

        /// <summary>
        /// A product's whole life with the usual world: its audience from its budget, its scope's usual marketing, price and a
        /// store, at a quality. The net money (cents) it keeps; the units and the return in thousandths of its budget by the outs.
        /// </summary>
        public static long Typical(long budget, int quality, out long units, out int ret)
        {
            Scope s = ScopeOf(budget);
            long audience = TypicalAudience(budget);
            long spend = budget * ScaleCatalogue.MarketingShare[(int)s] / 1000;
            units = Units(audience, Reach(spend, audience), quality, 1000, 1000);
            long net = Net(units * ScaleCatalogue.Price[(int)s], Platform.Pc, false);
            ret = Return(net, budget);
            return net;
        }

        /// <summary>
        /// One product's share of its kind's one market, thousandths: its appeal over every product's of the kind (rivals
        /// included) and the appeal of buying none. A share of one pool, so products of a kind never sell more together than
        /// the pool's people.
        /// </summary>
        public static int Share(long appeal, long allAppeal)
        {
            long a = Math.Max(0L, appeal), all = Math.Max(a, allAppeal);
            long whole = all + ScaleCatalogue.OutsideShare;
            return whole <= 0 ? 0 : (int)(a * 1000 / whole);
        }

        /// <summary>A product's quality on a roll: the team's mean with a spread close to a bell's (four rolls added), 0 to 100.</summary>
        public static int QualityRoll(int seed, int product, int mean)
        {
            int sum = TechRandom.Mille(seed, product, 1) + TechRandom.Mille(seed, product, 2) + TechRandom.Mille(seed, product, 3) + TechRandom.Mille(seed, product, 4);
            return Clamp(mean + (sum - 1998) * ScaleCatalogue.QualitySpread / 577, 0, 100);
        }

        /// <summary>True if a payback (days) is inside its kind's target.</summary>
        public static bool OnTarget(PaybackKind kind, int days)
        {
            int k = Clamp((int)kind, 0, (int)PaybackKind.Count - 1);
            return days >= ScaleCatalogue.PaybackLeast[k] && days <= ScaleCatalogue.PaybackMost[k];
        }

        private static int Clamp(int v, int least, int most)
        {
            return v < least ? least : v > most ? most : v;
        }
    }
}
