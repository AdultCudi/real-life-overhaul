using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Ui;

// The caret of a text field (UiDraw.Caret, the host's blink; the owner, 3 Nov: "In the places where you type or enter stuff, the
// blinking cursor is not coming."). Every field the kit draws shows a caret while it takes the keys: at the end of the typed
// words (before the placeholder of an empty field), inside the field, as tall as a line of its words, in the cyan. The host
// says which target takes the keys through the scene (UiScene.TypingCommand and TypingArg), so no screen can forget; the check
// lays each helper and each screen's samples with a field typing and wants the caret in it. The blink is the host's: one item's
// visibility on the clock, never a layout, nothing at all while no field types (read from the host's source).
internal static partial class Program
{
    // Every class that runs a screen with a text field (IUiTyping) and the commands its TypingCommand answers. A screen with no entry
    // fails below, so a new typing screen cannot ship without its field being checked. FlareScreen draws its own fields (FlareFace's
    // TextField, the side's search and the compose sheet's box), each with the caret through UiDraw.Typing.
    private static readonly Dictionary<string, int[]> TypingScreens = new Dictionary<string, int[]>(StringComparer.Ordinal)
    {
        { "BizManScreen", new[] { BizManPages.Type, BizManPages.OpsType, BizManPages.OpsSearch, UiDataTable.First + UiDataTable.Search, BizManPages.Goods + UiDataTable.Type,
            BizManPages.People + UiDataTable.Search, BizManPages.Table + UiDataTable.Type, BizManPages.Table + UiDataTable.Search } },
        { "UniformsScreen", new[] { UiUniforms.Name } },
        { "CheckoutScreen", new[] { UiCheckout.TaxTypeCommand } },
        { "WardrobeScreen", new[] { UiWardrobe.SearchCommand } },
        { "ContactsScreen", new[] { UiConversation.FormCommand, UiMessageList.SearchCommand } },
        { "DialogScreen", new[] { UiFormRows.FieldCommand } },
        { "EconoViewScreen", new[] { UiEcono.AmountCommand } },
        { "EmployeesScreen", new[] { UiDataTable.First + UiDataTable.Search } },
        { "MarketScreen", new[] { UiDataTable.First + UiDataTable.Search } },
        { "PauseScreen", new[] { UiPauseMenu.NameCommand } },
        { "BugReportScreen", new[] { UiReport.FieldCommand } },
        { "GuideScreen", new[] { UiSettingsList.SearchCommand } },
        { "SettingsScreen", new[] { UiSettingsList.SearchCommand } },
        { "StaffScreen", new[] { UiSettingsList.SearchCommand } },
        { "TextBoxScreen", new[] { UiCard.FieldCommand } },
        { "StocksScreen", new[] { StocksLay.Shares, StocksLay.PricePlain, UiDataTable.First + UiDataTable.Search, StocksSheetLay.Type } },
        { "ItemsListScreen", new[] { UiItemsList.TypeCommand } },
        { "UiPartsGalleryScreen", new[] { UiPartsGallery.Field } },
        { "GalleryScreen", new[] { UiAlbum.Search } },
        { "FlareScreen", new[] { FlareFace.FieldCommand } },
        { "FinanceScreen", new[] { FinanceFace.AmountType } },
        { "WorkScreen", new[] { WorkCmd.Search, WorkCmd.Type } },
        { "BanksScreen", new[] { MoneyCommands.TypeField } },
        { "OwnBankScreen", new[] { MoneyCommands.TypeField } },
        { "CompanyScreen", new[] { CompanyFace.StepType } },
        { "EstateScreen", new[] { EstateFace.SearchCommand } },
        { "VentScreen", new[] { VentFace.StepType } },
        { "BuildFace", new[] { UiBuildPanels.SearchCommand } },
        { "MapLive", new[] { UiMapFilters.SearchCommand } }
    };

    private static void UiCaretChecks()
    {
        UiCaretHelpers();
        UiCaretCards();
        UiCaretScreens();
        UiCaretBlink();
    }

