using System;
using System.Collections.Generic;
using System.Text;
using RealLifeOverhaul.Ui;

internal static partial class Program
{
    private static bool _companyFaceChecked;

    // The Company app's layout cases: every state of every tab, laid out in every language and frame (the registry's own checks), and the app's rule checks once.
    static partial void UiCasesCompany(UiScreenDef def, List<UiScreenCase> cases)
    {
        if (def.Id != "company")
        {
            return;
        }
        string only = Environment.GetEnvironmentVariable("RLO_UI_STATE");   // a state's name or its start: the checks of those alone, to iterate on a page
        foreach (string state in UiSamples.CompanyStates)
        {
            string st = state;
            if (only != null && !st.StartsWith(only, StringComparison.Ordinal))
            {
                continue;
            }
            cases.Add(new UiScreenCase
            {
                Def = def, Name = "company (" + st + ")", Title = "Company", Commands = CompanyFace.Commands,
                Lay = (s, f, dark, k, t) => CompanyFace.Lay(s, UiSamples.CompanyFactsOf(t, st), UiSamples.CompanyViewOf(st), UiSamples.CompanyWordsOf(t), new CompanyLayout(), f, dark, k)
            });
        }
        string dump = Environment.GetEnvironmentVariable("RLO_UI_DUMP");   // a state's name: its items with their places, in English at 16:9, to see where a box lands
        if (dump != null && !_companyFaceChecked)
        {
            _companyFaceChecked = true;
            CompanyDump(dump);
            return;
        }
        if (!_companyFaceChecked)
        {
            _companyFaceChecked = true;
            CompanyFaceChecks(only);
        }
    }

    // What a page costs to open: the heaviest pages laid out again and again. A page is laid out once when the facts change or the player acts, and the face does nothing per frame.
    private static void CompanyCost()
    {
        UiWords en = UiWords("en");
        string[] heavy = { "overview_big", "overview_tall", "managers_big", "rota_big", "rota_day", "tax_big", "staff_big", "managers_sub_policy", "managers_sub_dashboard" };
        var scene = new UiScene();
        UiFrame frame = UiFrame.FullMenu(16f / 9f, "Company");
        double worst = 0, total = 0;
        string worstState = string.Empty;
        var watch = System.Diagnostics.Stopwatch.StartNew();
        foreach (string state in heavy)
        {
            CompanyFacts facts = UiSamples.CompanyFactsOf(en, state);
            CompanyView view = UiSamples.CompanyViewOf(state);
            CompanyWords words = UiSamples.CompanyWordsOf(en);
            var layout = new CompanyLayout();
            CompanyFace.Lay(scene, facts, view, words, layout, frame, true, UiDefaultKeys.Keyboard);   // the first, warm
            watch.Restart();
            for (int i = 0; i < 40; i++)
            {
                layout = new CompanyLayout();
                CompanyFace.Lay(scene, facts, view, words, layout, frame, true, UiDefaultKeys.Keyboard);
            }
            watch.Stop();
            double each = watch.Elapsed.TotalMilliseconds / 40.0;
            total += each;
            if (each > worst)
            {
                worst = each;
                worstState = state;
            }
        }
        Console.WriteLine("    company face cost: the heaviest of " + heavy.Length + " pages (" + worstState + ") lays out in " + worst.ToString("0.00") + " ms, the " + heavy.Length + " together in " + total.ToString("0.00") + " ms");
        Check(worst < 5.0, "company face cost: the heaviest of " + heavy.Length + " pages (" + worstState + ") lays out in " + worst.ToString("0.00") + " ms, the " + heavy.Length + " together in " + total.ToString("0.00") + " ms: one layout when the facts change or the player acts, nothing per frame");
    }

