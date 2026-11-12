using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using RealLifeOverhaul.DaySeries;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Rivals;

namespace RealLifeOverhaul.Ui
{
    public sealed partial class FinanceLive
    {
        private const int StatementLinesMost = 240;

        // ------------------------------------------------------------------------------------------------- Cash flow

        private void ReadCash(FinanceFacts f, FinanceView v)
        {
            FinanceCashFacts c = f.Cash;
            GameInstance save = SaveGameManager.Current;
            UtilitiesWords w = W;
            c.Periods = new string[CashFlow.PeriodDays.Length];
            for (int i = 0; i < c.Periods.Length; i++)
            {
                c.Periods[i] = CashFlow.PeriodName(i);
            }
            v.Period = Math.Max(0, Math.Min(c.Periods.Length - 1, v.Period));
            var flow = new CashFlow();
            flow.Read(v.Period, save.Day, null);
            c.ExportLabel = FinanceText.T("reallife_money_export");
            c.Statement = Statement(flow, c.Periods[v.Period], save, w);
            c.Reach = flow.OldestDay > flow.FromDay && flow.FromDay >= 1 ? FinanceText.T("reallife_econ_cash_reach", "day", flow.OldestDay.ToString(Inv)) : null;
            // The cash over the days the save has.
            c.Chart = CashChart(w, DaySeriesRules.CashWindow);
            int shown = c.Chart.Points.Length;
            c.ChartTitle = w.T("reallife_fin_cash_chart", "Cash");
            c.ChartTotal = Money(save.Money);
            c.ChartChange = null;
            c.ChartRange = null;
            DaySeriesBook book = DaySeriesDirector.Book;
            int day = RivalGame.Day;
            int n = book != null ? FinanceRules.ExistingDays(book, DaySeriesCatalogue.Cash, day, DaySeriesRules.CashWindow, false, _values, _days) : 0;
            if (n >= 2)
            {
                float change = _values[n - 1] - _values[0];
                c.ChangeGood = change >= 0f;
                c.ChartChange = w.F("reallife_fin_cash_change", "{sign}{money} in {days} days", "sign", change >= 0f ? "+" : "-", "money",
                    Money(Math.Abs(change)), "days", (_days[n - 1] - _days[0]).ToString(Inv));
                c.ChartRange = w.F("reallife_fin_day_range", "Day {from} to {to}", "from", _days[0].ToString(Inv), "to", _days[n - 1].ToString(Inv));
            }
            c.ChartTitle = shown > 0 ? c.ChartTitle : w.T("reallife_fin_cash_chart", "Cash");
            // Where it went, 30 days: the saved ledger, a day at a time, so it reaches a month the game's own movements do not.
            Where(c, w, day);
            Coming(c, w, save);
        }

        private static UiStatementModel Statement(CashFlow flow, string period, GameInstance save, UtilitiesWords w)
        {
            var model = new UiStatementModel
            {
                Account = FinanceText.T("reallife_econ_tab_cash"), AccountIcon = UiIcon.Wallet, BalanceLabel = FinanceText.T("reallife_money_cash"),
                    Balance = Money(save.Money), PeriodLabel = period,
                Columns = new[] { FinanceText.T("reallife_money_col_when"), FinanceText.T("reallife_money_col_what"),
                    FinanceText.T("reallife_money_col_in"), FinanceText.T("reallife_money_col_out"),
                    FinanceText.T("reallife_money_col_balance") },
                EmptyTitle = FinanceText.T("reallife_money_cash_empty"), EmptyBody = FinanceText.T("reallife_econ_row_no_cash")
            };
            if (flow.Entries.Count == 0)
            {
                return model;
            }
            var groups = new List<UiStatementGroup>(8);
            var lines = new List<UiStatementLine>(32);
            float balance = save.Money, groupNet = 0f;
            int groupDay = -1, taken = 0;
            for (int i = flow.Entries.Count - 1; i >= 0 && taken < StatementLinesMost; i--, taken++)
            {
                CashEntry e = flow.Entries[i];
                if (groupDay >= 0 && e.Day != groupDay)
                {
                    groups.Add(Group(groupDay, save.Day, groupNet, lines));
                    lines.Clear();
                    groupNet = 0f;
                }
                groupDay = e.Day;
                groupNet += e.Amount;
                lines.Add(new UiStatementLine
                {
                    When = e.Hour.ToString("00", Inv) + ":00", Icon = e.Amount >= 0f ? UiIcon.Coin : UiIcon.Receipt, What = CashFlow.ItemName(e.Type,
                        e.Data), Where = FinanceText.T(CashCategory.Keys[e.Category]),
                    In = e.Amount > 0f ? Money(e.Amount) : null, Out = e.Amount < 0f ? Money(-e.Amount) : null, Balance = Money(balance)
                });
                balance -= e.Amount;
            }
            if (lines.Count > 0)
            {
                groups.Add(Group(groupDay, save.Day, groupNet, lines));
            }
            model.Groups = groups.ToArray();
            model.TotalLabels = new[] { FinanceText.T("reallife_money_col_in"), FinanceText.T("reallife_money_col_out") };
            model.Totals = new[] { Money(flow.In), Money(-flow.Out) };
            model.ChangeGood = flow.In + flow.Out >= 0f;
            return model;
        }

