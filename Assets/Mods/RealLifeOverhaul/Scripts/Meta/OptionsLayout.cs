using System;
using System.Collections.Generic;
using BAModAPI;
using BigAmbitions.Mods;
using Localizor;
using Localizor.LanguageChangeEvent;
using UnityEngine;

namespace RealLifeOverhaul.Meta
{
    /// <summary>
    /// Puts the options page in order. The mod API gives one flat page for all mods and these rows: header,
    /// toggle, slider, dropdown, button, splitter, and a custom row that draws itself; no text block, no
    /// sub-pages, no way to jump to a row. So the page stays as RealLifeOverhaulMod.cs builds it (features keep
    /// adding their group there, and no saved option id changes), and this class rearranges the finished list:
    ///
    ///   Guide, welcome, presets, reset, export and import first;
    ///   then one section per feature in alphabetical order, each a button that shows and hides its rows
    ///   (the rows are the game's own objects, switched on and off: nothing is rebuilt);
    ///   then Controls, Performance, the save tools, Diagnostics, and last "Test tools": every test button of
    ///   every feature, one group per feature that opens and closes like a section, there only while "Show test
    ///   tools" is on.
    ///
    /// Under the page's first header is the search box (OptionsSearch.cs, the other half of this class): while
    /// something is typed there, what was typed decides which rows are shown, and the sections do not.
    ///
    /// Which section a row goes to: the feature that owns its option id; else the feature that named its button;
    /// else the feature whose header it stood under; a header no feature claims becomes a section of its own, so
    /// a feature that registers nothing is still there. A button whose label has "_test_" in it is a test.
    /// If anything here fails, the page is registered exactly as it was built.
    /// </summary>
    public static partial class OptionsLayout
    {
        private const string TestsId = "tests";

        private sealed class Section
        {
            public Feature Feature;
            public bool IsTests;
            public Section Parent;                 // a group of test buttons is inside "Test tools"
            public readonly List<Section> Children = new List<Section>();
            public readonly List<ModOption> Rows = new List<ModOption>();
            public GameObject Header;
            public TextLocalizationComponent HeaderText;
            public int Start;
            public readonly List<GameObject> Objects = new List<GameObject>();

            // What stands after the name on its button: how much is in it, then open or closed. Made when the page is drawn.
            public string Closed = "   [+]";
            public string Opened = "   [-]";
            public string Found = string.Empty;
            public string Shown;                   // the one of the three that is on the button now
            public bool HasMaster;                 // one of its rows switches the whole feature
            public bool MasterOn;

            // For the search: made on the first letters typed after the page was drawn.
            public string Text;                    // its name, what it is, other words for it
            public readonly List<string> Texts = new List<string>();   // its rows' labels, one per object
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

        private static readonly List<Section> Sections = new List<Section>();
        private static ModOptions _built;
        private static string _modId;
        private static bool _hooked;
        private static TextLocalizationComponent _presetText;
        private static TextLocalizationComponent _allText;

        // ---------------------------------------------------------------- order

        /// <summary>The features in the order of the page: gameplay alphabetically, then controls, performance, tools.</summary>
        public static List<Feature> Ordered()
        {
            var list = new List<Feature>(Features.All);
            var position = new Dictionary<Feature, int>();
            for (int i = 0; i < list.Count; i++)
            {
                position[list[i]] = i;
            }
            list.Sort((a, b) =>
            {
                if (a.Kind != b.Kind)
                {
                    return a.Kind.CompareTo(b.Kind);
                }
                if (a.Kind == FeatureKind.Gameplay)
                {
                    int byName = string.CompareOrdinal(SortName(a), SortName(b));
                    if (byName != 0)
                    {
                        return byName;
                    }
                }
                return position[a].CompareTo(position[b]);
            });
            return list;
        }

        // Registered features have plain English ids ("camera", "citylife"); one made from a header is sorted by the header's middle.
        private static string SortName(Feature feature)
        {
            string name = feature.Id;
            if (feature.Auto)
            {
                if (SaveTools.IsOurs(name))
                {
                    name = name.Substring(9);   // past the prefix every key of the mod's has
                }
                name = name.Replace("_header", string.Empty);
            }
            return name;
        }

        // ---------------------------------------------------------------- building

