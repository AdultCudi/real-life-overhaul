using System;
using System.Collections.Generic;
using System.Globalization;
using RealLifeOverhaul.Meta;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Mod settings on the kit: the options tree's groups down the sidebar, the group on screen as bands, and each
    /// option saved under the same key the Mods tab uses. A group is built when it is shown. A row's words are written
    /// again only when its value changes. Nothing runs per frame: the host lays this out when it is dirty.
    /// </summary>
    public sealed partial class SettingsScreen : IUiScreen, IUiTyping, IUiStepping, IUiDragging, IUiPaging
    {
        public const string ScreenId = "mod_settings";

        private const int ModePage = 0, Found = 1, Differ = 2;

        private readonly UiSettingsModel _model = new UiSettingsModel();
        private readonly List<Slot> _slots = new List<Slot>(48);
        private readonly List<FindLine> _index = new List<FindLine>(420);
        private readonly List<string> _words = new List<string>(4);
        private readonly List<int> _hits = new List<int>(64);
        private readonly List<int> _past = new List<int>(16);
        private readonly CultureInfo _inv = CultureInfo.InvariantCulture;

        private string[] _groupNames = new string[0];
        private UiIcon[] _groupIcons = new UiIcon[0];
        private int[] _groupCounts = new int[0];
        private int[] _groupFound = new int[0];
        private bool _tests;
        private bool _indexed;
        private bool _dirty = true;
        private int _group;
        private int _mode;
        private int _at = -1;
        private int _choice = -1;
        private int _held = -1;
        private int _listen = -1;
        private int _used;
        private string _query = string.Empty;
        private Intensity _nearest = Intensity.Standard;
        private int _differ;
        private bool _exact;
        private string _presetName = string.Empty;
        private float _sliderX;
        private bool _wired;

        private static readonly string[] PresetKeys =
        {
            "reallife_meta_preset_button_relaxed", "reallife_meta_preset_button_standard",
            "reallife_meta_preset_button_realistic", "reallife_meta_preset_button_hardcore"
        };

        private Action _onChanged;
        private Action<RealLifeOverhaul.KeyAction, bool> _onKey;

        public static IUiScreen Make()
        {
            if (_one == null)
            {
                _one = new SettingsScreen();
            }
            return _one;
        }

        public static void Drop()
        {
            if (_one != null)
            {
                _one.Unwire();
            }
            _one = null;
        }

        private static SettingsScreen _one;

        /// <summary>The group on screen, so a report can name it. Null when the app is not the one open.</summary>
        public static string Place { get; private set; }

        public string Id => ScreenId;

        public bool Dirty => _dirty;

        /// <summary>The groups of the mod's settings, for the game's Options in the city to list them and open one (Ui/Options).</summary>
        internal int GroupCount
        {
            get
            {
                EnsureGroups();
                return _groupNames.Length;
            }
        }

        internal string GroupName(int group) => group >= 0 && group < _groupNames.Length ? _groupNames[group] : string.Empty;

        internal UiIcon GroupIcon(int group) => group >= 0 && group < _groupIcons.Length ? _groupIcons[group] : UiIcon.Gear;

        internal int GroupOptions(int group) => group >= 0 && group < _groupCounts.Length ? _groupCounts[group] : 0;

        /// <summary>Shows the list at one group, as a press on it in the sidebar does.</summary>
        internal void ShowGroup(int group)
        {
            EnsureGroups();
            OpenGroup(group, true);
        }

        public int TypingCommand => UiSettingsList.SearchCommand;

        public string TypingText => _query ?? string.Empty;

        public void Typed(string text)
        {
            _query = text ?? string.Empty;
            _choice = -1;
            _held = -1;
            _mode = _query.Trim().Length >= OptionsFind.ShortestQuery ? Found : ModePage;
            _dirty = true;
        }

        public bool Steps(int command) => command == UiSettingsList.RowCommand && _held >= 0;

        public void Step(int command, int arg, int step)
        {
            if (arg < 0 || arg >= _used || _slots[arg].Kind != SlotKind.Slider)
            {
                return;
            }
            Nudge(_slots[arg], step);
        }

        public void PointerDown(int command, int arg, float x, float y)
        {
            if (command == UiSettingsList.RowCommand && arg >= 0 && arg < _used && _slots[arg].Kind == SlotKind.Slider)
            {
                DragTo(_slots[arg], x);
            }
        }

        public bool DragBegin(int command, int arg, float x, float y)
        {
            if (command != UiSettingsList.RowCommand || arg < 0 || arg >= _used || _slots[arg].Kind != SlotKind.Slider)
            {
                return false;
            }
            _held = arg;
            DragTo(_slots[arg], x);
            return true;
        }

        public void DragMove(float x, float y)
        {
            if (_held >= 0 && _held < _used)
            {
                DragTo(_slots[_held], x);
            }
        }

        public void DragEnd(bool cancelled)
        {
            _held = -1;
            _presetGen++;
            _dirty = true;
        }

        public void Page(int step)
        {
            _model.Offset -= step * UiTokens.S16 * 8f;
            _dirty = true;
        }

        public void Lay(UiScene s, UiFrame frame, bool dark, IUiKeys keys)
        {
            Wire();
            s.Begin("mod settings", frame.Width, frame.Height, dark);
            frame.Icon = UiIcon.Gear;
            string title = T("reallife_set_app");
            UiAt window = UiAppWindow.Shell(s, frame, title, UiIcon.Gear, new UiNavEntry[0], 0, false, keys, out float _, out float _, out float _, out float _);
            if (!OptionsLayout.AppReady)
            {
                frame.Window(out float wx, out float wy, out float ww, out float wh);
                UiDraw.EmptyState(s, window, wx, wy + UiFrame.Strip, ww, wh - UiFrame.Strip, UiIcon.Gear, T("reallife_set_app"), T("reallife_set_not_here"));
            }
            else
            {
                EnsureGroups();
                EnsureIndex();
                Fill();
                RememberSlider(frame);
                int chosen = _mode == ModePage ? _group : -1;
                int[] counts = _mode == Found ? _groupFound : _groupCounts;
                UiSettingsList.LayWithGroups(s, window, _model, frame, _groupNames, _groupIcons, chosen, keys, counts);
                if (_choice >= 0 && _choice < _used && _slots[_choice].Choices != null)
                {
                    float cx, cy;
                    ChoiceAt(frame, _choice, out cx, out cy);
                    UiDraw.ChoiceList(s, UiAt.Screen, cx, cy, UiSettingsList.ControlWidth, _slots[_choice].Choices, _slots[_choice].Choices.Length,
                        _slots[_choice].Value, 0f, UiSettingsList.ChoiceCommand);
                }
                Place = _mode == ModePage && _group >= 0 && _group < _groupNames.Length ? _groupNames[_group] : title;
            }
            s.AddPrompt(UiAction.Move, T("reallife_ui_prompt_move"));
            s.AddPrompt(UiAction.Confirm, T("reallife_ui_prompt_select"));
            s.AddPrompt(UiAction.Back, T("reallife_ui_prompt_back"));
            UiDraw.PromptBar(s, frame, keys);
            _dirty = false;
        }

        public bool Press(int command, int arg)
        {
            if (!OptionsLayout.AppReady)
            {
                return false;
            }
            if (command == UiSettingsList.ChoiceCommand)
            {
                return PickChoice(arg);
            }
            if (command == UiSettingsList.GroupCommand)
            {
                return OpenGroup(arg, true);
            }
            if (command == UiSettingsList.HistoryCommand)
            {
                return History(arg);
            }
            if (command == UiSettingsList.PresetCommand)
            {
                return PresetPress(arg);
            }
            if (command == UiSettingsList.RowCommand)
            {
                return RowPress(arg);
            }
            return false;
        }

        public void Focused(int command, int arg)
        {
        }

        public bool Back()
        {
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
            if (!string.IsNullOrEmpty(_query))
            {
                _query = string.Empty;
                _mode = ModePage;
                _dirty = true;
                return true;
            }
            if (_mode == Differ)
            {
                _mode = ModePage;
                _dirty = true;
                return true;
            }
            return false;
        }

        public void Tab(int step)
        {
            if (_groupNames.Length == 0)
            {
                return;
            }
            int next = _group + (step < 0 ? -1 : 1);
            if (next < 0)
            {
                next = _groupNames.Length - 1;
            }
            else if (next >= _groupNames.Length)
            {
                next = 0;
            }
            OpenGroup(next, true);
        }

        public bool Act(UiAction action)
        {
            if (action == UiAction.Alt)
            {
                return ApplyNearest();
            }
            if (action == UiAction.Option)
            {
                return ShowDiffer();
            }
            return false;
        }

        public void Reveal(int list, int index)
        {
            _model.Reveal = index;
            _dirty = true;
        }

        public void Wheel(int clip, float notches)
        {
            _model.Offset -= notches * UiSettingsList.OptionHeight;
            _dirty = true;
        }

        private void Wire()
        {
            if (_wired)
            {
                return;
            }
            _wired = true;
            _onChanged = OnChanged;
            _onKey = OnKey;
            Settings.Changed += _onChanged;
            Keys.RowShown += _onKey;
        }

        private void Unwire()
        {
            if (!_wired)
            {
                return;
            }
            Settings.Changed -= _onChanged;
            Keys.RowShown -= _onKey;
            _wired = false;
            Place = null;
        }

        private void OnChanged()
        {
            _indexed = false;
            _exact = false;
            _stamp++;
            _presetGen++;
            _dirty = true;
        }

        private void OnKey(RealLifeOverhaul.KeyAction action, bool waiting)
        {
            _listen = waiting ? (int)action : -1;
            _dirty = true;
        }

        private static string T(string key) => UiText.T(key);

        private static string Fill(string key, string a, string av, string b, string bv) => UiText.Fill(key, a, av, b, bv);
    }
}
