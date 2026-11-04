using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using RealLifeOverhaul.Ui;

// Property and the Car Showroom on the kit: every state laid out at every frame in every language (UiCases), and the rules wave 3 broke: a tab draws
// its own page and none of the tab before, every action of every state does something, Back steps out of a sub page and then lets the window close,
// the screens' commands are their own block, and nothing here quits the game or loads a save.
internal static partial class Program
{
    private static bool _estateChecked;

    private static readonly int[] EstateCommandsAll = BuildEstateCommands();

    private static int[] BuildEstateCommands()
    {
        var list = new List<int>(EstateFace.Commands)
        {
            UiCatalogue.First + UiCatalogue.Card, UiCatalogue.First + UiCatalogue.Action, UiChart.First + UiChart.Point, UiInfoView.ViewCommand
        };
        return list.ToArray();
    }

    static partial void UiCasesEstate(UiScreenDef def, List<UiScreenCase> cases)
    {
        if (def.Id == "estate")
        {
            foreach (string state in UiSamples.EstateStates)
            {
                string st = state;
                cases.Add(new UiScreenCase
                {
                    Def = def, Name = "estate (" + st + ")", Title = "Property", Commands = EstateCommandsAll,
                    Lay = (s, f, dark, k, t) => EstateFace.Lay(s, UiSamples.Estate(t, st), f, dark, k)
                });
            }
        }
        else if (def.Id == "dealer")
        {
            foreach (string state in UiSamples.DealerStates)
            {
                string st = state;
                cases.Add(new UiScreenCase
                {
                    Def = def, Name = "dealer (" + st + ")", Title = "Car Showroom", Commands = EstateCommandsAll,
                    Lay = (s, f, dark, k, t) => EstateFace.Lay(s, UiSamples.Dealer(t, st), f, dark, k)
                });
            }
        }
        if (!_estateChecked && (def.Id == "estate" || def.Id == "dealer"))
        {
            _estateChecked = true;
            EstateScreenChecks();
        }
    }

    // A source that gives a sample page for what the screen has open: the tab's own, or the sub page the state names.
    private sealed class EstateStub : IEstateSource
    {
        private readonly bool _dealer;
        private readonly string _state;
        private static readonly string[] EstateTabs = { "roll", "tenants", "works", "market", "agency", "hotel", "hire" };
        private static readonly string[] DealerTabs = { "lots", "stock", "market", "offers", "finance", "growth", "reports" };
        public int Calls;

        public EstateStub(bool dealer, string state)
        {
            _dealer = dealer;
            _state = state;
        }

        public int Generation => 0;

        public void Opened(EstateView view)
        {
        }

        public void Fill(EstateModel m, EstateView v)
        {
            Calls++;
            bool sub = v.Pick != null;
            string[] tabs = _dealer ? DealerTabs : EstateTabs;
            string name = sub ? _state : tabs[Math.Max(0, Math.Min(tabs.Length - 1, v.Tab))];
            EstateModel from = _dealer ? UiSamples.Dealer(Words, name) : UiSamples.Estate(Words, name);
            foreach (FieldInfo f in typeof(EstateModel).GetFields())
            {
                f.SetValue(m, f.GetValue(from));
            }
            if (!sub)
            {
                m.Tab = v.Tab;
            }
        }

        private static string Words(string key, string english) => english;
    }

    private static readonly string[] EstateSubStates =
    {
        "unit_late", "unit_request", "unit_empty", "building", "listing", "works_page", "call", "applicant", "rents", "yard", "used", "auction", "auction_new", "deal", "event"
    };

    private static EstateScreen NewEstate(bool dealer, string state, out EstateStub stub)
    {
        stub = new EstateStub(dealer, state);
        var screen = new EstateScreen(stub, dealer ? "dealer" : "estate");
        if (Array.IndexOf(EstateSubStates, state) >= 0)
        {
            screen.View.Pick = "sub:" + state;
        }
        return screen;
    }

    private static UiScene LayEstate(EstateScreen screen)
    {
        var s = new UiScene();
        screen.Lay(s, UiFrame.FullMenu(16f / 9f, "Property"), true, UiDefaultKeys.Keyboard);
        return s;
    }

    private static string EstateTexts(UiScene s)
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

