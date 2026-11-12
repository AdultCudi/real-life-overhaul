using System;
using System.Collections.Generic;
using System.Reflection;
using RealLifeOverhaul;
using RealLifeOverhaul.Tech;
using RealLifeOverhaul.Ui;

// The Tech app on the kit: every state laid out at every frame in every language (UiCases), and the rules wave 3 broke: a tab draws its own page and none of the tab before,
// every action of every state does what it was drawn for (a recording source fails a press that reaches another run), Back steps out of a sub page, then a firm, then lets the
// window close, every entry point of the game lands on the right tab and row, and the screens' commands are their own block. --tech-ui runs these alone.
internal static partial class Program
{
    private static bool _techChecked;

    static partial void UiCasesTech(UiScreenDef def, List<UiScreenCase> cases)
    {
        if (def.Id != "tech")
        {
            return;
        }
        foreach (string state in UiSamples.TechStates)
        {
            string st = state;
            cases.Add(new UiScreenCase
            {
                Def = def, Name = "tech (" + st + ")", Title = "Tech", Commands = TechFace.Commands,
                Lay = (s, f, dark, k, t) => TechFace.Lay(s, UiSamples.Tech(t, st), f, dark, k)
            });
        }
        if (!_techChecked)
        {
            _techChecked = true;
            TechUiChecks();
        }
    }

    // The sample page of the state the view stands at, with every run recording which one it is.
    private sealed class TechStub : ITechSource
    {
        public int Fired = -1, FiredCount;
        public TechModel Last;
        public string Force;   // a state to write whatever the view says, or null: the view's own

        public int Generation => 0;

        public void Opened(TechView view)
        {
        }

        public void Fill(TechModel m, TechView v)
        {
            TechModel from = UiSamples.Tech((key, english) => english, Force ?? StateOf(v));
            foreach (FieldInfo f in typeof(TechModel).GetFields())
            {
                f.SetValue(m, f.GetValue(from));
            }
            var recording = new TechRun[m.Runs != null ? m.Runs.Length : 0];
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

        // The state a view stands at: a tab's own page, a firm's section, or the sub page a row of the page opens.
        public static string StateOf(TechView v)
        {
            if (v.Pick != null)
            {
                if (v.Pick.StartsWith("prod:", StringComparison.Ordinal)) { return v.Firm != null ? "game" : "product"; }
                if (v.Pick.StartsWith("work:", StringComparison.Ordinal)) { return "tender"; }
                if (v.Pick.StartsWith("dc:", StringComparison.Ordinal)) { return "datacentre"; }
                if (v.Pick.StartsWith("start:", StringComparison.Ordinal)) { return "startup"; }
                if (v.Pick.StartsWith("cap:", StringComparison.Ordinal)) { return v.Page == 3 ? "gtm" : "cap"; }
                if (v.Pick.StartsWith("found:", StringComparison.Ordinal)) { return "found"; }
                if (v.Pick.StartsWith("niches:", StringComparison.Ordinal)) { return "niches"; }
                if (v.Pick.StartsWith("niche:", StringComparison.Ordinal)) { return "niche"; }
                if (v.Pick.StartsWith("newprod:", StringComparison.Ordinal)) { return "concept"; }
                if (v.Pick.StartsWith("rival:", StringComparison.Ordinal)) { return "rivals"; }
                return "product";
            }
            if (v.Firm != null)
            {
                switch (v.Section)
                {
                    case TechSection.Products: return "game";
                    case TechSection.Team: return "team";
                    case TechSection.Rivals: return "rivals";
                    default: return "studio";
                }
            }
            string[] tabs = { "firms", "products", "contracts", "infra", "startups", "funding" };
            return tabs[Math.Max(0, Math.Min(tabs.Length - 1, v.Tab))];
        }
    }

    private static string TechTexts(UiScene s)
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

    private static UiScene LayTech(TechScreen screen)
    {
        var s = new UiScene();
        screen.Lay(s, UiFrame.FullMenu(16f / 9f, "Tech"), true, UiDefaultKeys.Keyboard);
        return s;
    }

