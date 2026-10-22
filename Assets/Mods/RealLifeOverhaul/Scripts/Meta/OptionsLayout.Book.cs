using System;
using System.Collections.Generic;
using BigAmbitions.Mods;
using Localizor;

namespace RealLifeOverhaul.Meta
{
    /// <summary>
    /// What the Settings app (<see cref="SettingsView"/>) knows of the options page: the same rows, read and written as the Mods
    /// tab's controls do (saved under the same key, then the row's handler called), so the two cannot disagree. Runs on a click.
    /// </summary>
    public static partial class OptionsLayout
    {
        internal const string SettingsButtonKey = SettingsRules.SettingsButtonKey;

        private static readonly List<TestPlace> TestPlaces = new List<TestPlace>();
        private static readonly List<Action> TestRuns = new List<Action>();
        private static Row[] _built;              // one per row the page placed, by its index in the built list
        private static SettingsTab[] _tabs;
        private static List<SettingsPage> _pages;
        private static Dictionary<string, SettingsLine> _lineOf;   // an option's id or a button's label key: its line (made on the first look, once per build)

        /// <summary>The page was put in order, so the app has its rows.</summary>
        internal static bool AppReady => _tabs != null && _pages != null && _built != null;

        /// <summary>The app's groups: one per group of the page, Start here first and Troubleshooting last.</summary>
        internal static SettingsTab[] AppTabs => _tabs;

        /// <summary>The app's pages: a section each, then the sections' test tools (SettingsRules.Pages).</summary>
        internal static List<SettingsPage> AppPages => _pages;

        /// <summary>The control a line is drawn with (SettingsRules.ControlOf over the row as the page built it).</summary>
        internal static ControlKind AppControl(SettingsLine line)
        {
            ModOption option = RowOf(line)?.Option;
            return SettingsRules.ControlOf(line.Kind, option != null ? KindOf(option) : PlanKind.Button);
        }

        private static Row RowOf(SettingsLine line)
        {
            return line != null && line.Row >= 0 && _built != null && line.Row < _built.Length && line.Kind != LineKind.Test ? _built[line.Row] : null;
        }

        /// <summary>The saved option a line stands for, or null (a heading, a button, a key, a test).</summary>
        internal static string AppId(SettingsLine line)
        {
            return line != null && line.Kind == LineKind.Option ? RowOf(line)?.Id : null;
        }

        /// <summary>The line of a saved option, or of a button by its label key; null if the page has none.</summary>
        internal static SettingsLine AppFind(string idOrLabel)
        {
            if (_tabs == null || string.IsNullOrEmpty(idOrLabel))
            {
                return null;
            }
            if (_lineOf == null)
            {
                var index = new Dictionary<string, SettingsLine>(StringComparer.Ordinal);
                foreach (SettingsTab tab in _tabs)
                {
                    foreach (SettingsLine line in tab.Lines)
                    {
                        Row row = RowOf(line);
                        string key = row == null ? null : row.Id ?? row.LabelKey;
                        if (key != null && !index.ContainsKey(key))
                        {
                            index[key] = line;
                        }
                    }
                }
                _lineOf = index;
            }
            return _lineOf.TryGetValue(idOrLabel, out SettingsLine found) ? found : null;
        }

        internal static string AppLabelKey(SettingsLine line)
        {
            return RowOf(line)?.LabelKey;
        }

        /// <summary>The heading of a section, by its id, or null.</summary>
        internal static SettingsLine AppHeading(string sectionId)
        {
            if (_tabs == null || sectionId == null)
            {
                return null;
            }
            foreach (SettingsTab tab in _tabs)
            {
                foreach (SettingsLine line in tab.Lines)
                {
                    if (line.Kind == LineKind.Heading && line.Section != null && line.Section.Id == sectionId)
                    {
                        return line;
                    }
                }
            }
            return null;
        }

        /// <summary>The words of a line: an option's or a button's label, a test's, or a heading's section.</summary>
        internal static string AppLabel(SettingsLine line)
        {
            if (line == null)
            {
                return string.Empty;
            }
            switch (line.Kind)
            {
                case LineKind.Heading:
                    return (line.Section != null ? line.Section.TitleKey : "reallife_meta_title_other").GetLocalization();
                case LineKind.TestHeading:
                    string group = line.TestGroup >= 0 ? OptionsTree.Groups[line.TestGroup].TitleKey.GetLocalization() : "reallife_meta_title_other".GetLocalization();
                    return line.Section != null ? group + ": " + line.Section.TitleKey.GetLocalization() : group;
                case LineKind.Test:
                    return line.Row >= 0 && line.Row < TestPlaces.Count ? TestPlaces[line.Row].LabelKey.GetLocalization() : string.Empty;
                default:
                    Row row = RowOf(line);
                    return row != null ? row.LabelKey.GetLocalization() : string.Empty;
            }
        }