    // The managers' tree and its two filters (the places waiting for you, the open posts, one district): what each keeps in the sample and in a company of the cap's size, and that a
    // chip turns through every value and that the page keeps to the targets a layer may hold.
    private static void CompanyFilterChecks()
    {
        UiWords en = UiWords("en");
        CompanyWords words = UiSamples.CompanyWordsOf(en);
        CompanyFacts facts = UiSamples.CompanyFactsOf(en, "managers_big");
        Func<CompanyFacts, int, string[]> ids = (f, filter) =>
        {
            CompanyView v = UiSamples.CompanyViewOf("managers_big");
            v.Filter = filter;
            var lay = new CompanyLayout();
            CompanyFace.Lay(new UiScene(), f, v, words, lay, UiFrame.FullMenu(16f / 9f, "Company"), true, UiDefaultKeys.Keyboard);
            return lay.TreeIds;
        };
        bool Has(string[] list, string id) => Array.IndexOf(list, id) >= 0;
        string[] all = ids(facts, 0), needs = ids(facts, CompanyView.StateNeeds), posts = ids(facts, CompanyView.StatePosts), chelsea = ids(facts, CompanyView.DistrictStep * 2);
        string[] none = ids(facts, CompanyView.StateNeeds + CompanyView.DistrictStep * 2);
        Check(Has(all, "mgr:Speedy Bites~7") && Has(all, "mgr:Bloom & Co~214") && Has(all, "post:Pioneer Game Studio~9") && !Has(all, "crew:dev"),
            "company managers filter: with none set the tree shows every place and a manager's crew starts folded away");
        Check(Has(needs, "mgr:Speedy Bites~7") && !Has(needs, "mgr:Bloom & Co~214") && !Has(needs, "post:Pioneer Game Studio~9") && Has(needs, "own"),
            "company managers filter: waiting for you keeps the places that wait for the owner and the owner");
        Check(Has(posts, "post:Pioneer Game Studio~9") && !Has(posts, "mgr:Speedy Bites~7") && !Has(posts, "exe:1"), "company managers filter: open posts keeps the posts and no one else");
        Check(Has(chelsea, "mgr:Bloom & Co~214") && !Has(chelsea, "mgr:Speedy Bites~7") && !Has(chelsea, "post:Pioneer Game Studio~9"), "company managers filter: a district keeps its places only");
        Check(Has(none, "none") && !Has(none, "mgr:Bloom & Co~214"), "company managers filter: both set to a district with nothing waiting says no place matches");
        // A company at the cap: sixty four places over twelve districts, a third of them posts.
        int most = RealLifeOverhaul.Economy.CompanyCatalogue.ShopsMost;
        CompanyFacts big = UiSamples.CompanyFactsOf(en, "managers_big");
        CompanyManagersFacts m = big.Managers;
        var nodes = new List<UiOrgNode>();
        var rowIds = new List<string>();
        var where = new List<string>();
        var wait = new List<bool>();
        nodes.Add(m.Tree[0]);
        rowIds.Add("cap:owner");
        where.Add(null);
        wait.Add(false);
        nodes.Add(m.Tree[1]);
        rowIds.Add("own");
        where.Add(null);
        wait.Add(false);
        var districts = new List<string>();
        for (int i = 0; i < 12; i++)
        {
            districts.Add("District " + (char)('A' + i));
        }
        for (int i = 0; i < most; i++)
        {
            bool post = i % 3 == 2;
            nodes.Add(new UiOrgNode { Kind = post ? UiOrgKind.Post : UiOrgKind.Person, Level = 1, Name = "Place " + i, Sub = "Shop", HasKids = false });
            rowIds.Add((post ? "post:p" : "mgr:p") + i);
            where.Add(districts[i % 12]);
            wait.Add(!post && i % 4 == 0);
        }
        m.Tree = nodes.ToArray();
        m.TreeIds = rowIds.ToArray();
        m.TreeDistricts = where.ToArray();
        m.TreeNeeds = wait.ToArray();
        m.Districts = districts.ToArray();
        m.Chosen = "own";
        m.Profile = null;
        bool turns = true;
        for (int d = 1; d <= districts.Count; d++)
        {
            string[] shown = ids(big, CompanyView.DistrictStep * d);
            int places = 0;
            foreach (string id in shown)
            {
                places += id.StartsWith("mgr:", StringComparison.Ordinal) || id.StartsWith("post:", StringComparison.Ordinal) ? 1 : 0;
            }
            int should = 0;
            for (int i = 0; i < most; i++)
            {
                should += i % 12 == d - 1 ? 1 : 0;
            }
            turns &= places == should;
        }
        CompanyView view = UiSamples.CompanyViewOf("managers_big");
        var scene = new UiScene();
        CompanyFace.Lay(scene, big, view, words, new CompanyLayout(), UiFrame.FullMenu(16f / 9f, "Company"), true, UiDefaultKeys.Keyboard);
        Check(turns && ids(big, 0).Length >= most, "company managers filter: " + most + " places in " + districts.Count + " districts: each district keeps exactly its own places (" + (turns ? "all" : "not all") + "), none is lost with no filter");
        Check(scene.NodeCount <= UiCheckRules.MostTargets, "company managers filter: the cap of " + most + " places lays out with " + scene.NodeCount + " targets in view, within " + UiCheckRules.MostTargets);
        // The chips turn: the state's three and the district's thirteen are each reached from the last by one press.
        CompanyScreen screen = CompanyScreenOf("managers_big", new CompanyRecorder(), new UiScene());
        int seen = 0;
        for (int i = 0; i < 4; i++)
        {
            screen.Press(CompanyFace.Filter, (screen.View.State + 1) % CompanyView.StateCount + screen.View.District * CompanyView.DistrictStep);
            seen |= 1 << screen.View.State;
        }
        Check(seen == 7 && screen.View.State == (4 % CompanyView.StateCount), "company managers filter: the state chip turns through all three values and round");
    }

