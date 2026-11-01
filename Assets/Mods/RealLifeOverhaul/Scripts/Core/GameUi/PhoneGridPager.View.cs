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
        // The game's buttons are found once by name, a mod button's rank read once from its name. Where each stands among the
        // grid's children is the view's order, set by Order.
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
            for (int i = 0; i < count; i++)
            {
                Transform found = _found[i];
                _isGame[i] = _gameButtons.Contains(found);
                if (_isGame[i])
                {
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
            }
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

        // Without folders every button; in folders the home screen's or the open folder's items (the two kept first lead the
        // HUD phone's home screen), each folder's count and its small pictures. A folder left empty closes.
        private void View()
        {
            _items.Clear();
            int count = _apps.Count;
            for (int i = 0; i < count; i++)
            {
                _itemOf[i] = -1;
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
            int n = PhoneLayoutRules.View(_kind, _place, count, _open, !_strip, _view, _members);
            if (_open >= 0 && _members[_open] == 0)
            {
                _open = -1;   // its last app moved out or switched off
                _page = _homePage;
                n = PhoneLayoutRules.View(_kind, _place, count, _open, !_strip, _view, _members);
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
                bool show = _itemOf[i] >= 0 && PageOf(_itemOf[i]) == _page;
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
                ShowFolders();
            }
            int onPage = PagerRules.PageSize(_page, _items.Count, ItemTiles);
            int gaps = PhoneLayoutRules.Gaps(onPage, ItemTiles, Pages);
            ShowGaps(gaps);
            More();
            Order(gaps);
            if (_strip)
            {
                PlaceStripPage();
            }
            else
            {
                PlaceGridPage();
            }
            Paint();   // the folders' badges too, with or without the button
            if (_checkDue && _strip && Meta.Dev.Verbose && isActiveAndEnabled)
            {
                _checkDue = false;
                StripCheck();
            }
        }

        // The folders' tiles of the page that shows, and the Back tile on a folder's page.
        private void ShowFolders()
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
        }

        // The layout places what shows in sibling order, so everything stands in the view's order: a folder's Back tile, the
        // items, the empty tiles that fill a short page, the More apps button, then the buttons no view holds (in closed
        // folders). Only the page's items show among the items, so the button is always the page's last tile. Moved only
        // when out of order.
        private void Order(int gaps)
        {
            _sorted.Clear();
            if (_foldered && _open >= 0 && _folders.BackTile != null)
            {
                _sorted.Add(_folders.BackTile);
            }
            for (int j = 0; j < _items.Count; j++)
            {
                _sorted.Add(_items[j]);
            }
            for (int g = 0; g < gaps; g++)
            {
                _sorted.Add(Gap(g));
            }
            if (_more != null)
            {
                _sorted.Add(_more);
            }
            for (int i = 0; i < _apps.Count; i++)
            {
                if (_itemOf[i] < 0)
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
