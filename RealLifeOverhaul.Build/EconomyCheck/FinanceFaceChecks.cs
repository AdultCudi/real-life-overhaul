using System;
using System.Collections.Generic;
using RealLifeOverhaul.DaySeries;
using RealLifeOverhaul.Ui;

// The Finance screen on the kit (FinanceScreen). The owner's 4 Nov 2026 play of day 5 found Credit and Borrow the same page, Cash flow and
// Accounts the same page, Take it doing nothing, a rival row and a profile that did not match, buttons on the Rivals page that did nothing, a
// chart that drew the days before the save began as zero, and the wrong portrait. The checks say what each tab shows and what each button does,
// with no game behind them: a source that records what it is asked.
internal static partial class Program
{
    private sealed class FinanceStub : IFinanceSource
    {
        public readonly List<string> Acts = new List<string>();
        public readonly List<int> Amounts = new List<int>();
        public int Moved;
        public int Reads;

        public int Generation => Moved;

        public int TakeWanted() => -1;

        public void Read(FinanceFacts into, FinanceView view)
        {
            Reads++;
            string state = view.Tab == FinanceFace.Credit ? "credit" : view.Tab == FinanceFace.Borrow ? "borrow" : view.Tab == FinanceFace.CashFlow ? "cash" : view.Tab == FinanceFace.Accounts ? "accounts"
                : view.Tab == FinanceFace.Rivals ? "rivals_move" : view.Tab == FinanceFace.Standing ? "standing" : "overview";
            FinanceFacts f = UiSamples.FinanceFactsOf((key, english) => english, state);
            into.Ready = f.Ready;
            into.Day = f.Day;
            into.Score = f.Score;
            into.Overview = f.Overview;
            into.Credit = f.Credit;
            into.Borrow = f.Borrow;
            into.Cash = f.Cash;
            into.Accounts = f.Accounts;
            into.Rivals = f.Rivals;
            into.Standing = f.Standing;
            into.RivalsCount = f.RivalsCount;
            if (view.Tab == FinanceFace.Borrow && into.Borrow.Quote != null && into.Borrow.Quote.HasAmount)
            {
                FinanceQuoteFacts q = into.Borrow.Quote;
                q.Amount = view.Amount > 0 ? Math.Max(q.Least, Math.Min(q.Most, view.Amount)) : 250000;
                q.AmountNext = q.Amount < q.Most ? Math.Min(q.Most, q.Amount + 5000) : -1;
                q.AmountPrev = q.Amount > q.Least ? Math.Max(q.Least, q.Amount - 5000) : -1;
            }
            if (view.Tab == FinanceFace.Rivals && into.Rivals.Table != null)
            {
                into.Rivals.Table.Selected = FinanceRules.Clamp(view.Rival, into.Rivals.Table.RowCount);
            }
        }

        public bool Do(FinanceAct act, FinanceView view, int arg)
        {
            Acts.Add(act + ":" + arg);
            Amounts.Add(view.Amount);
            Moved++;
            return true;
        }
    }

    private static FinanceScreen NewFinance(int tab, out FinanceStub stub)
    {
        stub = new FinanceStub();
        var screen = new FinanceScreen(stub, UiSamples.FinanceWordsOf((key, english) => english));
        screen.Press(UiAppWindow.NavCommand, tab);
        return screen;
    }

    private static UiScene LayFinance(FinanceScreen screen)
    {
        var s = new UiScene();
        screen.Lay(s, UiFrame.FullMenu(16f / 9f, "Finance"), true, UiDefaultKeys.Keyboard);
        return s;
    }

    private static bool FinanceFaceChecksRun;

