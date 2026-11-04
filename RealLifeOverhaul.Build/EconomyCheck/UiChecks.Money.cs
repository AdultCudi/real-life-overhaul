using System.Collections.Generic;
using RealLifeOverhaul.DaySeries;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Ui;

internal static partial class Program
{
    private static bool _moneyChecked, _financeFaceChecked;

    private static readonly int[] FinanceCommandsAll = BuildFinanceCommands();

    private static int[] BuildFinanceCommands()
    {
        var list = new List<int>(FinanceFace.Commands) { FinanceFace.TableA + UiDataTable.Act };
        return list.ToArray();
    }

    static partial void UiCasesMoney(UiScreenDef def, List<UiScreenCase> cases)
    {
        if (def.Id == "finance")
        {
            foreach (string state in UiSamples.FinanceStates)
            {
                string st = state;
                cases.Add(new UiScreenCase
                {
                    Def = def, Name = "finance (" + st + ")", Title = "Finance", Commands = FinanceCommandsAll,
                    Lay = (s, f, dark, k, t) => FinanceFace.Lay(s, UiSamples.FinanceFactsOf(t, st), UiSamples.FinanceViewOf(st), UiSamples.FinanceWordsOf(t), new FinanceLayout(), f, dark, k)
                });
            }
            if (!_financeFaceChecked)
            {
                _financeFaceChecked = true;
                FinanceFaceChecks();
            }
        }
        else if (def.Id == "banks")
        {
            foreach (string state in UiSamples.MoneyBankStates)
            {
                string st = state;
                cases.Add(new UiScreenCase
                {
                    Def = def, Name = "banks (" + st + ")", Title = "Banks", Commands = MoneyCommandsAll,
                    Lay = (s, f, dark, k, t) => MoneyPages.Lay(s, UiSamples.MoneyBanks(t, st), f, dark, k)
                });
            }
        }
        else if (def.Id == "own_bank")
        {
            foreach (string state in UiSamples.MoneyOwnStates)
            {
                string st = state;
                cases.Add(new UiScreenCase
                {
                    Def = def, Name = "own bank (" + st + ")", Title = "Your bank", Commands = MoneyCommandsAll,
                    Lay = (s, f, dark, k, t) => MoneyPages.Lay(s, UiSamples.MoneyOwn(t, st), f, dark, k)
                });
            }
        }
        else if (def.Id == "utilities")
        {
            foreach (string state in UiSamples.UtilitiesStates)
            {
                string st = state;
                cases.Add(new UiScreenCase
                {
                    Def = def, Name = "utilities (" + st + ")", Title = "Utilities", Commands = UtilitiesFace.Commands,
                    Lay = (s, f, dark, k, t) => UtilitiesFace.Lay(s, UiSamples.Utilities(t, st), f, dark, k)
                });
            }
        }
        if (!_moneyChecked && (def.Id == "finance" || def.Id == "banks" || def.Id == "own_bank" || def.Id == "utilities"))
        {
            _moneyChecked = true;
            MoneyRuleChecks();
        }
        if (!_bankFaceChecked && def.Id == "own_bank")
        {
            _bankFaceChecked = true;
            BankFaceChecks();
        }
        if (!_utilitiesFaceChecked && def.Id == "utilities")
        {
            _utilitiesFaceChecked = true;
            UtilitiesFaceChecks();
        }
    }

    private static readonly int[] MoneyCommandsAll = BuildMoneyCommands();

    private static int[] BuildMoneyCommands()
    {
        var list = new List<int>
        {
            UiAppWindow.NavCommand, UiAppWindow.ActionCommand, UiAppWindow.RowCommand, UiAppWindow.MoreCommand, UiAppWindow.NoteCommand,
            MoneyCommands.StepLess, MoneyCommands.StepType, MoneyCommands.StepMore, MoneyCommands.Choice, MoneyCommands.Follow, MoneyCommands.Export, MoneyCommands.Link,
            UiChart.First + UiChart.Point, UiStatement.First + UiStatement.Period, UiStatement.First + UiStatement.Group, UiStatement.First + UiStatement.Line,
            UiCatalogue.First + UiCatalogue.Card, UiCatalogue.First + UiCatalogue.Action,
            MoneyCommands.PickBank, MoneyCommands.CardAct, MoneyCommands.Go, MoneyCommands.PeriodNext, MoneyCommands.BackOut, MoneyCommands.TypeField
        };
        for (int k = 0; k < MoneyCommands.CardsMost; k++)
        {
            list.Add(MoneyCommands.CardLess + k);
            list.Add(MoneyCommands.CardMore + k);
            list.Add(MoneyCommands.CardSwitch + k);
        }
        for (int i = 0; i <= UiDataTable.MassAnswer; i++)
        {
            list.Add(MoneyPages.Table2Base + i);
        }
        for (int i = 0; i <= UiDataTable.MassAnswer; i++)
        {
            list.Add(UiDataTable.First + i);
        }
        return list.ToArray();
    }

