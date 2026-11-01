using System;
using RealLifeOverhaul.GameUi;

namespace RealLifeOverhaul.Ui
{
    /// <summary>What the kit phone's presses do in the game: each goes to the game's own button or the method it calls.</summary>
    public interface IPhoneActions
    {
        /// <summary>The item's own button is pressed (its onClick), so the game's, the mod's and other mods' handlers run.</summary>
        void Open(int item);

        /// <summary>The game's collapse button: the phone folds down to the screen's edge, or up again.</summary>
        void Collapse();

        void RadioSkip();

        /// <summary>The game's volume slider moved by a step (negative quieter).</summary>
        void RadioVolume(int step);

        /// <summary>The game's private driver button.</summary>
        void Driver();

        /// <summary>The phone has the keys and the pad.</summary>
        bool Focused { get; }

        /// <summary>The keys and the pad go back to the game.</summary>
        void Leave();
    }

    /// <summary>
    /// The kit phone's screen: the phone's apps (PhoneItems) as pages of tiles on the Phone template, its home screen in
    /// the player's order with the folders as tiles, a folder's own pages with its name and a way back, the game's radio
    /// and private driver, what comes next, and the pages turned by the nav bar's arrows, Q and E, the pad's shoulders or
    /// the wheel, round from the last to the first. It shows the page and folder it was on when the phone comes back.
    /// Back leaves a folder, then the phone's keys; at home without the keys Back is the game's. Game-free: the game file
    /// (PhoneKit) sets the readings and answers the presses (IPhoneActions). Cost: a layout when the apps or a reading
    /// change, nothing allocated after the first.
    /// </summary>
    public sealed class PhoneScreen : IUiScreen
    {
        public const string ScreenId = "phone";
        private const int MostTiles = 12;   // a page's most: four rows of three

        private readonly UiPhoneModel _model = new UiPhoneModel();
        private readonly UiPhoneApp[] _tiles = new UiPhoneApp[MostTiles];
        private readonly UiWords _words;
        private PhoneItems _items;
        private IPhoneActions _actions;
        private int[] _view = new int[64];
        private int _viewCount;
        private int _seenVersion = -1;
        private int _folder = -1;
        private int _page, _homePage;
        private int _perPage = 9;
        private bool _dirty = true;

        /// <summary>The phone stands on the game's phone: the host's frame is the device itself.</summary>
        public bool Docked;

        public PhoneScreen(PhoneItems items, IPhoneActions actions, UiWords words)
        {
            _items = items;
            _actions = actions;
            _words = words ?? ((key, english) => english);
            for (int i = 0; i < _tiles.Length; i++)
            {
                _tiles[i].Minis = new UiIcon[4];
            }
            _model.Apps = _tiles;
            _model.NextTitle = _words("reallife_phone_next", "Up next");
            _model.NextEmpty = _words("reallife_phone_next_none", "Nothing planned. What you plan in the Planner and the Calendar shows here.");
            _model.Prompts = new[]
            {
                new UiPrompt { Action = UiAction.Move, Label = _words("reallife_ui_prompt_move", "Move") },
                new UiPrompt { Action = UiAction.Confirm, Label = _words("reallife_ui_prompt_select", "Select") },
                new UiPrompt { Action = UiAction.TabPrev, Label = _words("reallife_ui_prompt_page", "Page") },
                new UiPrompt { Action = UiAction.Back, Label = _words("reallife_ui_prompt_back", "Back") },
            };
            _model.PromptCount = _model.Prompts.Length;
        }

        public string Id => ScreenId;

        public bool Dirty => _dirty || (_items != null && _items.Version != _seenVersion);

        /// <summary>The model as last laid out, for the checks and the test that reports the phone.</summary>
        public UiPhoneModel Model => _model;

        /// <summary>The folder whose page shows, -1 for the home screen.</summary>
        public int Folder => _folder;

        public int Page => _page;

        public int Pages => _model.Pages;

        /// <summary>A reading of the game's changed (the time, the radio, the driver, what comes next, the phone folded): laid out again.</summary>
        public void Touch()
        {
            _dirty = true;
        }

        /// <summary>The apps to show; the game file hands over a new list when the phone is bound again.</summary>
        public void Use(PhoneItems items)
        {
            _items = items;
            _seenVersion = -1;
            _dirty = true;
        }

        /// <summary>What the game file reads into the phone's head, radio, driver and what comes next.</summary>
        public UiPhoneModel Readings => _model;

        public void Lay(UiScene s, UiFrame frame, bool dark, IUiKeys keys)
        {
            if (Docked)
            {
                frame.Kind = UiFrameKind.Phone;
            }
            float height = UiPhone.HeightOn(frame, _model);
            _perPage = Math.Min(MostTiles, UiPhone.PerPage(height, _model.Radio));
            View();
            int pages = Math.Max(1, (_viewCount + _perPage - 1) / _perPage);
            _page = Math.Max(0, Math.Min(_page, pages - 1));
            _model.Pages = pages;
            _model.Page = _page;
            _model.Folder = _folder >= 0 ? _words(PhoneLayoutRules.FolderKeys[_folder], FolderEnglish[_folder]) : null;
            int from = _page * _perPage, n = Math.Min(_perPage, _viewCount - from);
            int waiting = 0;
            for (int e = 0; e < _viewCount; e++)
            {
                if (e >= from && e < from + n)
                {
                    Fill(ref _tiles[e - from], _view[e]);
                }
                else
                {
                    waiting += BadgeOf(_view[e]);
                }
            }
            _model.AppCount = Math.Max(0, n);
            _model.OtherPagesBadge = pages > 1 ? waiting : 0;
            UiPhone.Lay(s, _model, frame, dark, keys);
            s.Name = ScreenId;
            _seenVersion = _items != null ? _items.Version : -1;
            _dirty = false;
        }