    private static void CompanyDump(string state)
    {
        var s = new UiScene();
        UiWords en = UiWords("en");
        CompanyFace.Lay(s, UiSamples.CompanyFactsOf(en, state), UiSamples.CompanyViewOf(state), UiSamples.CompanyWordsOf(en), new CompanyLayout(), UiFrame.FullMenu(16f / 9f, "Company"), true, UiDefaultKeys.Keyboard);
        var byCommand = new SortedDictionary<int, int>();
        for (int i = 0; i < s.NodeCount; i++)
        {
            byCommand[s.Nodes[i].Command] = (byCommand.ContainsKey(s.Nodes[i].Command) ? byCommand[s.Nodes[i].Command] : 0) + 1;
        }
        var counts = new StringBuilder();
        foreach (KeyValuePair<int, int> kv in byCommand)
        {
            counts.Append(kv.Key).Append(':').Append(kv.Value).Append(' ');
        }
        Console.WriteLine("        targets " + s.NodeCount + " by command: " + counts);
        for (int i = 0; i < s.NodeCount; i++)
        {
            UiNode n = s.Nodes[i];
            Console.WriteLine("        node " + i + " cmd " + n.Command + "/" + n.Arg + " [" + n.X + "," + n.Y + " " + n.W + "x" + n.H + "] list " + n.List + " L" + n.Left + " R" + n.Right + " U" + n.Up + " D" + n.Down + (n.Disabled ? " disabled" : ""));
        }
        for (int i = 0; i < s.ItemCount; i++)
        {
            UiItem it = s.Items[i];
            Console.WriteLine("        item " + i + " " + it.Kind + " " + it.Style + " [" + it.X + "," + it.Y + " " + it.W + "x" + it.H + "] parent " + it.Parent + " clip " + it.Clip + " " + (it.Text ?? it.Fill.ToString()));
        }
    }

    // ------------------------------------------------------------------------------------------------- the face's rule checks

    /// <summary>A source that records what reaches it and says it did the action, so that a press that arrives as other than the action drawn is seen.</summary>
    private sealed class CompanyRecorder : ICompanySource
    {
        public CompanyFacts Facts;
        public readonly List<CompanyAct> Done = new List<CompanyAct>();

        public int Generation => 1;

        public bool TakeWanted(out int tab, out string pick, out int sub, out string subPick, out int question)
        {
            tab = -1;
            pick = subPick = null;
            sub = question = 0;
            return false;
        }

        public bool TakeOpened() => false;

        public void Read(CompanyFacts into, CompanyView view)
        {
            into.Ready = Facts.Ready;
            into.OffTitle = Facts.OffTitle;
            into.OffBody = Facts.OffBody;
            into.Name = Facts.Name;
            into.Mark = Facts.Mark;
            into.Counts = Facts.Counts;
            into.Overview = Facts.Overview;
            into.Staff = Facts.Staff;
            into.Managers = Facts.Managers;
            into.Rota = Facts.Rota;
            into.Marketing = Facts.Marketing;
            into.Cover = Facts.Cover;
            into.Tax = Facts.Tax;
            into.Business = Facts.Business;
            into.Question = Facts.Question;
            into.Deep = Facts.Deep;
        }