    // ---------------------------------------------------------------- the helpers

    // Lays one control with the field of a command and argument typing.
    private static UiScene UiCaretScene(Action<UiScene> draw, int command, int arg)
    {
        var s = new UiScene();
        s.Begin("caret", 1920f, 1080f, true);
        s.TypingCommand = command;
        s.TypingArg = arg;
        draw(s);
        return s;
    }

    // What is wrong with the caret in a scene for the node of a command and argument, or null: drawn, recorded, a decor box of the
    // cyan with a size, inside the field's box and as tall as a line of its words.
    private static string UiCaretFault(UiScene s, int command, int arg, UiStyle style)
    {
        int node = s.FindNode(command, arg);
        if (node < 0)
        {
            return "no target for command " + command;
        }
        if (s.CaretItem < 0 || s.CaretItem >= s.ItemCount)
        {
            return "no caret was drawn";
        }
        ref UiItem c = ref s.Items[s.CaretItem];
        s.ScreenBox(s.CaretItem, out float cx, out float cy, out float cw, out float ch);
        s.NodeBox(node, out float nx, out float ny, out float nw, out float nh);
        if (c.Kind != UiKind.Box || c.Fill != UiRole.Accent || (c.Flags & UiFlags.Decor) == 0)
        {
            return "the caret is not a decor box in the accent";
        }
        if (cw <= 0f || ch <= 0f)
        {
            return "the caret has no size (" + cw + " x " + ch + ")";
        }
        if (cx < nx - 0.01f || cy < ny - 0.01f || cx + cw > nx + nw + 0.01f || cy + ch > ny + nh + 0.01f)
        {
            return "the caret (" + cx + ", " + cy + ", " + cw + " x " + ch + ") is outside its field (" + nx + ", " + ny + ", " + nw + " x " + nh + ")";
        }
        if (Math.Abs(ch - UiTextRules.LineHeight(style)) > 0.5f)
        {
            return "the caret is " + ch + " px tall, the words' line " + UiTextRules.LineHeight(style);
        }
        return null;
    }

    // Whether a target is a field of words or an amount: a form row of the same command may be a choice (its chevron), a switch or a
    // slider (its track), which take no typed words.
    private static bool UiCaretTextLike(UiScene s, int node)
    {
        int face = s.Nodes[node].Face;
        for (int i = face + 1; i < s.ItemCount; i++)
        {
            if (s.Items[i].Parent == face && (s.Items[i].Kind == UiKind.Icon && s.Items[i].Icon == UiIcon.Down || (s.Items[i].Flags & UiFlags.Circle) != 0))
            {
                return false;
            }
        }
        return true;
    }

    // The words drawn in a field (the text item inside the field's face), or -1.
    private static int UiCaretWords(UiScene s, int command, int arg)
    {
        int node = s.FindNode(command, arg);
        if (node < 0)
        {
            return -1;
        }
        int found = -1;
        for (int i = s.Nodes[node].Face + 1; i < s.ItemCount; i++)
        {
            if (s.Items[i].Kind == UiKind.Text && s.Items[i].Parent == s.Nodes[node].Face)
            {
                found = i;
            }
        }
        return found;
    }

