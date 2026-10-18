using System;
using System.Collections.Generic;
using System.Diagnostics;
using BAModAPI;
using BigAmbitions.Mods;
using Localizor;
using Localizor.LanguageChangeEvent;
using UnityEngine;

namespace RealLifeOverhaul.Meta
{
    /// <summary>
    /// Puts the options page in order. The mod API gives every mod one flat page of headers, toggles, sliders, drop-downs,
    /// buttons, splitters and rows that draw themselves, each with a label and nothing else. So the page stays as
    /// RealLifeOverhaulMod.cs builds it (no saved id, default or handler changes) and this class rearranges the finished list
    /// by <see cref="OptionsTree"/>, with <see cref="OptionsPlan"/> doing the arithmetic: groups that open to sections, one
    /// open at a time, each button counting its switches on; a row that does nothing for now drawn disabled with the reason
    /// after its label; a tooltip sentence on every option; the test tools under Troubleshooting only while asked for; a row
    /// the table does not claim under "Other", named in the log.
    ///
    /// Cost: the table is read once at start-up (<see cref="Build"/>, timed in its log line); each drawing is one pass over
    /// the mod's rows, and a click recounts its section and group. Nothing per frame. If anything fails, the page is
    /// registered as it was built.
    /// </summary>
    public static partial class OptionsLayout
    {
        private const string AboutKey = "reallife_opt_about";
        private const string LayoutTestKey = "reallife_opt_test_layout";
        /// <summary>One row of a section and what the page knows of it.</summary>
        private sealed class Row
        {
            public Section Section;
            public int Index;
            public string Id;                      // the saved option's id, or null
            public string LabelKey;
            public string TipKey;                  // the sentence for the tooltip, or null
            public string Words;                   // more words for the search
            public bool IsSwitch;
            public bool On;
            public bool HasNeeds;                  // something must be on before it does anything
            public bool Needed;                    // another row needs it
            public Func<bool> Present;             // null: always on the page
            public Feature AsideOf;                // the feature that may say it does nothing for now
            public bool Away;                      // not on the page in this drawing
            public string Reason;                  // why it does nothing just now, as shown after its label; null while it works
            public ModOption Option;               // the row as registered, a switch wrapped so that the page hears of it: what the Settings app writes through
        }

        /// <summary>A button of the page that opens and closes: a group, a section, "Test tools" or a group of test buttons.</summary>
        private sealed class Section
        {
            public string Id;                      // "g:you", "s:needs", "t", "tg:you", "ts:needs": what is open is remembered by it
            public string TitleKey;
            public string Words;                   // more words for the search
            public bool IsTests;                   // "Test tools" itself: on the page only while they are asked for
            public bool InTests;                   // a group of test buttons
            public bool IsStart;                   // "Start here": says the preset in use
            public Section Parent;
            public readonly List<Section> Children = new List<Section>();
            public readonly List<ModOption> Rows = new List<ModOption>();
            public readonly List<Row> Infos = new List<Row>();        // one per row, in step with Rows
            public GameObject Header;
            public TextLocalizationComponent HeaderText;
            public Caption Caption;                // the word on its button: "Open" or "Close"
            public int Start;
            public readonly List<GameObject> Objects = new List<GameObject>();
            public bool Matched;                   // one object per row in this drawing: rows can be greyed and explained
            public bool Dressed;                   // its rows have their tooltips in this drawing
            public int SwitchesOn;
            public int Switches;
            public string Suffix = string.Empty;   // what stands after its name
            public string[] Features;              // its features, for Core/Guard's note
            public string Shown;                   // the suffix that is on the button now

            // For the search: made on the first letters typed after the page was drawn.
            public string Text;                    // its name and the other words for it
            public string Above;                   // the names of what it stands in
            public readonly List<string> Texts = new List<string>();   // its rows' texts, one per object
        }

        /// <summary>A row that draws nothing: it is told where on the page it stands, which is how the sections find their rows.</summary>
        private sealed class RowTap : ModOption
        {
            private readonly Action<Transform> _spawned;

            public RowTap(Action<Transform> spawned) : base(null, string.Empty)
            {
                _spawned = spawned;
            }

            public override void SpawnUi(Transform parent, string modId)
            {
                try
                {
                    _spawned(parent);
                }
                catch (Exception e)
                {
                    MetaLog.Error(e);
                }
            }
        }

