using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using RealLifeOverhaul;
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
        private readonly int _pinnedTab;   // the tab a variant state (roll_twenty, stock_full, agency_closed) stands on, or -1: there the state's own sample is laid
        public int Calls;
        public int Fired = -1, FiredCount;   // the run that last fired (its index) and how many fired: a recording source
        public EstateModel Last;

        public EstateStub(bool dealer, string state)
        {
            _dealer = dealer;
            _state = state;
            _pinnedTab = EstateVariantTab(dealer, state);
        }

        public int Generation => 0;

        public void Opened(EstateView view)
        {
        }

        public void Fill(EstateModel m, EstateView v)
        {
            Calls++;
            bool sub = v.Pick != null;
            string[] tabs = _dealer ? DealerTabNames : EstateTabNames;
            string name = sub || (_pinnedTab >= 0 && v.Tab == _pinnedTab) ? _state : tabs[Math.Max(0, Math.Min(tabs.Length - 1, v.Tab))];
            EstateModel from = _dealer ? UiSamples.Dealer(Words, name) : UiSamples.Estate(Words, name);
            foreach (FieldInfo f in typeof(EstateModel).GetFields())
            {
                f.SetValue(m, f.GetValue(from));
            }
            if (!sub)
            {
                m.Tab = v.Tab;
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

        private static string Words(string key, string english) => english;
    }

    private static readonly string[] EstateSubStates =
    {
        "unit_late", "unit_request", "unit_empty", "building", "listing", "works_page", "call", "applicant", "rents", "rents_new", "yard", "used", "auction", "auction_new", "deal", "event"
    };

    // The pages the sidebar or the tabs give, by name, in their order.
    private static readonly string[] EstateTabNames = { "roll", "tenants", "works", "market", "agency", "hotel", "hire" };
    private static readonly string[] DealerTabNames = { "lots", "stock", "market", "offers", "finance", "growth", "reports" };

    // The states that are a tab's page in another condition (twenty units, an empty lot, a closed agency): the tab they stand on, or -1 for a state that is not one.
    private static int EstateVariantTab(bool dealer, string state)
    {
        if (dealer)
        {
            return state == "stock_empty" || state == "stock_full" ? 1 : state == "reports_new" ? 6 : state == "off" ? 0 : -1;
        }
        return state == "roll_twenty" || state == "roll_first" || state == "roll_empty" || state == "off" ? 0 : state == "agency_closed" ? 4 : -1;
    }

    // A state's own tab: the page it lays, so the check presses each tab page as the player meets it (a tab state opens on its tab; a sub page opens from the first).
    private static int EstateTabOf(bool dealer, string state)
    {
        int at = Array.IndexOf(dealer ? DealerTabNames : EstateTabNames, state);
        return at >= 0 ? at : Math.Max(0, EstateVariantTab(dealer, state));
    }

    private static EstateScreen NewEstate(bool dealer, string state, out EstateStub stub)
    {
        stub = new EstateStub(dealer, state);
        var screen = new EstateScreen(stub, dealer ? "dealer" : "estate");
        if (Array.IndexOf(EstateSubStates, state) >= 0)
        {
            screen.View.Pick = "sub:" + state;
        }
        else
        {
            screen.View.Tab = EstateTabOf(dealer, state);
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
        int pressed = 0, silent = 0, wrong = 0, catalogueActions = 0, rowActions = 0;
        string quiet = "none", crossed = "none";
        var perPage = new Dictionary<string, int>();
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
                perPage[(dealer ? "dealer " : "estate ") + state] = targets.Count;
                foreach (int[] target in targets)
                {
                    EstateScreen screen = NewEstate(dealer, state, out EstateStub recorder);
                    LayEstate(screen);
                    bool did = screen.Press(target[0], target[1]);
                    pressed++;
                    if (!did)
                    {
                        silent++;
                        quiet = (dealer ? "dealer " : "estate ") + state + " " + target[0] + "/" + target[1];
                    }
                    // The run reached is the run drawn for: a button, a link, a switch or a step's minus runs its own index, a plus the next one,
                    // the catalogue's detail action the run its list names, and a row, a card or a back press runs none.
                    int want = -1;
                    bool runs = true;
                    if (target[0] == EstateFace.ActionCommand || target[0] == EstateFace.StepLess)
                    {
                        want = target[1];
                    }
                    else if (target[0] == EstateFace.StepMore)
                    {
                        want = target[1] + 1;
                    }
                    else if (target[0] == UiCatalogue.First + UiCatalogue.Action && recorder.Last != null && target[1] >= 0 && target[1] < recorder.Last.CatalogueRuns.Length)
                    {
                        want = recorder.Last.CatalogueRuns[target[1]];
                        catalogueActions++;
                    }
                    else if (target[0] == EstateFace.TableBase + UiDataTable.Act && recorder.Last != null && target[1] >= 0 && target[1] < recorder.Last.RowRuns.Length)
                    {
                        want = recorder.Last.RowRuns[target[1]];   // a table row's own button (Locate) runs the run its row names
                        rowActions++;
                    }
                    else
                    {
                        runs = false;
                    }
                    bool right = runs ? recorder.Fired == want && recorder.FiredCount == 1 : recorder.FiredCount == 0;
                    if (!right)
                    {
                        wrong++;
                        crossed = (dealer ? "dealer " : "estate ") + state + " " + target[0] + "/" + target[1] + " fired " + recorder.Fired + " x" + recorder.FiredCount + " not " + want;
                    }
                }
            }
        }
        // Every tab page of both apps was laid and pressed (rule 13): the market's Locate buttons and the showroom's detail actions among them.
        string bare = "none";
        foreach (bool dealer in new[] { false, true })
        {
            foreach (string tab in dealer ? DealerTabNames : EstateTabNames)
            {
                if (!perPage.TryGetValue((dealer ? "dealer " : "estate ") + tab, out int n) || n == 0)
                {
                    bare = (dealer ? "dealer " : "estate ") + tab;
                }
            }
        }
        Check(bare == "none" && rowActions >= 8 && catalogueActions >= 3,
            "estate: every tab page of both apps was laid and its actions pressed through the recording source (" + rowActions + " table buttons, " + catalogueActions + " detail actions; a tab with none: " + bare + ")");
        Check(pressed > 150 && silent == 0, "estate: every action of every state of both apps does something (" + pressed + " pressed, " + silent + " silent: " + quiet + ")");
        Check(wrong == 0, "estate: every target reaches the run it was drawn for, and a row, card or back press runs none (" + wrong + " crossed: " + crossed + ")");

        // The numbers: the screen's own block holds each kind apart, the kit's pieces keep theirs, and the info view's button number is the window's section number.
        var own = new[] { EstateFace.ActionCommand, EstateFace.StepLess, EstateFace.StepMore, EstateFace.BackCommand, EstateFace.SearchCommand };
        bool distinct = new HashSet<int>(own).Count == own.Length;
        foreach (int c in own)
        {
            distinct &= c >= 3000 && c <= 3019 && (c < UiDataTable.First || c > UiDataTable.First + UiDataTable.MassAnswer) && c != UiCatalogue.First && c != UiCatalogue.First + UiCatalogue.Action && c != UiAppWindow.NavCommand;
        }
        Check(distinct, "estate: each kind of the screen's own action has its own number in 3000 to 3019, none the kit's or the window's");
        var rents = NewEstate(false, "rents", out EstateStub rentsStub);
        UiScene rentScene = LayEstate(rents);
        int sectionNodes = 0, viewNodes = 0;
        for (int i = 0; i < rentScene.NodeCount; i++)
        {
            sectionNodes += rentScene.Nodes[i].Command == UiAppWindow.NavCommand && rentScene.Nodes[i].Layer == 0 ? 1 : 0;
            viewNodes += rentScene.Nodes[i].Command == UiInfoView.ViewCommand ? 1 : 0;
        }
        // The view's icons choose the kind of rent (flats, shops, offices): number 1 there is the view's own, and the only nodes of that number on the rents view are the view's own icons.
        Check(rents.Press(UiInfoView.ViewCommand, 1) && rents.Press(UiAppWindow.NavCommand, 2) && !rents.Press(UiAppWindow.NavCommand, 9) && sectionNodes == viewNodes && viewNodes == 3 && rentsStub.FiredCount == 0,
            "estate: on the rents view the number 1 is the view's own (it chooses the kind of rent) and never a change of the window's section (" + viewNodes + " view buttons)");

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

        // The class a car wears is one game-free rule, set by the live pages as well as the previews: the glyph by body and the colour by class.
        Check(CarClass.Work(CarKind.Van) && CarClass.Work(CarKind.Truck) && !CarClass.Work(CarKind.Utility) && !CarClass.Work(CarKind.Sports)
            && CarClass.Glyph(CarKind.Truck) == UiIcon.Truck && CarClass.Glyph(CarKind.Economy) == UiIcon.Car
            && CarClass.Tint(CarKind.Economy, 90000) == UiRole.Chart4 && CarClass.Tint(CarKind.Saloon, 5000) == UiRole.Chart5 && CarClass.Tint(CarKind.Utility, 5000) == UiRole.Chart5
            && CarClass.Tint(CarKind.Sports, 5000) == UiRole.Chart7 && CarClass.Tint(CarKind.Luxury, 5000) == UiRole.Chart7
            && CarClass.Tint(CarKind.Van, 90000) == UiRole.CatIndustry && CarClass.Tint(CarKind.Truck, 100) == UiRole.CatIndustry
            && CarClass.Tint(CarKind.Untouched, CarClass.Cheap - 1) == UiRole.Chart4 && CarClass.Tint(CarKind.Untouched, CarClass.Cheap) == UiRole.Chart5
            && CarClass.Tint(CarKind.Untouched, CarClass.Middle - 1) == UiRole.Chart5 && CarClass.Tint(CarKind.Untouched, CarClass.Middle) == UiRole.Chart7,
            "estate: the class rule colours a car by the kind the game's vehicle table gives it (work vehicles in the industry colour; amber, blue and violet by class, by price band where the table has none)");
        // The real vehicle types go through the same table: a hatchback, a saloon, a supercar, a van and a box truck.
        Check(CarCatalogue.For("ba:vehicletype_honzamimic", new CarTraits(true, false, 60, 8, false, 1200f)).Kind == CarKind.Economy
            && CarCatalogue.For("ba:vehicletype_bima320", new CarTraits(true, false, 70, 8, false, 1200f)).Kind == CarKind.Saloon
            && CarCatalogue.For("ba:vehicletype_anselmoaf90", new CarTraits(true, false, 100, 4, true, 1200f)).Kind == CarKind.Sports
            && CarCatalogue.For("ba:vehicletype_mersaididash", new CarTraits(true, false, 60, 20, false, 1200f)).Kind == CarKind.Van
            && CarCatalogue.For("ba:vehicletype_deliverytruck", new CarTraits(true, false, 50, 40, false, 1200f)).Kind == CarKind.Truck,
            "estate: the game's own vehicle types fall into the classes the showroom colours by");
        int tinted = 0, hand = 0;
        if (root != null)
        {
            foreach (string file in Directory.GetFiles(root, "DealerLive*.cs"))
            {
                string text = File.ReadAllText(file);
                tinted += System.Text.RegularExpressions.Regex.Matches(text, @"ClassTint\(").Count;
                hand += System.Text.RegularExpressions.Regex.Matches(text, @"Contains\(""(truck|pickup|van)""\)").Count;
            }
        }
        Check(tinted >= 6 && hand == 0, "estate: the showroom's live pages (stock, deliveries, rivals, like-it, other lots) set the class colour from the game's vehicle class through CarClass and keep no rule of their own (" + tinted + " set, " + hand + " by hand)");

        // No English in the pt and zh shots: the words that stood as English literals in the samples (a colour, a fault, a kind of building, a job, "Day 211") come from the locale.
        string[] leaks = { "White", "Black", "Heating service", "the heating", "boiler", "Nurse", "Cook", "Teacher", "Accountant", "Courier, ", "Designer", "bakery", "tailor", "import firm", "Homes", "Shops", "Offices",
            "Day ", "Quote asked", "Looking for" };
        string englishLeak = "none";
        foreach (string lang in new[] { "pt", "zh-cn" })
        {
            UiWords words = UiWords(lang);
            foreach (bool dealerApp in new[] { false, true })
            {
                foreach (string state in dealerApp ? UiSamples.DealerStates : UiSamples.EstateStates)
                {
                    EstateModel pageModel = dealerApp ? UiSamples.Dealer(words, state) : UiSamples.Estate(words, state);
                    var scene = new UiScene();
                    EstateFace.Lay(scene, pageModel, UiFrame.FullMenu(16f / 9f, "Property"), true, UiDefaultKeys.Keyboard);
                    foreach (string said in EstateTextLines(scene))
                    {
                        foreach (string leak in leaks)
                        {
                            if (said.IndexOf(leak, StringComparison.Ordinal) >= 0 && !(lang == "pt" && leak == "Designer"))   // pt writes the job the same way
                            {
                                englishLeak = lang + " " + (dealerApp ? "dealer " : "estate ") + state + ": " + said;
                            }
                        }
                    }
                }
            }
        }
        Check(englishLeak == "none", "estate: no English literal reaches the pt and zh pages (" + englishLeak + ")");

        // A decision is a decision (the approved desk session's card): no title strip with a close, no back button over it, its answers each a target of the action
        // kind, and the keys start on a safe answer, never on the way out; the cards that are pages of one thing end in the way out at their foot and start on it.
        foreach (string[] one in new[] { new[] { "estate", "call" }, new[] { "estate", "applicant" }, new[] { "dealer", "deal" } })
        {
            bool dealerApp = one[0] == "dealer";
            UiScene card = LayEstate(NewEstate(dealerApp, one[1], out EstateStub cardStub));
            int start = UiFocusRules.First(card);
            int answers = 0;
            int leave = -1;
            for (int i = 0; i < card.NodeCount; i++)
            {
                answers += card.Nodes[i].Command == EstateFace.ActionCommand && !card.Nodes[i].Disabled ? 1 : 0;
                leave = card.Nodes[i].Command == EstateFace.BackCommand ? i : leave;
            }
            Check(card.FindNode(UiDraw.CloseCommand, 0) < 0 && start >= 0 && start != leave && card.Nodes[start].Command == EstateFace.ActionCommand && answers >= 2 && leave >= 0,
                "estate: the " + one[1] + " decision has no title strip or back button, " + answers + " answers, and focus starts on an answer, not the way out");
        }
        foreach (string[] one in new[] { new[] { "estate", "listing" }, new[] { "estate", "works_page" }, new[] { "dealer", "used" }, new[] { "dealer", "auction" }, new[] { "dealer", "auction_new" } })
        {
            UiScene card = LayEstate(NewEstate(one[0] == "dealer", one[1], out EstateStub cardStub));
            int start = UiFocusRules.First(card);
            Check(card.FindNode(UiDraw.CloseCommand, 0) < 0 && start >= 0 && card.Nodes[start].Key == UiAction.Alt && !card.Nodes[start].Disabled && start != LowestNodeIndex(card),
                "estate: the " + one[1] + " card has no title strip or back button at its top, and the keys start on its main action, not the way out (focus " + start + " of " + card.NodeCount + ", y "
                + (start >= 0 ? card.Nodes[start].Y.ToString() : "-") + ")");
        }

        // The pad reaches a table row's own button: the row's target links to it and it links back.
        UiScene market = LayEstate(NewEstate(false, "market", out EstateStub marketStub));
        int rowsLinked = 0, rowsActing = 0;
        for (int i = 0; i < market.NodeCount; i++)
        {
            if (market.Nodes[i].Command == EstateFace.TableBase + UiDataTable.Row)
            {
                rowsActing++;
                int to = market.Nodes[i].Right;
                rowsLinked += to >= 0 && to < market.NodeCount && market.Nodes[to].Command == EstateFace.TableBase + UiDataTable.Act && market.Nodes[to].Arg == market.Nodes[i].Arg
                    && market.Nodes[to].Left == i ? 1 : 0;
            }
        }
        Check(rowsActing > 5 && rowsLinked == rowsActing, "estate: the pad goes from a table row to its own button and back (" + rowsLinked + " of " + rowsActing + " rows linked)");
    }

    private static List<string> EstateTextLines(UiScene s)
    {
        var list = new List<string>(64);
        for (int i = 0; i < s.ItemCount; i++)
        {
            if (s.Items[i].Kind == UiKind.Text && !string.IsNullOrEmpty(s.Items[i].Text))
            {
                list.Add(s.Items[i].Text);
            }
        }
        return list;
    }

    // The lowest target of a scene: the way out at a card's foot.
    private static int LowestNodeIndex(UiScene s)
    {
        int low = 0;
        for (int i = 0; i < s.NodeCount; i++)
        {
            low = s.Nodes[i].Y > s.Nodes[low].Y ? i : low;
        }
        return low;
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
