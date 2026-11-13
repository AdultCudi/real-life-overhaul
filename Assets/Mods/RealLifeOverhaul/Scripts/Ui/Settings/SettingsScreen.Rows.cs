using System;
using System.Globalization;
using RealLifeOverhaul.Meta;

namespace RealLifeOverhaul.Ui
{
    public sealed partial class SettingsScreen
    {
        private enum SlotKind : byte
        {
            Heading = 0,
            About,
            Switch,
            Slider,
            Choice,
            Action,
            Key
        }

        private sealed class Slot
        {
            public SlotKind Kind;
            public SettingsLine Line;
            public string Guide;
            public int Value;
            public int Low;
            public int High;
            public string[] Choices;
            public UiSettingRow Row;
            public KitForOtherMods.Setting Mod;   // a row another mod added through the kit's interface (Line is null then)
        }

        private struct FindLine
        {
            public SettingsLine Line;
            public string Text;
            public string Section;
            public string Group;
            public int GroupIndex;
        }

        private int _stamp;
        private int _builtStamp = -1;
        private int _builtGroup = -2;
        private int _builtMode = -1;
        private string _builtQuery;
        private int _chromeStamp = -1;

        private void EnsureGroups()
        {
            bool tests = Settings.ShowTestTools;
            if (_groupNames.Length > 0 && tests == _tests)
            {
                return;
            }
            _tests = tests;
            SettingsTab[] tabs = OptionsLayout.AppTabs;
            int n = tabs == null ? 0 : tabs.Length + (tests ? 1 : 0);
            _groupNames = new string[n];
            _groupIcons = new UiIcon[n];
            _groupCounts = new int[n];
            _groupFound = new int[n];
            for (int i = 0; i < n && tabs != null && i < tabs.Length; i++)
            {
                TreeGroup tree = tabs[i].Group;
                _groupNames[i] = T(tree.TitleKey);
                _groupIcons[i] = IconOf(tree.Id);
                int sections, options;
                OptionsTree.Counts(tree, out sections, out options);
                _groupCounts[i] = options;
            }
            if (tests && n > 0)
            {
                _groupNames[n - 1] = T("reallife_meta_title_tests");
                _groupIcons[n - 1] = UiIcon.Bug;
                _groupCounts[n - 1] = CountTests(tabs);
            }
            if (_group >= n)
            {
                _group = 0;
            }
            _indexed = false;
            _stamp++;
        }

        private static int CountTests(SettingsTab[] tabs)
        {
            int count = 0;
            if (tabs == null)
            {
                return 0;
            }
            for (int g = 0; g < tabs.Length; g++)
            {
                for (int i = 0; i < tabs[g].Lines.Count; i++)
                {
                    SettingsLine line = tabs[g].Lines[i];
                    if (line.Kind == LineKind.Test && OptionsLayout.AppShown(line))
                    {
                        count++;
                    }
                }
            }
            return count;
        }

        private void EnsureIndex()
        {
            if (_indexed)
            {
                return;
            }
            _index.Clear();
            SettingsTab[] tabs = OptionsLayout.AppTabs;
            if (tabs == null)
            {
                _indexed = true;
                return;
            }
            for (int g = 0; g < tabs.Length; g++)
            {
                string group = g < _groupNames.Length ? _groupNames[g].ToLowerInvariant() : string.Empty;
                for (int i = 0; i < tabs[g].Lines.Count; i++)
                {
                    SettingsLine line = tabs[g].Lines[i];
                    if (line.IsHeading || !OptionsLayout.AppShown(line))
                    {
                        continue;
                    }
                    if ((line.Kind == LineKind.Test || line.Kind == LineKind.TestHeading) && !_tests)
                    {
                        continue;
                    }
                    string section = line.Section != null ? T(line.Section.TitleKey).ToLowerInvariant() : string.Empty;
                    _index.Add(new FindLine
                    {
                        Line = line,
                        Text = OptionsLayout.AppSearchText(line),
                        Section = section,
                        Group = group,
                        GroupIndex = line.Kind == LineKind.Test ? tabs.Length : g
                    });
                }
            }
            _indexed = true;
        }

