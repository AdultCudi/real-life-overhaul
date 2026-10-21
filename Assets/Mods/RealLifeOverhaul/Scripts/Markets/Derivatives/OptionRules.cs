using System;

namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// What an option is worth. Game-free. A call or a put on a price whose ln at its day is bell-shaped around a forward:
    /// the Black formula, undiscounted, so a premium paid now for money at its day is never worth more than the payoff
    /// expected. A material's spread is exact (MaterialRules); a share's is worked out from the market's own variances,
    /// day by day to its day as the market will play them, with its results day and a margin for jumps and fat tails
    /// (<see cref="DerivCatalogue.ShareVolSafety"/>). Each price is a few dozen operations, asked when a page is written.
    /// </summary>
    public static class OptionRules
    {
        /// <summary>The share of a bell curve under a point, to seven places (erfc, Numerical Recipes' Chebyshev fit).</summary>
        public static double Cdf(double x)
        {
            double z = Math.Abs(x) / Math.Sqrt(2.0);
            double t = 1.0 / (1.0 + 0.5 * z);
            double erfc = t * Math.Exp(-z * z - 1.26551223 + t * (1.00002368 + t * (0.37409196 + t * (0.09678418 + t * (-0.18628806 + t * (0.27886807 + t * (-1.13520398
                + t * (1.48851587 + t * (-0.82215223 + t * 0.17087277)))))))));
            return x >= 0.0 ? 1.0 - 0.5 * erfc : 0.5 * erfc;
        }

        /// <summary>What a call (or a put) is worth on a price with this forward, strike and ln variance to its day.</summary>
        public static double Black(bool call, double forward, double strike, double variance)
        {
            if (forward <= 0.0 || strike <= 0.0)
            {
                return call ? Math.Max(0.0, forward - strike) : Math.Max(0.0, strike - forward);
            }
            if (variance <= 1e-14)
            {
                return call ? Math.Max(0.0, forward - strike) : Math.Max(0.0, strike - forward);
            }
            double sd = Math.Sqrt(variance);
            double d1 = (Math.Log(forward / strike) + variance * 0.5) / sd, d2 = d1 - sd;
            return call ? forward * Cdf(d1) - strike * Cdf(d2) : strike * Cdf(-d2) - forward * Cdf(-d1);
        }

        /// <summary>What an option pays at its day, a unit: the price over the strike for a call, under it for a put.</summary>
        public static long Intrinsic(bool call, long price, long strike)
        {
            return Math.Max(0L, call ? price - strike : strike - price);
        }

        // The market's weights of a day's steps (MarketEngine's, worked out the same way), and its share of the night.
        private static readonly double[] StepWeight = Weights();

        private static double[] Weights()
        {
            int steps = MarketCatalogue.Steps;
            var w = new double[steps + 1];
            double total = 0.0;
            for (int k = 1; k <= steps; k++)
            {
                w[k] = 1.0 + MarketCatalogue.IntradayOpenWeight * Math.Exp(-(k - 1) / MarketCatalogue.IntradayDecaySteps)
                       + MarketCatalogue.IntradayCloseWeight * Math.Exp(-(steps - k) / MarketCatalogue.IntradayDecaySteps);
                total += w[k];
            }
            for (int k = 1; k <= steps; k++)
            {
                w[k] /= total;
            }
            return w;
        }

        /// <summary>The share of a market day's variance still to come at a phase of it: the night before the open, then the steps.</summary>
        public static double DayLeft(int phase, int shut)
        {
            if (phase >= MarketCatalogue.PhaseClose)
            {
                return 0.0;
            }
            double night = MarketCatalogue.OvernightShare * (1.0 + MarketCatalogue.WeekendExtra * (Math.Max(1, shut) - 1));
            if (phase < MarketCatalogue.PhaseOpen)
            {
                return night + (1.0 - MarketCatalogue.OvernightShare);
            }
            double steps = 0.0;
            for (int k = phase - MarketCatalogue.PhaseOpen + 1; k <= MarketCatalogue.Steps; k++)
            {
                steps += StepWeight[k];
            }
            return (1.0 - MarketCatalogue.OvernightShare) * steps;
        }

        /// <summary>Days the market was shut before a market day, the day itself counted (a Monday's is 3).</summary>
        public static int Shut(int day)
        {
            int shut = 1;
            for (int d = day - 1; d >= 1 && !MarketCatalogue.TradingDay(d); d--)
            {
                shut++;
            }
            return shut;
        }

        /// <summary>
        /// A share's ln variance from now to the close of its day as the market will play it, its jumps apart: on each market
        /// day the market's own variance (as the GARCH expects it to come back), the sector's and the company's own, its
        /// results day larger; times the margin for what the business's own news adds at the opens, more the longer it runs.
        /// </summary>
        public static double ShareVariance(MarketState state, Stock s, int toDay)
        {
            if (s == null || s.Def == null || toDay < state.Day)
            {
                return 0.0;
            }
            int year = Math.Max(8, state.YearDays);
            double vs = MarketCatalogue.VolScale(year);
            MarketEnv env = state.Env;
            int cycle = Math.Max(0, Math.Min(3, env.Cycle));
            double cycleVol = MarketCatalogue.CycleVolatility[cycle] * (env.Campaign != 0 ? 1.1 : 1.0);
            double sectorCycle = MarketCatalogue.CycleVolatility[cycle];
            double beta = s.Def.Beta / 100.0;
            double marketSd = MarketCatalogue.IndexDayVol * vs * beta;
            double sectorSd = MarketCatalogue.Sectors[(int)s.Sector].SectorVolBp / 10000.0 * vs;
            double ownSd = s.Def.IdioBp / 10000.0 * vs;
            double persistM = MarketCatalogue.GarchA + MarketCatalogue.GarchB + MarketCatalogue.GarchGamma * 0.5;
            double persistS = MarketCatalogue.SectorGarchA + MarketCatalogue.SectorGarchB;
            double vm = state.Variance, vsec = state.SectorVar[(int)s.Sector] <= 0.0 ? 1.0 : state.SectorVar[(int)s.Sector];
            double total = 0.0, days = 0.0;
            int ahead = 0;   // closes since now: the variances' expected way back to 1
            for (int d = state.Day; d <= toDay; d++)
            {
                if (!MarketCatalogue.TradingDay(d))
                {
                    continue;
                }
                double share = d == state.Day ? DayLeft(state.Phase, Shut(d)) : DayLeft(MarketCatalogue.PhaseNone, Shut(d));
                if (share <= 0.0)
                {
                    continue;
                }
                double em = 1.0 + (vm - 1.0) * Math.Pow(persistM, ahead), es = 1.0 + (vsec - 1.0) * Math.Pow(persistS, ahead);
                em = Math.Max(MarketCatalogue.VarianceLeast, em);
                es = Math.Max(MarketCatalogue.VarianceLeast, es);
                double day = em * cycleVol;
                double own = 0.5 + 0.5 * Math.Sqrt(Math.Max(0.1, day));
                double ownVar = ownSd * ownSd * own * own * (s.ResultsBoostDay == d || s.ResultsDay == d ? MarketCatalogue.ResultsDayVolScale : 1.0);
                double piece = (marketSd * marketSd * day + sectorSd * sectorSd * es * sectorCycle + ownVar) * share;
                if (s.ResultsDay == d)
                {
                    piece += ownSd * ownSd * own * own * DerivCatalogue.ResultsExtraDays;   // the results themselves, at the open
                }
                total += piece;
                days += share;
                ahead++;
            }
            double safety = DerivCatalogue.ShareVolSafety * DerivCatalogue.ShareVolSafety;
            return total * safety * (1.0 + DerivCatalogue.ShareNewsPerDay * days);
        }

        // A share's jumps over the life of an option: the company's own crisis and windfall (its usual margin moved for good,
        // more with its gearing), a takeover bid at a premium, a crash of the whole market. Each a chance over the life, the
        // mean of its ln and that ln's variance; first order in the chances, which are small.
        private const int Jumps = 4;

        [ThreadStatic] private static double[] _jumpP, _jumpMu, _jumpVar;

        private static void ShareJumps(MarketState state, Stock s, int toDay, double[] p, double[] mu, double[] variance)
        {
            int year = Math.Max(8, state.YearDays);
            double tradingYear = MarketCatalogue.TradingDaysPerYear(year);
            int closes = DerivRules.Closes(state.Day, state.Phase, toDay);
            MarketEnv env = state.Env;
            double margin = MarketValuation.UsualMargin(s, env);
            double interest = s.Rev > 0.0 ? s.Debt * s.DebtRate / s.Rev : 0.0;
            double gearing = margin > interest + 0.005 ? margin / (margin - interest) : 10.0;
            int size = (int)s.Size;
            double crisis = MarketCatalogue.CrisisChanceYear * MarketCatalogue.CrisisSizeScale[size] / tradingYear;
            double windfall = MarketCatalogue.WindfallChanceYear * MarketCatalogue.CrisisSizeScale[size] / tradingYear;
            double bid = MarketValuation.TakeoverYield(s) > 0.0
                ? MarketCatalogue.TakeoverChanceYear * MarketCatalogue.TakeoverSizeScale[size] * (1.0 - MarketCatalogue.TakeoverFailChance) / tradingYear
                : 0.0;
            double crash = MarketCatalogue.CrashChanceYear / tradingYear * MarketCatalogue.CrashDearScale
                           * (env.Cycle == 3 ? MarketCatalogue.CrashRecessionScale : env.Cycle == 2 ? MarketCatalogue.CrashSlowdownScale : 1.0);
            double loss = Math.Min(0.9, (MarketCatalogue.CrisisMarginLeast + MarketCatalogue.CrisisMarginMost) * 0.5 * gearing);
            double lossSpread = Math.Min(0.4, (MarketCatalogue.CrisisMarginMost - MarketCatalogue.CrisisMarginLeast) * gearing / Math.Sqrt(12.0));
            double gain = Math.Min(2.0, (MarketCatalogue.WindfallMarginLeast + MarketCatalogue.WindfallMarginMost) * 0.5 * gearing);
            double premium = (MarketCatalogue.TakeoverPremiumLeast + MarketCatalogue.TakeoverPremiumMost) * 0.5;
            double fall = (MarketCatalogue.CrashLeast + MarketCatalogue.CrashMost) * 0.5 * s.Def.Beta / 100.0;
            double spread = DerivCatalogue.JumpSpread * DerivCatalogue.JumpSpread;
            p[0] = 1.0 - Math.Pow(1.0 - crisis, closes);
            mu[0] = Math.Log(1.0 - loss);
            variance[0] = Square(lossSpread / Math.Max(0.1, 1.0 - loss)) + spread;
            p[1] = 1.0 - Math.Pow(1.0 - windfall, closes);
            mu[1] = Math.Log(1.0 + gain);
            variance[1] = spread;
            p[2] = 1.0 - Math.Pow(1.0 - bid, closes);
            mu[2] = Math.Log(1.0 + premium);
            variance[2] = Square((MarketCatalogue.TakeoverPremiumMost - MarketCatalogue.TakeoverPremiumLeast) / Math.Sqrt(12.0) / (1.0 + premium));
            p[3] = 1.0 - Math.Pow(1.0 - Math.Min(0.5, crash), closes);
            mu[3] = Math.Log(Math.Max(0.1, 1.0 - fall));
            variance[3] = Square((MarketCatalogue.CrashMost - MarketCatalogue.CrashLeast) / Math.Sqrt(12.0) * s.Def.Beta / 100.0);
        }

        private static double Square(double x)
        {
            return x * x;
        }

        /// <summary>
        /// A share option's fair premium a share in money, with its forward and its spread to the day (for the page): the
        /// Black price of the body with each jump mixed in at its chance, the forward kept as the mean of it all.
        /// </summary>
        public static double ShareFair(MarketState state, Stock s, int toDay, bool call, double strike, out double forward, out double spread)
        {
            forward = ShareForward(state, s, toDay);
            double body = ShareVariance(state, s, toDay);
            double[] p = _jumpP ?? (_jumpP = new double[Jumps]), mu = _jumpMu ?? (_jumpMu = new double[Jumps]), v = _jumpVar ?? (_jumpVar = new double[Jumps]);
            ShareJumps(state, s, toDay, p, mu, v);
            double still = 1.0, mean = 0.0, extra = 0.0;
            for (int j = 0; j < Jumps; j++)
            {
                still -= p[j];
                mean += p[j] * Math.Exp(mu[j] + v[j] * 0.5);
                extra += p[j] * (mu[j] * mu[j] + v[j]);
            }
            still = Math.Max(0.0, still);
            double plain = forward / Math.Max(1e-9, still + mean);   // the forward with no jump, so the mean of all is the forward
            double value = still * Black(call, plain, strike, body);
            for (int j = 0; j < Jumps; j++)
            {
                value += p[j] * Black(call, plain * Math.Exp(mu[j] + v[j] * 0.5), strike, body + v[j]);
            }
            spread = Math.Sqrt(body + extra);
            return Math.Max(0.0, value);
        }

        /// <summary>
        /// The price a share is expected at by the close of its day, in money: what holding it returns (its cost of equity,
        /// dividends included), its mood fading, less the dividends that leave the price before then.
        /// </summary>
        public static double ShareForward(MarketState state, Stock s, int toDay)
        {
            if (s == null || s.Def == null)
            {
                return 0.0;
            }
            int year = Math.Max(8, state.YearDays);
            double days = Math.Max(0.0, toDay - state.Day + (MarketCatalogue.CloseMinute - MarketCatalogue.MinuteOfPhase(state.Phase)) / 1440.0);
            double k = MarketValuation.CostOfEquity(s, state.Env);
            int closes = 0;
            for (int d = state.Day + 1; d <= toDay; d++)
            {
                closes += MarketCatalogue.TradingDay(d) ? 1 : 0;
            }
            double kappa = Math.Log(2.0) / Math.Max(MarketCatalogue.MoodHalfLifeDaysLeast, MarketCatalogue.MoodHalfLifeYears * MarketCatalogue.TradingDaysPerYear(year));
            double fade = 1.0 - Math.Pow(Math.Max(0.0, 1.0 - kappa), closes + 1);
            double forward = s.Mid * Math.Exp(k * days / year - s.Sent * fade);
            return Math.Max(0.0, forward - DividendsBefore(state, s, toDay, year) / 100.0);
        }

        /// <summary>Cents a share of dividends that go out of the price after now and by a day: one declared, and one its results may declare.</summary>
        public static double DividendsBefore(MarketState state, Stock s, int toDay, int year)
        {
            double cents = 0.0;
            bool declared = s.DeclaredCents > 0 && !s.ExDone && s.RecordDay > 0;
            if (declared && s.RecordDay < toDay)
            {
                cents += s.DeclaredCents;   // it leaves the price at the open after its record day
            }
            if (s.ResultsDay > state.Day && s.ResultsDay <= toDay && s.DpsCents > 0)
            {
                int record = MarketTrading.MarketDays(s.ResultsDay, MarketCatalogue.DividendRecordDays);
                if (record < toDay)
                {
                    cents += s.DpsCents;   // a quarter's dividend as the last, declared with the results
                }
            }
            return cents;
        }
    }
}
