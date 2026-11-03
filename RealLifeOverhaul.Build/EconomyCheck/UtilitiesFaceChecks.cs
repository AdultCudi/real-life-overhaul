using System;
using System.Collections.Generic;
using RealLifeOverhaul.Ui;

// The Utilities screen on the kit (UtilitiesScreen): the owner's 4 Nov 2026 play of day 5 showed the bank's cash flow on the Accounts and
// Your network tabs, bare tiles for the internet and a dead company page for a player with no company. The checks say what each tab shows
// and what each button does, with no game behind them: a source that records what it is asked.
internal static partial class Program
{
    // A source standing in for the save: the facts of a sample, every act recorded, and a generation that moves when an act is done.
    private sealed class UtilitiesStub : IUtilitiesSource
    {
        private readonly UtilitiesFacts _facts;
        public readonly List<string> Acts = new List<string>();
        public int Reads;
        public int Moved;

        public UtilitiesStub(string state)
        {
            _facts = UiSamples.UtilitiesFactsOf(state);
        }

        public int Generation => Moved;

        public void Read(UtilitiesFacts into)
        {
            Reads++;
            into.Ready = _facts.Ready;
            into.Day = _facts.Day;
            into.Office = _facts.Office;
            into.ByPhone = _facts.ByPhone;
            into.InstallFee = _facts.InstallFee;
            into.InstallDays = _facts.InstallDays;
            into.ContractMonths = _facts.ContractMonths;
            into.Owed = _facts.Owed;
            into.Month = _facts.Month;
            into.Accounts = _facts.Accounts;
            into.Places = _facts.Places;
            into.Network = _facts.Network;
            into.GraceLeft = _facts.GraceLeft;
            into.GraceDays = _facts.GraceDays;
            into.GraceUntilDay = _facts.GraceUntilDay;
        }

        public bool Do(UtilitiesAct act, int a, int b, int c, int d)
        {
            Acts.Add(act + ":" + a + "," + b + "," + c + "," + d);
            Moved++;
            return true;
        }
    }

    private static readonly string[] CashFlowWords =
    {
        "Cash flow", "Brought forward", "Wages and staff", "Investments and property", "Rent and bills", "Customers over 60 days", "IN", "OUT", "BALANCE"
    };

    private static bool _utilitiesFaceChecked;

    private static string TextsOf(UiScene s)
    {
        var text = new System.Text.StringBuilder(1024);
        for (int i = 0; i < s.ItemCount; i++)
        {
            if (s.Items[i].Kind == UiKind.Text && !string.IsNullOrEmpty(s.Items[i].Text))
            {
                text.Append(s.Items[i].Text).Append('\n');
            }
        }
        return text.ToString();
    }

    private static UiScene LayUtilities(UtilitiesScreen screen)
    {
        var s = new UiScene();
        screen.Lay(s, UiFrame.FullMenu(16f / 9f, "Utilities"), true, UiDefaultKeys.Keyboard);
        return s;
    }

    private static UtilitiesScreen NewUtilities(string state, out UtilitiesStub stub)
    {
        stub = new UtilitiesStub(state);
        var screen = new UtilitiesScreen(stub, UiSamples.UtilitiesWordsOf((key, english) => english));
        screen.Press(UiAppWindow.NavCommand, state.StartsWith("internet", StringComparison.Ordinal) ? 1 : state.StartsWith("network", StringComparison.Ordinal) ? 2 : 0);
        return screen;
    }

    // The target of a command and argument, the one on the highest layer when a sheet stands over the page.
    private static int NodeOf(UiScene s, int command, int arg)
    {
        int found = -1;
        for (int i = 0; i < s.NodeCount; i++)
        {
            if (s.Nodes[i].Command == command && s.Nodes[i].Arg == arg && (found < 0 || s.Nodes[i].Layer >= s.Nodes[found].Layer))
            {
                found = i;
            }
        }
        return found;
    }

    // Presses a target of the laid scene by its command and argument; true when the screen did something.
    private static bool PressUtilities(UtilitiesScreen screen, int command, int arg, out UiScene after)
    {
        UiScene s = LayUtilities(screen);
        int node = NodeOf(s, command, arg);
        bool did = node >= 0 && !s.Nodes[node].Disabled && screen.Press(command, arg);
        after = LayUtilities(screen);
        return did;
    }