        public bool Do(CompanyAct act, CompanyView view)
        {
            Done.Add(act);
            return act.Do != CompanyDo.None && act.Do != CompanyDo.Go;   // moving about is the screen's own: an act of that kind reaching a source is a failure the check reads
        }
    }

    private static void CompanyFaceChecks(string only)
    {
        // Every kind of target has a number of its own: none appears twice among the screen's commands, and none is the frame's own.
        int[] commands = CompanyFace.Commands;
        var seen = new HashSet<int>();
        bool distinct = true, clear = true;
        foreach (int c in commands)
        {
            distinct &= seen.Add(c);
            clear &= c != UiDraw.CloseCommand && !UiDraw.IsChrome(c);
        }
        Check(distinct, "company face: every kind of target has a command number of its own, none shared by two kinds");
        Check(clear, "company face: no command number is the frame's own (the close and the app row)");
        Check(CompanyFace.Choice != UiDecision.ChoiceCommand && CompanyFace.Act != UiDecision.ButtonCommand && CompanyFace.Choice != UiAppWindow.NavCommand && CompanyFace.Act != UiAppWindow.NavCommand,
            "company face: the question card's choices and buttons run the Company's own numbers, not the kit card's (1 and 2), which the sidebar's 1 meets");
        bool all = true;
        int pressed = 0;
        var drawn = new HashSet<CompanyDo>();
        foreach (string state in UiSamples.CompanyStates)
        {
            all &= CompanyPresses(state, drawn, ref pressed);
        }
        Check(all, "company face: every target of every state runs the action drawn, as its own kind, and changes only what its kind owns (any failure is said above)");
        Console.WriteLine("    company face: " + pressed + " targets pressed in " + UiSamples.CompanyStates.Length + " states, " + drawn.Count + " kinds of action drawn");
        CompanyCost();
        CompanyFilterChecks();
        // No action without a page that draws it, and none without the live source's handler: an orphan either way is a button nobody can press or a press nothing answers.
        var orphans = new StringBuilder();
        var unhandled = new StringBuilder();
        string folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts\Ui\Company"));
        string live = System.IO.File.ReadAllText(System.IO.Path.Combine(folder, "CompanyLive.Do.cs")) + System.IO.File.ReadAllText(System.IO.Path.Combine(folder, "CompanyLiveDoes.cs"));   // the press map is game-free (CompanyLiveDoes), the live source runs it
        foreach (CompanyDo d in Enum.GetValues(typeof(CompanyDo)))
        {
            if (d == CompanyDo.None)
            {
                continue;
            }
            if (!drawn.Contains(d))
            {
                orphans.Append(orphans.Length > 0 ? ", " : string.Empty).Append(d);
            }
            if (d != CompanyDo.Go && !live.Contains("case CompanyDo." + d + ":"))
            {
                unhandled.Append(unhandled.Length > 0 ? ", " : string.Empty).Append(d);
            }
        }
        Check(orphans.Length == 0, "company face: every kind of action is drawn by some state of the samples (" + (orphans.Length == 0 ? "all" : "none draws " + orphans) + ")");
        Check(unhandled.Length == 0, "company live: the source answers every kind of action the face can draw (" + (unhandled.Length == 0 ? "all" : "unanswered: " + unhandled) + ")");
    }

    // Puts a screen in a state of its samples, with a recording source.
    private static CompanyScreen CompanyScreenOf(string state, CompanyRecorder source, UiScene scene, int choice = 0)
    {
        UiWords en = UiWords("en");
        CompanyWords w = UiSamples.CompanyWordsOf(en);
        source.Facts = UiSamples.CompanyFactsOf(en, state);
        var screen = new CompanyScreen(source, w);
        screen.View.CopyFrom(UiSamples.CompanyViewOf(state));
        screen.View.Choice = choice;
        screen.Lay(scene, UiFrame.FullMenu(16f / 9f, "Company"), true, UiDefaultKeys.Keyboard);
        return screen;
    }