    private static void EstateScreenChecks()
    {
        // Every sidebar entry or tab builds its own page: the model is new for each write, so no part of the tab before stays.
        foreach (bool dealer in new[] { false, true })
        {
            string who = dealer ? "dealer" : "estate";
            var seen = new HashSet<string>();
            int tabs = 7;
            for (int tab = 0; tab < tabs; tab++)
            {
                EstateScreen screen = NewEstate(dealer, dealer ? "stock" : "roll", out _);
                LayEstate(screen);
                screen.Press(UiAppWindow.NavCommand, tab);
                seen.Add(EstateTexts(LayEstate(screen)));
            }
            Check(seen.Count == tabs, who + ": every tab gives a page of its own (" + seen.Count + " different of " + tabs + ")");
            EstateScreen walk = NewEstate(dealer, dealer ? "stock" : "roll", out EstateStub stub);
            string before = null;
            bool leaked = false;
            for (int tab = 0; tab < tabs; tab++)
            {
                LayEstate(walk);
                walk.Press(UiAppWindow.NavCommand, tab);
                string now = EstateTexts(LayEstate(walk));
                leaked |= before != null && before == now;
                before = now;
            }
            Check(!leaked && stub.Calls >= tabs, who + ": a tab walked after another is never the one before it, and each write asks the source again");
        }

        // Every action of every state does something: a target the screen draws enabled is answered, and one it cannot answer is drawn disabled.
        int pressed = 0, silent = 0;
        string quiet = "none";
        var known = new List<int>(EstateCommandsAll);
        foreach (bool dealer in new[] { false, true })
        {
            foreach (string state in dealer ? UiSamples.DealerStates : UiSamples.EstateStates)
            {
                EstateScreen probe = NewEstate(dealer, state, out _);
                UiScene s = LayEstate(probe);
                var targets = new List<int[]>();
                for (int i = 0; i < s.NodeCount; i++)
                {
                    ref UiNode n = ref s.Nodes[i];
                    if (!n.Disabled && n.Command != UiAppWindow.NavCommand && n.Command != EstateFace.SearchCommand && known.Contains(n.Command) && n.Command != UiChart.First + UiChart.Point && n.Command != UiInfoView.ViewCommand)
                    {
                        targets.Add(new[] { n.Command, n.Arg });
                    }
                }
                foreach (int[] target in targets)
                {
                    EstateScreen screen = NewEstate(dealer, state, out _);
                    LayEstate(screen);
                    bool did = screen.Press(target[0], target[1]);
                    pressed++;
                    if (!did)
                    {
                        silent++;
                        quiet = (dealer ? "dealer " : "estate ") + state + " " + target[0] + "/" + target[1];
                    }
                }
            }
        }
        Check(pressed > 150 && silent == 0, "estate: every action of every state of both apps does something (" + pressed + " pressed, " + silent + " silent: " + quiet + ")");

        // Esc and the close button: a sub page steps back to its tab, a tab's own page lets the window close.
        int stuck = 0;
        string stuckAt = "none";
        foreach (bool dealer in new[] { false, true })
        {
            foreach (string state in dealer ? UiSamples.DealerStates : UiSamples.EstateStates)
            {
                EstateScreen screen = NewEstate(dealer, state, out _);
                LayEstate(screen);
                bool sub = Array.IndexOf(EstateSubStates, state) >= 0;
                bool first = screen.Back();
                LayEstate(screen);
                bool second = screen.Back();
                bool catalogue = state.StartsWith("stock", StringComparison.Ordinal);
                bool ok = sub ? first && !second : !first && !second;
                if (!ok && !catalogue)
                {
                    stuck++;
                    stuckAt = (dealer ? "dealer " : "estate ") + state + ": " + first + ", " + second;
                }
            }
        }
        Check(stuck == 0, "estate: in every state Back steps out of a sub page and then the window closes (" + stuckAt + ")");

        // Back from a sub page lands on its tab's own page, not on the same page again.
        EstateScreen unit = NewEstate(false, "unit_late", out _);
        string subText = EstateTexts(LayEstate(unit));
        unit.Back();
        string tabText = EstateTexts(LayEstate(unit));
        Check(subText != tabText && tabText.Contains("The rent roll"), "estate: Back from a unit's page shows the rent roll, a page of its own");

        // The commands are this screen's own block, and no other part answers them.
        Check(EstateFace.ActionCommand >= 3000 && EstateFace.BackCommand <= 3019 && EstateFace.StepLess > EstateFace.ActionCommand, "estate: the screen's own commands stand in the block 3000 to 3019");
        var model = UiSamples.Estate((k, e) => e, "roll");
        bool rows = model.RowKeys.Length == model.Table.RowCount;
        Check(rows, "estate: a table's rows each carry the key a press opens");

        // Nothing here quits the game or loads a save, and nothing types without the kit's field.
        string root = EstateSourceRoot();
        string danger = "none";
        if (root != null)
        {
            foreach (string file in Directory.GetFiles(root, "*.cs"))
            {
                string text = File.ReadAllText(file);
                if (text.Contains("Application.Quit") || text.Contains("LoadGame(") || text.Contains("SaveGameManager.Load"))
                {
                    danger = System.IO.Path.GetFileName(file);
                }
            }
        }
        Check(danger == "none", "estate: no screen of the two apps quits the game or loads a save (" + danger + ")");
        Check(root != null, "estate: the screens' source folder was found for the quit guard");
    }