    private static void UtilitiesFaceChecks()
    {
        // The cause: the money host reused one model for every tab and no tab cleared what the one before had set, so a tab that set no
        // statement still showed the cash flow another tab had filled, and a hero, a chart and a button stayed too.
        var model = new MoneyModel { Statement = new UiStatementModel(), Chart = new UiChartModel(), Hero = new UiHeroModel(), Table = new UiDataTableModel(), Tab = 2, Icon = UiIcon.Bank };
        model.Actions = new[] { new UiActionModel { Label = "Dig" } };
        model.Reset();
        Check(model.Statement == null && model.Chart == null && model.Hero == null && model.Table == null && model.Actions.Length == 0 && model.Tab == 2 && model.Icon == UiIcon.Bank,
            "utilities: a money tab starts empty (nothing is left of the tab before) and keeps its place");
        var cash = new MoneySourceStub();
        var host = new MoneyHost("finance", MoneyApp.Finance, UiIcon.Wallet, cash);
        var scene = new UiScene();
        host.Lay(scene, UiFrame.FullMenu(16f / 9f, "Finance"), true, UiDefaultKeys.Keyboard);
        Check(TextsOf(scene).Contains("Brought forward"), "utilities: the money host still lays the cash flow statement where a tab asks for it (Finance, Banks)");
        host.Press(UiAppWindow.NavCommand, 1);
        host.Lay(scene, UiFrame.FullMenu(16f / 9f, "Finance"), true, UiDefaultKeys.Keyboard);
        Check(!TextsOf(scene).Contains("Brought forward"), "utilities: a money tab that sets no statement does not show the one the tab before set");

        // No Utilities tab shows the bank's cash flow, with the utilities' own facts in its place.
        foreach (string state in UiSamples.UtilitiesStates)
        {
            UtilitiesScreen screen = NewUtilities(state, out _);
            UiScene s = LayUtilities(screen);
            string all = TextsOf(s);
            string leaked = "none";
            foreach (string word in CashFlowWords)
            {
                if (state.StartsWith("network", StringComparison.Ordinal) && word == "Customers over 60 days")
                {
                    continue;
                }
                leaked = all.Contains("\n" + word + "\n") ? word : leaked;
            }
            Check(leaked == "none", "utilities: no tab shows the bank's cash flow (" + state + ": " + leaked + ")");
        }
        string accounts = TextsOf(LayUtilities(NewUtilities("accounts", out _)));
        Check(accounts.Contains("Bloom Cafe") && accounts.Contains("Paid") && accounts.Contains("Unpaid") && accounts.Contains("Overdue") && accounts.Contains("Pay now"),
            "utilities: the Accounts tab lists each place's power, water and internet with paid, unpaid and overdue chips and a way to pay");
        string internet = TextsOf(LayUtilities(NewUtilities("internet", out _)));
        Check(internet.Contains("Kestrel Cable") && internet.Contains("300 Mbit") && internet.Contains("$65") && internet.Contains("Your plan") && internet.Contains("Change plan"),
            "utilities: the Internet tab names each plan's provider, its speed with its unit and its price, marks the plan the place has and offers the change");

        // The company not founded is the pitch to found it, with its cost and a button that works: never a page of zeros.
        foreach (string state in new[] { "network_new", "network_day5", "network_nohq" })
        {
            UtilitiesScreen screen = NewUtilities(state, out _);
            UiScene s = LayUtilities(screen);
            string all = TextsOf(s);
            int found = NodeOf(s, UtilitiesFace.HeroAction, 2);
            Check(all.Contains("Found your broadband company") && all.Contains("$1,000,000") && all.Contains("What you get") && found >= 0 && !all.Contains("Takings a day"),
                "utilities: with no company the Your network tab shows the pitch to found it, what it costs and what it gets (" + state + ")");
            Check(found >= 0 && s.Nodes[found].Disabled == (state == "network_nohq"), "utilities: the Found button works with a headquarters and waits for one without (" + state + ")");
        }
        {
            UtilitiesScreen screen = NewUtilities("network_new", out UtilitiesStub stub);
            bool asked = PressUtilities(screen, UtilitiesFace.HeroAction, 2, out UiScene asking);
            bool question = TextsOf(asking).Contains("Found a broadband company") && stub.Acts.Count == 0;
            bool done = PressUtilities(screen, UtilitiesFace.SheetAnswer, 1, out _);
            Check(asked && question && done && stub.Acts.Count == 1 && stub.Acts[0].StartsWith("Found", StringComparison.Ordinal),
                "utilities: Found asks its question with the licence's cost, and Found the company does it once it is answered");
        }
        {
            UtilitiesScreen screen = NewUtilities("network_new", out UtilitiesStub stub);
            PressUtilities(screen, UtilitiesFace.HeroAction, 2, out _);
            bool back = screen.Back();
            Check(back && stub.Acts.Count == 0 && !TextsOf(LayUtilities(screen)).Contains("Found a broadband company"), "utilities: Back closes a question without doing it");
        }

        UtilitiesButtonChecks();
        UtilitiesIdleChecks();
    }

