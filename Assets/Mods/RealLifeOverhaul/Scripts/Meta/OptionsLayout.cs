using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using BAModAPI;
using BigAmbitions.Mods;
using Localizor;
using Localizor.LanguageChangeEvent;
using UnityEngine;

namespace RealLifeOverhaul.Meta
{
    /// <summary>
    /// Puts the options page in order. The mod API gives one flat page for all mods and these rows: header,
    /// toggle, slider, dropdown, button, splitter, and a custom row that draws itself; a row has a label and
    /// nothing else (no second line, no disabled state, no indent, no sub-pages, no way to jump to a row). So
    /// the page stays as RealLifeOverhaulMod.cs builds it (features keep adding their group there, and no saved
    /// option id, default or handler changes), and this class rearranges the finished list by the one table
    /// <see cref="OptionsTree"/>, with <see cref="OptionsPlan"/> doing the arithmetic:
    ///
    ///   ten groups in the order a player thinks ("Start here", "Your character and home", "The city", ...),
    ///   each a button that opens to its sections, each section a button that opens to at most ten rows, its
    ///   main switch first. One group is open at a time and one section in it; what is open is remembered
    ///   until the game is closed. Each button says how many of its switches are on.
    ///   A row whose option does nothing just now (its main switch is off, something it needs is off, another
    ///   mod has what it steers) is drawn as the game draws a control that cannot be used, with the reason
    ///   after its label ("needs: Credit rating and phone loans").
    ///   Every option has one sentence, shown by the game's own tooltip when the pointer rests on its row.
    ///   Test buttons stand only under Troubleshooting, in "Test tools", grouped as the page is; they are on
    ///   the page at all only while "Show test tools" is on (the page is registered again when that changes).
    ///   A row the table does not claim is shown under Troubleshooting in "Other" and named in the log.
    ///
    /// Under the page's first header is the search box (OptionsSearch.cs, the other half of this class).
    ///
    /// Cost: the table is read once at start-up (<see cref="Build"/>, timed in its log line). Each drawing of
    /// the page runs one pass over the mod's rows as the game makes them; a click on a switch recounts its
    /// section and group and looks again at the rows that need something (a few dictionary reads each).
    /// Nothing runs per frame. If anything here fails, the page is registered exactly as it was built.
    /// </summary>
    public static partial class OptionsLayout
    {
        private const string AboutKey = "reallife_opt_about";
        private const string LayoutTestKey = "reallife_opt_test_layout";
        private const float BlockedAlpha = 0.5f;   // a label beside a control that cannot be used, when the control itself does not say how faint

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
            public string[] Features;              // the features of Meta/FeatureList it is the section of (for Core/Guard's note)
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
        private static readonly Dictionary<string, string> OpenChild = new Dictionary<string, string>(StringComparer.Ordinal)   // parent id: the one child that is open (until the game is closed)
        {
            { string.Empty, "g:" + OptionsTree.StartId }
        };
        private static readonly Dictionary<Feature, string> AsideNow = new Dictionary<Feature, string>();   // of this drawing
        private static readonly HashSet<GameObject> Absent = new HashSet<GameObject>();   // rows of this drawing that are not on the page (Feature.Present said no)
        private static readonly Func<string, bool> IsOnNow = IsOn;
        private static ModOptions _plain;
        private static ModOptions _withTests;
        private static bool _testsShown;           // which of the two is registered
        private static string _modId;
        private static bool _hooked;
        private static Plan _plan;
        private static int _otherRows;
        private static int _testRows;
        private static double _buildMs;
        private static int _drawFrame = -1;        // the frame the page is being drawn in; -1 once it is drawn
        private static long _drawStarted;
        private static string _countFormat;
        private static string _needsFormat;

        // ---------------------------------------------------------------- order

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

        // ---------------------------------------------------------------- building

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

