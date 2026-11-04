using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    public static partial class OwnBankBuild
    {
        // The rate offsets a bank may set over the rivals', hundredths of a point: the range and the step the director holds them to.
        public const int SaverLeast = -200, SaverMost = 400, SaverStep = 25, LoanLeast = -300, LoanMost = 900, LoanStep = 25;
        public const int FundLimitMost = 250, FundLimitStep = 50;

        private static string Whole(long value) => value.ToString("N0", Inv);

        // A rate offset on the steps the bank may take: its words (the rate it makes and its points over the rivals'), and the offsets a minus and a plus go to.
        private static UtilitiesStep OffsetStep(UtilitiesWords w, string label, int offset, int rival, int least, int most, int step, int floorBp, MoneyAct act, int id, out MoneyStep move)
        {
            int count = (most - least) / step + 1;
            var values = new int[count];
            for (int i = 0; i < count; i++)
            {
                values[i] = least + i * step;
            }
            int at = Math.Max(0, Math.Min(count - 1, (offset - least) / step));
            string words = w.F("reallife_ob_rate_option", "{rate} a year ({points} against the rival banks)", "rate", Rate(Math.Max(floorBp, rival + values[at])), "points", Points(w, values[at]));
            return BankParts.ChoiceStep(label, words, null, at, count, -1, act, id, values, out move);
        }

        // ------------------------------------------------------------------------------------------------- Savers

        // The kinds of saver the bank serves, the largest balance first, each with its rate against the rivals'; the one picked with the rate to set.
        private static void SaversPage(MoneyModel m, OwnBankFacts f, UtilitiesWords w, BankCards cards)
        {
            var order = new List<int>(f.Savers.Length);
            for (int i = 0; i < f.Savers.Length; i++)
            {
                order.Add(i);
            }
            order.Sort((a, b) => f.Savers[b].Deposits.CompareTo(f.Savers[a].Deposits));
            var rows = new List<UiDataRow>(4);
            var ids = new string[order.Count];
            int selected = -1;
            for (int r = 0; r < order.Count; r++)
            {
                int k = order[r];
                OwnSaverFact s = f.Savers[k];
                ids[r] = "s|" + k.ToString(Inv);
                selected = m.View.Row == ids[r] ? r : selected;
                rows.Add(BankParts.Row(new[]
                {
                    s.Name, C(w, s.Deposits), Share(s.Deposits, s.Pool), Rate(s.OwnBp), Rate(s.RivalBp), f.Run ? w.T("reallife_ob_state_running", "running on the bank") : w.T("reallife_ob_state_saving", "saving")
                }, BankParts.Tones(6, 3, s.OwnBp > s.RivalBp ? UiRole.OkText : s.OwnBp < s.RivalBp ? UiRole.WarnText : UiRole.None)));
            }
            m.Table = BankParts.Table(new[] { w.T("reallife_ob_col_savers", "Savers"), w.T("reallife_ob_col_deposits", "Deposits"), w.T("reallife_ob_col_share", "Of the city"),
                w.T("reallife_ob_col_rate", "Your rate"), w.T("reallife_ob_col_rivals", "Rival banks"), w.T("reallife_ob_col_state", "Now") },
                new[] { UiCellKind.Text, UiCellKind.Figure, UiCellKind.Figure, UiCellKind.Figure, UiCellKind.Figure, UiCellKind.Text }, rows, selected, w.T("reallife_ob_empty_savers", "No savers yet."), string.Empty, UiIcon.PiggyBank);
            m.RowIds = ids;
            m.Figures = new[]
            {
                new UiFigureModel { Label = w.T("reallife_ob_box_deposits", "Deposits:"), Value = C(w, f.Deposits) },
                new UiFigureModel { Label = w.T("reallife_ob_box_trust", "Trust:"), Value = Pct(f.Trust) },
                new UiFigureModel { Label = w.T("reallife_ob_col_share", "Of the city"), Value = Share(f.Deposits, f.CitySavings) }
            };
            if (selected < 0)
            {
                m.View.Row = null;
                var lines = new List<UtilitiesLine>(4);
                if (f.Run)
                {
                    lines.Add(BankParts.Say(w.T("reallife_ob_box_run", "A run on the bank: no lending until the savers stop."), UiRole.DangerText));
                }
                lines.Add(BankParts.Pair(w.T("reallife_ob_box_deposits", "Deposits:"), C(w, f.Deposits)));
                lines.Add(BankParts.Pair(w.T("reallife_ob_box_trust", "Trust:"), w.F("reallife_ob_box_trust_value", "{trust} (city's confidence {confidence})", "trust", Pct(f.Trust), "confidence", Pct(f.Confidence))));
                lines.Add(BankParts.Say(w.T("reallife_ob_box_savers_pick", "Pick a kind of saver to set its rate.")));
                lines.Add(BankParts.Say(w.T("reallife_ob_box_savers_intro", "Savers move their money over months, by your rate against the rivals' and by the city's trust. A new bank earns its name over months.")));
                cards.Add(new UtilitiesCard { Title = w.T("reallife_ob_tab_savers", "Savers"), Sub = f.Name, Lines = lines.ToArray() });
                return;
            }
            int kind = int.Parse(m.View.Row.Substring(2), Inv);
            OwnSaverFact pick = f.Savers[Math.Max(0, Math.Min(f.Savers.Length - 1, kind))];
            var steps = new[] { OffsetStep(w, w.T("reallife_ob_box_rate_head", "Your rate"), pick.Offset, pick.RivalBp, SaverLeast, SaverMost, SaverStep, 0, MoneyAct.SaverRate, kind, out MoneyStep move) };
            cards.Add(new UtilitiesCard
            {
                Title = pick.Name, Sub = f.Name,
                Lines = new[]
                {
                    BankParts.Pair(w.T("reallife_ob_box_deposits", "Deposits:"), C(w, pick.Deposits)), BankParts.Pair(w.T("reallife_ob_box_wanted", "Where it is heading:"), C(w, pick.Wanted)),
                    BankParts.Pair(w.T("reallife_ob_box_rival_rate", "The rival banks' rate:"), Rate(pick.RivalBp)),
                    BankParts.Say(BankParts.Either(w, kind == 1, "reallife_ob_box_firms_note", "Businesses chase a rate, and leave first in a run.", "reallife_ob_box_people_note", "People trust a name more than a rate: your integrity as an owner counts for much."))
                },
                Steps = steps
            }, new[] { move });
        }

        // ------------------------------------------------------------------------------------------------- Lending

        private static void LendingPage(MoneyModel m, OwnBankFacts f, UtilitiesWords w, BankCards cards)
        {
            var order = new List<int>(f.Segments.Length);
            for (int i = 0; i < f.Segments.Length; i++)
            {
                order.Add(i);
            }
            order.Sort((a, b) => f.Segments[b].Lent.CompareTo(f.Segments[a].Lent));
            var rows = new List<UiDataRow>(5);
            var ids = new string[order.Count];
            int selected = -1;
            for (int r = 0; r < order.Count; r++)
            {
                int j = order[r];
                OwnSegmentFact s = f.Segments[j];
                ids[r] = "l|" + j.ToString(Inv);
                selected = m.View.Row == ids[r] ? r : selected;
                rows.Add(BankParts.Row(new[]
                {
                    s.Name, C(w, s.Lent), C(w, s.Arrears), Rate(s.OwnBp), Pct(s.DefaultPerMille), s.Lends ? w.T("reallife_ob_lends_yes", "yes") : w.T("reallife_ob_lends_no", "no")
                }, BankParts.Tones(6, 5, s.Lends ? UiRole.Ok : UiRole.Warn)));
            }
            m.Table = BankParts.Table(new[] { w.T("reallife_ob_col_borrowers", "Borrowers"), w.T("reallife_ob_col_lent", "Lent"), w.T("reallife_ob_col_arrears", "In arrears"), w.T("reallife_ob_col_rate", "Your rate"),
                w.T("reallife_ob_col_defaults", "Defaults a year"), w.T("reallife_ob_col_lends", "Lending") },
                new[] { UiCellKind.Text, UiCellKind.Figure, UiCellKind.Figure, UiCellKind.Figure, UiCellKind.Figure, UiCellKind.Chip }, rows, selected,
                w.T("reallife_ob_empty_lending", "Nothing lent yet."), string.Empty, UiIcon.Coin);
            m.RowIds = ids;
            m.Figures = new[]
            {
                new UiFigureModel { Label = w.T("reallife_ob_box_lent", "Lent:"), Value = C(w, f.Lent) },
                new UiFigureModel { Label = w.T("reallife_ob_box_arrears", "In arrears:"), Value = C(w, f.Arrears) },
                new UiFigureModel { Label = w.T("reallife_ui_ob_written", "Written off"), Value = C(w, f.LostAll) }
            };
            if (selected < 0)
            {
                m.View.Row = null;
                bool stopped = f.Run || f.Stage == 2;
                var lines = new List<UtilitiesLine>(5);
                if (stopped)
                {
                    lines.Add(BankParts.Say(w.T("reallife_ob_box_no_lending", "No new loans during a run or while the regulator finds the bank in breach."), UiRole.DangerText));
                }
                lines.Add(BankParts.Pair(w.T("reallife_ob_box_lent", "Lent:"), C(w, f.Lent)));
                lines.Add(BankParts.Pair(w.T("reallife_ob_box_arrears", "In arrears:"), C(w, f.Arrears), f.Arrears > 0 ? UiRole.WarnText : UiRole.Text));
                lines.Add(BankParts.Pair(w.T("reallife_ob_box_written", "Written off:"), w.F("reallife_ob_box_written_value", "{month} last month, {all} in all, {back} come back", "month", C(w, f.MonthLost),
                    "all", C(w, f.LostAll), "back", C(w, f.RecoveredAll))));
                if (!stopped)
                {
                    lines.Add(BankParts.Say(w.T("reallife_ob_box_lending_pick", "Pick a kind of borrower to set its rate or stop lending to it.")));
                }
                lines.Add(BankParts.Say(BankParts.Either(w, f.Courts, "reallife_ob_box_courts_on", "Loans written off go to the courts: more comes back, later, less the courts' costs.", "reallife_ob_box_courts_off", "Justice system off: less of a loan written off comes back.")));
                cards.Add(new UtilitiesCard { Title = w.T("reallife_ob_tab_lending", "Lending"), Sub = f.Name, Lines = lines.ToArray() });
                return;
            }
            int seg = int.Parse(m.View.Row.Substring(2), Inv);
            OwnSegmentFact pick = f.Segments[Math.Max(0, Math.Min(f.Segments.Length - 1, seg))];
            var steps = new[] { OffsetStep(w, w.T("reallife_ob_box_rate_head", "Your rate"), pick.Offset, pick.RivalBp, LoanLeast, LoanMost, LoanStep, 0, MoneyAct.LoanRate, seg, out MoneyStep move) };
            cards.Add(new UtilitiesCard
            {
                Title = pick.Name, Sub = f.Name, Chip = pick.Lends ? w.T("reallife_ibox_chip_lending", "Lending") : w.T("reallife_ibox_chip_stopped", "Stopped"), ChipTone = pick.Lends ? UiRole.Ok : UiRole.Warn,
                Lines = new[]
                {
                    BankParts.Pair(w.T("reallife_ob_box_lent", "Lent:"), C(w, pick.Lent)),
                    BankParts.Pair(w.T("reallife_ob_box_arrears", "In arrears:"), w.F("reallife_ob_box_arrears_value", "{arrears}, and {back} written off still to come back", "arrears", C(w, pick.Arrears), "back", C(w, pick.Recovering))),
                    BankParts.Pair(w.T("reallife_ob_box_defaults", "Defaults a year:"), w.F("reallife_ob_box_defaults_value", "{rate} now ({usual} in a steady city)", "rate", Pct(pick.DefaultPerMille), "usual", Pct(pick.DefaultUsual))),
                    BankParts.Pair(w.T("reallife_ob_box_wanted", "Where it is heading:"), C(w, pick.Wanted))
                },
                Steps = steps, SwitchLabel = w.T("reallife_ob_box_lend_switch", "Lend to them"), SwitchOn = pick.Lends
            }, new[] { move }, MoneyAct.Lending);
        }

        // ------------------------------------------------------------------------------------------------- Fund

        // The fund over its days (the days that exist), its holdings as a donut and a table, and the strategy and limit that steer it.
        private static void FundPage(MoneyModel m, OwnBankFacts f, UtilitiesWords w, BankCards cards)
        {
            string title = w.T("reallife_money_fund_days", "Fund over 30 days");
            int n = Math.Min(f.FundSeries.Length, f.FundDays.Length);
            m.Chart = FinanceRules.Chart(title, new[] { title }, new[] { f.FundSeries }, f.FundDays, n, v => w.Money(v), w.T("reallife_fin_chart_empty", "Not enough days yet"),
                w.T("reallife_fin_chart_empty_body", "The chart fills in as days pass."));
            long shares = Math.Max(0L, f.Fund);   // the shares the fund holds, beside the cash reserve the bank keeps
            m.DonutTitle = w.T("reallife_ui_ob_holds", "What the bank holds");
            m.Donut = f.Fund + f.Reserves <= 0 ? new float[0] : new[] { (float)Math.Max(0L, f.Reserves), (float)shares };
            m.DonutLabels = new[] { w.T("reallife_ui_ob_reserve", "Cash reserve"), w.T("reallife_ui_ob_shares", "Shares") };
            m.DonutValues = new[] { C(w, f.Reserves), C(w, shares) };
            var rows = new List<UiDataRow>(f.Holdings.Length);
            for (int i = 0; i < f.Holdings.Length; i++)
            {
                OwnHoldingFact h = f.Holdings[i];
                long gain = h.Value - h.Cost;
                rows.Add(BankParts.Row(new[] { h.Name, Whole(h.Qty), C(w, h.Cost), C(w, h.Value), Signed(w, gain), C(w, h.Year) }, BankParts.Tones(6, 4, gain < 0 ? UiRole.DangerText : gain > 0 ? UiRole.OkText : UiRole.None)));
            }
            m.Table = BankParts.Table(new[] { w.T("reallife_ob_col_company", "Company"), w.T("reallife_ob_col_shares", "Shares"), w.T("reallife_ob_col_cost", "Cost"), w.T("reallife_ob_col_value", "Value"),
                w.T("reallife_ob_col_gain", "Gain"), w.T("reallife_ob_col_dividends", "Dividends a year") }, null, rows, -1, w.T("reallife_ui_ob_fund_empty", "The fund holds no shares"), string.Empty, UiIcon.ChartPie);
            m.RowIds = new string[rows.Count];
            int sCount = Math.Max(1, f.StrategyNames.Length);
            var strategies = new int[sCount];
            for (int i = 0; i < sCount; i++)
            {
                strategies[i] = i;
            }
            var steps = new List<UtilitiesStep>(2);
            var moves = new List<MoneyStep>(2);
            steps.Add(BankParts.ChoiceStep(w.T("reallife_ui_ob_strategy", "Strategy"), f.Strategy >= 0 && f.Strategy < f.StrategyNames.Length ? f.StrategyNames[f.Strategy] : string.Empty, null,
                Math.Max(0, Math.Min(sCount - 1, f.Strategy)), sCount, -1, MoneyAct.Strategy, 0, strategies, out MoneyStep strategy));
            moves.Add(strategy);
            int limits = FundLimitMost / FundLimitStep + 1;
            var limitValues = new int[limits];
            for (int i = 0; i < limits; i++)
            {
                limitValues[i] = i * FundLimitStep;
            }
            steps.Add(BankParts.ChoiceStep(w.T("reallife_ui_ob_limit", "Most of the deposits it may hold"), w.F("reallife_ob_fund_limit_option", "At most {share} of the deposits", "share", Pct(f.FundLimit)), null,
                Math.Max(0, Math.Min(limits - 1, f.FundLimit / FundLimitStep)), limits, -1, MoneyAct.FundLimit, 0, limitValues, out MoneyStep limit));
            moves.Add(limit);
            cards.Add(new UtilitiesCard
            {
                Title = w.T("reallife_ob_tab_fund", "Fund"), Sub = f.Name,
                Lines = new[]
                {
                    BankParts.Pair(w.T("reallife_ob_box_fund_value", "The fund:"), w.F("reallife_ob_box_fund_value_value", "{value} ({gain} on its cost)", "value", C(w, f.Fund), "gain", Signed(w, f.Fund - f.FundCost))),
                    BankParts.Pair(w.T("reallife_ob_box_fund_room", "It may hold now:"), C(w, f.FundRoom)),
                    BankParts.Pair(w.T("reallife_ob_box_fund_dividends", "Dividends a year:"), C(w, f.FundDividends)),
                    BankParts.Say(w.F("reallife_ob_box_fund_rules", "Holds the {companies} largest companies its strategy allows, in even parts, rebalanced each week. The regulator counts a share at {weight} of its value.", "companies", f.StrategyCompanies.ToString(Inv), "weight", Pct(250)))
                },
                Steps = steps.ToArray()
            }, moves.ToArray());
        }

        // ------------------------------------------------------------------------------------------------- Branches

        private static void BranchesPage(MoneyModel m, OwnBankFacts f, UtilitiesWords w, BankCards cards)
        {
            var rows = new List<UiDataRow>(f.Branches.Length);
            var ids = new string[f.Branches.Length];
            int selected = -1;
            for (int i = 0; i < f.Branches.Length; i++)
            {
                OwnBranchFact b = f.Branches[i];
                ids[i] = "r|" + b.Id;
                selected = m.View.Row == ids[i] ? i : selected;
                rows.Add(BankParts.Row(new[] { b.Name, Whole(b.Visitors), Whole(b.Served), C(w, b.CostDay) },
                    BankParts.Tones(4, 2, b.Lost * 2 > b.Visitors && b.Lost > 0 ? UiRole.WarnText : UiRole.None)));
            }
            m.Table = BankParts.Table(new[] { w.T("reallife_branch_col_branch", "Branch"), w.T("reallife_branch_col_visitors", "Customers"), w.T("reallife_branch_col_served", "Seen to"),
                w.T("reallife_ui_ob_costs_day", "Costs a day") },
                new[] { UiCellKind.Text, UiCellKind.Figure, UiCellKind.Figure, UiCellKind.Figure },
                rows, selected, w.T("reallife_branch_empty", "No branches yet."), w.T("reallife_branch_box_none", "Open a branch: rent a shop unit, start a business there and pick Bank Branch. Hire tellers with customer service as their main skill and give each a till."), UiIcon.Building);
            m.RowIds = ids;
            m.Figures = new[]
            {
                new UiFigureModel { Label = w.T("reallife_branch_box_count", "Branches"), Value = f.Branches.Length.ToString(Inv) },
                new UiFigureModel { Label = w.T("reallife_ui_ob_f_week", "Seen this week"), Value = Whole(f.WeekServed) },
                new UiFigureModel { Label = w.T("reallife_branch_col_fees", "Fees"), Value = C(w, f.BranchFees) },
                new UiFigureModel { Label = w.T("reallife_ui_ob_costs_day", "Costs a day"), Value = C(w, f.BranchCosts) }
            };
            if (selected < 0)
            {
                m.View.Row = null;
                var lines = new List<UtilitiesLine>(6);
                if (!f.BranchOn)
                {
                    lines.Add(BankParts.Say(w.T("reallife_branch_box_off", "Bank branches are switched off in the options. Your bank holds what it would without them."), UiRole.WarnText));
                }
                lines.Add(BankParts.Pair(w.T("reallife_branch_box_reach", "Reach"), w.F("reallife_branch_box_reach_value", "{reach} more of the city's people, {firms} more of its businesses", "reach", Pct(f.Reach), "firms", Pct(f.ReachFirms)),
                    f.Reach > 0 ? UiRole.OkText : UiRole.Text));
                if (f.BranchBrings)
                {
                    lines.Add(BankParts.Pair(w.T("reallife_branch_box_brings", "What it brings"), w.F("reallife_branch_box_brings_value", "{deposits} more in deposits, {loans} more in loans", "deposits", C(w, f.BranchDeposits),
                        "loans", C(w, f.BranchLoans))));
                    lines.Add(BankParts.Pair(w.T("reallife_branch_box_margin", "Its margin a day"), Signed(w, f.BranchMargin), f.BranchMargin >= f.BranchCosts ? UiRole.OkText : UiRole.WarnText));
                }
                lines.Add(BankParts.Say(BankParts.Either(w, f.Branches.Length == 0, "reallife_branch_box_none", "Open a branch: rent a shop unit, start a business there and pick Bank Branch. Hire tellers with customer service as their main skill and give each a till.", "reallife_branch_box_pick", "Pick a branch to see its month.")));
                lines.Add(BankParts.Say(w.T("reallife_branch_box_books", "A branch's own statement in the game shows only its costs. Its fees and what it brings are your bank's income, booked at your headquarters.")));
                cards.Add(new UtilitiesCard { Title = w.T("reallife_branch_tab", "Branches"), Sub = f.Name, Lines = lines.ToArray() });
                return;
            }
            OwnBranchFact br = f.Branches[selected];
            var detail = new List<UtilitiesLine>(10)
            {
                BankParts.Pair(w.T("reallife_branch_box_tellers", "Tellers"), w.F("reallife_branch_box_tellers_value", "{now} at the counter now of {all}; each sees to about {each} customers an hour", "now", br.Tellers.ToString(Inv), "all", br.StaffAll.ToString(Inv),
                    "each", br.ServePerTeller.ToString(Inv)), br.Open && br.Tellers == 0 ? UiRole.WarnText : UiRole.Text),
                BankParts.Pair(w.T("reallife_branch_box_week", "Customers seen to in the last week"), Whole(br.Week)),
                BankParts.Pair(w.T("reallife_branch_box_costs", "Rent and wages a day"), C(w, br.CostDay)),
                BankParts.Pair(w.T("reallife_branch_box_month", "This month"), w.F("reallife_branch_box_visitors_value", "{visitors} came, {served} seen to, {lost} left unseen", "visitors", Whole(br.Visitors), "served", Whole(br.Served),
                    "lost", Whole(br.Lost))),
                BankParts.Pair(w.T("reallife_branch_box_money", "Cash"), w.F("reallife_branch_box_money_value", "{in} paid in, {out} taken out, {opened} accounts opened", "in", C(w, br.PaidIn), "out", C(w, br.TakenOut), "opened", Whole(br.Opened))),
                BankParts.Pair(w.T("reallife_branch_box_lending", "Loans"), w.F("reallife_branch_box_lending_value", "{loans} arranged for {lent}, {refused} turned down", "loans", Whole(br.Loans), "lent", C(w, br.Lent), "refused", Whole(br.Refused))),
                BankParts.Pair(w.T("reallife_branch_box_fees", "Fees at the counter"), C(w, br.Fees))
            };
            if (br.LastVisitors > 0)
            {
                detail.Add(BankParts.Pair(w.T("reallife_branch_box_last", "Last month"), w.F("reallife_branch_box_visitors_value", "{visitors} came, {served} seen to, {lost} left unseen", "visitors", Whole(br.LastVisitors),
                    "served", Whole(br.LastServed), "lost", Whole(br.LastLost))));
            }
            detail.Add(BankParts.Say(w.T("reallife_branch_box_counter_note", "You can work the counter yourself: click a free till in the branch.")));
            cards.Add(new UtilitiesCard
            {
                Title = br.Name, Sub = f.Name, Chip = br.Open ? w.T("reallife_branch_chip_open", "Open") : w.T("reallife_branch_chip_closed", "Closed"), ChipTone = br.Open ? UiRole.Ok : UiRole.None, Lines = detail.ToArray()
            });
        }
    }
}
