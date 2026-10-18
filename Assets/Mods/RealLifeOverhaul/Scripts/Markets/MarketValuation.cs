using System;

namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// What a share is worth, from the business as the market knows it. Game-free. The worth is the payouts to come over
    /// fifteen years, as growth fades to the city's long run and a good or bad patch passes, then the rest for ever, each
    /// discounted at the cost of equity plus what the price must make up for a failure, a bid, a crash or news of its own.
    /// What is known is priced at once, so nothing readable in the city tells where a price is going (the checks measure
    /// it over thousands of days). Prices follow inflation only through turnover and costs.
    /// </summary>
    public static class MarketValuation
    {
        public const double EquityPremium = 0.04;        // a year, for the whole market (beta 1) in ordinary times
        public const double NeutralRate = 0.03;          // the bank rate the long run comes back to
        public const double LongRateShare = 0.5;         // of today's rate in the discount rate; the rest is the long run
        public const double SpreadLeast = 0.04;          // the discount rate is at least this over long-run growth
        public const double TargetInflation = 0.02;
        public const double InflationFadeYears = 2.0;
        public const double PatchFadeYears = 1.0;        // years
        public const double SupplyYears = 0.05;          // years a dear or cheap kind of goods is expected to last
        public const int Horizon = 15;                   // years of fading growth before the rest is valued as one

        private static readonly double[] OwnFade = Fade(MarketCatalogue.GrowthFadeYears);
        private static readonly double[] PatchFade = Fade(PatchFadeYears);
        private static readonly double[] PriceFade = Fade(InflationFadeYears);

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

        // How much of what is expected beyond the figures has come through in them by each year to come.
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

        public static double BankRate(in MarketEnv env)
        {
            return env.RateBp / 10000.0;
        }

        /// <summary>The discount rate: half today's rate with half the next decision, half the long run.</summary>
        public static double DiscountRate(in MarketEnv env)
        {
            double now = (env.RateBp + env.RateNextBp * 0.5) / 10000.0;
            return LongRateShare * now + (1.0 - LongRateShare) * (NeutralRate + Math.Max(-0.01, env.InflationBp / 10000.0 - TargetInflation) * 0.5);
        }

        public static double Premium(in MarketEnv env)
        {
            return EquityPremium * MarketCatalogue.Expected(MarketCatalogue.CyclePremium, env.Cycle);
        }

        /// <summary>The cost of equity a year: what holding the company returns on average, dividends included.</summary>
        public static double CostOfEquity(Stock s, in MarketEnv env)
        {
            double k = DiscountRate(env) + s.Def.Beta / 100.0 * Premium(env)
                       + (s.Size == MarketSize.Small ? MarketCatalogue.SizePremiumSmall : s.Size == MarketSize.Mid ? MarketCatalogue.SizePremiumMid : 0.0);
            return Math.Max(k, TerminalGrowth(s) + SpreadLeast);
        }

        public static double Hazard(double distress)
        {
            return distress < MarketCatalogue.DistressFrom ? 0.0 : MarketCatalogue.DistressHazardMost * distress * distress;
        }

        public static double LossOnFailure(double distress)
        {
            return 1.0 - MarketCatalogue.RecoveryChance * MarketCatalogue.RecoveryMost * (1.0 - Clamp(distress, 0.0, 1.0)) * 0.5;
        }

        /// <summary>What the price gains a year on average from bids: their chance times the jump when one is made.</summary>
        public static double TakeoverYield(Stock s)
        {
            if (s.Distress >= 0.6 || MarketCatalogue.IsRival(s.Id) || s.Own != null)
            {
                return 0.0;   // a player's company is bid for only when cheap (ListingVotes)
            }
            double premium = (MarketCatalogue.TakeoverPremiumLeast + MarketCatalogue.TakeoverPremiumMost) * 0.5;
            return MarketCatalogue.TakeoverChanceYear * MarketCatalogue.TakeoverSizeScale[(int)s.Size] * (1.0 - MarketCatalogue.TakeoverFailChance) * premium;
        }

        /// <summary>What the price loses a year on average to the part of crashes never made up.</summary>
        public static double CrashLoss(Stock s, in MarketEnv env)
        {
            double chance = MarketCatalogue.CrashChanceYear * (env.Cycle == 3 ? MarketCatalogue.CrashRecessionScale : env.Cycle == 2 ? MarketCatalogue.CrashSlowdownScale : 1.0);
            return chance * (MarketCatalogue.CrashLeast + MarketCatalogue.CrashMost) * 0.5 * (1.0 - MarketCatalogue.CrashMoodShare) * s.Def.Beta / 100.0;
        }

        /// <summary>What the price loses a year on average to its own crises, less windfalls; more with much debt.</summary>
        public static double OwnNewsLoss(Stock s, in MarketEnv env)
        {
            if (s.Own != null)
            {
                return 0.0;   // a player's company's news is its own books
            }
            double margin = UsualMargin(s, env);
            double interest = s.Rev > 0.0 ? s.Debt * s.DebtRate / s.Rev : 0.0;
            double gearing = margin > interest + 0.005 ? margin / (margin - interest) : 10.0;
            double loss = Math.Min(0.9, (MarketCatalogue.CrisisMarginLeast + MarketCatalogue.CrisisMarginMost) * 0.5 * gearing);
            double gain = Math.Min(2.0, (MarketCatalogue.WindfallMarginLeast + MarketCatalogue.WindfallMarginMost) * 0.5 * gearing);
            return MarketCatalogue.CrisisSizeScale[(int)s.Size] * (MarketCatalogue.CrisisChanceYear * loss - MarketCatalogue.WindfallChanceYear * gain);
        }

        /// <summary>The discount rate before failing: the cost of equity with what a bid, a crash or its own news may bring.</summary>
        public static double Rate(Stock s, in MarketEnv env)
        {
            return CostOfEquity(s, env) - TakeoverYield(s) + CrashLoss(s, env) + OwnNewsLoss(s, env);
        }

        /// <summary>The share of profit paid out at a growth: what it does not need to keep back to grow.</summary>
        public static double Payout(double growth)
        {
            return Clamp(1.0 - growth / MarketCatalogue.RoeLong, 0.0, 0.95);
        }

        public static double PayoutOf(Stock s, double growth)
        {
            double payout = Payout(growth);
            return TrustRules.Is(s) ? Math.Max(MarketCatalogue.TrustPayout, payout) : payout;
        }

        public static double GrowthNow(Stock s, in MarketEnv env)
        {
            return s.Growth + env.InflationBp / 10000.0 * s.Def.PassPct / 100.0;
        }

        public static double TerminalGrowth(Stock s)
        {
            return MarketCatalogue.LongRealGrowth + TargetInflation * s.Def.PassPct / 100.0;
        }

        /// <summary>The margin a company comes back to outside a good or bad patch: the policies, the bank rate, what has come through.</summary>
        public static double UsualMargin(Stock s, in MarketEnv env)
        {
            CompanyDef d = s.Def;
            double bp = d.MarginBp + MarketInputs.LeversBp(env, s.Sector) + d.RateBp * (env.RateBp - 300) / 100.0;
            return bp / 10000.0 + s.MarginShift;
        }

        public static double CycleMargin(Stock s, in MarketEnv env)
        {
            return MarketCatalogue.Expected(MarketCatalogue.CycleMarginBp, env.Cycle) * s.Def.Cyclical / 100.0 / 10000.0;
        }

        public static double InflationCost(Stock s, in MarketEnv env)
        {
            return (1.0 - s.Def.PassPct / 100.0) * Math.Max(0, env.InflationBp - 200) * 0.5 / 10000.0;
        }

        public static double TargetMargin(Stock s, in MarketEnv env)
        {
            return UsualMargin(s, env) + CycleMargin(s, env) + MarketInputs.SupplyBp(env, s.Sector) / 10000.0 - InflationCost(s, env);
        }

        public static double CityGrowth(Stock s, in MarketEnv env)
        {
            double cycle = MarketCatalogue.Expected(MarketCatalogue.CycleGrowthBp, env.Cycle) * s.Def.Cyclical / 100.0 / 10000.0;
            double income = (env.DistrictIncome - 1000) / 1000.0 * MarketInputs.IncomeShare[(int)s.Sector] / 100.0 * 0.5;
            return cycle + income;
        }

        public static double Profit(Stock s, double margin, double tax)
        {
            double before = s.Rev * margin - s.Debt * s.DebtRate;
            return before > 0.0 ? before * (1.0 - tax) : before;
        }

        public static double Earnings(Stock s, in MarketEnv env)
        {
            return s.Shares > 0 ? Profit(s, s.Margin, MarketInputs.TaxRate(env)) / s.Shares : 0.0;
        }

        /// <summary>One share's worth in money with its outlook, before the dividend built up since the last one.</summary>
        public static double Fair(Stock s, in MarketEnv env)
        {
            return Worth(s, env, s.Outlook);
        }

        /// <summary>One share's worth in money if the market expects so much beyond the figures (ln); always above nothing.</summary>
        public static double Worth(Stock s, in MarketEnv env, double outlook)
        {
            Terms terms = _worth ?? (_worth = new Terms());
            terms.Fill(s, env);
            return terms.Worth(outlook);
        }

        [ThreadStatic] private static Terms _worth;    // one per thread
        [ThreadStatic] private static Terms _search;   // OutlookFor's own, so a Worth between its tries leaves it as it was

        /// <summary>
        /// A company's worth taken apart so many expectations can be tried: all but the expectation's factor is filled once,
        /// and each try costs seventeen exps. A try gives Worth's very bits, the same products in the same order with the
        /// expectation's factor last (EconomyCheck's MarketPaceChecks compare). Nothing is allocated after it is made; one
        /// thread at a time.
        /// </summary>
        public sealed class Terms
        {
            private readonly double[] _year = new double[Horizon + 1];   // payout x profit x (1 - tax) x discount
            private readonly bool[] _paid = new bool[Horizon + 1];       // a loss pays nothing
            private double _rest;                                       // the years after the horizon as one
            private bool _restPaid;
            private bool _trust;                                        // a property trust: the sum at where the property market is heading
            private double _property;
            private double _shares;
            private double _floorEach;                                  // the floor: 3% of turnover a share
            private bool _none;                                         // no shares: worth a cent

            public void Fill(Stock s, in MarketEnv env)
            {
                _none = s.Shares <= 0;
                if (_none)
                {
                    return;
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
                double payoutT = s.Own != null ? ListingCatalogue.OwnPayout : PayoutOf(s, gT);   // a player's company's profit is its holders' (ListingRules)
                double rev = s.Rev, discount = 1.0, debt = debtNow, rate = rateNow;
                for (int t = 1; t <= Horizon; t++)
                {
                    double real = MarketCatalogue.LongRealGrowth + own * OwnFade[t] + city * PatchFade[t];
                    double prices = TargetInflation + (inflation - TargetInflation) * PriceFade[t];
                    rev *= 1.0 + Math.Max(-0.3, real + prices * pass);
        // Debt and its cost come back to their usual over the year, as the business does day by day.
                    double interest = (debtUsual + (debt - debtUsual) * DebtHalfYear) * (rateUsual + (rate - rateUsual) * DebtHalfYear);
                    double margin = usual + patch * PatchFade[t] - cost * PriceFade[t] + (t == 1 ? supply * SupplyYears : 0.0);
                    double profit = rev * (margin - interest);
        // The year's chance of failing from its margin without its own swings, which pass, and its debt as expected then.
                    double distress = DistressAt(usual + cycle * PatchFade[t] - cost * PriceFade[t], interest);
                    discount /= YearRate(k, distress, least);
                    _paid[t] = profit > 0.0;
                    _year[t] = _paid[t] ? (s.Own != null ? ListingCatalogue.OwnPayout : PayoutOf(s, real + prices * pass)) * profit * (1.0 - tax) * discount : 0.0;
        // A loss is borrowed: more debt for the years after.
                    debt = debtUsual + (debt - debtUsual) * DebtYear + (profit < 0.0 ? -profit / rev : 0.0);
                    rate = rateUsual + (rate - rateUsual) * DebtYear;
                }
                double interestUsual = debtUsual * rateUsual;
                double last = rev * (usual - interestUsual);
                double distressUsual = DistressAt(usual, interestUsual);
                double kT = YearRate(k, distressUsual, least) - 1.0;
                _restPaid = last > 0.0;
                _rest = _restPaid ? payoutT * last * (1.0 - tax) * (1.0 + gT) / (kT - gT) * discount : 0.0;
                _trust = TrustRules.Is(s);
                _property = _trust ? TrustRules.PropertyFactor(env) : 1.0;
                _shares = s.Shares;
                _floorEach = 0.03 * s.Rev / s.Shares;
            }

        /// <summary>One share's worth at an expectation (ln), for the company and city last filled.</summary>
            public double Worth(double outlook)
            {
                if (_none)
                {
                    return 0.01;
                }
                double sum = 0.0;
                for (int t = 1; t <= Horizon; t++)
                {
                    if (_paid[t])
                    {
                        sum += _year[t] * Math.Exp(outlook * OutlookShare[t]);
                    }
                }
                double expected = Math.Exp(outlook * OutlookShare[Horizon + 1]);
                if (_restPaid)
                {
                    sum += _rest * expected;
                }
                if (_trust)
                {
                    sum *= _property;
                }
        // Never quite nothing: a smooth floor of a few per cent of turnover a share, what it may yet be worth.
                double value = sum / _shares;
                double floor = Math.Max(1e-6, _floorEach * expected);
                double x = Math.Max(-30.0, value / floor);
                return x > 30.0 ? value : floor * Math.Log(1.0 + Math.Exp(x));
            }
        }

        /// <summary>The expectation (ln) at which a share is worth so much, by the secant rule from <paramref name="from"/>, within -8 and 8.</summary>
        public static double OutlookFor(Stock s, in MarketEnv env, double worth, double from)
        {
            if (!(worth > 0.0))
            {
                return from;
            }
            Terms terms = _search ?? (_search = new Terms());
            terms.Fill(s, env);
            return OutlookFor(terms, worth, from);
        }

        /// <summary>The same for a company already taken apart.</summary>
        public static double OutlookFor(Terms terms, double worth, double from)
        {
            if (!(worth > 0.0))
            {
                return from;
            }
            double target = Math.Log(worth);
            double a = Clamp(from, -8.0, 8.0), ga = Math.Log(terms.Worth(a)) - target;
            if (Math.Abs(ga) < 1e-10)
            {
                return a;
            }
            double b = Clamp(a - ga / 0.8, -8.0, 8.0), gb = Math.Log(terms.Worth(b)) - target;
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
                gb = Math.Log(terms.Worth(b)) - target;
            }
            return b;
        }

        // The worth's parts: the usual margin, the patch that fades, the day's supply, inflation not passed on, and debt and its cost.
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

        /// <summary>What a company's debt costs in its usual shape, which its debt comes to cost over time.</summary>
        public static double UsualDebtRate(Stock s, in MarketEnv env)
        {
            double debtUsual = s.Def.DebtPct / 100.0;
            return BankRate(env) + CreditSpread(DistressAt(UsualMargin(s, env), debtUsual * (BankRate(env) + MarketCatalogue.CreditSpreadLeast)));
        }

        // A year's discount with the chance of failing drawn day by day: a hazard h leaves e^-h standing, not 1 / (1 + h).
        private static double YearRate(double k, double distress, double least)
        {
            return Math.Max(1.0 + least, (1.0 + k) * Math.Exp(Hazard(distress) * LossOnFailure(distress)));
        }

        /// <summary>How near a company is to failing, 0 to 1, as the first year of its worth prices it.</summary>
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