    // What a view holds, as names and values, so that two can be compared field by field.
    private static Dictionary<string, string> CompanyViewFields(CompanyView v)
    {
        var d = new Dictionary<string, string>
        {
            ["Tab"] = v.Tab.ToString(), ["Question"] = v.Question.ToString(), ["Choice"] = v.Choice.ToString(), ["Typing"] = v.Typing ?? "-", ["TypingKind"] = v.TypingKind.ToString(),
            ["Day"] = v.Day.ToString(), ["Amount"] = v.Amount.ToString(), ["Insurer"] = v.Insurer.ToString(), ["Kind"] = v.Kind.ToString(), ["Filter"] = v.Filter.ToString(),
            ["Armed"] = v.Armed + "/" + (v.ArmedId ?? "-"), ["Later"] = v.Later.Count.ToString(), ["Folds"] = string.Join(",", new List<string>(v.FoldedNodes).ToArray())
        };
        for (int i = 0; i < CompanyView.Tabs; i++)
        {
            d["Pick" + i] = v.Pick[i] ?? "-";
            d["Sub" + i] = v.Sub[i].ToString();
            d["SubPick" + i] = v.SubPick[i] ?? "-";
            d["SubArg" + i] = v.SubArg[i].ToString();
        }
        return d;
    }

    private static bool SameAct(CompanyAct a, CompanyAct b, bool typed) =>
        a.Do == b.Do && a.S == b.S && a.A == b.A && a.B == b.B && (typed || a.C == b.C) && (typed || a.S2 == b.S2);

    // Presses every target the state draws, one on a fresh screen, and reads back what it did: the action drawn reaches the source as the kind it was drawn as, moving about
    // never reaches it, and the press changes the view only in the fields its kind owns. Returns whether all held; says what did not.
    private static bool CompanyPresses(string state, HashSet<CompanyDo> drawn, ref int pressed)
    {
        var probe = new CompanyRecorder();
        var scene = new UiScene();
        CompanyScreen shown = CompanyScreenOf(state, probe, scene);
        for (int i = 0; i < shown.Layout.ActCount; i++)
        {
            drawn.Add(shown.Layout.Acts[i].Act.Do);
        }
        var targets = new List<int[]>();
        for (int i = 0; i < scene.NodeCount; i++)
        {
            UiNode n = scene.Nodes[i];
            if (n.Disabled || n.Command == UiDraw.CloseCommand || UiDraw.IsChrome(n.Command))
            {
                continue;
            }
            bool again = false;
            foreach (int[] t in targets)
            {
                again |= t[0] == n.Command && t[1] == n.Arg;
            }
            if (!again)
            {
                targets.Add(new[] { n.Command, n.Arg });
            }
        }
        bool ok = true;
        foreach (int[] t in targets)
        {
            pressed++;
            string why = CompanyPress(state, t[0], t[1]);
            if (why != null)
            {
                ok = false;
                Console.WriteLine("        company (" + state + "): target (" + t[0] + ", " + t[1] + ") " + why);
            }
        }
        // A question's main button answers with the choice chosen: each choice in turn is chosen, and its answer must be the one that reaches the source.
        bool leadShown = shown.View.Question != 0 || (shown.View.Tab == CompanyView.Overview && shown.View.Sub[CompanyView.Overview] == CompanyView.SubMain);
        CompanyQuestion q = !leadShown ? null : shown.View.Question != 0 ? shown.Facts.Question : shown.Facts.Overview.Lead;
        for (int c = 0; q != null && !q.Deep && c < q.Choices.Length; c++)
        {
            if (q.Choices[c].Disabled)
            {
                continue;
            }
            var rec = new CompanyRecorder();
            CompanyScreen screen = CompanyScreenOf(state, rec, new UiScene(), c);
            int main = screen.Layout.Main;
            pressed++;
            if (main < 0 || screen.Layout.Acts[main].Disabled)
            {
                ok = false;
                Console.WriteLine("        company (" + state + "): choice " + c + " has no main button to answer it with");
                continue;
            }
            drawn.Add(screen.Layout.Acts[main].Act.Do);
            bool did = screen.Press(CompanyFace.Act, main);
            if (!did || rec.Done.Count != 1 || !SameAct(rec.Done[0], q.Choices[c].Act, false))
            {
                ok = false;
                Console.WriteLine("        company (" + state + "): choice " + c + " answered with " + (rec.Done.Count == 1 ? rec.Done[0].Do + " (" + rec.Done[0].A + ", " + rec.Done[0].B + ")" : rec.Done.Count + " actions")
                                  + " and not the answer drawn (" + q.Choices[c].Act.Do + ")");
            }
        }
        return ok;
    }