        private static readonly List<Section> Sections = new List<Section>();   // every one, parents before children
        private static readonly List<Section> Top = new List<Section>();        // the groups
        private static readonly List<Row> Needy = new List<Row>();              // rows that need something, or may stand aside
        private static readonly Dictionary<string, Row> ById = new Dictionary<string, Row>(StringComparer.Ordinal);
        private static readonly Dictionary<string, bool> On = new Dictionary<string, bool>(StringComparer.Ordinal);   // switches as the page last reported them
        private static ModOptions _plain;
        private static ModOptions _withTests;
        private static bool _testsShown;           // which of the two is registered
        private static string _modId;
        private static bool _hooked;
        private static Plan _plan;
        private static int _otherRows;
        private static int _testRows;
        private static double _buildMs;

        /// <summary>The features in the order of the page: as the table names them, then any it does not name.</summary>
        public static List<Feature> Ordered()
        {
            var list = new List<Feature>();
            foreach (TreeGroup group in OptionsTree.Groups)
            {
                foreach (TreeSection section in group.Sections)
                {
                    foreach (string id in section.Features)
                    {
                        Feature feature = Features.Find(id);
                        if (feature != null && !list.Contains(feature))
                        {
                            list.Add(feature);
                        }
                    }
                }
            }
            foreach (Feature feature in Features.All)
            {
                if (!list.Contains(feature))
                {
                    list.Add(feature);
                }
            }
            return list;
        }

        /// <summary>The one call from RealLifeOverhaulInit: the page as built goes in, the page in order comes out.</summary>
        public static ModOptions Organise(ModContext context, ModOptions original)
        {
            try
            {
                MetaLog.Sink = context.Logger;
                _modId = context.ModId;
                Build(original);
                if (!_hooked)
                {
                    _hooked = true;
                    OptionsService.OnReset += GameReset;
                    LocalizorManager.OnLanguageChanged += CaptionsLanguage;
                    Settings.Changed += Dev.Apply;
                }
                Dev.Apply();
                _testsShown = Settings.ShowTestTools;
                return _testsShown ? _withTests : _plain;
            }
            catch (Exception e)
            {
                MetaLog.Error(e);
                MetaLog.Warn("the options page could not be put in order and is shown as it was built.");
                _tabs = null;
                _plain = null;
                _withTests = null;
                return AsBuilt(original);
            }
        }

        /// <summary>The mod is being unloaded.</summary>
        public static void Dispose()
        {
            if (_hooked)
            {
                _hooked = false;
                OptionsService.OnReset -= GameReset;
                LocalizorManager.OnLanguageChanged -= CaptionsLanguage;
                Settings.Changed -= Dev.Apply;
            }
            _plain = null;
            _withTests = null;
            SearchDisposed();
            try
            {
                if (MetaUi.Exists)
                {
                    MetaUi.Instance.CloseAll();
                }
            }
            catch (Exception e)
            {
                MetaLog.Error(e);
            }
        }

        private static PlanKind KindOf(ModOption option)
        {
            if (option is ToggleOption)
            {
                return PlanKind.Toggle;
            }
            if (option is SliderOption)
            {
                return PlanKind.Slider;
            }
            if (option is DropdownOption)
            {
                return PlanKind.Choice;
            }
            return option is ButtonOption ? PlanKind.Button : PlanKind.Key;
        }