            // Whose a row is, as before: the feature that owns its option id; else the one that named its button; else
            // the one whose header it stood under. A button whose label has "_test_" in it is a test.
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
            built.Add(new ButtonOption("reallife_meta_guide_button", Guide.Open));
            built.Add(new ButtonOption("reallife_meta_preset_button_relaxed", () => AskPreset(Intensity.Relaxed)));
            built.Add(new ButtonOption("reallife_meta_preset_button_standard", () => AskPreset(Intensity.Standard)));
            built.Add(new ButtonOption("reallife_meta_preset_button_realistic", () => AskPreset(Intensity.Realistic)));
            built.Add(new ButtonOption("reallife_meta_preset_button_hardcore", () => AskPreset(Intensity.Hardcore)));
            built.Add(new ButtonOption("reallife_meta_reset_button", AskReset));
            built.Add(new ButtonOption("reallife_meta_export_button", ExportNow));
            built.Add(new ButtonOption("reallife_meta_import_button", AskImport));
            built.Add(new ButtonOption("reallife_meta_welcome_button", () => Welcome.Open(true)));
            built.Add(new ButtonOption("reallife_guard_button", () => OptionsNotice.Tell("reallife_guard_button", Guard.Status())));   // what a game update switched off (Core/Guard)
            Features.Find("diagnostics")?.AddTest(LayoutTestKey, TestLayout);

