using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using RealLifeOverhaul;
using RealLifeOverhaul.Ui;

// The Construction app on the kit: every state laid out at every frame in every language (UiCases), and the rules wave 3 broke: a tab draws its own page and none of the tab
// before, every action of every state does something and reaches the run it was drawn for, Back steps out of a sub page and then lets the window close, the screen's commands are
// its own block, no page loses a line for want of room, and nothing here quits the game or loads a save.
internal static partial class Program
{
    private static bool _constructionChecked;

    private static readonly int[] ConstructionCommandsAll = BuildConstructionCommands();

    private static int[] BuildConstructionCommands() => ConstructionFace.Commands;

    // The pages the sidebar gives, by name, in their order; the other states are a sub page or a tab in another condition.
    private static readonly string[] ConstructionTabNames = { "overview", "land", "projects", "tenders", "crews", "company", "rivals", "city" };
    private static readonly string[] ConstructionSubStates = { "land_plot", "project_plans", "project_ready", "project_site", "project_quotes", "tender_bid", "crew", "licence" };

    static partial void UiCasesConstruction(UiScreenDef def, List<UiScreenCase> cases)
    {
        if (def.Id == "build_mode")
        {
            UiCasesBuildMode(def, cases);   // Build mode's kit face (UiChecks.BuildMode.cs)
            return;
        }
        if (def.Id != "construction")
        {
            return;
        }
        foreach (string state in UiSamples.ConstructionStates)
        {
            string st = state;
            cases.Add(new UiScreenCase
            {
                Def = def, Name = "construction (" + st + ")", Title = "Construction", Commands = ConstructionCommandsAll,
                Lay = (s, f, dark, k, t) => ConstructionFace.Lay(s, UiSamples.Construction(t, st), f, dark, k)
            });
        }
        if (!_constructionChecked)
        {
            _constructionChecked = true;
            ConstructionScreenChecks();
        }
    }

    // A source that gives a sample page for what the screen has open: the tab's own, or the sub page the state names.
    private sealed class ConstructionStub : IConstructionSource
    {
        private readonly string _state;
        public int Tab, Calls;
        public string Picked;
        public int Fired = -1, FiredCount;   // the run that last fired (its index) and how many fired: a recording source
        public ConstructionModel Last;

        public ConstructionStub(string state)
        {
            _state = state;
            if (Array.IndexOf(ConstructionSubStates, state) >= 0)
            {
                Picked = "sub:" + state;
            }
            else
            {
                Tab = Math.Max(0, Array.IndexOf(ConstructionTabNames, state));
            }
        }

        public int Generation => 0;

        public void Opened()
        {
        }

        public void Show(int tab)
        {
            Tab = tab;
            Picked = null;
        }

        public bool Pick(string key)
        {
            Picked = key;
            return true;
        }

        public bool Back()
        {
            if (Picked == null)
            {
                return false;
            }
            Picked = null;
            return true;
        }

