using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using RealLifeOverhaul;

// The RLO interface off for everyone (1.0.3; Scripts/Core/UiOffRules, Settings, Meta/Welcome). It shipped switched on, and in
// its first day players could not eat at a counter, could not pick items when ordering by phone, lost the vehicle's fuel and
// cargo and saw no Korean or Chinese text on it. So the default is off, no preset sets it, a switch saved as on is written off
// at one start and never again, and only the player it was written off for is told, once. First the rule against a pretend
// PlayerPrefs through three starts, then the sources (the default, the presets, the order at start, where the notice is
// raised, the obituary's page), then the texts.
internal static partial class Program
{
    private const string UiOffOption = "ui_interface";
    private const string UiOffNotice = "reallife_ui_note_interface_off";
    private const string UiOffSentence = "The RLO interface is now switched off by default, and has been switched off once for everyone";

    private static void UiOff()
    {
        UiOffOnce();
        UiOffDefaults();
        UiOffOrder();
        UiOffTexts();
    }

    // A pretend PlayerPrefs: the saved values by id, and every write.
    private sealed class PretendUiOffPrefs : IUiOffStore
    {
        public readonly Dictionary<string, int> Kept = new Dictionary<string, int>(StringComparer.Ordinal);
        public int Writes;

        public bool Has(string id) => Kept.ContainsKey(id);

        public int Get(string id) => Kept.TryGetValue(id, out int value) ? value : 0;

        public void Set(string id, int value)
        {
            Kept[id] = value;
            Writes++;
        }
    }

    // One run of the game for the one switch: Settings.UiInterface, what the options page shows, and the notice at a city's load.
    // stepRuns false and shipped true is 1.0.2, kept here so that the sequences below are shown to tell the two apart.
    private sealed class PretendUiOffGame
    {
        private readonly PretendUiOffPrefs _prefs;
        private readonly bool _shipped;
        public bool Field;      // Settings.UiInterface
        public bool Shown;      // the options page's row
        public int Notices;     // how often this run raised the notice

        public PretendUiOffGame(PretendUiOffPrefs prefs, bool shipped, bool stepRuns)
        {
            _prefs = prefs;
            _shipped = shipped;
            Field = shipped;   // the shipped value, until the first read
            if (stepRuns)
            {
                UiOffRules.SwitchOffOnce(_prefs, UiOffOption);   // Settings.Load: before the switch is read
            }
            Field = _prefs.Has(UiOffOption) ? _prefs.Get(UiOffOption) != 0 : Field;
        }

        // The options page opened: the game's toggle shows the saved value, else the shipped one, and sets the field so.
        public void OpenPage()
        {
            Shown = _prefs.Has(UiOffOption) ? _prefs.Get(UiOffOption) != 0 : _shipped;
            Field = Shown;
        }

        // The player's own click on the row: saved, as the game saves it.
        public void Click(bool on)
        {
            _prefs.Set(UiOffOption, on ? 1 : 0);
            Shown = on;
            Field = on;
        }

        // A city loaded: Welcome.Open raises the notice while it is owed, and notes it as shown.
        public void LoadCity()
        {
            if (UiOffRules.NoticeOwed(_prefs))
            {
                Notices++;
                UiOffRules.NoticeShown(_prefs);
            }
        }
    }

