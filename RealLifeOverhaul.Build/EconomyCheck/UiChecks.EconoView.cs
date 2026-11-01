using System.Collections.Generic;
using RealLifeOverhaul.Ui;

internal static partial class Program
{
    private static bool _econoChecked;

    // EconoView, every state: laid out by the screen checks in every language and frame, with the commands the money page
    // answers. The view's own rules are checked once, beside them.
    static partial void UiCasesEconoView(UiScreenDef def, List<UiScreenCase> cases)
    {
        if (def.Id != "econoview")
        {
            return;
        }
        foreach (string state in UiSamples.EconoStates)
        {
            string st = state;
            cases.Add(new UiScreenCase
            {
                Def = def, Name = "econoview (" + st + ")", Title = "EconoView", Commands = EconoCommands,
                Lay = (s, f, dark, k, t) => UiEcono.Lay(s, UiSamples.Econo(t, st), f, dark, k)
            });
        }
        if (!_econoChecked)
        {
            _econoChecked = true;
            EconoChecks();
        }
    }

    private static readonly int[] EconoCommands =
    {
        UiAppWindow.NavCommand, UiAppWindow.ActionCommand, UiAppWindow.RowCommand, UiAppWindow.MoreCommand, UiEcono.StatementFirst + UiStatement.Period, UiEcono.StatementFirst + UiStatement.Group,
        UiEcono.StatementFirst + UiStatement.Line, UiChart.First + UiChart.Point, UiEcono.PickCommand, UiEcono.FilterCommand, UiEcono.OptionCommand, UiEcono.LessCommand,
        UiEcono.AmountCommand, UiEcono.MoreCommand, UiEcono.SwitchCommand, UiEcono.ButtonCommand, UiEcono.LinkCommand, UiEcono.ChoiceCommand,
        UiEcono.EmptyCommand
    };

