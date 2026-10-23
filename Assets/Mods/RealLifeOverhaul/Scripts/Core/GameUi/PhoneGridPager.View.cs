using System;
using UnityEngine;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>The pager's view: which button is on which page, the home screen's or a folder's, and their order.</summary>
    public sealed partial class PhoneGridPager
    {
        // The apps are every child of the grid its layout places, whoever made it (Going Public's Business, Chat and Hub are
        // copies with the game's script taken off), that is showing or that a pager of this grid switched off (PageMark). A
        // child somebody else keeps off is theirs to show.
        private void Collect()
        {
            _found.Clear();
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child == _template || child.GetComponent<PhoneTile>() != null)
                {
                    continue;   // the template, and this pager's own tiles
                }
                var mark = child.GetComponent<PageMark>();
                if (mark == null && !_aside)
                {
                    mark = child.gameObject.AddComponent<PageMark>();   // on any button: its being shown or hidden reaches the pager
                }
                if (mark != null)
                {
                    mark.Pager = this;
                }
                if (!(child is RectTransform placed) || Ignored(placed))
                {
                    continue;   // placed by whoever put it there, not by the layout
                }
                if (child.gameObject.activeSelf || mark != null && mark.Off)
                {
                    _found.Add(child);
                }
            }
            GameFirst();
        }

        // The game's apps first in the game's order, then the mod's in their fixed order, then other mods' (PagerRules.Arrange).
        // The game's buttons are found once by name, a mod button's rank read once from its name. If any stands out of that
        // order, every button after the game's is moved to the end in it, so the layout shows the pages in order.
        private void GameFirst()
        {
            int count = _found.Count;
            if (_gameApps < 0)
            {
                for (int i = 0; i < count; i++)
                {
                    if (Enum.TryParse(_found[i].name, out AppName _))
                    {
                        _gameButtons.Add(_found[i]);
                    }
                }
                if (_gameButtons.Count >= PagerRules.LeastTiles)
                {
                    _gameApps = _gameButtons.Count;   // once: the game's apps come with its interface and never go
                }
                else
                {
                    _gameButtons.Clear();
                }
            }
            if (_isGame.Length < count)
            {
                _isGame = new bool[count * 2];
                _order = new int[count * 2];
                _rank = new int[count * 2];
            }
            if (_ranks.Count > RanksKept)
            {
                _ranks.Clear();   // buttons taken off leave their names behind
            }
            int lastGame = -1;
            for (int i = 0; i < count; i++)
            {
                Transform found = _found[i];
                _isGame[i] = _gameButtons.Contains(found);
                if (_isGame[i])
                {
                    lastGame = found.GetSiblingIndex();
                    _rank[i] = -1;
                }
                else if (!_ranks.TryGetValue(found, out _rank[i]))
                {
                    _rank[i] = PagerRules.ModRank(found.name);
                    _ranks[found] = _rank[i];
                }
            }
            _gameShown = PagerRules.Arrange(_isGame, _rank, count, _order);
            _apps.Clear();
            if (_kind.Length < count)
            {
                _kind = new int[count * 2];
                _place = new int[count * 2];
                _badge = new int[count * 2];
                _itemOf = new int[count * 2];
                _docked = new bool[count * 2];
            }
            bool among = false;
            int before = -1;
            for (int i = 0; i < count; i++)
            {
                Transform item = _found[_order[i]];
                _apps.Add(item);
                _kind[i] = _isGame[_order[i]] ? PhoneLayoutRules.GameItem : _rank[_order[i]] >= 0 ? PhoneLayoutRules.ModItem : PhoneLayoutRules.OtherItem;
                if (_kind[i] == PhoneLayoutRules.ModItem && !_strip)
                {
                    var press = item.GetComponent<PhoneAppPress>() ?? item.gameObject.AddComponent<PhoneAppPress>();   // a right click moves it (PhoneMove)
                    press.Pager = this;
                }
                int at = item.GetSiblingIndex();
                among |= i >= _gameShown && (at < lastGame || at < before);   // among the game's, or out of the order worked out
                before = i >= _gameShown ? at : before;
            }
            if (among && !_aside && !FoldersWanted)   // in folders the order is the view's: Order
            {
                for (int i = _gameShown; i < count; i++)
                {
                    _apps[i].SetAsLastSibling();   // in order: the others keep theirs among themselves
                }
            }
        }

        private int PageOf(int index)
        {
            return PagerRules.PageOf(index, _items.Count, ItemTiles);
        }

        // Made once for the grid; failing the game's button, plain pages for the session.
        private bool MakeFolders()
        {
            _folders = PhoneFolders.Make(this, _template, _strip);
            _noFolders = _folders == null;
            return !_noFolders;
        }

        // The option on and room for a folder's page.
        private bool FoldersWanted => PhoneLayoutPrefs.On && PhoneLayoutRules.FoldersFit(_strip, Tiles);

        // Without folders every button; in folders the home screen's or the open folder's items, the dock row, each folder's
        // count and its small pictures. A folder left empty closes.
        private void View()
        {
            _items.Clear();
            _dock.Clear();
            int count = _apps.Count;
            for (int i = 0; i < count; i++)
            {
                _itemOf[i] = -1;
                _docked[i] = false;
            }
            _inDock = 0;
            _foldered = FoldersWanted && !_noFolders && (_folders != null || MakeFolders());
            if (!_foldered)
            {
                if (_folders != null)
                {
                    _folders.Destroy();   // the option off: nothing of the folders left on the grid
                    _folders = null;
                }
                bool wasOpen = _open >= 0;
                _open = -1;
                Array.Clear(_members, 0, _members.Length);
                for (int i = 0; i < count; i++)
                {
                    _itemOf[i] = _items.Count;
                    _items.Add(_apps[i]);
                }
                if (wasOpen)
                {
                    Keys();
                }
                return;
            }
            PhoneLayout layout = PhoneLayoutPrefs.Layout;
            for (int i = 0; i < count; i++)
            {
                _place[i] = _kind[i] == PhoneLayoutRules.ModItem ? layout.Place(_apps[i].name) : PhoneLayoutRules.Home;
                _inDock += _kind[i] == PhoneLayoutRules.ModItem && _place[i] == PhoneLayoutRules.Dock && _inDock < PhoneLayoutRules.DockMost ? 1 : 0;
            }
            if (_view.Length < count + PhoneLayoutRules.FolderCount)
            {
                _view = new int[(count + PhoneLayoutRules.FolderCount) * 2];
            }
            int n = PhoneLayoutRules.View(_kind, _place, count, _open, !_strip, _view, _dockAt, out int docked, _members);
            if (_open >= 0 && _members[_open] == 0)
            {
                _open = -1;   // its last app moved out or switched off
                _page = _homePage;
                n = PhoneLayoutRules.View(_kind, _place, count, _open, !_strip, _view, _dockAt, out docked, _members);
                Keys();
            }
            for (int j = 0; j < n; j++)
            {
                int entry = _view[j];
                if (entry >= 0)
                {
                    _itemOf[entry] = _items.Count;
                    _items.Add(_apps[entry]);
                }
                else
                {
                    _items.Add(_folders.Tile(-1 - entry));
                }
            }
            for (int d = 0; d < docked; d++)
            {
                _docked[_dockAt[d]] = true;
                _dock.Add(_apps[_dockAt[d]]);
            }
            _folders.Fill(_apps, _kind, _place, count);
        }

        private static bool PagedOff(Transform button)
        {
            var mark = button.GetComponent<PageMark>();
            return mark != null && mark.Off;
        }

        private void Apply()
        {
            for (int i = 0; i < _apps.Count; i++)
            {
                Transform app = _apps[i];
                bool show = _itemOf[i] >= 0 ? PageOf(_itemOf[i]) == _page : _docked[i];   // on the page that shows, or in the dock row
                var mark = app.GetComponent<PageMark>();
                if (!show && mark == null)
                {
                    mark = app.gameObject.AddComponent<PageMark>();
                }
                if (mark != null)
                {
                    mark.Off = !show;
                }
                if (app.gameObject.activeSelf != show)
                {
                    app.gameObject.SetActive(show);
                }
            }
            if (_foldered)
            {
                ShowTiles();
            }
            if (_strip)
            {
                PlaceStripPage();
            }
            else
            {
                PlaceGridPage();
            }
            More();
            Paint();   // the folders' badges too, with or without the button
            if (_checkDue && _strip && Meta.Dev.Verbose && isActiveAndEnabled)
            {
                _checkDue = false;
                StripCheck();
            }
        }

        // The folders' tiles of the page that shows, the Back tile, the empty tiles that keep the dock on the last row, then
        // every item in the order the layout is to place it.
        private void ShowTiles()
        {
            int per = PagerRules.PerPage(_items.Count, ItemTiles);
            int first = _page * per;
            int onPage = PagerRules.PageSize(_page, _items.Count, ItemTiles);
            for (int f = 0; f < PhoneLayoutRules.FolderCount; f++)
            {
                Transform tile = _folders.Tile(f);
                if (tile != null)
                {
                    int at = _items.IndexOf(tile);
                    _folders.Show(f, at >= first && at < first + onPage);
                }
            }
            _folders.Back(_open);
            int gaps = PhoneLayoutRules.Gaps(onPage, ItemTiles, !_strip && _open < 0 && _dock.Count > 0);
            _folders.ShowGaps(gaps);
            Order(gaps);
        }

        // The layout places what shows in sibling order, so everything stands in the view's order: the game's apps, the Back
        // tile, the other items, the empty tiles, the dock, then the buttons in closed folders. Moved only when out of order.
        private void Order(int gaps)
        {
            _sorted.Clear();
            for (int i = 0; i < _apps.Count; i++)
            {
                if (_kind[i] == PhoneLayoutRules.GameItem)
                {
                    _sorted.Add(_apps[i]);
                }
            }
            if (_open >= 0 && _folders.BackTile != null)
            {
                _sorted.Add(_folders.BackTile);
            }
            for (int j = 0; j < _items.Count; j++)
            {
                if (!_gameButtons.Contains(_items[j]))
                {
                    _sorted.Add(_items[j]);
                }
            }
            for (int g = 0; g < gaps; g++)
            {
                _sorted.Add(_folders.Gap(g));
            }
            for (int d = 0; d < _dock.Count; d++)
            {
                _sorted.Add(_dock[d]);
            }
            for (int i = 0; i < _apps.Count; i++)
            {
                if (_kind[i] != PhoneLayoutRules.GameItem && _itemOf[i] < 0 && !_docked[i])
                {
                    _sorted.Add(_apps[i]);
                }
            }
            int last = -1;
            bool inOrder = true;
            for (int k = 0; k < _sorted.Count && inOrder; k++)
            {
                int at = _sorted[k] != null ? _sorted[k].GetSiblingIndex() : last + 1;
                inOrder = at > last;
                last = at;
            }
            if (inOrder)
            {
                return;
            }
            for (int k = 0; k < _sorted.Count; k++)
            {
                if (_sorted[k] != null)
                {
                    _sorted[k].SetAsLastSibling();
                }
            }
        }
    }
}
