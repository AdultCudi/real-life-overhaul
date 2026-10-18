using System;
using System.Collections.Generic;
using System.Globalization;
using Localizor;
using RealLifeOverhaul.GameUi;
using TMPro;
using UnityEngine;

namespace RealLifeOverhaul.Meta
{
    /// <summary>
    /// The Settings app's white box (InfoBox, of the game's own parts): what is picked in the table, or with nothing picked the
    /// group and the presets against the choices made. Written on a click; nothing per frame.
    /// </summary>
    public sealed partial class SettingsView
    {
        private const int Presets4 = 4;          // Relaxed, Standard, Realistic, Hardcore (Meta/Presets)
        private const int NumberLength = 7;      // characters the game's text entry takes for a number ("-100000")

        private static readonly string[] PresetButtons =
        {
            "reallife_meta_preset_button_relaxed", "reallife_meta_preset_button_standard", "reallife_meta_preset_button_realistic", "reallife_meta_preset_button_hardcore"
        };

        private static readonly string[] PresetAbout =
        {
            "reallife_meta_preset_about_relaxed", "reallife_meta_preset_about_standard", "reallife_meta_preset_about_realistic", "reallife_meta_preset_about_hardcore"
        };

        // The presets against the choices made, worked out once per writing of the page.
        private readonly List<OptionInfo> _infos = new List<OptionInfo>(400);
        private readonly List<int> _now = new List<int>(400);
        private readonly List<int>[] _presetValues = { new List<int>(400), new List<int>(400), new List<int>(400), new List<int>(400) };
        private readonly List<bool>[] _covered = { new List<bool>(400), new List<bool>(400), new List<bool>(400), new List<bool>(400) };
        private readonly int[] _counts = new int[Presets4];
        private readonly int[] _covers = new int[Presets4];
        private readonly List<int> _differ = new List<int>(64);
        private readonly List<int> _steps = new List<int>(SettingsRules.MostSteps + 2);
        private int _closest = -1;
        private bool _exact;

        // Every saved option against what each preset sets: how many differ, and which preset the choices are nearest.
        private void Compare()
        {
            _infos.Clear();
            _now.Clear();
            for (int p = 0; p < Presets4; p++)
            {
                _presetValues[p].Clear();
                _covered[p].Clear();
                _covers[p] = 0;
            }
            foreach (OptionInfo info in SettingsTable.All)
            {
                _infos.Add(info);
                _now.Add(SettingsTable.Current(info));
                for (int p = 0; p < Presets4; p++)
                {
                    bool covered = Presets.TryValue(info, (Intensity)p, out int value);
                    _covered[p].Add(covered);
                    _presetValues[p].Add(value);
                    _covers[p] += covered ? 1 : 0;
                }
            }
            for (int p = 0; p < Presets4; p++)
            {
                _counts[p] = SettingsRules.Differences(_now, _presetValues[p], _covered[p], null);
            }
            _closest = SettingsRules.Closest(_counts, _covers, (int)Intensity.Standard, out _exact);
        }

        private static string PresetName(int preset)
        {
            return Presets.NameKey((Intensity)preset).GetLocalization();
        }

        private static int PresetOf(SettingsLine line)
        {
            return line.Kind == LineKind.Button ? Array.IndexOf(PresetButtons, OptionsLayout.AppLabelKey(line)) : -1;
        }

        private void WriteOverview(InfoBox box, SettingsTab tab, int on, int all)
        {
            box.Title(tab.Group.TitleKey.GetLocalization());
            bool start = tab.Group.Id == OptionsTree.StartId;
            if (start && _closest >= 0)
            {
                box.Row("reallife_set_box_preset", _exact ? PresetName(_closest) : T("reallife_set_box_custom", "preset", PresetName(_closest)));
                box.Line(_exact ? T("reallife_set_box_same", "preset", PresetName(_closest))
                    : T("reallife_set_box_differ", "preset", PresetName(_closest), "count", _counts[_closest].ToString(CultureInfo.InvariantCulture)));
            }
            if (all > 0)
            {
                box.Row("reallife_set_box_on", T("reallife_set_of", "on", on.ToString(CultureInfo.InvariantCulture), "total", all.ToString(CultureInfo.InvariantCulture)));
            }
            box.Row("reallife_set_box_sections", tab.Kinds.Count.ToString(CultureInfo.InvariantCulture));
            box.Line(T("reallife_set_box_pick"));
            Transform buttons = box.ButtonRow();
            InfoBox.Button(buttons, InfoBox.Positive, "reallife_set_search", () => UiClone.Safe(AskSearch));
            if (_closest >= 0 && !_exact)
            {
                InfoBox.Button(buttons, InfoBox.Additional, "reallife_set_list_differ", () => UiClone.Safe(() =>
                {
                    _mode = ModeDiffer;
                    _pick = null;
                    Refresh();
                }));
            }
            box.Line(T("reallife_set_box_same_page"));
            Guide.Point(box, GuideTitle);
        }