        /// <summary>The one call from RealLifeOverhaulInit: the page as built goes in, the page in order comes out.</summary>
        public static ModOptions Organise(ModContext context, ModOptions original)
        {
            try
            {
                MetaLog.Sink = context.Logger;
                _modId = context.ModId;
                _built = Build(original);
                if (!_hooked)
                {
                    _hooked = true;
                    OptionsService.OnReset += GameReset;
                    Settings.Changed += Dev.Apply;
                }
                Dev.Apply();
                return _built;
            }
            catch (Exception e)
            {
                MetaLog.Error(e);
                MetaLog.Warn("the options page could not be put in order and is shown as it was built.");
                _built = null;
                return original;
            }
        }

        /// <summary>The mod is being unloaded.</summary>
        public static void Dispose()
        {
            if (_hooked)
            {
                _hooked = false;
                OptionsService.OnReset -= GameReset;
                Settings.Changed -= Dev.Apply;
            }
            _built = null;
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

        private static Section SectionOf(Feature feature, Dictionary<Feature, Section> byFeature)
        {
            if (!byFeature.TryGetValue(feature, out Section section))
            {
                section = new Section { Feature = feature };
                byFeature[feature] = section;
            }
            return section;
        }

        private static ModOptions Build(ModOptions original)
        {
            Features.EnsureRegistered();
            Sections.Clear();
            Loose.Clear();
            var byFeature = new Dictionary<Feature, Section>();
            Feature current = null;

            foreach (ModOption option in original.Options)
            {
                if (option is HeaderOption)
                {
                    current = Features.OwnerOfGroup(option.Label);
                    if (current == null)
                    {
                        // A group nobody registered: a section of its own under the header's own text.
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
                    owner = Features.Add("other", "reallife_meta_title_other");
                }

                if (test && option is ButtonOption button)
                {
                    owner.AddTest(button.Label, button.OnClick);
                    continue;
                }
                SettingsTable.Harvest(option, owner);
                SectionOf(owner, byFeature).Rows.Add(SettingsTable.WithTrueDefault(option));
            }

            // Rows the features add themselves (the save tools, the diagnostics switches).
            foreach (Feature feature in Features.All)
            {
                foreach (ModOption extra in feature.ExtraRows)
                {
                    SettingsTable.Harvest(extra, feature);
                    SectionOf(feature, byFeature).Rows.Add(SettingsTable.WithTrueDefault(extra));
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

            var page = new ModOptions();
            page.AddHeader("reallife_meta_header");
            page.AddCustom(new RowTap(parent =>
            {
                // The page is being drawn again: the rows of the last drawing are on their way out, and nothing may switch them back on.
                foreach (Section section in Sections)
                {
                    section.Header = null;
                    section.HeaderText = null;
                    section.Shown = null;
                    section.Objects.Clear();
                }
                _allText = null;
                _presetText = null;
                SearchDrawn(parent);
            }));
            page.AddCustom(new SearchRow());
            AddLoose(page, new ButtonOption("reallife_meta_guide_button", Guide.Open), true);
            page.AddCustom(new RowTap(parent => SearchStyle(Last(parent))));   // the box takes its size and font from that button
            AddLoose(page, new ButtonOption("reallife_meta_welcome_button", () => Welcome.Open(true)), true);

            AddLoose(page, new HeaderOption("reallife_meta_presets_header"), false);
            page.AddCustom(new RowTap(parent =>
            {
                _presetText = TextOf(Last(parent));
                UpdateLabels();
            }));
            AddLoose(page, new ButtonOption("reallife_meta_preset_button_relaxed", () => AskPreset(Intensity.Relaxed)), true);
            AddLoose(page, new ButtonOption("reallife_meta_preset_button_standard", () => AskPreset(Intensity.Standard)), true);
            AddLoose(page, new ButtonOption("reallife_meta_preset_button_realistic", () => AskPreset(Intensity.Realistic)), true);
            AddLoose(page, new ButtonOption("reallife_meta_preset_button_hardcore", () => AskPreset(Intensity.Hardcore)), true);
            AddLoose(page, new ButtonOption("reallife_meta_reset_button", () => MetaUi.Confirm("reallife_meta_reset_title".GetLocalization(),
                "reallife_meta_reset_explain".GetLocalization(), "reallife_meta_reset_yes",
                () => MetaUi.Message("reallife_meta_reset_title".GetLocalization(), Presets.ResetAll()))), true);
            AddLoose(page, new ButtonOption("reallife_meta_export_button", ExportNow), true);
            AddLoose(page, new ButtonOption("reallife_meta_import_button", () => MetaUi.Confirm("reallife_meta_import_title".GetLocalization(),
                "reallife_meta_import_explain".GetLocalization().Replace("{file}", System.IO.Path.Combine(Presets.Folder, Presets.SettingsFile)),
                "reallife_meta_import_yes",
                () => MetaUi.Message("reallife_meta_import_title".GetLocalization(), Presets.Import()))), true);

            AddLoose(page, new HeaderOption("reallife_meta_features_header"), false);
            AddLoose(page, new ButtonOption("reallife_meta_sections_button", ToggleAll), false);
            page.AddCustom(new RowTap(parent =>
            {
                _allText = TextOf(Last(parent));
                UpdateLabels();
            }));

            bool toolsHeader = false;
            Section tests = null;
            foreach (Feature feature in Ordered())
            {
                if (feature.Kind != FeatureKind.Gameplay && !toolsHeader)
                {
                    toolsHeader = true;
                    AddLoose(page, new HeaderOption("reallife_meta_tools_header"), false);
                }
                if (feature.Id == TestsId)
                {
                    tests = new Section { Feature = feature, IsTests = true };
                    continue;   // always the very last
                }
                if (byFeature.TryGetValue(feature, out Section section) && section.Rows.Count > 0)
                {
                    AddSection(page, section);
                }
            }

            int testRows = 0;
            if (tests != null)
            {
                // One group per feature, in the order of the sections above, each closed until it is asked for.
                foreach (Feature feature in Ordered())
                {
                    Section group = null;
                    foreach (TestAction test in feature.Tests)
                    {
                        if (test.Run == null)
                        {
                            continue;   // named by a feature but not on the page
                        }
                        if (group == null)
                        {
                            group = new Section { Feature = feature, Parent = tests };
                            tests.Children.Add(group);
                        }
                        group.Rows.Add(new ButtonOption(test.LabelKey, test.Run));
                        testRows++;
                    }
                }
                if (tests.Children.Count > 0)
                {
                    AddTests(page, tests);
                }
            }
            AddLoose(page, new SplitterOption(), false);
            MetaLog.Info("options page in order: " + Sections.Count + " sections (" + (tests != null ? tests.Children.Count : 0)
                         + " of them groups of test tools), " + SettingsTable.All.Count + " saved options, " + testRows + " test tools.");
            return page;
        }

        // "Test tools": its button, then a group per feature. It has no rows of its own.
        private static void AddTests(ModOptions page, Section tests)
        {
            Sections.Add(tests);
            page.AddButton(tests.Feature.TitleKey, () => Toggle(tests));
            page.AddCustom(new RowTap(parent =>
            {
                tests.Header = Last(parent);
                tests.HeaderText = TextOf(tests.Header);
                tests.Objects.Clear();
                Describe(tests);
                Apply(tests);
            }));
            foreach (Section group in tests.Children)
            {
                AddSection(page, group);
            }
        }

        private static void AddSection(ModOptions page, Section section)
        {
            Sections.Add(section);
            if (section.Parent == null)
            {
                WatchMaster(section);
            }
            // A group of test tools is named in its button's suffix: the feature's own title may be a whole header.
            page.AddButton(section.Parent != null ? "reallife_meta_tests_group" : section.Feature.TitleKey, () => Toggle(section));
            page.AddCustom(new RowTap(parent =>
            {
                section.Header = Last(parent);
                section.HeaderText = TextOf(section.Header);
                section.Start = parent.childCount;
                section.Objects.Clear();
                Describe(section);
            }));
            foreach (ModOption row in section.Rows)
            {
                page.AddCustom(row);
            }
            page.AddCustom(new RowTap(parent =>
            {
                section.Objects.Clear();
                for (int i = section.Start; i < parent.childCount; i++)
                {
                    section.Objects.Add(parent.GetChild(i).gameObject);
                }
                Apply(section);
            }));
        }

        // ---------------------------------------------------------------- the page while it is open

        private static GameObject Last(Transform parent)
        {
            return parent != null && parent.childCount > 0 ? parent.GetChild(parent.childCount - 1).gameObject : null;
        }

        private static TextLocalizationComponent TextOf(GameObject row)
        {
            return row != null ? row.GetComponentInChildren<TextLocalizationComponent>(true) : null;
        }

        // The row that switches a whole feature on and off (the features name it "..._enabled") also tells its section's button.
        private static void WatchMaster(Section section)
        {
            for (int i = 0; i < section.Rows.Count; i++)
            {
                if (section.Rows[i] is ToggleOption toggle && toggle.Label != null && toggle.Label.EndsWith("_enabled", StringComparison.Ordinal))
                {
                    Action<bool> inner = toggle.OnValueChanged;
                    section.HasMaster = true;
                    section.MasterOn = toggle.DefaultValue;
                    // The game calls this with the saved value each time the page is drawn, and on every click.
                    section.Rows[i] = new ToggleOption(toggle.Id, toggle.Label, toggle.DefaultValue, on =>
                    {
                        try
                        {
                            inner?.Invoke(on);
                        }
                        finally
                        {
                            MasterChanged(section, on);
                        }
                    });
                    return;
                }
            }
        }

        private static void MasterChanged(Section section, bool on)
        {
            try
            {
                if (section.MasterOn == on && section.Shown != null)
                {
                    return;
                }
                section.MasterOn = on;
                Describe(section);
                if (Searching)
                {
                    Label(section, section.Found);
                }
                else
                {
                    Label(section, Visible(section) && IsOpen(section) ? section.Opened : section.Closed);
                }
            }
            catch (Exception e)
            {
                MetaLog.Error(e);
            }
        }

        // What a section's button says after the name. Made when the page is drawn and when the feature is switched, never per frame.
        private static void Describe(Section section)
        {
            string about;
            if (section.Parent != null)
            {
                about = ":  " + Title(section.Feature) + "   (" + section.Rows.Count + ")";
            }
            else if (section.IsTests)
            {
                int count = 0;
                foreach (Section group in section.Children)
                {
                    count += group.Rows.Count;
                }
                about = "   (" + count + ")";
            }
            else
            {
                int options = 0;
                foreach (ModOption row in section.Rows)
                {
                    if (row is IPersistableOption)
                    {
                        options++;
                    }
                }
                string what = options > 0
                    ? Counted(options, "reallife_meta_section_option", "reallife_meta_section_options")
                    : Counted(section.Rows.Count, "reallife_meta_section_button", "reallife_meta_section_buttons");
                string state = !section.HasMaster ? string.Empty
                    : ", " + (section.MasterOn ? "reallife_meta_section_on" : "reallife_meta_section_off").GetLocalization();
                about = "   (" + what + state + ")";
            }
            section.Found = about;
            section.Closed = about + "   [+]";
            section.Opened = about + "   [-]";
        }

        private static string Counted(int count, string oneKey, string manyKey)
        {
            return count == 1 ? oneKey.GetLocalization() : manyKey.GetLocalization().Replace("{count}", count.ToString());
        }

        private static readonly string[] HeaderPrefixes = { "Real Life Overhaul - ", "Real Life Overhaul: ", "Real Life: " };

        // A feature's name as text. One made from a header nobody registered is that header without the mod's name in front.
        private static string Title(Feature feature)
        {
            string title = feature.TitleKey.GetLocalization();
            if (feature.Auto)
            {
                foreach (string prefix in HeaderPrefixes)
                {
                    if (title.Length > prefix.Length && title.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        return title.Substring(prefix.Length).Trim();
                    }
                }
            }
            return title;
        }

        // The text after the name on a section's button, written only when it is another one.
        private static void Label(Section section, string suffix)
        {
            if (section.HeaderText != null && !ReferenceEquals(section.Shown, suffix))
            {
                section.Shown = suffix;
                section.HeaderText.Suffix = suffix;
            }
        }

        private static void Show(GameObject row, bool on)
        {
            if (row != null && row.activeSelf != on)
            {
                row.SetActive(on);
            }
        }

        private static string OpenKey(Section section)
        {
            return Settings.PrefsKey((section.Parent != null ? "ui_open_tests_" : "ui_open_") + section.Feature.Id);
        }

        private static bool IsOpen(Section section)
        {
            string key = OpenKey(section);
            bool first = section.Parent == null && section.Feature.StartsOpen;
            return key == null ? first : UnityEngine.PlayerPrefs.GetInt(key, first ? 1 : 0) != 0;
        }

        private static void SetOpen(Section section, bool open)
        {
            string key = OpenKey(section);
            if (key != null)
            {
                UnityEngine.PlayerPrefs.SetInt(key, open ? 1 : 0);
            }
            Apply(section);
        }

        private static void Toggle(Section section)
        {
            if (Searching)
            {
                return;   // what is shown is what was found; what is open and closed stays as it was left
            }
            SetOpen(section, !IsOpen(section));
            UpdateLabels();
        }

        // The sections only: the groups of test tools stay as they are, inside theirs.
        private static void ToggleAll()
        {
            if (Searching)
            {
                return;
            }
            bool anyOpen = false;
            foreach (Section section in Sections)
            {
                anyOpen |= section.Parent == null && IsOpen(section);
            }
            foreach (Section section in Sections)
            {
                if (section.Parent == null)
                {
                    SetOpen(section, !anyOpen);
                }
            }
            UpdateLabels();
        }

        // Is the section's own button to be seen: "Test tools" only while they are asked for, a group in it only while it is open.
        private static bool Visible(Section section)
        {
            if (section.Parent != null)
            {
                return Visible(section.Parent) && IsOpen(section.Parent);
            }
            return !section.IsTests || Settings.ShowTestTools;
        }

        // Shows or hides a section's rows, and the groups inside it. The objects are the game's; one that has gone (the page was closed) is skipped.
        private static void Apply(Section section)
        {
            if (Searching)
            {
                return;   // the search decides until the box is empty again, and then calls this
            }
            bool visible = Visible(section);
            bool open = visible && IsOpen(section);
            Show(section.Header, visible);
            Label(section, open ? section.Opened : section.Closed);
            foreach (GameObject row in section.Objects)
            {
                Show(row, open);
            }
            foreach (Section group in section.Children)
            {
                Apply(group);
            }
        }

        /// <summary>After "Show test tools" changed.</summary>
        public static void ApplyAll()
        {
            if (Searching)
            {
                SearchAgain();   // that toggle may itself be something that was found
                return;
            }
            foreach (Section section in Sections)
            {
                if (section.Parent == null)
                {
                    Apply(section);
                }
            }
        }

        private static void UpdateLabels()
        {
            try
            {
                if (_presetText != null)
                {
                    _presetText.Suffix = ": " + Presets.CurrentName();
                }
                if (_allText != null)
                {
                    bool anyOpen = false;
                    foreach (Section section in Sections)
                    {
                        anyOpen |= section.Parent == null && IsOpen(section);
                    }
                    _allText.Suffix = anyOpen ? "   [-]" : "   [+]";
                }
            }
            catch (Exception e)
            {
                MetaLog.Error(e);
            }
        }

        /// <summary>
        /// Option values were changed from outside the page (a preset, an import, a reset): if the page is open
        /// it is drawn again on the next frame, which is the game's own way of showing saved values.
        /// </summary>
        public static void Refresh()
        {
            if (_built == null || _modId == null || !OptionsService.RegisteredEntries.ContainsKey(_modId))
            {
                return;
            }
            MetaUi.Instance.NextFrame(() =>
            {
                if (_built != null && OptionsService.RegisteredEntries.ContainsKey(_modId))
                {
                    OptionsService.Register(_modId, _built);
                }
                UpdateLabels();
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
                    UpdateLabels();
                });
            }
            catch (Exception e)
            {
                MetaLog.Error(e);
            }
        }

        // ---------------------------------------------------------------- the buttons at the top

        private static void AskPreset(Intensity intensity)
        {
            string name = Presets.NameKey(intensity).GetLocalization();
            string about;
            switch (intensity)
            {
                case Intensity.Relaxed: about = "reallife_meta_preset_about_relaxed"; break;
                case Intensity.Realistic: about = "reallife_meta_preset_about_realistic"; break;
                case Intensity.Hardcore: about = "reallife_meta_preset_about_hardcore"; break;
                default: about = "reallife_meta_preset_about_standard"; break;
            }
            MetaUi.Confirm(name, about.GetLocalization() + "\n\n" + "reallife_meta_preset_confirm".GetLocalization(), "reallife_meta_preset_yes",
                () => MetaUi.Message(name, Presets.Apply(intensity)));
        }

        private static void ExportNow()
        {
            string title = "reallife_meta_export_title".GetLocalization();
            try
            {
                string path = Presets.Export(Presets.SettingsFile);
                MetaLog.Info("settings written to " + path);
                MetaUi.Message(title, "reallife_meta_export_done".GetLocalization().Replace("{file}", path));
            }
            catch (Exception e)
            {
                MetaLog.Error(e);
                MetaUi.Message(title, "reallife_meta_export_failed".GetLocalization().Replace("{error}", e.Message));
            }
        }

        // ---------------------------------------------------------------- the set-up check

        /// <summary>
        /// The "Check the mod's set-up" button: looks through the registry, the presets, the options page and the
        /// guide for anything that does not fit together, and shows what it found. For whoever merges a feature.
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
            MetaUi.Message("reallife_meta_check_title".GetLocalization(), summary + (problems.Count > 0 ? "\n\n- " + string.Join("\n- ", problems) : string.Empty));
        }
    }
}
