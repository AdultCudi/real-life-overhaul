using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Ui;

// The conversion kept total (--sweep-ui, and the regular run through UiCasesSweep): the audit table of the sweep (SweepLedger) and what it
// promises. Every file outside the kit that builds interface of its own (a canvas, an image or text made in code, a copy of a game panel,
// a phone app) is named in the table with how it is drawn, so a new one fails here until it has a kit screen or a dated decision; every
// phone app has its kit face; every screen the kit runs has its switch, its texts and its place on a newer template than the four older
// shapes (but the two pages listed as pending); every row says why in plain words; and the sweep's own conversions (the radio's Dance, the
// seat's Eat, the desk session on the decision card) are read in the source: each press goes to the game's own button.
internal static partial class Program
{
    private static bool _sweepChecked;

    // Screens that run on the kit and have no switch of their own: the gallery's pages are the kit's, not the game's.
    private static readonly string[] SweepNoSwitch = { "gallery", "kit_parts" };

    // What builds interface of its own: the same words the table was written from.
    private static readonly Regex SweepBuilds = new Regex(
        @"typeof\((Image|TextMeshProUGUI|TMP_Text|Canvas|RawImage|Button)\)|AddComponent<(Canvas|TextMeshProUGUI|TMP_Text|Image|RawImage|Button|ScrollRect)>"
        + @"|UiClone\.(Asleep|Dormant)\(|PhoneApp\.Install\(",
        RegexOptions.Compiled);

    static partial void UiCasesSweep(UiScreenDef def, List<UiScreenCase> cases)
    {
        if (def.Id == "session_panel" && !_sweepChecked)
        {
            SweepUiChecks();
        }
    }

    private static void SweepUiChecks()
    {
        _sweepChecked = true;
        SweepTableChecks();
        SweepFileChecks();
        SweepScreenChecks();
        SweepConversionChecks();
        SweepRuleChecks();
        SweepIconChecks();
        OldWindowsAudit();
    }

    // ---------------------------------------------------------------- the table

    private static void SweepTableChecks()
    {
        SweepRow[] rows = SweepLedger.Rows;
        Check(rows.Length >= 80, "sweep: the audit table holds the whole conversion (" + rows.Length + " rows)");
        var names = new HashSet<string>(StringComparer.Ordinal);
        bool unique = true, said = true, dated = true, screens = true, pending = true;
        var ids = new HashSet<string>(UiScreens.All.Select(d => d.Id), StringComparer.Ordinal);
        foreach (SweepRow r in rows)
        {
            unique &= names.Add(r.Surface);
            said &= !string.IsNullOrWhiteSpace(r.Words) && r.Words.Length > 12;
            bool needsScreen = r.State == SweepState.Kit || r.State == SweepState.Follows || r.State == SweepState.WayBack;
            screens &= needsScreen ? r.Screen != null && ids.Contains(r.Screen) : r.State == SweepState.Pending || r.Screen == null;
            dated &= r.State != SweepState.Decision || (!string.IsNullOrEmpty(r.Date) && Regex.IsMatch(r.Date, @"^\d{1,2} (Oct|Nov|Dec) 202\d$"));
            pending &= r.State != SweepState.Pending || (r.Screen != null && ids.Contains(r.Screen));
        }
        Check(unique, "sweep: each surface stands once in the table");
        Check(said, "sweep: every row says in plain words how it is drawn or why it is left");
        Check(screens, "sweep: every row drawn on the kit or following a screen names a registered screen, and no other row names one");
        Check(dated, "sweep: every row left the game's by decision carries the decision's date");
        Check(pending, "sweep: a pending row names the registered screen that is still on an older shape");
        foreach (SweepRow r in rows.Where(x => x.State == SweepState.Decision))
        {
            Check(r.Files.Length == 0 && r.Words.EndsWith(".", StringComparison.Ordinal), "sweep: the decision '" + r.Surface + "' is a plain sentence and names no file");
        }
    }

    // ---------------------------------------------------------------- the files

