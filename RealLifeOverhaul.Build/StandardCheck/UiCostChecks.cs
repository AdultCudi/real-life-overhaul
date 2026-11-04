using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

// docs/standards/performance.md, RLO-PERF-025: the RLO interface's cost is its own and named, the game's work it hands
// on is the game's, and it builds again only what changed. The owner's log of 2 Nov 2026 put a click of the interface at
// 217.7 ms of the mod's own scripts (Contacts' View on map): the game's own map opening run inside the click, billed to the
// mod, and the screen laid out again for a full menu already closing; and every layout wrote each box's slice scale,
// which the engine's setter takes as a new mesh whatever the value. Each fact is listed with the words that show it holds
// and the words that would show it went back; every press of a game button under Scripts/Ui is a hand-off to the game.
internal static partial class Program
{
    private const string UiCostRule = "RLO-PERF-025";

    // File under Scripts, the method (null: the whole file), words it must hold (all), words it must not hold (any), and why.
    private static readonly (string File, string Method, string[] Must, string[] Not, string Why)[] UiCostFacts =
    {
        ("Ui/UiHost.cs", "PressNode", new[] { "Measured(UiWork.Press)", "if (_screen.Dirty && !Gone())" }, new[] { "if (_screen.Dirty)\n" },
            "a press that handed its screen away (View on map closing the full menu) is not laid out again for nobody"),
        ("Ui/UiHost.cs", "OnUpdate", new[] { "if (Leaving != null && Leaving())" }, new string[0],
            "a screen on its way out reads and lays out nothing in the frame before its owner hides it"),
        ("Ui/UiHost.cs", "Update", new[] { "Measured(UiWork.Frame)" }, new string[0], "the screen's frame is counted by its name"),
        ("Ui/UiHost.cs", "Layout", new[] { "Measured(UiWork.Layout)" }, new string[0], "each layout is counted by the screen's name"),
        ("Ui/UiHost.cs", "Show", new[] { "HotSpots.Time(HotShow)", "Measured(UiWork.Open)" }, new string[0],
            "the open, which the game's own enabling of its screen runs inside a press, is the mod's work and named"),
        ("Ui/UiHost.cs", "Hide", new[] { "HotSpots.Time(HotHide)", "_cost?.Hidden(" }, new string[0], "the screen's line is written as it closes"),
        ("Ui/UiHost.cs", "PointerPressed", new[] { "Measured(UiWork.Press)" }, new string[0], "a click is counted as one press of the screen"),
        ("Ui/Host/UiGameHost.cs", "Install", new[] { "_host.Measure(_screen.Name", "_host.Leaving = Closing" }, new string[0],
            "a screen over the game's is named for its line, and an app of the full menu knows when the menu is closing"),
        ("Ui/Host/UiGameHost.cs", "Watch", new[] { "UiWork.Frame" }, new string[0], "the watch of the game's screen is the screen's own work"),
        ("Ui/Host/UiWidgets.cs", "Hand", new[] { "using (HotSpots.Game())", "b.onClick.Invoke()" }, new string[0],
            "a button of the game's pressed for the interface runs its listeners as the game's own work"),
        ("Ui/Host/UiWidgets.cs", "Press", new[] { "Hand(b)" }, new[] { "onClick.Invoke" }, "the kit's press of a game button is a hand-off"),
        ("Ui/Host/UiWidgets.cs", "Set", new[] { "HotSpots.Game()" }, new string[0], "a toggle's listeners are the game's work"),
        ("Ui/Host/UiWidgets.cs", "Pick", new[] { "HotSpots.Game()" }, new string[0], "a drop-down's listeners are the game's work"),
        ("Ui/Host/UiWidgets.cs", "Type", new[] { "HotSpots.Game()" }, new string[0], "a field's listeners are the game's work"),
        ("Ui/Host/UiWidgets.cs", "Slide", new[] { "HotSpots.Game()" }, new string[0], "a slider's listeners are the game's work"),
        ("Ui/Phone/PhoneItemsGame.cs", "Press", new[] { "UiWidgets.Hand(button)" }, new string[0], "an app opened from the phone is the game's work"),
        ("Ui/Phone/PhoneKit.cs", "Shown", new[] { "HotSpots.Time(HotShown)" }, new string[0],
            "the phone shown by the game (the full menu closing inside a press) is the mod's work and named"),
        ("Ui/Phone/PhoneKit.cs", "Taken", new[] { "NamedSoon(" }, new[] { "Named(phone" },
            "the once-a-second look for another mod's screen on the phone reads the names only when the phone's objects changed"),
        ("Ui/Phone/PhoneKit.cs", "Shape", new[] { "GetInstanceID()" }, new[] { ".name" }, "the walk that decides whether the names are read again reads none"),
        ("Ui/Shell/UiShell.cs", "CloseMenu", new[] { "HotSpots.Game()" }, new string[0], "the game's own close of the full menu is the game's work"),
        ("Ui/UiView.Slots.cs", "SliceScale", new[] { "if (image.pixelsPerUnitMultiplier != want)" }, new string[0],
            "the engine builds a picture's mesh again on every write of its slice scale, so it is written only when it changes"),
        ("Ui/UiView.Slots.cs", "BindBox", new[] { "SliceScale(b.Fill", "SliceScale(b.Edge" }, new[] { "pixelsPerUnitMultiplier =" },
            "a layout writes a box's slice scale only when it changes: written each time, every box of the screen was built again"),
        ("Ui/UiView.Slots.cs", "Bind", new string[0], new[] { "pixelsPerUnitMultiplier =", "localEulerAngles =" },
            "an avatar's, a busy row's and a slice's pieces are written only when they change"),
        ("Ui/UiView.Slots.cs", "BindKeyCap", new[] { "SliceScale(k.Face", "SliceScale(k.Edge" }, new[] { "pixelsPerUnitMultiplier =" },
            "a key cap's slice scale is written only when it changes"),
        ("Ui/UiView.cs", "Write", new[] { "if (_root.sizeDelta != rootSize)", "if (c.Content.Rect.sizeDelta != room)" }, new string[0],
            "the root's and the scroll regions' boxes are written only when they change"),
        ("Diagnostics/SessionWatch.Lines.cs", "OwnScripts", new[] { "UiCost.MinuteLine(_line, seconds)" }, new string[0],
            "the minute's line carries the interface's share of every frame"),
        ("Diagnostics/SessionWatch.cs", "Long", new[] { "HotSpots.FrameGameMs(frame)" }, new string[0],
            "a long frame's line says the game's own work the mod handed to it, apart from the mod's"),
        // Flare's feed (the owner, 3 Nov: the kit's Flare complete; its cost measured in EconomyCheck's FlareCompleteCost): the
        // account is read again only when it changed, a scroll lays the rows already filled, and only the rows in view are laid.
        ("Ui/Flare/FlareScreen.cs", "Lay", new[] { "if (fill)" }, new string[0], "Flare reads its account again only when something changed, not on every layout"),
        ("Ui/Flare/FlareScreen.cs", "Wheel", new[] { "_moved = true" }, new[] { "_dirty = true" },
            "a notch of the wheel over Flare's feed lays its kept rows again without reading the account"),
        ("Ui/Flare/FlareScreen.cs", "Reveal", new[] { "_moved = true" }, new[] { "_dirty = true" },
            "a row the d-pad steps to is scrolled into view without reading the account"),
        ("Ui/Flare/FlareFace.Rows.cs", "Measure", new[] { "if (Math.Abs(list.Measured - w) < 0.5f)" }, new string[0],
            "a feed's rows are measured once at a width; a scroll lays them by the heights kept"),
        ("Ui/Flare/FlareFace.Rows.cs", "List", new[] { "if (py + rh > offset && py < offset + vh)", "OutOfView(" }, new string[0],
            "only the rows in view are laid out, and a target past the view's edge waits for the scroll"),
        // A choice on the city map (the owner's log of 4 Nov 2026: a pick of a bus stop on the map at 64 to 108 ms of the mod's own
        // scripts, all but a few of them the game's own shutting of its map run inside the click): the game's opening and shutting
        // of its map is the game's work, its buildings, trees, lights and markers switched for the map, kept out of the click's figure.
        ("Core/GameUi/MapPicker.cs", "ToggleMap", new[] { "using (HotSpots.Game())", "city.cityMap.Toggle(" }, new string[0],
            "the game's own opening and shutting of its map for a choice on it is the game's work, apart from the click's"),
        ("Core/GameUi/MapPicker.cs", "Open", new[] { "ToggleMap(city, centre, false)" }, new[] { ".cityMap.Toggle(" },
            "the choice opens the game's map only through ToggleMap, as the game's work"),
        ("Core/GameUi/MapPicker.cs", "Close", new[] { "ToggleMap(city, Vector3.zero, true)" }, new[] { ".cityMap.Toggle(" },
            "the choice shuts the game's map only through ToggleMap, as the game's work"),
        ("Core/GameUi/MapPicker.cs", "Picked", new[] { "_pickNow ??=" }, new[] { "() => pick(" }, "a pick on the map allocates nothing"),
        ("Core/MapPlaces.Names.cs", "GoTo", new[] { "using (HotSpots.Game())" }, new string[0],
            "a marker's click shuts the game's map as the game's work, apart from the click's"),
        ("Transport/TransitPointer.cs", "Clicked", new[] { "HotSpots.Time(HotClicked)" }, new string[0],
            "a click on a stop's pole, shelter or bus, which opens the stop map, is the mod's work and named"),
        ("Transport/TransitDirector.cs", "Awake", new[] { "PrewarmPicker)" }, new string[0],
            "the stop map's window, entries and markers are made behind the loading screen, none in play"),
        ("Transport/TransitDirector.Stops.cs", "PrewarmPicker", new[] { "_picker.Prepare(" }, new string[0],
            "an entry and a marker for every stop are made ahead, asleep"),
        ("Core/SpecialNames.cs", "Awake", new[] { "PrewarmSearch)" }, new string[0],
            "the map's search list of every building is made behind the loading screen, not at the map's first opening"),
        ("Core/MapTreeGuard.cs", "OnMapToggled", new[] { "SceneryTrees.Prune()" }, new[] { "SceneryTrees.Dead()", "Logger" },
            "the frame the map switches in has the one pass that keeps it openable; the check and its line wait for the next frame"),
        ("NewBuildings/CopyLook.cs", "Start", new[] { "if (Standing.Count == 1)" }, new[] { "_onMap = Guard.Wrap<bool>(OnMap);" },
            "one handler on the map's event for every copied building, not one a copy")
    };

