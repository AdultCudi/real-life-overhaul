using System;
using RealLifeOverhaul.GameUi;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// A list window of the mod's as the kit's picker (UiPicker): its headline in the strip with the glyph of what the list
    /// is for, the lines under it as what the desk says, its entries numbered on the number keys with their lines, its
    /// notes (a bill, a charge sheet, a desk's records) with every line of their words, a greyed entry dimmed and refused
    /// as its greyed button is, and a page at a time when they do not all stand on the screen. Locked (custody, a test): no
    /// close, Back is the game's menu, and a line at its foot says why. Every word is the caller's own, as it gave them to
    /// the list; every press goes through the list's own pick or close, so each caller's rules stay its own. Cost: a number
    /// compared a frame; a layout a change.
    /// </summary>
    public sealed class WorldPickerScreen : IUiScreen
    {
        private const int MostPerPage = 8;

        private readonly UiPickerModel _m = new UiPickerModel { PerPage = MostPerPage };
        private TravelList _list;
        private int[] _ids = new int[0];
        private int _seen = -1, _laidPage = -1;
        private bool _locked;
        private float _room;

        public string Id => WorldKits.PickerId;

        /// <summary>The window's title and glyph, as the host is bound with them.</summary>
        public string Title => _m.Title;

        public UiIcon Icon => _m.Icon;

        /// <summary>The model last read, for the tests.</summary>
        public UiPickerModel Model => _m;

        /// <summary>True when the list says more than was laid out (a filling, a line, a lock) or the page turned.</summary>
        public bool Dirty => _list != null && (_list.Version != _seen || _list.Locked != _locked || _m.Page != _laidPage);

        /// <summary>The list to draw; null when none.</summary>
        public void Show(TravelList list)
        {
            _list = list;
            _seen = -1;
            _m.Page = 0;
            if (list != null)
            {
                Read();
            }
        }

        // The list's words and entries into the picker's model, as the caller gave them.
        private void Read()
        {
            _seen = _list.Version;
            _locked = _list.Locked;
            bool custody = _list.Name == "RealLifeOverhaul Custody";
            _m.Title = Plain(_list.TitleWords) ?? string.Empty;
            _m.Icon = IconOf(_list.Name);
            _m.EmptyIcon = _m.Icon;
            _m.Said = Plain(_list.InfoWords);
            _m.Locked = _locked;
            _m.LockWords = _locked && custody ? UiText.T("reallife_ui_world_pick_custody") : null;
            _m.FootNote = !_locked ? null : UiText.T(custody ? "reallife_ui_world_pick_custody_foot" : "reallife_ui_world_pick_locked_foot");
            _m.Numbered = true;
            _m.PageWords = UiText.T("reallife_ui_world_pick_page").Replace("{page}", "{0}").Replace("{pages}", "{1}");
            _m.EmptyTitle = UiText.T("reallife_ui_world_pick_empty");
            _m.EmptyBody = UiText.T("reallife_ui_world_pick_empty_body");
            _m.EmptyAction = _locked ? null : UiText.T("common_close");
            int n = _list.Count;
            if (_m.Entries.Length != n)
            {
                _m.Entries = new UiPickEntry[n];
                _ids = new int[n];
            }
            for (int i = 0; i < n; i++)
            {
                _list.Read(i, out int id, out string name, out string detail, out string _, out bool enabled, out bool note);
                _list.ReadMark(i, out string figure, out string reason);
                _ids[i] = id;
                _m.Entries[i] = new UiPickEntry
                {
                    Icon = custody && !note ? CustodyIcon(id) : _m.Icon, Name = Plain(name) ?? string.Empty, Detail = Plain(detail), Note = note,
                    Disabled = !note && !enabled, Figure = Plain(figure), Reason = Plain(reason),
                };
            }
            // The list's facts over its entries ("Held since"), as many as the panel shows.
            int facts = Math.Min(UiPickerModel.MostContext, _list.FactCount);
            if (_m.Context.Length != facts)
            {
                _m.Context = new UiInfoRow[facts];
            }
            for (int i = 0; i < facts; i++)
            {
                _list.ReadFact(i, out string label, out string value);
                _m.Context[i] = new UiInfoRow { Label = Plain(label) ?? string.Empty, Value = Plain(value) };
            }
            _m.Prompts = new[]
            {
                new UiPrompt { Action = UiAction.Move, Label = UiText.T("reallife_ui_prompt_move") },
                new UiPrompt { Action = UiAction.Confirm, Label = UiText.T("reallife_ui_prompt_select") },
                new UiPrompt { Action = UiAction.Back, Label = _locked ? UiText.T("input_key_menu") : UiText.T("reallife_ui_prompt_back") },
            };
            _m.PromptCount = _m.Prompts.Length;
            _room = -1f;   // the page's size again at the next layout, for the screen it stands on
        }

        public void Lay(UiScene s, UiFrame frame, bool dark, IUiKeys keys)
        {
            if (_list != null && (_list.Version != _seen || _list.Locked != _locked))
            {
                int page = _m.Page;
                Read();
                _m.Page = page;
            }
            // The room a page's entries have: the screen above the bar less the panel's own parts and its pages' bar.
            float room = frame.BandY - UiTokens.S8 * 2f - UiPicker.Height(Empty(_m)) + UiTokens.Row - (UiTokens.Control + UiTokens.S2);
            if (room != _room)
            {
                _room = room;
                _m.PerPage = UiPicker.PerPageFor(_m, MostPerPage, room);
            }
            _m.Page = Math.Max(0, Math.Min(_m.Page, UiPicker.Pages(_m) - 1));
            _laidPage = _m.Page;
            UiWorld.PickerLay(s, _m, frame, dark, keys, Docked);
        }

        // The panel without its entries: what a page's entries have left of the screen.
        private static UiPickerModel Empty(UiPickerModel m) => new UiPickerModel
        {
            Title = m.Title, Icon = m.Icon, Said = m.Said, Locked = m.Locked, LockWords = m.LockWords, FootNote = m.FootNote, Context = m.Context,
            Entries = new[] { new UiPickEntry { Name = string.Empty } }, PerPage = 1,
        };

        // The cell's entries by what each does (Law/CustodyList: sit it out, bail, a lawyer, the duty doctor).
        private UiIcon CustodyIcon(int id) => id == 0 ? UiIcon.Clock : id == 1 ? UiIcon.Key : id == 2 ? UiIcon.Scales : id == 3 ? UiIcon.Heart : _m.Icon;

        // The caller stood its window at the screen's right edge (custody beside the cell): the picker stands there too.
        private bool Docked => _list != null && _list.Window != null && _list.Window.anchorMin.x > 0.9f;

        public bool Press(int command, int arg)
        {
            if (_list == null)
            {
                return false;
            }
            switch (command)
            {
                case UiPicker.EntryCommand:
                    return arg >= 0 && arg < _ids.Length && !_m.Entries[arg].Note && !_m.Entries[arg].Disabled && _list.Pick(_ids[arg]);
                case UiPicker.PageCommand:
                    return Turn(arg);
                case UiPicker.EmptyCommand:
                case UiDraw.CloseCommand:
                    return Back();
            }
            return false;
        }

        private bool Turn(int step)
        {
            int page = _m.Page + Math.Sign(step);
            if (page < 0 || page >= UiPicker.Pages(_m))
            {
                return false;
            }
            _m.Page = page;
            return true;
        }

        public void Focused(int command, int arg)
        {
        }

        /// <summary>Back closes the list as its Cancel did; a locked list keeps the player, and Escape opens the game's menu as over the window.</summary>
        public bool Back()
        {
            if (_list == null || _list.Locked)
            {
                return false;
            }
            _list.Close();
            return true;
        }

        public void Tab(int step) => Turn(step);

        public bool Act(UiAction action) => false;

        public void Reveal(int list, int index)
        {
            if (index >= 0 && _m.PerPage > 0)
            {
                _m.Page = Math.Min(UiPicker.Pages(_m) - 1, index / _m.PerPage);
            }
        }

        public void Wheel(int clip, float notches) => Turn(notches > 0f ? -1 : 1);

        // The glyph for what a list is for, by its window's name (the feature names it); a list's glyph when none says more.
        internal static UiIcon IconOf(string name)
        {
            string n = name ?? string.Empty;
            if (n.EndsWith("Custody", StringComparison.Ordinal) || n.EndsWith(" Law", StringComparison.Ordinal))
            {
                return UiIcon.ShieldCheck;
            }
            if (n.EndsWith("Bus Stops", StringComparison.Ordinal))
            {
                return UiIcon.Train;
            }
            if (n.EndsWith("Travel", StringComparison.Ordinal))
            {
                return UiIcon.MapTrifold;
            }
            if (n.EndsWith("Garage", StringComparison.Ordinal))
            {
                return UiIcon.Wrench;
            }
            if (n.EndsWith("Cooking", StringComparison.Ordinal))
            {
                return UiIcon.ForkKnife;
            }
            if (n.EndsWith("Dispatch", StringComparison.Ordinal))
            {
                return UiIcon.Truck;
            }
            if (n.EndsWith("Design", StringComparison.Ordinal))
            {
                return UiIcon.PaintBrush;
            }
            if (n.EndsWith("Tender", StringComparison.Ordinal))
            {
                return UiIcon.Receipt;
            }
            if (n.EndsWith("Licences", StringComparison.Ordinal))
            {
                return UiIcon.Cap;
            }
            if (n.EndsWith("Utilities Desk", StringComparison.Ordinal))
            {
                return UiIcon.Lightning;
            }
            if (n.EndsWith("PhoneMove", StringComparison.Ordinal))
            {
                return UiIcon.Phone;
            }
            return UiIcon.ListChecks;
        }

        /// <summary>The caller's words with their rich text taken off (sizes, colours, line breaks kept as lines); null for none.</summary>
        internal static string Plain(string words) => WorldHost.Plain(words);
    }
}