        private static void Build(ModOptions original)
        {
            Stopwatch watch = Stopwatch.StartNew();
            Features.EnsureRegistered();
            Sections.Clear();
            Top.Clear();
            Needy.Clear();
            ById.Clear();
            On.Clear();
            var built = new List<ModOption>();
            Feature current = null;

            // Whose a row is: the feature that owns its option id, else the one that named its button, else the one whose header it
            // stood under. A button with "_test_" in its label is a test.
            foreach (ModOption option in original.Options)
            {
                if (option is HeaderOption)
                {
                    current = Features.OwnerOfGroup(option.Label);
                    if (current == null)
                    {
                        // A group nobody registered: its rows go to "Other" and its tests to a group of their own, so nothing is lost.
                        current = Features.Add(option.Label, option.Label).Group(option.Label);
                        current.Auto = true;
                    }
                    continue;
                }
                if (option is SplitterOption)
                {
                    continue;
                }

                Feature owner;
                bool test = false;
                if (option is IPersistableOption persistable && !string.IsNullOrEmpty(persistable.Id))
                {
                    owner = Features.OwnerOfOption(persistable.Id) ?? current;
                }
                else
                {
                    Feature tester = Features.OwnerOfTest(option.Label);
                    Feature named = tester == null ? Features.OwnerOfRow(option.Label) : null;
                    if (tester != null)
                    {
                        owner = tester;
                        test = true;
                    }
                    else if (named != null)
                    {
                        owner = named;   // a button its feature named as a row is the player's, whatever its key is called
                    }
                    else if (option is ButtonOption && option.Label != null && option.Label.IndexOf("_test_", StringComparison.Ordinal) >= 0)
                    {
                        owner = current;
                        test = true;
                    }
                    else
                    {
                        owner = current;
                    }
                }
                if (owner == null)
                {
                    owner = Features.Add(OptionsTree.OtherId, "reallife_meta_title_other");
                }

                if (test && option is ButtonOption button)
                {
                    owner.AddTest(button.Label, button.OnClick);
                    continue;
                }
                SettingsTable.Harvest(option, owner);
                built.Add(SettingsTable.WithTrueDefault(option));
            }

            // Rows the features add themselves (the save tools, the diagnostics switches).
            foreach (Feature feature in Features.All)
            {
                foreach (ModOption extra in feature.ExtraRows)
                {
                    SettingsTable.Harvest(extra, feature);
                    built.Add(SettingsTable.WithTrueDefault(extra));
                }
            }
            // An option some feature owns that is on no row is still that feature's (for the guide and the presets).
            foreach (OptionInfo info in SettingsTable.All)
            {
                if (info.Owner == null)
                {
                    info.Owner = Features.OwnerOfOption(info.Id);
                }
            }
            // And the page's own buttons.
            built.Add(new ButtonOption("reallife_meta_settings_button", () => SettingsApp.Open(null)));
            built.Add(new ButtonOption("reallife_meta_guide_button", Guide.Open));
            built.Add(new ButtonOption("reallife_meta_preset_button_relaxed", () => AskPreset(Intensity.Relaxed)));
            built.Add(new ButtonOption("reallife_meta_preset_button_standard", () => AskPreset(Intensity.Standard)));
            built.Add(new ButtonOption("reallife_meta_preset_button_realistic", () => AskPreset(Intensity.Realistic)));
            built.Add(new ButtonOption("reallife_meta_preset_button_hardcore", () => AskPreset(Intensity.Hardcore)));
            built.Add(new ButtonOption("reallife_meta_reset_button", AskReset));
            built.Add(new ButtonOption("reallife_meta_export_button", ExportNow));
            built.Add(new ButtonOption("reallife_meta_import_button", AskImport));
            built.Add(new ButtonOption("reallife_meta_bug_button", BugReport.Press));
            built.Add(new ButtonOption("reallife_meta_welcome_button", () => Welcome.Open(true)));
            built.Add(new ButtonOption("reallife_guard_button", () => OptionsNotice.Tell("reallife_guard_button", Guard.Status())));   // what a game update switched off
            Features.Find("diagnostics")?.AddTest(LayoutTestKey, TestLayout);

            var rows = new List<PlanRow>(built.Count);
            foreach (ModOption option in built)
            {
                rows.Add(new PlanRow(KindOf(option), option is IPersistableOption saved ? saved.Id : null, option.Label));
            }
            _plan = OptionsPlan.Place(rows);
            _built = new Row[built.Count];
            TestPlaces.Clear();
            TestRuns.Clear();

            // The groups and their sections, in the table's order.
            Section trouble = null;
            foreach (TreeGroup group in OptionsTree.Groups)
            {
                var top = new Section { Id = "g:" + group.Id, TitleKey = group.TitleKey, IsStart = group.Id == OptionsTree.StartId };
                foreach (PlanSection placed in _plan.Sections)
                {
                    if (placed.Tree.Group != group)
                    {
                        continue;
                    }
                    Section into = top;
                    if (group.IsFlat)
                    {
                        top.Words = top.Words + " " + placed.Tree.TitleKey.GetLocalization().ToLowerInvariant() + " " + placed.Tree.Words;
                    }
                    else
                    {
                        into = new Section { Id = "s:" + placed.Tree.Id, TitleKey = placed.Tree.TitleKey, Words = placed.Tree.Words, Parent = top, Features = placed.Tree.Features };
                        top.Children.Add(into);
                    }
                    foreach (int index in placed.Rows)
                    {
                        _built[index] = AddRow(into, built[index], OptionsTree.Row(rows[index].Key));
                    }
                    Feature guide = group.IsFlat || placed.Tree.GuideFeature == null ? null : Features.Find(placed.Tree.GuideFeature);
                    if (guide != null && (guide.HelpKey != null || guide.AboutKey != null))
                    {
                        string topic = "feature_" + guide.Id;
                        AddRow(into, new ButtonOption(AboutKey, () => Guide.Open(topic)), null);
                    }
                }
                if (group.Id == OptionsTree.TroubleId)
                {
                    trouble = top;
                }
                if (top.Rows.Count > 0 || top.Children.Count > 0 || trouble == top)
                {
                    Top.Add(top);
                }
            }

            // Rows the table does not claim: shown, and named in the log, so that nothing can vanish.
            _otherRows = _plan.Other.Rows.Count;
            if (_otherRows > 0 && trouble != null)
            {
                var other = new Section { Id = "s:" + OptionsTree.OtherId, TitleKey = "reallife_meta_title_other", Parent = trouble };
                trouble.Children.Add(other);
                foreach (int index in _plan.Other.Rows)
                {
                    _built[index] = AddRow(other, built[index], null);
                    MetaLog.Warn("options page: the row " + rows[index].Key + " is in no section of Meta/OptionsTree.cs and is shown under Troubleshooting, Other.");
                }
            }
            foreach (string missing in _plan.Missing)
            {
                MetaLog.Warn("options page: Meta/OptionsTree.cs names the row " + missing + ", which the page does not have.");
            }

            _testRows = trouble != null ? AddTests(trouble) : 0;
            _tabs = SettingsRules.Tabs(_plan, rows, TestPlaces);
            _lineOf = null;
            foreach (Section top in Top)
            {
                Register(top);
            }
            foreach (Row row in Needy)
            {
                foreach (string need in OptionsPlan.NeedsOf(row.Id))
                {
                    if (ById.TryGetValue(need, out Row needed))
                    {
                        needed.Needed = true;
                    }
                }
            }

            _plain = Emit(false);
            _withTests = Emit(true);
            watch.Stop();
            _buildMs = watch.Elapsed.TotalMilliseconds;
            MetaLog.Info("options page in order: " + Top.Count + " groups, " + _plan.Sections.Count + " sections, " + _plan.Placed + " rows placed, "
                         + _otherRows + " in Other, " + _testRows + " test tools, " + SettingsTable.All.Count + " saved options, in "
                         + _buildMs.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " ms.");
        }

