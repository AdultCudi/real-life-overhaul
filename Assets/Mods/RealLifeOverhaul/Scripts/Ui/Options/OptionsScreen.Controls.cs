using System;
using Localizor.LanguageChangeEvent;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    internal sealed partial class OptionsScreen
    {
        // One of the game's rebinding rows (its RebindActionUI), read once as the page is built; its words are read from its own text.
        private sealed class Bind
        {
            public Component Row;
            public TMP_Text Text;
        }

        private OptionsKey[] _keys = new OptionsKey[0];
        private Bind[] _binds = new Bind[0];
        private int[] _clash = new int[0];
        private int _keyCount;
        private int _scheme;                 // 0 the keyboard's rows, 1 the gamepad's, when the game's rows carry both
        private int _listen = -1;            // the key row waiting for its new key
        private int _listenFrame, _endFrame = -10;

        private void Controls()
        {
            Heading("reallife_ui_opt_sec_play", UiIcon.GameController);
            AddToggle("menu_options_controls_vehicle_mouse_input", Field<Toggle>(GameContract.OptMouseDrive));
            AddToggle("menu_options_controls_invert_rotation", Field<Toggle>(GameContract.OptInvert));
            AddToggle("menu_options_controls_run_by_default_indoor", Field<Toggle>(GameContract.OptRun));
            AddToggle("menu_options_controls_steering_assist", Field<Toggle>(GameContract.OptSteer));
            ReadBinds();
            bool both = OptionsRules.TwoSchemes(_keys, _keyCount);
            if (!both)
            {
                _scheme = _keyCount > 0 && _keys[0].Gamepad ? 1 : 0;
            }
            if (both)
            {
                Take(Kind.Scheme).NameKey = "reallife_ui_opt_scheme";
            }
            _rows.Clear();
            _source.Clear();
            for (int i = 0; i < _keyCount; i++)
            {
                _keys[i].Binding = _binds[i].Text != null ? UiWidgets.Text(_binds[i].Text) : null;
            }
            OptionsPages.KeyRows(_keys, _keyCount, _clash, _scheme == 1, both, Words, _rows, _source);
            for (int i = 0; i < _rows.Count; i++)
            {
                int k = _source[i];
                Slot s = Take(k < 0 ? Kind.Heading : Kind.Key);
                s.Row = _rows[i];
                if (k >= 0)
                {
                    s.Index = k;
                    s.Text = _binds[k].Text;
                }
            }
        }

        private readonly System.Collections.Generic.List<UiSettingRow> _rows = new System.Collections.Generic.List<UiSettingRow>(96);
        private readonly System.Collections.Generic.List<int> _source = new System.Collections.Generic.List<int>(96);

        private static readonly UiWords Words = (key, english) => UiText.T(key);

        // The game's rows, in the game's order: each action's name and the key now bound, the control it is bound to and the scheme it belongs to.
        private void ReadBinds()
        {
            _keyCount = 0;
            Transform root = Field<Transform>(GameContract.OptBindings);
            System.Type rowType = GameContract.RebindRow.Type;
            Component[] rows = root != null && rowType != null ? root.GetComponentsInChildren(rowType, true) : new Component[0];
            if (_keys.Length < rows.Length)
            {
                _keys = new OptionsKey[rows.Length];
                _binds = new Bind[rows.Length];
                _clash = new int[rows.Length];
            }
            for (int i = 0; i < rows.Length; i++)
            {
                Component row = rows[i];
                if (!row.gameObject.activeSelf || GameContract.RebindAction.Property == null)
                {
                    continue;
                }
                var reference = GameContract.RebindAction.Property.GetValue(row, null) as InputActionReference;
                InputAction action = reference != null ? reference.action : null;
                string id = GameContract.RebindId.Property != null ? GameContract.RebindId.Property.GetValue(row, null) as string : null;
                if (action == null || string.IsNullOrEmpty(id))
                {
                    continue;
                }
                int at = -1;
                for (int b = 0; b < action.bindings.Count && at < 0; b++)
                {
                    at = string.Equals(action.bindings[b].id.ToString(), id, StringComparison.OrdinalIgnoreCase) ? b : -1;
                }
                if (at < 0)
                {
                    continue;
                }
                InputBinding binding = action.bindings[at];
                var text = GameContract.RebindText.Property.GetValue(row, null) as TextLocalizationComponent;
                var label = GameContract.RebindLabel.Property.GetValue(row, null) as TextLocalizationComponent;
                string name = label != null && label.TextContainer != null ? UiWidgets.Text(label.TextContainer) : null;
                string path = binding.isComposite ? null : binding.effectivePath;
                bool pad = OptionsRules.GamepadScheme(binding.groups) || (path != null && path.StartsWith("<Gamepad>", StringComparison.OrdinalIgnoreCase))
                           || (path != null && path.IndexOf("Controller>", StringComparison.OrdinalIgnoreCase) >= 0);
                _keys[_keyCount] = new OptionsKey
                {
                    Action = action.name,
                    Label = string.IsNullOrEmpty(name) ? ActionName(action.name) : name,
                    Path = path,
                    Gamepad = pad,
                    Composite = binding.isComposite
                };
                _binds[_keyCount] = new Bind { Row = row, Text = text != null ? text.TextContainer : null };
                _keyCount++;
            }
            OptionsRules.Shared(_keys, _keyCount, _clash);
        }

        // The game's own words for an action, or its name when the game has none.
        private static string ActionName(string action)
        {
            string key = "input_key_" + action.ToLowerInvariant();
            string words = T(key);
            return string.IsNullOrEmpty(words) || words == key ? action : words;
        }

        private void WriteKey(Slot s, ref UiSettingRow r)
        {
            r.Control = UiSettingControl.Key;
            bool listening = _listen == s.Index && _listen >= 0;
            r.Listening = listening;
            string words = s.Text != null ? UiWidgets.Text(s.Text) : null;
            r.Value = listening ? (Taken() ? T("reallife_ui_opt_taken") : T("reallife_set_press")) : string.IsNullOrEmpty(words) ? "-" : words;
            r.Greyed = false;
        }

        // The key that was tried is already used by another action: the game goes on waiting, and says so in its own text.
        private bool Taken()
        {
            if (_listen < 0 || _listen >= _keyCount || GameContract.RebindCollision.Property == null)
            {
                return false;
            }
            return GameContract.RebindCollision.Property.GetValue(_binds[_listen].Row, null) is GameObject g && g.activeSelf;
        }

        private bool Rebind(Slot s)
        {
            if (s.Index < 0 || s.Index >= _keyCount || _binds[s.Index].Row == null || GameContract.RebindStart.Method == null)
            {
                return false;
            }
            if (_listen >= 0)
            {
                return true;
            }
            using (HotSpots.Game())
            {
                GameContract.RebindStart.Method.Invoke(_binds[s.Index].Row, null);
            }
            _listen = s.Index;
            _listenFrame = Time.frameCount;
            _dirty = true;
            return true;
        }

        // True while the game waits for a key, and the frame after it ended: the key that ended it is the game's, never a press of the kit's.
        private bool Busy => _listen >= 0 || Time.frameCount - _endFrame <= 1;

        // Looked at each frame while a key is awaited: the game's own rebinding ends on a key, on Escape or on its time running out.
        private void WatchRebind()
        {
            if (_listen < 0 || Time.frameCount - _listenFrame < 2)
            {
                return;
            }
            bool going = _listen < _keyCount && GameContract.RebindOngoing.Property != null && GameContract.RebindOngoing.Property.GetValue(_binds[_listen].Row, null) != null;
            if (!going)
            {
                _listen = -1;
                _endFrame = Time.frameCount;
                _stamp++;   // the key now bound is read again, and the clash marks with it
                _dirty = true;
            }
        }
    }
}
