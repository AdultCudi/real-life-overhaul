using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>The Cash flow tab: every movement of money over yesterday, 7 days or 30, with what it was for.</summary>
    public sealed partial class FinanceApp
    {
        private int _period = CashFlow.Week;

        // The period's figures in the box. With 'table' the period is read again and its movements go in the table (CashTable,
        // written as it scrolls); without it only the box is written again, from what was read.
        private void WriteCashFlow(InfoBox box, bool table)
        {
            if (table)
            {
                _flow.Read(_period, SaveGameManager.Current.Day, null);
            }
            _spec = FinanceTables.Cash;
            box.Title(FinanceText.T("reallife_econ_tab_cash"));
            if (!box.HasSelector || !box.Selector(CashFlow.PeriodDays.Length, _period, CashFlow.PeriodName, i =>
                {
                    _period = i;
                    Refresh();
                }))
            {
                box.Line(CashFlow.PeriodName(_period));
            }
            box.Section("reallife_ibox_sec_totals");
            box.Row("reallife_econ_cash_in", FinanceText.Money(_flow.In), CellTone.Green);
            box.Row("reallife_econ_cash_out", FinanceText.Money(-_flow.Out), CellTone.Red);
            float net = _flow.In + _flow.Out;
            box.Row("reallife_econ_cash_net", FinanceText.Money(net), net >= 0f ? CellTone.Green : CellTone.Red);
            if (_flow.OldestDay > _flow.FromDay && _flow.FromDay >= 1)
            {
                box.Note(FinanceText.T("reallife_econ_cash_reach", "day", _flow.OldestDay.ToString()));
            }
            CategoryLines(box);
            if (_flow.TopCount > 0)
            {
                box.Headline("reallife_econ_cash_largest");
                for (int i = 0; i < _flow.TopCount; i++)
                {
                    CashEntry item = _flow.Top[i];
                    box.RowWords(CashFlow.ItemName(item.Type, item.Data), FinanceText.Money(item.Amount), item.Amount >= 0f ? CellTone.Green : CellTone.Red);
                }
            }
            if (table)
            {
                _cashRows = _flow.Entries.Count > 0;
                if (!_cashRows)
                {
                    Say("none", FinanceText.T("reallife_econ_row_no_cash"));
                }
            }
            Meta.Guide.Point(box, GuideTitle);
        }

        // The category chosen in the table's filter: what it came to in the period, and its share of all in and out.
        private void CategoryLines(InfoBox box)
        {
            int category = FinanceTables.Cash.Filters[0].Chosen;
            if (category < 0 || category >= CashCategory.Count)
            {
                return;
            }
            float came = _flow.CategoryIn[category], went = -_flow.CategoryOut[category];
            if (came <= 0f && went <= 0f)
            {
                return;   // none in this period: the table goes back to all of them
            }
            box.Headline(CashCategory.Keys[category]);
            if (came > 0f)
            {
                box.Line(FinanceText.T("reallife_tab_cash_share_in", "amount", FinanceText.Money(came), "share", Share(came, _flow.In)));
            }
            if (went > 0f)
            {
                box.Line(FinanceText.T("reallife_tab_cash_share_out", "amount", FinanceText.Money(went), "share", Share(went, -_flow.Out)));
            }
            box.Row("reallife_econ_cash_net", FinanceText.Money(came - went), came >= went ? CellTone.Green : CellTone.Red);
        }

        private static string Share(float part, float whole)
        {
            return whole > 0f ? Mathf.RoundToInt(part / whole * 100f) + "%" : "0%";
        }
    }
}