        private static void Register(Section section)
        {
            Sections.Add(section);
            foreach (Section child in section.Children)
            {
                Register(child);
            }
        }

        // One row into a section. A switch is wrapped so that the page hears of it: the game calls the handler with the
        // saved value each time the page is drawn, and on every click. The option's own handler runs first, unchanged.
        private static Row AddRow(Section section, ModOption option, TreeRow tree)
        {
            string id = option is IPersistableOption saved && !string.IsNullOrEmpty(saved.Id) ? saved.Id : null;
            var row = new Row { Section = section, Index = section.Rows.Count, Id = id, LabelKey = option.Label };
            if (option.Label == SettingsButtonKey)
            {
                row.Present = SettingsApp.IsInstalled;   // there is a phone to open it on only while a save is loaded
            }
            if (tree != null)
            {
                row.TipKey = tree.TipKey;
                row.Words = tree.Words;
            }
            if (id != null)
            {
                Feature owner = Features.OwnerOfOption(id);
                if (owner != null)
                {
                    owner.RowPresent.TryGetValue(id, out row.Present);
                    row.AsideOf = owner.AsideWhy != null && owner.AsideOptions.Contains(id) ? owner : null;
                }
                row.HasNeeds = OptionsPlan.NeedsOf(id).Length > 0;
                if (row.HasNeeds || row.AsideOf != null)
                {
                    Needy.Add(row);
                }
                ById[id] = row;
            }
            if (option is ToggleOption toggle)
            {
                Action<bool> inner = toggle.OnValueChanged;
                row.IsSwitch = true;
                row.On = toggle.DefaultValue;
                option = new ToggleOption(toggle.Id, toggle.Label, toggle.DefaultValue, on =>
                {
                    try
                    {
                        inner?.Invoke(on);
                    }
                    finally
                    {
                        Switched(row, on);
                    }
                });
            }
            row.Option = option;
            section.Rows.Add(option);
            section.Infos.Add(row);
            return row;
        }

