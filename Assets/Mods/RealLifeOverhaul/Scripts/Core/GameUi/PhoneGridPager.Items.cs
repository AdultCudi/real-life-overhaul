using System.Collections.Generic;
using UnityEngine;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>The pager's apps as it last arranged them, for the RLO interface's phone (Ui/Phone/PhoneItemsGame).</summary>
    public sealed partial class PhoneGridPager
    {
        /// <summary>
        /// Every app of the grid in the pager's order (the game's, the mod's, other mods'), each one's kind and its place
        /// (a folder, kept first or the home screen; the home screen for all while the folders are off). Copies into the
        /// caller's lists; nothing kept. Empty while it steps aside.
        /// </summary>
        internal void Arranged(List<Transform> apps, List<int> kinds, List<int> places)
        {
            apps.Clear();
            kinds.Clear();
            places.Clear();
            if (_aside)
            {
                return;
            }
            PhoneLayout layout = _foldered ? PhoneLayoutPrefs.Layout : null;
            for (int i = 0; i < _apps.Count; i++)
            {
                if (_apps[i] == null)
                {
                    continue;
                }
                apps.Add(_apps[i]);
                kinds.Add(_kind[i]);
                places.Add(layout != null && _kind[i] == PhoneLayoutRules.ModItem ? layout.Place(_apps[i].name) : PhoneLayoutRules.Home);
            }
        }

        /// <summary>
        /// Test: every page of the home screen and of every folder laid out in turn, the More apps button asked where it
        /// stands on each (the last tile, or not there with one page), then the view the player had put back. "PASS" when it
        /// stood on the last tile of every page of every view.
        /// </summary>
        internal string MoreEverywhere()
        {
            if (_aside)
            {
                return "another mod pages this grid: PASS (no More apps button of this pager's)";
            }
            int open = _open, page = _page, home = _homePage, views = 0, wrong = 0;
            string first = null;
            for (int folder = -1; folder < PhoneLayoutRules.FolderCount; folder++)
            {
                if (folder >= 0 && (!_foldered || Members(folder) == 0))
                {
                    continue;
                }
                _open = folder;
                _page = 0;
                Refresh();
                for (int p = 0; p < Pages; p++)
                {
                    _page = p;
                    Refresh();
                    views++;
                    string said = MoreSays(out bool ok);
                    if (!ok)
                    {
                        wrong++;
                        first = first ?? (folder < 0 ? "home" : PhoneLayoutRules.FolderIds[folder]) + " page " + (p + 1) + ": " + said;
                    }
                }
            }
            _open = open;
            _page = page;
            _homePage = home;
            Refresh();
            return views + " page(s) of the home screen and the folders, the More apps button on the last tile of each: " + (wrong == 0 ? "PASS" : "FAIL, " + wrong + " wrong (" + first + ")");
        }
    }
}