        // A section or an option asked for from outside: its group opens on the list, the search and the other views cleared.
        private void TakeWanted()
        {
            string target = _wanted;
            _wanted = null;
            if (target == null)
            {
                return;
            }
            EnsureGroups();
            bool section = target.StartsWith("s:", StringComparison.Ordinal);
            SettingsLine line = section ? OptionsLayout.AppHeading(target.Substring(2)) : OptionsLayout.AppFind(target);
            SettingsTab[] tabs = OptionsLayout.AppTabs;
            int group = -1;
            for (int g = 0; line != null && tabs != null && g < tabs.Length && group < 0; g++)
            {
                group = tabs[g].Lines.Contains(line) ? g : -1;
            }
            if (group < 0)
            {
                return;   // the page has no such place: the screen stays where the player left it
            }
            _query = string.Empty;
            _mode = ModePage;
            _group = group;
            _scrollTo = section ? null : line;
            _scrollToHeading = section ? OptionsLayout.AppLabel(line) : null;
            _builtGroup = -1;   // filled again for that group
        }

        // The row asked for, found among the rows just filled and brought to the top of the list.
        private void ScrollToWanted()
        {
            if (_scrollTo == null && _scrollToHeading == null)
            {
                return;
            }
            float top = 0f;
            for (int i = 0; i < _used; i++)
            {
                bool hit = _scrollTo != null ? _slots[i].Line == _scrollTo : _slots[i].Row.Control == UiSettingControl.None && _slots[i].Row.Name == _scrollToHeading;
                if (hit)
                {
                    float lead = _scrollTo != null && i > 0 && _slots[i - 1].Row.Control == UiSettingControl.None ? UiSettingsList.SectionHeight : 0f;   // its section's band over it
                    _model.Offset = Math.Max(0f, top - lead);
                    break;
                }
                top += UiSettingsList.RowHeight(ref _slots[i].Row);
            }
            _scrollTo = null;
            _scrollToHeading = null;
        }

        private void Fill()
        {
            TakeWanted();
            bool view = _builtGroup != _group || _builtMode != _mode || _builtQuery != _query;
            if (view || _builtStamp != _stamp)
            {
                Rebuild();
                if (view)
                {
                    _model.Offset = 0f;
                }
                _builtStamp = _stamp;
                _builtGroup = _group;
                _builtMode = _mode;
                _builtQuery = _query;
            }
            else
            {
                Touch();
            }
            if (_model.Rows == null || _model.Rows.Length < _used)
            {
                _model.Rows = new UiSettingRow[Math.Max(8, _used)];
            }
            for (int i = 0; i < _used; i++)
            {
                _model.Rows[i] = _slots[i].Row;
            }
            _model.RowCount = _used;
            ScrollToWanted();
            Chrome();
        }

        private void Rebuild()
        {
            _used = 0;
            _choice = -1;
            RefreshPreset();
            if (_mode == Found)
            {
                FillFound();
            }
            else if (_mode == Differ)
            {
                FillDiffer();
            }
            else if (_tests && _group == _groupNames.Length - 1)
            {
                FillTests();
            }
            else
            {
                FillGroup(_group);
            }
        }

        private void FillGroup(int group)
        {
            SettingsTab[] tabs = OptionsLayout.AppTabs;
            if (tabs == null || group < 0 || group >= tabs.Length)
            {
                return;
            }
            int sections, options;
            OptionsTree.Counts(tabs[group].Group, out sections, out options);
            AddHeading(Fill("reallife_set_group_count", "sections", sections.ToString(_inv), "options", options.ToString(_inv)), UiIcon.Info);
            SettingsLine heading = null;
            for (int i = 0; i < tabs[group].Lines.Count; i++)
            {
                SettingsLine line = tabs[group].Lines[i];
                if (line.Kind == LineKind.Test || line.Kind == LineKind.TestHeading)
                {
                    continue;
                }
                if (line.IsHeading)
                {
                    heading = line;
                    continue;
                }
                if (!OptionsLayout.AppShown(line))
                {
                    continue;
                }
                if (heading != null)
                {
                    AddHeading(OptionsLayout.AppLabel(heading), IconOf(tabs[group].Group.Id));
                    AddAbout(heading.Section);
                    heading = null;
                }
                AddControl(line);
            }
            AddModRows(tabs[group]);
        }

