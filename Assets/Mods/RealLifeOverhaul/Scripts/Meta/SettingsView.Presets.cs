using System;
using System.Collections.Generic;
using System.Globalization;
using Localizor;

namespace RealLifeOverhaul.Meta
{
    /// <summary>
    /// The Settings app against the presets: the one the choices are, or are nearest, in the top bar; and the page of the
    /// options that differ from it. Worked out when a page is written and on a change; nothing a frame.
    /// </summary>
    public sealed partial class SettingsView
    {
        private const int Presets4 = 4;          // Relaxed, Standard, Realistic, Hardcore (Meta/Presets)

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
        private int _closest = -1;
        private bool _exact;

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

        // The top bar's right: the preset in use, or the one the choices are nearest and how far.
        private void WriteStatus()
        {
            SetText(_frame.Status, _closest < 0 ? string.Empty
                : _exact ? T("reallife_set_headline_in_use", "preset", PresetName(_closest))
                : T("reallife_set_headline_differ", "preset", PresetName(_closest), "count", _counts[_closest].ToString(CultureInfo.InvariantCulture)));
        }

        private static string PresetName(int preset)
        {
            return Presets.NameKey((Intensity)preset).GetLocalization();
        }

        private static int PresetOf(SettingsLine line)
        {
            return line.Kind == LineKind.Button ? Array.IndexOf(PresetButtons, OptionsLayout.AppLabelKey(line)) : -1;
        }

        // The options that differ from the nearest preset, each with its own control and what the preset sets; the preset applied in one press.
        private void WriteDiffer()
        {
            if (_closest < 0)
            {
                _mode = ModePage;
                WritePage(_pages[_page]);
                return;
            }
            int count = SettingsRules.Differences(_now, _presetValues[_closest], _covered[_closest], _differ);
            string name = PresetName(_closest);
            SettingsLine presets = OptionsLayout.AppHeading(PresetsSection);
            Topic(0, presets != null ? OptionsLayout.AppLabel(presets) : string.Empty, T("reallife_set_headline_differ", "preset", name, "count", count.ToString(CultureInfo.InvariantCulture)), null);
            Paragraph(count == 0 ? T("reallife_set_box_same", "preset", name) : T("reallife_set_differ_note", "preset", name));
            SettingsLine apply = OptionsLayout.AppFind(PresetButtons[_closest]);
            if (apply != null && count > 0)
            {
                Own(T("reallife_set_apply_preset", "preset", name), PresetAbout[_closest].GetLocalization(), "reallife_opt_caption_apply", () => OptionsLayout.AppRun(apply));
            }
            for (int d = 0; d < _differ.Count; d++)
            {
                int i = _differ[d];
                OptionInfo info = _infos[i];
                SettingsLine line = OptionsLayout.AppFind(info.Id);
                int value = _presetValues[_closest][i];
                string sets = T("reallife_set_preset_sets", "preset", name, "value", line != null ? OptionsLayout.AppValueText(line, value) : Presets.Show(info, value));
                if (line == null || !OptionsLayout.AppShown(line))
                {
                    // an option on no row of the page: named, with both values
                    Paragraph(T("reallife_set_differ_elsewhere", "option", Presets.Label(info), "now", Presets.Show(info, _now[i])) + " " + sets);
                    continue;
                }
                Slot slot = Put(line);
                slot.Extra = sets;
                Fill(slot);
            }
        }
    }
}