    private static void UiOffOnce()
    {
        Check(UiOffRules.Done == "ui_interface_reset" && UiOffRules.DoneAt == 103 && UiOffRules.Owed == "ui_interface_reset_note" && UiOffRules.NoticeMost == 240,
            "interface off: the step is marked by ui_interface_reset = 103 and its notice by ui_interface_reset_note, neither an option's id");

        // A player of 1.0.0 to 1.0.2 who had it saved as on: off at the first start, told once, and their own choice afterwards.
        var on = new PretendUiOffPrefs();
        on.Kept[UiOffOption] = 1;
        var first = new PretendUiOffGame(on, false, true);
        bool firstOff = !first.Field && on.Kept[UiOffOption] == 0 && on.Kept[UiOffRules.Done] == UiOffRules.DoneAt && UiOffRules.NoticeOwed(on);
        first.OpenPage();
        Check(firstOff && !first.Shown && !first.Field, "interface off: a switch saved as on is written off at the first start, and the options page shows it off");
        first.LoadCity();
        first.LoadCity();
        Check(first.Notices == 1 && !UiOffRules.NoticeOwed(on), "interface off: that player is told once, at the first city loaded, and not at the next");
        first.Click(true);
        int writes = on.Writes;
        var second = new PretendUiOffGame(on, false, true);
        second.OpenPage();
        second.LoadCity();
        Check(second.Field && second.Shown && on.Kept[UiOffOption] == 1 && on.Writes == writes && second.Notices == 0,
            "interface off: switched on again by the player, it is on at the next start, nothing is written and nothing is said");
        var third = new PretendUiOffGame(on, false, true);
        third.LoadCity();
        Check(third.Field && on.Kept[UiOffOption] == 1 && on.Writes == writes && third.Notices == 0 && !UiOffRules.SwitchOffOnce(on, UiOffOption) && on.Writes == writes,
            "interface off: and it stays on at every start after that: the step ran once");

        // The notice waits for a city: a start that ends in the main menu still owes it at the next.
        var menu = new PretendUiOffPrefs();
        menu.Kept[UiOffOption] = 1;
        var quit = new PretendUiOffGame(menu, false, true);
        var back = new PretendUiOffGame(menu, false, true);
        bool owedAcross = quit.Notices == 0 && UiOffRules.NoticeOwed(menu) && !back.Field;
        back.LoadCity();
        var later = new PretendUiOffGame(menu, false, true);
        later.LoadCity();
        Check(owedAcross && back.Notices == 1 && later.Notices == 0, "interface off: a start that never loaded a city still owes the notice at the next, once");

        // A player who had it off: nothing changes, nothing is said; only the mark is written.
        var off = new PretendUiOffPrefs();
        off.Kept[UiOffOption] = 0;
        var offGame = new PretendUiOffGame(off, false, true);
        offGame.OpenPage();
        offGame.LoadCity();
        Check(!offGame.Field && !offGame.Shown && off.Kept[UiOffOption] == 0 && off.Writes == 1 && off.Kept[UiOffRules.Done] == UiOffRules.DoneAt && !off.Has(UiOffRules.Owed) && offGame.Notices == 0,
            "interface off: a switch saved as off is left as it is, and that player gets no notice");

        // A new player, or one who never touched the row: nothing saved, the shipped off, no notice; the row is not written.
        var fresh = new PretendUiOffPrefs();
        var freshGame = new PretendUiOffGame(fresh, false, true);
        freshGame.OpenPage();
        freshGame.LoadCity();
        Check(!freshGame.Field && !freshGame.Shown && !fresh.Has(UiOffOption) && fresh.Writes == 1 && !fresh.Has(UiOffRules.Owed) && freshGame.Notices == 0,
            "interface off: with nothing saved the interface is off by its default, the option is not written and there is no notice");
        freshGame.Click(true);
        var freshNext = new PretendUiOffGame(fresh, false, true);
        freshNext.LoadCity();
        Check(freshNext.Field && freshNext.Notices == 0, "interface off: a new player who switches it on keeps it on, and is told nothing");

        // The mark alone decides: one from a later version's step keeps this one away, one below 103 does not.
        var ahead = new PretendUiOffPrefs();
        ahead.Kept[UiOffOption] = 1;
        ahead.Kept[UiOffRules.Done] = UiOffRules.DoneAt + 1;
        var behind = new PretendUiOffPrefs();
        behind.Kept[UiOffOption] = 1;
        behind.Kept[UiOffRules.Done] = UiOffRules.DoneAt - 1;
        Check(!UiOffRules.SwitchOffOnce(ahead, UiOffOption) && ahead.Kept[UiOffOption] == 1 && ahead.Writes == 0
              && UiOffRules.SwitchOffOnce(behind, UiOffOption) && behind.Kept[UiOffOption] == 0 && behind.Kept[UiOffRules.Done] == UiOffRules.DoneAt,
            "interface off: a mark of 103 or more keeps the step away, a lower one does not");

        // 1.0.2 by the same sequences: on for a new player and for one who had it on, which is what these checks are for.
        var old = new PretendUiOffPrefs();
        old.Kept[UiOffOption] = 1;
        var oldGame = new PretendUiOffGame(old, true, false);
        var oldFresh = new PretendUiOffGame(new PretendUiOffPrefs(), true, false);
        oldFresh.OpenPage();
        Check(oldGame.Field && oldFresh.Field && oldFresh.Shown, "interface off: without the step and with the old default the same sequences leave it on (the checks tell the two apart)");
    }