        // The rows other mods added (KitForOtherMods.AddSetting), after the interface's own section: on and off, each its mod's own to set.
        private void AddModRows(SettingsTab tab)
        {
            bool here = false;
            for (int i = 0; i < tab.Group.Sections.Count; i++)
            {
                here |= tab.Group.Sections[i].Id == "ui";
            }
            if (!here || KitForOtherMods.SettingRows.Count == 0)
            {
                return;
            }
            AddHeading(T("reallife_ui_shell_other_mods"), UiIcon.Code);
            for (int i = 0; i < KitForOtherMods.SettingRows.Count; i++)
            {
                Slot slot = Take();
                slot.Kind = SlotKind.Switch;
                slot.Line = null;
                slot.Guide = null;
                slot.Choices = null;
                slot.Mod = KitForOtherMods.SettingRows[i];
                slot.Row = new UiSettingRow { Control = UiSettingControl.Switch, Name = slot.Mod.Label };
                WriteMod(slot);
            }
        }

        // A mod's row read as it stands: its own getter, guarded, as every call into another mod's code is.
        private void WriteMod(Slot slot)
        {
            bool on = false;
            try
            {
                on = slot.Mod.Get();
            }
            catch (Exception e)
            {
                UiForeign.Note("settings", UiPieceKind.Fault, UiForeignGame.Blame(e) ?? slot.Mod.Id, e.GetType().Name + " reading its row", "its row reads as off");
            }
            slot.Value = on ? 1 : 0;
            slot.Row.On = on;
            slot.Row.Value = on ? T("reallife_meta_on") : T("reallife_meta_off");
        }

        private void FillTests()
        {
            SettingsTab[] tabs = OptionsLayout.AppTabs;
            AddHeading(T("reallife_set_box_tests"), UiIcon.Bug);
            if (tabs == null)
            {
                return;
            }
            for (int g = 0; g < tabs.Length; g++)
            {
                SettingsLine heading = null;
                for (int i = 0; i < tabs[g].Lines.Count; i++)
                {
                    SettingsLine line = tabs[g].Lines[i];
                    if (line.Kind != LineKind.Test && line.Kind != LineKind.TestHeading)
                    {
                        continue;
                    }
                    if (line.IsHeading)
                    {
                        heading = line;
                        continue;
                    }
                    if (!OptionsLayout.AppShown(line))
                    {
                        continue;
                    }
                    if (heading != null)
                    {
                        AddHeading(OptionsLayout.AppLabel(heading), UiIcon.Bug);
                        heading = null;
                    }
                    AddControl(line);
                }
            }
        }

        private void FillFound()
        {
            for (int i = 0; i < _groupFound.Length; i++)
            {
                _groupFound[i] = 0;
            }
            OptionsFind.Split(_query, _words);
            int shown = 0;
            for (int i = 0; i < _index.Count && shown < SettingsRules.MostFound; i++)
            {
                FindLine found = _index[i];
                if (!OptionsFind.Matches(_words, found.Text, found.Section, found.Group))
                {
                    continue;
                }
                AddControl(found.Line);
                if (found.GroupIndex >= 0 && found.GroupIndex < _groupFound.Length)
                {
                    _groupFound[found.GroupIndex]++;
                }
                shown++;
            }
            _model.Found = shown.ToString(_inv);
            _model.EmptyTitle = T("reallife_set_empty_found");
            _model.EmptyBody = T("reallife_set_box_found_none");
        }

        private void FillDiffer()
        {
            int shown = 0;
            for (int i = 0; i < _index.Count && shown < SettingsRules.MostFound; i++)
            {
                SettingsLine line = _index[i].Line;
                if (line.Kind != LineKind.Option || !Differs(line))
                {
                    continue;
                }
                AddControl(line);
                shown++;
            }
            _model.EmptyTitle = T("reallife_set_box_same").Replace("{preset}", _presetName);
            _model.EmptyBody = T("reallife_set_box_same");
        }