    private static void UiCaretHelpers()
    {
        const int command = 77, arg = 3;
        UiAt at = UiAt.Screen;
        string longWords = "Harbour Lights Bakery and Coffee House on Fifth Avenue, open late on Sundays for the evening crowd, with live music";
        var cases = new List<(string name, Action<UiScene> draw, UiStyle style, bool words, int arg)>
        {
            ("text field, typed", s => UiDraw.TextField(s, at, 100f, 100f, 360f, "Bloom Cafe", "Name", command, arg, false), UiStyle.Body, true, arg),
            ("text field, empty", s => UiDraw.TextField(s, at, 100f, 100f, 360f, string.Empty, "Name", command, arg, false), UiStyle.Body, false, arg),
            ("text field, longer than it", s => UiDraw.TextField(s, at, 100f, 100f, 360f, longWords, "Name", command, arg, false), UiStyle.Body, true, arg),
            ("search, typed", s => UiDraw.Search(s, at, 100f, 100f, 360f, "Search", "credit", "3", command, arg), UiStyle.Body, true, arg),
            ("search, empty", s => UiDraw.Search(s, at, 100f, 100f, 360f, "Search", string.Empty, null, command, arg), UiStyle.Body, false, arg),
            ("search, longer than it", s => UiDraw.Search(s, at, 100f, 100f, 360f, "Search", longWords, "12", command, arg), UiStyle.Body, true, arg),
            ("composer, typed", s => UiDraw.Composer(s, at, 100f, 100f, 520f, "Write a message", "Of course, thank you", "Send", command, 99), UiStyle.Body, true, 0),
            ("composer, empty", s => UiDraw.Composer(s, at, 100f, 100f, 520f, "Write a message", string.Empty, "Send", command, 99), UiStyle.Body, false, 0),
            ("amount, typed", s => UiDraw.Stepper(s, at, 100f, 100f, 240f, "1,200", true, true, 80, command, 81, arg, false), UiStyle.BodyStrong, false, arg),
            ("amount, empty with a hint", s => UiDraw.Stepper(s, at, 100f, 100f, 240f, string.Empty, true, true, 80, command, 81, arg, false, "Type an amount"), UiStyle.BodyStrong, false, arg),
            ("amount, empty", s => UiDraw.Stepper(s, at, 100f, 100f, 240f, string.Empty, true, true, 80, command, 81, arg, false), UiStyle.BodyStrong, false, arg)
        };
        foreach (var c in cases)
        {
            UiScene on = UiCaretScene(c.draw, command, c.arg);
            string fault = UiCaretFault(on, command, c.arg, c.style);
            Check(fault == null, "ui caret: " + c.name + " shows its caret while it takes the keys" + (fault == null ? string.Empty : " (" + fault + ")"));
            UiScene off = UiCaretScene(c.draw, -1, 0);
            Check(off.CaretItem < 0, "ui caret: " + c.name + " draws none while nothing types");
            UiScene other = UiCaretScene(c.draw, command, c.arg + 1);
            Check(other.CaretItem < 0, "ui caret: " + c.name + " draws none while another field of the screen types");
            if (c.words && fault == null)
            {
                int words = UiCaretWords(on, command, c.arg);
                on.ScreenBox(on.CaretItem, out float cx, out float _, out float _, out float _);
                float end = words >= 0 ? on.Items[words].X + Math.Min(on.Items[words].W, UiTextRules.Width(on.Items[words].Text, UiStyle.Body)) : -1f;
                Check(words >= 0 && cx >= end - 0.5f && on.Items[words].Text.Length > 0, "ui caret: " + c.name + " puts the caret after the last letter shown");
                Check(words >= 0 && UiTextRules.Fits(on.Items[words].Text, UiStyle.Body, on.Items[words].W),
                    "ui caret: " + c.name + " shows the end of its words that fits, so the last letter is never cut");
            }
        }
        // An empty field's caret comes before its placeholder, not over it.
        UiScene empty = UiCaretScene(s => UiDraw.TextField(s, at, 100f, 100f, 360f, string.Empty, "Name", command, arg, false), command, arg);
        int hint = UiCaretWords(empty, command, arg);
        Check(hint >= 0 && empty.CaretItem >= 0 && empty.Items[empty.CaretItem].X + empty.Items[empty.CaretItem].W <= empty.Items[hint].X,
            "ui caret: an empty field's caret stands before its placeholder");
        // A disabled field takes no keys.
        UiScene disabled = UiCaretScene(s => UiDraw.TextField(s, at, 100f, 100f, 360f, "Locked", "Name", command, arg, true), command, arg);
        Check(disabled.CaretItem < 0, "ui caret: a disabled field draws no caret");
        // The command alone is not enough: a screen with two fields of one command shows the caret in the one typing.
        UiScene two = UiCaretScene(s =>
        {
            UiDraw.TextField(s, at, 100f, 100f, 360f, "one", null, command, 0, false);
            UiDraw.TextField(s, at, 100f, 160f, 360f, "two", null, command, 1, false);
        }, command, 1);
        float second = two.CaretItem >= 0 ? two.Items[two.CaretItem].Y : 0f;
        Check(two.CaretItem >= 0 && second >= 160f, "ui caret: of two fields of one command, only the one typing has the caret");
        // A shape tried and taken back leaves no caret behind.
        var s2 = new UiScene();
        s2.Begin("caret", 1920f, 1080f, true);
        s2.TypingCommand = command;
        s2.TypingArg = arg;
        UiMark mark = UiMark.Of(s2);
        UiDraw.TextField(s2, at, 100f, 100f, 360f, "tried", null, command, arg, false);
        bool drew = s2.CaretItem >= 0;
        mark.Back(s2);
        Check(drew && s2.CaretItem < 0, "ui caret: a layout taken back (UiMark) takes its caret back");
        // A sample's own Typing flag still shows a caret with no host.
        UiScene forced = UiCaretScene(s => UiDraw.TextField(s, at, 100f, 100f, 360f, "sample", null, command, arg, false, true), -1, 0);
        Check(forced.CaretItem >= 0, "ui caret: a sample's Typing flag shows a caret without the host");
    }