    // Every way the model can be pressed: the lead's and the head's buttons by their place in the list, a card's buttons, links and steps by the run they carry. Each is a press the
    // screen must answer with the run it was drawn for.
    private static List<KeyValuePair<KeyValuePair<int, int>, int>> TechPresses(TechModel m)
    {
        var list = new List<KeyValuePair<KeyValuePair<int, int>, int>>();   // ((command, arg), the run it must fire; -1 for a press that only moves the view)
        void Acts(UiActionModel[] acts, bool kitCommand)
        {
            for (int i = 0; acts != null && i < acts.Length; i++)
            {
                if (!acts[i].Disabled)
                {
                    list.Add(new KeyValuePair<KeyValuePair<int, int>, int>(new KeyValuePair<int, int>(kitCommand ? UiAppWindow.ActionCommand : acts[i].Command, kitCommand ? i : acts[i].Arg), acts[i].Arg));
                }
            }
        }
        void Card(UiFactCardModel c)
        {
            Acts(c.Actions, false);
            for (int i = 0; i < c.Lines.Length; i++)
            {
                UiFactLine l = c.Lines[i];
                if (l.Kind == UiFactKind.Link)
                {
                    list.Add(new KeyValuePair<KeyValuePair<int, int>, int>(new KeyValuePair<int, int>(l.Command, l.Arg), l.Arg));
                }
                else if (l.Kind == UiFactKind.Step)
                {
                    if (l.CanLess)
                    {
                        list.Add(new KeyValuePair<KeyValuePair<int, int>, int>(new KeyValuePair<int, int>(l.Command, l.Arg), l.Arg));
                    }
                    if (l.CanMore)
                    {
                        list.Add(new KeyValuePair<KeyValuePair<int, int>, int>(new KeyValuePair<int, int>(l.Command2, l.Arg), l.Arg + 1));
                    }
                }
            }
        }
        Acts(m.Actions, true);
        Acts(m.HeadActions, true);
        for (int i = 0; i < m.Left.Length; i++) { Card(m.Left[i]); }
        for (int i = 0; i < m.Right.Length; i++) { Card(m.Right[i]); }
        if (m.Side != null) { Card(m.Side); }
        if (m.NoteRun >= 0 && m.Shape == TechShape.Overview)
        {
            list.Add(new KeyValuePair<KeyValuePair<int, int>, int>(new KeyValuePair<int, int>(UiAppWindow.NoteCommand, 0), m.NoteRun));
        }
        return list;
    }

