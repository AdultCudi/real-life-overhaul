using System;
using System.Collections.Generic;
using System.Globalization;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// The words of the terms of a sale, shared by both interfaces so the old Tech app and the kit say the same figures: the choices, every line of what a choice comes to (shown on the
    /// exits page and again in the game's confirm box), and the lines of a deal that stands. Built when a page is written or a box opened, never per frame.
    /// </summary>
    public static class SaleTermText
    {
        /// <summary>The whole keys of the ways to be paid, in the order of <see cref="TermKind"/>, so the text checks find each one.</summary>
        public static readonly string[] KindKeys =
        {
            "reallife_terms_kind_cash", "reallife_terms_kind_shares", "reallife_terms_kind_slice", "reallife_terms_kind_earn", "reallife_terms_kind_boss"
        };

        public static readonly string[] ShareKeys = { "reallife_terms_share_0", "reallife_terms_share_1", "reallife_terms_share_2" };
        public static readonly string[] SliceKeys = { "reallife_terms_slice_0", "reallife_terms_slice_1", "reallife_terms_slice_2" };
        public static readonly string[] TargetKeys = { "reallife_terms_target_0", "reallife_terms_target_1", "reallife_terms_target_2" };
        public static readonly string[] MeasureKeys = { "reallife_terms_measure_0", "reallife_terms_measure_1" };
        public static readonly string[] YearKeys = { "reallife_terms_years_0", "reallife_terms_years_1" };

        /// <summary>The kind of a deal in words, with its size: "Keeps 20%", "Earn-out", "Contract".</summary>
        public static string KindWords(TermDeal d)
        {
            return d.Kind == TermKind.Slice ? TechText.Fill("reallife_terms_deal_slice", "stake", TechText.Mille(d.Mille)) : TechText.T(KindKeys[Math.Max(0, Math.Min(KindKeys.Length - 1, d.Kind))]);
        }

        /// <summary>The written target of a deal in the words of its measure.</summary>
        public static string TargetWords(TechDirector dir, TermDeal d)
        {
            return Target(dir, d.Measure, SaleTermRules.Aim(d));
        }

        private static string Target(TechDirector dir, int measure, long aim)
        {
            return measure == TermMeasure.Customers ? TechText.Fill("reallife_terms_target_customers", "count", TechText.Whole(aim))
                : TechText.Fill("reallife_terms_target_takings", "amount", TechText.Cents(dir.Level(aim)));
        }

        /// <summary>Every line of what a choice of terms comes to for an offer, in the order a player reads them; empty for cash, whose lines the page already has.</summary>
        public static List<string> Lines(TechDirector dir, TermPlan p, SaleResult r, ExitOffer o, int day)
        {
            var lines = new List<string>(5);
            if (p.Kind == TermKind.Cash)
            {
                return lines;
            }
            if (!p.Ok)
            {
                lines.Add(TechText.T(p.Why ?? SaleTermRules.WhyNone));
                return lines;
            }
            CultureInfo inv = CultureInfo.InvariantCulture;
            string you = TechText.Cents(dir.Level(Math.Max(0L, r.Player - p.Deferred)));
            lines.Add(TechText.Fill("reallife_terms_line_now", "now", TechText.Cents(dir.Level(p.Now)), "price", TechText.Cents(dir.Level(p.Price)), "you", you));
            string target = Target(dir, p.Measure, p.Figure * p.Target / 1000);
            switch (p.Kind)
            {
                case TermKind.Shares:
                    lines.Add(TechText.Fill("reallife_terms_line_shares", "shares", TechText.Whole(p.Shares), "buyer", o.Name, "price", TechText.Cents(p.ShareCents),
                        "amount", TechText.Cents(dir.Level(p.Deferred)), "days", p.LockDays.ToString(inv)));
                    break;
                case TermKind.Slice:
                    lines.Add(TechText.Fill("reallife_terms_line_slice", "stake", TechText.Mille(p.Mille), "amount", TechText.Cents(dir.Level(p.Deferred)),
                        "month", TechText.Cents(dir.Level(p.Figure * SaleTermCatalogue.SliceMargin / 1000 * p.Mille / 1000)),
                        "years", (SaleTermCatalogue.SliceTermDays / SaleTermCatalogue.YearDays).ToString(inv), "buyer", o.Name, "discount", TechText.Mille(SaleTermCatalogue.MinorityDiscount),
                        "cap", TechText.Cents(dir.Level(p.Cap))));
                    break;
                case TermKind.Earn:
                    lines.Add(TechText.Fill("reallife_terms_line_earn", "held", TechText.Cents(dir.Level(p.Deferred)), "target", target,
                        "day", (day + SaleTermCatalogue.EarnDays).ToString(inv), "further", TechText.Cents(dir.Level(p.Further)), "buyer", o.Name));
                    break;
                default:
                    lines.Add(TechText.Fill("reallife_terms_line_boss", "years", p.Years.ToString(inv), "salary", TechText.Cents(dir.Level(p.Salary)), "buyer", o.Name,
                        "cover", TechText.Cents(dir.Level(p.Figure * SaleTermCatalogue.CoverMille / 1000))));
                    lines.Add(p.Shares > 0
                        ? TechText.Fill("reallife_terms_line_boss_shares", "grant", TechText.Cents(dir.Level(p.Grant)), "shares", TechText.Whole(p.Shares), "buyer", o.Name)
                        : TechText.Fill("reallife_terms_line_boss_bonus", "grant", TechText.Cents(dir.Level(p.Grant))));
                    lines.Add(TechText.Fill("reallife_terms_line_earn", "held", TechText.Cents(dir.Level(p.Deferred - p.Grant)), "target", target,
                        "day", (day + SaleTermCatalogue.EarnDays).ToString(inv), "further", TechText.Cents(dir.Level(p.Further)), "buyer", o.Name));
                    lines.Add(TechText.Fill("reallife_terms_line_boss_rules", "days", SaleTermCatalogue.ReviewDays.ToString(inv), "buyer", o.Name,
                        "least", TechText.Cents(dir.Level(p.Figure * SaleTermCatalogue.KeepMille / 1000))));
                    break;
            }
            return lines;
        }

        /// <summary>The body of the game's confirm box: the sale, then every line of its terms.</summary>
        public static string ConfirmBody(TechDirector dir, string firm, ExitOffer o, TermPlan p, SaleResult r, int day)
        {
            List<string> lines = Lines(dir, p, r, o, day);
            return TechText.Fill("reallife_terms_confirm_body", "firm", firm, "buyer", o.Name, "amount", TechText.Cents(dir.Level(o.Price)), "terms", string.Join("\n", lines.ToArray()));
        }

        /// <summary>The lines of a deal that stands: what it was, what it has paid, what is still to come, and where the written target is.</summary>
        public static List<string> DealLines(TechDirector dir, TermDeal d, int day)
        {
            var lines = new List<string>(6);
            CultureInfo inv = CultureInfo.InvariantCulture;
            TermWorld w = dir.TermWorldOf(d);
            lines.Add(TechText.Fill("reallife_terms_deal_head", "firm", d.Firm, "buyer", d.Buyer, "day", d.Day.ToString(inv), "price", TechText.Cents(dir.Level(d.Price)),
                "now", TechText.Cents(dir.Level(d.Now))));
            switch (d.Kind)
            {
                case TermKind.Shares:
                    lines.Add(d.Open
                        ? TechText.Fill("reallife_terms_deal_shares", "shares", TechText.Whole(d.Shares), "days", SaleTermRules.LockLeft(d, day).ToString(inv), "day", d.LockTo.ToString(inv))
                        : TechText.T("reallife_terms_deal_shares_free"));
                    break;
                case TermKind.Slice:
                    lines.Add(TechText.Fill("reallife_terms_deal_stake", "stake", TechText.Mille(d.Mille), "worth", TechText.Cents(dir.Level(SaleTermRules.SliceWorth(d))),
                        "paid", TechText.Cents(dir.Level(d.Paid)), "day", d.EndDay.ToString(inv), "month", TechText.Cents(dir.Level(SaleTermRules.SliceMonth(d, w)))));
                    lines.Add(TechText.Fill("reallife_terms_deal_firm_now", "takings", TechText.Cents(dir.Level(SaleTermRules.Takings(d, w))), "then", TechText.Cents(dir.Level(d.Mrr0))));
                    break;
                case TermKind.Earn:
                    lines.Add(EarnLine(dir, d, w));
                    break;
                case TermKind.Boss:
                    lines.Add(TechText.Fill("reallife_terms_deal_boss", "salary", TechText.Cents(dir.Level(d.Salary)), "day", d.EndDay.ToString(inv), "vested", d.Vested.ToString(inv),
                        "years", d.Years.ToString(inv), "paid", TechText.Cents(dir.Level(d.Paid)), "strikes", d.Strikes.ToString(inv)));
                    if (d.EarnState == 0 && d.Further > 0)
                    {
                        lines.Add(EarnLine(dir, d, w));
                    }
                    break;
            }
            if (!d.Open)
            {
                lines.Add(TechText.T(d.State == TermState.Done ? "reallife_terms_deal_over_done" : d.State == TermState.Resigned ? "reallife_terms_deal_over_resigned"
                    : d.State == TermState.Dismissed ? "reallife_terms_deal_over_dismissed" : d.State == TermState.Gone ? "reallife_terms_deal_over_gone" : "reallife_terms_deal_over_lapsed"));
            }
            return lines;
        }

        private static string EarnLine(TechDirector dir, TermDeal d, in TermWorld w)
        {
            long have = SaleTermRules.Progress(d, w), aim = SaleTermRules.Aim(d);
            string progress = d.Measure == TermMeasure.Customers ? TechText.Whole(have) : TechText.Cents(dir.Level(have));
            return TechText.Fill("reallife_terms_deal_earn", "target", TargetWords(dir, d), "day", d.Due.ToString(CultureInfo.InvariantCulture), "now", progress,
                "pct", TechText.Mille(aim > 0 ? (int)Math.Min(9990L, have * 1000 / aim) : 0), "further", TechText.Cents(dir.Level(d.Further)));
        }
    }
}
