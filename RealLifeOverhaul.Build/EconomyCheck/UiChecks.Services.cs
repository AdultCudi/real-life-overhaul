using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using RealLifeOverhaul;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Ui;

// The Services app on the kit: every state laid out at every frame in every language (UiCases), and the rules wave 3 broke: a tab draws its own page and none
// of the tab before, every action of every state does something and reaches the run it was drawn for, Back steps out of a picked row and then lets the window
// close, the screen's commands are its own block, nothing here quits the game or loads a save, and a sale keeps the rules it had (SaleRules and the director's
// question): proved here from the rules and from the director's own text, since the game is not there to ask.
internal static partial class Program
{
    private static readonly int[] ServicesCommandsAll = BuildServicesCommands();

    private static int[] BuildServicesCommands() => new List<int>(ServicesFace.Commands).ToArray();

    static partial void UiCasesServices(UiScreenDef def, List<UiScreenCase> cases)
    {
        if (def.Id != "services")
        {
            return;
        }
        foreach (string state in UiSamples.ServicesStates)
        {
            string st = state;
            cases.Add(new UiScreenCase
            {
                Def = def, Name = "services (" + st + ")", Title = "Services", Commands = ServicesCommandsAll,
                Lay = (s, f, dark, k, t) => ServicesFace.Lay(s, UiSamples.Services(t, st), f, dark, k)
            });
        }
    }

    private static readonly string[] ServicesTabNames = { "cleaning", "crew", "contracts", "sell" };
    private static readonly string[] ServicesPickStates = { "cleaning_client", "cleaning_add", "crew_person", "contracts_offer", "contracts_running" };

    // A source that gives the sample page for what the screen has open: the tab's own, or the state a picked row stands for.
    private sealed class ServicesStub : IServicesSource
    {
        private readonly string _state;
        public int Calls;
        public int Fired = -1, FiredCount;   // the run that last fired (its index) and how many fired: a recording source
        public ServicesModel Last;

        public ServicesStub(string state)
        {
            _state = state;
        }

        public int Generation => 0;

        public void Opened(ServicesView view)
        {
        }

        public void Fill(ServicesModel m, ServicesView v)
        {
            Calls++;
            bool variant = Array.IndexOf(ServicesPickStates, _state) < 0 && UiSamples.ServicesTabOf(_state) == v.Tab;   // a tab's page in another condition (empty, refused, rivals): laid while that tab shows
            string name = v.Pick != null || variant ? _state : ServicesTabNames[Math.Max(0, Math.Min(3, v.Tab))];
            ServicesModel from = UiSamples.Services((k, e) => e, name);
            foreach (FieldInfo f in typeof(ServicesModel).GetFields())
            {
                f.SetValue(m, f.GetValue(from));
            }
            m.Tab = v.Tab;
            if (m.Sell != null)
            {
                m.Sell.PickerOpen = v.PickerOpen && m.Sell.Businesses.Length > 1;
            }
            // Every run records which one it is: a press that reaches another run than the one the target was drawn for is seen.
            var recording = new Func<bool>[m.Runs != null ? m.Runs.Length : 0];
            for (int i = 0; i < recording.Length; i++)
            {
                int k = i;
                recording[i] = () =>
                {
                    Fired = k;
                    FiredCount++;
                    return true;
                };
            }
            m.Runs = recording;
            Last = m;
        }
    }

    private static ServicesScreen NewServices(string state, out ServicesStub stub)
    {
        stub = new ServicesStub(state);
        var screen = new ServicesScreen(stub);
        screen.View.Tab = UiSamples.ServicesTabOf(state);
        if (Array.IndexOf(ServicesPickStates, state) >= 0)
        {
            screen.View.Pick = "sub:" + state;
        }
        else if (state == "sell_picker")
        {
            screen.View.PickerOpen = true;
        }
        else if (state == "sell_rivals")
        {
            screen.View.SellView = 1;
        }
        return screen;
    }

    private static UiScene LayServices(ServicesScreen screen)
    {
        var s = new UiScene();
        screen.Lay(s, UiFrame.FullMenu(16f / 9f, "Services"), true, UiDefaultKeys.Keyboard);
        return s;
    }