    // Every button of every state has a handler that does something, and the ones that spend money reach the feature behind them.
    private static void UtilitiesButtonChecks()
    {
        int pressed = 0, silent = 0;
        string quiet = "none";
        foreach (string state in UiSamples.UtilitiesStates)
        {
            UtilitiesScreen probe = NewUtilities(state, out _);
            UiScene s = LayUtilities(probe);
            var targets = new List<int[]>();
            for (int i = 0; i < s.NodeCount; i++)
            {
                ref UiNode n = ref s.Nodes[i];
                if (!n.Disabled && n.Command != UiAppWindow.NavCommand && n.Command != UiChart.First + UiChart.Point && Array.IndexOf(UtilitiesFace.Commands, n.Command) >= 0)
                {
                    targets.Add(new[] { n.Command, n.Arg });
                }
            }
            foreach (int[] t in targets)
            {
                UtilitiesScreen screen = NewUtilities(state, out _);
                LayUtilities(screen);
                pressed++;
                if (!screen.Press(t[0], t[1]))
                {
                    silent++;
                    quiet = state + " " + t[0] + "/" + t[1];
                }
            }
        }
        Check(pressed > 100 && silent == 0, "utilities: every button and row of every state has a handler that does something (" + pressed + " pressed, first silent: " + quiet + ")");

        {
            UtilitiesScreen screen = NewUtilities("accounts", out UtilitiesStub stub);
            PressUtilities(screen, UtilitiesFace.CardAction, 0, out _);
            PressUtilities(screen, UtilitiesFace.CardSwitch, 0, out _);
            Check(stub.Acts.Count == 2 && stub.Acts[0].StartsWith("Pay:0", StringComparison.Ordinal) && stub.Acts[1].StartsWith("Debit:0,1", StringComparison.Ordinal),
                "utilities: Pay now pays the chosen account and the auto debit switch flips it (" + string.Join(" ", stub.Acts) + ")");
        }
        {
            UtilitiesScreen screen = NewUtilities("accounts_phone", out UtilitiesStub stub);
            PressUtilities(screen, UtilitiesFace.AccountsBase + UiDataTable.Row, 8, out _);
            bool apply = PressUtilities(screen, UtilitiesFace.CardAction, 1, out _);
            Check(apply && stub.Acts.Count == 1 && stub.Acts[0].StartsWith("Apply:8", StringComparison.Ordinal), "utilities: with no office to visit the app applies for an account by phone");
        }
        {
            UtilitiesScreen screen = NewUtilities("internet", out UtilitiesStub stub);
            PressUtilities(screen, UtilitiesFace.PlansBase + UiDataTable.Row, 4, out _);
            PressUtilities(screen, UtilitiesFace.BuySwitch, 0, out _);
            PressUtilities(screen, UtilitiesFace.BuyAction, 0, out _);
            PressUtilities(screen, UtilitiesFace.BuyAction, 1, out _);
            Check(stub.Acts.Count == 2 && stub.Acts[0] == "Buy:0,1,1,0" && stub.Acts[1].StartsWith("Cancel:0", StringComparison.Ordinal),
                "utilities: choosing a plan and Change plan buys it at the place (month by month once the contract is off), and Cancel the connection cancels (" + string.Join(" ", stub.Acts) + ")");
        }
        {
            UtilitiesScreen screen = NewUtilities("network", out UtilitiesStub stub);
            PressUtilities(screen, UtilitiesFace.DistrictAct, 6, out UiScene asking);
            bool body = TextsOf(asking).Contains("Build your network in The Hamptons");
            PressUtilities(screen, UtilitiesFace.SheetAnswer, 1, out _);
            PressUtilities(screen, UtilitiesFace.HeroAction, 0, out _);
            PressUtilities(screen, UtilitiesFace.SheetPick, 6, out _);
            PressUtilities(screen, UtilitiesFace.SheetAnswer, 0, out UiScene listed);
            PressUtilities(screen, UtilitiesFace.SheetClose, 0, out _);
            Check(body && stub.Acts.Count == 1 && stub.Acts[0].StartsWith("Dig:6", StringComparison.Ordinal) && TextsOf(listed).Contains("Dig in a district"),
                "utilities: Dig asks first with its cost and months, digs the chosen district once answered, and No goes back to the districts to dig in (" + string.Join(" ", stub.Acts) + ")");
        }
        {
            UtilitiesScreen screen = NewUtilities("network", out UtilitiesStub stub);
            PressUtilities(screen, UtilitiesFace.HeroAction, 1, out _);
            PressUtilities(screen, UtilitiesFace.SheetMore, 0, out _);
            PressUtilities(screen, UtilitiesFace.SheetLess, 1, out _);
            PressUtilities(screen, UtilitiesFace.SheetMore, 2, out _);
            PressUtilities(screen, UtilitiesFace.SheetAction, 0, out UiScene sell);
            bool ask = TextsOf(sell).Contains("Sell your broadband company") && TextsOf(sell).Contains("Harbour Fibre offers $3,480,000");
            PressUtilities(screen, UtilitiesFace.SheetAnswer, 1, out _);
            Check(ask && string.Join(" ", stub.Acts) == "Prices:1050,0,0,0 Maintenance:900,0,0,0 Marketing:3,0,0,0 Sell:0,0,0,0",
                "utilities: set the plans steps the prices, the upkeep and the marketing, and Sell asks with the buyer's offer before it sells (" + string.Join(" ", stub.Acts) + ")");
        }
        {
            UtilitiesScreen screen = NewUtilities("network_extend", out UtilitiesStub stub);
            PressUtilities(screen, UtilitiesFace.DistrictAct, 2, out UiScene asking);
            bool body = TextsOf(asking).Contains("Extend the network in Murray Hill");
            PressUtilities(screen, UtilitiesFace.SheetAnswer, 1, out _);
            Check(body && stub.Acts.Count == 1 && stub.Acts[0].StartsWith("Extend:2", StringComparison.Ordinal), "utilities: Extend asks first and extends the district's network once answered");
        }
    }