        private void AddHeading(string name, UiIcon icon)
        {
            Slot slot = Take();
            slot.Kind = SlotKind.Heading;
            slot.Line = null;
            slot.Guide = null;
            slot.Choices = null;
            slot.Row = new UiSettingRow { Control = UiSettingControl.None, Name = name, Icon = icon };
        }

        private void AddAbout(TreeSection section)
        {
            string page = OptionsTree.GuidePage(section);
            if (page == null)
            {
                return;
            }
            Slot slot = Take();
            slot.Kind = SlotKind.About;
            slot.Line = null;
            slot.Guide = page;
            slot.Choices = null;
            slot.Row = new UiSettingRow { Control = UiSettingControl.Action, Name = T("reallife_set_about"), Tip = T("reallife_set_about_tip"), Value = T("reallife_set_about") };
        }

        private void AddControl(SettingsLine line)
        {
            Slot slot = Take();
            slot.Line = line;
            slot.Guide = null;
            slot.Choices = null;
            ControlKind kind = OptionsLayout.AppControl(line);
            if (line.Kind == LineKind.Key || kind == ControlKind.Key)
            {
                slot.Kind = SlotKind.Key;
            }
            else if (line.Kind == LineKind.Button || line.Kind == LineKind.Test || kind == ControlKind.Action)
            {
                slot.Kind = SlotKind.Action;
            }
            else if (kind == ControlKind.Slider)
            {
                slot.Kind = SlotKind.Slider;
            }
            else if (kind == ControlKind.Choice)
            {
                slot.Kind = SlotKind.Choice;
            }
            else
            {
                slot.Kind = SlotKind.Switch;
            }
            Write(slot, true);
        }

        private void Touch()
        {
            for (int i = 0; i < _used; i++)
            {
                Slot slot = _slots[i];
                if (slot.Line == null)
                {
                    if (slot.Mod != null)
                    {
                        int before = slot.Value;
                        WriteMod(slot);
                        _dirty |= before != slot.Value;
                    }
                    continue;
                }
                int value = slot.Kind == SlotKind.Switch || slot.Kind == SlotKind.Slider || slot.Kind == SlotKind.Choice ? OptionsLayout.AppValue(slot.Line) : slot.Value;
                bool grey = Grey(slot.Line, slot.Kind);
                bool held = _held == i;
                bool listen = Listening(slot);
                if (value != slot.Value || SwitchMoved(slot, value) || grey != slot.Row.Greyed || held != slot.Row.Held || listen != slot.Row.Listening || Differs(slot.Line) != slot.Row.Differs)
                {
                    Write(slot, false);
                }
            }
        }

        // A switch that reads differently from its row: its screen faulted, or came back, since it was written.
        private static bool SwitchMoved(Slot slot, int value) =>
            slot.Kind == SlotKind.Switch && slot.Row.On != (value != 0 && !UiScreenSwitches.Faulted(OptionsLayout.AppId(slot.Line)));

