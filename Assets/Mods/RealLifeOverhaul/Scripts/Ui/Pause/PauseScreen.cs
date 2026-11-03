using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using BigAmbitions.DayNightCycle;
using BigAmbitions.InputSystem;
using Entities;
using Helpers;
using TMPro;
using UI.MiniMenu;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The pause menu as the kit's pause screen: the day and time, the save's name typed into the game's own field, when
    /// the game last saved, and the game's rows in the game's order, each pressing the game's own button, so its checks, its
    /// questions and Going Public's patches on them run as they always do. A piece another mod put on the panel is a row
    /// too, pressing its own button. Esc and the way back at the foot press the game's Close. Beside it, read as the menu
    /// opens: the city's card (money, net worth, businesses, employees, the day, the time played) and this city's latest
    /// saves, each loaded or saved over through the game's own calls and questions (<see cref="PauseSaves"/>).
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
        private static readonly string[] DayKeys =
        {
            "common_monday", "common_tuesday", "common_wednesday", "common_thursday", "common_friday", "common_saturday", "common_sunday"
        };
        private readonly List<SaveGameManager.SaveGameStruct> _saves = new List<SaveGameManager.SaveGameStruct>(UiPauseMenu.SavesMost);
        private readonly List<UiPauseFact> _facts = new List<UiPauseFact>(6);
        private bool _open;   // the game's menu stood open at the last look: the city and the saves are read as it opens

        public PauseScreen(MiniMenu menu, Transform panel)
        {
            _menu = menu;
            _panel = panel;
            _name = BuyKits.Get<TMP_InputField>(GameContract.PauseName, menu);
            _m.Title = UiText.T("reallife_ui_pause_title");
            _m.NameLabel = UiText.T("reallife_ui_pause_name");
            _m.NamePlaceholder = UiText.T("common_name");
            _m.Back = UiText.T("reallife_ui_pause_back");
            _m.CityTitle = UiText.T("reallife_ui_pause_city");
            _m.SavesEmpty = UiText.T("reallife_ui_pause_saves_none");
            _m.Load = UiText.T("reallife_ui_pause_load");
            _m.Over = UiText.T("reallife_ui_pause_over");
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
                case UiPauseMenu.LoadCommand:
                    if (arg < 0 || arg >= _saves.Count || !PauseSaves.CanLoad)
                    {
                        return false;
                    }
                    PauseSaves.AskLoad(_saves[arg]);
                    return true;
                case UiPauseMenu.OverCommand:
                    return arg >= 0 && arg < _saves.Count && Again(Over(_saves[arg]));
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

        // Saving over a save: its name typed into the game's field and the game's own Save pressed, so the game asks first
        // ("it exists, save over it?") and its checks and Going Public's patches run. The game's autosaves are never named over.
        private bool Over(SaveGameManager.SaveGameStruct s)
        {
            if (s == null || s.isRecoverSave || _name == null || !_name.interactable || _buttons.Count == 0 || _rows.Count == 0 || !_rows[0].Primary)
            {
                return false;
            }
            UiWidgets.Type(_name, s.name, false);
            return UiWidgets.Press(_buttons[0]);
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
                string words = Own(name) ?? BuyKits.NoKey(UiWidgets.Label(b));
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
            bool open = _panel != null && _panel.gameObject.activeInHierarchy;
            if (open && !_open)
            {
                City();
                Saves();
            }
            _open = open;
            for (int i = 0; i < _m.Saves.Length; i++)
            {
                _m.Saves[i].NoLoad = !PauseSaves.CanLoad;
                _m.Saves[i].Current = _m.Name != null && string.Equals(_m.Saves[i].Name, _m.Name.Trim(), StringComparison.Ordinal);
                _sb.Append(_m.Saves[i].Current ? '*' : '-');
            }
            _sb.Append(_m.Facts.Length).Append(_m.Saves.Length);
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
                    row.Primary = true;   // the menu's main action, on the cyan with its F
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
                    row.Tint = UiRole.CatTech;   // the violet of the mockup's house
                    break;
                case "SaveAndExitToDesktopButton":
                    row.Icon = UiIcon.SignOut;
                    row.Tint = UiRole.WarnText;   // leaving, though with a save: amber, short of quitting's red
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

        // The menu's own words where the game's mean the same (the mockup's "Save game", "Main menu", "Quit to desktop");
        // null keeps the game's own label (Options, Blueprints, Save and exit, another mod's row).
        private static string Own(string name)
        {
            switch (name)
            {
                case SaveRow: return UiText.T("reallife_ui_pause_save_game");
                case "MainMenuButton": return UiText.T("reallife_ui_pause_main_menu");
                case "ExitToDesktopButton": return UiText.T("reallife_ui_pause_quit");
                default: return null;
            }
        }

        // The city at a glance, read as the menu opens (the clock stands still behind it): the game's money and day, the
        // books' net worth and businesses, the game's own count of employees, and the time played (Ui/Pause/PlayTime).
        private void City()
        {
            GameInstance save = SaveGameManager.Current;
            _facts.Clear();
            if (save == null)
            {
                _m.Facts = new UiPauseFact[0];
                return;
            }
            _facts.Add(new UiPauseFact { Icon = UiIcon.Wallet, Label = UiText.T("reallife_ui_pause_money"), Value = UiText.Money(save.Money) });
            if (PauseKits.NetWorth != null)
            {
                _facts.Add(new UiPauseFact { Icon = UiIcon.ChartLineUp, Label = UiText.T("reallife_ui_pause_worth"), Value = UiText.Money(PauseKits.NetWorth()) });
            }
            if (PauseKits.Businesses != null)
            {
                _facts.Add(new UiPauseFact { Icon = UiIcon.Store, Label = UiText.T("reallife_ui_pause_businesses"), Value = Count(PauseKits.Businesses()) });
            }
            _facts.Add(new UiPauseFact { Icon = UiIcon.People, Label = UiText.T("reallife_ui_pause_employees"), Value = Count(Staff()) });
            _facts.Add(new UiPauseFact { Icon = UiIcon.Calendar, Label = UiText.T("reallife_ui_pause_day"), Value = Count(save.Day) });
            long played = PlayTime.Seconds;
            if (played >= 0L)
            {
                _facts.Add(new UiPauseFact { Icon = UiIcon.Clock, Label = UiText.T("reallife_ui_pause_played"), Value = UiPauseMenu.Played(Words, played) });
            }
            _m.Facts = _facts.ToArray();
            int day = Math.Max(0, Math.Min(DayKeys.Length - 1, TimeHelper.GetDayOfWeekIndex(TimeHelper.GetDayOfWeek())));
            _m.CityNote = UiText.T(DayKeys[day]);
        }

        // The player's employees as the game's Employees app counts them (those being replaced left out).
        private static readonly List<EmployeeInstance> Staffed = new List<EmployeeInstance>(32);

        private static int Staff()
        {
            int n = EmployeeHelper.GetEmployeeInstances(new EmployeeInstancesQueryInfo { excludeBeingReplaced = true }, Staffed).Count;
            Staffed.Clear();
            return n;
        }

        private static string Count(int n) => n.ToString("N0", CultureInfo.CurrentCulture);

        // This city's latest saves, read once as the menu opens (Ui/Pause/PauseSaves): the name, its day and its age.
        private void Saves()
        {
            int total = PauseSaves.Read(_saves, UiPauseMenu.SavesMost);
            if (total < 0)
            {
                _m.SavesTitle = null;   // the list could not be read: the card stands down
                _m.Saves = new UiPauseSave[0];
                return;
            }
            _m.SavesTitle = UiText.T("reallife_ui_pause_saves");
            var saves = new UiPauseSave[_saves.Count];
            DateTime now = DateTime.Now;
            for (int i = 0; i < _saves.Count; i++)
            {
                SaveGameManager.SaveGameStruct sv = _saves[i];
                long minutes = sv.lastPlayedDate > DateTime.MinValue && sv.lastPlayedDate <= now ? (long)(now - sv.lastPlayedDate).TotalMinutes : -1L;
                saves[i] = new UiPauseSave { Name = sv.name, Line = UiPauseMenu.SaveLine(Words, sv.day, minutes), Auto = sv.isRecoverSave };
            }
            _m.Saves = saves;
            _m.SavesCount = total > _saves.Count
                ? UiText.Fill("reallife_ui_pause_saves_count", "shown", _saves.Count.ToString(CultureInfo.CurrentCulture), "all", total.ToString(CultureInfo.CurrentCulture))
                : null;
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