        private void WritePicked(InfoBox box, SettingsLine line)
        {
            switch (line.Kind)
            {
                case LineKind.Heading:
                    WriteSection(box, line);
                    break;
                case LineKind.TestHeading:
                    WriteTests(box, line);
                    break;
                case LineKind.Option:
                    WriteOption(box, line);
                    break;
                case LineKind.Key:
                    WriteKey(box, line);
                    break;
                default:
                    WriteButton(box, line);
                    break;
            }
        }

        private void WriteOption(InfoBox box, SettingsLine line)
        {
            int value = OptionsLayout.AppValue(line);
            int standard = OptionsLayout.AppDefault(line);
            OptionType type = OptionsLayout.AppType(line);
            box.Title(OptionsLayout.AppLabel(line));
            box.Section("reallife_ibox_sec_setting");
            box.Row("reallife_set_box_now", OptionsLayout.AppValueText(line, value));
            string tip = OptionsLayout.AppTip(line);
            if (tip != null)
            {
                box.Line(tip);
            }
            string aside = OptionsLayout.AppAside(line);
            string blocker = aside == null ? OptionsLayout.AppBlocker(line) : null;
            bool control = aside == null && blocker == null;
            if (aside != null)
            {
                box.Line(aside);
            }
            else if (blocker != null)
            {
                Needs(box, blocker);
            }
            else if (type == OptionType.Toggle)
            {
                box.Switch(OptionsLayout.AppLabelKey(line), value != 0, on => UiClone.Safe(() => Set(line, on ? 1 : 0)));
            }
            else if (type == OptionType.Choice)
            {
                OptionsLayout.AppRange(line, out int min, out int max);
                box.Selector(max + 1, value, i => OptionsLayout.AppValueText(line, i), i => UiClone.Safe(() => Set(line, i)));
            }
            else
            {
                OptionsLayout.AppRange(line, out int min, out int max);
                SettingsRules.Steps(min, max, value, standard, _steps);
                int[] steps = _steps.ToArray();
                box.Selector(steps.Length, SettingsRules.StepOf(_steps, value), i => OptionsLayout.AppValueText(line, steps[i]), i => UiClone.Safe(() => Set(line, steps[i])));
            }
            if (value != standard)
            {
                box.Row("reallife_set_box_default", OptionsLayout.AppValueText(line, standard));
            }
            box.Row("reallife_set_box_effect", T(OptionsLayout.AppNeedsReload(line) ? "reallife_set_effect_reload" : "reallife_set_effect_now"));
            box.Row("reallife_set_box_where", Where(line));
            Transform buttons = box.ButtonRow();
            if (control && type == OptionType.Slider)
            {
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_set_type_number", () => UiClone.Safe(() => AskNumber(line)));
            }
            if (control && value != standard)
            {
                InfoBox.Button(buttons, InfoBox.Negative, "reallife_set_to_default", () => UiClone.Safe(() => Set(line, standard)));
            }
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_set_back", Back);
            GuideButton(box, line.Section);
        }

        // What keeps an option from doing anything, and a button that switches it on.
        private void Needs(InfoBox box, string blocker)
        {
            string label = OptionsLayout.AppLabelOf(blocker);
            box.Row("reallife_set_box_needs", label);
            box.Line(T("reallife_set_box_needs_line", "option", label));
            SettingsLine need = OptionsLayout.AppFind(blocker);
            if (need == null || OptionsLayout.AppAside(need) != null)
            {
                return;
            }
            Button(box.ButtonRow(), InfoBox.Positive, T("reallife_set_switch_on", "option", label), () => UiClone.Safe(() => Set(need, 1)));
        }

