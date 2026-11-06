using System.Collections.Generic;
using RealLifeOverhaul.GameUi;
using UI.Smartphone;
using UI.Smartphone.Apps.Persona;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>The Overview tab: cash, debts, what leaves tomorrow and net worth.</summary>
    public sealed partial class FinanceApp
    {
        private readonly List<DebtLine> _debts = new List<DebtLine>(6);

        private void WriteOverview(InfoBox box, CreditScore score)
        {
            GameInstance save = SaveGameManager.Current;
            List<Loan> loans = save.Loans;
            CreditFile file = CreditLedger.File;
            Obligations due = Books.Tomorrow(file);
            float owedLoans = 0f;
            _spec = FinanceTables.Overview;
            for (int i = 0; i < loans.Count; i++)
            {
                Loan loan = loans[i];
                owedLoans += loan.remainingAmount;
                TableRow row = Row("loan:" + i);
                DebtCells(row, GameLoans.BankName(loan), FinanceTables.OfLoan(Purpose(file, loan)), loan.remainingAmount, GameLoans.Due(loan));
                if (loan.dailyPayment > 0)
                {
                    int days = Mathf.CeilToInt(loan.remainingAmount / loan.dailyPayment);
                    row.Set(4, FinanceText.T("reallife_econ_row_days", "days", days.ToString()), days);
                }
            }
            float owedAccounts = OverviewAccounts(file, save.Day);
            _debts.Clear();
            EconomyBridges.OtherDebts?.Invoke(_debts);
            float owedOther = 0f;
            for (int i = 0; i < _debts.Count; i++)
            {
                DebtLine debt = _debts[i];
                owedOther += debt.Owed;
                TableRow row = Row("debt:" + i);
                DebtCells(row, FinanceText.T(debt.NameKey), FinanceTables.Other, debt.Owed, debt.PerDay > 0 ? Mathf.Min(debt.PerDay, debt.Owed) : 0f);
                if (debt.LateDays > 0)
                {
                    row.Set(4, FinanceText.T("reallife_econ_row_late", "days", debt.LateDays.ToString()), TableRow.Red);
                }
            }
            if (_rows.Count == 0)
            {
                Say("none", FinanceText.T("reallife_econ_row_no_debts"));
            }
            Books.Wealth(out PersonalWealthData wealth);

            box.Title(FinanceText.T("reallife_econ_tab_overview"));
            box.Chip(FinanceText.Grade(score.Grade), Tone(score.Grade));
            box.Section("reallife_ibox_sec_now");
            box.Row("reallife_econ_over_cash", FinanceText.Money(save.Money), save.Money < 0f ? CellTone.Red : CellTone.Plain);
            box.Row("reallife_econ_over_owed", FinanceText.Money(owedLoans + owedAccounts + owedOther));
            if (owedOther > 0f || owedAccounts > 0f)
            {
                box.Note(FinanceText.T("reallife_econ_over_owed_three", "loans", FinanceText.Money(owedLoans), "accounts", FinanceText.Money(owedAccounts), "other", FinanceText.Money(owedOther)));
            }
            box.Row("reallife_econ_over_rating", FinanceText.T("reallife_econ_rating_value", "score", score.Value.ToString(), "grade", FinanceText.Grade(score.Grade)), Tone(score.Grade));
            bool cityOn = CityLines(box);
            box.Headline("reallife_econ_over_costs");
            box.Row("reallife_econ_over_leaves", FinanceText.Money(due.Total));
            box.Row("reallife_econ_over_loan_payments", FinanceText.Money(due.Loans));
            if (due.LoanInterest > 0f)
            {
                box.Note(FinanceText.T("reallife_econ_over_interest", "amount", FinanceText.Money(due.LoanInterest)));
            }
            if (due.Accounts > 0f)
            {
                box.Row("reallife_econ_over_accounts", FinanceText.Money(due.Accounts));
            }
            if (due.Other > 0f)
            {
                box.Row("reallife_econ_over_fines", FinanceText.Money(due.Other));
            }
            box.Row("reallife_econ_over_rent", FinanceText.Money(due.Rent));
            box.Row("reallife_econ_over_wages", FinanceText.Money(due.Wages));
            box.Headline("reallife_econ_over_worth_head");
            box.Row("reallife_econ_over_worth", FinanceText.Money(Books.NetWorth()));
            box.Note(FinanceText.T("reallife_econ_over_worth_parts", "investments", FinanceText.Money(wealth.totalInvestments), "assets", FinanceText.Money(wealth.totalAssets)));
            Transform buttons = box.ButtonRow();
            if (cityOn && EconomyAppOn())
            {
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_econapp_finance_open", OpenEconomyApp);
            }
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_econ_open_econoview", OpenEconoView);
            if (StandingOn() != null)
            {
                InfoBox.Button(buttons, InfoBox.Additional, "reallife_fame_open", ShowStanding);
            }
            if (RivalsOn() != null)
            {
                InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_rival_open", ShowRivals);
            }
            Meta.Guide.Point(box, GuideTitle);
        }

        // A debt's first four cells: who it is owed to, its kind, what is owed and what it takes tomorrow.
        private static void DebtCells(TableRow row, string name, int kind, float owed, float daily)
        {
            row.Kind = kind;
            row.Set(0, name);
            row.Set(1, FinanceText.T(FinanceTables.DebtKinds[kind]));
            row.Set(2, FinanceText.Money(owed), owed);
            if (daily > 0f)
            {
                row.Set(3, FinanceText.Money(daily), daily);
            }
        }

        private static int Purpose(CreditFile file, Loan loan)
        {
            for (int i = 0; i < file.Loans.Count; i++)
            {
                if (ReferenceEquals(file.Loans[i].Ref, loan))
                {
                    return file.Loans[i].Purpose;
                }
            }
            return LoanPurpose.Plain;
        }

        // The mod's own accounts and the private lender in the table; returns what is owed on them.
        private float OverviewAccounts(CreditFile file, int today)
        {
            float owed = 0f;
            for (int i = 0; i < file.Accounts.Count; i++)
            {
                CreditAccount a = file.Accounts[i];
                if (!Books.Owing(a))
                {
                    continue;
                }
                owed += a.Owed;
                TableRow row = Row("account:" + a.Id);
                DebtCells(row, CreditDesk.Name(a), FinanceTables.Of(a.Kind), a.Owed, AccountRules.DueTomorrow(a, today));
                CellTone tone = a.Ladder.Misses > 0 ? TableRow.Red : TableRow.Plain;
                if (ProductCatalogue.Instalment(a.Kind) && a.Instalment > 0)
                {
                    int days = Mathf.CeilToInt(a.Owed / (float)a.Instalment);
                    row.Set(4, FinanceText.T("reallife_econ_row_days", "days", days.ToString()), days, tone);
                }
                else
                {
                    row.Set(4, FinanceText.State(a), tone);
                }
            }
            if (file.Shark.Active)
            {
                owed += file.Shark.Owed;
                TableRow row = Row("shark");
                DebtCells(row, FinanceText.T("reallife-lender"), FinanceTables.Private, file.Shark.Owed, 0f);
                row.Set(4, FinanceText.T("reallife_econ_row_due_day", "day", file.Shark.DueDay.ToString()), file.Shark.DueDay - today, today > file.Shark.DueDay ? TableRow.Red : TableRow.Plain);
            }
            return owed;
        }

        // Repaying a loan and changing its daily payment stay where the game has them: its own loans page.
        private void OpenEconoView()
        {
            if (_menu != null)
            {
                _menu.ShowApp(AppName.EconoView);
            }
        }
    }
}