    private static IEnumerable<string> SweepScriptFiles(string scripts)
    {
        return Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Substring(scripts.Length + 1).Replace('\\', '/').StartsWith("Ui/", StringComparison.Ordinal));
    }

    private static void SweepFileChecks()
    {
        string scripts = ModScripts();
        var built = new List<string>(100);
        foreach (string file in SweepScriptFiles(scripts))
        {
            if (SweepBuilds.IsMatch(File.ReadAllText(file)))
            {
                built.Add(file.Substring(scripts.Length + 1).Replace('\\', '/'));
            }
        }
        built.Sort(StringComparer.Ordinal);
        string[] named = SweepLedger.AllFiles();
        string[] missing = built.Where(f => Array.BinarySearch(named, f, StringComparer.Ordinal) < 0).ToArray();
        string[] stale = named.Where(f => !File.Exists(System.IO.Path.Combine(scripts, f.Replace('/', System.IO.Path.DirectorySeparatorChar)))).ToArray();
        string[] gone = named.Where(f => built.BinarySearch(f, StringComparer.Ordinal) < 0
                                         && File.Exists(System.IO.Path.Combine(scripts, f.Replace('/', System.IO.Path.DirectorySeparatorChar)))).ToArray();
        Check(missing.Length == 0, "sweep: every file that builds interface of its own is in the audit table; add " + (missing.Length > 0 ? string.Join(", ", missing) : "none")
            + " to Ui/Sweep/SweepLedger with how it is drawn (a kit screen) or the decision that leaves it");
        Check(stale.Length == 0, "sweep: every file the table names exists (" + (stale.Length > 0 ? string.Join(", ", stale) : "all") + ")");
        Check(gone.Length == 0, "sweep: every file the table names still builds interface of its own (" + (gone.Length > 0 ? string.Join(", ", gone) : "all") + "): take the others out");
        Check(named.Distinct(StringComparer.Ordinal).Count() == named.Length, "sweep: a file stands in one row only");

        // Every phone app the mod installs has its kit face, its screen id and the frame's call that draws it.
        int apps = 0;
        bool faces = true, called = true;
        string why = null;
        foreach (string file in SweepScriptFiles(scripts))
        {
            string rel = file.Substring(scripts.Length + 1).Replace('\\', '/');
            if (rel.StartsWith("Core/GameUi/", StringComparison.Ordinal))
            {
                continue;
            }
            string text = File.ReadAllText(file);
            int installs = Regex.Matches(text, @"PhoneApp\.Install\(").Count;
            if (installs == 0)
            {
                continue;
            }
            apps += installs;
            bool kit = Regex.IsMatch(text, @"\bKit\s*=") && text.Contains("KitScreen") || Regex.IsMatch(text, @"\bKit\s*=\s*\(\)\s*=>");
            bool face = Regex.Matches(text, @"UiShell\.Face\(").Count >= installs;
            faces &= kit;
            called &= face;
            why = kit && face ? why : (why ?? rel);
        }
        Check(apps >= 28, "sweep: the mod's phone apps are all found (" + apps + ")");
        Check(faces, "sweep: every phone app is defined with a kit face and its screen id" + (why != null ? " (" + why + ")" : ""));
        Check(called, "sweep: every phone app is given the frame's kit face after its install" + (why != null ? " (" + why + ")" : ""));
    }

    // ---------------------------------------------------------------- the registry

    private static void SweepScreenChecks()
    {
        string en = File.ReadAllText(System.IO.Path.Combine(ModScripts(), "..", "Locales", "en.json"));
        var noSwitch = new List<string>();
        var noText = new List<string>();
        var older = new List<string>();
        foreach (UiScreenDef def in UiScreens.All)
        {
            if (def.Runner == null)
            {
                continue;   // a template's sample or a screen the kit's host draws: no runner of its own to turn off
            }
            if (def.Section == null && Array.IndexOf(SweepNoSwitch, def.Id) < 0)
            {
                noSwitch.Add(def.Id);
            }
            if (def.Section != null && (!en.Contains("\"reallife_ui_screen_" + def.Id + "\"") || !en.Contains("\"reallife_opt_tip_ui_screen_" + def.Id + "\"")))
            {
                noText.Add(def.Id);
            }
            if (def.Template == UiTemplate.ListDetail || def.Template == UiTemplate.Form || def.Template == UiTemplate.Session)
            {
                older.Add(def.Id);
            }
        }
        Check(noSwitch.Count == 0, "sweep: every screen the kit runs has its own switch (" + (noSwitch.Count > 0 ? string.Join(", ", noSwitch) : "all") + ")");
        Check(noText.Count == 0, "sweep: every switch has its label and its tip in English (" + (noText.Count > 0 ? string.Join(", ", noText) : "all") + ")");
        string[] pending = SweepLedger.Rows.Where(r => r.State == SweepState.Pending).Select(r => r.Screen).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        older.Sort(StringComparer.Ordinal);
        Check(older.SequenceEqual(pending), "sweep: the screens still on an older shape (" + string.Join(", ", older) + ") are the pending rows of the table (" + string.Join(", ", pending) + ")");
        Check(SweepLedger.Rows.All(r => r.State != SweepState.Pending || r.Words.Length > 40), "sweep: a pending row says what stands in its way");
        Check(Array.Exists(UiScreenSwitches.Sections, s => s == UiScreenSwitches.Apps), "sweep: the apps section of the switches is on the page");
    }

    // ---------------------------------------------------------------- what the sweep converted

    private static void SweepConversionChecks()
    {
        string scripts = ModScripts();
        string radio = File.ReadAllText(System.IO.Path.Combine(scripts, "Ui", "Overlays", "OverlayCardsLive.Click.cs"));
        string at = radio.Substring(radio.IndexOf("private void Radio(", StringComparison.Ordinal));
        at = at.Substring(0, at.IndexOf("// The workstation's name", StringComparison.Ordinal) > 0 ? at.IndexOf("// The workstation's name", StringComparison.Ordinal) : at.Length);
        Check(at.Contains("GetComponentsInChildren(false, _found)") && at.Contains("b == next || b == toggle") && at.Contains("Put(m, new UiOverlayAction { Label = label, Disabled = !b.interactable }, b, null);"),
            "sweep: the radio's card lists a button put in its panel after the game made it (Dance) and presses it as the game's own button");
        string seat = File.ReadAllText(System.IO.Path.Combine(scripts, "Ui", "Activity", "ActivityPanelScreen.cs"));
        string eat = File.ReadAllText(System.IO.Path.Combine(scripts, "Meals", "TakeoutButton.cs"));
        Check(seat.Contains("Meals.TakeoutButton.Offered(") && seat.Contains("Guard.Wrap(() => UiWidgets.Hand(eat))") && seat.Contains("strip.Add(button)")
              && seat.Contains("card.Add(button)"),
            "sweep: the activity card and strip list Eat at a seat and press the panel's own Eat button");
        Check(eat.Contains("public static bool Offered(out Button button, out string words)") && eat.Contains("activeInHierarchy"),
            "sweep: Eat is offered only while the game's panel shows its button");
        Check(Regex.Matches(seat, "Meals\\.TakeoutButton\\.Offered\\(").Count == 2, "sweep: the activity panel's signature sees Eat come and go, so the card is laid again");
        string desk = File.ReadAllText(System.IO.Path.Combine(scripts, "Ui", "Pages", "SessionKit.cs"));
        Check(desk.Contains("UiDecision.Lay(s, Build(), frame, dark, keys)") && !desk.Contains("UiSessionSteps") && desk.Contains("UiDecision.ChoiceCommand")
              && desk.Contains("UiDecision.ButtonCommand"),
            "sweep: a desk session's pages are the decision card, and each press is its own number");
        Check(desk.Contains("b.onClick()") && desk.Contains("Kind = main ? UiButtonKind.Primary"), "sweep: a desk session's buttons run the activity panel's own onClick");
        UiScreenDef session = UiScreens.All.First(d => d.Id == "session_panel");
        Check(session.Template == UiTemplate.Decision && session.Section == UiScreenSwitches.Apps, "sweep: the desk session is registered as a decision with its switch");
    }

    private static void SweepRuleChecks()
    {
        UiConsequence[] two = SweepRules.Consequences("Burnout eases for the people present. Output this week is a little lower.", "Lift morale");
        Check(two.Length == 2 && two[0].Text == "Burnout eases for the people present" && two[1].Text == "Output this week is a little lower" && two[0].Tone == 0 && two[1].Tone == 0,
            "sweep: a choice's trade is a plain line a sentence, none coloured as good or bad since the panel does not say");
        UiConsequence[] named = SweepRules.Consequences("Push output: More gets done this week. Burnout builds each day they work.", "Push output");
        Check(named.Length == 2 && named[0].Text == "More gets done this week", "sweep: the choice's own name is not said twice");
        Check(SweepRules.Consequences(null, "x").Length == 0 && SweepRules.Consequences("  ", "x").Length == 0 && SweepRules.Consequences("One", "x").Length == 1
              && SweepRules.Consequences("A. B. C. D. E.", null).Length == SweepRules.MostLines, "sweep: no trade gives no line, one without a stop gives one, and a long one stops at three");
        Check(SweepRules.Consequences("Costs $1.50 a day.", null)[0].Text == "Costs $1.50 a day", "sweep: a figure's point does not end a sentence");
    }

    // ---------------------------------------------------------------- the icon sheet

    // The gallery's Icons page draws every glyph of the family, the waves' among them, each once on its tile and once small, in its one colour.
    private static void SweepIconChecks()
    {
        var s = new UiScene();
        UiWords en = UiWords("en");
        UiFrame frame = UiFrame.FullMenu(16f / 9f, "Gallery");
        UiGallery.Lay(s, UiSamples.Gallery(en, UiGallery.IconsPage), frame, true, UiDefaultKeys.Keyboard);
        var drawn = new HashSet<UiIcon>();
        for (int i = 0; i < s.ItemCount; i++)
        {
            if (s.Items[i].Kind == UiKind.Icon)
            {
                drawn.Add(s.Items[i].Icon);
            }
        }
        var missing = new List<string>();
        for (int i = 1; i < (int)UiIcon.Count; i++)
        {
            var icon = (UiIcon)i;
            bool keyCap = icon >= UiIcon.Arrows && icon <= UiIcon.PadR1;   // the prompts' glyphs, not the family
            if (!keyCap && !drawn.Contains(icon))
            {
                missing.Add(icon.ToString());
            }
        }
        Check(missing.Count == 0, "sweep: the icon sheet draws every glyph of the family (" + (missing.Count > 0 ? "missing " + string.Join(", ", missing) : drawn.Count + " drawn") + ")");
        bool one = true;
        for (int i = 1; i < (int)UiIcon.Count; i++)
        {
            one &= UiIconTints.Of((UiIcon)i) == UiIconTints.Of((UiIcon)i);
        }
        Check(one, "sweep: a glyph has one colour wherever it is drawn (the tint is a function of the glyph alone)");
    }
}
