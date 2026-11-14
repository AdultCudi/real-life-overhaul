using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// All apps as a screen of the kit: the frame's apps (UiShellApps) in a Favourites tab first, the phone's folders and Other
    /// mods, and the bar's page last; the chosen tab's apps as rows with the line each app gives, a press opening that app's own
    /// page through its own button (UiShell), Back or the tile closing the panel. Every row has the player's star (the favourites,
    /// UiAppDeck), the keys F and P mark the app in focus a favourite and put it on the bar or take it off, and in the favourites
    /// and on the bar's page the rows move up and down by buttons, the keys and a drag. Laid out again only when the apps, their
    /// news or the deck change. Cost: a model and a layout on a change; nothing a frame.
    /// </summary>
    public sealed class AllAppsScreen : IUiScreen, IUiDragging
    {
        // The tabs, in the order they stand: Favourites at the top, the folders, the bar's page last.
        private const int FavSlot = 0, BarSlot = UiShellApps.Folders + 1, Slots = UiShellApps.Folders + 2;

        private readonly UiAllAppsModel _m = new UiAllAppsModel();
        private readonly List<UiShellApp> _rows = new List<UiShellApp>(24);
        private readonly List<int> _at = new List<int>(24);       // each row's index among UiShellApps.Items
        private readonly int[] _badges = new int[Slots];
        private int _slot = -1, _version = int.MinValue, _revision = -1, _shown = -1;
        private int _focusRow = -1;     // the row whose own controls the keys are on, for F and P
        private string _open, _dragId, _dragWas;
        private bool _dirty = true;

        /// <summary>The panel closes: Back, the tile again, or an app opened from it.</summary>
        public Action Close;

        /// <summary>An app pressed: the frame opens it through its own button; false when it could not.</summary>
        public Func<string, bool> Opening;

        public string Id => "all_apps";

        public bool Dirty => _dirty || UiShellApps.Version != _version || UiAppDeck.Current.Revision != _revision || UiShellApps.RowShown != _shown;

        /// <summary>Opens on a folder (a folder's entry in the app row), or on the open app's folder when -1; with no app open, on the favourites when the player has any.</summary>
        public void Show(int folder, string open)
        {
            _open = open;
            _slot = folder >= 0 ? folder + 1 : -1;
            for (int i = 0; _slot < 0 && i < UiShellApps.Items.Count; i++)
            {
                if (UiShellApps.Items[i].Id == open)
                {
                    _slot = UiShellApps.Items[i].Folder + 1;
                }
            }
            if (_slot < 0)
            {
                _slot = HasFavourites() ? FavSlot : 1;
            }
            _slot = Math.Max(0, Math.Min(Slots - 1, _slot));
            _m.First = 0;
            _dirty = true;
        }

        private static bool HasFavourites()
        {
            for (int i = 0; i < UiShellApps.Items.Count; i++)
            {
                if (UiShellApps.Items[i].Favourite)
                {
                    return true;
                }
            }
            return false;
        }

        public void Lay(UiScene s, UiFrame frame, bool dark, IUiKeys keys)
        {
            Build();
            UiAllApps.Lay(s, _m, frame, dark, keys);
            _m.FocusCommand = -1;
            _dirty = false;
        }

        public bool Press(int command, int arg)
        {
            if (command == UiAllApps.TabCommand && arg >= 0 && arg < Slots)
            {
                _slot = arg;
                _m.First = 0;
                _dirty = true;
                return true;
            }
            if (command == UiAllApps.EditCommand)
            {
                _slot = BarSlot;
                _m.First = 0;
                _dirty = true;
                return true;
            }
            if (command == UiAllApps.ResetCommand)
            {
                UiAppDeck deck = UiAppDeck.Current;
                return deck.ResetBar() && Changed(-1, 0);
            }
            if (arg < 0 || arg >= _rows.Count)
            {
                return false;
            }
            switch (command)
            {
                case UiAllApps.RowCommand:
                    if (_slot == BarSlot)
                    {
                        return PinRow(arg);
                    }
                    bool opened = Opening != null && Opening(_rows[arg].Id);
                    if (opened)
                    {
                        Close?.Invoke();
                    }
                    return opened;
                case UiAllApps.StarCommand: return StarRow(arg);
                case UiAllApps.PinCommand: return PinRow(arg);
                case UiAllApps.EarlierCommand: return MoveRow(arg, -1);
                case UiAllApps.LaterCommand: return MoveRow(arg, 1);
            }
            return false;
        }

        // The deck changed: kept, put on the apps, and the row's frame told; the keys follow the app to where it stands now.
        private bool Changed(int focusCommand, int focusArg)
        {
            UiAppDeck.Current.Commit();
            UiShellApps.Rearrange();
            _m.FocusCommand = focusCommand;
            _m.FocusArg = focusArg;
            _dirty = true;
            return true;
        }

        private bool StarRow(int row) => UiAppDeck.Current.ToggleFav(_rows[row].Id) && Changed(UiAllApps.StarCommand, row);

        // On the bar or off it. An app put on the bar stays where it is in the bar's page's list, which shows the new order from the next layout.
        private bool PinRow(int row)
        {
            UiAppDeck deck = UiAppDeck.Current;
            string id = _rows[row].Id;
            bool done = _rows[row].InRow ? deck.TakeOffBar(id, UiShellApps.Items) : deck.PutOnBar(id, UiShellApps.Items);
            return done && Changed(-1, 0);
        }

        // One place up or down the list the player orders: the favourites, or the bar's apps on its page. Past an app that is not here.
        private bool MoveRow(int row, int step)
        {
            if (_slot != FavSlot && !(_slot == BarSlot && _rows[row].InRow))
            {
                return false;
            }
            UiAppDeck deck = UiAppDeck.Current;
            bool done = _slot == FavSlot ? deck.MoveFav(_rows[row].Id, step, UiShellApps.Items) : deck.MoveBar(_rows[row].Id, step, UiShellApps.Items);
            return done && Changed(step < 0 ? UiAllApps.EarlierCommand : UiAllApps.LaterCommand, row + step);
        }

        public void Focused(int command, int arg)
        {
            _focusRow = command >= UiAllApps.RowCommand && command <= UiAllApps.PinCommand ? arg : -1;
        }

        public bool Back()
        {
            Close?.Invoke();
            return true;
        }

        public void Tab(int step)
        {
            _slot = (_slot + step + Slots) % Slots;
            _m.First = 0;
            _dirty = true;
        }

        // F marks the app in focus a favourite, P puts it on the bar or takes it off, from any tab.
        public bool Act(UiAction action)
        {
            if (_focusRow < 0 || _focusRow >= _rows.Count)
            {
                return false;
            }
            if (action == UiAction.Alt)
            {
                return StarRow(_focusRow);
            }
            return action == UiAction.Option && PinRow(_focusRow);
        }

        public void Reveal(int list, int index)
        {
            int fit = Math.Max(1, _m.RowCount - _m.First);
            if (index < _m.First)
            {
                _m.First = Math.Max(0, index);
                _dirty = true;
            }
            else if (index >= 0 && index < _m.RowCount && index >= _m.First + fit)
            {
                _m.First = index;
                _dirty = true;
            }
            else if (index >= 0 && index < _m.RowCount)
            {
                _m.First = Math.Min(index, Math.Max(0, _m.RowCount - 1));
                _dirty = true;
            }
        }

        public void Wheel(int clip, float notches)
        {
            int next = Math.Max(0, Math.Min(Math.Max(0, _m.RowCount - 1), _m.First - (int)Math.Round(notches)));
            if (next != _m.First)
            {
                _m.First = next;
                _dirty = true;
            }
        }

        // ---------------------------------------------------------------------------------------------------- the drag

        public void PointerDown(int command, int arg, float x, float y)
        {
        }

        // A row of the favourites or an app of the bar can be taken by its body and dropped on another's place.
        public bool DragBegin(int command, int arg, float x, float y)
        {
            bool orders = _slot == FavSlot || _slot == BarSlot;
            if (command != UiAllApps.RowCommand || !orders || arg < 0 || arg >= _rows.Count || (_slot == BarSlot && !_rows[arg].InRow))
            {
                return false;
            }
            _dragId = _rows[arg].Id;
            _dragWas = UiAppDeck.Current.Write();
            return true;
        }

        public void DragMove(float x, float y)
        {
            if (_dragId == null)
            {
                return;
            }
            int most = _slot == BarSlot ? UiShellApps.Row.Count : _m.RowCount;   // the bar's apps stand first on its page: an app drops among them
            int place = Math.Max(0, Math.Min(most - 1, _m.First + (int)Math.Floor((y - _m.ListTop) / UiAllApps.RowStep)));
            UiAppDeck deck = UiAppDeck.Current;
            if (_slot == FavSlot ? deck.MoveFavTo(_dragId, place, UiShellApps.Items) : deck.MoveBarTo(_dragId, place, UiShellApps.Items))
            {
                UiShellApps.Rearrange(false);   // the list follows the pointer; the bar's row waits for the drop
                _dirty = true;
            }
        }

        public void DragEnd(bool cancelled)
        {
            if (_dragId == null)
            {
                return;
            }
            UiAppDeck deck = UiAppDeck.Current;
            if (cancelled)
            {
                deck.Restore(_dragWas);
            }
            else
            {
                deck.Commit();
            }
            _dragId = _dragWas = null;
            UiShellApps.Rearrange();
            _dirty = true;
        }

        // ------------------------------------------------------------------------------------------------------ the model

        private void Build()
        {
            UiAppDeck deck = UiAppDeck.Current;
            _version = UiShellApps.Version;
            _revision = deck.Revision;
            _shown = UiShellApps.RowShown;
            Array.Clear(_badges, 0, _badges.Length);
            _rows.Clear();
            _at.Clear();
            for (int i = 0; i < UiShellApps.Items.Count; i++)
            {
                UiShellApp a = UiShellApps.Items[i];
                if (a.Folder >= 0 && a.Folder < UiShellApps.Folders)
                {
                    _badges[a.Folder + 1] += a.Badge;
                }
                _badges[FavSlot] += a.Favourite ? a.Badge : 0;
            }
            if (_slot == FavSlot)
            {
                deck.Favourites(UiShellApps.Items, _at);
            }
            else if (_slot == BarSlot)
            {
                _at.AddRange(UiShellApps.Row);   // the bar's apps in the bar's order, then the rest in the phone's
                for (int i = 0; i < UiShellApps.Items.Count; i++)
                {
                    if (!UiShellApps.Items[i].InRow)
                    {
                        _at.Add(i);
                    }
                }
            }
            else
            {
                for (int i = 0; i < UiShellApps.Items.Count; i++)
                {
                    if (UiShellApps.Items[i].Folder == _slot - 1)
                    {
                        _at.Add(i);
                    }
                }
            }
            for (int i = 0; i < _at.Count; i++)
            {
                _rows.Add(UiShellApps.Items[_at[i]]);
            }
            if (_m.Tabs.Length != Slots)
            {
                _m.Tabs = new UiAllAppsTab[Slots];
            }
            _m.Tabs[FavSlot] = new UiAllAppsTab { Icon = UiIcon.Star, Tint = UiIconTints.Of(UiIcon.Star), Label = UiText.T("reallife_ui_apps_favourites"), Badge = _badges[FavSlot] };
            for (int f = 0; f < UiShellApps.Folders; f++)
            {
                UiIcon icon = UiShellApps.FolderIcon(f);
                _m.Tabs[f + 1] = new UiAllAppsTab { Icon = icon, Tint = UiIconTints.Of(icon), Label = UiText.T(UiShellApps.FolderKey(f)), Badge = _badges[f + 1] };
            }
            _m.Tabs[BarSlot] = new UiAllAppsTab { Icon = UiIcon.Sliders, Tint = UiIconTints.Of(UiIcon.Sliders), Label = UiText.T("reallife_ui_apps_bar") };
            _m.Tab = _slot;
            _m.Mode = _slot == FavSlot ? UiAllApps.Favourites : _slot == BarSlot ? UiAllApps.Bar : UiAllApps.Folder;
            _m.EditLabel = UiText.T("reallife_ui_apps_edit");
            _m.ResetLabel = UiText.T("reallife_ui_apps_reset");
            _m.Title = UiText.T("reallife_ui_shell_all_apps");
            if (_m.Rows.Length < _rows.Count)
            {
                _m.Rows = new UiAllAppsRow[_rows.Count + 8];
            }
            for (int i = 0; i < _rows.Count; i++)
            {
                UiShellApp a = _rows[i];
                _m.Rows[i] = new UiAllAppsRow
                {
                    Icon = a.Icon, Tint = a.Tint, Title = a.Title, Badge = a.Badge, Open = a.Id == _open, Status = RowLine(a, _at[i]),
                    Picture = a.Picture, Fav = a.Favourite, OnBar = a.InRow
                };
            }
            _m.RowCount = _rows.Count;
            _m.First = Math.Min(_m.First, Math.Max(0, _m.RowCount - 1));
            _m.Empty = UiText.T(_slot == FavSlot ? "reallife_ui_apps_none_fav" : _slot == UiShellApps.Other + 1 ? "reallife_ui_shell_none_other" : "reallife_ui_shell_none");
            if (_m.Prompts.Length != 6)
            {
                _m.Prompts = new UiPrompt[6];
            }
            _m.Prompts[0] = new UiPrompt { Action = UiAction.Move, Label = UiText.T("reallife_ui_prompt_move") };
            _m.Prompts[1] = new UiPrompt { Action = UiAction.Confirm, Label = UiText.T("reallife_ui_prompt_select") };
            _m.Prompts[2] = new UiPrompt { Action = UiAction.TabPrev, Label = UiText.T("reallife_ui_shell_prompt_folder") };
            _m.Prompts[3] = new UiPrompt { Action = UiAction.Alt, Label = UiText.T("reallife_ui_apps_prompt_favourite") };
            _m.Prompts[4] = new UiPrompt { Action = UiAction.Option, Label = UiText.T("reallife_ui_apps_prompt_bar") };
            _m.Prompts[5] = new UiPrompt { Action = UiAction.Back, Label = UiText.T("reallife_ui_prompt_back") };
            _m.PromptCount = 6;
        }

        // The line under an app's name: on the bar's page where it stands (or that the strip has no room for it), else what the app gives.
        private string RowLine(UiShellApp a, int item)
        {
            if (_slot != BarSlot)
            {
                return UiShellApps.Status != null ? UiShellApps.Status(a.Id, a.Badge) : null;
            }
            if (!a.InRow)
            {
                return UiText.T("reallife_ui_apps_not_on_bar");
            }
            int place = UiShellApps.Row.IndexOf(item) + 1;
            return place > UiShellApps.RowShown && UiShellApps.RowShown > 0
                ? UiText.T("reallife_ui_apps_no_room")
                : UiText.Fill("reallife_ui_apps_bar_place", "place", UiText.Whole(place));
        }
    }
}