    private static string CompanyPress(string state, int command, int arg)
    {
        var rec = new CompanyRecorder();
        var scene = new UiScene();
        CompanyScreen screen = CompanyScreenOf(state, rec, scene);
        CompanyLayout lay = screen.Layout;
        CompanyView v = screen.View;
        CompanyFacts f = screen.Facts;
        Dictionary<string, string> before = CompanyViewFields(v);
        var acts = new CompanyButton[lay.ActCount];
        Array.Copy(lay.Acts, acts, acts.Length);
        string[] rowIds = (string[])lay.RowIds.Clone();
        string[] treeIds = (string[])lay.TreeIds.Clone();
        int tab = v.Tab;
        string treeId = arg >= 0 && arg < treeIds.Length ? treeIds[arg] : null;
        bool wasFolded = treeId != null && v.FoldedNodes.Contains(treeId);
        CompanyAct drawn = default(CompanyAct);
        bool anAction = false, typedStep = false, arms = false;
        switch (command)
        {
            case CompanyFace.Act:
            case CompanyFace.Item:
            case CompanyFace.Opt:
                if (arg < 0 || arg >= acts.Length)
                {
                    return "names an action (" + arg + ") that the layout did not register";
                }
                drawn = acts[arg].Act;
                arms = acts[arg].Arms;
                anAction = true;
                break;
            case CompanyFace.StepLess:
                drawn = arg >= 0 && arg < acts.Length ? acts[arg].Act : default(CompanyAct);
                anAction = true;
                break;
            case CompanyFace.StepMore:
                drawn = arg + 1 >= 0 && arg + 1 < acts.Length ? acts[arg + 1].Act : default(CompanyAct);
                anAction = true;
                break;
            case CompanyFace.StepType:
                drawn = arg + 2 >= 0 && arg + 2 < acts.Length ? acts[arg + 2].Act : default(CompanyAct);
                typedStep = true;
                break;
        }
        if (anAction)
        {
            if (drawn.Do == CompanyDo.None)
            {
                return "is drawn with no action behind it";
            }
            if (drawn.Do == CompanyDo.Go)
            {
                bool moved = screen.Press(command, arg);
                if (rec.Done.Count != 0)
                {
                    return "moving about (" + drawn.S + ", " + drawn.A + ") reached the source as " + rec.Done[0].Do;
                }
                if (!moved)
                {
                    return "moving about (" + drawn.S + ", " + drawn.A + ") was refused";
                }
                return CompanyGoEffect(v, drawn, before);
            }
            bool pressed = screen.Press(command, arg);
            if (arms)
            {
                if (rec.Done.Count != 0)
                {
                    return "a destructive action (" + drawn.Do + ") was done on the first press, which should only arm it";
                }
                pressed = screen.Press(command, arg);
            }
            if (rec.Done.Count != 1)
            {
                return "the action " + drawn.Do + " reached the source " + rec.Done.Count + " times";
            }
            if (!SameAct(rec.Done[0], drawn, false))
            {
                return "the action drawn was " + drawn.Do + " (" + drawn.S + ", " + drawn.A + ", " + drawn.B + ") and the source got " + rec.Done[0].Do + " (" + rec.Done[0].S + ", " + rec.Done[0].A + ", " + rec.Done[0].B + ")";
            }
            if (!pressed)
            {
                return "the action " + drawn.Do + " was done but the press said it was not";
            }
            return CompanyOnly(before, CompanyViewFields(v), new[] { "Armed" });
        }
        if (typedStep)
        {
            if (drawn.Do == CompanyDo.None)
            {
                return "types into a field with no action behind it";
            }
            if (!screen.Press(command, arg) || rec.Done.Count != 0)
            {
                return "pressing a typed field began no typing, or did an action";
            }
            bool name = drawn.Do == CompanyDo.PlanChange && drawn.B == CompanyTyping.PlanNameField;
            string text = name ? "Weekend" : "37";
            screen.Typed(text);
            screen.Submitted(text);
            if (drawn.Do == CompanyDo.Go)
            {
                // A typed figure that only sets what the page shows (the reserve's amount) never leaves the screen.
                if (rec.Done.Count != 0 || drawn.S != "amount" || v.Amount != 37)
                {
                    return "a typed figure of moving about did not set the figure shown (" + v.Amount + ") or reached the source " + rec.Done.Count + " times";
                }
                return CompanyOnly(before, CompanyViewFields(v), new[] { "Amount", "Typing", "TypingKind" });
            }
            if (rec.Done.Count != 1)
            {
                return "a typed " + (name ? "name" : "figure") + " reached the source " + rec.Done.Count + " times";
            }
            CompanyAct got = rec.Done[0];
            if (!SameAct(got, drawn, true) || (name ? got.S2 != text : got.C != 37))
            {
                return "the typed " + (name ? "name" : "figure") + " arrived as " + got.Do + " (" + got.S2 + ", " + got.C + ") and not in the action drawn (" + drawn.Do + ")";
            }
            return null;
        }
        // Every other kind of target moves about the page or picks something; none reaches the source.
        bool done = screen.Press(command, arg);
        if (rec.Done.Count != 0)
        {
            return "of kind " + CompanyKindName(command) + " reached the source as " + rec.Done[0].Do;
        }
        Dictionary<string, string> after = CompanyViewFields(v);
        switch (command)
        {
            case UiAppWindow.NavCommand:
                if (!done || v.Tab != arg || v.Sub[arg] != 0)
                {
                    return "the sidebar's entry " + arg + " did not open its tab";
                }
                return CompanyOnly(before, after, new[] { "Tab", "Sub" + arg, "SubPick" + arg, "Filter", "Typing", "Armed" });
            case CompanyFace.Row:
            {
                string id = arg >= 0 && arg < rowIds.Length ? rowIds[arg] : null;
                bool inSub = tab == CompanyView.Managers && before["Sub" + tab] != "0";
                if (!done || string.IsNullOrEmpty(id) || (inSub ? v.SubPick[tab] != id : v.Pick[tab] != id))
                {
                    return "the row " + arg + " (" + id + ") was not picked";
                }
                return CompanyOnly(before, after, new[] { inSub ? "SubPick" + tab : "Pick" + tab, "Sub" + tab, "Choice", "Insurer", "Kind" });
            }
            case CompanyFace.Choice:
                if (!done || v.Choice != arg)
                {
                    return "the choice " + arg + " was not chosen";
                }
                return CompanyOnly(before, after, new[] { "Choice" });
            case CompanyFace.Filter:
            {
                bool scope = tab == CompanyView.Managers && before["Sub" + tab] == CompanyView.SubPolicy.ToString();
                if (!done || (scope ? arg >= f.Managers.FilterIds.Length || v.Pick[tab] != f.Managers.FilterIds[arg] : v.Filter != arg))
                {
                    return "the chip " + arg + " did not set " + (scope ? "the scope" : "the filter");
                }
                return CompanyOnly(before, after, scope ? new[] { "Pick" + tab, "SubPick" + tab } : new[] { "Filter" });
            }
            case CompanyFace.Badge:
                if (!done || v.Tab != CompanyView.Overview || v.Sub[CompanyView.Overview] != CompanyView.SubBusiness || v.Pick[CompanyView.Overview] != f.Overview.Businesses[arg].Place)
                {
                    return "the badge " + arg + " did not open its business";
                }
                return CompanyOnly(before, after, new[] { "Tab", "Pick0", "Sub0" });
            case CompanyFace.DayHead:
            case UiBoard.DayCommand:
                if (!done || v.Day != arg)
                {
                    return "the day " + arg + " was not chosen";
                }
                return CompanyOnly(before, after, new[] { "Day" });
            case UiBoard.ShiftCommand:
            case UiBoard.LaneCommand:
                return CompanyOnly(before, after, new string[0]);
            default:
                break;
        }
        if (command == UiOrgTree.First + UiOrgTree.Pick)
        {
            bool folds = treeId == f.Managers.Chosen && IsFoldable(f.Managers, treeId);
            if (!done || (folds ? v.FoldedNodes.Contains(treeId) == wasFolded : v.Pick[CompanyView.Managers] != treeId))
            {
                return "the tree's node " + arg + " (" + treeId + ") was not " + (folds ? "folded or unfolded" : "picked");
            }
            return CompanyOnly(before, after, folds ? new[] { "Folds" } : new[] { "Pick2", "Sub2", "SubPick2" });
        }
        if (command == UiOrgTree.First + UiOrgTree.All)
        {
            return done ? CompanyOnly(before, after, new[] { "Folds" }) : "the tree's fold-all did nothing";
        }
        if (command == UiOrgTree.First + UiOrgTree.Foot)
        {
            if (!done || v.Tab != CompanyView.Managers || v.Sub[CompanyView.Managers] != CompanyView.SubPolicy)
            {
                return "the tree's foot did not open the policy centre";
            }
            return CompanyOnly(before, after, new[] { "Sub2", "SubArg2", "SubPick2", "Filter" });
        }
        return "runs command " + command + ", which the screen answers nowhere";
    }

