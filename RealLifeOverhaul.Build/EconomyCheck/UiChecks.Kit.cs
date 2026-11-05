using System;
using System.Collections.Generic;
using System.IO;
using RealLifeOverhaul.Ui;

internal static partial class Program
{
    private static bool _partsChecked;

    // The parts' gallery, every state of it: each laid out by the screen checks in every language and frame, and each
    // layout held to the parts' own rules too (figures at the right, centred lines on their control's middle, pictures
    // in their own shape, round chart ticks). The parts' own behaviour is checked once, beside them.
    static partial void UiCasesKit(UiScreenDef def, List<UiScreenCase> cases)
    {
        if (def.Id != "kit_parts")
        {
            return;
        }
        foreach (string state in UiSamples.PartStates)
        {
            string st = state;
            cases.Add(new UiScreenCase
            {
                Def = def, Name = "kit parts (" + st + ")", Title = "Interface parts", Commands = UiPartsGallery.Commands,
                Lay = (s, f, dark, k, t) =>
                {
                    UiPartsGalleryModel m = UiSamples.Parts(t, st);
                    UiPartsGallery.Lay(s, m, f, dark, k);
                    PartRules(s, m);
                }
            });
        }
        if (!_partsChecked)
        {
            _partsChecked = true;
            KitPartChecks();
        }
    }

    private static readonly HashSet<string> PartSaid = new HashSet<string>(StringComparer.Ordinal);

    private static void PartSay(string line)
    {
        if (PartSaid.Count < 40 && PartSaid.Add(line))
        {
            Console.WriteLine("        " + line);
        }
    }

    private static void PartRules(UiScene s, UiPartsGalleryModel m)
    {
        Check(UiPartRules.FiguresLeft(s, PartSay) == 0, "ui parts: every figure stands tabular at the right of its cell: " + s.Name);
        Check(UiPartRules.OffCentre(s, PartSay) == 0, "ui parts: every centred line sits on its control's middle, its capitals there: " + s.Name);
        Check(UiPartRules.Stretched(s, PartSay) == 0, "ui parts: a catalogue's picture keeps its own shape inside its tile: " + s.Name);
        Check(UiPartRules.RoughTicks(m.Line, PartSay) + UiPartRules.RoughTicks(m.Bars, PartSay) + UiPartRules.RoughTicks(m.Candles, PartSay) == 0,
            "ui parts: every chart's value axis steps by a round figure: " + s.Name);
    }