    // The default: off in Settings, so off on the page, in Standard, after a reset and in a settings file; and no preset sets it.
    private static void UiOffDefaults()
    {
        string settings = ModSource(@"Core\Settings.cs");
        Match shipped = Regex.Match(settings, @"public static bool UiInterface = (true|false);");
        Match living = Regex.Match(settings, @"public static bool LivingPopulation = (true|false);");
        Check(shipped.Success && shipped.Groups[1].Value == "false" && Regex.Matches(settings, @"\bUiInterface = (true|false)\b").Count == 1,
            "interface off: Settings.UiInterface ships as false, and nothing else in Settings sets it");

        // UiDirector.On at the shipped values, read from the two sources as they stand.
        string director = ModSource(@"Ui\UiDirector.cs");
        // The player's switch (Wanted) is those two alone; On adds the part running (UiGuardChecks: a part the guard switched off draws nothing).
        bool onIsBoth = director.Contains("public static bool Wanted => Settings.LivingPopulation && Settings.UiInterface;") && director.Contains("public static bool On => Wanted && !Stopped;");
        bool onAtDefaults = living.Success && shipped.Success && living.Groups[1].Value == "true" && shipped.Groups[1].Value == "true";
        Check(onIsBoth && living.Success && !onAtDefaults, "interface off: UiDirector.On is Living population and the switch and the part running, false at the shipped values");
        Check(StatementsInOrder(Block(director, "if (!On)"), "RemoveAll(\"the switch is off\");", "return;"),
            "interface off: with the switch off the director takes every kit off and installs none");

        // Every default the mod states comes from that field: the table's, the page's row, Standard, the reset.
        string table = ModSource(@"Meta\SettingsTable.cs"), presets = ModSource(@"Meta\Presets.cs");
        Check(MemberText(table, "public static void CaptureDefaults()").Contains("info.Default = value.FieldType == typeof(bool) ? ((bool)value.GetValue(null) ? 1 : 0) : (int)value.GetValue(null);")
              && MemberText(table, "public static ModOption WithTrueDefault(ModOption option)").Contains("new ToggleOption(toggle.Id, toggle.Label, info.Default != 0, toggle.OnValueChanged)")
              && StatementsInOrder(MemberText(settings, "private static void Load(string modId)"), "Meta.SettingsTable.CaptureDefaults();", "UiInterface = Bool(modId, UiInterfaceId, UiInterface);"),
            "interface off: the option's default everywhere is the shipped field, taken before any saved value is read");
        string tryValue = MemberText(presets, "public static bool TryValue(OptionInfo info, Intensity intensity, out int value)");
        Check(StatementsInOrder(tryValue, "PresetRule rule = Features.RuleFor(info.Id);", "if (rule == null || !info.HasDefault)", "return false;")
              && tryValue.Contains("default: value = info.Default; break;")
              && MemberText(presets, "public static string ResetAll()").Contains("SettingsTable.Write(info, info.Default);"),
            "interface off: a preset sets only options with a rule (Standard: the default), and reset everything writes the default");
        var setters = new List<string>();
        int presetCalls = 0;
        foreach (string file in Directory.GetFiles(ModScripts(), "*.cs", SearchOption.AllDirectories))
        {
            string text = File.ReadAllText(file);
            presetCalls += Regex.Matches(text, @"\.Preset\(Settings\.\w+Id,").Count;
            if (Regex.IsMatch(text, @"\.Preset\(\s*(Settings\.UiInterfaceId|""ui_interface"")"))
            {
                setters.Add(System.IO.Path.GetFileName(file));
            }
        }
        Check(presetCalls >= 20 && setters.Count == 0 && ModSource(@"Ui\UiGuide.cs").Contains(".Owns(Settings.UiInterfaceId, Settings.UiMotionId)"),
            "interface off: no preset has a rule for the switch, so none switches it on and each leaves the player's choice (" + presetCalls + " rules read"
            + (setters.Count > 0 ? "; set in " + string.Join(", ", setters) : string.Empty) + ")");
    }

