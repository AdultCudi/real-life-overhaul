namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// The phone's home screen as a real phone has it (PhoneGridPager): the mod's apps in five folders, a dock of two
    /// along the foot of the HUD phone, and the game's own apps and other mods' where they always stood. Which app is
    /// where: the table below, each app of <see cref="PagerRules.ModOrder"/> in the folder of its group, and on top of it
    /// the player's own moves (<see cref="PhoneLayout"/>, kept by PhoneLayoutPrefs).
    ///
    /// The home screen holds, in this order: the game's own apps in the game's order; on the full menu's row the docked
    /// apps (the HUD phone has them in its dock row instead); a tile for each folder with an app in it, in the folders'
    /// order; the mod's apps the player put on the home screen by themselves; then other mods' buttons as they stand. A
    /// folder opened shows its apps in the mod's order after its Back tile. Every button is in exactly one of those
    /// places, whatever the layout says: an app the layout puts nowhere it can be (a folder this version lacks, a third
    /// app in a dock of two) stands on the home screen.
    ///
    /// Plain arithmetic and ordinal comparisons: no Unity or game types, nothing allocated. Called only when the grid is
    /// shown or changed, never per frame; checked offline (ContractCheck, PhoneLayoutChecks).
    /// </summary>
    public static class PhoneLayoutRules
    {
        public const int Money = 0, Work = 1, Life = 2, Business = 3, Setup = 4;   // the folders, in the order they stand
        public const int FolderCount = 5;
        public const int Home = -1;      // a place: on the home screen by itself
        public const int Dock = -2;      // a place: in the dock
        public const int DockMost = 2;   // apps in the dock: the HUD phone's grid is two wide, and the dock is its last row
        public const int MiniMost = 9;   // small pictures on a folder's tile: three rows of three, as a real phone shows them
        public const int MiniRow = 3;    // ...a row of them
        public const int LeastStrip = 3; // tiles a page of the full menu's row needs for a folder's page: its Back tile, an app and the More apps button

        public const int GameItem = 0, ModItem = 1, OtherItem = 2;   // what a button of the grid is: the game's own, the mod's, another mod's

        /// <summary>Each folder's name in the saved layout: never translated, never changed.</summary>
        public static readonly string[] FolderIds = { "money", "work", "life", "business", "setup" };

        /// <summary>Each folder's name on its tile and its Back tile.</summary>
        public static readonly string[] FolderKeys =
        {
            "reallife_phone_folder_money", "reallife_phone_folder_work", "reallife_phone_folder_life", "reallife_phone_folder_business", "reallife_phone_folder_setup"
        };

        /// <summary>
        /// Where each app of <see cref="PagerRules.ModOrder"/> stands until the player moves it, name for name: the Planner and
        /// Flare in the dock, the rest in the folder of its group (money with the paper and the bills, work and study, the
        /// camera and the gallery, business, the Guide and the options). The City Ledger's own page has no button; its place
        /// is kept for the table's sake.
        /// </summary>
        public static readonly int[] Defaults =
        {
            Dock, Dock, Life, Life, Money, Money,
            Work, Work, Work,
            Money, Money, Money, Money,
            Business, Business, Business, Business, Business, Business, Business,
            Business, Money,
            Setup, Setup
        };

        /// <summary>Where an app of the mod's stands while the player has not moved it; the home screen for a name not in the table.</summary>
        public static int DefaultOf(string id)
        {
            int at = PagerRules.IndexOf(id);
            return at >= 0 && at < Defaults.Length ? Defaults[at] : Home;
        }

        /// <summary>Whether a place is one this version has: a folder, the home screen or the dock.</summary>
        public static bool Known(int place)
        {
            return place == Home || place == Dock || place >= 0 && place < FolderCount;
        }

        /// <summary>A place as the saved layout writes it: a folder's id, "home" or "dock".</summary>
        public static string Token(int place)
        {
            return place == Dock ? "dock" : place >= 0 && place < FolderCount ? FolderIds[place] : "home";
        }

        /// <summary>A place from the saved layout's word for it; false for a word this version does not know (a later one's folder).</summary>
        public static bool FromToken(string token, out int place)
        {
            place = Home;
            if (token == "home")
            {
                return true;
            }
            if (token == "dock")
            {
                place = Dock;
                return true;
            }
            for (int f = 0; f < FolderCount; f++)
            {
                if (string.Equals(token, FolderIds[f], System.StringComparison.Ordinal))
                {
                    place = f;
                    return true;
                }
            }
            return false;
        }

        /// <summary>Whether folders can be shown on a row of this many tiles: the HUD phone's always, the full menu's from three (a 4:3 screen has two).</summary>
        public static bool FoldersFit(bool strip, int tiles)
        {
            return !strip || tiles >= LeastStrip;
        }

        /// <summary>
        /// The tiles of a page that hold the view's items: the HUD phone's eight, less its dock row on the home screen while
        /// the dock has an app, less the Back tile on a folder's page; the full menu's row less the Back tile on a folder's page.
        /// The More apps button takes the last of them when there are pages (PagerRules).
        /// </summary>
        public static int ItemTiles(bool strip, int tiles, bool folderOpen, bool dockRow)
        {
            int all = strip ? tiles : PagerRules.PhoneTiles;
            int held = all - (folderOpen ? 1 : 0) - (!strip && !folderOpen && dockRow ? DockMost : 0);
            return held < PagerRules.LeastTiles ? PagerRules.LeastTiles : held;
        }

        /// <summary>The tile of the whole page (from 0) the More apps button stands on: the last of the items' tiles, after the Back tile on a folder's page.</summary>
        public static int MoreTile(bool strip, int tiles, bool folderOpen, bool dockRow)
        {
            return (folderOpen ? 1 : 0) + PagerRules.ButtonTile(ItemTiles(strip, tiles, folderOpen, dockRow));
        }

        /// <summary>
        /// The empty tiles the HUD phone's grid is given before its dock row, so that the dock always stands on the last row:
        /// every tile of the page's items not taken by one, the More apps button's too (it stands beside the grid, placed by
        /// hand). None without a dock row.
        /// </summary>
        public static int Gaps(int onPage, int itemTiles, bool dockRow)
        {
            if (!dockRow)
            {
                return 0;
            }
            int gaps = itemTiles - (onPage < 0 ? 0 : onPage);
            return gaps < 0 ? 0 : gaps;
        }

        /// <summary>
        /// The view of a grid: what its pages hold (<paramref name="view"/>: a button's place, or a folder's tile as -1 - the
        /// folder) and its dock row. Buttons by kind and layout place, in the pages' order (PagerRules.Arrange). A folder at
        /// 0 or more: its apps. Else the home screen (see the class), the dock's first two in <paramref name="dock"/> on the
        /// HUD phone (<paramref name="dockRow"/>), on the row after the game's own. Each folder's count goes in
        /// <paramref name="members"/>. Returns the view's length.
        /// </summary>
        public static int View(int[] kind, int[] place, int count, int folder, bool dockRow, int[] view, int[] dock, out int docked, int[] members)
        {
            docked = 0;
            if (members != null)
            {
                for (int f = 0; f < members.Length; f++)
                {
                    members[f] = 0;
                }
            }
            if (kind == null || place == null || view == null || count <= 0)
            {
                return 0;
            }
            if (count > kind.Length || count > place.Length)
            {
                count = kind.Length < place.Length ? kind.Length : place.Length;
            }
            for (int i = 0; i < count; i++)
            {
                int f = FolderOf(kind[i], place[i]);
                if (f >= 0 && members != null && f < members.Length)
                {
                    members[f]++;
                }
            }
            int at = 0;
            if (folder >= 0 && folder < FolderCount)
            {
                for (int i = 0; i < count && at < view.Length; i++)
                {
                    if (FolderOf(kind[i], place[i]) == folder)
                    {
                        view[at++] = i;
                    }
                }
                return at;
            }
            for (int i = 0; i < count && at < view.Length; i++)
            {
                if (kind[i] == GameItem)
                {
                    view[at++] = i;
                }
            }
            // The dock: its first two in the mod's order; on the full menu's row they stand after the game's own.
            int inDock = 0;
            for (int i = 0; i < count; i++)
            {
                if (kind[i] != ModItem || place[i] != Dock || inDock >= DockMost)
                {
                    continue;
                }
                inDock++;
                if (dockRow)
                {
                    if (dock != null && docked < dock.Length)
                    {
                        dock[docked++] = i;
                    }
                }
                else if (at < view.Length)
                {
                    view[at++] = i;
                }
            }
            for (int f = 0; f < FolderCount && at < view.Length; f++)
            {
                if (members != null && f < members.Length && members[f] > 0)
                {
                    view[at++] = -1 - f;
                }
            }
            // The home screen's own: those put there, and any the layout puts nowhere this version has (a third in the dock).
            inDock = 0;
            for (int i = 0; i < count && at < view.Length; i++)
            {
                if (kind[i] != ModItem)
                {
                    continue;
                }
                int p = place[i];
                if (p == Dock)
                {
                    if (inDock++ < DockMost)
                    {
                        continue;
                    }
                }
                else if (Known(p) && p != Home)
                {
                    continue;
                }
                view[at++] = i;
            }
            for (int i = 0; i < count && at < view.Length; i++)
            {
                if (kind[i] != GameItem && kind[i] != ModItem)
                {
                    view[at++] = i;
                }
            }
            return at;
        }

        /// <summary>The folder a button stands in: one of the mod's in a folder this version has; -1 for the rest.</summary>
        public static int FolderOf(int kind, int place)
        {
            return kind == ModItem && place >= 0 && place < FolderCount ? place : -1;
        }

        /// <summary>
        /// Whether an app may be moved from one place to another: to a place this version has that it is not in already, and
        /// into the dock only while the dock has room (<paramref name="docked"/>: the apps in it now on the phone).
        /// </summary>
        public static bool CanMove(int from, int to, int docked)
        {
            return Known(to) && to != from && (to != Dock || docked < DockMost);
        }

        /// <summary>
        /// The number on each folder's tile: the sum of its apps' own numbers, as a real phone adds them up (the game's badge
        /// shows 99+ past 99). <paramref name="sums"/> is given one per folder.
        /// </summary>
        public static void FolderBadges(int[] kind, int[] place, int[] badge, int count, int[] sums)
        {
            if (sums == null)
            {
                return;
            }
            for (int f = 0; f < sums.Length; f++)
            {
                sums[f] = 0;
            }
            if (kind == null || place == null || badge == null)
            {
                return;
            }
            for (int i = 0; i < count && i < kind.Length && i < place.Length && i < badge.Length; i++)
            {
                int f = FolderOf(kind[i], place[i]);
                if (f >= 0 && f < sums.Length && badge[i] > 0)
                {
                    sums[f] = sums[f] > int.MaxValue - badge[i] ? int.MaxValue : sums[f] + badge[i];
                }
            }
        }
    }
}