        private void WriteSection(InfoBox box, SettingsLine heading)
        {
            box.Title(OptionsLayout.AppLabel(heading));
            CountSection(heading, out int on, out int all);
            if (all > 0)
            {
                box.Row("reallife_set_box_on", T("reallife_set_of", "on", on.ToString(CultureInfo.InvariantCulture), "total", all.ToString(CultureInfo.InvariantCulture)));
            }
            SettingsLine master = heading.Section != null && heading.Section.Master != null ? OptionsLayout.AppFind(heading.Section.Master) : null;
            if (master != null && OptionsLayout.AppShown(master))
            {
                string blocker = OptionsLayout.AppBlocker(master);
                string aside = OptionsLayout.AppAside(master);
                if (aside != null)
                {
                    box.Line(aside);
                }
                else if (blocker != null)
                {
                    Needs(box, blocker);
                }
                else
                {
                    box.Switch(OptionsLayout.AppLabelKey(master), OptionsLayout.AppValue(master) != 0, value => UiClone.Safe(() => Set(master, value ? 1 : 0)));
                }
            }
            box.Line(T("reallife_set_box_section_pick"));
            box.Row("reallife_set_box_where", Where(heading));
            InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_set_back", Back);
            GuideButton(box, heading.Section);
        }

        private void WriteTests(InfoBox box, SettingsLine heading)
        {
            box.Title(OptionsLayout.AppLabel(heading));
            int count = 0;
            foreach (SettingsLine line in OptionsLayout.AppTabs[heading.Tab].Lines)
            {
                count += line.Kind == LineKind.Test && line.Section == heading.Section && line.TestGroup == heading.TestGroup ? 1 : 0;
            }
            box.Row("reallife_set_box_tools", count.ToString(CultureInfo.InvariantCulture));
            box.Line(T("reallife_set_box_tests"));
            InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_set_back", Back);
            Guide.Point(box, "reallife_guide_trouble_title");
        }

        private void WriteButton(InfoBox box, SettingsLine line)
        {
            box.Title(OptionsLayout.AppLabel(line));
            int preset = PresetOf(line);
            if (preset >= 0)
            {
                box.Row("reallife_set_box_preset_differ", _covers[preset] > 0 ? _counts[preset].ToString(CultureInfo.InvariantCulture) : "-");
                box.Line(PresetAbout[preset].GetLocalization());
            }
            if (line.Kind == LineKind.Test)
            {
                box.Line(T("reallife_set_box_test"));
            }
            box.Row("reallife_set_box_where", Where(line));
            Transform buttons = box.ButtonRow();
            InfoBox.Button(buttons, InfoBox.Positive, line.Kind == LineKind.Test ? "reallife_opt_caption_run" : OptionsLayout.AppCaptionKey(line), () => UiClone.Safe(() =>
            {
                OptionsLayout.AppRun(line);
                if (Showing)
                {
                    Refresh();
                }
            }));
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_set_back", Back);
            if (line.Kind == LineKind.Test)
            {
                Guide.Point(box, "reallife_guide_trouble_title");
                return;
            }
            GuideButton(box, line.Section);
        }

        private void WriteKey(InfoBox box, SettingsLine line)
        {
            box.Title(OptionsLayout.AppLabel(line));
            int action = OptionsLayout.AppKeyAction(line);
            box.Row("reallife_set_box_key", action >= 0 ? Keys.Label((KeyAction)action) : "-");
            box.Line(T("reallife_set_box_key_where"));
            box.Row("reallife_set_box_where", Where(line));
            InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_set_back", Back);
            GuideButton(box, line.Section);
        }

        // The section's page in the Guide: a button that opens it, and the line every app's page ends with.
        private static void GuideButton(InfoBox box, TreeSection section)
        {
            string topic = OptionsLayout.AppGuideTopic(section);
            string title = OptionsLayout.AppGuideTitleKey(section);
            if (topic != null)
            {
                InfoBox.Button(box.ButtonRow(), InfoBox.Positive, "reallife_set_guide_page", () => UiClone.Safe(() => Guide.Open(topic)));
            }
            Guide.Point(box, title ?? GuideTitle);
        }