    private static string EstateSourceRoot()
    {
        string dir = AppContext.BaseDirectory;
        for (int i = 0; i < 10 && dir != null; i++)
        {
            string path = System.IO.Path.Combine(dir, "Assets", "Mods", "RealLifeOverhaul", "Scripts", "Ui", "Estate");
            if (Directory.Exists(path))
            {
                return path;
            }
            dir = System.IO.Path.GetDirectoryName(dir);
        }
        return null;
    }
}

internal static partial class Program
{
    // For the maker: the rules of the layout, said line by line for one state at one frame (dotnet EconomyCheck.dll --estate-debug estate:roll 1.78).
    private static void EstateDebug(string which, float aspect, string lang)
    {
        string[] parts = which.Split(':');
        bool dealer = parts[0] == "dealer";
        var s = new UiScene();
        UiWords t = UiWords(lang);
        EstateModel m = dealer ? UiSamples.Dealer(t, parts[1]) : UiSamples.Estate(t, parts[1]);
        UiFrame frame = UiFrame.FullMenu(aspect, dealer ? "Car Showroom" : "Property");
        EstateFace.Lay(s, m, frame, true, UiDefaultKeys.Keyboard);
        Action<string> say = line => Console.WriteLine("  " + line);
        Console.WriteLine("overlaps"); UiCheckRules.Overlaps(s, say);
        Console.WriteLine("spills"); UiCheckRules.Spills(s, say);
        Console.WriteLine("grid"); UiCheckRules.OffGrid(s, say);
        Console.WriteLine("unfit"); UiCheckRules.Unfit(s, say);
        Console.WriteLine("small targets"); UiCheckRules.SmallTargets(s, say);
        Console.WriteLine("crowded"); UiCheckRules.Crowded(s, say);
        Console.WriteLine("dead space"); UiCheckRules.DeadSpace(s, say);
        Console.WriteLine("unfilled"); UiCheckRules.Unfilled(s, frame, say);
        Console.WriteLine("key clash"); UiCheckRules.KeyClash(s, say);
        Console.WriteLine("stacked"); UiCheckRules.Stacked(s, say);
        Console.WriteLine("empty panels"); UiCheckRules.EmptyPanels(s, say);
        Console.WriteLine("strays"); UiCheckRules.Strays(s, say);
        Console.WriteLine("spines"); UiCheckRules.Spines(s, say);
        Console.WriteLine("type levels"); UiCheckRules.TypeLevels(s, say);
        Console.WriteLine("unknown commands"); UiCheckRules.UnknownCommands(s, EstateCommandsAll, say);
        Console.WriteLine("low contrast"); UiCheckRules.LowContrast(s, say);
        Console.WriteLine("repeats"); UiCheckRules.Repeats(s, frame, say);
        Console.WriteLine("contradictions"); UiCheckRules.Contradictions(s, say);
        string missed;
        Console.WriteLine("unreached " + UiFocusRules.Unreached(s, out missed) + " " + missed);
    }
}