    private static void TechUiChecks()
    {
        // Every tab and every firm section builds its own page: the model is new for each write, so no part of the one before stays.
        {
            var seen = new HashSet<string>();
            for (int tab = 0; tab < 6; tab++)
            {
                var stub = new TechStub();
                var screen = new TechScreen(stub);
                LayTech(screen);
                screen.Press(UiAppWindow.NavCommand, tab);
                seen.Add(TechTexts(LayTech(screen)));
            }
            Check(seen.Count == 6, "tech: every tab gives a page of its own (" + seen.Count + " different of 6)");
            var firm = new TechScreen(new TechStub());
            firm.View.Open("firm:a");
            var sections = new HashSet<string>();
            LayTech(firm);
            for (int i = 1; i < firm.Model.Nav.Length; i++)
            {
                firm.Press(UiAppWindow.NavCommand, i);
                sections.Add(TechTexts(LayTech(firm)));
            }
            Check(sections.Count >= 4, "tech: the firm's sections give pages of their own (" + sections.Count + " different of " + (firm.Model.Nav.Length - 1) + ")");
        }
        // Every action of every state: pressed through the screen's own handler, it reaches the run it was drawn for and no other.
        int pressed = 0, wrong = 0;
        foreach (string state in UiSamples.TechStates)
        {
            if (state == "off")
            {
                continue;
            }
            var probe = new TechStub { Force = state };
            var layout = new TechScreen(probe);
            LayTech(layout);
            TechModel model = probe.Last;
            var presses = TechPresses(model);
            Check(presses.Count > 0 || model.Shape == TechShape.Table || model.Shape == TechShape.Game || model.Shape == TechShape.Overview, "tech (" + state + "): the page has something to press");
            foreach (var press in presses)
            {
                var stub = new TechStub { Force = state };
                var screen = new TechScreen(stub);
                LayTech(screen);
                stub.Fired = -1;
                bool done = screen.Press(press.Key.Key, press.Key.Value);
                pressed++;
                if (!done || stub.Fired != press.Value || stub.FiredCount != 1)
                {
                    wrong++;
                    Check(false, "tech (" + state + "): a press of " + press.Key.Key + "/" + press.Key.Value + " fired run " + stub.Fired + " (" + stub.FiredCount + " runs), drawn for " + press.Value);
                }
            }
        }
        Check(pressed >= 40 && wrong == 0, "tech: every action of every state fires the run it was drawn for (" + pressed + " pressed, " + wrong + " wrong)");
        // A command of the kit that shares a number with another part is answered only by its own shape: the window's navigation never fires a run, a row of a table opens a page.
        {
            var stub = new TechStub();
            var screen = new TechScreen(stub);
            LayTech(screen);
            screen.Press(UiAppWindow.NavCommand, 2);
            Check(stub.FiredCount == 0, "tech: a navigation press changes the view and fires no run");
            Check(!screen.Press(TechFace.QuoteCommand, 0), "tech: a review press on a page with none does nothing");
            Check(!screen.Press(UiAppWindow.RowCommand, 0), "tech: the overview's row command does nothing on a table page");
        }
        // The table's rows open the page their key says, and Back steps out: a sub page, then a firm, then the window closes.
        {
            var stub = new TechStub();
            var screen = new TechScreen(stub);
            LayTech(screen);
            Check(screen.Press(TechFace.TableBase + UiDataTable.Row, 0) && screen.View.Firm == "a", "tech: a firm's row opens the firm");
            LayTech(screen);
            screen.View.Open("prod:3");
            LayTech(screen);
            Check(screen.Back() && screen.View.Pick == null && screen.View.Firm == "a", "tech: Back steps from a product to its firm");
            Check(screen.Back() && screen.View.Firm == null, "tech: Back steps from a firm to the firms");
            Check(!screen.Back(), "tech: Back at the firms lets the window close");
            screen.View.Open("tab:5");
            screen.View.Open("cap:a");
            screen.View.Page = 1;
            Check(screen.Back() && screen.View.Page == 0 && screen.View.Pick == "cap:a" && screen.Back() && screen.View.Pick == null, "tech: Back steps out of a raise page and then the cap table");
        }
        // Every entry point of the game lands on the right tab and row: the director's tab number and row id a notice carries.
        {
            var v = new TechView();
            void Lands(int tab, string pick, int kitTab, string firm, string row, int section, string what)
            {
                v.Land(tab, pick);
                Check(v.Tab == kitTab && v.Firm == firm && v.Pick == row && v.Section == section, "tech entry: " + what + " (tab " + v.Tab + ", firm " + v.Firm + ", pick " + v.Pick + ", section " + v.Section + ")");
            }
            Lands(0, null, 0, null, null, TechSection.Overview, "the phone tile and the calendar open the firms");
            Lands(0, "firm:p1", 0, "p1", null, TechSection.Overview, "a firm notice opens the firm");
            Lands(1, "prod:7", 1, null, "prod:7", TechSection.Overview, "an outage or a launch notice opens the product");
            Lands(2, "work:4", 2, null, "work:4", TechSection.Overview, "a contract notice opens the contract");
            Lands(3, "start:2", 4, null, "start:2", TechSection.Overview, "a startup notice opens the startup");
            Lands(4, "cap:p1", 5, null, "cap:p1", TechSection.Overview, "a funding notice opens the cap table");
            Lands(5, "dc:p9", 3, null, "dc:p9", TechSection.Overview, "a data centre notice opens the hall");
            Lands(5, "cloud", 3, null, "cloud", TechSection.Overview, "a cloud notice opens the cloud");
            Lands(5, "fit:p5", 3, null, "fit:p5", TechSection.Overview, "a warehouse offer opens its fit out");
            Lands(0, "rivals:p1", 0, "p1", null, TechSection.Rivals, "a competitors link opens the firm's rivals");
            Lands(0, "rival:p1|c3", 0, "p1", "rival:p1|c3", TechSection.Rivals, "a competitor link opens the competitor");
            Lands(1, "newprod:p1", 0, "p1", "newprod:p1", TechSection.Products, "a new product link opens the firm's concept");
        }
        // The six tabs have a glyph each, the sidebar names them, and the commands are this screen's own block.
        {
            var seen = new HashSet<int>(TechFace.Commands);
            Check(TechFace.ActionCommand >= 8400 && TechFace.QuoteCommand <= 8419, "tech: the screen's own commands stand in the block 8400 to 8419");
            Check(TechGlyphs.Firm.Length == (int)FirmKind.Count && TechGlyphs.Product.Length == (int)ProductKind.Count && TechGlyphs.Work.Length == (int)WorkKind.Count
                  && TechGlyphs.Genre.Length == (int)Genre.Count, "tech: a glyph for every kind of firm, product, work and genre");
            Check(seen.Count == TechFace.Commands.Length, "tech: no two of the screen's commands share a number");
        }
        TechRulesChecks();
    }
}