    // The decision card's field and the report's two boxes, laid whole, and the wrapping rules the report's caret stands on.
    private static void UiCaretCards()
    {
        UiWords en = UiWords("en");
        UiFrame frame = UiFrame.FullMenu(16f / 9f, "Text box");
        foreach (string typed in new[] { null, "Harbour Lights Bakery", "Harbour Lights Bakery and Coffee House on Fifth Avenue, open late on Sundays" })
        {
            string words = typed;
            UiScene s = UiCaretScene(sc => UiCard.Lay(sc, UiSamples.TextBox(en, words), frame, true, UiDefaultKeys.Keyboard), UiCard.FieldCommand, 0);
            string fault = UiCaretFault(s, UiCard.FieldCommand, 0, UiStyle.Body);
            Check(fault == null, "ui caret: the text box card shows its caret (" + (words == null ? "empty" : words.Length + " letters") + ")" + (fault == null ? string.Empty : ": " + fault));
        }
        string one = "I opened Finance and went to Borrow.", many = string.Empty;
        for (int i = 0; i < 12; i++)
        {
            many += "Every bank showed nothing as the most it would lend, so I could not borrow. ";
        }
        foreach (string happened in new[] { null, one, many })
        {
            string words = happened;
            var m = new UiReportModel
            {
                Title = "Report", HappenedLabel = "What happened", Happened = words, HappenedHint = "A few sentences", ExpectedLabel = "What you expected",
                Expected = "Offers", ExpectedHint = "What you thought", WhereLabel = "Where", Where = "The city"
            };
            for (int box = 0; box < 2; box++)
            {
                int which = box;
                UiScene s = UiCaretScene(sc => UiReport.Lay(sc, m, frame, true, UiDefaultKeys.Keyboard), UiReport.FieldCommand, which);
                string fault = UiCaretFault(s, UiReport.FieldCommand, which, UiStyle.Body);
                Check(fault == null, "ui caret: the report's box " + which + " shows its caret (" + (words == null ? "empty" : words.Length + " letters") + ")" + (fault == null ? string.Empty : ": " + fault));
            }
        }
        string tail = UiTextRules.LastLines(many, UiStyle.Body, 800f, 3, out float last);
        Check(UiTextRules.Lines(tail, UiStyle.Body, 800f, 0, out float _) <= 3 && many.EndsWith(tail, StringComparison.Ordinal) && tail.Length < many.Length && last > 0f && last <= 800f,
            "ui caret: a box of a few lines shows the last lines of long words, where the caret is");
        string head = "short words";
        Check(UiTextRules.LastLines(head, UiStyle.Body, 800f, 3, out float _) == head && UiTextRules.Tail(head, UiStyle.Body, 800f) == head, "ui caret: words that fit are shown whole");
        string cut = UiTextRules.Tail(many, UiStyle.Body, 300f);
        Check(UiTextRules.Fits(cut, UiStyle.Body, 300f) && many.EndsWith(cut, StringComparison.Ordinal) && cut.Length > 5,
            "ui caret: a line shows the end of long words that fits, the last letter kept");
    }