        private void Write(Slot slot, bool named)
        {
            SettingsLine line = slot.Line;
            UiSettingRow row = slot.Row;
            if (named)
            {
                row.Name = OptionsLayout.AppLabel(line);
                row.Icon = UiIcon.None;
            }
            row.Greyed = Grey(line, slot.Kind);
            row.Differs = Differs(line);
            row.Held = _held >= 0 && _slots[_held] == slot;
            row.Listening = Listening(slot);
            row.MatchStart = 0;
            row.MatchLength = 0;
            if (_mode == Found && !string.IsNullOrEmpty(row.Name))
            {
                int at = row.Name.IndexOf(_query.Trim(), StringComparison.OrdinalIgnoreCase);
                if (at >= 0)
                {
                    row.MatchStart = at;
                    row.MatchLength = _query.Trim().Length;
                }
            }
            switch (slot.Kind)
            {
                case SlotKind.Switch:
                    slot.Value = OptionsLayout.AppValue(line);
                    row.Control = UiSettingControl.Switch;
                    bool faulted = UiScreenSwitches.Faulted(OptionsLayout.AppId(line));   // the kit faulted on this screen: it reads off, with the words that say why
                    row.On = slot.Value != 0 && !faulted;
                    row.Value = row.On ? T("reallife_meta_on") : T("reallife_meta_off");
                    row.Tip = faulted ? T("reallife_ui_note_fallback") : Tip(line, row.Greyed);
                    break;
                case SlotKind.Slider:
                    OptionsLayout.AppRange(line, out int min, out int max);
                    slot.Value = SettingsRules.SliderStart(min, max, OptionsLayout.AppValue(line), out slot.Low, out slot.High);
                    row.Control = UiSettingControl.Slider;
                    row.Value = OptionsLayout.AppValueText(line, slot.Value);
                    row.Share = slot.High > slot.Low ? (slot.Value - slot.Low) / (float)(slot.High - slot.Low) : 0f;
                    row.Tip = Tip(line, row.Greyed);
                    break;
                case SlotKind.Choice:
                    OptionsLayout.AppRange(line, out int first, out int last);
                    slot.Low = first;
                    slot.High = last;
                    slot.Value = OptionsLayout.AppValue(line);
                    if (slot.Choices == null || slot.Choices.Length != last - first + 1)
                    {
                        slot.Choices = new string[Math.Max(0, last - first + 1)];
                        for (int i = first; i <= last; i++)
                        {
                            slot.Choices[i - first] = OptionsLayout.AppValueText(line, i);
                        }
                    }
                    row.Control = UiSettingControl.Choice;
                    row.Value = OptionsLayout.AppValueText(line, slot.Value);
                    row.Tip = Tip(line, row.Greyed);
                    break;
                case SlotKind.Key:
                    int action = OptionsLayout.AppKeyAction(line);
                    row.Control = UiSettingControl.Key;
                    row.Listening = action >= 0 && _listen == action;
                    row.Value = row.Listening ? T("reallife_set_press") : action >= 0 ? Keys.Label((RealLifeOverhaul.KeyAction)action) : "-";
                    row.Tip = T("reallife_set_keys_note");
                    break;
                default:
                    row.Control = UiSettingControl.Action;
                    row.Value = line.Kind == LineKind.Test ? T("reallife_opt_caption_run") : T(OptionsLayout.AppCaptionKey(line));
                    row.Tip = null;
                    break;
            }
            slot.Row = row;
        }

        private static string Tip(SettingsLine line, bool grey)
        {
            if (grey)
            {
                string blocker = OptionsLayout.AppBlocker(line);
                if (blocker != null)
                {
                    return T("reallife_set_box_needs_line").Replace("{option}", OptionsLayout.AppLabelOf(blocker));
                }
            }
            return OptionsLayout.AppTip(line);
        }

        private static bool Grey(SettingsLine line, SlotKind kind)
        {
            if (line == null || kind == SlotKind.Action || kind == SlotKind.Key || kind == SlotKind.Heading || kind == SlotKind.About)
            {
                return false;
            }
            return OptionsLayout.AppAside(line) != null || OptionsLayout.AppBlocker(line) != null;
        }

        private bool Differs(SettingsLine line)
        {
            string id = OptionsLayout.AppId(line);
            if (id == null)
            {
                return false;
            }
            OptionInfo info = SettingsTable.Get(id);
            int want;
            return info != null && Presets.TryValue(info, _nearest, out want) && SettingsTable.Current(info) != want;
        }

        private bool Listening(Slot slot)
        {
            if (slot.Kind != SlotKind.Key || slot.Line == null)
            {
                return false;
            }
            return OptionsLayout.AppKeyAction(slot.Line) == _listen && _listen >= 0;
        }

        private Slot Take()
        {
            if (_used >= _slots.Count)
            {
                _slots.Add(new Slot());
            }
            Slot slot = _slots[_used];
            _used++;
            slot.Mod = null;
            return slot;
        }