    // A screen that has not changed reads nothing and lays nothing: Dirty is a comparison, and the facts are read once a generation.
    private static void UtilitiesIdleChecks()
    {
        UtilitiesScreen screen = NewUtilities("network", out UtilitiesStub stub);
        LayUtilities(screen);
        int reads = stub.Reads;
        bool idle = true;
        for (int i = 0; i < 1000; i++)
        {
            idle &= !screen.Dirty;
        }
        Check(idle && stub.Reads == reads, "utilities: nothing is read or laid out while idle");
        PressUtilities(screen, UtilitiesFace.HeroAction, 1, out _);
        Check(stub.Reads == reads, "utilities: opening a sheet lays the page again without reading the save again");
        stub.Moved++;
        Check(screen.Dirty, "utilities: the screen lays again when the save's utilities change");
        UtilitiesFacts young = UiSamples.UtilitiesFactsOf("network_young");
        UtilitiesFaceModel built = UtilitiesBuild.Make(young, UiSamples.UtilitiesViewOf("network_young"), UiSamples.UtilitiesWordsOf((key, english) => english));
        Check(built.Network.Chart.Points.Length == 3 && built.Network.Chart.Series[0].Values.Length == 3, "utilities: day 5 with little history charts the days there are, no more");
        UtilitiesFaceModel dug = UtilitiesBuild.Make(UiSamples.UtilitiesFactsOf("network_dig"), UiSamples.UtilitiesViewOf("network_dig"), UiSamples.UtilitiesWordsOf((key, english) => english));
        Check(dug.Network.Chart.Points.Length == 0 && !string.IsNullOrEmpty(dug.Network.Chart.EmptyTitle), "utilities: a company with no history says so rather than drawing a flat line");
        UtilitiesFaceModel none = UtilitiesBuild.Make(UiSamples.UtilitiesFactsOf("accounts_new"), UiSamples.UtilitiesViewOf("accounts_new"), UiSamples.UtilitiesWordsOf((key, english) => english));
        Check(none.Accounts.RowCount == 0 && none.Accounts.EmptyTitle.Length > 0 && none.AccountCard == null, "utilities: with no homes or businesses the Accounts tab says so");
    }

    // A source for the money host that fills the cash flow on its first tab alone.
    private sealed class MoneySourceStub : IMoneySource
    {
        public int Generation => 0;

        public void Fill(MoneyModel model)
        {
            model.Title = "Finance";
            model.Nav = new[] { new UiNavEntry { Label = "Cash flow", Icon = UiIcon.Wallet }, new UiNavEntry { Label = "Other", Icon = UiIcon.Wallet } };
            model.Shape = MoneyShape.Flow;
            if (model.Tab == 0)
            {
                model.Statement = new UiStatementModel
                {
                    Account = "Cash flow", Balance = "$1", BalanceLabel = "Cash", PeriodLabel = "30", Columns = new[] { "When", "What", "In", "Out", "Balance" },
                    Groups = new[] { new UiStatementGroup { Label = "Brought forward", Total = "$1", Lines = new UiStatementLine[0] } }
                };
            }
        }

        public bool Press(MoneyModel model, int command, int arg) => false;
    }
}
