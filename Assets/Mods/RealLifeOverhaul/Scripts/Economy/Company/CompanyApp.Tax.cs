using System.Collections.Generic;
using RealLifeOverhaul.GameUi;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The tax half of the Tax and licences tab. The table: the return, the accountant, and each business's cash off
    /// the books. The box: the return with its figures and the one button that files it; the three accountants to
    /// step through; a business's share off the books with what it saves and what it risks.
    /// </summary>
    public sealed partial class CompanyApp
    {
        private int _tier = 1;   // the accountant whose terms are showing

        partial void WriteTax(InfoBox box, CompanyState state, int day, ref string selected)
        {
            if (!_director.On(CompanyPart.Tax))
            {
                return;
            }
            TaxFile t = state.Tax;
            bool late = t.ReturnWaiting && day > t.ReturnDue;
            Pick("ret", CompanyText.T("reallife_co_row_return"), CompanyText.T("reallife_co_row_quarter", "quarter", (t.ReturnWaiting ? t.ReturnQuarter : t.Quarter).ToString()),
                CompanyText.Day(t.ReturnWaiting ? t.ReturnDue : t.QuarterFrom + _director.QuarterDays),
                CompanyText.T(late ? "reallife_co_row_return_late" : t.ReturnWaiting ? "reallife_co_row_return_due" : "reallife_co_row_return_running"), late ? 2 : t.ReturnWaiting ? 1 : 0, ref selected);
            Last.Kind = CompanyTables.AboutTax;
            Last.Key[2] = t.ReturnWaiting ? t.ReturnDue : t.QuarterFrom + _director.QuarterDays;
            Pick("acc", CompanyText.T("reallife_co_row_accountant"), CompanyText.Accountant(t.Accountant), t.Accountant >= 0 ? CompanyText.Day(t.AccountantNext) : string.Empty,
                t.Accountant >= 0 ? CompanyText.T("reallife_co_row_a_month", "amount", CompanyText.Money(TaxRules.Fee(t.Accountant, _director.Index, _director.Hidden() > 0))) : string.Empty, 0, ref selected);
            Last.Kind = CompanyTables.AboutTax;
            Last.Key[2] = t.Accountant >= 0 ? t.AccountantNext : double.NaN;
            IReadOnlyList<CompanyShop> shops = _director.Shops;
            for (int i = 0; Settings.EconomyCompanyHide && i < shops.Count; i++)
            {
                Pick("hide:" + shops[i].File.Place, shops[i].Name, CompanyText.T("reallife_co_row_hidden"), string.Empty, shops[i].File.Hide + "%", shops[i].File.Hide > 0 ? 1 : 0, ref selected);
                Last.Kind = CompanyTables.AboutHidden;
            }

            string place = Picked("hide:");
            CompanyShop hide = place != null && Settings.EconomyCompanyHide ? _director.ShopAt(place) : null;
            if (_pick == "ret")
            {
                WriteReturn(box, t, day);
            }
            else if (_pick == "acc")
            {
                WriteAccountant(box, t);
            }
            else if (hide != null)
            {
                WriteHidden(box, t, hide);
            }
            else if (Picked("lic:") == null || !_director.On(CompanyPart.Licences))
            {
                box.Title(CompanyText.T("reallife_co_tab_tax"));
                if (t.Owed > 0)
                {
                    box.Line(FinanceText.Pair("reallife_co_tax_owed", CompanyText.Money(t.Owed)));
                }
                if (Settings.EconomyCompanyHide)
                {
                    box.Line(FinanceText.Pair("reallife_co_tax_risk", CompanyText.Share(TaxRules.AuditChance(t.Risk, t.Accountant) / 10)));
                }
                box.Line(FinanceText.Pair("reallife_co_tax_quarter", CompanyText.T("reallife_co_tax_quarter_value", "quarter", t.Quarter.ToString(), "day", (t.QuarterFrom + _director.QuarterDays).ToString())));
                box.Line(FinanceText.Pair("reallife_co_tax_costs", CompanyText.Money(TaxRules.CostsSum(t))));
                box.Line(FinanceText.Pair("reallife_co_row_accountant", CompanyText.Accountant(t.Accountant)));
                box.Line(CompanyText.T("reallife_co_tax_game", "percent", CompanyGame.TaxPercent.ToString()));
                box.Line(CompanyText.T("reallife_co_tax_deduct"));
                Guide(box);
            }
        }

        private void WriteReturn(InfoBox box, TaxFile t, int day)
        {
            box.Title(CompanyText.T("reallife_co_row_return"));
            if (!t.ReturnWaiting)
            {
                box.Line(CompanyText.Fill("reallife_co_return_running", "quarter", t.Quarter.ToString(), "from", t.QuarterFrom.ToString(), "day", (t.QuarterFrom + _director.QuarterDays).ToString(),
                    "days", CompanyCatalogue.ReturnDays.ToString()));
                box.Headline("reallife_co_return_costs");
                for (int k = 0; k < t.Costs.Length; k++)
                {
                    box.Line(CompanyText.T(CompanyText.CostKeys[k]) + ": <b>" + CompanyText.Money(t.Costs[k]) + "</b>");
                }
                BackButton(box);
                return;
            }
            box.Line(CompanyText.T(day > t.ReturnDue ? "reallife_co_return_late" : "reallife_co_return_due", "day", t.ReturnDue.ToString(), "amount", CompanyText.Money(TaxRules.Penalty(t.ReturnTakings)),
                "count", t.ReturnPenalties.ToString()));
            box.Line(FinanceText.Pair("reallife_co_return_quarter", t.ReturnQuarter.ToString()));
            box.Line(FinanceText.Pair("reallife_co_return_takings", CompanyText.Money(t.ReturnTakings)));
            box.Line(FinanceText.Pair("reallife_co_return_deduct", CompanyText.Money(t.ReturnCosts)));
            bool helped = TaxRules.Files(t.Accountant, _director.Hidden() > 0);
            if (helped)
            {
                box.Line(CompanyText.T("reallife_co_return_rebate", "name", CompanyText.Accountant(t.Accountant), "amount", CompanyText.Money(TaxRules.Rebate(t.Accountant, t.ReturnCosts))));
            }
            else if (t.Accountant >= 0)
            {
                box.Line(CompanyText.T("reallife_co_return_wont", "name", CompanyText.Accountant(t.Accountant)));
            }
            bool order = CompanyGame.BooksInOrder();
            if (!order)
            {
                box.Line(CompanyText.T("reallife_co_return_books"));
            }
            var buttons = box.ButtonRow();
            if (order)
            {
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_co_return_file", _director.FileReturn);
            }
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_co_back", Back);
            Guide(box);
        }

        private void WriteAccountant(InfoBox box, TaxFile t)
        {
            bool hiding = _director.Hidden() > 0;
            int tier = CompanyCatalogue.Clamp(_tier, 0, CompanyCatalogue.Accountants - 1);
            box.Title(CompanyText.T("reallife_co_row_accountant"));
            box.Line(FinanceText.Pair("reallife_co_acc_hired", CompanyText.Accountant(t.Accountant)));
            if (!box.HasSelector || !box.Selector(CompanyCatalogue.Accountants, tier, CompanyText.Accountant, i =>
                {
                    _tier = i;
                    Refresh();
                }))
            {
                box.Line(CompanyText.Accountant(tier));
            }
            box.Line(FinanceText.Pair("reallife_co_acc_fee", CompanyText.T("reallife_co_row_a_month", "amount", CompanyText.Money(TaxRules.Fee(tier, _director.Index, hiding)))));
            box.Line(CompanyText.T("reallife_co_acc_files", "days", CompanyCatalogue.ReturnDays.ToString()));
            box.Line(CompanyText.T("reallife_co_acc_saves", "share", CompanyText.Share(CompanyCatalogue.AccountantSaving[tier])));
            box.Line(CompanyText.T("reallife_co_acc_risk", "share", CompanyText.Share(CompanyCatalogue.AccountantRisk[tier])));
            if (tier == 0)
            {
                box.Line(CompanyText.T("reallife_co_acc_careless", "chance", CompanyText.Share(CompanyCatalogue.AccountantErrorChance), "more", CompanyText.Share(CompanyCatalogue.AccountantHidingFee - 1000)));
            }
            else
            {
                box.Line(CompanyText.T(tier == CompanyCatalogue.Accountants - 1 ? "reallife_co_acc_honest_resigns" : "reallife_co_acc_honest_stays"));
            }
            var buttons = box.ButtonRow();
            int chosen = tier;
            if (t.Accountant != tier && !TaxRules.Resigns(tier, hiding))
            {
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_co_acc_hire", () => _director.Hire(chosen));
            }
            if (t.Accountant >= 0)
            {
                InfoBox.Button(buttons, InfoBox.Negative, "reallife_co_acc_dismiss", _director.Dismiss);
            }
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_co_back", Back);
            Guide(box);
        }

        private void WriteHidden(InfoBox box, TaxFile t, CompanyShop shop)
        {
            string place = shop.File.Place;
            int level = _director.Level, most = TaxRules.HideMost(level), steps = 0, at = 0;
            for (int i = 0; i < CompanyCatalogue.HideSteps.Length && CompanyCatalogue.HideSteps[i] <= most; i++)
            {
                steps++;
                at = CompanyCatalogue.HideSteps[i] == shop.File.Hide ? i : at;
            }
            box.Title(CompanyText.T("reallife_co_hide_title", "business", shop.Name));
            box.Line(CompanyText.T("reallife_co_hide_what", "percent", CompanyGame.TaxPercent.ToString()));
            if (!box.HasSelector || !box.Selector(steps, at, i => CompanyCatalogue.HideSteps[i] + "%", i => _director.SetHide(place, CompanyCatalogue.HideSteps[i])))
            {
                box.Line(shop.File.Hide + "%");
            }
            box.Line(FinanceText.Pair("reallife_co_hide_saved", CompanyText.Money(t.Saved)));
            box.Line(FinanceText.Pair("reallife_co_tax_risk", CompanyText.Share(TaxRules.AuditChance(t.Risk, t.Accountant) / 10)));
            box.Line(CompanyText.T("reallife_co_hide_risk", "grow", (CompanyCatalogue.RiskPerPercent / 100f).ToString("0.##"), "fall", (CompanyCatalogue.RiskDecay / 100f).ToString("0.##"),
                "most", CompanyText.Share(CompanyCatalogue.RiskMost / 10)));
            box.Line(CompanyText.T(level == CompanyCatalogue.Relaxed ? "reallife_co_hide_audit_relaxed" : "reallife_co_hide_audit", "fine", CompanyText.Share(CompanyCatalogue.FineShare[level]),
                "sum", CompanyText.Money(CompanyCatalogue.CourtSum)));
            box.Line(CompanyText.T("reallife_co_hide_threshold"));
            BackButton(box);
        }
    }
}
