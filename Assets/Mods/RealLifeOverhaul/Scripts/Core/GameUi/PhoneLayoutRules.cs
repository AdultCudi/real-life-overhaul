namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// The phone's home screen as a real phone has it (PhoneGridPager): the mod's apps in five folders, a dock of two along
    /// the foot of the HUD phone, and the game's and other mods' apps where they always stood. Each app of
    /// <see cref="PagerRules.ModOrder"/> is in its group's folder unless the player moved it (<see cref="PhoneLayout"/>).
    ///
    /// The home screen holds in order: the game's apps; on the full menu's row the docked apps (the HUD phone has its dock
    /// row); a tile per folder with an app in it; the mod's apps put on the home screen; other mods' buttons. An open
    /// folder shows its apps after its Back tile. Every button is in exactly one place: one the layout puts nowhere this
    /// version has (an unknown folder, a third app in the dock) stands on the home screen.
    ///
    /// Game-free arithmetic and ordinal comparisons, nothing allocated; used only when the grid is shown or changed.
    /// Checked offline (ContractCheck PhoneLayoutChecks).
    /// </summary>
    public static class PhoneLayoutRules
    {
        public const int Money = 0, Work = 1, Life = 2, Business = 3, Setup = 4;   // the folders, in the order they stand
        public const int FolderCount = 5;
        public const int Home = -1;      // a place: on the home screen by itself
        public const int Dock = -2;      // a place: in the dock
        public const int DockMost = 2;   // apps in the dock: the HUD grid is two wide, and the dock is its last row
        public const int MiniMost = 9;   // small pictures on a folder's tile: three rows of three
        public const int MiniRow = 3;    // ...a row of them
        public const int LeastStrip = 3; // tiles a folder's page needs on the full menu's row: Back, an app and More apps

        public const int GameItem = 0, ModItem = 1, OtherItem = 2;   // what a button of the grid is: the game's, the mod's, another mod's

        /// <summary>Each folder's name in the saved layout: never translated or changed.</summary>
        public static readonly string[] FolderIds = { "money", "work", "life", "business", "setup" };

        /// <summary>Each folder's name on its tile and its Back tile.</summary>
        public static readonly string[] FolderKeys =
        {
            "reallife_phone_folder_money", "reallife_phone_folder_work", "reallife_phone_folder_life", "reallife_phone_folder_business", "reallife_phone_folder_setup"
        };

        /// <summary>
        /// Where each app of <see cref="PagerRules.ModOrder"/> stands until the player moves it, name for name: the Planner and
        /// Flare in the dock, the rest in their group's folder. The Calendar is in Social and life, the month of the player's
        /// life beside the Planner's dock; Money holds the apps that move money. The City Ledger's page has no button: its place
        /// is kept for the table's sake.
        /// </summary>
        public static readonly int[] Defaults =
        {
            Dock, Dock, Life, Life, Life, Money, Money,
            Work, Work, Work,
            Money, Money, Money, Money,
            Business, Business, Business, Business, Business, Business, Business,
            Business, Money,
            Setup, Setup
        };

        /// <summary>Where a mod app stands while the player has not moved it; the home screen for a name not in the table.</summary>
        public static int DefaultOf(string id)
        {
            int at = PagerRules.IndexOf(id);
            return at >= 0 && at < Defaults.Length ? Defaults[at] : Home;
        }

        /// <summary>A place this version has: a folder, the home screen or the dock.</summary>
        public static bool Known(int place)
        {
            return place == Home || place == Dock || place >= 0 && place < FolderCount;
        }

        /// <summary>A place as the saved layout writes it: a folder's id, "home" or "dock".</summary>
        public static string Token(int place)
        {
            return place == Dock ? "dock" : place >= 0 && place < FolderCount ? FolderIds[place] : "home";
        }

        /// <summary>A place from the saved layout's word; false for a word this version does not know (a later one's folder).</summary>
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

        /// <summary>Folders fit a row of this many tiles: the HUD phone's always, the full menu's from three (a 4:3 screen has two).</summary>
        public static bool FoldersFit(bool strip, int tiles)
        {
            return !strip || tiles >= LeastStrip;
        }

        /// <summary>
        /// The tiles of a page that hold the view's items: the HUD phone's eight less its dock row on the home screen while the
        /// dock has an app, less the Back tile on a folder's page; the full menu's row less the Back tile. With pages, the More
        /// apps button takes the last of them.
        /// </summary>
        public static int ItemTiles(bool strip, int tiles, bool folderOpen, bool dockRow)
        {
            int all = strip ? tiles : PagerRules.PhoneTiles;
            int held = all - (folderOpen ? 1 : 0) - (!strip && !folderOpen && dockRow ? DockMost : 0);
            return held < PagerRules.LeastTiles ? PagerRules.LeastTiles : held;
        }

        /// <summary>The tile of the whole page (from 0) the More apps button stands on: the last item tile, after the Back tile on a folder's page.</summary>
        public static int MoreTile(bool strip, int tiles, bool folderOpen, bool dockRow)
        {
            return (folderOpen ? 1 : 0) + PagerRules.ButtonTile(ItemTiles(strip, tiles, folderOpen, dockRow));
        }

        /// <summary>
        /// The empty tiles before the HUD phone's dock row, so the dock stands on the last row: every item tile not taken, the
        /// More apps button's too (it stands beside the grid). None without a dock row.
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
        /// A grid's view: what its pages hold (<paramref name="view"/>: a button's index, or -1 - folder for a folder's tile) and
        /// its dock row, the buttons in the pages' order (PagerRules.Arrange). A <paramref name="folder"/> of 0 or more: its
        /// apps; else the home screen (see the class), with the dock's first two in <paramref name="dock"/> on the HUD phone.
        /// Each folder's count goes in <paramref name="members"/>. Returns the view's length.
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
            // The dock's first two in the mod's order; on the full menu's row after the game's own.
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

        /// <summary>The folder a button stands in: a mod app in a folder this version has; -1 for the rest.</summary>
        public static int FolderOf(int kind, int place)
        {
            return kind == ModItem && place >= 0 && place < FolderCount ? place : -1;
        }

        /// <summary>
        /// An app may move: to a place this version has that it is not in, and into the dock only while it has room
        /// (<paramref name="docked"/>: the apps in it now).
        /// </summary>
        public static bool CanMove(int from, int to, int docked)
        {
            return Known(to) && to != from && (to != Dock || docked < DockMost);
        }

        /// <summary>
        /// Each folder's number: the sum of its apps' numbers, as a real phone adds them (the game's badge shows 99+ past 99).
        /// <paramref name="sums"/> holds one per folder.
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