        private static UiStatementGroup Group(int day, int today, float net, List<UiStatementLine> lines)
        {
            string label = day == today ? W.F("reallife_fin_day_today", "Day {day}, today", "day", day.ToString(Inv)) : FinanceText.Day(day);
            return new UiStatementGroup { Label = label, Total = (net >= 0f ? "+" : "-") + Money(Math.Abs(net)), TotalGood = net >= 0f, Lines = lines.ToArray() };
        }

        // Where it went in the last 30 days, from the saved category series (written each day for the day before): the kinds where more came in
        // than went out, and those where more went out, the biggest five of each with a bar. A day before the book began is not a day and not a
        // zero: the note says how many of the 30 the book holds.
        private void Where(FinanceCashFacts c, UtilitiesWords w, int day)
        {
            DaySeriesBook book = DaySeriesDirector.Book;
            var net = new long[CashCategory.Count];
            int have = FinanceRules.CategoryNet(book, day - 1, DaySeriesRules.CashWindow, net, out int first);   // the ledger holds yesterday as its newest day
            c.WhereTitle = w.F("reallife_fin_where", "Where it went, {days} days", "days", DaySeriesRules.CashWindow.ToString(Inv));
            c.WhereNote = have >= DaySeriesRules.CashWindow ? null : have == 0
                ? w.T("reallife_fin_where_none", "Not yet: the ledger fills in from tomorrow, a day at a time.")
                : w.F("reallife_fin_where_part", "Not yet before day {first}: the ledger holds {have} of {days} days.", "first", first.ToString(Inv), "have", have.ToString(Inv),
                    "days", DaySeriesRules.CashWindow.ToString(Inv));
            Kinds(net, true, out c.InNames, out c.InValues, out c.InShare);
            Kinds(net, false, out c.OutNames, out c.OutValues, out c.OutShare);
            long came = 0, went = 0;
            for (int i = 0; i < net.Length; i++)
            {
                came += net[i] > 0 ? net[i] : 0;
                went += net[i] < 0 ? -net[i] : 0;
            }
            c.CameInLabel = w.T("reallife_fin_where_in", "More came in than went out");
            c.CameIn = "+" + Money(came);
            c.WentOutLabel = w.T("reallife_fin_where_out", "More went out than came in");
            c.WentOut = "-" + Money(went);
            c.NetLabel = w.T("reallife_fin_cash_changed", "Cash changed by");
            c.Net = (came >= went ? "+" : "-") + Money(Math.Abs(came - went));
            c.NetGood = came >= went;
        }

