using System;
using System.Collections.Generic;
using RealLifeOverhaul.Ui;

namespace RealLifeOverhaul.Schemes
{
    /// <summary>What an answer on a page of the schemes does: the director's call it stands for.</summary>
    public enum SchemeDo
    {
        Leave = 0,     // nothing is done: the page closes
        Claim = 1,     // an invented claim at Place on cover A, of size B
        Expense = 2,   // an invented expense of size B
        OpenLift = 3,  // the page of the dressed books at Place
        OpenWash = 4,  // the page of the till at Place
        Lift = 5,      // the books at Place show A thousandths over the truth
        Wash = 6,      // the till at Place washes A thousandths of a day's takings a day
        Club = 7,      // a club sold from Place, of size B
        FundPut = 8,   // the money goes into the fund on offer
        FundLook = 9,  // the fund on offer is looked at first
        FundDown = 10  // the fund on offer is turned down
    }

    /// <summary>One press: what it does, where, and its figures.</summary>
    public struct SchemePress
    {
        public SchemeDo Do;
        public string Place;
        public int A, B;

        public static SchemePress Of(SchemeDo what, string place = null, int a = 0, int b = 0)
        {
            return new SchemePress { Do = what, Place = place, A = a, B = b };
        }
    }

    /// <summary>One page of the schemes' panel: what it is about in plain words, and up to three answers, each with what it brings and what it risks.</summary>
    public sealed class SchemePage
    {
        public const int MostChoices = 3;

        public string Title;
        public string Kicker;
        public string Body;
        public readonly string[] Choices = new string[MostChoices];
        public readonly string[] Hints = new string[MostChoices];
        public readonly SchemePress[] Presses = new SchemePress[MostChoices];
        public readonly List<UiConsequence>[] Lines = new List<UiConsequence>[MostChoices];
        public int Count;

        public SchemePage Add(string choice, string hint, SchemePress press)
        {
            if (Count < MostChoices)
            {
                Choices[Count] = choice;
                Hints[Count] = hint;
                Presses[Count] = press;
                Count++;
            }
            return this;
        }

        /// <summary>A line under the answer added last: what it brings (+1), what it risks (-1), the money it costs (UiConsequence.Loss) or neither (0).</summary>
        public SchemePage Line(string text, int tone)
        {
            if (Count > 0 && !string.IsNullOrEmpty(text))
            {
                List<UiConsequence> lines = Lines[Count - 1] ?? (Lines[Count - 1] = new List<UiConsequence>(2));
                lines.Add(new UiConsequence { Text = text, Tone = tone, Change = tone == UiConsequence.Loss ? -1 : 0 });
            }
            return this;
        }
    }

    /// <summary>The words a page is written with: a text by its key and what fills it, and money as the game writes it. The game's, or the checks' own.</summary>
    public sealed class SchemeWords
    {
        public Func<string, string[], string> Fill;
        public Func<long, string> Money;

        public string T(string key, params string[] pairs) => Fill != null ? Fill(key, pairs) : key;

        public string M(long amount) => Money != null ? Money(amount) : amount.ToString(System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>Thousandths as a whole per cent: "25%".</summary>
        public static string Percent(int thousandths) => (thousandths / 10).ToString(System.Globalization.CultureInfo.InvariantCulture) + "%";
    }

    /// <summary>
    /// The schemes' pages as plain data, each from plain figures: what the scheme is, the gain and the risk said before
    /// the player commits, the two sizes and the way out. The director shows them and carries out the press an answer
    /// holds; the checks build each one and read every answer's press. Game-free.
    /// </summary>
    public static class SchemePages
    {
        public const int Gain = 1, Risk = -1;

        private static SchemePage Page(SchemeWords w, string titleKey, string kicker, string body)
        {
            return new SchemePage { Title = w.T(titleKey), Kicker = kicker ?? string.Empty, Body = body };
        }

        private static SchemePage Leave(SchemePage page, SchemeWords w)
        {
            return page.Add(w.T("reallife_scheme_leave"), w.T("reallife_scheme_leave_hint"), SchemePress.Of(SchemeDo.Leave));
        }

        // The two sizes and the way out, the same on every page that offers them: a small one is rarely looked into, a large one always.
        private static SchemePage Sizes(SchemePage page, SchemeWords w, SchemeDo what, string place, int a, string smallHint, string smallGain, string largeHint, string largeGain)
        {
            page.Add(w.T("reallife_scheme_size_small"), smallHint, SchemePress.Of(what, place, a, SchemeCatalogue.Small)).Line(smallGain, Gain).Line(w.T("reallife_scheme_risk_small"), 0)
                .Add(w.T("reallife_scheme_size_large"), largeHint, SchemePress.Of(what, place, a, SchemeCatalogue.Large)).Line(largeGain, Gain).Line(w.T("reallife_scheme_risk_large"), Risk);
            return Leave(page, w);
        }