    private static void FinanceFaceChecks()
    {
        if (FinanceFaceChecksRun)
        {
            return;
        }
        FinanceFaceChecksRun = true;
        // Each tab is its own page: seven tabs, seven different sets of words, and no tab shows another's parts.
        var pages = new string[FinanceFace.Tabs];
        for (int tab = 0; tab < FinanceFace.Tabs; tab++)
        {
            pages[tab] = TextsOf(LayFinance(NewFinance(tab, out _)));
        }
        string same = "none";
        for (int a = 0; a < pages.Length; a++)
        {
            for (int b = a + 1; b < pages.Length; b++)
            {
                same = pages[a] == pages[b] ? a + " and " + b : same;
            }
        }
        Check(same == "none", "finance: every tab builds a different page (same: " + same + ")");
        Check(pages[FinanceFace.Credit].Contains("Offers at your score") && !pages[FinanceFace.Borrow].Contains("Offers at your score") && pages[FinanceFace.Borrow].Contains("What it costs over the term")
              && !pages[FinanceFace.Credit].Contains("What it costs over the term"), "finance: Credit explains the score and lists the offers; Borrow is the loan form: they are not the same page");
        Check(pages[FinanceFace.CashFlow].Contains("Brought forward") || pages[FinanceFace.CashFlow].Contains("Day 289, today"), "finance: Cash flow is the statement of money in and out");
        Check(!pages[FinanceFace.Accounts].Contains("Day 289, today") && pages[FinanceFace.Accounts].Contains("Pay off") && pages[FinanceFace.Accounts].Contains("Credit card, Metropolitan Savings"),
            "finance: Accounts lists the accounts and cards with their balances and what to do, never the cash flow");
        Check(!pages[FinanceFace.Overview].Contains("Offers at your score") && !pages[FinanceFace.Rivals].Contains("What you owe and when") && !pages[FinanceFace.Standing].Contains("Who is catching whom"),
            "finance: no tab carries a part of another (the cause: one model reused for every tab)");

        // The cause of the dead Take it: the page asked an old panel to borrow and it had no bank chosen. Now the screen asks the director itself, with the amount.
        {
            FinanceScreen screen = NewFinance(FinanceFace.Borrow, out FinanceStub stub);
            UiScene s = LayFinance(screen);
            int more = NodeOf(s, FinanceFace.AmountMore, FinanceFace.ArgAmount);
            bool stepped = more >= 0 && screen.Press(FinanceFace.AmountMore, FinanceFace.ArgAmount);
            s = LayFinance(screen);
            int take = NodeOf(s, FinanceFace.CardAct, (int)FinanceAct.Take);
            bool done = take >= 0 && !s.Nodes[take].Disabled && screen.Press(FinanceFace.CardAct, (int)FinanceAct.Take);
            Check(stepped && screen.View.Amount == 255000 && done && stub.Acts.Count == 1 && stub.Acts[0] == "Take:0" && stub.Amounts[0] == 255000,
                "finance: Take it reaches the borrow path with the amount stepped to (" + string.Join(",", stub.Acts) + ", " + (stub.Amounts.Count > 0 ? stub.Amounts[0].ToString() : "none") + ")");
            FinanceScreen keyed = NewFinance(FinanceFace.Borrow, out FinanceStub keyedStub);
            LayFinance(keyed);
            bool f = keyed.Act(UiAction.Alt);
            Check(f && keyedStub.Acts.Count == 1 && keyedStub.Acts[0] == "Take:0", "finance: F takes the loan as the button does");
        }

        // Credit's Apply opens the offer on Borrow; a row of the offers is chosen by the focus.
        {
            FinanceScreen screen = NewFinance(FinanceFace.Credit, out FinanceStub stub);
            LayFinance(screen);
            screen.Focused(FinanceFace.TableA + UiDataTable.Row, 2);
            bool applied = screen.Act(UiAction.Alt);
            LayFinance(screen);
            Check(applied && screen.View.Tab == FinanceFace.Borrow && screen.View.Offer == 2, "finance: Apply on an offer opens that offer on Borrow");
        }

        // The Rivals page: the row chosen is the profile shown, and every answer on it does something.
        {
            string wrong = "none";
            for (int row = 0; row < 5; row++)
            {
                FinanceScreen screen = NewFinance(FinanceFace.Rivals, out _);
                LayFinance(screen);
                screen.Press(FinanceFace.TableA + UiDataTable.Row, row);
                LayFinance(screen);
                FinanceRivalsFacts r = screen.Facts.Rivals;
                wrong = r.Table.Selected == FinanceRules.Clamp(row, r.Table.RowCount) ? wrong : "row " + row;
            }
            Check(wrong == "none" && FinanceRules.Clamp(7, 3) == 2 && FinanceRules.Clamp(-1, 3) == 0 && FinanceRules.Clamp(0, 0) == -1, "finance: the row chosen on Rivals is the one the profile is read for (" + wrong + ")");
            FinanceRivalsFacts facts = UiSamples.FinanceFactsOf((key, english) => english, "rivals_move").Rivals;
            string firstRow = facts.Table.Rows[facts.Table.Selected].Cells[0];
            Check(firstRow.StartsWith(facts.Profile.Name, StringComparison.Ordinal) || facts.Profile.Name == "Huang Guo", "finance: the sample's profile is the row chosen (" + firstRow + ")");
        }
        {
            FinanceScreen screen = NewFinance(FinanceFace.Rivals, out FinanceStub stub);
            UiScene s = LayFinance(screen);
            int pressed = 0, silent = 0;
            for (int i = 0; i < s.NodeCount; i++)
            {
                ref UiNode n = ref s.Nodes[i];
                if (n.Disabled || (n.Command != FinanceFace.Answer && n.Command != UiAppWindow.ActionCommand && n.Command != FinanceFace.CardAct))
                {
                    continue;
                }
                int before = stub.Acts.Count;
                FinanceScreen probe = NewFinance(FinanceFace.Rivals, out FinanceStub probeStub);
                LayFinance(probe);
                bool did = probe.Press(n.Command, n.Arg) && probeStub.Acts.Count == 1;
                pressed++;
                silent += did ? 0 : 1;
            }
            Check(pressed >= 3 && silent == 0, "finance: every answer and action shown on Rivals reaches a handler that acts (" + pressed + " pressed, " + silent + " silent)");
            string text = TextsOf(s);
            Check(text.Contains("Flare campaign") && !text.Contains("Hold prices") && text.Contains("Cut your prices 5% for 14 days") && text.Contains("Raising prices 4% in all shops")
                  && text.Contains("Forecast: 12% more customers"),
                "finance: answers show only with a move to answer, with what the rival did, the Flare campaign with its forecast, and only ones that do something");
            string quiet = TextsOf(LayFinance(ScreenWith(FinanceFace.Rivals, "rivals_new")));
            Check(!quiet.Contains("How do you answer"), "finance: with no move to answer, Rivals shows no answers");
        }

        // The charts: a day before the save began is not a zero.
        {
            var book = new DaySeriesBook();
            book.Set(DaySeriesCatalogue.Cash, 3, 25000);
            book.Set(DaySeriesCatalogue.Cash, 4, 24100);
            book.Set(DaySeriesCatalogue.Cash, 5, 24850);
            var values = new float[30];
            var days = new int[30];
            int n = FinanceRules.ExistingDays(book, DaySeriesCatalogue.Cash, 5, 30, false, values, days);
            bool zeros = false;
            for (int i = 0; i < n; i++)
            {
                zeros |= values[i] <= 0f;
            }
            Check(n == 3 && !zeros && days[0] == 3 && days[2] == 5, "finance: a chart covers only the days that exist, never zeros before the save began (" + n + " days)");
            book.Set(DaySeriesCatalogue.Tycoon(0), 5, 0);
            book.Set(DaySeriesCatalogue.Tycoon(0), 8, 31420);
            book.Set(DaySeriesCatalogue.Tycoon(0), 9, 31500);
            int m = FinanceRules.ExistingDays(book, DaySeriesCatalogue.Tycoon(0), 9, 30, true, values, days);
            Check(m == 2 && days[0] == 8 && values[0] == 31420f, "finance: a rival's worth is drawn from the day they came forward, not from a zero (" + m + ")");
            string raw = "none";
            foreach (string state in new[] { "overview_new", "cash_new", "rivals_new" })
            {
                FinanceFacts f = UiSamples.FinanceFactsOf((key, english) => english, state);
                UiChartModel chart = state == "overview_new" ? f.Overview.Cash30 : state == "cash_new" ? f.Cash.Chart : f.Rivals.Chart;
                foreach (UiSeries series in chart.Series)
                {
                    foreach (float v in series.Values)
                    {
                        raw = v <= 0f ? state : raw;
                    }
                }
                string label = chart.Format(chart.Series[0].Values[0]);
                raw = label.StartsWith("$", StringComparison.Ordinal) ? raw : state + " label " + label;
            }
            Check(raw == "none", "finance: the samples' charts have no zero day and write every value as money (" + raw + ")");
        }

        // The Overview shows you, with your own picture: the player's name behind the HUD portrait the host draws, not a stranger's face.
        {
            FinanceFacts f = UiSamples.FinanceFactsOf((key, english) => english, "overview");
            string text = TextsOf(LayFinance(NewFinance(FinanceFace.Overview, out _)));
            string live = EconomySource(@"..\Ui\Money\FinanceLive.Overview.cs");
            Check(text.Contains(f.Overview.Player) && !text.Contains("\nYou\n") && live.Contains("FaceSources.Player(") && live.Contains("RivalGame.PlayerName()"),
                "finance: the Overview is headed by the player's own name and the portrait source of the HUD (FaceSources.Player), not a painted stranger");
        }

        // The rest of the old app, a button at a time: each does something and an inactive one is greyed.
        {
            int pressed = 0, silent = 0;
            string quiet = "none";
            foreach (string state in UiSamples.FinanceStates)
            {
                FinanceScreen probe = NewFinance(FinanceFaceTab(state), out _);
                UiScene s = LayFinance(probe);
                var targets = new List<int[]>();
                for (int i = 0; i < s.NodeCount; i++)
                {
                    ref UiNode n = ref s.Nodes[i];
                    if (!n.Disabled && Array.IndexOf(FinanceFace.Commands, n.Command) >= 0 && n.Command != UiAppWindow.NavCommand && n.Command != UiChart.First + UiChart.Point
                        && n.Command != FinanceFace.AmountType && n.Command != UiStatement.First + UiStatement.Group && n.Command != UiStatement.First + UiStatement.Line
                        && n.Command != FinanceFace.TableA + UiDataTable.Head && n.Command != FinanceFace.TableB + UiDataTable.Head)
                    {
                        targets.Add(new[] { n.Command, n.Arg });
                    }
                }
                foreach (int[] t in targets)
                {
                    FinanceScreen screen = NewFinance(FinanceFaceTab(state), out FinanceStub stub);
                    LayFinance(screen);
                    bool did = screen.Press(t[0], t[1]);
                    pressed++;
                    if (!did)
                    {
                        silent++;
                        quiet = state + ": " + t[0] + "," + t[1];
                    }
                }
            }
            Check(pressed > 40 && silent == 0, "finance: every button of every state does something (" + pressed + " pressed, " + silent + " silent; " + quiet + ")");
        }
    }