    private static bool IsFoldable(CompanyManagersFacts m, string id)
    {
        for (int i = 0; i < m.TreeIds.Length && i < m.Tree.Length; i++)
        {
            if (m.TreeIds[i] == id)
            {
                return m.Tree[i].HasKids;
            }
        }
        return false;
    }

    private static string CompanyKindName(int command)
    {
        switch (command)
        {
            case CompanyFace.Row: return "a row";
            case CompanyFace.Choice: return "a choice";
            case CompanyFace.Filter: return "a chip";
            case CompanyFace.Badge: return "a badge";
            case CompanyFace.DayHead: return "a day";
            case UiAppWindow.NavCommand: return "the sidebar";
            default: return "command " + command;
        }
    }

    // The fields that changed must be among those the kind of press owns.
    private static string CompanyOnly(Dictionary<string, string> before, Dictionary<string, string> after, string[] allowed)
    {
        var ok = new HashSet<string>(allowed);
        var wrong = new StringBuilder();
        foreach (KeyValuePair<string, string> kv in before)
        {
            if (after[kv.Key] != kv.Value && !ok.Contains(kv.Key))
            {
                wrong.Append(wrong.Length > 0 ? ", " : string.Empty).Append(kv.Key);
            }
        }
        return wrong.Length == 0 ? null : "changed " + wrong + ", which its kind does not own";
    }