    private static string ServicesTexts(UiScene s)
    {
        var text = new System.Text.StringBuilder(2048);
        for (int i = 0; i < s.ItemCount; i++)
        {
            if (s.Items[i].Kind == UiKind.Text && !string.IsNullOrEmpty(s.Items[i].Text))
            {
                text.Append(s.Items[i].Text).Append('\n');
            }
        }
        return text.ToString();
    }

    private static string ServicesSourceRoot()
    {
        string dir = AppContext.BaseDirectory;
        for (int i = 0; i < 10 && dir != null; i++)
        {
            string path = System.IO.Path.Combine(dir, "Assets", "Mods", "RealLifeOverhaul", "Scripts", "Ui", "Services");
            if (Directory.Exists(path))
            {
                return path;
            }
            dir = System.IO.Path.GetDirectoryName(dir);
        }
        return null;
    }

    private static string ServicesEconomyRoot()
    {
        string ui = ServicesSourceRoot();
        return ui != null ? System.IO.Path.GetFullPath(System.IO.Path.Combine(ui, "..", "..", "Economy", "Services")) : null;
    }

    private static string ServicesRead(string root, string file) => root != null && File.Exists(System.IO.Path.Combine(root, file)) ? File.ReadAllText(System.IO.Path.Combine(root, file)) : string.Empty;

