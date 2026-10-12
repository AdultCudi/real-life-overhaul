using System;

namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// What a share is worth, from the business as the market knows it. Game-free.
    ///
    ///     worth = the profits to come that the company pays out, each discounted: fifteen years in which its own growth
    ///             fades towards the city's long run (over about ten years), a good or bad patch (the cycle, its own ups
    ///             and downs in its margin) fades within about a year, inflation comes back to its target and its debt
    ///             and what the debt costs come back to its usual; then the rest for ever, at its usual margin and the
    ///             long run's growth
    ///     cost of equity = the bank rate (half today's with half the next decision, half the long run) + beta x the
    ///                      equity premium (more in bad times) + a premium for small companies
    ///     the rate it is discounted at = the cost of equity, and what its price must make up on the days nothing
    ///                      happens for what may happen on one: the chance it fails in each year to come (from its margin
    ///                      and debt as they are expected to go, and what holders lose then), less the chance it is bid
    ///                      for (and the premium), more the market's share of a crash that stays, more a crisis of its own
    ///                      less a windfall
    ///     a business expected to lose money for good is still worth a little: it may yet turn round (its worth never
    ///                      quite reaches nothing, smoothly, so every change in its prospects still moves it)
    ///
    /// The market prices what is known the moment it is known: a change in the city moves the worth at once, and what
    /// is expected to follow from it (the patch fading, the debt coming back to its usual, the next state of the
    /// economy as the city's own odds have it) is in the worth already. So a share's expected return, failures,
    /// takeovers and crashes counted, is its cost of equity, and nothing that can be read in the city or the figures
    /// tells where a price is going (the checks measure it over thousands of days). A higher bank rate lowers every
    /// price, a growing company is dearer, a company with debt and thin margins is cheap and may fail. Prices follow
    /// the city's inflation through the companies' turnover and costs, never through a multiplier of their own.
    /// </summary>
    public static class MarketValuation
    {
        public const double EquityPremium = 0.04;        // a year, for the whole market (beta 1) in ordinary times
        public const double NeutralRate = 0.03;          // the bank rate the long run comes back to
        public const double LongRateShare = 0.5;         // of today's rate in the rate a share is discounted at (the rest is the long run)
        public const double SpreadLeast = 0.04;          // the rate a share is discounted at is at least this over long-run growth
        public const double TargetInflation = 0.02;
        public const double InflationFadeYears = 2.0;
        public const double PatchFadeYears = 1.0;        // a good or bad patch is over in about a year
        public const double SupplyYears = 0.05;          // a kind of goods dear or cheap: the market expects it to last about this long
        public const int Horizon = 15;                   // years of fading growth before the rest is valued as one

        private static readonly double[] OwnFade = Fade(MarketCatalogue.GrowthFadeYears);
        private static readonly double[] PatchFade = Fade(PatchFadeYears);
        private static readonly double[] PriceFade = Fade(InflationFadeYears);

        // How much of something that fades is left, on average over each year to come (the middle of the year).
        private static double[] Fade(double years)
        {
            var f = new double[Horizon + 1];
            f[0] = 1.0;
            for (int t = 1; t <= Horizon; t++)
            {
                f[t] = Math.Exp(-(t - 0.5) / years);
            }
            return f;
        }

        private static readonly double DebtYear = Math.Exp(-1.0 / MarketCatalogue.DebtRepriceYears), DebtHalfYear = Math.Exp(-0.5 / MarketCatalogue.DebtRepriceYears);

        // How much of what the market expects of a business beyond its figures has come through in them, on average over each
        // year to come (the last for the years after the horizon): it comes through day by day (MarketLife.BearOut).
        private static readonly double[] OutlookShare = MakeOutlookShare();

        private static double[] MakeOutlookShare()
        {
            var f = new double[Horizon + 2];
            for (int t = 1; t <= Horizon + 1; t++)
            {
                f[t] = 1.0 - Math.Exp(-(t - 0.5) / MarketCatalogue.OutlookYears);
            }
            return f;
        }

        /// <summary>The bank rate a year as the market reads it.</summary>
        public static double BankRate(in MarketEnv env)
        {
            return env.RateBp / 10000.0;
        }

        /// <summary>The rate the profits to come are discounted at: half today's (with half the next decision), half the long run.</summary>
        public static double DiscountRate(in MarketEnv env)
        {
            double now = (env.RateBp + env.RateNextBp * 0.5) / 10000.0;
            return LongRateShare * now + (1.0 - LongRateShare) * (NeutralRate + Math.Max(-0.01, env.InflationBp / 10000.0 - TargetInflation) * 0.5);
        }

        public static double Premium(in MarketEnv env)
        {
            return EquityPremium * MarketCatalogue.Expected(MarketCatalogue.CyclePremium, env.Cycle);
        }

        /// <summary>The cost of equity of a company a year: what holding it returns on average, dividends included.</summary>
        public static double CostOfEquity(Stock s, in MarketEnv env)
        {
            double k = DiscountRate(env) + s.Def.Beta / 100.0 * Premium(env) + (s.Size == MarketSize.Small ? MarketCatalogue.SizePremiumSmall : s.Size == MarketSize.Mid ? MarketCatalogue.SizePremiumMid : 0.0);
            return Math.Max(k, TerminalGrowth(s) + SpreadLeast);
        }

        /// <summary>The chance a year that a company fails, by its distress.</summary>
        public static double Hazard(double distress)
        {
            return distress < MarketCatalogue.DistressFrom ? 0.0 : MarketCatalogue.DistressHazardMost * distress * distress;
        }

        /// <summary>What holders lose of the price when a company fails, on average (MarketEngine's payout: often nothing).</summary>
        public static double LossOnFailure(double distress)
        {
            return 1.0 - MarketCatalogue.RecoveryChance * MarketCatalogue.RecoveryMost * (1.0 - Clamp(distress, 0.0, 1.0)) * 0.5;
        }

        /// <summary>What a company's price gains a year on average from bids for it: their chance times what the price jumps by when one is made.</summary>
        public static double TakeoverYield(Stock s)
        {
            if (s.Distress >= 0.6 || MarketCatalogue.IsRival(s.Id))
            {
                return 0.0;
            }
            double premium = (MarketCatalogue.TakeoverPremiumLeast + MarketCatalogue.TakeoverPremiumMost) * 0.5;
            return MarketCatalogue.TakeoverChanceYear * MarketCatalogue.TakeoverSizeScale[(int)s.Size] * (1.0 - MarketCatalogue.TakeoverFailChance) * premium;
        }

        /// <summary>What a company's price loses a year on average to the part of crashes the market never makes up (more likely in a slowdown or a recession).</summary>
        public static double CrashLoss(Stock s, in MarketEnv env)
        {
            double chance = MarketCatalogue.CrashChanceYear * (env.Cycle == 3 ? MarketCatalogue.CrashRecessionScale : env.Cycle == 2 ? MarketCatalogue.CrashSlowdownScale : 1.0);
            return chance * (MarketCatalogue.CrashLeast + MarketCatalogue.CrashMost) * 0.5 * (1.0 - MarketCatalogue.CrashMoodShare) * s.Def.Beta / 100.0;
        }

        /// <summary>
        /// What a company's price loses a year on average to crises of its own, less what it gains from windfalls: their
        /// chances times what the margin they move does to its worth (more for a company with much debt).
        /// </summary>
        public static double OwnNewsLoss(Stock s, in MarketEnv env)
        {
            double margin = UsualMargin(s, env);
            double interest = s.Rev > 0.0 ? s.Debt * s.DebtRate / s.Rev : 0.0;
            double gearing = margin > interest + 0.005 ? margin / (margin - interest) : 10.0;
            double loss = Math.Min(0.9, (MarketCatalogue.CrisisMarginLeast + MarketCatalogue.CrisisMarginMost) * 0.5 * gearing);
            double gain = Math.Min(2.0, (MarketCatalogue.WindfallMarginLeast + MarketCatalogue.WindfallMarginMost) * 0.5 * gearing);
            return MarketCatalogue.CrisisSizeScale[(int)s.Size] * (MarketCatalogue.CrisisChanceYear * loss - MarketCatalogue.WindfallChanceYear * gain);
        }

        /// <summary>
        /// The rate a company's profits to come are discounted at, before the chance it fails: its cost of equity, with
        /// what its price must make up on ordinary days for a bid, a crash or news of its own that may come.
        /// </summary>
        public static double Rate(Stock s, in MarketEnv env)
        {
            return CostOfEquity(s, env) - TakeoverYield(s) + CrashLoss(s, env) + OwnNewsLoss(s, env);
        }

        /// <summary>
        /// The share of its profit a company pays out when it grows by so much a year: what it does not need to keep back
        /// to grow (growth over what it earns on what it keeps). A fast grower pays little, a mature company most. What
        /// it pays is what its worth is made of, so nothing a company earns is lost to its holders.
        /// </summary>
        public static double Payout(double growth)
        {
            return Clamp(1.0 - growth / MarketCatalogue.RoeLong, 0.0, 0.95);
        }

        /// <summary>A company's growth a year as things stand, prices included (what its payout follows).</summary>
        public static double GrowthNow(Stock s, in MarketEnv env)
        {
            return s.Growth + env.InflationBp / 10000.0 * s.Def.PassPct / 100.0;
        }

        /// <summary>Growth for ever after the horizon: the city's long-run growth and the inflation target the company passes on.</summary>
        public static double TerminalGrowth(Stock s)
        {
            return MarketCatalogue.LongRealGrowth + TargetInflation * s.Def.PassPct / 100.0;
        }

        /// <summary>The operating margin a company comes back to as things stand (the council's policies, the bank rate, what has come through for good), outside a good or bad patch.</summary>
        public static double UsualMargin(Stock s, in MarketEnv env)
        {
            CompanyDef d = s.Def;
            double bp = d.MarginBp + MarketInputs.LeversBp(env, s.Sector) + d.RateBp * (env.RateBp - 300) / 100.0;
            return bp / 10000.0 + s.MarginShift;
        }

        /// <summary>What the state of the economy does to a company's margin now (as the market expects it to go on: CycleNext).</summary>
        public static double CycleMargin(Stock s, in MarketEnv env)
        {
            return MarketCatalogue.Expected(MarketCatalogue.CycleMarginBp, env.Cycle) * s.Def.Cyclical / 100.0 / 10000.0;
        }

        /// <summary>What inflation the company cannot pass on takes from its margin, now.</summary>
        public static double InflationCost(Stock s, in MarketEnv env)
        {
            return (1.0 - s.Def.PassPct / 100.0) * Math.Max(0, env.InflationBp - 200) * 0.5 / 10000.0;
        }

        /// <summary>The margin a company stands at today before its own ups and downs: its usual margin, the cycle, the day's supply, inflation it cannot pass on.</summary>
        public static double TargetMargin(Stock s, in MarketEnv env)
        {
            return UsualMargin(s, env) + CycleMargin(s, env) + MarketInputs.SupplyBp(env, s.Sector) / 10000.0 - InflationCost(s, env);
        }

        /// <summary>The growth a company has from the city now: the cycle and what households earn (it fades within about a year).</summary>
        public static double CityGrowth(Stock s, in MarketEnv env)
        {
            double cycle = MarketCatalogue.Expected(MarketCatalogue.CycleGrowthBp, env.Cycle) * s.Def.Cyclical / 100.0 / 10000.0;
            double income = (env.DistrictIncome - 1000) / 1000.0 * MarketInputs.IncomeShare[(int)s.Sector] / 100.0 * 0.5;
            return cycle + income;
        }

        /// <summary>Profit after tax a year, in money, of the business at a margin.</summary>
        public static double Profit(Stock s, double margin, double tax)
        {
            double before = s.Rev * margin - s.Debt * s.DebtRate;
            return before > 0.0 ? before * (1.0 - tax) : before;
        }

        /// <summary>Earnings a share a year as things stand, in money.</summary>
        public static double Earnings(Stock s, in MarketEnv env)
        {
            return s.Shares > 0 ? Profit(s, s.Margin, MarketInputs.TaxRate(env)) / s.Shares : 0.0;
        }

        /// <summary>
        /// What one share is worth, in money, with what the market expects of the business beyond its figures
        /// (<see cref="Stock.Outlook"/>), before the dividend the price has built up since the last one
        /// (<see cref="MarketLife.Accrued"/>).
        /// </summary>
        public static double Fair(Stock s, in MarketEnv env)
        {
            return Worth(s, env, s.Outlook);
        }

        /// <summary>
        /// What one share is worth, in money, if the market expects so much of the business beyond its figures (ln): the
        /// payouts to come as the figures say, each with the part of that expectation that will have come through in them
        /// by then. Always more than nothing (see the class's notes); higher the more is expected.
        /// </summary>
        public static double Worth(Stock s, in MarketEnv env, double outlook)
        {
            if (s.Shares <= 0)
            {
                return 0.01;
            }
            CompanyDef d = s.Def;
            double tax = MarketInputs.TaxRate(env);
            Paths(s, env, out double usual, out double patch, out double supply, out double cost, out double debtNow, out double debtUsual, out double rateNow, out double rateUsual);
            double cycle = CycleMargin(s, env);
            double k = Rate(s, env);
            double gT = TerminalGrowth(s);
            double least = gT + SpreadLeast;
            double pass = d.PassPct / 100.0;
            double inflation = env.InflationBp / 10000.0;
            double city = CityGrowth(s, env);
            double own = s.Growth - s.GrowthTarget;
            double payoutT = Payout(gT);
            double rev = s.Rev, discount = 1.0, sum = 0.0, debt = debtNow, rate = rateNow;
            for (int t = 1; t <= Horizon; t++)
            {
                double real = MarketCatalogue.LongRealGrowth + own * OwnFade[t] + city * PatchFade[t];
                double prices = TargetInflation + (inflation - TargetInflation) * PriceFade[t];
                rev *= 1.0 + Math.Max(-0.3, real + prices * pass);
                // Debt and what it costs come back to their usual over the year (as the business does it day by day).
                double interest = (debtUsual + (debt - debtUsual) * DebtHalfYear) * (rateUsual + (rate - rateUsual) * DebtHalfYear);
                double margin = usual + patch * PatchFade[t] - cost * PriceFade[t] + (t == 1 ? supply * SupplyYears : 0.0);
                double profit = rev * (margin - interest);
                // The chance it fails that year, from its margin as it stands (its own ups and downs left out: they pass) and
                // its debt as they are expected to be then.
                double distress = DistressAt(usual + cycle * PatchFade[t] - cost * PriceFade[t], interest);
                discount /= YearRate(k, distress, least);
                if (profit > 0.0)
                {
                    sum += Payout(real + prices * pass) * profit * (1.0 - tax) * discount * Math.Exp(outlook * OutlookShare[t]);
                }
                // A loss is borrowed (as the business does): more debt for the years after.
                debt = debtUsual + (debt - debtUsual) * DebtYear + (profit < 0.0 ? -profit / rev : 0.0);
                rate = rateUsual + (rate - rateUsual) * DebtYear;
            }
            double interestUsual = debtUsual * rateUsual;
            double last = rev * (usual - interestUsual);
            double distressUsual = DistressAt(usual, interestUsual);
            double kT = YearRate(k, distressUsual, least) - 1.0;
            double expected = Math.Exp(outlook * OutlookShare[Horizon + 1]);
            if (last > 0.0)
            {
                sum += payoutT * last * (1.0 - tax) * (1.0 + gT) / (kT - gT) * discount * expected;
            }
            // Worth never quite nothing: a smooth floor of a few per cent of its turnover a share (what it may yet be worth).
            double value = sum / s.Shares;
            double floor = Math.Max(1e-6, 0.03 * s.Rev / s.Shares * expected);
            double x = Math.Max(-30.0, value / floor);
            return x > 30.0 ? value : floor * Math.Log(1.0 + Math.Exp(x));
        }

        /// <summary>
        /// The expectation (ln) at which a share is worth so much: the outlook the market's price stands for. Found by the
        /// secant rule from where it is (the worth only ever grows with what is expected); within -8 and 8.
        /// </summary>
        public static double OutlookFor(Stock s, in MarketEnv env, double worth, double from)
        {
            if (!(worth > 0.0))
            {
                return from;
            }
            double target = Math.Log(worth);
            double a = Clamp(from, -8.0, 8.0), ga = Math.Log(Worth(s, env, a)) - target;
            if (Math.Abs(ga) < 1e-10)
            {
                return a;
            }
            double b = Clamp(a - ga / 0.8, -8.0, 8.0), gb = Math.Log(Worth(s, env, b)) - target;
            for (int i = 0; i < 12 && Math.Abs(gb) > 1e-10; i++)
            {
                double slope = Math.Abs(b - a) > 1e-12 ? (gb - ga) / (b - a) : 0.8;
                if (!(slope > 1e-6))
                {
                    slope = 0.8;
                }
                double c = Clamp(b - gb / slope, -8.0, 8.0);
                if (c == b)
                {
                    break;
                }
                a = b;
                ga = gb;
                b = c;
                gb = Math.Log(Worth(s, env, b)) - target;
            }
            return b;
        }

        // What the worth is made from: the usual margin, the patch that fades (the cycle and its own ups and downs), the
        // day's supply, inflation it cannot pass on; its debt on turnover now and as usual, and what the debt costs now
        // and as usual (the bank rate and the spread of a company in its usual shape).
        private static void Paths(Stock s, in MarketEnv env, out double usual, out double patch, out double supply, out double cost, out double debtNow, out double debtUsual,
            out double rateNow, out double rateUsual)
        {
            usual = UsualMargin(s, env);
            patch = CycleMargin(s, env) + (s.Margin - s.MarginTarget);
            supply = MarketInputs.SupplyBp(env, s.Sector) / 10000.0;
            cost = InflationCost(s, env);
            debtNow = s.Rev > 0.0 ? s.Debt / s.Rev : 0.0;
            debtUsual = s.Def.DebtPct / 100.0;
            rateNow = s.DebtRate;
            rateUsual = UsualDebtRate(s, env);
        }

        /// <summary>What a company's debt costs in its usual shape: the bank rate and the spread of its usual margin and debt. Its debt comes to cost this over time.</summary>
        public static double UsualDebtRate(Stock s, in MarketEnv env)
        {
            double debtUsual = s.Def.DebtPct / 100.0;
            return BankRate(env) + CreditSpread(DistressAt(UsualMargin(s, env), debtUsual * (BankRate(env) + MarketCatalogue.CreditSpreadLeast)));
        }

        // A year's discount: the rate, and the chance of failing over the year as the market draws it day by day (a chance of
        // h a year leaves e^-h of companies standing, not 1 / (1 + h): a company in deep distress is rightly cheap).
        private static double YearRate(double k, double distress, double least)
        {
            return Math.Max(1.0 + least, (1.0 + k) * Math.Exp(Hazard(distress) * LossOnFailure(distress)));
        }

        /// <summary>
        /// How near a company is to failing, 0 to 1: interest it will hardly be able to pay over the year to come, or
        /// losses it cannot carry, from its margin as it stands (with the cycle; its own ups and downs pass) and its debt.
        /// As the market prices it (the first year of its worth), so the chance it fails (<see cref="Hazard"/>) is the
        /// chance its price is paid for.
        /// </summary>
        public static double DistressOf(Stock s, in MarketEnv env)
        {
            Paths(s, env, out double usual, out double patch, out double supply, out double cost, out double debtNow, out double debtUsual, out double rateNow, out double rateUsual);
            double interest = (debtUsual + (debtNow - debtUsual) * DebtHalfYear) * (rateUsual + (rateNow - rateUsual) * DebtHalfYear);
            return DistressAt(usual + CycleMargin(s, env) * PatchFade[1] - cost * PriceFade[1], interest);
        }

        /// <summary>Distress, 0 to 1, of a business at an operating margin with interest of so much of its turnover.</summary>
        public static double DistressAt(double margin, double interest)
        {
            double byCover = 0.0;
            if (interest > 0.0)
            {
                double cover = margin / interest;
                byCover = cover >= 2.0 ? 0.0 : cover <= 0.0 ? 1.0 : (2.0 - cover) / 2.0;
            }
            double byLoss = margin >= 0.0 ? 0.0 : Clamp(-margin / 0.08, 0.0, 1.0);
            return Clamp(Math.Max(byCover * byCover, byLoss), 0.0, 1.0);
        }

        /// <summary>What a company in distress pays on its debt over the bank rate.</summary>
        public static double CreditSpread(double distress)
        {
            return MarketCatalogue.CreditSpreadLeast + (MarketCatalogue.CreditSpreadMost - MarketCatalogue.CreditSpreadLeast) * distress;
        }

        public static double Clamp(double value, double least, double most)
        {
            return value < least ? least : value > most ? most : value;
        }

        public static int Clamp(int value, int least, int most)
        {
            return value < least ? least : value > most ? most : value;
        }
    }
}