        // "Test tools" under Troubleshooting: a group per group of the page, and in it one per section, each closed until it is asked for.
        private static int AddTests(Section trouble)
        {
            var tests = new Section { Id = "t", TitleKey = "reallife_meta_title_tests", IsTests = true, InTests = true, Parent = trouble };
            int count = 0;
            var claimed = new HashSet<Feature>();
            for (int g = 0; g < OptionsTree.Groups.Length; g++)
            {
                TreeGroup group = OptionsTree.Groups[g];
                var top = new Section { Id = "tg:" + group.Id, TitleKey = group.TitleKey, InTests = true, Parent = tests };
                foreach (TreeSection section in group.Sections)
                {
                    Section into = null;
                    foreach (string id in section.Features)
                    {
                        Feature feature = Features.Find(id);
                        if (feature == null || !claimed.Add(feature))
                        {
                            continue;
                        }
                        foreach (TestAction test in feature.Tests)
                        {
                            if (test.Run == null)
                            {
                                continue;   // named by a feature but not on the page
                            }
                            if (into == null)
                            {
                                into = new Section { Id = "ts:" + section.Id, TitleKey = section.TitleKey, InTests = true, Parent = top };
                                top.Children.Add(into);
                            }
                            PlaceTest(into, test, new TestPlace(g, section.Id, test.LabelKey));
                            count++;
                        }
                    }
                }
                if (top.Children.Count > 0)
                {
                    tests.Children.Add(top);
                }
            }
            // The tests of a feature no section stands for (one that registered after the table was written).
            Section rest = null;
            foreach (Feature feature in Features.All)
            {
                if (claimed.Contains(feature) || feature.Id == OptionsTree.TestsId)
                {
                    continue;
                }
                Section into = null;
                foreach (TestAction test in feature.Tests)
                {
                    if (test.Run == null)
                    {
                        continue;
                    }
                    if (rest == null)
                    {
                        rest = new Section { Id = "tg:" + OptionsTree.OtherId, TitleKey = "reallife_meta_title_other", InTests = true, Parent = tests };
                        tests.Children.Add(rest);
                    }
                    if (into == null)
                    {
                        into = new Section { Id = "ts:f:" + feature.Id, TitleKey = feature.TitleKey, InTests = true, Parent = rest };
                        rest.Children.Add(into);
                        MetaLog.Warn("options page: the feature " + feature.Id + " is in no section of Meta/OptionsTree.cs; its test tools are under Test tools, Other.");
                    }
                    PlaceTest(into, test, new TestPlace(-1, null, test.LabelKey));
                    count++;
                }
            }
            if (tests.Children.Count > 0)
            {
                trouble.Children.Add(tests);
            }
            return count;
        }

        private static void PlaceTest(Section into, TestAction test, TestPlace place)
        {
            Action run = InCity(test);
            AddRow(into, new ButtonOption(test.LabelKey, run), null);
            TestPlaces.Add(place);
            TestRuns.Add(run);
        }

        // A test stages something in the city. Without one (the main menu) its button says so, where it would do nothing.
        private static Action InCity(TestAction test)
        {
            if (test.LabelKey == LayoutTestKey)
            {
                return test.Run;   // the layout is the page's own and can be written from anywhere
            }
            Action run = test.Run;
            return () =>
            {
                if (MetaCity.Root == null)
                {
                    OptionsNotice.Tell("reallife_meta_title_tests", "reallife_opt_test_no_city".GetLocalization());
                    return;
                }
                run();
            };
        }

        // The list the game draws: the mod's header, the search box, then every group with what is in it.
        private static ModOptions Emit(bool withTests)
        {
            var page = new ModOptions();
            page.AddHeader("reallife_meta_header");
            page.AddCustom(new RowTap(DrawBegin));
            page.AddCustom(new SearchRow());
            foreach (Section top in Top)
            {
                Emit(page, top, withTests);
            }
            page.AddSplitter();
            page.AddCustom(new RowTap(DrawEnd));
            return page;
        }

        private static void Emit(ModOptions page, Section section, bool withTests)
        {
            if (section.IsTests && !withTests)
            {
                return;
            }
            page.AddButton(section.TitleKey, () => Toggle(section));
            page.AddCustom(new RowTap(parent =>
            {
                section.Header = Last(parent);
                section.HeaderText = TextOf(section.Header);
                CaptionSection(section);
                section.Start = parent.childCount;
                if (section == Top[0])
                {
                    SearchStyle(section.Header);   // the box takes its size and font from the first button under it
                }
            }));
            foreach (ModOption row in section.Rows)
            {
                page.AddCustom(row);
            }
            page.AddCustom(new RowTap(parent => RowsDrawn(section, parent)));
            foreach (Section child in section.Children)
            {
                Emit(page, child, withTests);
            }
        }
    }
}
