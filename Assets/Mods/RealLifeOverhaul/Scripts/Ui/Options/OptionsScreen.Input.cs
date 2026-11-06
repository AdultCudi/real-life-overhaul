using System;
using Localizor.LanguageChangeEvent;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    internal sealed partial class OptionsScreen
    {
        private float _choiceOffset;

        public int TypingCommand => _modsOpen ? UiSettingsList.SearchCommand : _wasDiff ? NameCommand : UiSettingsList.SearchCommand;

        public string TypingText => _modsOpen ? _settings.TypingText : _wasDiff ? _presetName : _query ?? string.Empty;

        public void Typed(string text)
        {
            if (_modsOpen)
            {
                _settings.Typed(text);
                _dirty = true;
                return;
            }
            text = text ?? string.Empty;
            if (_wasDiff)
            {
                _presetName = text;
            }
            else
            {
                _query = text;
                _mode = _query.Trim().Length >= 2 ? ModeFound : ModePage;
                _choice = -1;
                _held = -1;
            }
            _dirty = true;
        }

        public bool Press(int command, int arg)
        {
            if (_modsOpen)
            {
                bool done = _settings.Press(command, arg);
                _dirty |= _settings.Dirty;
                return done;
            }
            switch (command)
            {
                case UiSettingsList.ChoiceCommand: return PickChoice(arg);
                case UiSettingsList.GroupCommand: return OpenGroup(arg);
                case UiSettingsList.HistoryCommand: return History(arg);
                case UiSettingsList.PresetCommand: return PresetPress(arg);
                case UiSettingsList.RowCommand: return RowPress(arg);
            }
            return false;
        }

        public void Focused(int command, int arg)
        {
            if (_modsOpen)
            {
                _settings.Focused(command, arg);
            }
        }

        public bool Back()
        {
            if (_modsOpen)
            {
                return BackMods();
            }
            if (Busy)
            {
                return true;   // a key being awaited: Escape is the game's rebinding's to cancel, not a way out of Options
            }
            if (_choice >= 0)
            {
                _choice = -1;
                _dirty = true;
                return true;
            }
            if (_held >= 0)
            {
                _held = -1;
                _dirty = true;
                return true;
            }
            if (!_wasDiff && !string.IsNullOrEmpty(_query))
            {
                _query = string.Empty;
                _mode = ModePage;
                _dirty = true;
                return true;
            }
            return Close();
        }

        // Out of Options as the game's own Back goes: its custom difficulty panel first (it asks about changes not applied), then the window.
        private bool Close()
        {
            UI.MiniMenu.MiniMenu menu = PauseGame.Menu();
            if (menu == null || !Scenes.MainMenu.Options.IsVisible)
            {
                return false;
            }
            using (HotSpots.Game())
            {
                menu.CloseOptions();
            }
            return true;
        }

        public void Tab(int step)
        {
            if (_modsOpen)
            {
                _settings.Tab(step);
                _dirty = true;
                return;
            }
            int n = _wasDiff ? _names.Length : OptionsRules.Categories;
            if (n == 0 || Busy)
            {
                return;
            }
            int next = _chosen + (step < 0 ? -1 : 1);
            next = next < 0 ? n - 1 : next >= n ? 0 : next;
            OpenGroup(next);
        }

        public bool Act(UiAction action)
        {
            if (_modsOpen)
            {
                bool done = _settings.Act(action);
                _dirty |= _settings.Dirty;
                return done;
            }
            if (Busy)
            {
                return false;
            }
            if (_wasDiff)
            {
                return action == UiAction.Alt ? Apply() : action == UiAction.Option && Discard();
            }
            return action == UiAction.Option && AskReset();
        }

        public void Reveal(int list, int index)
        {
            if (_modsOpen)
            {
                _settings.Reveal(list, index);
                return;
            }
            if (_choice >= 0)
            {
                float row = UiTokens.ControlSmall;
                int first = (int)(_choiceOffset / row);
                if (index < first)
                {
                    _choiceOffset = index * row;
                }
                else if (index >= first + 8)
                {
                    _choiceOffset = (index - 7) * row;
                }
                _dirty = true;
                return;
            }
            _m.List.Reveal = index;
            _dirty = true;
        }

        public void Wheel(int clip, float notches)
        {
            if (_modsOpen)
            {
                _settings.Wheel(clip, notches);
                return;
            }
            if (_choice >= 0 && clip == _m.ChoiceClip)
            {
                _choiceOffset = Math.Max(0f, _choiceOffset - notches * UiTokens.ControlSmall);
            }
            else
            {
                _m.List.Offset -= notches * UiSettingsList.OptionHeight;
            }
            _dirty = true;
        }

        public void Page(int step)
        {
            if (_modsOpen)
            {
                _settings.Page(step);
                return;
            }
            _m.List.Offset -= step * UiTokens.S16 * 8f;
            _dirty = true;
        }

        // ------------------------------------------------------------------------------------------------- a slider held, stepped and dragged

        public bool Steps(int command) => _modsOpen ? _settings.Steps(command) : command == UiSettingsList.RowCommand && _held >= 0;

        public void Step(int command, int arg, int step)
        {
            if (_modsOpen)
            {
                _settings.Step(command, arg, step);
                return;
            }
            if (arg >= 0 && arg < _used && _slots[arg].Kind == Kind.Slider && _slots[arg].S != null)
            {
                Slot s = _slots[arg];
                Slide(s, OptionsRules.Nudged(s.S.value, step, s.S.minValue, s.S.maxValue, s.S.wholeNumbers));
            }
        }

        public void PointerDown(int command, int arg, float x, float y)
        {
            if (_modsOpen)
            {
                _settings.PointerDown(command, arg, x, y);
            }
            else if (command == UiSettingsList.RowCommand && arg >= 0 && arg < _used && _slots[arg].Kind == Kind.Slider)
            {
                DragTo(_slots[arg], x);
            }
        }

        public bool DragBegin(int command, int arg, float x, float y)
        {
            if (_modsOpen)
            {
                return _settings.DragBegin(command, arg, x, y);
            }
            if (command != UiSettingsList.RowCommand || arg < 0 || arg >= _used || _slots[arg].Kind != Kind.Slider || _slots[arg].Row.Greyed)
            {
                return false;
            }
            _held = arg;
            DragTo(_slots[arg], x);
            return true;
        }

        public void DragMove(float x, float y)
        {
            if (_modsOpen)
            {
                _settings.DragMove(x, y);
            }
            else if (_held >= 0 && _held < _used)
            {
                DragTo(_slots[_held], x);
            }
        }

        public void DragEnd(bool cancelled)
        {
            if (_modsOpen)
            {
                _settings.DragEnd(cancelled);
                return;
            }
            _held = -1;
            _dirty = true;
        }

        private void DragTo(Slot s, float x)
        {
            if (s.S == null || !s.S.interactable)
            {
                return;
            }
            float share = UiDraw.SliderShare(UiSettingsList.ControlWidth, s.Row.Value, (x - _controlX) / UiSettingsList.ControlWidth);
            Slide(s, OptionsRules.ValueAt(share, s.S.minValue, s.S.maxValue, s.S.wholeNumbers));
        }

        private void Slide(Slot s, float value)
        {
            if (Math.Abs(value - s.S.value) > 0.00001f && UiWidgets.Slide(s.S, value))
            {
                _dirty = true;
            }
        }

        // ------------------------------------------------------------------------------------------------- the groups and the rows

        private bool OpenGroup(int group)
        {
            if (group < 0 || group >= _names.Length)
            {
                return false;
            }
            if (!_wasDiff && group >= OptionsRules.Categories)
            {
                return OpenForeign(group - OptionsRules.Categories);
            }
            if (!_wasDiff)
            {
                if (_at < _past.Count - 1)
                {
                    _past.RemoveRange(_at + 1, _past.Count - _at - 1);
                }
                _past.Add(group);
                _at = _past.Count - 1;
                SelectGame(group);
            }
            _chosen = group;
            _mode = ModePage;
            _query = string.Empty;
            _choice = -1;
            _held = -1;
            _stamp++;
            _dirty = true;
            return true;
        }

        private bool History(int step)
        {
            int next = _at + step;
            if (_wasDiff || next < 0 || next >= _past.Count)
            {
                return false;
            }
            _at = next;
            _chosen = _past[next];
            _mode = ModePage;
            _query = string.Empty;
            SelectGame(_chosen);
            _stamp++;
            _dirty = true;
            return true;
        }

        // The game's own category button for the page on view, pressed so its own selection stands where the kit's does: its views are the
        // ones that stand active (what Reset this category resets, and what other mods' views key to).
        private void SelectGame(int category)
        {
            if (category < 0 || category >= OptionsRules.Categories)
            {
                return;
            }
            Transform tab = _o.transform.Find("Menu/" + OptionsRules.GameTabs[category]);
            Button b = tab != null ? tab.GetComponent<Button>() : null;
            if (b != null && b.interactable)
            {
                UiWidgets.Hand(b);
            }
        }

        // Another mod's tab: the game's own view of Options, with that tab chosen, until Options closes.
        private bool OpenForeign(int tab)
        {
            Button b = tab >= 0 && tab < _tabs.Count ? _tabs[tab].GetComponent<Button>() : null;
            if (b == null)
            {
                return false;
            }
            UiWidgets.Hand(b);
            OptionsKits.Classic();
            return true;
        }

        private bool RowPress(int arg)
        {
            if (arg < 0 || arg >= _used)
            {
                return false;
            }
            Slot s = _slots[arg];
            if (s.Row.Greyed)
            {
                return false;
            }
            if (Busy)
            {
                return true;
            }
            switch (s.Kind)
            {
                case Kind.Toggle:
                    return s.T != null && SetToggle(s.T, !s.T.isOn);
                case Kind.Slider:
                    _held = _held == arg ? -1 : arg;
                    _dirty = true;
                    return true;
                case Kind.Choice:
                case Kind.Scheme:
                    _choice = _choice == arg ? -1 : arg;
                    _choiceOffset = 0f;
                    _dirty = true;
                    return true;
                case Kind.Key:
                    return Rebind(s);
                case Kind.Group:
                    return OpenMods(s.Index);
                case Kind.Action:
                    return Run(s);
            }
            return false;
        }

        private bool SetToggle(Toggle t, bool on)
        {
            if (t == null || !t.interactable)
            {
                return false;
            }
            using (HotSpots.Game())
            {
                t.isOn = on;   // its own listeners run: the game's setting, other mods' patches on it
            }
            _dirty = true;
            return true;
        }

        private bool PickChoice(int option)
        {
            if (_choice < 0 || _choice >= _used)
            {
                return false;
            }
            Slot s = _slots[_choice];
            _choice = -1;
            _dirty = true;
            if (s.Kind == Kind.Scheme)
            {
                _scheme = option == 1 ? 1 : 0;
                _stamp++;
                return true;
            }
            if (s.D == null || option < 0 || (s.Choices != null && option >= s.Choices.Length) || Held(s.D))
            {
                return false;
            }
            using (HotSpots.Game())
            {
                s.D.SelectOption(option);
            }
            return true;
        }

        private bool Run(Slot s)
        {
            switch (s.Act)
            {
                case Do.SavePreset:
                case Do.CopyPreset:
                case Do.PastePreset:
                case Do.DeletePreset:
                    return RunDifficulty(s.Act);
                case Do.OpenClassic:
                    return OpenMore();
                case Do.ResetWindows:
                    return Hit(s.B, () => _o.ResetWindowsPosition());
                case Do.OpenRadio:
                    return Hit(null, () => _o.OpenRadioFolder());
                case Do.RefreshSongs:
                    return Hit(null, () => _o.RefreshSongs());
                case Do.Customize:
                    return Hit(s.B, () => _o.OpenDifficultyCustomizeUI());
                case Do.AskDeletion:
                    return Hit(s.B, () => _o.RequestDataDeletion());
                default:
                    return Hit(s.B, null);
            }
        }

        // The game's own button pressed as a click presses it (its listeners and other mods' on it run), or the public method that button calls.
        private bool Hit(Button b, Action method)
        {
            if (b != null && b.interactable && Free(b))
            {
                UiWidgets.Hand(b);
            }
            else if (method != null)
            {
                using (HotSpots.Game())
                {
                    method();
                }
            }
            else
            {
                return false;
            }
            _stamp++;
            _dirty = true;
            return true;
        }

        // A row only the game's own list of other mods' options can draw: that list, until Options closes.
        private bool OpenMore()
        {
            SelectGame(OptionsRules.Mods);
            OptionsKits.Classic();
            return true;
        }

        // ------------------------------------------------------------------------------------------------- the resets, each through the game's question

        private bool PresetPress(int arg)
        {
            if (_wasDiff)
            {
                return arg == 0 ? Discard() : Apply();
            }
            return arg == 0 ? AskReset() : AskResetAll();
        }

        private bool AskReset()
        {
            if (_mode == ModeFound || _chosen < 0 || _chosen >= OptionsRules.Categories)
            {
                return false;
            }
            LanguageChangeEventDataHolder body = LanguageChangeEventDataHolder.Create("reallife_ui_opt_reset_category_body", new { category = _names[_chosen] });
            GameConfirm.Show(default, body, ResetNow, null, "reallife_ui_opt_reset_yes", null, true, true);
            return true;
        }

        private void ResetNow()
        {
            SelectGame(_chosen);
            Button b = UiWidgets.Calling(_o, "ResetSelectedCategoryToDefaults");
            if (b != null && b.interactable)
            {
                UiWidgets.Hand(b);
            }
            else
            {
                using (HotSpots.Game())
                {
                    _o.ResetSelectedCategoryToDefaults();
                }
            }
            _stamp++;
            _dirty = true;
        }

        // The game's own Reset all, with its own question.
        private bool AskResetAll()
        {
            Button b = UiWidgets.Calling(_o, "ResetAllCategoriesToDefaultsWithConfirm");
            if (b != null && b.interactable)
            {
                UiWidgets.Hand(b);
            }
            else
            {
                using (HotSpots.Game())
                {
                    _o.ResetAllCategoriesToDefaultsWithConfirm();
                }
            }
            _stamp++;
            return true;
        }
    }
}
