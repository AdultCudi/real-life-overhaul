using System.Collections.Generic;
using RealLifeOverhaul.GameUi;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul.Ui
{
    public sealed partial class TechLive
    {
        private const int PageBooks = 2;
        private static int _tierStep, _siblingStep;

        private sealed partial class Page
        {
            private string AccountLine(BoardPack p)
            {
                return p.Grace
                    ? TechText.Fill("reallife_vc_acct_grace", "balance", Cents(p.Balance), "day", (p.GraceTo + 1).ToString())
                    : TechText.Fill("reallife_vc_acct_line", "balance", Cents(p.Balance), "investors", Cents(p.Investors), "floor", Cents(p.Floor));
            }

            // The firm account in a few lines on the firm's own page: what the wallet must keep, how the board feels, and what is counted as taken.
            private void AccountLines(TechCard card, FirmPlace firm, Cap c)
            {
                if (!StartupMoney.Has(c))
                {
                    return;
                }
                BoardPack p = _d.PackOf(c, firm, _d.Today);
                card.Say(AccountLine(p));
                card.Say(TechText.T(VentureCatalogue.MoodKey(p.Mood)), p.Mood >= 2 ? UiRole.WarnText : UiRole.None);
                if (c.Acc.Taken > 0)
                {
                    card.Say(TechText.T("reallife_vc_cap_taken", "taken", Cents(p.Taken)), UiRole.WarnText);
                }
            }

            private void BooksPage(FirmPlace firm)
            {
                string place = firm.File.Place;
                Cap c = _d.CapFor(place, false);
                _m.Shape = TechShape.Sheet;
                _m.Back = TechText.T("reallife_ui_tech_back_cap");
                _m.Head = new UiHeadModel { Name = TechText.T("reallife_vc_acct_title", "firm", firm.File.Name), Icon = UiIcon.Coin, Role = TechText.FirmName(firm.File.K) };
                if (c == null || !StartupMoney.Has(c))
                {
                    var none = new TechCard(_runs, TechText.T("reallife_ui_tech_books"));
                    none.Say(TechText.T("reallife_vc_acct_none"));
                    _m.Left = new[] { none.Build() };
                    return;
                }
                int day = _d.Today;
                BoardPack p = _d.PackOf(c, firm, day);
                var books = new TechCard(_runs, TechText.T("reallife_ui_tech_books"));
                books.Say(AccountLine(p));
                books.Row(TechText.T("reallife_ui_tech_books_wallet"), Cents(p.Wallet));
                if (c.Acc.Taken > 0)
                {
                    MisuseLine last = c.Acc.Misuse.Count > 0 ? c.Acc.Misuse[c.Acc.Misuse.Count - 1] : null;
                    books.Say(TechText.Fill("reallife_vc_acct_taken", "taken", Cents(p.Taken), "what", last != null ? TechDirector.Describe(last.Key, last.Name) : "-",
                        "day", last != null ? last.Day.ToString() : "-"), UiRole.WarnText);
                }
                books.Say(p.Runway >= 0
                    ? TechText.Fill("reallife_vc_acct_pack", "burn", Cents(p.Burn), "runway", p.Runway.ToString(), "revenue", Cents(p.Mrr), "target", Cents(p.Target), "day", p.PackDay.ToString())
                    : TechText.Fill("reallife_vc_acct_pack_idle", "revenue", Cents(p.Mrr), "target", Cents(p.Target), "day", p.PackDay.ToString()));
                books.Say(p.RelatedDeals > 0
                    ? TechText.Fill("reallife_vc_acct_related", "count", p.RelatedDeals.ToString(), "paid", Cents(p.RelatedPaid), "over", Cents(p.RelatedOver))
                    : TechText.T("reallife_vc_acct_related_none", "fair", Cents(_d.FairPrice(place))));
                books.Say(TechText.Fill("reallife_vc_acct_salary", "drawn", Cents(p.SalaryDrawn), "band", Cents(p.Band)));
                books.Say(c.Acc.Loan > 0
                    ? TechText.Fill("reallife_vc_acct_loan", "owed", Cents(p.LoanOwed), "days", MoneyCatalogue.LoanDays.ToString(), "day", c.Acc.LoanDay.ToString())
                    : c.Acc.LoanUsed ? TechText.T("reallife_vc_acct_loan_used")
                    : TechText.Fill("reallife_vc_acct_loan_free", "max", Cents(_d.Level(StartupMoney.LoanMax(c))), "days", MoneyCatalogue.LoanDays.ToString()));
                books.Say(TechText.T(VentureCatalogue.MoodKey(p.Mood)), p.Mood >= 2 ? UiRole.WarnText : UiRole.None);
                if (p.Fit >= 0)
                {
                    books.Say(TechText.Fill("reallife_vc_acct_fit", "fit", TechText.Mille(p.Fit), "stage", TechText.T(FoundCatalogue.StageKey(p.Stage))));
                }
                if (TechBridges.SkillNow(TalentCatalogue.Law) >= 40)
                {
                    int diligence = StartupMoney.Diligence(c);
                    books.Say(TechText.T(diligence >= 700 ? "reallife_vc_dig_strict" : diligence >= 450 ? "reallife_vc_dig_ordinary" : "reallife_vc_dig_easy", "vc", _d.LeadOf(c)));
                }
                if (Meta.Dev.Verbose)
                {
                    books.Say(TechText.T("reallife_vc_acct_dev", "n", p.Suspicion.ToString(), "threshold", p.Threshold.ToString()));
                }
                _m.Left = new[] { books.Build() };

                var out1 = new TechCard(_runs, TechText.T("reallife_ui_tech_books_out"));
                if (p.Question)
                {
                    out1.Say(TechText.T("reallife_vc_ask_line"), UiRole.WarnText);
                    out1.Button(TechText.T("reallife_vc_button_answer_repay"), UiButtonKind.Primary, UiAction.Count, Do(() => _d.AnswerBoard(place, 0)));
                    out1.Button(TechText.T("reallife_vc_button_answer_explain"), UiButtonKind.Secondary, UiAction.Count, Do(() => _d.AnswerBoard(place, 1)));
                    out1.Button(TechText.T("reallife_vc_button_answer_stonewall"), UiButtonKind.Secondary, UiAction.Count, Do(() => _d.AnswerBoard(place, 2)));
                    // The board's answer comes first: until it is given, the page offers nothing else to press.
                    _m.Right = new[] { out1.Build() };
                    return;
                }
                if (c.Acc.Taken > 0)
                {
                    out1.Button(TechText.T("reallife_vc_button_repay"), UiButtonKind.Primary, UiAction.Count, Do(() => _d.Repay(place)));
                }
                out1.Button(TechText.T("reallife_vc_button_salary"), UiButtonKind.Secondary, UiAction.Count, Do(() => _d.TakeSalary(place)));
                if (c.Acc.Loan > 0)
                {
                    out1.Button(TechText.T("reallife_vc_button_loan_repay"), UiButtonKind.Secondary, UiAction.Count, Do(() => _d.RepayLoan(place)));
                }
                else if (!c.Acc.LoanUsed)
                {
                    out1.Button(TechText.T("reallife_vc_button_loan"), UiButtonKind.Secondary, UiAction.Count, Do(() => _d.TakeLoan(place)));
                }
                out1.Button(TechText.T("reallife_vc_button_dividend"), UiButtonKind.Secondary, UiAction.Count, Do(() => _d.PayDividend(place)));
                if (p.Delayable)
                {
                    out1.Button(TechText.T("reallife_vc_button_delay"), UiButtonKind.Secondary, UiAction.Count, Do(() => _d.DelayPack(place)));
                }
                var work = new List<BuildingRegistration>(8);
                _d.Siblings(place, work);
                if (work.Count == 0)
                {
                    out1.Say(TechText.T("reallife_vc_related_none"));
                }
                else
                {
                    _siblingStep = System.Math.Max(0, System.Math.Min(work.Count - 1, _siblingStep));
                    _tierStep = System.Math.Max(0, System.Math.Min(2, _tierStep));
                    long fair = _d.FairPrice(place);
                    int n = work.Count;
                    TechRun Pick(int i) => Go(v => _siblingStep = i);
                    TechRun Tier(int i) => Go(v => _tierStep = i);
                    out1.Step(TechText.T("reallife_ui_tech_books_from"), TalentGame.NameOf(work[_siblingStep]), null, n > 1, n > 1, Pick((_siblingStep + n - 1) % n), Pick((_siblingStep + 1) % n));
                    long price = fair * (_tierStep == 0 ? 1000 : _tierStep == 1 ? 1500 : 2000) / 1000;
                    out1.Step(TechText.T("reallife_ui_tech_books_price"), TechText.T(VentureCatalogue.TierKeys[_tierStep], "price", Cents(price)),
                        null, true, true, Tier((_tierStep + 2) % 3), Tier((_tierStep + 1) % 3));
                    int business = _siblingStep, tier = _tierStep;
                    out1.Button(TechText.T("reallife_vc_button_related"), UiButtonKind.Secondary, UiAction.Count, Do(() => _d.PayRelated(place, business, tier)));
                }
                _m.Right = new[] { out1.Build() };
            }
        }
    }
}