    // The order at start, the store, and the one place the notice is raised.
    private static void UiOffOrder()
    {
        string settings = ModSource(@"Core\Settings.cs"), mod = ModSource(@"Core\RealLifeOverhaulMod.cs"), welcome = ModSource(@"Meta\Welcome.cs");
        Check(StatementsInOrder(MemberText(settings, "private static void Load(string modId)"), "SwitchInterfaceOffOnce(modId);", "UiInterface = Bool(modId, UiInterfaceId, UiInterface);")
              && StatementsInOrder(MemberText(settings, "private static void SwitchInterfaceOffOnce(string modId)"), "if (UiOffRules.SwitchOffOnce(new UiOffPrefs(modId), UiInterfaceId))", "UnityEngine.PlayerPrefs.Save();"),
            "interface off: the step runs in Settings.Load before the switch is read, and what it wrote is saved at once");
        int start = mod.IndexOf("Settings.Start(context.ModId);", StringComparison.Ordinal), built = mod.IndexOf("Guard.Add<Ui.UiDirector>(_root)", StringComparison.Ordinal);
        Check(start > 0 && built > start && StatementsInOrder(MemberText(settings, "public static void Start(string running)"), "OptionKeys.Bring(running);", "Load(OptionKeysRules.FixedId);"),
            "interface off: the options are read, an older copy's brought over first, before the interface's director is made");
        Check(StatementsInOrder(MemberText(settings, "private readonly struct UiOffPrefs : IUiOffStore"),
                  "public bool Has(string id) => UnityEngine.PlayerPrefs.HasKey(Key(_modId, id));",
                  "public int Get(string id) => UnityEngine.PlayerPrefs.GetInt(Key(_modId, id), 0);",
                  "public void Set(string id, int value) => UnityEngine.PlayerPrefs.SetInt(Key(_modId, id), value);"),
            "interface off: the store is PlayerPrefs under the key the game's own toggle reads and writes");
        Check(settings.Contains("public static bool InterfaceOffNoticeOwed => _modId != null && UiOffRules.NoticeOwed(new UiOffPrefs(_modId));")
              && StatementsInOrder(MemberText(settings, "public static void InterfaceOffNoticeShown()"), "UiOffRules.NoticeShown(new UiOffPrefs(_modId));", "UnityEngine.PlayerPrefs.Save();"),
            "interface off: whether the notice is owed is read from the store, and its showing is saved at once");
        string open = MemberText(welcome, "public static void Open(bool asked)");
        Check(welcome.Contains("PropsFailed || Settings.InterfaceOffNoticeOwed);")
              && StatementsInOrder(open, "if (Settings.InterfaceOffNoticeOwed)", "Notifications.Show(NotificationType.Info, \"" + UiOffNotice + "\", null, 20f, \"" + UiOffNotice + "\");",
                  "Settings.InterfaceOffNoticeShown();")
              && welcome.Contains("using UI.Notification;") && ModSource(@"Meta\MetaCity.cs").Contains("Welcome.Open(false);"),
            "interface off: the notice is the game's own notification, raised at a city's load only while it is owed, then noted as shown");
        int raised = 0;
        foreach (string file in Directory.GetFiles(ModScripts(), "*.cs", SearchOption.AllDirectories))
        {
            raised += Regex.Matches(File.ReadAllText(file), "\"" + UiOffNotice + "\"").Count;
        }
        Check(raised == 2, "interface off: the notice's key is named in that one call only (" + raised + ")");

        // The obituary's page with the interface off: the kit's page refuses then, so the game-styled page must be the one asked.
        string print = Block(ModSource(@"Permadeath\PermadeathDirector.Ending.cs"), "private IEnumerator Print(LifeStory story, bool test)");
        Check(print.Contains("if (Ui.UiDirector.On && Ui.UiFonts.Ready && Ui.UiScreenSwitches.On(Ui.EndingScreen.ScreenId) && Ui.UiDirector.Instance != null)")
              && StatementsInOrder(ModSource(@"Ui\UiDirector.ShellTests.cs"), "private static bool Ready()", "if (!On)", "return false;", "if (!UiFonts.Ready)", "return false;"),
            "interface off: Permadeath's obituary goes to the kit's page only while the kit would open it, else to the City Ledger's page");
    }

