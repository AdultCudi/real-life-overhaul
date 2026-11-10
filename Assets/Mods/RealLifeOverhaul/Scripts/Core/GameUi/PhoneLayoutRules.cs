namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// The phone's home screen as a real phone has it: the mod's apps in five folders, two the player keeps first on the
    /// home screen (the Planner and Flare until moved; the place is still saved as "dock"), the game's and other mods' apps
    /// where they always stood; every button in exactly one place, one the layout puts nowhere this version has on the home
    /// screen. Game-free; checked offline (ContractCheck PhoneLayoutChecks).
    /// </summary>
    public static class PhoneLayoutRules
    {
        public const int Money = 0, Work = 1, Life = 2, Business = 3, Setup = 4;   // the folders, in the order they stand
        public const int FolderCount = 5;
        public const int Home = -1;      // a place: on the home screen by itself
        public const int Dock = -2;      // a place: first on the home screen (saved as "dock", the name it had as a row of its own)
        public const int DockMost = 2;   // apps kept first: the HUD grid's first row
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

        /// <summary>Where each app of <see cref="PagerRules.ModOrder"/> stands until moved: the Planner and Flare first on the home screen, the rest in their group's folder.</summary>
        public static readonly int[] Defaults =
        {
            Dock, Dock, Life, Life, Life, Money, Money,
            Work, Work, Work, Work,
            Money, Money, Money, Money, Money, Money,
            Business, Business, Business, Business, Business, Business, Business,
            Business,
            Business, Business, Money,
            Setup, Setup, Setup
        };

        /// <summary>Where a mod app stands while the player has not moved it; the home screen for a name not in the table.</summary>
        public static int DefaultOf(string id)
        {
            int at = PagerRules.IndexOf(id);
            return at >= 0 && at < Defaults.Length ? Defaults[at] : Home;
        }

        /// <summary>A place this version has: a folder, the home screen or first on it.</summary>
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

        /// <summary>The tiles of a page for the view's items: all but the Back tile on a folder's page.</summary>
        public static int ItemTiles(bool strip, int tiles, bool folderOpen)
        {
            int all = strip ? tiles : PagerRules.PhoneTiles;
            int held = all - (folderOpen ? 1 : 0);
            return held < PagerRules.LeastTiles ? PagerRules.LeastTiles : held;
        }

        /// <summary>
        /// The tile of the whole page (from 0) the More apps button stands on: the page's last, on the home screen and in
        /// every folder alike, so it never moves (the owner, 1 Nov 2026: "The next button must be at the bottom").
        /// </summary>
        public static int MoreTile(bool strip, int tiles)
        {
            return (strip ? (tiles < PagerRules.LeastTiles ? PagerRules.LeastTiles : tiles) : PagerRules.PhoneTiles) - 1;
        }

        /// <summary>
        /// The empty tiles after a short page's items, so its More apps button still stands on the last tile; none on a
        /// view of one page, which has no button.
        /// </summary>
        public static int Gaps(int onPage, int itemTiles, int pages)
        {
            if (pages <= 1)
            {
                return 0;
            }
            int gaps = itemTiles - 1 - (onPage < 0 ? 0 : onPage);
            return gaps < 0 ? 0 : gaps;
        }

        /// <summary>
        /// A grid's view (a button's index, or -1 - folder for a folder's tile) and each folder's count; returns the view's
        /// length. On the home screen the apps the player keeps first (the old dock's two) lead on the HUD phone
        /// (<paramref name="keptFirst"/>) and follow the game's own on the full menu's row, whose first apps are the game's.
        /// </summary>
        public static int View(int[] kind, int[] place, int count, int folder, bool keptFirst, int[] view, int[] members)
        {
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
            if (keptFirst)
            {
                at = First(kind, place, count, view, at);
            }
            for (int i = 0; i < count && at < view.Length; i++)
            {
                if (kind[i] == GameItem)
                {
                    view[at++] = i;
                }
            }
            if (!keptFirst)
            {
                at = First(kind, place, count, view, at);
            }
            for (int f = 0; f < FolderCount && at < view.Length; f++)
            {
                if (members != null && f < members.Length && members[f] > 0)
                {
                    view[at++] = -1 - f;
                }
            }
            // The home screen's own: those put there, and any the layout puts nowhere this version has (a third kept first).
            int kept = 0;
            for (int i = 0; i < count && at < view.Length; i++)
            {
                if (kind[i] != ModItem)
                {
                    continue;
                }
                int p = place[i];
                if (p == Dock)
                {
                    if (kept++ < DockMost)
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

        // The apps kept first, at most two, in the mod's order.
        private static int First(int[] kind, int[] place, int count, int[] view, int at)
        {
            int kept = 0;
            for (int i = 0; i < count && at < view.Length && kept < DockMost; i++)
            {
                if (kind[i] == ModItem && place[i] == Dock)
                {
                    view[at++] = i;
                    kept++;
                }
            }
            return at;
        }

        /// <summary>The folder a button stands in: a mod app in a folder this version has; -1 for the rest.</summary>
        public static int FolderOf(int kind, int place)
        {
            return kind == ModItem && place >= 0 && place < FolderCount ? place : -1;
        }

        /// <summary>An app may move: to another place this version has, and first on the home screen only while two are not there already.</summary>
        public static bool CanMove(int from, int to, int docked)
        {
            return Known(to) && to != from && (to != Dock || docked < DockMost);
        }

        /// <summary>Each folder's number: the sum of its apps' numbers, as a real phone adds them.</summary>
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