    /// <summary>The Services screen's own checks, alone: <c>--services-ui</c>.</summary>
    private static void ServicesUiChecks()
    {
        // Every tab builds its own page: the model is new for each write, so no part of the tab before stays.
        var seen = new HashSet<string>();
        for (int tab = 0; tab < 4; tab++)
        {
            ServicesScreen screen = NewServices("cleaning", out _);
            LayServices(screen);
            screen.Press(UiAppWindow.NavCommand, tab);
            seen.Add(ServicesTexts(LayServices(screen)));
        }
        Check(seen.Count == 4, "services: every tab gives a page of its own (" + seen.Count + " different of 4)");
        ServicesScreen walk = NewServices("cleaning", out ServicesStub walked);
        string before = null;
        bool leaked = false;
        for (int tab = 0; tab < 4; tab++)
        {
            LayServices(walk);
            walk.Press(UiAppWindow.NavCommand, tab);
            string now = ServicesTexts(LayServices(walk));
            leaked |= before != null && before == now;
            before = now;
        }
        Check(!leaked && walked.Calls >= 4, "services: a tab walked after another is never the one before it, and each write asks the source again");

        // Every action of every state does something: a target the screen draws enabled is answered, and the run it reaches is the one it was drawn for.
        int pressed = 0, silent = 0, wrong = 0, rowPresses = 0;
        string quiet = "none", crossed = "none";
        var perState = new Dictionary<string, int>();
        foreach (string state in UiSamples.ServicesStates)
        {
            ServicesScreen probe = NewServices(state, out _);
            UiScene s = LayServices(probe);
            var targets = new List<int[]>();
            for (int i = 0; i < s.NodeCount; i++)
            {
                ref UiNode n = ref s.Nodes[i];
                if (!n.Disabled && n.Command != UiAppWindow.NavCommand && n.Command != UiDraw.CloseCommand && Array.IndexOf(ServicesCommandsAll, n.Command) >= 0 && !(n.Command >= UiDataTable.First && n.Command < UiDataTable.First + UiDataTable.Row)
                    && n.Command != UiDataTable.First + UiDataTable.Act)
                {
                    targets.Add(new[] { n.Command, n.Arg });
                }
            }
            perState[state] = targets.Count;
            foreach (int[] target in targets)
            {
                ServicesScreen screen = NewServices(state, out ServicesStub recorder);
                LayServices(screen);
                bool did = screen.Press(target[0], target[1]);
                pressed++;
                if (!did)
                {
                    silent++;
                    quiet = state + " " + target[0] + "/" + target[1];
                }
                int want = -1;
                bool runs = true;
                if (target[0] == ServicesFace.ActionCommand || target[0] == ServicesFace.StepLess)
                {
                    want = target[1];
                }
                else if (target[0] == ServicesFace.StepMore)
                {
                    want = target[1] + 1;
                }
                else
                {
                    runs = false;   // a row, a chip, a view, the picker, a price bar or a back press changes the view and runs none
                    rowPresses += target[0] == UiDataTable.First + UiDataTable.Row ? 1 : 0;
                }
                bool right = runs ? recorder.Fired == want && recorder.FiredCount == 1 : recorder.FiredCount == 0;
                if (!right)
                {
                    wrong++;
                    crossed = state + " " + target[0] + "/" + target[1] + " fired " + recorder.Fired + " x" + recorder.FiredCount + " not " + want;
                }
            }
        }
        string bare = "none";
        foreach (string tab in ServicesTabNames)
        {
            if (!perState.TryGetValue(tab, out int n) || n == 0)
            {
                bare = tab;
            }
        }
        Check(bare == "none" && pressed > 60, "services: every tab page was laid and its targets pressed through the recording source (" + pressed + " pressed; a tab with none: " + bare + ")");
        Check(silent == 0, "services: every enabled target of every state does something (" + silent + " silent: " + quiet + ")");
        Check(wrong == 0, "services: every target reaches the run it was drawn for, and a chip, a view, the picker and a back press run none (" + wrong + " crossed: " + crossed + ")");

        // The table's row presses pick the row's key, and the pick shows in the next write; a chip and a view change what shows; the picker chooses the business.
        ServicesScreen pick = NewServices("sell", out _);
        LayServices(pick);
        bool picked = pick.Press(ServicesFace.TableBase + UiDataTable.Row, 2) && pick.View.Pick == "bid:3";
        bool viewed = pick.Press(ServicesFace.ViewCommand, 1) && pick.View.SellView == 1 && pick.View.Pick == null;
        LayServices(pick);
        bool opened = pick.Press(ServicesFace.PickerCommand, 0) && pick.View.PickerOpen;
        LayServices(pick);
        bool chose = pick.Press(ServicesFace.OptionCommand, 1) && pick.View.Business == "b" && !pick.View.PickerOpen && pick.View.Step < 0;
        LayServices(pick);
        bool step = pick.Press(UiChart.First + UiChart.Point, 3) && pick.View.Step == 3;
        ServicesScreen chip = NewServices("contracts", out _);
        LayServices(chip);
        bool chipped = chip.Press(ServicesFace.ChipCommand, 2) && chip.View.Filter == 2 && !chip.Press(ServicesFace.ChipCommand, 9);
        Check(picked && viewed && opened && chose && step && chipped, "services: a row picks its key, a view, a business, a price bar and a chip each change the view (" + picked + viewed + opened + chose + step + chipped + ")");

        // Esc and the close button: a picked row steps back to its tab, an open picker closes, and a tab's own page lets the window close.
        int stuck = 0;
        string stuckAt = "none";
        foreach (string state in UiSamples.ServicesStates)
        {
            ServicesScreen screen = NewServices(state, out _);
            LayServices(screen);
            bool sub = Array.IndexOf(ServicesPickStates, state) >= 0 || state == "sell_picker";
            bool first = screen.Back();
            LayServices(screen);
            bool second = screen.Back();
            if (sub ? !(first && !second) : (first || second))
            {
                stuck++;
                stuckAt = state + ": " + first + ", " + second;
            }
        }
        Check(stuck == 0, "services: in every state Back steps out of a picked row or a picker and then the window closes (" + stuckAt + ")");
        ServicesScreen leave = NewServices("contracts_offer", out _);
        string offerText = ServicesTexts(LayServices(leave));
        leave.Back();
        string tabText = ServicesTexts(LayServices(leave));
        Check(offerText != tabText && tabText.Contains("Contracts"), "services: Back from an offer shows the contracts table, a page of its own");

        // The numbers: the screen's own block holds each kind apart from the kit's pieces and the window's section number.
        var own = new[] { ServicesFace.ActionCommand, ServicesFace.StepLess, ServicesFace.StepMore, ServicesFace.BackCommand, ServicesFace.ChipCommand, ServicesFace.PickerCommand, ServicesFace.OptionCommand,
            ServicesFace.ViewCommand };
        bool distinct = new HashSet<int>(own).Count == own.Length;
        foreach (int c in own)
        {
            distinct &= c >= 6700 && c <= 6719 && (c < UiDataTable.First || c > UiDataTable.First + UiDataTable.MassAnswer) && c != UiChart.First && c != UiAppWindow.NavCommand;
        }
        Check(distinct, "services: each kind of the screen's own action has its own number in 6700 to 6719, none the kit's or the window's");

        // Nothing here quits the game, loads a save or opens the old window or a game box.
        string root = ServicesSourceRoot();
        string danger = "none", literal = "none";
        string[] sampleWords = { "Huang", "Kowalski", "Mbeki", "Hudson Data", "Bloom and Co", "Murray Hill", "Petrova", "Harbourside", "Corner Kitchen" };
        if (root != null)
        {
            foreach (string file in Directory.GetFiles(root, "*.cs"))
            {
                string text = File.ReadAllText(file);
                if (text.Contains("Application.Quit") || text.Contains("LoadGame(") || text.Contains("SaveGameManager.Load") || text.Contains("AppShell") || text.Contains("InfoBox"))
                {
                    danger = System.IO.Path.GetFileName(file);
                }
                if (System.IO.Path.GetFileName(file).StartsWith("ServicesLive", StringComparison.Ordinal))
                {
                    foreach (string word in sampleWords)
                    {
                        if (text.Contains(word))
                        {
                            literal = System.IO.Path.GetFileName(file) + ": " + word;
                        }
                    }
                }
            }
        }
        Check(root != null && danger == "none", "services: no file of the kit face quits the game, loads a save or opens the old window (" + danger + ")");
        Check(literal == "none", "services: the live source holds no sample name or figure (" + literal + ")");

        // Every entry point lands on the kit: the app's definition gives the kit face, a notice, a button of another app and the Calendar all name a tab and a row.
        string eco = ServicesEconomyRoot();
        string director = ServicesRead(eco, "ServicesDirector.cs");
        string sale = ServicesRead(eco, "ServicesDirector.Sale.cs");
        string company = ServicesRead(System.IO.Path.GetFullPath(System.IO.Path.Combine(eco ?? ".", "..", "Company")), "CompanyGame.Kit.cs");
        Check(director.Contains("Kit = () => new RealLifeOverhaul.Ui.ServicesScreen(") && director.Contains("UiShell.Face(_app, def)") && director.Contains("RealLifeOverhaul.Ui.ServicesLive.Want(tab, pick)")
              && director.Contains("public static void OpenCleaning()") && director.Contains("Instance?.Open(TabCleaning, null)") && director.Contains("Instance?.Open(TabSale,"),
            "services: the app is drawn by the kit, and a notice, the Company page's buttons and every other opening name a tab and a row for it");
        Check(company.Contains("CanSell(string place) => ServicesDirector.CanSellPlace(place, out _)") && company.Contains("public static bool SaleOffered(string place, out string why)"),
            "services: the Company page asks the director about that place, and shows a refused sale off with the reason");

        // The sale keeps the rules it had. The game's question runs first and the sale completes only on its answer; the refusals are the director's own.
        int confirmAt = sale.IndexOf("GameConfirm.Show(\"reallife_svc_confirm_sell_title\"", StringComparison.Ordinal);
        int sellAt = sale.IndexOf("Sell(place, price, parts, keepsStaff, clauseDays, buyer, buyerName, rival)", StringComparison.Ordinal);
        Check(confirmAt > 0 && sellAt > confirmAt && sale.Contains("MaySell(reg, price, parts, out SaleMoney m)") && sale.Contains("if (!Ready(false))"),
            "services: a sale asks the game's question first and completes on its answer, and is checked again then");
        Check(sale.Contains("Settle(price, parts, b, ServicesGame.Cash)") && sale.Contains("ServicesBridges.PayInvestors") && sale.Contains("ServicesBridges.SettleLoans")
              && sale.Contains("SaleRules.Record(_state, name, price, m.Gain, day)") && sale.Contains("SaleRules.Clause(_state") && sale.Contains("ServicesGame.Complete(reg, keepsStaff, rival, out int deposit, out string log)"),
            "services: investors and loans are paid out of the price, the gain is recorded for the tax return, the clause is set and the business ends as the game ends one");
        Check(director.Contains("GoingPublic.InSession") && director.Contains("reallife_ui_svc_going_public"), "services: selling is off while a Going Public session runs, and the page says why");
        bool allKeys = true;
        for (int block = 1; block <= 7; block++)
        {
            allKeys &= ServicesDirectorBlockKey(block) != null;
        }
        Check(allKeys, "services: every refusal the game's state can give (not a business, inside, vehicle, gift, busy, stock, lot) has its words");

        ServicesSaleRulesChecks();
    }