        private static readonly string[] FolderEnglish = { "Money", "Work and study", "Social and life", "Business", "Settings" };

        // The view's entries: a folder's apps, or the home screen with the folders as tiles. A folder left empty closes.
        private void View()
        {
            _viewCount = 0;
            if (_items == null)
            {
                return;
            }
            if (_view.Length < _items.Count + PhoneLayoutRules.FolderCount)
            {
                _view = new int[(_items.Count + PhoneLayoutRules.FolderCount) * 2];
            }
            if (_folder >= 0)
            {
                _viewCount = _items.InFolder(_folder, _view);
                if (_viewCount > 0)
                {
                    return;
                }
                _folder = -1;
                _page = _homePage;
            }
            _viewCount = _items.Home(_view);
        }

        private void Fill(ref UiPhoneApp tile, int entry)
        {
            UiIcon[] minis = tile.Minis;
            if (entry < 0)
            {
                int folder = -1 - entry;
                _items.Minis(folder, minis);
                tile = new UiPhoneApp
                {
                    Folder = true, Minis = minis, Label = _words(PhoneLayoutRules.FolderKeys[folder], FolderEnglish[folder]), Badge = _items.FolderBadge(folder)
                };
                return;
            }
            ref PhoneItem it = ref _items.Items[entry];
            tile = new UiPhoneApp
            {
                Icon = it.Picture > 0 ? UiIcon.None : it.Icon != UiIcon.None ? it.Icon : PhoneLooks.OtherMods, Tint = it.Tint, Label = it.Title, Badge = it.Badge,
                Picture = it.Picture, Minis = minis
            };
        }

        private int BadgeOf(int entry)
        {
            return entry < 0 ? _items.FolderBadge(-1 - entry) : _items.Items[entry].Badge;
        }

        public bool Press(int command, int arg)
        {
            switch (command)
            {
                case UiPhone.AppCommand:
                {
                    int e = _page * _perPage + arg;
                    if (arg < 0 || e >= _viewCount)
                    {
                        return false;
                    }
                    int entry = _view[e];
                    if (entry < 0)
                    {
                        OpenFolder(-1 - entry);
                        return true;
                    }
                    _actions?.Open(entry);
                    return true;
                }
                case UiPhone.PageCommand:
                    return Turn(arg);
                case UiPhone.ControlCommand:
                    return Control(arg);
                default:
                    return false;
            }
        }

        private bool Control(int arg)
        {
            switch (arg)
            {
                case UiPhone.Collapse:
                    _actions?.Collapse();
                    return true;
                case UiPhone.FolderBack:
                    return CloseFolder();
                case UiPhone.RadioSkip:
                    _actions?.RadioSkip();
                    return true;
                case UiPhone.RadioQuieter:
                    _actions?.RadioVolume(-1);
                    return true;
                case UiPhone.RadioLouder:
                    _actions?.RadioVolume(1);
                    return true;
                case UiPhone.DriverButton:
                    _actions?.Driver();
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>A view and a page at once (a test, a sample): -1 for the home screen; the page is held to the view's at the next layout.</summary>
        public void Show(int folder, int page)
        {
            _folder = folder >= 0 && folder < PhoneLayoutRules.FolderCount ? folder : -1;
            _page = Math.Max(0, page);
            _dirty = true;
        }

        /// <summary>A folder's first page; the home screen's page is kept to come back to.</summary>
        public void OpenFolder(int folder)
        {
            if (folder < 0 || folder >= PhoneLayoutRules.FolderCount || folder == _folder)
            {
                return;
            }
            if (_folder < 0)
            {
                _homePage = _page;
            }
            _folder = folder;
            _page = 0;
            _dirty = true;
        }

        /// <summary>The home screen again, on the page it was left on; false when no folder is open.</summary>
        public bool CloseFolder()
        {
            if (_folder < 0)
            {
                return false;
            }
            _folder = -1;
            _page = _homePage;
            _dirty = true;
            return true;
        }

        /// <summary>A page on or back, round from the last to the first; false with one page.</summary>
        public bool Turn(int step)
        {
            int pages = Math.Max(1, _model.Pages);
            if (pages <= 1 || step == 0)
            {
                return false;
            }
            _page = ((_page + Math.Sign(step)) % pages + pages) % pages;
            _dirty = true;
            return true;
        }

        public void Focused(int command, int arg)
        {
        }

        public bool Back()
        {
            if (CloseFolder())
            {
                return true;
            }
            if (_actions != null && _actions.Focused)
            {
                _actions.Leave();
                return true;
            }
            return false;
        }

        public void Tab(int step)
        {
            Turn(step);
        }

        public bool Act(UiAction action)
        {
            return false;
        }

        public void Reveal(int list, int index)
        {
        }

        public void Wheel(int clip, float notches)
        {
            Turn(notches > 0f ? -1 : 1);
        }
    }
}
