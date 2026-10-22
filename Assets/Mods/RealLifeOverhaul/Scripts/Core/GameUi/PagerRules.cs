namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// Which app stands on which page of the phone's apps (PhoneGridPager), and where the button that turns the pages
    /// stands. A page is a set number of tiles: eight on the HUD phone's grid; on the full menu's row as many as fit from
    /// where the game's row begins to the money's box (RightData, 1350 wide: as wide as the money and date can be), between
    /// two and the game's own row, worked out again only when the bar's width changes, so the same whatever the money, the
    /// date or the language: six on a 16:9 screen at 100%, five at 16:10, two at 4:3. A full page stands centred there.
    ///
    /// The game's apps come first in its order, then the mod's in one fixed order (<see cref="ModOrder"/>), then other
    /// mods' as they stand (Arrange). With more than one page, every page's last tile is the More apps button, the pages
    /// fill in order and only the last may be shorter; the row keeps a full page's left edge, so the button never moves.
    /// The button goes to the next page and round; the wheel stops at either end.
    ///
    /// Plain arithmetic: game-free, nothing kept or allocated, called only when the pages are worked out. Checked offline
    /// (ContractCheck PagerChecks).
    /// </summary>
    public static class PagerRules
    {
        public const int PhoneTiles = 8;     // two wide, and exactly four rows fit above the phone's lower edge (cells of 250 by 225 in a box 868 high)
        public const int GameRowApps = 7;    // the full menu's row when the game's own apps cannot be counted: the game's seven (Build 3682)
        public const int LeastTiles = 2;     // one app and the button: the fewest a page can have

        /// <summary>The tiles on a page of the full menu's row: the game's own apps there; seven when fewer than <see cref="LeastTiles"/> were counted.</summary>
        public static int StripTiles(int ownApps)
        {
            return ownApps >= LeastTiles ? ownApps : GameRowApps;
        }

        public const float WidthTolerance = 0.5f;   // canvas pixels: a smaller change of the bar's width is no change of screen

        /// <summary>
        /// The tiles for this much room (from where the game's row begins to the money's box): as many as fit, between
        /// <see cref="LeastTiles"/> and the game's own row (<see cref="StripTiles(int)"/>).
        /// </summary>
        public static int StripTiles(float room, float pitch, float spacing, int ownApps)
        {
            int most = StripTiles(ownApps);
            int fit = Fit(room, pitch, spacing);
            return fit < LeastTiles ? LeastTiles : fit > most ? most : fit;
        }

        /// <summary>
        /// Where a full page begins: centred in the room from <paramref name="rowStart"/> to the money's box, as the game
        /// centres its row; at the room's start when the page is as wide or wider, or the room is not known yet. Every page
        /// starts there, so the button never moves.
        /// </summary>
        public static float PageStart(float rowStart, float room, int tiles, float pitch, float spacing)
        {
            float spare = room - RowWidth(tiles, pitch, spacing);
            return spare > 0f ? rowStart + spare * 0.5f : rowStart;   // NaN is no spare
        }

        /// <summary>The row's tiles are to be worked out again: the first time (NaN measured) and when the bar's width changed.</summary>
        public static bool Remeasure(float barWidth, float measuredFor)
        {
            return !(System.Math.Abs(barWidth - measuredFor) <= WidthTolerance);
        }

        /// <summary>
        /// The game's items (<paramref name="game"/> true) first, then the others, each in the order they stand, their places
        /// written into <paramref name="order"/>. Returns how many are the game's. Stable.
        /// </summary>
        public static int GameFirst(bool[] game, int count, int[] order)
        {
            if (game == null || order == null || count <= 0)
            {
                return 0;
            }
            if (count > game.Length || count > order.Length)
            {
                count = game.Length < order.Length ? game.Length : order.Length;
            }
            int at = 0;
            for (int i = 0; i < count; i++)
            {
                if (game[i])
                {
                    order[at++] = i;
                }
            }
            int games = at;
            for (int i = 0; i < count; i++)
            {
                if (!game[i])
                {
                    order[at++] = i;
                }
            }
            return games;
        }

        /// <summary>
        /// The mod's apps in the one order they stand on both phones, whichever feature put its app on first: the day (Planner,
        /// Flare, the camera, the paper), work and study, money, business, then the Guide and the options. The same groups are
        /// the home screen's folders (PhoneLayoutRules.Defaults).
        /// </summary>
        public static readonly string[] ModOrder =
        {
            "RealLifePlanner", "RealLifeFlare", "RealLifeCalendar", "RealLifeCamera", "RealLifeGallery", "RealLifeNewsstand", "RealLifeLedger",
            "RealLifeCareer", "RealLifeCampus", "RealLifeOpenRoles",
            "RealLifeFinance", "RealLifeBanks", "RealLifeOwnBank", "RealLifeStocks",
            "RealLifeCompany", "RealLifeBrands", "RealLifeServices", "RealLifeVentures", "RealLifeOnline", "RealLifeEstate", "RealLifeDealer",
            "RealLifeTechSector", "RealLifeOverhaulUtilities",
            "RealLifeGuide", "RealLifeSettings"
        };

        private const int RankStep = 100;   // places kept under each name, room to put a name between two without moving the rest

        /// <summary>
        /// A button's place among the mod's apps: its <see cref="ModOrder"/> index times <see cref="RankStep"/>; -1 for a name
        /// that is not one of the mod's (another mod's button). Ordinal comparisons, no allocation.
        /// </summary>
        public static int ModRank(string name)
        {
            int at = IndexOf(name);
            return at >= 0 ? at * RankStep : -1;
        }

        /// <summary>The place of a name in <see cref="ModOrder"/>, from 0; -1 for none.</summary>
        public static int IndexOf(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return -1;
            }
            for (int i = 0; i < ModOrder.Length; i++)
            {
                if (string.Equals(name, ModOrder[i], System.StringComparison.Ordinal))
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// The game's items first as they stand, then the mod's (<paramref name="rank"/> 0 or more, from <see cref="ModRank"/>)
        /// by rank, equal ranks as they stand, then the rest (rank under 0) as they stand; places written into
        /// <paramref name="order"/>. Returns how many are the game's. Stable whatever order the items were put on in; no
        /// allocation.
        /// </summary>
        public static int Arrange(bool[] game, int[] rank, int count, int[] order)
        {
            if (rank == null)
            {
                return GameFirst(game, count, order);
            }
            if (game == null || order == null || count <= 0)
            {
                return 0;
            }
            if (count > game.Length || count > order.Length || count > rank.Length)
            {
                count = System.Math.Min(game.Length, System.Math.Min(order.Length, rank.Length));
            }
            int at = 0;
            for (int i = 0; i < count; i++)
            {
                if (game[i])
                {
                    order[at++] = i;
                }
            }
            int games = at;
            // The mod's: each next is the least rank not yet placed, the first standing of equal ones.
            int last = -1, lastAt = -1;
            while (true)
            {
                int best = -1;
                for (int i = 0; i < count; i++)
                {
                    if (game[i] || rank[i] < 0)
                    {
                        continue;
                    }
                    bool after = rank[i] > last || (rank[i] == last && i > lastAt);
                    if (after && (best < 0 || rank[i] < rank[best]))
                    {
                        best = i;
                    }
                }
                if (best < 0)
                {
                    break;
                }
                order[at++] = best;
                last = rank[best];
                lastAt = best;
            }
            for (int i = 0; i < count; i++)
            {
                if (!game[i] && rank[i] < 0)
                {
                    order[at++] = i;
                }
            }
            return games;
        }

        /// <summary>A row of this many tiles: each tile's pitch (its width and the gap after it), less the last gap.</summary>
        public static float RowWidth(int tiles, float pitch, float spacing)
        {
            return tiles > 0 ? tiles * pitch - spacing : 0f;
        }

        /// <summary>How many tiles of this pitch fit in this room (none in a room of no width).</summary>
        public static int Fit(float room, float pitch, float spacing)
        {
            if (!(pitch > 0f) || !(room > 0f))
            {
                return 0;
            }
            double fit = System.Math.Floor((room + spacing) / pitch + 1e-4);   // a hair over, so a row that fills the room exactly fits it
            return fit > 100000.0 ? 100000 : (int)fit;   // a room with no end holds a great many, not an overflow
        }

        /// <summary>Pages for this many apps on pages of this many tiles; 1 when they all fit.</summary>
        public static int Pages(int count, int tiles)
        {
            tiles = Tiles(tiles);
            if (count <= tiles)
            {
                return 1;
            }
            int each = tiles - 1;
            return (count + each - 1) / each;
        }

        /// <summary>Apps on a full page: every tile with one page, every tile but the button's with more.</summary>
        public static int PerPage(int count, int tiles)
        {
            tiles = Tiles(tiles);
            return count <= tiles ? tiles : tiles - 1;
        }

        /// <summary>The page the app at this place (from 0) is on.</summary>
        public static int PageOf(int index, int count, int tiles)
        {
            if (index <= 0)
            {
                return 0;
            }
            int page = index / PerPage(count, tiles);
            int last = Pages(count, tiles) - 1;
            return page < last ? page : last;
        }

        /// <summary>Apps on this page (from 0): a full page, the rest on the last, none past it.</summary>
        public static int PageSize(int page, int count, int tiles)
        {
            if (page < 0 || count <= 0)
            {
                return 0;
            }
            int each = PerPage(count, tiles);
            int left = count - page * each;
            return left <= 0 ? 0 : left < each ? left : each;
        }

        /// <summary>The tile (from 0) an app stands on in its page.</summary>
        public static int TileOf(int index, int count, int tiles)
        {
            return index < 0 ? 0 : index - PageOf(index, count, tiles) * PerPage(count, tiles);
        }

        /// <summary>The button's tile (from 0): the last of every page.</summary>
        public static int ButtonTile(int tiles)
        {
            return Tiles(tiles) - 1;
        }

        /// <summary>The button shows only with more than one page.</summary>
        public static bool ButtonShown(int pages)
        {
            return pages > 1;
        }

        /// <summary>The button's page: the next, and from the last back to the first.</summary>
        public static int Next(int page, int pages)
        {
            if (pages <= 1)
            {
                return 0;
            }
            int next = page + 1;
            return next >= pages || next < 0 ? 0 : next;
        }

        /// <summary>The wheel's page: one on or back, stopping at either end.</summary>
        public static int Wheel(int page, int pages, bool down)
        {
            int to = page + (down ? 1 : -1);
            return to < 0 ? 0 : to > pages - 1 ? (pages > 0 ? pages - 1 : 0) : to;
        }

        /// <summary>The page counted from 1 ("2" of "3"), for the text's {page} and {pages}.</summary>
        public static void Shown(int page, int pages, out int shown, out int of)
        {
            of = pages > 1 ? pages : 1;
            shown = page < 0 ? 1 : page >= of ? of : page + 1;
        }

        private static int Tiles(int tiles)
        {
            return tiles < LeastTiles ? LeastTiles : tiles;
        }
    }
}