    private static string ServicesDirectorBlockKey(int block)
    {
        string eco = ServicesEconomyRoot();
        string sale = ServicesRead(eco, "ServicesDirector.Sale.cs");
        string[] keys = { null, "reallife_svc_refuse_not_business", "reallife_svc_refuse_inside", "reallife_svc_refuse_vehicle", "reallife_svc_refuse_gift", "reallife_svc_refuse_busy", "reallife_svc_refuse_stock",
            "reallife_svc_refuse_lot" };
        return block > 0 && block < keys.Length && sale.Contains("\"" + keys[block] + "\"") ? keys[block] : null;
    }

    // The rules a sale keeps, run from the rules themselves: what the buyer pays and what the seller gets, parts a week apart, the gain for the tax return and a clause charged once.
    private static void ServicesSaleRulesChecks()
    {
        var state = new ServicesState { Seed = 7 };
        var b = new SaleBreakdown { Value = 1000000, Investors = 120000, Loans = 90000, Deposit = 8000, Fixtures = 300000 };
        SaleMoney once = SaleRules.Settle(1100000, 1, b, 500000);
        Check(once.Now == 1100000 && once.Investors == 120000 && once.Loans == 90000 && once.Left == 1100000 - 120000 - 90000 && once.Short == 0 && once.Gain == 1100000 - 300000,
            "services rules: investors and secured loans are paid out of the price, and the gain is the price above the stock and furniture");
        SaleMoney parts = SaleRules.Settle(1100000, 4, b, 500000);
        Check(parts.PartsLeft == 3 && parts.Now + parts.Part * parts.PartsLeft >= 1100000 - 3 && parts.Now + parts.Part * parts.PartsLeft <= 1100000 + 3,
            "services rules: a price in parts is paid on the day and then each week, and the parts make the price");
        SaleMoney short1 = SaleRules.Settle(100000, 1, b, 20000);
        Check(short1.Short > 0, "services rules: a sale that would leave investors or a lender unpaid is short, so it is refused");
        SaleDue due = SaleRules.Due(state, "Shop", 2, parts, 100);
        int paid1 = due != null ? SaleRules.TakeDue(due, due.NextDay - 1) : -1, paid2 = due != null ? SaleRules.TakeDue(due, due.NextDay) : -1;
        Check(due != null && paid1 == 0 && paid2 > 0 && due.NextDay == 100 + ServicesCatalogue.InstalmentDays * 2, "services rules: a part falls due every " + ServicesCatalogue.InstalmentDays + " days and not before");
        SaleRules.Record(state, "Shop", 1100000, once.Gain, 120);
        Check(SaleRules.Gains(state, 100, 130) == once.Gain && SaleRules.Gains(state, 121, 130) == 0, "services rules: the gain goes into the quarter that holds the day of the sale");
        SaleClause clause = SaleRules.Clause(state, "ba:businesstype_cafe", "Midtown", "Shop", 1100000, 30, 120, false);
        Check(clause != null && SaleRules.Breaks(clause, "ba:businesstype_cafe", "Midtown", 125) && !SaleRules.Breaks(clause, "ba:businesstype_cafe", "Uptown", 125) && SaleRules.ClausePenalty(1100000) == 165000,
            "services rules: a business of the kind sold, in that district, inside the days, breaks the promise and is charged a share of the price");
        Check(SaleRules.ExpireClauses(state, 120 + 31) == 1 && state.Clauses.Count == 0, "services rules: a clause that has run out goes");
        SaleBreakdown valued = SaleRules.Value(new SaleInputs { AvgProfit7 = 5000f, DaysOnRecord = 14, Sector = 0, Income = 100, Phase = 0, Fixtures = 200000, Deposit = 4000, Investors = 0, Loans = 0 });
        Check(valued.Value > 200000 && valued.Value == valued.Goodwill + valued.Fixtures && Math.Abs(SaleRules.Asking(valued.Value, ServicesCatalogue.AskingDefault) - valued.Value) <= 10,
            "services rules: the worth is the goodwill and the fixtures, and the asking price at a hundred per cent is the worth");
        Check(SaleRules.Quick(1000000) == 700000, "services rules: a quick sale pays seven tenths of the value");
    }
}