    // What the money page promises beyond the layout: each line and transaction wears the glyph of its kind; where the
    // money came from and went are the day's lines by their sign, the four largest and the rest; an amount steps by a
    // round tenth and stays between its ends; a fund's return reads with its sign and grades even at nothing; a heading
    // with nothing under it and nothing on it is left out; the open lists stand under their own fields.
    private static void EconoChecks()
    {
        UiWords en = UiWords("en");
        Check(EconoViews.TxIcon("ba:transaction_wage") == UiIcon.People && EconoViews.TxIcon("ba:transaction_playerjobsalary") == UiIcon.Briefcase
              && EconoViews.TxIcon("ba:transaction_taxpayment") == UiIcon.Receipt && EconoViews.TxIcon("ba:transaction_loanpayoff") == UiIcon.Bank
              && EconoViews.TxIcon("ba:transaction_investmentpayout") == UiIcon.ChartLineUp && EconoViews.TxIcon("ba:transaction_taxiride") == UiIcon.Car
              && EconoViews.TxIcon("some_mod:transaction_new") == UiIcon.Coin, "econoview: a transaction wears its kind's glyph, one the kit cannot tell a coin");
        Check(EconoViews.RowIcon("Bloom Café", "econoview_row_businesses", 1) == UiIcon.Store && EconoViews.RowIcon("econoview_row_loans", "econoview_row_ongoing_expenses", -1) == UiIcon.Bank
              && EconoViews.RowIcon("ba:itemname_coffee", "econoview_row_sales", 1) == UiIcon.Coin && EconoViews.RowIcon("x", "y", -1) == UiIcon.Receipt,
            "econoview: a statement's line wears its own glyph, else its heading's, else money in or out");
        var rows = new[]
        {
            new EconoIncomeRow { Name = "Shops", Parent = -1, Values = new[] { 100f, 0f, 0f, 0f } },
            new EconoIncomeRow { Name = "A", Parent = 0, Values = new[] { 500f, 0f, 0f, 0f } }, new EconoIncomeRow { Name = "B", Parent = 0, Values = new[] { -400f, 0f, 0f, 0f } },
            new EconoIncomeRow { Name = "C", Parent = 0, Values = new[] { 300f, 0f, 0f, 0f } }, new EconoIncomeRow { Name = "D", Parent = 0, Values = new[] { 200f, 0f, 0f, 0f } },
            new EconoIncomeRow { Name = "E", Parent = 0, Values = new[] { 100f, 0f, 0f, 0f } }, new EconoIncomeRow { Name = "F", Parent = 0, Values = new[] { 50f, 0f, 0f, 0f } },
            new EconoIncomeRow { Name = "Salary", Parent = -1, Values = new[] { 250f, 0f, 0f, 0f } },
            new EconoIncomeRow { Name = "Total", Parent = -1, Total = true, Values = new[] { 1000f, 0f, 0f, 0f } },
        };
        var shareIn = new UiEconoShare();
        EconoViews.Shares(rows, 0, 1, shareIn, v => v.ToString("0"), "Rest");
        Check(shareIn.Labels.Length == 5 && shareIn.Labels[0] == "A" && shareIn.Labels[1] == "C" && shareIn.Labels[2] == "Salary" && shareIn.Labels[4] == "Rest"
              && shareIn.Shares[4] == 150f, "econoview: money in is the day's lines above nothing (a heading with no lines for itself), the four largest first and the rest as one");
        var shareOut = new UiEconoShare();
        EconoViews.Shares(rows, 0, -1, shareOut, v => v.ToString("0"), "Rest");
        Check(shareOut.Labels.Length == 1 && shareOut.Labels[0] == "B" && shareOut.Shares[0] == 400f, "econoview: money out is the day's lines below nothing, as positive shares");
        Check(EconoViews.Step(500) == 50 && EconoViews.Step(38000) == 5000 && EconoViews.Step(1200) == 100 && EconoViews.Step(0) == 1 && EconoViews.Step(7) == 1,
            "econoview: an amount steps by about a tenth, rounded to 1, 2 or 5 of a power of ten, never under one");
        Check(EconoViews.Stepped(500, -1, 480, 1000) == 480 && EconoViews.Stepped(990, 1, 0, 1000) == 1000 && EconoViews.Stepped(500, 1, 0, 1000) == 550,
            "econoview: a step stays between the amount's ends");
        var fund = new EconoFundView { Invested = 10000f, Interest = 420f };
        Check(EconoViews.Return(fund) == "+4.2%" && EconoViews.Grade(fund) == 100 && EconoViews.Grade(new EconoFundView()) == 60
              && EconoViews.Grade(new EconoFundView { Invested = 100f, Interest = -4f }) == 20
              && EconoViews.Return(new EconoFundView { Invested = 1000f, Interest = -25f }) == "-2.5%",
            "econoview: a fund's return reads with its sign, nothing gained the middle grade, four lost in a hundred the bottom's");
        Check(EconoViews.Unsigned("-$1,200") == "$1,200" && EconoViews.Unsigned("$40") == "$40" && EconoViews.Paid(new EconoLoanView { Total = 50000f, Left = 38000f }) == 24,
            "econoview: money out shows without its minus under its column, and a loan's share paid is what it has paid of the whole");
        var state = new EconoState { Money = v => v.ToString("0"), DayNames = new[] { "Yesterday (Day 9)", "Day 8" }, DayNumbers = new[] { 9, 8 } };
        var empty = new[]
        {
            new EconoIncomeRow { Key = "econoview_row_businesses", Name = "Businesses", Parent = -1, Values = new[] { 0f, 0f, 0f, 0f } },
            new EconoIncomeRow { Key = "econoview_row_other_income", Name = "Other income", Parent = -1, Values = new[] { 80f, 0f, 0f, 0f } },
            new EconoIncomeRow { Name = "Salary", Parent = 1, Values = new[] { 80f, 60f, 0f, 0f } },
            new EconoIncomeRow { Name = "Total", Parent = -1, Total = true, Values = new[] { 80f, 60f, 0f, 0f } },
        };
        var statement = new UiStatementModel();
        EconoViews.Statement(statement, en, state, empty, state.DayNames, 0, UiIcon.Chart, null, true, out int[] heads, out int[][] under);
        Check(statement.Groups.Length == 1 && statement.Groups[0].Lines.Length == 1 && statement.Groups[0].Lines[0].In == "80" && statement.Groups[0].Lines[0].Balance == null
              && statement.Balance == null && statement.Columns[4] == string.Empty && statement.Totals.Length == 3 && statement.Totals[2] == "+80" && statement.ChangeGood,
            "econoview: a heading with nothing is left out, a line's money stands in or out, the day's profit stands at the foot and the head holds no large figure");
        var scene = new UiScene();
        UiFrame frame = UiFrame.FullMenu(16f / 9f, "EconoView");
        foreach (string st in new[] { "overview_period_open", "transactions_type_open", "business_choice_open" })
        {
            UiEconoModel open = UiSamples.Econo(en, st);
            UiEcono.Lay(scene, open, frame, true, UiDefaultKeys.Keyboard);
            Check(open.OpenList >= 0 && open.FieldW[open.OpenList] > 0f && EconoListUnder(scene, open.FieldX[open.OpenList], open.FieldY[open.OpenList] + UiTokens.Control),
                "econoview: an open choice's list stands under its own field (" + st + ")");
        }
        UiEconoModel loans = UiSamples.Econo(en, "loans_three");
        Check(loans.Controls.Length == 2 && loans.Controls[1].ActionKey == UiAction.Alt && loans.Controls[1].ActionKind == UiButtonKind.Primary && loans.Picks.Chosen == 0,
            "econoview: a loan's payment is the page's one main action, on F, for the loan chosen in the list");
    }

    // True when an open list's panel (on the overlay) starts at x, just under the field whose foot is at y.
    private static bool EconoListUnder(UiScene s, float x, float y)
    {
        for (int i = 0; i < s.ItemCount; i++)
        {
            if (s.Items[i].Layer == 1 && s.Items[i].Kind == UiKind.Box && System.Math.Abs(s.Items[i].X - x) < 0.5f && s.Items[i].Y > y && s.Items[i].Y <= y + UiTokens.S2)
            {
                return true;
            }
        }
        return false;
    }
}
