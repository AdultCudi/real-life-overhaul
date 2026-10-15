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
    /// on a 16:9 screen at 100%, five at 16:10, two at 4:3.
    ///
    /// The game's own apps come first, in the game's order, then everyone else's in the order they stand (GameFirst).
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
