using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Localizor;
using Scenes.MainMenu;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The game's Options in the city as the kit's settings list, as Mod settings draws its own: the categories down the
    /// sidebar with their glyphs, the one in view as sections and rows, the game's controls mirrored one for one (a switch, a
    /// slider, a drop-down, a button, a key binding) and every press made on the game's own control, so its handlers, other
    /// mods' patches on them and Going Public's locks run as they always do. What the game holds still (the locks) reads as
    /// greyed rows. Controls has its search, its action groups with a sentence under each name and the clash marks; Game has the
    /// custom difficulty panel (OptionsScreen.Difficulty); Mods shows the mod's own groups as Mod settings does and the
    /// other mods' options; another mod's tab opens the game's own Options view until it closes. A fixed Reset this category
    /// and Reset all stand at the head, each asking through the game's own question box. Cost: a look at the game's controls
    /// four times a second while it shows (a hash of their values), a page built when the category, the search or the
    /// controls' shape changes, nothing otherwise.
    /// </summary>
    internal sealed partial class OptionsScreen : IUiScreen, IUiTyping, IUiStepping, IUiDragging, IUiPaging
    {
        public const string ScreenId = OptionsKits.CityId;

        /// <summary>The custom difficulty's name field (this job's command numbers are 9500 to 9519).</summary>
        public const int NameCommand = UiOptions.NameCommand;

        private const float Every = 0.25f;           // seconds between looks at the game's controls
        private const int ModePage = 0, ModeFound = 1;

        private readonly Scenes.MainMenu.Options _o;
        private readonly OptionsGame _game;
        private readonly UiOptionsModel _m = new UiOptionsModel();
        private readonly List<Slot> _slots = new List<Slot>(64);
        private readonly List<Transform> _tabs = new List<Transform>(4);
        private readonly List<int> _past = new List<int>(16);
        private readonly CultureInfo _inv = CultureInfo.InvariantCulture;

        private string[] _names = new string[0];
        private UiIcon[] _icons = new UiIcon[0];
        private int[] _found = new int[0];
        private int _used;
        private int _chosen;
        private int _at = -1;
        private int _mode;
        private string _query = string.Empty;
        private int _choice = -1;
        private int _held = -1;
        private bool _dirty = true;
        private int _stamp, _builtStamp = -1, _builtChosen = -2, _builtMode = -1;
        private bool _builtDiff;
        private float _next;
        private long _seen, _shape;
        private float _controlX;
        private string _locale, _build, _modsLine;
        private bool _wasDiff;

        public OptionsScreen(Scenes.MainMenu.Options options, OptionsGame game)
        {
            _o = options;
            _game = game;
        }

        public string Id => ScreenId;

        public bool Dirty
        {
            get
            {
                if (_modsOpen)
                {
                    return _settings.Dirty || _dirty;
                }
                if (_listen >= 0)
                {
                    WatchRebind();   // a frame at a time while a key is awaited: Escape and the key itself end it in the game's own code
                }
                if (Time.unscaledTime >= _next)
                {
                    _next = Time.unscaledTime + Every;
                    Look();
                }
                return _dirty;
            }
        }

        // ------------------------------------------------------------------------------------------------- the game's controls read

        // The controls as the game holds them now: a shape (what is there) and a hash of their values. A change of shape builds the
        // page again; a change of value lays it out again.
        private void Look()
        {
            bool diff = Customizing;
            if (diff != _wasDiff)
            {
                _wasDiff = diff;
                _chosen = 0;
                _mode = ModePage;
                _query = string.Empty;
                _choice = -1;
                _held = -1;
                _stamp++;
                _dirty = true;
            }
            if (diff && !UiScreenSwitches.On(OptionsKits.DifficultyId))
            {
                OptionsKits.Classic();   // its switch is off: the game's own custom difficulty, until Options closes
                return;
            }
            FindTabs();
            string locale = LocalizorManager.LoadedLocale;
            long shape = _tabs.Count * 31L + (diff ? 7 : 0) + (UiScreenSwitches.On(OptionsKits.DifficultyId) ? 3 : 0);
            shape = shape * 31 + Shape();
            if (locale != _locale || shape != _shape)
            {
                _locale = locale;
                _shape = shape;
                _stamp++;
                _dirty = true;
            }
            long now = Values();
            if (now != _seen)
            {
                _seen = now;
                _dirty = true;
            }
        }

        private bool Customizing
        {
            get
            {
                GameObject ui = BuyKits.Get<GameObject>(GameContract.OptCustomUi, _o);
                return ui != null && ui.activeSelf;
            }
        }

        // Another mod's tab beside the game's own: found by its name under the game's Menu, hidden under the kit with the rest.
        private void FindTabs()
        {
            _tabs.Clear();
            Transform menu = _o.transform.Find("Menu");
            for (int i = 0; menu != null && i < menu.childCount; i++)
            {
                Transform c = menu.GetChild(i);
                if (c.gameObject.activeSelf && !UiHostRules.Own(c.name, OptionsRules.GameTabs) && c.GetComponent<Button>() != null)
                {
                    _tabs.Add(c);
                }
            }
            Transform root = _o.transform;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform c = root.GetChild(i);
                if (c.GetComponent<CanvasGroup>() == null)
                {
                    _game.Hide(c);   // a piece another mod added straight under the window since the bind
                }
            }
        }

        private static long Mix(long hash, long value) => unchecked(hash * 1099511628211L + value);

        private long Shape()
        {
            long h = _used;
            for (int i = 0; i < _used; i++)
            {
                Slot s = _slots[i];
                if (s.Kind == Kind.Choice && s.D != null)
                {
                    List<string> words = BuyKits.Get<List<string>>(GameContract.BizOptions, s.D);
                    h = Mix(h, words != null ? words.Count : -1);
                }
                else if (s.Kind == Kind.Action && s.B != null)
                {
                    h = Mix(h, s.B.gameObject.activeSelf ? 1 : 0);
                }
            }
            return h;
        }

        private long Values()
        {
            long h = 17;
            for (int i = 0; i < _used; i++)
            {
                Slot s = _slots[i];
                switch (s.Kind)
                {
                    case Kind.Toggle:
                        h = Mix(h, s.T != null ? (s.T.isOn ? 2 : 1) + (s.T.interactable ? 4 : 0) : 0);
                        break;
                    case Kind.Slider:
                        h = Mix(h, s.S != null ? BitConverter.SingleToInt32Bits(s.S.value) + (s.S.interactable ? 1 : 0) : 0);
                        h = Mix(h, s.Fig != null ? s.Fig.text.GetHashCode() : 0);
                        break;
                    case Kind.Choice:
                        h = Mix(h, s.D != null ? s.D.SelectedOptionIndex * 3 + (Held(s.D) ? 1 : 0) : 0);
                        break;
                    case Kind.Action:
                        h = Mix(h, s.B != null ? (s.B.interactable && Free(s.B) ? 1 : 0) : 0);
                        break;
                    case Kind.Key:
                        h = Mix(h, s.Text != null && s.Text.text != null ? s.Text.text.GetHashCode() : 0);
                        break;
                }
            }
            if (Customizing)
            {
                h = Mix(h, DifficultySign());
            }
            return h;
        }

        // A button whose own group holds it (Going Public greys the custom difficulty button that way).
        private static bool Free(Button b)
        {
            CanvasGroup g = b.GetComponent<CanvasGroup>();
            return g == null || g.interactable;
        }

        private static bool Held(UI.Elements.Dropdown d)
        {
            CanvasGroup g = BuyKits.Get<CanvasGroup>(GameContract.DropdownGroup, d);
            return g != null && !g.interactable;
        }

        // ------------------------------------------------------------------------------------------------- the window

        public void Lay(UiScene s, UiFrame frame, bool dark, IUiKeys keys)
        {
            if (_modsOpen)
            {
                _settings.Lay(s, frame, dark, keys);
                _dirty = false;
                return;
            }
            Prepare();
            _m.Title = _builtDiff ? T("reallife_ui_opt_diff_title") : T("reallife_ui_opt_title");
            _m.Icon = _builtDiff ? UiIcon.Sliders : UiIcon.Gear;
            _m.Build = _builtDiff ? null : BuildLine();
            UiOptions.Lay(s, frame, _m, dark, keys, Words);
            _controlX = UiOptions.ControlLeft(frame);
            _dirty = false;
        }

        // The window's page: built again when the category, the search or the shape of the game's controls changed, its values read always.
        private void Prepare()
        {
            bool view = _builtChosen != _chosen || _builtMode != _mode || _builtDiff != _wasDiff;
            if (view || _builtStamp != _stamp || _builtQuery != _query)
            {
                Rebuild();
                if (view || _builtQuery != _query)
                {
                    _m.List.Offset = 0f;
                }
                _builtStamp = _stamp;
                _builtChosen = _chosen;
                _builtMode = _mode;
                _builtDiff = _wasDiff;
                _builtQuery = _query;
            }
            for (int i = 0; i < _used; i++)
            {
                Write(_slots[i], false);
            }
            if (_m.List.Rows == null || _m.List.Rows.Length < _used)
            {
                _m.List.Rows = new UiSettingRow[Math.Max(16, _used)];
            }
            for (int i = 0; i < _used; i++)
            {
                _m.List.Rows[i] = _slots[i].Row;
            }
            _m.List.RowCount = _used;
            Chrome();
        }

        private string _builtQuery;

        private void Rebuild()
        {
            _used = 0;
            _choice = -1;
            _held = -1;
            _m.Choices = null;
            Groups();
            if (_wasDiff)
            {
                FillDifficulty(_chosen);
            }
            else if (_mode == ModeFound)
            {
                FillFound();
            }
            else
            {
                FillCategory(_chosen);
            }
        }

        private void Groups()
        {
            if (_wasDiff)
            {
                _names = new[] { T("reallife_ui_opt_diff_presets"), T("reallife_ui_opt_diff_general"), T("reallife_ui_opt_diff_character"), T("reallife_ui_opt_diff_economy") };
                _icons = new[] { UiIcon.Bookmark, UiIcon.Gear, UiIcon.Person, UiIcon.Coin };
            }
            else
            {
                int n = OptionsRules.Categories + _tabs.Count;
                _names = new string[n];
                _icons = new UiIcon[n];
                for (int i = 0; i < OptionsRules.Categories; i++)
                {
                    _names[i] = T(OptionsRules.CategoryKeys[i]);
                    _icons[i] = OptionsRules.CategoryIcons[i];
                }
                for (int i = 0; i < _tabs.Count; i++)
                {
                    string words = UiWidgets.Label(_tabs[i]);
                    _names[OptionsRules.Categories + i] = string.IsNullOrEmpty(words) ? _tabs[i].name : words;
                    _icons[OptionsRules.Categories + i] = UiIcon.Code;
                }
            }
            if (_chosen >= _names.Length)
            {
                _chosen = 0;
            }
            if (_found.Length != _names.Length)
            {
                _found = new int[_names.Length];
            }
        }

        private void Chrome()
        {
            _m.Groups = _names;
            _m.Icons = _icons;
            _m.Chosen = _mode == ModeFound ? -1 : _chosen;
            _m.Found = _mode == ModeFound ? _found : null;
            UiSettingsModel l = _m.List;
            l.Query = _query;
            l.NoHead = _wasDiff;
            l.SearchPlaceholder = T("reallife_ui_opt_search");
            l.CanBack = _at > 0;
            l.CanForward = _at >= 0 && _at < _past.Count - 1;
            if (_mode != ModeFound)
            {
                l.Found = null;
            }
            if (_wasDiff)
            {
                DifficultyChrome();
            }
            else
            {
                l.Preset = T("reallife_ui_opt_apply_note");
                l.Differs = null;
                bool resets = _mode == ModePage;
                l.PresetActions = resets
                    ? new[]
                    {
                        new UiActionModel { Label = T("reallife_ui_opt_reset_category"), Kind = UiButtonKind.Secondary, Icon = UiIcon.Reset, Key = UiAction.Option },
                        new UiActionModel { Label = T("reallife_ui_opt_reset_all"), Kind = UiButtonKind.Secondary, Icon = UiIcon.Trash, Key = UiAction.Count }
                    }
                    : new UiActionModel[0];
            }
            _m.ChoiceRow = _choice;
            _m.ChoiceOffset = _choiceOffset;
            if (_choice >= 0 && _choice < _used && _slots[_choice].Choices != null)
            {
                _m.Choices = _slots[_choice].Choices;
                _m.ChoiceCount = _slots[_choice].Choices.Length;
                _m.ChoiceValue = _slots[_choice].Chosen;
            }
            else
            {
                _m.Choices = null;
            }
        }

        private string BuildLine()
        {
            if (_build == null || _locale != _builtLocale)
            {
                _builtLocale = _locale;
                int mods = ModsCount();
                string count = mods == 1 ? T("reallife_ui_opt_mods_one") : T("reallife_ui_opt_mods").Replace("{count}", mods.ToString(_inv));
                _modsLine = mods > 0 ? count : null;
                _build = OptionsRules.BuildLine(GameBuild.Running, T("reallife_ui_opt_build"), _modsLine) ?? string.Empty;
            }
            return _build.Length > 0 ? _build : null;
        }

        private string _builtLocale;

        // The mods the game has loaded (its own list, the mod itself among them), read once for the head of the window.
        private static int ModsCount()
        {
            try
            {
                System.Reflection.MethodInfo m = GameContract.ModsActive.Method;
                return m != null && m.Invoke(null, null) is ICollection list ? list.Count : 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        private static string T(string key) => UiText.T(key);
    }
}