        public void Fill(ConstructionModel m, ConstructionView v)
        {
            Calls++;
            string name = Picked != null ? _state : _state == "off" || _state == "overview_new" || _state == "overview_empty" ? _state : ConstructionTabNames[Math.Max(0, Math.Min(ConstructionTabNames.Length - 1, Tab))];
            ConstructionModel from = UiSamples.Construction((k, e) => e, name);
            foreach (FieldInfo f in typeof(ConstructionModel).GetFields())
            {
                f.SetValue(m, f.GetValue(from));
            }
            if (Picked == null)
            {
                m.Tab = Tab;
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

    private static ConstructionScreen NewConstruction(string state, out ConstructionStub stub)
    {
        stub = new ConstructionStub(state);
        return new ConstructionScreen(stub);
    }

    private static UiScene LayConstruction(ConstructionScreen screen)
    {
        var s = new UiScene();
        screen.Lay(s, UiFrame.FullMenu(16f / 9f, "Construction"), true, UiDefaultKeys.Keyboard);
        return s;
    }

    private static string ConstructionTexts(UiScene s)
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

    private static void ConstructionScreenChecks()
    {
        // Every sidebar entry builds its own page: the model is new for each write, so no part of the tab before stays.
        var seen = new HashSet<string>();
        int tabs = ConstructionTabNames.Length;
        for (int tab = 0; tab < tabs; tab++)
        {
            ConstructionScreen screen = NewConstruction("overview", out _);
            LayConstruction(screen);
            screen.Press(UiAppWindow.NavCommand, tab);
            seen.Add(ConstructionTexts(LayConstruction(screen)));
        }
        Check(seen.Count == tabs, "construction: every section gives a page of its own (" + seen.Count + " different of " + tabs + ")");
        ConstructionScreen walk = NewConstruction("overview", out ConstructionStub walkStub);
        string before = null;
        bool leaked = false;
        for (int tab = 0; tab < tabs; tab++)
        {
            LayConstruction(walk);
            walk.Press(UiAppWindow.NavCommand, tab);
            string now = ConstructionTexts(LayConstruction(walk));
            leaked |= before != null && before == now;
            before = now;
        }
        Check(!leaked && walkStub.Calls >= tabs, "construction: a section walked after another is never the one before it, and each write asks the source again");

        // Every action of every state does something: a target the screen draws enabled is answered, and the run it reaches is the one it was drawn for.
        int pressed = 0, silent = 0, wrong = 0;
        string quiet = "none", crossed = "none";
        var perPage = new Dictionary<string, int>();
        var known = new List<int>(ConstructionCommandsAll);
        foreach (string state in UiSamples.ConstructionStates)
        {
            ConstructionScreen probe = NewConstruction(state, out _);
            UiScene s = LayConstruction(probe);
            var targets = new List<int[]>();
            for (int i = 0; i < s.NodeCount; i++)
            {
                ref UiNode n = ref s.Nodes[i];
                if (!n.Disabled && n.Command != UiAppWindow.NavCommand && n.Command != UiDraw.CloseCommand && known.Contains(n.Command))
                {
                    targets.Add(new[] { n.Command, n.Arg });
                }
            }
            perPage[state] = targets.Count;
            foreach (int[] target in targets)
            {
                ConstructionScreen screen = NewConstruction(state, out ConstructionStub recorder);
                LayConstruction(screen);
                bool did = screen.Press(target[0], target[1]);
                pressed++;
                if (!did)
                {
                    silent++;
                    quiet = state + " " + target[0] + "/" + target[1];
                }
                int want = -1;
                bool runs = true;
                if (target[0] == ConstructionFace.ActionCommand || target[0] == ConstructionFace.StepLess)
                {
                    want = target[1];
                }
                else if (target[0] == ConstructionFace.StepMore)
                {
                    want = target[1] + 1;
                }
                else
                {
                    runs = false;   // a row, a head, a filter or a back press changes the page and runs none
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
        foreach (string tab in ConstructionTabNames)
        {
            if (!perPage.TryGetValue(tab, out int n) || n == 0)
            {
                bare = tab;
            }
        }
        Check(bare == "none", "construction: every section's page was laid and has targets to press (a section with none: " + bare + ")");
        Check(pressed > 80 && silent == 0, "construction: every action of every state does something (" + pressed + " pressed, " + silent + " silent: " + quiet + ")");
        Check(wrong == 0, "construction: every target reaches the run it was drawn for, and a row, head, filter or back press runs none (" + wrong + " crossed: " + crossed + ")");

        // A row of a table opens its page, and a table's head sorts it: a press the screen answers.
        ConstructionScreen rows = NewConstruction("land", out ConstructionStub rowStub);
        LayConstruction(rows);
        bool opened = rows.Press(ConstructionFace.TableBase + UiDataTable.Row, 0) && rowStub.Picked == "lot:a";
        bool sorted = rows.Press(ConstructionFace.TableBase + UiDataTable.Head, 3) && rows.View.Sort == 3 && rows.Press(ConstructionFace.TableBase + UiDataTable.Head, 3) && rows.View.SortDown;
        bool filtered = rows.Press(ConstructionFace.TableBase + UiDataTable.Filter, 0) && rows.View.Filter == 0;
        Check(opened && sorted && filtered, "construction: a row opens its page, a head sorts the table by its column and again the other way, and the filter steps through the kinds of row");

        // Esc and the close button: a sub page steps back to its tab, a tab's own page lets the window close.
        int stuck = 0;
        string stuckAt = "none";
        foreach (string state in UiSamples.ConstructionStates)
        {
            ConstructionScreen screen = NewConstruction(state, out _);
            LayConstruction(screen);
            bool sub = Array.IndexOf(ConstructionSubStates, state) >= 0;
            bool first = screen.Back();
            LayConstruction(screen);
            bool second = screen.Back();
            bool ok = sub ? first && !second : !first && !second;
            if (!ok)
            {
                stuck++;
                stuckAt = state + ": " + first + ", " + second;
            }
        }
        Check(stuck == 0, "construction: in every state Back steps out of a sub page and then the window closes (" + stuckAt + ")");

        // The commands are the screen's own block, held apart from the kit's numbers.
        var own = new[] { ConstructionFace.ActionCommand, ConstructionFace.StepLess, ConstructionFace.StepMore, ConstructionFace.BackCommand };
        bool distinct = new HashSet<int>(own).Count == own.Length;
        foreach (int c in own)
        {
            distinct &= c >= 3000 && c <= 3019 && (c < UiDataTable.First || c > UiDataTable.First + UiDataTable.MassAnswer) && c != UiAppWindow.NavCommand;
        }
        Check(distinct, "construction: each kind of the screen's own action has its own number in 3000 to 3019, none the kit's or the window's");

        // No page loses a line or a card for want of room: every state's cards and rows are drawn whole at the frame the check uses, in every language.
        string lost = "none";
        foreach (string lang in ModLanguages.All)
        {
            UiWords words = UiWords(lang);
            foreach (string state in UiSamples.ConstructionStates)
            {
                ConstructionModel model = UiSamples.Construction(words, state);
                var scene = new UiScene();
                ConstructionFace.Lay(scene, model, UiFrame.FullMenu(16f / 9f, "Construction"), true, UiDefaultKeys.Keyboard);
                if (model.Dropped > 0)
                {
                    lost = lang + " " + state + ": " + model.Dropped;
                }
            }
        }
        Check(lost == "none", "construction: no page loses a line or a card for want of room, in any language (" + lost + ")");

        // A table's rows each carry the key a press opens.
        var table = UiSamples.Construction((k, e) => e, "land");
        Check(table.RowKeys.Length == table.Table.RowCount, "construction: a table's rows each carry the key a press opens");

        // Nothing here quits the game or loads a save.
        string root = ConstructionSourceRoot();
        string danger = "none";
        if (root != null)
        {
            var files = new List<string>(Directory.GetFiles(root, "*.cs"));
            files.AddRange(Directory.GetFiles(System.IO.Path.Combine(root, "..", "..", "Construction", "Live"), "*.cs"));   // the live source, which carries every press to the director
            foreach (string file in files)
            {
                string text = File.ReadAllText(file);
                if (text.Contains("Application.Quit") || text.Contains("LoadGame(") || text.Contains("SaveGameManager.Load"))
                {
                    danger = System.IO.Path.GetFileName(file);
                }
            }
        }
        Check(root != null && danger == "none", "construction: no screen of the app quits the game or loads a save (" + danger + ")");

        // Every way into the app lands on the kit's page: the director opens the app through one method that tells the kit's source the tab and the row (and the page for a project's
        // hedge and quotes), the app is given its kit face, and the kit's source reaches no page of the old frame.
        string constructionRoot = root != null ? System.IO.Path.GetFullPath(System.IO.Path.Combine(root, "..", "..", "Construction")) : null;
        string app = constructionRoot != null ? File.ReadAllText(System.IO.Path.Combine(constructionRoot, "ConstructionDirector.App.cs")) : string.Empty;
        string hedge = constructionRoot != null ? File.ReadAllText(System.IO.Path.Combine(constructionRoot, "ConstructionDirector.Hedge.cs")) : string.Empty;
        string hire = constructionRoot != null ? File.ReadAllText(System.IO.Path.Combine(constructionRoot, "ConstructionDirector.Hire.cs")) : string.Empty;
        Check(app.Contains("ConstructionLive.Instance.Want(tab, pick)") && app.Contains("KitScreen = RealLifeOverhaul.Ui.ConstructionScreen.ScreenId") && app.Contains("UiShell.Face(_app, def)")
            && hedge.Contains("ConstructionLive.Instance.WantPage(project, 2)") && hire.Contains("ConstructionLive.Instance.WantPage(project, 3)"),
            "construction: every entry (a notice, the objectives, the desk, a project's hedge and quotes) opens the app through the director's one method, which tells the kit's source, and the app has its kit face");
        int oldFrame = 0;
        string frameAt = "none";
        if (constructionRoot != null)
        {
            foreach (string file in Directory.GetFiles(System.IO.Path.Combine(constructionRoot, "Live"), "*.cs"))
            {
                string text = File.ReadAllText(file);
                if (text.Contains("AppShell") || text.Contains("InfoBox") || text.Contains("DataTable.On(") || text.Contains("FullMenu"))
                {
                    oldFrame++;
                    frameAt = System.IO.Path.GetFileName(file);
                }
            }
        }
        Check(constructionRoot != null && oldFrame == 0, "construction: the kit's live source opens no page of the old frame (" + frameAt + ")");
    }

    private static string ConstructionSourceRoot()
    {
        string dir = AppContext.BaseDirectory;
        for (int i = 0; i < 10 && dir != null; i++)
        {
            string path = System.IO.Path.Combine(dir, "Assets", "Mods", "RealLifeOverhaul", "Scripts", "Ui", "Construction");
            if (Directory.Exists(path))
            {
                return path;
            }
            dir = System.IO.Path.GetDirectoryName(dir);
        }
        return null;
    }

    // One state at one aspect and in one language, each rule's findings printed: how a layout is worked on without the full run (--construction-debug state aspect lang).
    private static void ConstructionDebug(string state, float aspect, string lang)
    {
        var s = new UiScene();
        ConstructionModel m = UiSamples.Construction(UiWords(lang), state);
        UiFrame frame = UiFrame.FullMenu(aspect, "Construction");
        ConstructionFace.Lay(s, m, frame, true, UiDefaultKeys.Keyboard);
        Action<string> say = line => Console.WriteLine("  " + line);
        Console.WriteLine("dropped " + m.Dropped);
        Console.WriteLine("overlaps"); UiCheckRules.Overlaps(s, say);
        Console.WriteLine("spills"); UiCheckRules.Spills(s, say);
        Console.WriteLine("grid"); UiCheckRules.OffGrid(s, say);
        Console.WriteLine("unfit"); UiCheckRules.Unfit(s, say);
        Console.WriteLine("crowded"); UiCheckRules.Crowded(s, say);
        Console.WriteLine("dead space"); UiCheckRules.DeadSpace(s, say);
        Console.WriteLine("unfilled"); UiCheckRules.Unfilled(s, frame, say);
        Console.WriteLine("stacked"); UiCheckRules.Stacked(s, say);
        Console.WriteLine("empty panels"); UiCheckRules.EmptyPanels(s, say);
        Console.WriteLine("unknown commands"); UiCheckRules.UnknownCommands(s, ConstructionCommandsAll, say);
        Console.WriteLine("repeats"); UiCheckRules.Repeats(s, frame, say);
        Console.WriteLine("contradictions"); UiCheckRules.Contradictions(s, say);
        Console.WriteLine("unreached " + UiFocusRules.Unreached(s, out string missed) + " " + missed);
        if (Environment.GetEnvironmentVariable("RLO_DUMP") != null)
        {
            for (int i = 0; i < s.ItemCount; i++)
            {
                ref UiItem it = ref s.Items[i];
                Console.WriteLine("  item " + i + " " + it.Kind + " [" + it.X + "," + it.Y + " " + it.W + "x" + it.H + "] " + it.Text);
            }
        }
    }
}