            var rows = new List<PlanRow>(built.Count);
            foreach (ModOption option in built)
            {
                rows.Add(new PlanRow(KindOf(option), option is IPersistableOption saved ? saved.Id : null, option.Label));
            }
            _plan = OptionsPlan.Place(rows);

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
                        AddRow(into, built[index], OptionsTree.Row(rows[index].Key));
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
                    AddRow(other, built[index], null);
                    MetaLog.Warn("options page: the row " + rows[index].Key + " is in no section of Meta/OptionsTree.cs and is shown under Troubleshooting, Other.");
                }
            }
            foreach (string missing in _plan.Missing)
            {
                MetaLog.Warn("options page: Meta/OptionsTree.cs names the row " + missing + ", which the page does not have.");
            }

            _testRows = trouble != null ? AddTests(trouble) : 0;
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
        private static void AddRow(Section section, ModOption option, TreeRow tree)
        {
            string id = option is IPersistableOption saved && !string.IsNullOrEmpty(saved.Id) ? saved.Id : null;
            var row = new Row { Section = section, Index = section.Rows.Count, Id = id, LabelKey = option.Label };
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
            section.Rows.Add(option);
            section.Infos.Add(row);
        }

        // "Test tools" under Troubleshooting: a group per group of the page, and in it one per section, each closed until it is asked for.
        private static int AddTests(Section trouble)
        {
            var tests = new Section { Id = "t", TitleKey = "reallife_meta_title_tests", IsTests = true, InTests = true, Parent = trouble };
            int count = 0;
            var claimed = new HashSet<Feature>();
            foreach (TreeGroup group in OptionsTree.Groups)
            {
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
                            AddRow(into, new ButtonOption(test.LabelKey, InCity(test)), null);
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
                    AddRow(into, new ButtonOption(test.LabelKey, InCity(test)), null);
                    count++;
                }
            }
            if (tests.Children.Count > 0)
            {
                trouble.Children.Add(tests);
            }
            return count;
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

        // ---------------------------------------------------------------- the page being drawn

        private static bool Drawing => _drawFrame == Time.frameCount;

        // The first of the mod's rows is being made: the rows of the last drawing are on their way out, and nothing may switch them back on.
        private static void DrawBegin(Transform parent)
        {
            _drawFrame = Time.frameCount;
            _drawStarted = Stopwatch.GetTimestamp();
            foreach (Section section in Sections)
            {
                section.Header = null;
                section.HeaderText = null;
                section.Shown = null;
                section.Objects.Clear();
                section.Matched = false;
                section.Dressed = false;
                foreach (Row row in section.Infos)
                {
                    row.Reason = null;
                    row.Away = false;
                }
            }
            Absent.Clear();
            AsideNow.Clear();
            OtherMods.Look(MetaLog.Sink);   // a row may be there only with another author's mod: looked for when the page is drawn
            _countFormat = "reallife_opt_count".GetLocalization();
            _needsFormat = "reallife_opt_needs".GetLocalization();
            CaptionsBegin(parent);
            SearchDrawn(parent);
        }

        // A section's rows have been made: which object is which row, and which rows are not on the page this time.
        private static void RowsDrawn(Section section, Transform parent)
        {
            section.Objects.Clear();
            for (int i = section.Start; i < parent.childCount; i++)
            {
                section.Objects.Add(parent.GetChild(i).gameObject);
            }
            // One object per row is what the game makes. Should a row have made none or several, the section still opens and closes as a whole.
            section.Matched = section.Objects.Count == section.Rows.Count;
            if (!section.Matched)
            {
                return;
            }
            for (int i = 0; i < section.Infos.Count; i++)
            {
                Row row = section.Infos[i];
                if (row.Present != null && !row.Present())
                {
                    row.Away = true;
                    Absent.Add(section.Objects[i]);
                    section.Objects[i].SetActive(false);
                }
            }
        }

        // The last of the mod's rows has been made: every switch has reported, so what is greyed, the counts and what is open can be set.
        private static void DrawEnd(Transform parent)
        {
            CaptionsEnd(parent);
            _drawFrame = -1;
            try
            {
                foreach (Row row in Needy)
                {
                    Block(row);
                }
                foreach (Section top in Top)
                {
                    Count(top);
                }
            }
            catch (Exception e)
            {
                MetaLog.Error(e);   // the groups must still close what is not open: a page with every row shown is the wall this class is there to prevent
            }
            foreach (Section top in Top)
            {
                Apply(top);
            }
            if (Dev.Verbose)
            {
                double drawMs = (Stopwatch.GetTimestamp() - _drawStarted) * 1000.0 / Stopwatch.Frequency;
                int drawn = 0;
                int unmatched = 0;
                foreach (Section section in Sections)
                {
                    drawn += section.Objects.Count;
                    unmatched += section.Header != null && !section.Matched ? 1 : 0;
                }
                bool pass = _otherRows == 0 && unmatched == 0 && _plan != null && _plan.Missing.Count == 0;
                MetaLog.Info("Options check (page drawn): " + Top.Count + " groups, " + (_plan != null ? _plan.Sections.Count : 0) + " sections, "
                             + (_plan != null ? _plan.Placed : 0) + " rows placed, " + _otherRows + " in Other (wanted 0), " + drawn + " row objects, "
                             + unmatched + " sections whose rows did not match their objects (wanted 0), test tools " + (_testsShown ? "on the page" : "left off")
                             + ", put in order in " + _buildMs.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " ms, drawn in "
                             + drawMs.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " ms: " + (pass ? "PASS" : "FAIL") + ".");
            }
        }

        private static GameObject Last(Transform parent)
        {
            return parent != null && parent.childCount > 0 ? parent.GetChild(parent.childCount - 1).gameObject : null;
        }

        private static TextLocalizationComponent TextOf(GameObject row)
        {
            return row != null ? row.GetComponentInChildren<TextLocalizationComponent>(true) : null;
        }

        // The text of a row that shows its label (a slider has a second one for its number).
        private static TextLocalizationComponent LabelOf(GameObject row, string labelKey)
        {
            if (row == null)
            {
                return null;
            }
            TextLocalizationComponent first = null;
            foreach (TextLocalizationComponent text in row.GetComponentsInChildren<TextLocalizationComponent>(true))
            {
                if (text.Key == labelKey)
                {
                    return text;
                }
                first = first ?? text;
            }
            return first;
        }

        // ---------------------------------------------------------------- switches, counts, rows that do nothing for now

        private static bool IsOn(string optionId)
        {
            if (On.TryGetValue(optionId, out bool on))
            {
                return on;
            }
            OptionInfo info = SettingsTable.Get(optionId);
            return info == null || SettingsTable.Current(info) != 0;   // an option the page does not know keeps nothing from working
        }

        // A switch reported: with its saved value while the page is drawn, or because it was clicked.
        private static void Switched(Row row, bool on)
        {
            try
            {
                row.On = on;
                if (row.Id != null)
                {
                    On[row.Id] = on;
                }
                if (Drawing)
                {
                    return;   // DrawEnd counts and greys everything once, when all have reported
                }
                if (row.Needed)
                {
                    foreach (Row needy in Needy)
                    {
                        Block(needy);
                    }
                }
                for (Section section = row.Section; section != null; section = section.Parent)
                {
                    CountOwn(section);
                    Label(section);
                }
                if (Top.Count > 0 && Top[0].IsStart)
                {
                    CountOwn(Top[0]);   // the preset in use may be another one now
                    Label(Top[0]);
                }
            }
            catch (Exception e)
            {
                MetaLog.Error(e);
            }
        }

        // Why a row does nothing just now, or null: another mod has what it steers, or something it needs is off.
        private static string ReasonFor(Row row)
        {
            if (row.AsideOf != null)
            {
                if (!AsideNow.TryGetValue(row.AsideOf, out string aside))
                {
                    aside = row.AsideOf.AsideWhy();
                    AsideNow[row.AsideOf] = aside;
                }
                if (aside != null)
                {
                    return aside;
                }
            }
            if (!row.HasNeeds)
            {
                return null;
            }
            string blocker = OptionsPlan.Blocker(row.Id, IsOnNow);
            if (blocker == null)
            {
                return null;
            }
            string label = ById.TryGetValue(blocker, out Row needed) ? needed.LabelKey.GetLocalization() : blocker;
            return _needsFormat.Replace("{option}", label);
        }

        // Draws a row as the game draws a control that cannot be used, with the reason after its label; or puts it back.
        private static void Block(Row row)
        {
            Section section = row.Section;
            if (!section.Matched || row.Away)
            {
                return;
            }
            string reason = ReasonFor(row);
            if (reason == row.Reason)
            {
                return;
            }
            row.Reason = reason;
            GameObject item = section.Objects[row.Index];
            if (item == null)
            {
                return;
            }
            float faint = BlockedAlpha;
            foreach (UnityEngine.UI.Selectable control in item.GetComponentsInChildren<UnityEngine.UI.Selectable>(true))
            {
                control.interactable = reason == null;
                faint = control.colors.disabledColor.a;
            }
            TextLocalizationComponent label = LabelOf(item, row.LabelKey);
            if (label != null)
            {
                label.Suffix = reason == null ? string.Empty : "   (" + reason + ")";
                if (label.TextContainer != null)
                {
                    label.TextContainer.alpha = reason == null ? 1f : Mathf.Clamp(faint, 0.35f, 0.8f);
                }
            }
        }

        // Counts a section's switches and those of what is in it, and says so after its name.
        private static void Count(Section section)
        {
            foreach (Section child in section.Children)
            {
                Count(child);
            }
            CountOwn(section);
        }

        private static void CountOwn(Section section)
        {
            int on = 0;
            int all = 0;
            foreach (Row row in section.Infos)
            {
                if (row.IsSwitch && !row.Away)
                {
                    all++;
                    on += row.On ? 1 : 0;
                }
            }
            int buttons = section.Infos.Count;
            foreach (Section child in section.Children)
            {
                if (child.IsTests)
                {
                    continue;   // the test tools are counted where they stand
                }
                on += child.SwitchesOn;
                all += child.Switches;
                buttons += child.Switches;   // (a group of test buttons keeps its number of buttons there)
            }
            section.SwitchesOn = on;
            section.Switches = section.InTests ? buttons : all;
            if (section.InTests)
            {
                section.Suffix = "   (" + section.Switches + ")";
            }
            else if (section.IsStart)
            {
                section.Suffix = ":  " + Presets.CurrentName() + (Guard.AnyOff ? "   (" + Guard.Note(null) + ")" : string.Empty);
            }
            else
            {
                section.Suffix = all == 0 ? string.Empty : _countFormat.Replace("{on}", on.ToString()).Replace("{total}", all.ToString());
                string note = GuardNote(section);
                if (note != null)
                {
                    section.Suffix += "   (" + note + ")";   // a part of it is off after a game update, or it was switched off after its faults
                }
            }
        }

        // Core/Guard's note for the first of a section's features that has a part off, or null.
        private static string GuardNote(Section section)
        {
            if (section.Features == null)
            {
                return null;
            }
            foreach (string feature in section.Features)
            {
                string note = Guard.Note(feature);
                if (note != null)
                {
                    return note;
                }
            }
            return null;
        }

        // The text after the name on a section's button, written only when it is another one.
        private static void Label(Section section)
        {
            if (section.HeaderText != null && section.Shown != section.Suffix)
            {
                section.Shown = section.Suffix;
                section.HeaderText.Suffix = section.Suffix;
            }
        }

        // The game's own tooltip on every row of a section that has a sentence: made the first time the section is shown in a drawing.
        private static void Dress(Section section)
        {
            if (section.Dressed || !section.Matched)
            {
                return;
            }
            section.Dressed = true;
            for (int i = 0; i < section.Infos.Count; i++)
            {
                Row row = section.Infos[i];
                GameObject item = section.Objects[i];
                if (row.TipKey == null || item == null || item.GetComponent<BasicTooltip>() != null)
                {
                    continue;
                }
                BasicTooltip tip = item.AddComponent<BasicTooltip>();
                tip.descriptionKey = row.TipKey;
            }
        }

        // ---------------------------------------------------------------- open and closed

        private static void Show(GameObject row, bool on)
        {
            on = on && !Absent.Contains(row);   // a row that is not on the page stays away, whatever opens or is found
            if (row != null && row.activeSelf != on)
            {
                row.SetActive(on);
            }
        }

        private static string ParentId(Section section)
        {
            return section.Parent != null ? section.Parent.Id : string.Empty;
        }

        private static bool IsOpen(Section section)
        {
            return OpenChild.TryGetValue(ParentId(section), out string open) && open == section.Id;
        }

        // One group is open at a time, and one section in it: opening one closes the one that was.
        private static void Toggle(Section section)
        {
            try
            {
                if (Searching)
                {
                    return;   // what is shown is what was found; what is open and closed stays as it was left
                }
                string parent = ParentId(section);
                bool open = !IsOpen(section);
                bool closedAbove = false;
                if (open && OpenChild.TryGetValue(parent, out string was))
                {
                    foreach (Section beside in section.Parent != null ? section.Parent.Children : Top)
                    {
                        if (beside == section)
                        {
                            break;
                        }
                        closedAbove |= beside.Id == was;
                    }
                }
                if (open)
                {
                    OpenChild[parent] = section.Id;
                }
                else
                {
                    OpenChild.Remove(parent);
                }
                foreach (Section top in Top)
                {
                    Apply(top);
                }
                if (closedAbove && _box != null)
                {
                    _box.ScrollTo(section.Header);   // what closed above it took its rows away: the one just opened goes to the top of the view
                }
            }
            catch (Exception e)
            {
                MetaLog.Error(e);
            }
        }

        // Is the section's own button to be seen: a group always, anything else while what it stands in is open.
        private static bool Visible(Section section)
        {
            return section.Parent == null || (Visible(section.Parent) && IsOpen(section.Parent));
        }

        // Shows or hides a section's rows, and what is inside it. The objects are the game's; one that has gone (the page was closed) is skipped.
        private static void Apply(Section section)
        {
            if (Searching)
            {
                return;   // the search decides until the box is empty again, and then calls this
            }
            bool visible = Visible(section);
            bool open = visible && IsOpen(section);
            Show(section.Header, visible);
            Label(section);
            CaptionState(section, open ? CaptionClose : CaptionOpen);
            if (open)
            {
                Dress(section);
            }
            foreach (GameObject row in section.Objects)
            {
                Show(row, open);
            }
            foreach (Section child in section.Children)
            {
                Apply(child);
            }
        }

        /// <summary>After "Show test tools" changed: the page with them, or without, is registered in place of the other on the next frame.</summary>
        public static void ApplyAll()
        {
            if (_plain == null || _modId == null || Settings.ShowTestTools == _testsShown)
            {
                return;
            }
            Refresh();
        }

        /// <summary>
        /// Option values were changed from outside the page (a preset, an import, a reset), or the test tools were
        /// asked for: if the page is registered it is drawn again on the next frame, which is the game's own way
        /// of showing saved values.
        /// </summary>
        public static void Refresh()
        {
            if (_plain == null || _modId == null || !OptionsService.RegisteredEntries.ContainsKey(_modId))
            {
                return;
            }
            MetaUi.Instance.NextFrame(() =>
            {
                if (_plain != null && OptionsService.RegisteredEntries.ContainsKey(_modId))
                {
                    _testsShown = Settings.ShowTestTools;
                    OptionsService.Register(_modId, Guard.Rows(_testsShown ? _withTests : _plain));
                }
            });
        }

        // The game's own "reset all options": it clears the saved values and draws the page again, and every row then
        // reports its default (the rows carry the shipped values, see SettingsTable.WithTrueDefault). A frame later the
        // options are read again so that whoever listens to Settings.Changed hears of it.
        private static void GameReset()
        {
            try
            {
                MetaUi.Instance.NextFrame(() =>
                {
                    Settings.Reload();
                    ApplyAll();
                });
            }
            catch (Exception e)
            {
                MetaLog.Error(e);
            }
        }

        // ---------------------------------------------------------------- the buttons of "Start here"

        // A preset is asked first, with the game's own question, which says how many options it will change.
        private static void AskPreset(Intensity intensity)
        {
            string about;
            switch (intensity)
            {
                case Intensity.Relaxed: about = "reallife_meta_preset_about_relaxed"; break;
                case Intensity.Realistic: about = "reallife_meta_preset_about_realistic"; break;
                case Intensity.Hardcore: about = "reallife_meta_preset_about_hardcore"; break;
                default: about = "reallife_meta_preset_about_standard"; break;
            }
            int switches;
            int changes = Presets.CountChanges(intensity, out switches);
            string body = "reallife_opt_ask_preset".GetLocalization()
                .Replace("{about}", about.GetLocalization())
                .Replace("{count}", changes.ToString())
                .Replace("{switches}", switches.ToString());
            OptionsNotice.Ask(Presets.NameKey(intensity), body, "reallife_meta_preset_yes", false, () => OptionsNotice.Tell(Presets.NameKey(intensity), Presets.Apply(intensity)));
        }

        private static void AskReset()
        {
            OptionsNotice.Ask("reallife_meta_reset_title", "reallife_meta_reset_explain".GetLocalization(), "reallife_meta_reset_yes", true,
                () => OptionsNotice.Tell("reallife_meta_reset_title", Presets.ResetAll()));
        }

        private static void AskImport()
        {
            OptionsNotice.Ask("reallife_meta_import_title",
                "reallife_meta_import_explain".GetLocalization().Replace("{file}", System.IO.Path.Combine(Presets.Folder, Presets.SettingsFile)),
                "reallife_meta_import_yes", false, () => OptionsNotice.Tell("reallife_meta_import_title", Presets.Import()));
        }

        private static void ExportNow()
        {
            try
            {
                string path = Presets.Export(Presets.SettingsFile);
                MetaLog.Info("settings written to " + path);
                OptionsNotice.Tell("reallife_meta_export_title", "reallife_meta_export_done".GetLocalization().Replace("{file}", path));
            }
            catch (Exception e)
            {
                MetaLog.Error(e);
                OptionsNotice.Tell("reallife_meta_export_title", "reallife_meta_export_failed".GetLocalization().Replace("{error}", e.Message));
            }
        }

        // ---------------------------------------------------------------- the layout as text

        /// <summary>The whole page as it is arranged, one line per group, section and row, for Player.log.</summary>
        public static string Describe()
        {
            var text = new StringBuilder(8192);
            text.Append("the options page: ").Append(Top.Count).Append(" groups, ").Append(_plan != null ? _plan.Sections.Count : 0).Append(" sections, ")
                .Append(_plan != null ? _plan.Placed : 0).Append(" rows placed, ").Append(_otherRows).Append(" in Other, ").Append(_testRows).Append(" test tools\n");
            foreach (Section top in Top)
            {
                Describe(text, top, 0);
            }
            return text.ToString();
        }

        private static void Describe(StringBuilder text, Section section, int depth)
        {
            text.Append(' ', depth * 2).Append(depth == 0 ? "# " : "+ ").Append(section.TitleKey.GetLocalization()).Append("   [").Append(section.Id).Append(']');
            if (IsOpen(section))
            {
                text.Append(" (open)");
            }
            text.Append('\n');
            foreach (Row row in section.Infos)
            {
                text.Append(' ', depth * 2 + 2).Append("- ").Append(row.LabelKey.GetLocalization());
                if (row.Id != null)
                {
                    text.Append("   [").Append(row.Id).Append(row.IsSwitch ? (row.On ? ": on" : ": off") : string.Empty).Append(']');
                    string[] needs = OptionsPlan.NeedsOf(row.Id);
                    if (needs.Length > 0)
                    {
                        text.Append(" needs ").Append(string.Join(", ", needs));
                    }
                }
                text.Append('\n');
            }
            foreach (Section child in section.Children)
            {
                Describe(text, child, depth + 1);
            }
        }

        // The test button: the layout goes to Player.log whether or not "Developer diagnostics" is on, since it was asked for.
        private static void TestLayout()
        {
            try
            {
                UnityEngine.Debug.Log("[RealLifeOverhaul] Meta: " + Describe());
                OptionsNotice.Tell("reallife_opt_test_layout", "reallife_opt_test_layout_done".GetLocalization());
            }
            catch (Exception e)
            {
                MetaLog.Error(e);
            }
        }

        // ---------------------------------------------------------------- the set-up check

        /// <summary>
        /// The "Check the mod's set-up" button: looks through the registry, the presets, the options page and the
        /// guide for anything that does not fit together, and says what it found. For whoever merges a feature.
        /// </summary>
        public static void CheckSetup()
        {
            var problems = new List<string>();
            int rules = 0;
            try
            {
                foreach (Feature feature in Features.All)
                {
                    foreach (string key in new[] { feature.TitleKey, feature.AboutKey, feature.HelpKey })
                    {
                        if (key != null && key.GetLocalization() == key)
                        {
                            problems.Add(feature.Id + ": the text " + key + " is not in the locale file");
                        }
                    }
                    if (feature.Auto)
                    {
                        problems.Add(feature.Id + ": this options group is not registered (it works, but has no guide page, presets or clean-up)");
                    }
                    else if (feature.Id != OptionsTree.TestsId && feature.Id != OptionsTree.OtherId && OptionsTree.SectionOfFeature(feature.Id) == null)
                    {
                        problems.Add(feature.Id + ": no section of Meta/OptionsTree.cs stands for this feature (its test tools are under Other)");
                    }
                    foreach (PresetRule rule in feature.Presets)
                    {
                        rules++;
                        OptionInfo info = SettingsTable.Get(rule.OptionId);
                        if (info == null || !info.HasDefault)
                        {
                            problems.Add(feature.Id + ": the preset names the option " + rule.OptionId + ", which has no known default");
                            continue;
                        }
                        if (info.LabelKey == null)
                        {
                            problems.Add(feature.Id + ": the option " + rule.OptionId + " is in the presets but not on the options page");
                        }
                        foreach (int value in new[] { rule.Relaxed, rule.Realistic, rule.Hardcore })
                        {
                            if (SettingsTable.Clamp(info, value) != value)
                            {
                                problems.Add(feature.Id + ": the preset value " + value + " for " + rule.OptionId + " is outside " + info.Min + ".." + info.Max);
                            }
                        }
                    }
                    foreach (string id in feature.Options)
                    {
                        if (SettingsTable.Get(id) == null)
                        {
                            problems.Add(feature.Id + ": owns the option " + id + ", which does not exist");
                        }
                    }
                    foreach (string id in feature.ReloadOptions)
                    {
                        if (SettingsTable.Get(id) == null)
                        {
                            problems.Add(feature.Id + ": marks the option " + id + " as needing a reload, which does not exist");
                        }
                    }
                    foreach (TestAction test in feature.Tests)
                    {
                        if (test.Run == null)
                        {
                            problems.Add(feature.Id + ": names the test button " + test.LabelKey + ", which is not on the page and has no action");
                        }
                    }
                    foreach (string contact in feature.ContactIds)
                    {
                        if (contact.GetLocalization() == contact)
                        {
                            problems.Add(feature.Id + ": the contact " + contact + " has no name in the locale file");
                        }
                    }
                }
                foreach (OptionInfo info in SettingsTable.All)
                {
                    if (info.LabelKey != null && info.Owner != null && info.Owner.Auto)
                    {
                        continue;   // already said once for its group
                    }
                    if (info.Field != null && info.LabelKey == null)
                    {
                        problems.Add("the option " + info.Id + " is in Settings but on no row of the options page");
                    }
                }
                if (_plan != null)
                {
                    foreach (Section section in Sections)
                    {
                        foreach (Row row in section.Infos)
                        {
                            if (section.Id == "s:" + OptionsTree.OtherId)
                            {
                                problems.Add("the row " + (row.Id ?? row.LabelKey) + " is in no section of Meta/OptionsTree.cs");
                            }
                            if (row.TipKey != null && row.TipKey.GetLocalization() == row.TipKey)
                            {
                                problems.Add("the option " + row.Id + " has no sentence " + row.TipKey + " in the locale file");
                            }
                        }
                    }
                    foreach (string missing in _plan.Missing)
                    {
                        problems.Add("Meta/OptionsTree.cs names the row " + missing + ", which the page does not have");
                    }
                    string cycle = OptionsPlan.FindCycle();
                    if (cycle != null)
                    {
                        problems.Add("options that need each other in a circle: " + cycle);
                    }
                }
                Guide.Check(problems);
            }
            catch (Exception e)
            {
                MetaLog.Error(e);
                problems.Add("the check itself failed: " + e.Message);
            }

            string summary = "reallife_meta_check_summary".GetLocalization()
                .Replace("{features}", Features.All.Count.ToString())
                .Replace("{options}", SettingsTable.All.Count.ToString())
                .Replace("{rules}", rules.ToString())
                .Replace("{problems}", problems.Count.ToString());
            foreach (string problem in problems)
            {
                MetaLog.Warn("set-up check: " + problem);
            }
            MetaLog.Info("set-up check: " + problems.Count + " problem(s).");
            OptionsNotice.Tell("reallife_meta_check_title", summary);
        }
    }
}