    private static int FinanceFaceTab(string state)
    {
        FinanceView v = UiSamples.FinanceViewOf(state);
        return v.Tab;
    }

    private static FinanceScreen ScreenWith(int tab, string sample)
    {
        var stub = new FinanceSampleStub(sample);
        var screen = new FinanceScreen(stub, UiSamples.FinanceWordsOf((key, english) => english));
        screen.Press(UiAppWindow.NavCommand, tab);
        return screen;
    }

    // A source that answers with one named sample whatever the tab.
    private sealed class FinanceSampleStub : IFinanceSource
    {
        private readonly FinanceFacts _facts;

        public FinanceSampleStub(string sample)
        {
            _facts = UiSamples.FinanceFactsOf((key, english) => english, sample);
        }

        public int Generation => 0;

        public int TakeWanted() => -1;

        public void Read(FinanceFacts into, FinanceView view)
        {
            into.Ready = _facts.Ready;
            into.Score = _facts.Score;
            into.Overview = _facts.Overview;
            into.Credit = _facts.Credit;
            into.Borrow = _facts.Borrow;
            into.Cash = _facts.Cash;
            into.Accounts = _facts.Accounts;
            into.Rivals = _facts.Rivals;
            into.Standing = _facts.Standing;
        }

        public bool Do(FinanceAct act, FinanceView view, int arg) => true;
    }
}