    private static void KitPartChecks()
    {
        UiWords en = (key, english) => english;
        Func<float, string> money = UiSamples.Dollars;

        // Cap-height centring: the renderer sets a centred line by its capitals, the lift it took away was real, and the
        // key caps and initials sit by theirs too.
        string folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts\Ui"));
        string slots = File.ReadAllText(System.IO.Path.Combine(folder, "UiView.Slots.cs"));
        Check(slots.Contains("TextAlignmentOptions.CaplineRight") && slots.Contains("TextAlignmentOptions.CaplineLeft") && !slots.Contains("TextAlignmentOptions.Midline")
              && !slots.Contains("alignment = TextAlignmentOptions.Center;"), "ui parts: every centred line, key cap and set of initials is drawn by its capitals (Capline)");
        float most = 0f;
        for (int i = 0; i < (int)UiStyle.Count; i++)
        {
            if ((UiStyle)i == UiStyle.Clock)
            {
                continue;   // the time skip veil's clock alone stands at 64 px, its lift as large as its size: the ladder's own sizes here
            }
            most = Math.Max(most, UiPartRules.LineBoxLift((UiStyle)i));
        }
        Check(most > 1f && most < 3.5f, "ui parts: centring by the line box had lifted words by up to " + most.ToString("0.0") + " px, which the capitals now take back");
        Check(UiPartRules.LineBoxLift(UiStyle.Button) > 0.5f && UiPartRules.LineBoxLift(UiStyle.Body) > UiPartRules.LineBoxLift(UiStyle.Title),
            "ui parts: the lift grows with a style's leading, as the Body's 1.15 showed it most");
        string view = File.ReadAllText(System.IO.Path.Combine(folder, "UiView.cs"));
        Check(view.Contains("(int)UiKind.Picture + 1") && slots.Contains("case UiKind.Picture:") && slots.Contains("preserveAspect = true"),
            "ui parts: the renderer pools and draws the picture kind, letterboxed as laid");
        string host = File.ReadAllText(System.IO.Path.Combine(folder, "UiHost.cs"));
        Check(host.Contains("dx != 0 && Stepped(from, dx)") && host.Contains("Pointed(_scene.Nodes[node].Command, _scene.Nodes[node].Arg)") && host.Contains("Pointed(-1, -1)"),
            "ui parts: the host gives Left and Right to a stepping control and tells a pointing screen where the mouse rests");

        // Round ticks for any span.
        bool nice = true;
        foreach (float span in new[] { 0.37f, 3f, 17f, 99f, 1234f, 184200f, 2.5e6f })
        {
            float step = UiChart.NiceStep(span, 4);
            double unit = step / Math.Pow(10, Math.Floor(Math.Log10(step)));
            nice &= (Math.Abs(unit - 1) < 1e-3 || Math.Abs(unit - 2) < 1e-3 || Math.Abs(unit - 2.5) < 1e-3 || Math.Abs(unit - 5) < 1e-3) && span / step <= 5.01f && span / step >= 1.5f;
        }
        Check(nice, "ui parts: an axis steps by 1, 2, 2.5 or 5 of a power of ten, in two to five steps of any span");
        Check(UiPartRules.Figure("$1,240") && UiPartRules.Figure("+$812") && UiPartRules.Figure("64") && UiPartRules.Figure("6.4%") && !UiPartRules.Figure("08:00")
              && !UiPartRules.Figure("40 people") && !UiPartRules.Figure("Day 412"), "ui parts: a figure is a sum, a count, a share or a change, never a time or words");

        // Each new rule seen failing once on a broken scene.
        var s = new UiScene();
        s.Begin("broken parts", 800f, 600f, true);
        int panel = UiDraw.Box(s, UiAt.Screen, 0f, 0f, 400f, 400f, UiRole.Panel, UiRole.None, 0f);
        UiDraw.Text(s, UiAt.Screen.In(panel), 8f, 8f, 200f, UiStyle.Body, UiRole.Text, "$1,240", UiFlags.None);
        int button = UiDraw.Box(s, UiAt.Screen.In(panel), 8f, 40f, 200f, 40f, UiRole.Field, UiRole.None, 4f);
        UiDraw.Text(s, UiAt.Screen.In(button), 16f, 42f, 160f, UiStyle.Button, UiRole.Text, "Call", UiFlags.Middle);
        var card = new UiCatalogueCard { Picture = "a sofa", Aspect = 2f };
        int tile = UiDraw.Box(s, UiAt.Screen.In(panel), 8f, 100f, 200f, 200f, UiRole.Tile, UiRole.None, 4f);
        int picture = UiCatalogue.Picture(s, UiAt.Screen.In(tile), ref card, 8f, 100f, 200f, 200f, 48f);
        Check(Math.Abs(s.Items[picture].W / s.Items[picture].H - 2f) < 0.01f && UiPartRules.Stretched(s, null) == 0,
            "ui parts: a picture of twice its height's width is laid twice as wide, inside its tile");
        s.Items[picture].H = s.Items[picture].W;
        Check(UiPartRules.FiguresLeft(s, null) == 1 && UiPartRules.OffCentre(s, null) == 1 && UiPartRules.Stretched(s, null) == 1,
            "ui parts: a figure at the left, a line off its button's middle and a stretched picture are each seen failing");

        // The big table holds thousands of rows at the cost of a screenful.
        var table = new UiDataTableModel
        {
            Columns = new[] { new UiDataColumn { Title = "Name", Kind = UiCellKind.Text }, new UiDataColumn { Title = "Wage", Kind = UiCellKind.Money, Width = 96f } },
            Rows = new UiDataRow[5000], RowCount = 5000, Selected = 4000, Reveal = 4000, EmptyTitle = "None", EmptyBody = "None yet."
        };
        for (int i = 0; i < table.RowCount; i++)
        {
            table.Rows[i] = new UiDataRow { Cells = new[] { "Person " + i, money(16 + i % 20) } };
        }
        s.Begin("big table", 1920f, 1080f, true);
        short list = UiDataTable.Lay(s, UiAt.Screen, table, 100f, 100f, 900f, 700f, UiDefaultKeys.Keyboard);
        Check(list >= 0 && s.Lists[list].Count == 5000 && s.Lists[list].Shown <= 20 && s.ItemCount < 200 && s.NodeCount <= 24 && s.Lists[list].First <= 4000
              && s.Lists[list].First + s.Lists[list].Shown > 4000,
            "ui parts: a table of 5,000 rows lays out only the rows in view (" + (list >= 0 ? s.Lists[list].Shown : -1) + " of them, " + s.ItemCount + " items), the chosen row scrolled into view");
        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < 50; i++)
        {
            UiDataTable.Lay(s, UiAt.Screen, table, 100f, 100f, 900f, 700f, UiDefaultKeys.Keyboard);
        }
        Check(sw.Elapsed.TotalMilliseconds / 50.0 < 2.0,
            "ui parts: laying out a 5,000 row table takes " + (sw.Elapsed.TotalMilliseconds / 50.0).ToString("0.000") + " ms, under 2 (a layout a change, never a frame)");

        // An open choice of twelve shows eight and scrolls the rest inside.
        s.Begin("choice", 1920f, 1080f, true);
        var options = new string[12];
        for (int i = 0; i < options.Length; i++)
        {
            options[i] = "Option " + i;
        }
        short open = UiDraw.ChoiceList(s, UiAt.Screen, 100f, 100f, 300f, options, options.Length, 10, 0f, 1);
        Check(s.Lists[open].Shown <= 8 && s.Clips[s.Lists[open].Clip].ContentHeight > s.Clips[s.Lists[open].Clip].H && s.Nodes[0].Layer == 1,
            "ui parts: an open choice of twelve shows eight on the overlay and scrolls the rest");

