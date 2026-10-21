namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// Which app stands on which page of the phone's apps (PhoneGridPager), and where the button that turns the pages
    /// stands. A page is a set number of tiles. The HUD phone's grid has eight (two wide, four rows above the phone's
    /// lower edge). The full menu's row has as many as fit, once for each screen, from where the game's own row begins
    /// to the left edge of the box the game keeps for the money, the date and the time (RightData, 1350 wide, ending 100
    /// from the bar's right edge: as wide as they can ever be), never fewer than two and never more than the game's own
    /// row. It is worked out at the first opening and again only when the bar's width changes (the resolution, the
    /// screen's shape, the interface's scale), so it is the same at every opening on a screen, whatever the money, the
    /// date or the language (those were measured each opening before, and the row's length changed with them): six tiles
    /// on a 16:9 screen at 100%, five at 16:10, two at 4:3. A full page stands centred in that room (PageStart).
    ///
    /// The game's own apps come first, in the game's order, then the mod's in one fixed order (<see cref="ModOrder"/>,
    /// the same at every opening, load and start, whichever feature put its app on first), then other mods' in the order
    /// they stand (Arrange).
    ///
    /// When every app fits on one page there are no pages and no button. Otherwise the last tile of every page is the
    /// button ("More apps"), so a page holds one app fewer than it has tiles; the pages fill in order and only the last
    /// may be shorter. The row stands where a full page stands (the same left edge), so the button never moves. The
    /// button goes to the next page, and from the last back to the first; the wheel turns the page and stops at either
    /// end, as before.
    ///
    /// Plain arithmetic: no Unity or game types, nothing kept, nothing allocated. Called only when the pages are worked
    /// out (the grid shown or changed, a page turned); checked offline (ContractCheck, PagerChecks).
    /// </summary>
    public static class PagerRules
    {
        public const int PhoneTiles = 8;     // tiles on a page of the HUD phone's grid: two wide, and exactly four rows fit above the phone's lower edge (cells of 250 by 225 in a box 868 high)
        public const int GameRowApps = 7;    // tiles on a page of the full menu's row when the game's own apps cannot be counted: the game's seven (Build 3682)
        public const int LeastTiles = 2;     // tiles: one app and the button, the fewest a page can have

        /// <summary>
        /// The tiles on a page of the full menu's row: the game's own apps there, the room their row takes; the game's
        /// seven when fewer than <see cref="LeastTiles"/> could be counted.
        /// </summary>
        public static int StripTiles(int ownApps)
        {
            return ownApps >= LeastTiles ? ownApps : GameRowApps;
        }

        public const float WidthTolerance = 0.5f;   // canvas pixels: a change of the bar's width smaller than this is no change of screen

        /// <summary>
        /// The tiles on a page of the full menu's row for this much room (from where the game's own row begins to the left
        /// edge of the money's box): as many as fit, never fewer than <see cref="LeastTiles"/>, never more than the game's
        /// own row (<see cref="StripTiles(int)"/>). The money, the date and the language are no part of it.
        /// </summary>
        public static int StripTiles(float room, float pitch, float spacing, int ownApps)
        {
            int most = StripTiles(ownApps);
            int fit = Fit(room, pitch, spacing);
            return fit < LeastTiles ? LeastTiles : fit > most ? most : fit;
        }

        /// <summary>
        /// Where a full page of this many tiles begins: centred in the room that begins at <paramref name="rowStart"/> (where
        /// the game's own row begins) and runs to the money's box, as the game centres its own row; at the room's start
        /// when the page is as wide as the room or wider (the least page on a screen with no room for it), or the room is
        /// not known yet. Every page starts there, a short last one too, so the button never moves.
        /// </summary>
        public static float PageStart(float rowStart, float room, int tiles, float pitch, float spacing)
        {
            float spare = room - RowWidth(tiles, pitch, spacing);
            return spare > 0f ? rowStart + spare * 0.5f : rowStart;   // (NaN is no spare)
        }

        /// <summary>Whether the row's tiles are to be worked out again: the first time (nothing measured, NaN), and when the bar's width has changed.</summary>
        public static bool Remeasure(float barWidth, float measuredFor)
        {
            return !(System.Math.Abs(barWidth - measuredFor) <= WidthTolerance);
        }

        /// <summary>
        /// The order the items go on the pages: those of the game (<paramref name="game"/> true) first, in the order they
        /// stand, then the others in the order they stand. Writes the items' places into <paramref name="order"/>; returns
        /// how many of the game's there are. Stable: the same items always come out in the same order.
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
        /// The mod's apps in the one order they stand on both phones: the day (Planner, Flare, the camera, the paper), work
        /// and study, money, business, then the Guide and the options. Each feature puts its app on when it is ready, so the
        /// order they were put on changed from one load to the next; their place is this table's, never that. The same
        /// groups are the home screen's folders (PhoneLayoutRules.Defaults, one place for each name here).
        /// </summary>
        public static readonly string[] ModOrder =
        {
            "RealLifePlanner", "RealLifeFlare", "RealLifeCamera", "RealLifeGallery", "RealLifeNewsstand", "RealLifeLedger",
            "RealLifeCareer", "RealLifeCampus", "RealLifeOpenRoles",
            "RealLifeFinance", "RealLifeBanks", "RealLifeOwnBank", "RealLifeStocks",
            "RealLifeCompany", "RealLifeBrands", "RealLifeServices", "RealLifeVentures", "RealLifeOnline", "RealLifeEstate", "RealLifeDealer",
            "RealLifeTechSector", "RealLifeOverhaulUtilities",
            "RealLifeGuide", "RealLifeSettings"
        };

        private const int RankStep = 100;   // places kept under each name of the table (room for a name put between two later without moving the rest)

        /// <summary>
        /// Where a button of this name stands among the mod's apps (<see cref="ModOrder"/>): its place in the table times
        /// <see cref="RankStep"/>; -1 for a name that is not one of the mod's (another mod's button). No allocation: an
        /// ordinal comparison of the name against the table. (The banks once had an app each, RealLifeBank1 and on: they are
        /// one Banks app now, so no name is read for a number any more.)
        /// </summary>
        public static int ModRank(string name)
        {
            int at = IndexOf(name);
            return at >= 0 ? at * RankStep : -1;
        }

        /// <summary>The place of a name in <see cref="ModOrder"/>, from 0; -1 for none. An ordinal comparison: no allocation.</summary>
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
        /// The order the items go on the pages: those of the game (<paramref name="game"/> true) first, in the order they
        /// stand; then the mod's (<paramref name="rank"/> at 0 or more, from <see cref="ModRank"/>) by their rank, equal ranks
        /// in the order they stand; then the rest (another mod's, rank under 0) in the order they stand. Writes the items'
        /// places into <paramref name="order"/>; returns how many of the game's there are. Stable and the same whatever order
        /// the items were put on in. A pass over a few dozen buttons: no allocation.
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
            // The mod's: each next one is the least rank not yet placed (the first standing of equal ones).
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

        /// <summary>How wide a row of this many tiles is: each tile's pitch (its width and the gap after it), less the last gap.</summary>
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
            double fit = System.Math.Floor((room + spacing) / pitch + 1e-4);   // (a hair over, so a row that fills the room exactly fits it)
            return fit > 100000.0 ? 100000 : (int)fit;   // (a room with no end holds a great many, not an overflow)
        }

        /// <summary>The number of pages for this many apps on pages of this many tiles: 1 when they all fit on one page.</summary>
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

        /// <summary>The apps on a full page: every tile when there is one page, every tile but the button's when there are more.</summary>
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

        /// <summary>The number of apps on this page (from 0): a full page, the rest on the last, none past it.</summary>
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

        /// <summary>The tile (from 0) the button stands on: the last of every page, whatever the page holds.</summary>
        public static int ButtonTile(int tiles)
        {
            return Tiles(tiles) - 1;
        }

        /// <summary>Whether the button shows: only when there is more than one page.</summary>
        public static bool ButtonShown(int pages)
        {
            return pages > 1;
        }

        /// <summary>The page the button goes to: the next, and from the last back to the first.</summary>
        public static int Next(int page, int pages)
        {
            if (pages <= 1)
            {
                return 0;
            }
            int next = page + 1;
            return next >= pages || next < 0 ? 0 : next;
        }

        /// <summary>The page the wheel goes to: one on or back, stopping at either end.</summary>
        public static int Wheel(int page, int pages, bool down)
        {
            int to = page + (down ? 1 : -1);
            return to < 0 ? 0 : to > pages - 1 ? (pages > 0 ? pages - 1 : 0) : to;
        }

        /// <summary>The page as the button shows it, counted from 1 ("2" of "3"): what fills the text's {page} and {pages}.</summary>
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