    // The series, the loan feed and the sums the pages show, with no game behind them.
    private static void MoneyRuleChecks()
    {
        var book = new DaySeriesBook { Seed = DaySeriesRules.DefaultSeed };
        Check(book.Set(DaySeriesCatalogue.Cash, 10, 100) && book.Set(DaySeriesCatalogue.Cash, 40, 112)
              && DaySeriesRules.Change(book, DaySeriesCatalogue.Cash, 40, 30, out int up) && up == 12, "day series: a change over 30 days is the later reading less the earlier");
        Check(!book.Set(DaySeriesCatalogue.Cash, 1, DaySeriesCatalogue.NoValue), "day series: the empty mark is not a reading");
        string written = DaySeriesStore.Write(book);
        DaySeriesBook back = DaySeriesStore.Read(written, out int version);
        Check(version == DaySeriesCatalogue.Version && back != null && back.Get(DaySeriesCatalogue.Cash, 40, out int cash) && cash == 112 && back.Seed == book.Seed,
            "day series: a line round trips, versioned");
        Check(DaySeriesStore.Read("not a line", out _) == null && DaySeriesStore.Read("v=1~s:only", out _) == null, "day series: a line that is not the series, or has no day, is refused");
        int loans = DaySeriesRules.Borrow(book, 20, new[] { "Karen Aerospace & Motors", "Luthor Everything Co." }, new[] { 40000000, 20000000 }, 3, new[] { 2000000, 2000000, 2000000 });
        int again = DaySeriesRules.Borrow(book, 20, new[] { "Karen Aerospace & Motors", "Luthor Everything Co." }, new[] { 40000000, 20000000 }, 3, new[] { 2000000, 2000000, 2000000 });
        Check(loans >= 0 && loans <= DaySeriesRules.LoansADay && again == 0, "day series: the loan feed is capped and the same day does not borrow twice");
        var other = new DaySeriesBook { Seed = DaySeriesRules.DefaultSeed };
        DaySeriesRules.Borrow(other, 20, new[] { "Luthor Everything Co.", "Karen Aerospace & Motors" }, new[] { 20000000, 40000000 }, 3, new[] { 2000000, 2000000, 2000000 });
        Check(book.LoanCount == other.LoanCount, "day series: the same seed and day borrow the same number, whoever asks");
        Check(DaySeriesRules.DailyPayment(100000, 480, 365) > 0 && DaySeriesRules.Interest(100000, DaySeriesRules.DailyPayment(100000, 480, 365), 365) > 0,
            "day series: a loan's daily payment and its interest over the term are both worked out");
        Check(DaySeriesRules.HistoryOk(34) && !DaySeriesRules.HistoryOk(29) && DaySeriesRules.CoverOk(2000, 100) && !DaySeriesRules.CoverOk(1000, 100),
            "day series: a business loan wants 30 days of takings and takings that cover the payment 20 times");
        Check(DaySeriesRules.Headroom(579, CreditCatalogue.LendingThreshold, 800) == 0 && DaySeriesRules.Headroom(800, CreditCatalogue.LendingThreshold, 800) == 100
              && DaySeriesRules.Headroom(690, CreditCatalogue.LendingThreshold, 800) > 0 && DaySeriesRules.Headroom(690, CreditCatalogue.LendingThreshold, 800) < 100,
            "day series: headroom is how far the rating sits over 580, and nothing under it");
        var parts = new int[3];
        int n = DaySeriesRules.Split(7, 1000, new[] { "A", "B", "C" }, parts);
        Check(n == 3 && parts[0] + parts[1] + parts[2] == 1000, "day series: named accounts sum to the pool");
        Check(DaySeriesRules.WorthUnits(2200000000L) == 2200000 && DaySeriesRules.CutFootfall(new DaySeriesBook { CutFrom = 5, CutUntil = 19 }, 10) == 3
              && DaySeriesRules.CutFootfall(new DaySeriesBook { CutFrom = 5, CutUntil = 19 }, 19) == 0, "day series: worth is kept in thousands, and a price cut lasts 14 days");
        Check(DaySeriesCatalogue.Clean("a|b;c") == "a b c", "day series: a name cannot break the save line");
        string kept = DaySeriesStore.Write(book);
        Check(kept.Contains("~n:") && kept.StartsWith("v=1"), "day series: loans share the one line");
    }
}