        private void WriteDiffer(InfoBox box)
        {
            if (_closest < 0)
            {
                _mode = ModeTab;
                WriteOverview(box, OptionsLayout.AppTabs[_tab], 0, 0);
                return;
            }
            int count = SettingsRules.Differences(_now, _presetValues[_closest], _covered[_closest], _differ);
            for (int d = 0; d < _differ.Count; d++)
            {
                int i = _differ[d];
                OptionInfo info = _infos[i];
                SettingsLine line = OptionsLayout.AppFind(info.Id);
                int index = line != null ? _all.IndexOf(line) : -1;
                TableRow row;
                if (index >= 0)
                {
                    row = Row(index);
                    row.Set(1, OptionsLayout.AppValueText(line, _now[i]));
                    row.Set(2, OptionsLayout.AppValueText(line, _presetValues[_closest][i]));
                    row.Set(3, Where(line));
                }
                else
                {
                    row = Row(0);
                    row.Reset("d:" + info.Id);   // an option on no row of the page: listed, not picked
                    row.Set(1, Presets.Show(info, _now[i]));
                    row.Set(2, Presets.Show(info, _presetValues[_closest][i]));
                }
                row.Set(0, Presets.Label(info));
            }
            string name = PresetName(_closest);
            UiClone.Words(_shell.Header, T("reallife_set_headline_differ", "preset", name, "count", count.ToString(CultureInfo.InvariantCulture)));
            box.Title(T("reallife_set_box_differ_title"));
            box.Section("reallife_ibox_sec_now");
            box.Row("reallife_set_box_compared", name);
            box.Row("reallife_set_box_differ_count", count.ToString(CultureInfo.InvariantCulture));
            box.Line(count == 0 ? T("reallife_set_box_same", "preset", name) : T("reallife_set_box_differ_pick", "preset", name));
            Transform buttons = box.ButtonRow();
            SettingsLine apply = OptionsLayout.AppFind(PresetButtons[_closest]);
            if (apply != null && count > 0)
            {
                Button(buttons, InfoBox.Positive, T("reallife_set_apply_preset", "preset", name), () => UiClone.Safe(() => OptionsLayout.AppRun(apply)));
            }
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_set_back", Back);
            Guide.Point(box, "reallife_guide_presets_title");
        }

        private void Set(SettingsLine line, int value)
        {
            if (line != null && OptionsLayout.AppSet(line, value))
            {
                _wrote = true;
                if (Dev.Verbose)
                {
                    MetaLog.Info("Meta check (settings app): " + OptionsLayout.AppId(line) + " set to " + OptionsLayout.AppValue(line) + " (asked " + value + "), the same key the Mods tab saves: "
                                 + (OptionsLayout.AppValue(line) == value || OptionsLayout.AppType(line) != OptionType.Slider ? "PASS" : "CLAMPED") + ".");
                }
            }
            Refresh();
        }

        // A number typed in the game's text entry, kept inside what the option allows.
        private void AskNumber(SettingsLine line)
        {
            UI.UIs uis = InstanceBehavior<UI.UIs>.Instance;
            if (uis == null || uis.textInputUi == null)
            {
                return;
            }
            OptionsLayout.AppRange(line, out int min, out int max);
            uis.textInputUi.Show("reallife_set_number_hint", "common_confirm", text => UiClone.Safe(() =>
            {
                if (int.TryParse((text ?? string.Empty).Trim().TrimEnd('%'), NumberStyles.Integer, CultureInfo.InvariantCulture, out int typed))
                {
                    Set(line, Math.Max(min, Math.Min(max, typed)));
                }
            }), null, OptionsLayout.AppValue(line).ToString(CultureInfo.InvariantCulture), false, NumberLength);
        }

        // One of the box's buttons with words made here ("Switch on Home life and health") rather than a key.
        private static void Button(Transform row, int kind, string words, Action click)
        {
            UnityEngine.UI.Button button = InfoBox.Button(row, kind, "reallife_set_back", click);
            if (button != null)
            {
                UiClone.Words(button.GetComponentInChildren<TMP_Text>(true), words);
            }
        }
    }
}