        private void Chrome()
        {
            if (_chromeStamp != _presetGen)
            {
                RefreshPreset();
                _model.Preset = T("reallife_set_headline_in_use").Replace("{preset}", _presetName);
                _model.Differs = _differ > 0 ? T("reallife_set_differ_count").Replace("{count}", _differ.ToString(_inv)) : null;
                _model.PresetActions = _differ > 0
                    ? new[]
                    {
                        new UiActionModel { Label = T("reallife_set_list_differ"), Kind = UiButtonKind.Secondary, Key = UiAction.Option },
                        new UiActionModel { Label = T("reallife_set_apply_preset").Replace("{preset}", _presetName), Kind = UiButtonKind.Primary, Key = UiAction.Alt }
                    }
                    : new[]
                    {
                        new UiActionModel { Label = T("reallife_set_apply_preset").Replace("{preset}", _presetName), Kind = UiButtonKind.Primary, Key = UiAction.Alt }
                    };
                _model.SearchPlaceholder = T("reallife_set_search_hint");
                _chromeStamp = _presetGen;
            }
            _model.Query = _query;
            _model.CanBack = _at > 0;
            _model.CanForward = _at >= 0 && _at < _past.Count - 1;
            if (_mode != Found)
            {
                _model.Found = null;
            }
        }

        private void RefreshPreset()
        {
            Intensity? now = Presets.Detect();
            _exact = now.HasValue;
            _nearest = now ?? Intensity.Standard;
            int ignored;
            _differ = _exact ? 0 : Presets.CountChanges(_nearest, out ignored);
            _presetName = Presets.CurrentName();
        }

        private bool OpenGroup(int group, bool remember)
        {
            if (group < 0 || group >= _groupNames.Length)
            {
                return false;
            }
            if (remember)
            {
                if (_at < _past.Count - 1)
                {
                    _past.RemoveRange(_at + 1, _past.Count - _at - 1);
                }
                _past.Add(group);
                _at = _past.Count - 1;
            }
            _group = group;
            _mode = ModePage;
            _query = string.Empty;
            _choice = -1;
            _held = -1;
            _dirty = true;
            return true;
        }

        private bool History(int step)
        {
            int next = _at + step;
            if (next < 0 || next >= _past.Count)
            {
                return false;
            }
            _at = next;
            _group = _past[next];
            _mode = ModePage;
            _query = string.Empty;
            _dirty = true;
            return true;
        }

        private bool RowPress(int arg)
        {
            if (arg < 0 || arg >= _used)
            {
                return false;
            }
            Slot slot = _slots[arg];
            if (slot.Row.Greyed)
            {
                return false;
            }
            if (slot.Kind == SlotKind.About)
            {
                GuideApp.Open(slot.Guide);
                return true;
            }
            if (slot.Kind == SlotKind.Switch)
            {
                if (slot.Mod == null && UiScreenSwitches.Faulted(OptionsLayout.AppId(slot.Line)))
                {
                    UiScreenSwitches.Retry(OptionsLayout.AppId(slot.Line));   // it reads off: pressed it gives the screen another try
                    UiScreenSwitches.Changed?.Invoke();
                    _dirty = true;
                    return true;
                }
                if (slot.Mod != null)
                {
                    try
                    {
                        slot.Mod.Set(slot.Value == 0);
                    }
                    catch (Exception e)
                    {
                        UiForeign.Note("settings", UiPieceKind.Fault, UiForeignGame.Blame(e) ?? slot.Mod.Id, e.GetType().Name + " setting its row", "the row is left as it was");
                    }
                    WriteMod(slot);
                    _dirty = true;
                    return true;
                }
                return Set(slot, slot.Value == 0 ? 1 : 0, true);
            }
            if (slot.Kind == SlotKind.Slider)
            {
                _held = _held == arg ? -1 : arg;
                _dirty = true;
                return true;
            }
            if (slot.Kind == SlotKind.Choice)
            {
                _choice = _choice == arg ? -1 : arg;
                _dirty = true;
                return true;
            }
            if (slot.Kind == SlotKind.Key)
            {
                int action = OptionsLayout.AppKeyAction(slot.Line);
                if (action < 0)
                {
                    return false;
                }
                Keys.Rebind((RealLifeOverhaul.KeyAction)action);
                return true;
            }
            if (slot.Kind == SlotKind.Action && slot.Line != null)
            {
                OptionsLayout.AppRun(slot.Line);
                _stamp++;
                _dirty = true;
                return true;
            }
            return false;
        }