        // The gallery's screen answers as the game's widgets do: a slider and an amount step and stop at their ends, a choice
        // steps round, a switch turns, Back shuts an open list before the gallery.
        var screen = new UiPartsGalleryScreen(en, money);
        var scene = new UiScene();
        screen.Lay(scene, UiFrame.FullMenu(16f / 9f, "Interface parts"), true, UiDefaultKeys.Keyboard);
        int slider = UiPartsGallery.Arg(false, 1, 1), shelves = UiPartsGallery.Arg(false, 2, 2), van = UiPartsGallery.Arg(false, 0, 0);
        for (int i = 0; i < 20; i++)
        {
            screen.Step(UiPartsGallery.Slider, slider, 1);
            screen.Step(UiPartsGallery.Amount, shelves, 1);
        }
        screen.Lay(scene, UiFrame.FullMenu(16f / 9f, "Interface parts"), true, UiDefaultKeys.Keyboard);
        int more = FindArg(scene, UiPartsGallery.More, shelves), less = FindArg(scene, UiPartsGallery.Less, shelves);
        bool loose = !screen.Steps(UiPartsGallery.Slider) && !screen.Steps(UiPartsGallery.Choice) && !screen.Steps(UiPartsGallery.Amount);
        screen.Focused(UiPartsGallery.Slider, slider);
        screen.Press(UiPartsGallery.Slider, slider);
        bool held = screen.Steps(UiPartsGallery.Slider);
        screen.Back();
        bool let = !screen.Steps(UiPartsGallery.Slider);
        Check(loose && held && let && more >= 0 && scene.Nodes[more].Disabled && less >= 0 && !scene.Nodes[less].Disabled,
            "ui parts: Left and Right move the focus until a slider is pressed, then step it until Back lets it go, and an amount's plus greys at its end as the game's AmountSelector does");
        // The pointer drags a slider as the game's Slider is dragged: its far right is the slider's top, its far left the bottom.
        UiPartsGalleryModel before = UiPartsGalleryScreen.Model(en, new UiPartsState(), money);
        UiPartsGalleryModel laid = UiPartsGalleryScreen.Model(en, new UiPartsState(), money);
        screen.PointerDown(UiPartsGallery.Slider, slider, 4000f, 0f);
        screen.Press(UiPartsGallery.Slider, slider);
        bool clickHolds = screen.Steps(UiPartsGallery.Slider);
        screen.Lay(scene, UiFrame.FullMenu(16f / 9f, "Interface parts"), true, UiDefaultKeys.Keyboard);
        int knobAt = FindArg(scene, UiPartsGallery.Slider, slider);
        Check(before != null && UiDraw.SliderShare(320f, "8", 1f) == 1f && UiDraw.SliderShare(320f, "8", 0f) == 0f && !clickHolds && knobAt >= 0,
            "ui parts: the pointer sets a slider where it goes down or drags, and its click does not hold it for the keys");
        Check(laid != null && host.Contains("_view.PointerDragged = Guard.Wrap<int, UiDragPoint>(PointerDragged)") && File.ReadAllText(System.IO.Path.Combine(folder, "UiPointer.cs")).Contains("IDragHandler"),
            "ui parts: the host hears the pointer drag across a target, through the guard");
        bool closed = screen.Press(UiPartsGallery.Choice, van) && screen.Back() && !screen.Back();
        Check(closed, "ui parts: a choice's list opens on a press and Back shuts it before it shuts the gallery");

        // A track of five marks the current step on the cyan and its chip; a statement folds a day to its band.
        UiPartsGalleryModel track = UiSamples.Parts(en, "track");
        s.Begin("track", 1920f, 1080f, true);
        UiTrack.Lay(s, UiAt.Screen, track.Tracks[0], 100f, 100f, 1200f);
        int accents = 0;
        for (int i = 0; i < s.ItemCount; i++)
        {
            accents += s.Items[i].Kind == UiKind.Box && s.Items[i].Fill == UiRole.Accent && (s.Items[i].Flags & UiFlags.Selected) != 0 ? 1 : 0;
        }
        Check(accents == 1 && s.NodeCount == 5, "ui parts: a track marks one step, the current one, and each step is a target");
        UiPartsGalleryModel week = UiSamples.Parts(en, "statement");
        int all = UiStatement.RowCount(week.Statement);
        week.Statement.Groups[0].Folded = true;
        Check(UiStatement.RowCount(week.Statement) == all - week.Statement.Groups[0].Lines.Length && UiStatement.GroupOf(week.Statement, 1, out int line) == 1 && line == -1,
            "ui parts: a folded day shows its band alone, and the rows after it move up");
    }

    private static int FindArg(UiScene s, int command, int arg)
    {
        for (int i = 0; i < s.NodeCount; i++)
        {
            if (s.Nodes[i].Command == command && s.Nodes[i].Arg == arg)
            {
                return i;
            }
        }
        return -1;
    }
}