        /// <summary>An insurance claim for a loss that never happened, on a cover in force at a business.</summary>
        public static SchemePage Claim(SchemeWords w, string place, string business, int cover, int coverMost)
        {
            string small = w.M(SchemeRules.ClaimLoss(coverMost, SchemeCatalogue.Small)), large = w.M(SchemeRules.ClaimLoss(coverMost, SchemeCatalogue.Large));
            SchemePage page = Page(w, "reallife_scheme_claim_title", business,
                w.T("reallife_scheme_claim_body", "business", business, "share", SchemeWords.Percent(SchemeCatalogue.SurchargePermille)));
            return Sizes(page, w, SchemeDo.Claim, place, cover, w.T("reallife_scheme_claim_hint", "amount", small), w.T("reallife_scheme_claim_gain", "amount", small),
                w.T("reallife_scheme_claim_hint", "amount", large), w.T("reallife_scheme_claim_gain", "amount", large));
        }

        /// <summary>An invented expense on the return: worth the game's tax on it.</summary>
        public static SchemePage Expense(SchemeWords w, long averageTakings, int taxPercent)
        {
            long small = SchemeRules.ExpenseAmount(averageTakings, SchemeCatalogue.Small), large = SchemeRules.ExpenseAmount(averageTakings, SchemeCatalogue.Large);
            SchemePage page = Page(w, "reallife_scheme_expense_title", w.T("reallife_scheme_expense_kicker"), w.T("reallife_scheme_expense_body", "percent",
                taxPercent.ToString(System.Globalization.CultureInfo.InvariantCulture), "share", SchemeWords.Percent(SchemeCatalogue.SurchargePermille)));
            return Sizes(page, w, SchemeDo.Expense, null, 0, w.T("reallife_scheme_expense_hint", "amount", w.M(small)),
                w.T("reallife_scheme_expense_gain", "amount", w.M(SchemeRules.ExpenseGain(small, taxPercent))), w.T("reallife_scheme_expense_hint", "amount", w.M(large)),
                w.T("reallife_scheme_expense_gain", "amount", w.M(SchemeRules.ExpenseGain(large, taxPercent))));
        }

        /// <summary>A business's second set of books: the sales that never happened, and the cash that goes through the till.</summary>
        public static SchemePage Books(SchemeWords w, string place, string business, int lift, int wash, long cash)
        {
            SchemePage page = Page(w, "reallife_scheme_books_title", business, w.T("reallife_scheme_books_body", "business", business, "cash", w.M(cash)));
            page.Add(w.T("reallife_scheme_books_lift"), w.T("reallife_scheme_books_lift_hint", "share", SchemeWords.Percent(lift)), SchemePress.Of(SchemeDo.OpenLift, place))
                .Add(w.T("reallife_scheme_books_wash"), w.T("reallife_scheme_books_wash_hint", "share", SchemeWords.Percent(wash)), SchemePress.Of(SchemeDo.OpenWash, place));
            return Leave(page, w);
        }

        /// <summary>The dressed books of a business: a little, a lot, or plain again.</summary>
        public static SchemePage Lift(SchemeWords w, string place, string business, int now)
        {
            int little = SchemeCatalogue.LiftSteps[1], lot = SchemeCatalogue.LiftSteps[SchemeCatalogue.LiftSteps.Length - 1];
            SchemePage page = Page(w, "reallife_scheme_lift_title", business, w.T("reallife_scheme_lift_body", "business", business, "now", SchemeWords.Percent(now),
                "share", SchemeWords.Percent(SchemeCatalogue.SurchargePermille)));
            page.Add(w.T("reallife_scheme_lift_set", "share", SchemeWords.Percent(little)), w.T("reallife_scheme_lift_little_hint"), SchemePress.Of(SchemeDo.Lift, place, little))
                .Line(w.T("reallife_scheme_lift_risk_little"), 0)
                .Add(w.T("reallife_scheme_lift_set", "share", SchemeWords.Percent(lot)), w.T("reallife_scheme_lift_lot_hint"), SchemePress.Of(SchemeDo.Lift, place, lot))
                .Line(w.T("reallife_scheme_lift_risk_lot"), Risk);
            return now > 0 ? page.Add(w.T("reallife_scheme_lift_plain"), w.T("reallife_scheme_lift_plain_hint"), SchemePress.Of(SchemeDo.Lift, place, 0)) : Leave(page, w);
        }