    private static readonly Regex GameClick = new Regex(@"\.onClick\.Invoke\(\)", RegexOptions.Compiled);
    private static readonly Regex SliceWrite = new Regex(@"\.pixelsPerUnitMultiplier\s*=[^=]", RegexOptions.Compiled);

    private static void UiCostChecks()
    {
        var files = new Dictionary<string, SourceFile>(StringComparer.Ordinal);
        foreach (SourceFile f in Scripts)
        {
            files[f.Rel] = f;
        }
        foreach ((string file, string method, string[] must, string[] not, string why) in UiCostFacts)
        {
            string rel = ScriptsDir + "/" + file;
            Examine(UiCostRule, rel);
            if (!files.TryGetValue(rel, out SourceFile f))
            {
                Hit(UiCostRule, rel, 0, "the file is gone: put the list of the interface's costs right (StandardCheck/UiCostChecks.cs)");
                continue;
            }
            string body = f.CodeText;
            int line = 1;
            string where = "the file";
            if (method != null)
            {
                Match m = Regex.Match(f.CodeText, @"[\w>\]]\s+" + method + @"\s*\([^;{}]*\)\s*\{");
                if (!m.Success || !f.Block(m.Index, out int open, out int close))
                {
                    Hit(UiCostRule, rel, 0, method + " is gone: put the list of the interface's costs right (StandardCheck/UiCostChecks.cs)");
                    continue;
                }
                line = f.LineAt(open);
                body = f.CodeText.Substring(open + 1, close - open - 1).Replace("\r\n", "\n");
                where = method;
            }
            foreach (string word in must)
            {
                if (!body.Contains(word))
                {
                    Hit(UiCostRule, rel, line, where + " no longer holds \"" + word.Trim() + "\": " + why);
                }
            }
            foreach (string word in not)
            {
                if (body.Contains(word))
                {
                    Hit(UiCostRule, rel, line, where + " holds \"" + word.Trim() + "\" again: " + why);
                }
            }
        }

        // Every press of a game button under Scripts/Ui is a hand-off to the game (UiWidgets.Hand, or a HotSpots.Game
        // scope opened just above it), so what its listeners do is never counted as the mod's own; and a sliced picture's
        // scale is written through UiView.SliceScale alone. A file that must still press the game's buttons directly is named
        // in DirectPresses rather than in the baseline (RLO-TST-011), with why; none is now (Contacts' own presses,
        // ContactsScreen's Handed, Click and CallNow, View on map among them, go through UiWidgets.Hand since wave 2's merge).
        foreach (SourceFile f in Scripts)
        {
            if (!f.Rel.StartsWith(ScriptsDir + "/Ui/", StringComparison.Ordinal))
            {
                continue;
            }
            Examine(UiCostRule, f.Rel);
            bool excepted = Array.IndexOf(DirectPresses, f.Rel.Substring(ScriptsDir.Length + 1)) >= 0;
            int direct = 0;
            for (int i = 0; i < f.Code.Length; i++)
            {
                if (GameClick.IsMatch(f.Code[i]) && !HandedAbove(f, i))
                {
                    direct++;
                    if (!excepted)
                    {
                        Hit(UiCostRule, f.Rel, i + 1, "a game button pressed outside a hand-off (UiWidgets.Hand): " + f.Code[i]);
                    }
                }
                if (SliceWrite.IsMatch(f.Code[i]) && !f.Code[i].Contains("image.pixelsPerUnitMultiplier = want"))
                {
                    Hit(UiCostRule, f.Rel, i + 1, "a slice scale written outside UiView.SliceScale, which writes it only on a change: " + f.Code[i]);
                }
            }
            if (excepted && direct == 0)
            {
                Notes.Add(UiCostRule + ": " + f.Rel + " presses no game button directly now: take it off DirectPresses (StandardCheck/UiCostChecks.cs)");
            }
        }
    }

    // The files whose presses of a game button are not handed on yet (paths under Scripts), each named with why above.
    private static readonly string[] DirectPresses = Array.Empty<string>();

    // A HotSpots.Game scope opened on the line or within the three lines above it.
    private static bool HandedAbove(SourceFile f, int line)
    {
        for (int k = line; k >= 0 && k >= line - 3; k--)
        {
            if (f.Code[k].Contains("HotSpots.Game()"))
            {
                return true;
            }
        }
        return false;
    }
}