        /// <summary>The one sentence an option has (its tooltip on the Mods tab), or null.</summary>
        internal static string AppTip(SettingsLine line)
        {
            Row row = RowOf(line);
            return row != null && row.TipKey != null ? row.TipKey.GetLocalization() : null;
        }

        /// <summary>The lower-case words the search looks in: the label, the sentence and the table's own words for the row.</summary>
        internal static string AppSearchText(SettingsLine line)
        {
            if (line == null || line.IsHeading)
            {
                return string.Empty;
            }
            Row row = RowOf(line);
            string tip = AppTip(line);
            return (AppLabel(line) + " " + (tip ?? string.Empty) + " " + (row != null ? row.Words : null)).ToLowerInvariant();
        }

        /// <summary>Whether a line is to be shown: a row a feature leaves off for now (Feature.Present) is not, nor are the test tools while they are asked to stay hidden.</summary>
        internal static bool AppShown(SettingsLine line)
        {
            if (line == null)
            {
                return false;
            }
            if (line.Kind == LineKind.Test || line.Kind == LineKind.TestHeading)
            {
                return Settings.ShowTestTools;
            }
            Row row = RowOf(line);
            if (row == null)
            {
                return line.Kind == LineKind.Heading;
            }
            try
            {
                return row.Present == null || row.Present();
            }
            catch (Exception e)
            {
                MetaLog.Error(e);
                return true;
            }
        }

        /// <summary>What an option is now: its field in Settings where it has one, else what is saved, else its default.</summary>
        internal static int AppValue(SettingsLine line)
        {
            Row row = RowOf(line);
            if (row == null || row.Id == null)
            {
                return 0;
            }
            OptionInfo info = SettingsTable.Get(row.Id);
            if (info != null && info.Field != null)
            {
                return SettingsTable.Current(info);
            }
            int standard = DefaultOf(row.Option);
            string key = Settings.PrefsKey(row.Id);
            int value = key == null ? standard : UnityEngine.PlayerPrefs.GetInt(key, standard);
            AppRange(line, out int min, out int max);
            return value < min || value > max ? standard : value;   // as the game's drop-down takes a saved index it has no choice for
        }

        /// <summary>The value an option has when the mod ships.</summary>
        internal static int AppDefault(SettingsLine line)
        {
            Row row = RowOf(line);
            return row != null ? DefaultOf(row.Option) : 0;
        }

        private static int DefaultOf(ModOption option)
        {
            if (option is ToggleOption toggle)
            {
                return toggle.DefaultValue ? 1 : 0;
            }
            if (option is SliderOption slider)
            {
                return slider.DefaultValue;
            }
            return option is DropdownOption dropdown ? dropdown.DefaultIndex : 0;
        }

        internal static OptionType AppType(SettingsLine line)
        {
            ModOption option = RowOf(line)?.Option;
            return option is SliderOption ? OptionType.Slider : option is DropdownOption ? OptionType.Choice : OptionType.Toggle;
        }

        internal static void AppRange(SettingsLine line, out int min, out int max)
        {
            ModOption option = RowOf(line)?.Option;
            min = 0;
            max = 1;
            if (option is SliderOption slider)
            {
                min = slider.Min;
                max = slider.Max;
            }
            else if (option is DropdownOption dropdown)
            {
                max = dropdown.ChoiceKeys.Length - 1;
            }
        }

