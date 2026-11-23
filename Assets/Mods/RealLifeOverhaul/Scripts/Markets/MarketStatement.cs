using System;

namespace RealLifeOverhaul.Markets
{
    /// <summary>A quarter's statement of the player's company, split as the market splits it: what came in, what it cost, the founder's pay, the tax and what is left. Money.</summary>
    public struct QuarterStatement
    {
        public double Revenue, Costs, Pay, Tax, Net;
        public bool Known;
    }

    /// <summary>
    /// The player's listed company's statement for the quarter so far and the next, from the figures the engine already follows: the books' takings and profit, the founder's pay,
    /// the tax rate and the growth the market believes. The books hold costs as one figure (takings less profit), so the split is the costs, the founder's pay and the tax; an
    /// estimate for the next quarter is said to be one. Game-free; checked offline (EconomyCheck, --segments).
    /// </summary>
    public static class MarketStatement
    {
        /// <summary>
        /// A quarter's takings and what is left of them after the founder's pay and the tax as a statement: with a profit, the profit before pay is what is left over the pay and the
        /// tax; with a loss there is neither, and what the business lost is its costs over its takings.
        /// </summary>
        public static QuarterStatement Split(double revenue, double net, int payBp, double taxRate)
        {
            var q = new QuarterStatement { Revenue = Math.Max(0.0, revenue), Net = net, Known = true };
            if (net > 0.0)
            {
                double afterPay = net / Math.Max(0.01, 1.0 - taxRate);
                double before = afterPay / Math.Max(0.01, 1.0 - payBp / 10000.0);
                q.Pay = before - afterPay;
                q.Tax = afterPay - net;
                q.Costs = Math.Max(0.0, q.Revenue - before);
            }
            else
            {
                q.Costs = Math.Max(0.0, q.Revenue - net);
            }
            return q;
        }

        /// <summary>The quarter so far: what the books have taken in and kept since it began.</summary>
        public static QuarterStatement Now(Stock k, in MarketEnv env)
        {
            return k.Own == null ? default : Split(k.QRev, k.QNi, k.Own.PayBp, MarketInputs.TaxRate(env));
        }

        /// <summary>
        /// The next quarter as the market's own figures project it: takings at the running rate of the last quarter grown at the growth the market believes, over the days of
        /// that quarter, kept at the margin of the year (the usual margin the quarter's passing patch is taken out of). An estimate.
        /// </summary>
        public static QuarterStatement Next(Stock k, in MarketEnv env, int day, int year, out int days)
        {
            days = 0;
            OwnListing o = k.Own;
            if (o == null || o.RevQ <= 0.0)
            {
                return default;
            }
            int quarter = MarketCatalogue.QuarterOf(day, year) + 1;
            days = MarketCatalogue.QuarterStart(quarter + 1, year) - MarketCatalogue.QuarterStart(quarter, year);
            double revenue = o.RevQ * days * Math.Exp(k.Growth * days / Math.Max(8, year));
            double margin = o.RevY > 1e-9 ? o.NiY / o.RevY : o.NiQ / o.RevQ;   // after the founder's pay, before tax
            double afterPay = revenue * MarketValuation.Clamp(margin, -0.5, 0.8);
            double tax = MarketInputs.TaxRate(env);
            return Split(revenue, afterPay > 0.0 ? afterPay * (1.0 - tax) : afterPay, o.PayBp, tax);
        }
    }
}