        private bool PickChoice(int option)
        {
            if (_choice < 0 || _choice >= _used)
            {
                return false;
            }
            Slot slot = _slots[_choice];
            _choice = -1;
            return Set(slot, slot.Low + option, true);
        }

        private bool Set(Slot slot, int value, bool preset)
        {
            if (slot.Line == null || !OptionsLayout.AppSet(slot.Line, value))
            {
                return false;
            }
            if (preset)
            {
                _presetGen++;
            }
            _dirty = true;
            return true;
        }

        private int _presetGen;

        private void Nudge(Slot slot, int step)
        {
            Set(slot, slot.Value + step, false);
        }

        private void DragTo(Slot slot, float x)
        {
            float share = UiDraw.SliderShare(UiSettingsList.ControlWidth, slot.Row.Value, (x - _sliderX) / UiSettingsList.ControlWidth);
            int span = slot.High - slot.Low;
            int value = slot.Low + (int)Math.Round(span * share, MidpointRounding.AwayFromZero);
            if (value != slot.Value)
            {
                Set(slot, value, false);
            }
        }

        private bool PresetPress(int arg)
        {
            if (_differ > 0 && arg == 0)
            {
                return ShowDiffer();
            }
            return ApplyNearest();
        }

        private bool ApplyNearest()
        {
            int index = (int)_nearest;
            if (index < 0 || index >= PresetKeys.Length)
            {
                return false;
            }
            SettingsLine line = OptionsLayout.AppFind(PresetKeys[index]);
            if (line == null)
            {
                return false;
            }
            OptionsLayout.AppRun(line);
            return true;
        }

        private bool ShowDiffer()
        {
            if (_differ <= 0)
            {
                return false;
            }
            _mode = Differ;
            _query = string.Empty;
            _stamp++;
            _dirty = true;
            return true;
        }

        private void ChoiceAt(UiFrame frame, int row, out float x, out float y)
        {
            float top = 0f;
            for (int i = 0; i < row && i < _used; i++)
            {
                top += _slots[i].Row.Control == UiSettingControl.None ? UiSettingsList.SectionHeight : UiSettingsList.OptionHeight;
            }
            x = _sliderX;
            float rows = RowsTop(frame);
            y = rows + top - _model.Offset + UiSettingsList.OptionHeight + UiTokens.S1;
        }

        private void RememberSlider(UiFrame frame)
        {
            frame.Window(out float wx, out float _, out float ww, out float _);
            float x = wx + UiAppWindow.Sidebar + UiTokens.S5;
            float w = wx + ww - UiTokens.S5 - x;
            _sliderX = x + w - UiTokens.S3 - UiSettingsList.ControlWidth;
        }

        private float RowsTop(UiFrame frame)
        {
            frame.Window(out float _, out float wy, out float _, out float _);
            float y = wy + UiFrame.Strip + UiTokens.S4 + UiTokens.Control + UiTokens.S4;
            if (!string.IsNullOrEmpty(_model.Preset))
            {
                y += UiTokens.Control + UiTokens.S2 * 2f + UiTokens.S3;
            }
            return y;
        }

        private static UiIcon IconOf(string id)
        {
            switch (id)
            {
                case "start": return UiIcon.Info;
                case "you": return UiIcon.Person;
                case "city": return UiIcon.Building;
                case "around": return UiIcon.Car;
                case "work": return UiIcon.Briefcase;
                case "money": return UiIcon.Coin;
                case "business": return UiIcon.Store;
                case "controls": return UiIcon.Key;
                case "performance": return UiIcon.Cpu;
                case "trouble": return UiIcon.Wrench;
                default: return UiIcon.Gear;
            }
        }
    }
}