        /// <summary>The till of a business: how much of the cash in hand it takes a day as sales. A business with no counter can take none.</summary>
        public static SchemePage Wash(SchemeWords w, string place, string business, bool till, long average, int now, long cash, bool watched, int level, int taxPercent)
        {
            int little = SchemeCatalogue.WashSteps[1], lot = SchemeCatalogue.WashSteps[SchemeCatalogue.WashSteps.Length - 1];
            string body = w.T(till ? "reallife_scheme_wash_body" : "reallife_scheme_wash_body_no_till", "business", business, "cash", w.M(cash),
                "cut", SchemeWords.Percent(SchemeCatalogue.WashCut), "report", w.M(SchemeCatalogue.AtPrices(SchemeCatalogue.ReportFrom, level)), "now", SchemeWords.Percent(now),
                "percent", SchemeCatalogue.Clamp(taxPercent, 0, 100).ToString(System.Globalization.CultureInfo.InvariantCulture));
            if (watched)
            {
                body += "\n\n" + w.T("reallife_scheme_wash_watched");
            }
            SchemePage page = Page(w, "reallife_scheme_wash_title", business, body);
            if (till)
            {
                page.Add(w.T("reallife_scheme_wash_set", "share", SchemeWords.Percent(little)), w.T("reallife_scheme_wash_hint", "amount", w.M(SchemeCatalogue.Share(average, little))),
                        SchemePress.Of(SchemeDo.Wash, place, little))
                    .Line(w.T("reallife_scheme_wash_risk_little"), 0)
                    .Add(w.T("reallife_scheme_wash_set", "share", SchemeWords.Percent(lot)), w.T("reallife_scheme_wash_hint", "amount", w.M(SchemeCatalogue.Share(average, lot))),
                        SchemePress.Of(SchemeDo.Wash, place, lot))
                    .Line(w.T("reallife_scheme_wash_risk_lot"), Risk);
            }
            return now > 0 ? page.Add(w.T("reallife_scheme_wash_stop"), w.T("reallife_scheme_wash_stop_hint"), SchemePress.Of(SchemeDo.Wash, place, 0)) : Leave(page, w);
        }

        /// <summary>
        /// A club that does not exist, sold from a business to its district's households. Where the city's people are not
        /// this machine's own, the page says so and offers nothing: no money moves.
        /// </summary>
        public static SchemePage Club(SchemeWords w, string place, string business, bool people, int level)
        {
            if (!people)
            {
                return Leave(Page(w, "reallife_scheme_club_title", business, w.T("reallife_scheme_note_club_no_people")), w);
            }
            long stake = SchemeCatalogue.AtPrices(SchemeCatalogue.ClubStakeMost, level);
            SchemePage page = Page(w, "reallife_scheme_club_title", business, w.T("reallife_scheme_club_body", "business", business, "stake", w.M(stake),
                "share", SchemeWords.Percent(SchemeCatalogue.SurchargePermille)));
            string small = SchemeCatalogue.ClubSmall.ToString(System.Globalization.CultureInfo.InvariantCulture);
            string large = SchemeCatalogue.ClubLarge.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return Sizes(page, w, SchemeDo.Club, place, 0, w.T("reallife_scheme_club_hint", "count", small), w.T("reallife_scheme_club_gain", "amount", w.M(SchemeCatalogue.ClubSmall * stake)),
                w.T("reallife_scheme_club_hint", "count", large), w.T("reallife_scheme_club_gain", "amount", w.M(SchemeCatalogue.ClubLarge * stake)));
        }

        /// <summary>A rival's fund on offer: put the money in, have it looked at first (once), or turn it down. What a look found is said on the page.</summary>
        public static SchemePage Fund(SchemeWords w, string rival, string holding, SchemeFund f, int level)
        {
            long fee = SchemeRules.FundFee(f.Amount, level), back = SchemeRules.FundBack(f.Amount);
            string body = w.T("reallife_scheme_fund_body", "rival", rival, "holding", holding, "amount", w.M(f.Amount), "back", w.M(back),
                "days", SchemeCatalogue.FundDays.ToString(System.Globalization.CultureInfo.InvariantCulture), "day", f.Due.ToString(System.Globalization.CultureInfo.InvariantCulture));
            if (f.Checked)
            {
                body += "\n\n" + w.T(f.Sham ? "reallife_scheme_fund_look_sham" : "reallife_scheme_fund_look_honest", "holding", holding);
            }
            SchemePage page = Page(w, "reallife_scheme_fund_title", holding, body);
            page.Add(w.T("reallife_scheme_fund_put", "amount", w.M(f.Amount)), w.T("reallife_scheme_fund_put_hint"), SchemePress.Of(SchemeDo.FundPut))
                .Line(w.T("reallife_scheme_fund_put_cost", "amount", w.M(f.Amount)), UiConsequence.Loss)
                .Line(w.T("reallife_scheme_fund_put_gain", "amount", w.M(back)), f.Checked && f.Sham ? Risk : 0);
            if (!f.Checked)
            {
                page.Add(w.T("reallife_scheme_fund_look"), w.T("reallife_scheme_fund_look_hint"), SchemePress.Of(SchemeDo.FundLook))
                    .Line(w.T("reallife_scheme_fund_look_cost", "amount", w.M(fee)), UiConsequence.Loss);
            }
            return page.Add(w.T("reallife_scheme_fund_down"), w.T("reallife_scheme_fund_down_hint"), SchemePress.Of(SchemeDo.FundDown));
        }
    }
}