    // ---------------------------------------------------------------- every screen with a field

    private static void UiCaretScreens()
    {
        string scripts = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        var found = new HashSet<string>(StringComparer.Ordinal);
        var declared = new Regex(@"class\s+(\w+)\s*:[^{;]*?\bIUiTyping(?:Submit)?\b");
        foreach (string path in Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories))
        {
            foreach (Match m in declared.Matches(File.ReadAllText(path)))
            {
                found.Add(m.Groups[1].Value);
            }
        }
        string missing = "none", stale = "none";
        foreach (string name in found)
        {
            if (!TypingScreens.ContainsKey(name))
            {
                missing = name;
            }
        }
        foreach (string name in TypingScreens.Keys)
        {
            if (!found.Contains(name))
            {
                stale = name;
            }
        }
        Check(found.Count >= 15 && missing == "none", "ui caret: every screen with a text field is in the caret check (IUiTyping; not listed: " + missing + ")");
        Check(stale == "none", "ui caret: every screen the caret check lists still takes typed words (" + stale + ")");
        // Each one's fields, in its samples: lay with the field typing and want the caret in it.
        var tried = new Dictionary<string, int>(StringComparer.Ordinal);
        var failed = new List<string>();
        UiWords en = UiWords("en");
        var scene = new UiScene();
        foreach (UiScreenCase c in UiCases())
        {
            if (c.Def.Runner == null || !TypingScreens.TryGetValue(c.Def.Runner, out int[] commands) || commands == null)
            {
                continue;
            }
            UiFrame frame = UiFrame.FullMenu(16f / 9f, c.Title);
            scene.TypingCommand = -1;
            c.Lay(scene, frame, true, UiDefaultKeys.Keyboard, en);
            Check(scene.CaretItem < 0, "ui caret: " + c.Name + " draws no caret while nothing types");
            var targets = new List<int[]>();
            for (int n = 0; n < scene.NodeCount && targets.Count < 6; n++)
            {
                if (Array.IndexOf(commands, scene.Nodes[n].Command) >= 0 && !scene.Nodes[n].Disabled && scene.Nodes[n].Layer == 0 && UiCaretTextLike(scene, n))
                {
                    targets.Add(new[] { scene.Nodes[n].Command, scene.Nodes[n].Arg });
                }
            }
            foreach (int[] target in targets)
            {
                scene.TypingCommand = target[0];
                scene.TypingArg = target[1];
                c.Lay(scene, frame, true, UiDefaultKeys.Keyboard, en);
                string fault = UiCaretFault(scene, target[0], target[1], UiStyle.Body);
                if (fault != null && UiCaretFault(scene, target[0], target[1], UiStyle.BodyStrong) == null)
                {
                    fault = null;   // a stepper's figure is in the strong style
                }
                tried[c.Def.Runner] = (tried.TryGetValue(c.Def.Runner, out int before) ? before : 0) + 1;
                if (fault != null)
                {
                    failed.Add(c.Name + ", command " + target[0] + " argument " + target[1] + ": " + fault);
                }
            }
            scene.TypingCommand = -1;
        }
        Check(failed.Count == 0, "ui caret: every text field of every screen shows its caret while it takes the keys" + (failed.Count == 0 ? string.Empty : " (" + failed[0] + "; " + failed.Count + " in all)"));
        var untried = new List<string>();
        foreach (var pair in TypingScreens)
        {
            if (pair.Value != null && !tried.ContainsKey(pair.Key))
            {
                untried.Add(pair.Key);
            }
        }
        Check(untried.Count == 0, "ui caret: each screen's samples reach one of its fields (none reached: " + string.Join(", ", untried) + ")");
    }

    // ---------------------------------------------------------------- the blink

    // A member's text from its signature to its closing brace (members of the kit's files stand at eight spaces).
    private static string UiCaretMember(string text, string signature)
    {
        int start = text.IndexOf(signature, StringComparison.Ordinal);
        if (start < 0)
        {
            return null;
        }
        int end = text.IndexOf("\n        }", start, StringComparison.Ordinal);
        return end < 0 ? null : text.Substring(start, end - start);
    }

    private static void UiCaretBlink()
    {
        string folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts\Ui"));
        string host = File.ReadAllText(System.IO.Path.Combine(folder, "UiHost.cs")).Replace("\r\n", "\n");
        string view = File.ReadAllText(System.IO.Path.Combine(folder, "UiView.cs")).Replace("\r\n", "\n");
        string blink = UiCaretMember(host, "private void Blink(float now)"), set = UiCaretMember(view, "public void SetCaret(bool on)");
        Check(blink != null && set != null, "ui caret: the host's blink and the view's SetCaret are there");
        blink = blink ?? string.Empty;
        set = set ?? string.Empty;
        Check(!blink.Contains("Layout(") && !blink.Contains("Dirty") && !blink.Contains("Again(") && !blink.Contains("_laidSize") && !blink.Contains(".Write("),
            "ui caret: the blink never lays the screen out or marks it dirty");
        Check(!set.Contains("new ") && !set.Contains("Write(") && !set.Contains("Layout") && set.Contains("Show(slot.Rect, on)"),
            "ui caret: SetCaret shows or hides the one item and allocates nothing");
        Check(blink.Contains("now + BlinkEvery") && blink.Contains("_view.SetCaret(_caretOn)") && host.Contains("private const float BlinkEvery = 0.5f;"),
            "ui caret: the blink turns every half second on the clock");
        int typing = host.IndexOf("if (_typing)\n            {\n                if (_state.Back)", StringComparison.Ordinal);
        int call = host.IndexOf("Blink(now);", StringComparison.Ordinal);
        int next = typing < 0 ? -1 : host.IndexOf("return;", typing, StringComparison.Ordinal);
        Check(typing >= 0 && call > typing && call < next && host.IndexOf("Blink(now);", call + 1, StringComparison.Ordinal) < 0 && host.Contains("else if (now >= _blinkAt)"),
            "ui caret: the blink runs only while a field takes the keys, and costs one clock comparison a frame");
        string end = UiCaretMember(host, "private void EndTyping()") ?? string.Empty;
        Check(host.Contains("_scene.TypingCommand = _typing ? _typeCommand : -1;") && host.Contains("BeginTyping(typing, n.Command, n.Arg);")
            && host.Contains("BeginTyping(box, box.TypingCommand, 0);")
              && end.Contains("_typeCommand = -1;") && end.Contains("_laidSize = Vector2.zero;"),
            "ui caret: the host tells the scene which target takes the keys, from the press to the end of the typing");
        string layout = UiCaretMember(host, "private void Layout()") ?? string.Empty;
        Check(layout.Contains("_caretOn = true;") && layout.Contains("_blinkAt = Time.unscaledTime + SolidFor"), "ui caret: a layout (a key typed) leaves the caret solid for a moment");
        string hide = UiCaretMember(host, "public void Hide()") ?? string.Empty, hold = UiCaretMember(host, "public void Hold(bool hold)") ?? string.Empty;
        string pointer = UiCaretMember(host, "private void PointerPressed(int node)") ?? string.Empty, ended = UiCaretMember(host, "private void TypingEnded(string text)") ?? string.Empty;
        Check(hide.Contains("EndTyping();") && hold.Contains("EndTyping();") && pointer.Contains("EndTyping();") && ended.Contains("EndTyping();")
            && host.Contains("_input.TakeBack();\n                    EndTyping();"),
            "ui caret: typing ends on Enter, Esc, a click elsewhere and the screen closing, and the caret goes with it");
    }
}