        /// <summary>A value of an option in words: "On", "75%", the name of a choice.</summary>
        internal static string AppValueText(SettingsLine line, int value)
        {
            ModOption option = RowOf(line)?.Option;
            if (option is SliderOption slider)
            {
                string format = string.IsNullOrEmpty(slider.ValueLabelKey) ? SettingsTable.SliderValue : slider.ValueLabelKey;
                return format.GetLocalization().Replace("{value}", value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            if (option is DropdownOption dropdown)
            {
                return value >= 0 && value < dropdown.ChoiceKeys.Length ? dropdown.ChoiceKeys[value].GetLocalization() : string.Empty;
            }
            return (value != 0 ? "reallife_meta_on" : "reallife_meta_off").GetLocalization();
        }

        /// <summary>Changes an option as its Mods tab control does: saved under its key, then the row's handler called. False with nothing to write.</summary>
        internal static bool AppSet(SettingsLine line, int value)
        {
            Row row = RowOf(line);
            string key = row != null && row.Id != null ? Settings.PrefsKey(row.Id) : null;
            if (key == null)
            {
                return false;
            }
            AppRange(line, out int min, out int max);
            value = Math.Max(min, Math.Min(max, value));
            UnityEngine.PlayerPrefs.SetInt(key, value);
            switch (row.Option)
            {
                case ToggleOption toggle:
                    toggle.OnValueChanged?.Invoke(value != 0);
                    break;
                case SliderOption slider:
                    slider.OnValueChanged?.Invoke(value);
                    break;
                case DropdownOption dropdown:
                    dropdown.OnValueChanged?.Invoke(value);
                    break;
            }
            return true;
        }

        /// <summary>Whether an option is on now, read afresh (a preset may have changed it since the Mods tab was drawn).</summary>
        internal static bool AppIsOn(string optionId)
        {
            SettingsLine line = AppFind(optionId);
            if (line != null)
            {
                return AppValue(line) != 0;
            }
            OptionInfo info = SettingsTable.Get(optionId);
            return info == null || SettingsTable.Current(info) != 0;
        }

        private static readonly Func<string, bool> AppIsOnNow = AppIsOn;

        /// <summary>The option that keeps this one from doing anything (OptionsTree's needs), or null.</summary>
        internal static string AppBlocker(SettingsLine line)
        {
            Row row = RowOf(line);
            return row != null && row.HasNeeds ? OptionsPlan.Blocker(row.Id, AppIsOnNow) : null;
        }

        /// <summary>Why another mod's having the same thing keeps an option from doing anything now, or null.</summary>
        internal static string AppAside(SettingsLine line)
        {
            Row row = RowOf(line);
            if (row == null || row.AsideOf == null || row.AsideOf.AsideWhy == null)
            {
                return null;
            }
            try
            {
                return row.AsideOf.AsideWhy();
            }
            catch (Exception e)
            {
                MetaLog.Error(e);
                return null;
            }
        }

        /// <summary>The label of an option by its id, for "needs: ...".</summary>
        internal static string AppLabelOf(string optionId)
        {
            return ById.TryGetValue(optionId ?? string.Empty, out Row row) ? row.LabelKey.GetLocalization() : optionId;
        }

        internal static bool AppNeedsReload(SettingsLine line)
        {
            string id = AppId(line);
            return id != null && Features.NeedsReload(id);
        }

        /// <summary>Presses a button or runs a test tool, as its row on the Mods tab does.</summary>
        internal static void AppRun(SettingsLine line)
        {
            if (line == null)
            {
                return;
            }
            if (line.Kind == LineKind.Test)
            {
                if (line.Row >= 0 && line.Row < TestRuns.Count)
                {
                    TestRuns[line.Row]?.Invoke();
                }
                return;
            }
            if (RowOf(line)?.Option is ButtonOption button)
            {
                button.OnClick?.Invoke();
            }
        }

        /// <summary>The word on a button's row on the Mods tab ("Apply", "Open", "Run"), for the button in the app's box.</summary>
        internal static string AppCaptionKey(SettingsLine line)
        {
            Row row = RowOf(line);
            if (row != null && row.LabelKey != null && CaptionByLabel.TryGetValue(row.LabelKey, out string key))
            {
                return key;
            }
            return CaptionRun;
        }

        /// <summary>The key a key's line stands for, or -1.</summary>
        internal static int AppKeyAction(SettingsLine line)
        {
            Row row = RowOf(line);
            if (row == null || line.Kind != LineKind.Key)
            {
                return -1;
            }
            for (int i = 0; i < Keys.Count; i++)
            {
                if (Keys.LabelKey((KeyAction)i) == row.LabelKey)
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>The Guide's page that explains a section ("feature_home"), or null if it has none.</summary>
        internal static string AppGuideTopic(TreeSection section)
        {
            Feature guide = section != null && section.GuideFeature != null ? Features.Find(section.GuideFeature) : null;
            return guide != null && (guide.HelpKey != null || guide.AboutKey != null) ? "feature_" + guide.Id : null;
        }

        /// <summary>The Guide's title of a section's page, for the line at the foot of the box.</summary>
        internal static string AppGuideTitleKey(TreeSection section)
        {
            Feature guide = section != null && section.GuideFeature != null ? Features.Find(section.GuideFeature) : null;
            return guide != null && (guide.HelpKey != null || guide.AboutKey != null) ? guide.TitleKey : null;
        }
    }
}