    // What moving about did, as the action drawn says it: the tab, its page, its pick, or one of the screen's own routes.
    private static string CompanyGoEffect(CompanyView v, CompanyAct a, Dictionary<string, string> before)
    {
        Dictionary<string, string> after = CompanyViewFields(v);
        switch (a.S)
        {
            case "later":
                return v.Later.Contains(a.A) ? CompanyOnly(before, after, new[] { "Later", "Question", "Choice" }) : "put nothing off";
            case "card":
                return v.Question == a.A ? CompanyOnly(before, after, new[] { "Question", "Choice" }) : "opened no card";
            case "kind":
                return v.Kind == a.A ? CompanyOnly(before, after, new[] { "Kind" }) : "chose no kind";
            case "insurer":
                return v.Insurer == a.A ? CompanyOnly(before, after, new[] { "Insurer" }) : "chose no insurer";
            case "amount":
                return v.Amount > 0 ? CompanyOnly(before, after, new[] { "Amount" }) : "set no figure";
            default:
                if (a.A < 0 || a.A >= CompanyView.Tabs || v.Tab != a.A || v.Sub[a.A] != a.B || v.SubArg[a.A] != a.C || (a.S != null && v.Pick[a.A] != a.S))
                {
                    return "moving to tab " + a.A + " page " + a.B + " went to tab " + v.Tab + " page " + (a.A >= 0 && a.A < CompanyView.Tabs ? v.Sub[a.A].ToString() : "?");
                }
                return CompanyOnly(before, after, new[] { "Tab", "Pick" + a.A, "Sub" + a.A, "SubArg" + a.A, "SubPick" + a.A, "Question", "Filter" });
        }
    }
}
