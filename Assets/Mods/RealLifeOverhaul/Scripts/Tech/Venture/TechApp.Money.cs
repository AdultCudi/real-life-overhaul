using System.Collections.Generic;
using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Tech
{
    /// <summary>The Funding tab's firm account: what the firm holds, what the wallet must keep, the board's pack and mood, and the lawful ways to take money out.</summary>
    public sealed partial class TechApp
    {
        private const int PageBooks = 2;
        private int _tierStep, _siblingStep;
        private readonly List<BuildingRegistration> _siblings = new List<BuildingRegistration>(8);

        // The firm account on the firm's page in a few lines, and the button to the page of its own.
        private void WriteAccount(InfoBox box, FirmPlace firm, Cap c, int day)
        {
            if (!StartupMoney.Has(c))
            {
                return;
            }
            BoardPack p = _director.PackOf(c, firm, day);
            box.Line(AccountLine(p));
            box.Line(TechText.T(VentureCatalogue.MoodKey(p.Mood)), p.Mood >= 2 ? CellTone.Yellow : CellTone.Plain);
            if (c.Acc.Taken > 0)
            {
                box.Line(TechText.T("reallife_vc_cap_taken", "taken", TechText.Cents(p.Taken)), CellTone.Yellow);
            }
        }

        private static string AccountLine(BoardPack p)
        {
            return p.Grace
                ? TechText.Fill("reallife_vc_acct_grace", "balance", TechText.Cents(p.Balance), "day", (p.GraceTo + 1).ToString())
                : TechText.Fill("reallife_vc_acct_line", "balance", TechText.Cents(p.Balance), "investors", TechText.Cents(p.Investors), "floor", TechText.Cents(p.Floor));
        }

        private void WriteBooks(InfoBox box, FirmPlace firm, int day)
        {
            string place = firm.File.Place;
            Cap c = _director.CapFor(place, false);
            box.Title(TechText.T("reallife_vc_acct_title", "firm", firm.File.Name));
            if (c == null || !StartupMoney.Has(c))
            {
                box.Note(TechText.T("reallife_vc_acct_none"));
                InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_roles_button_back", BackToFirm);
                return;
            }
            BoardPack p = _director.PackOf(c, firm, day);
            box.Line(AccountLine(p));
            box.Line(TechText.T("reallife_vc_acct_wallet", "wallet", TechText.Cents(p.Wallet)));
            if (c.Acc.Taken > 0)
            {
                MisuseLine last = c.Acc.Misuse.Count > 0 ? c.Acc.Misuse[c.Acc.Misuse.Count - 1] : null;
                box.Line(TechText.Fill("reallife_vc_acct_taken", "taken", TechText.Cents(p.Taken), "what", last != null ? TechDirector.Describe(last.Key, last.Name) : "-",
                    "day", last != null ? last.Day.ToString() : "-"), CellTone.Yellow);
            }
            box.Line(p.Runway >= 0
                ? TechText.Fill("reallife_vc_acct_pack", "burn", TechText.Cents(p.Burn), "runway", p.Runway.ToString(), "revenue", TechText.Cents(p.Mrr), "target", TechText.Cents(p.Target),
                    "day", p.PackDay.ToString())
                : TechText.Fill("reallife_vc_acct_pack_idle", "revenue", TechText.Cents(p.Mrr), "target", TechText.Cents(p.Target), "day", p.PackDay.ToString()));
            box.Line(p.RelatedDeals > 0
                ? TechText.Fill("reallife_vc_acct_related", "count", p.RelatedDeals.ToString(), "paid", TechText.Cents(p.RelatedPaid), "over", TechText.Cents(p.RelatedOver))
                : TechText.T("reallife_vc_acct_related_none", "fair", TechText.Cents(_director.FairPrice(place))));
            box.Line(TechText.Fill("reallife_vc_acct_salary", "drawn", TechText.Cents(p.SalaryDrawn), "band", TechText.Cents(p.Band)));
            box.Line(c.Acc.Loan > 0
                ? TechText.Fill("reallife_vc_acct_loan", "owed", TechText.Cents(p.LoanOwed), "days", MoneyCatalogue.LoanDays.ToString(), "day", c.Acc.LoanDay.ToString())
                : c.Acc.LoanUsed ? TechText.T("reallife_vc_acct_loan_used")
                : TechText.Fill("reallife_vc_acct_loan_free", "max", TechText.Cents(_director.Level(StartupMoney.LoanMax(c))), "days", MoneyCatalogue.LoanDays.ToString()));
            box.Line(TechText.T(VentureCatalogue.MoodKey(p.Mood)), p.Mood >= 2 ? CellTone.Yellow : CellTone.Plain);
            if (TechBridges.SkillNow(TalentCatalogue.Law) >= 40)
            {
                int diligence = StartupMoney.Diligence(c);
                box.Line(TechText.T(diligence >= 700 ? "reallife_vc_dig_strict" : diligence >= 450 ? "reallife_vc_dig_ordinary" : "reallife_vc_dig_easy", "vc", LeadOf(c)));
            }
            if (Meta.Dev.Verbose)
            {
                box.Line(TechText.T("reallife_vc_acct_dev", "n", p.Suspicion.ToString(), "threshold", p.Threshold.ToString()));
            }
            if (p.Question)
            {
                box.Line(TechText.T("reallife_vc_ask_line"), CellTone.Yellow);
                Transform answer = box.ButtonRow();
                InfoBox.Button(answer, InfoBox.Positive, "reallife_vc_button_answer_repay", () => Do(() => _director.AnswerBoard(place, 0)));
                InfoBox.Button(answer, InfoBox.Additional, "reallife_vc_button_answer_explain", () => Do(() => _director.AnswerBoard(place, 1)));
                InfoBox.Button(answer, InfoBox.Additional, "reallife_vc_button_answer_stonewall", () => Do(() => _director.AnswerBoard(place, 2)));
            }
            Transform out1 = box.ButtonRow();
            if (c.Acc.Taken > 0)
            {
                InfoBox.Button(out1, InfoBox.Positive, "reallife_vc_button_repay", () => Do(() => _director.Repay(place)));
            }
            InfoBox.Button(out1, InfoBox.Additional, "reallife_vc_button_salary", () => Do(() => _director.TakeSalary(place)));
            if (c.Acc.Loan > 0)
            {
                InfoBox.Button(out1, InfoBox.Additional, "reallife_vc_button_loan_repay", () => Do(() => _director.RepayLoan(place)));
            }
            else if (!c.Acc.LoanUsed)
            {
                InfoBox.Button(out1, InfoBox.Additional, "reallife_vc_button_loan", () => Do(() => _director.TakeLoan(place)));
            }
            Transform out2 = box.ButtonRow();
            InfoBox.Button(out2, InfoBox.Additional, "reallife_vc_button_dividend", () => Do(() => _director.PayDividend(place)));
            if (p.Delayable)
            {
                InfoBox.Button(out2, InfoBox.Additional, "reallife_vc_button_delay", () => Do(() => _director.DelayPack(place)));
            }
            WriteRights(box, place, c, day, out2);
            WriteRelated(box, place);
            InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_roles_button_back", BackToFirm);
            Meta.Guide.Point(box, StartupsGuide);
        }

        private void WriteRelated(InfoBox box, string place)
        {
            _director.Siblings(place, _siblings);
            if (_siblings.Count == 0)
            {
                box.Note(TechText.T("reallife_vc_related_none"));
                return;
            }
            _siblingStep = Mathf.Clamp(_siblingStep, 0, _siblings.Count - 1);
            _tierStep = Mathf.Clamp(_tierStep, 0, 2);
            long fair = _director.FairPrice(place);
            if (box.HasSelector)
            {
                box.Selector(_siblings.Count, _siblingStep, i => TechText.T("reallife_vc_related_pick", "name", TalentGame.NameOf(_siblings[i])), i => Set(() => _siblingStep = i));
                box.Selector(3, _tierStep, i => TechText.T(VentureCatalogue.TierKeys[i], "price", TechText.Cents(fair * (i == 0 ? 1000 : i == 1 ? 1500 : 2000) / 1000)),
                i => Set(() => _tierStep = i));
            }
            int business = _siblingStep, tier = _tierStep;
            InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_vc_button_related", () => Do(() => _director.PayRelated(place, business, tier)));
        }

        private static string LeadOf(Cap c)
        {
            Holder lead = null;
            for (int i = 0; i < c.Holders.Count; i++)
            {
                if (c.Holders[i].Kind == (int)HolderKind.Vc && (lead == null || c.Holders[i].Invested > lead.Invested))
                {
                    lead = c.Holders[i];
                }
            }
            return lead != null ? VentureCatalogue.Firms[TalentCatalogue.Clamp(lead.Who, 0, VentureCatalogue.FirmCount - 1)].Name : string.Empty;
        }

        private void BackToFirm()
        {
            _page = PageCap;
            Refresh();
        }
    }
}