    private static void UiOffTexts()
    {
        Dictionary<string, string> en = LoadWords(System.IO.Path.Combine(DesignLocales, "en.json"));
        Check(en != null, "interface off texts: en.json is read");
        if (en == null)
        {
            return;
        }
        bool there = en.TryGetValue(UiOffNotice, out string notice) && notice.Length > 0;
        Check(there && notice.Length <= UiOffRules.NoticeMost, "interface off texts: the notice is in en.json and at most 240 characters (" + (there ? notice.Length : 0) + ")");
        Check(there && UiOffPlain(notice) && notice.Contains("RLO interface") && notice.Contains("switched off") && notice.Contains("switch it on again"),
            "interface off texts: the notice says it is off and how to switch it on, with no dash or exclamation mark");
        Check(RealLifeOverhaul.GameUi.NoticeKinds.Of(UiOffNotice) != RealLifeOverhaul.GameUi.NoticeKind.None
              && RealLifeOverhaul.GameUi.NoticeMuteRules.Always(UiOffNotice) && !RealLifeOverhaul.GameUi.NoticeMuteRules.Muted(UiOffNotice, false, NoticeMuteAll()),
            "interface off texts: the notice is taken for the mod's and pops up with every kind of notice switched off");
        Check(en.TryGetValue("reallife_opt_tip_ui_interface", out string tip) && tip.Contains("Off by default") && tip.Contains("switch it on here") && UiOffPlain(tip),
            "interface off texts: the option's tip says it is off by default and switched on there");
        Check(en.TryGetValue("reallife_guide_ui", out string guide) && guide.Contains("off by default") && guide.Contains("<b>RLO interface</b> in the mod's options")
              && !guide.Contains("Off, every screen is the game's own again") && UiOffPlain(guide),
            "interface off texts: the Guide's page says it is off by default and where its switch is");
        string row = en.Keys.FirstOrDefault(k => k.StartsWith("reallife_changes_", StringComparison.Ordinal) && !k.EndsWith("_title", StringComparison.Ordinal) && en[k].Contains(UiOffSentence));
        Check(row != null && UiOffPlain(en[row]), "interface off texts: What's new has the line in English");
        if (row != null)
        {
            int at = en[row].IndexOf(UiOffSentence, StringComparison.Ordinal), mark = en[row].IndexOf("Fixed: ", StringComparison.Ordinal);
            Check(at < 7 || en[row].Substring(at - 7, 7) != "Fixed: ", "interface off texts: the line is not marked as a fix (nothing was mended yet)");
            Check(row != "reallife_changes_next" || mark < 0 || at < mark, "interface off texts: the line stands before the first fix mark of the next row");
        }

        // The other languages come with the translation step: where one has the notice, it fits the toast too.
        foreach (string file in Directory.GetFiles(DesignLocales, "*.json"))
        {
            Dictionary<string, string> words = LoadWords(file);
            if (words != null && words.TryGetValue(UiOffNotice, out string theirs))
            {
                Check(theirs.Length > 0 && theirs.Length <= UiOffRules.NoticeMost && !theirs.Contains('!') && !theirs.Contains('！'),
                    "interface off texts: the notice in " + System.IO.Path.GetFileName(file) + " is at most 240 characters with no exclamation mark (" + theirs.Length + ")");
            }
        }
    }

    // No exclamation mark, em or en dash, or hyphen used as a dash.
    private static bool UiOffPlain(string text)
    {
        return !text.Contains('!') && !text.Contains((char)0x2014) && !text.Contains((char)0x2013) && !text.Contains(" - ");
    }
}
