using System.Collections.Generic;
using System.Globalization;
using System.Text;
using BigAmbitions.DayNightCycle;
using BigAmbitions.InputSystem;
using Helpers;
using TMPro;
using UI.MiniMenu;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The pause menu as the kit's narrow panel: the day and time, the save's name typed into the game's own field, when
    /// the game last saved, and the game's rows in the game's order with the game's words, each pressing the game's own
    /// button, so its checks, its questions and Going Public's patches on them run as they always do. A piece another mod
    /// put on the panel is a row too, pressing its own button. Esc and the way back at the foot press the game's Close.
    /// </summary>
    internal sealed class PauseScreen : IUiScreen, IUiTyping
    {
        internal const string SaveButton = "Savegame/SavegameButton";
        private const string CloseButton = "Close", Splitter = "Splitter", Blocker = "RaycastBlocker", SaveRow = "Savegame";
        private const float Every = 0.25f;   // seconds between looks at the game's panel

        private readonly MiniMenu _menu;
        private readonly Transform _panel;
        private readonly TMP_InputField _name;
        private readonly UiPauseModel _m = new UiPauseModel();
        private readonly List<UiMenuRowModel> _rows = new List<UiMenuRowModel>(10);
        private readonly List<Button> _buttons = new List<Button>(10);   // in step with the rows
        private readonly StringBuilder _sb = new StringBuilder(256);
        private Button _close;
        private string _seen;
        private float _next;
        private bool _dirty = true;
        private string _savedLine, _when;   // the two lines of words, made again only when a minute passes
        private int _savedMinutes = -2, _autosave = -1, _whenMinute = -1;
        private static readonly UiWords Words = (key, english) => UiText.T(key);

        public PauseScreen(MiniMenu menu, Transform panel)
        {
            _menu = menu;
            _panel = panel;
            _name = BuyKits.Get<TMP_InputField>(GameContract.PauseName, menu);
            _m.Title = UiText.T("reallife_ui_pause_title");
            _m.NameLabel = UiText.T("reallife_ui_pause_name");
            _m.NamePlaceholder = UiText.T("common_name");
            _m.Back = UiText.T("reallife_ui_pause_back");
        }

        public string Id => PauseKits.PauseId;

        public bool Dirty
        {
            get
            {
                if (Time.unscaledTime >= _next)
                {
                    _next = Time.unscaledTime + Every;
                    string now = Read();
                    _dirty |= now != _seen;
                    _seen = now;
                }
                return _dirty;
            }
        }

        public void Lay(UiScene s, UiFrame frame, bool dark, IUiKeys keys)
        {
            if (_seen == null)
            {
                _seen = Read();
            }
            UiPauseMenu.Lay(s, _m, frame, dark, keys);
            _dirty = false;
        }

        public bool Press(int command, int arg)
        {
            switch (command)
            {
                case UiPauseMenu.RowCommand:
                    if (arg < 0 || arg >= _buttons.Count)
                    {
                        return false;
                    }
                    // A row pressed by its key (F, P): the game's world never hears the same press.
                    PlayerAction.Interact.Reset();
                    PlayerAction.SecondaryInteract.Reset();
                    return Again(UiWidgets.Press(_buttons[arg]));
                case UiPauseMenu.BackCommand:
                    return Again(Close());
            }
            return false;
        }

        public void Focused(int command, int arg)
        {
        }

        public bool Back() => false;   // the game's own Esc closes its menu (and steps a question back first)

        public void Tab(int step)
        {
        }

        public bool Act(UiAction action) => false;

        public void Reveal(int list, int index)
        {
        }

        public void Wheel(int clip, float notches)
        {
        }

        // ------------------------------------------------------------------------------------------------- the save's name

        public int TypingCommand => UiPauseMenu.NameCommand;

        public string TypingText => _name != null ? _name.text : string.Empty;

        public void Typed(string text)
        {
            if (_name == null)
            {
                return;
            }
            text = text ?? string.Empty;
            if (_name.characterLimit > 0 && text.Length > _name.characterLimit)
            {
                text = text.Substring(0, _name.characterLimit);
            }
            Again(UiWidgets.Type(_name, text, false));   // into the game's own field: its save, and Going Public's, read it there
        }

        // ------------------------------------------------------------------------------------------------- the game's panel read

        private bool Again(bool done)
        {
            if (done)
            {
                _next = 0f;
                _dirty = true;
            }
            return done;
        }

        // The game's Close, as its own button; the game's own close when another version has none.
        private bool Close()
        {
            if (UiWidgets.Press(_close))
            {
                return true;
            }
            if (_menu == null || !MiniMenu.IsOpen)
            {
                return false;
            }
            _menu.Toggle(false);
            return true;
        }

        // The panel as the game shows it: its rows in its order, the field, the clock and the last save. Returns what the
        // menu shows, to see a change by.
        private string Read()
        {
            _sb.Length = 0;
            _rows.Clear();
            _buttons.Clear();
            _close = null;
            bool split = false;
            for (int i = 0; _panel != null && i < _panel.childCount; i++)
            {
                Transform c = _panel.GetChild(i);
                string name = c.name;
                if (name == Splitter)
                {
                    split = _rows.Count > 0;
                    continue;
                }
                if (name == Blocker || UiHostRules.Own(name, null) || !c.gameObject.activeSelf)
                {
                    continue;
                }
                if (name == CloseButton)
                {
                    _close = c.GetComponent<Button>();
                    continue;
                }
                Button b = name == SaveRow ? Find(c, "SavegameButton") : c.GetComponent<Button>() ?? c.GetComponentInChildren<Button>(false);
                if (b == null || !b.gameObject.activeSelf)
                {
                    continue;
                }
                string words = BuyKits.NoKey(UiWidgets.Label(b));
                var row = new UiMenuRowModel { Label = words ?? Fallback(name), Key = UiAction.Count, Split = split, Disabled = !b.interactable };
                Look(name, ref row);
                split = false;
                _rows.Add(row);
                _buttons.Add(b);
                _sb.Append(row.Label).Append(row.Disabled ? '-' : '+').Append(row.Split ? '/' : '|');
            }
            if (_m.Rows.Length != _rows.Count)
            {
                _m.Rows = new UiMenuRowModel[_rows.Count];
            }
            _rows.CopyTo(_m.Rows);
            _m.Name = _name != null ? _name.text : null;
            _m.NameDisabled = _name == null || !_name.interactable;
            _m.When = When();
            _m.Saved = Saved();
            _sb.Append(_m.Name).Append('|').Append(_m.NameDisabled).Append('|').Append(_m.When).Append('|').Append(_m.Saved).Append('|').Append(_close != null);
            return _sb.ToString();
        }

        private static Button Find(Transform root, string path)
        {
            Transform t = root.Find(path);
            return t != null ? t.GetComponent<Button>() : null;
        }

        // The glyph and the key of each of the game's rows; another mod's row keeps the plain glyph and no key.
        private static void Look(string name, ref UiMenuRowModel row)
        {
            switch (name)
            {
                case SaveRow:
                    row.Icon = UiIcon.FloppyDisk;
                    row.Key = UiAction.Alt;
                    break;
                case "WishlistButton":
                    row.Icon = UiIcon.Star;
                    break;
                case "OptionsButton":
                    row.Icon = UiIcon.Gear;
                    row.Key = UiAction.Option;
                    break;
                case "BlueprintsButton":
                    row.Icon = UiIcon.Layout;
                    break;
                case "MainMenuButton":
                    row.Icon = UiIcon.Home;
                    row.Tint = UiRole.Chart7;
                    break;
                case "SaveAndExitToDesktopButton":
                    row.Icon = UiIcon.SignOut;
                    break;
                case "ExitToDesktopButton":
                    row.Icon = UiIcon.Power;
                    row.Danger = true;   // leaves without saving: the game asks first, the kit says it in red
                    break;
                default:
                    row.Icon = UiIcon.Cube;
                    break;
            }
        }

        // The game's words for a row whose button shows none yet (its text set a frame late).
        private static string Fallback(string name)
        {
            switch (name)
            {
                case SaveRow: return UiText.T("menu_save");
                case "OptionsButton": return UiText.T("menu_options");
                case "BlueprintsButton": return UiText.T("common_blueprints");
                case "MainMenuButton": return UiText.T("menu_main_menu");
                case "SaveAndExitToDesktopButton": return UiText.T("menu_save_and_exit_to_desktop");
                case "ExitToDesktopButton": return UiText.T("menu_exit_to_desktop");
                case "WishlistButton": return UiText.T("common_wishlist_now");
                default: return name;
            }
        }

        // "Day 12, 08:45": the day and the time where the clock stopped, made again only when a minute passes.
        private string When()
        {
            if (SaveGameManager.Current == null)
            {
                return null;
            }
            Timestamp now = TimeHelper.Now();
            int minute = (now.Day * 24 + now.Hour) * 60 + (int)now.Minute;
            if (_when == null || minute != _whenMinute)
            {
                _whenMinute = minute;
                string time = now.Hour.ToString("00", CultureInfo.InvariantCulture) + ":" + ((int)now.Minute).ToString("00", CultureInfo.InvariantCulture);
                _when = UiText.Fill("reallife_ui_pause_when", "day", now.Day.ToString(CultureInfo.InvariantCulture), "time", time);
            }
            return _when;
        }

        // "Last saved 14 minutes ago, autosave every 10 minutes", made again only when a whole minute changes.
        private string Saved()
        {
            float since = PauseKits.SinceSave;
            int saved = since < 0f ? -1 : (int)(since / 60f), every = PlayerPrefSettings.MinutesBetweenAutoSaves;
            if (_savedLine == null || saved != _savedMinutes || every != _autosave)
            {
                _savedMinutes = saved;
                _autosave = every;
                _savedLine = UiPauseMenu.SavedLine(Words, saved, every);
            }
            return _savedLine;
        }
    }
}