        // The biggest five kinds of money in or out, each with its share of the biggest, for a bar.
        private static void Kinds(long[] net, bool money, out string[] names, out string[] values, out int[] shares)
        {
            var order = new List<int>(CashCategory.Count);
            for (int i = 0; i < net.Length; i++)
            {
                if ((money ? net[i] : -net[i]) > 0L)
                {
                    order.Add(i);
                }
            }
            order.Sort((a, b) => (money ? net[b] : -net[b]).CompareTo(money ? net[a] : -net[a]));
            int n = Math.Min(5, order.Count);
            names = new string[n];
            values = new string[n];
            shares = new int[n];
            float top = n > 0 ? Math.Abs((float)net[order[0]]) : 0f;
            for (int i = 0; i < n; i++)
            {
                float amount = Math.Abs((float)net[order[i]]);
                names[i] = FinanceText.T(CashCategory.Keys[order[i]]);
                values[i] = Money(amount);
                shares[i] = FinanceRules.Share(amount, top);
            }
        }

        // Coming up: what leaves tomorrow, and the next payment of each account with its day.
        private static void Coming(FinanceCashFacts c, UtilitiesWords w, GameInstance save)
        {
            c.ComingTitle = w.T("reallife_fin_coming", "Coming up");
            var labels = new List<string>(6);
            var values = new List<string>(6);
            CreditFile file = CreditLedger.File;
            Obligations due = Books.Tomorrow(file);
            string tomorrow = W.T("reallife_fin_tomorrow", "Tomorrow");
            if (due.Loans > 0f)
            {
                labels.Add(tomorrow + ", " + Label("reallife_econ_over_loan_payments").ToLowerInvariant());
                values.Add(Money(due.Loans));
            }
            if (due.Rent > 0f)
            {
                labels.Add(tomorrow + ", " + Label("reallife_econ_over_rent").ToLowerInvariant());
                values.Add(Money(due.Rent));
            }
            for (int i = 0; i < file.Accounts.Count && labels.Count < 6; i++)
            {
                CreditAccount a = file.Accounts[i];
                FinanceApp.NextPayment(a, save.Day, out int amount, out int day);
                if (amount > 0 && day > save.Day + 1)
                {
                    labels.Add(FinanceText.Day(day) + ", " + CreditDesk.Name(a));
                    values.Add(Money(amount));
                }
            }
            c.ComingLabels = labels.ToArray();
            c.ComingValues = values.ToArray();
        }

        /// <summary>Writes the statement shown to a file in the mod's folder: when, what, in, out and the balance.</summary>
        private bool Export(FinanceView v)
        {
            GameInstance save = SaveGameManager.Current;
            var flow = new CashFlow();
            flow.Read(Math.Max(0, Math.Min(CashFlow.PeriodDays.Length - 1, v.Period)), save.Day, null);
            if (flow.Entries.Count == 0)
            {
                return false;
            }
            var text = new StringBuilder(flow.Entries.Count * 48);
            text.Append("day,hour,what,in,out,balance\n");
            float balance = save.Money;
            var lines = new string[flow.Entries.Count];
            for (int i = flow.Entries.Count - 1; i >= 0; i--)
            {
                CashEntry e = flow.Entries[i];
                lines[i] = e.Day.ToString(Inv) + "," + e.Hour.ToString(Inv) + "," + Csv(CashFlow.ItemName(e.Type,
                    e.Data)) + "," + (e.Amount > 0f ? e.Amount.ToString("0.##", Inv) : string.Empty) + ","
                    + (e.Amount < 0f ? (-e.Amount).ToString("0.##", Inv) : string.Empty) + "," + balance.ToString("0.##", Inv);
                balance -= e.Amount;
            }
            for (int i = 0; i < lines.Length; i++)
            {
                text.Append(lines[i]).Append('\n');
            }
            File.WriteAllText(Path.Combine(RealLifeOverhaul.Meta.Presets.Folder, "statement.csv"), text.ToString(), new UTF8Encoding(false));
            Tell(UI.Notification.NotificationType.Success, "reallife_fin_note_exported");
            return true;
        }

        private static string Csv(string text) => string.IsNullOrEmpty(text) ? string.Empty : text.Replace(',', ' ').Replace('\n', ' ');
    }
}
