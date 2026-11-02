using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The status bar's app row, the way between apps while the full menu shows: the phone's first home page in the
    /// player's order as glyph tiles (the open app's tile in the chosen row's tone, its news counted on a tile's corner),
    /// then the All apps tile, counting the news of the apps not in the row. Its targets belong to the frame, not the page
    /// (UiHost hands them to its Chrome, as it does the window's close), so their commands sit in a range no screen uses.
    /// Game-free. Cost: a few boxes a layout.
    /// </summary>
    public static partial class UiDraw
    {
        /// <summary>An app's tile in the row: arg is its index in UiStatusModel.Apps.</summary>
        public const int AppRowCommand = 9010;

        /// <summary>The All apps tile at the row's end, and anything that opens the All apps panel.</summary>
        public const int AllAppsCommand = 9011;

        private const int ChromeFirst = 9010, ChromeLast = 9019;   // the frame's own commands: the app row, All apps and what may join them
        private const int RowMost = 10;                            // tiles before All apps: the phone's first page is eight, with room for a long one

        /// <summary>
        /// News waiting on a tile (an app, a folder): a small cyan dot at its top right corner, clear of the glyph, set in a
        /// ring of what stands round the tile so it reads on a slate tile and beside the chosen cyan alike. One mark for
        /// news on every tile; a list row counts it in a badge.
        /// </summary>
        public static void NewsDot(UiScene s, UiAt on, float right, float top, UiRole ring, UiFlags flags = UiFlags.None)
        {
            const float outer = UiTokens.S4, inner = UiTokens.S2;   // 16 px with its ring, 8 px the dot
            float x = right - outer / 2f, y = top - outer / 2f;   // centred on the corner: past the glyph's box but for a hair
            int r = Circle(s, on, x, y, outer, outer, ring, UiFlags.Overflow | flags);
            // The accent's cyan, never a near white: nothing on the bar glares (the owner, 1 Nov), and it reads 3:1 on slate and the bar.
            Circle(s, on.In(r), x + (outer - inner) / 2f, y + (outer - inner) / 2f, inner, inner, UiRole.Accent, UiFlags.Overflow | flags);
        }

        /// <summary>True for a command of the frame's (the app row, All apps), which the host hands to its Chrome.</summary>
        public static bool IsChrome(int command) => command >= ChromeFirst && command <= ChromeLast;

        /// <summary>
        /// How many of the model's apps the row shows: every one it is given (the phone's first page, at most ten before All
        /// apps), fewer only while the pills would not stand whole beside them on the strip (<see cref="PlanPills"/>).
        /// </summary>
        public static int AppsShown(UiStatusModel m, float width) => PlanPills(m, width).Apps;

        /// <summary>px: an app's tile on the status bar, big as a game's dock and as tall as the pills, its glyph <see cref="BarGlyph"/> on it.</summary>
        public const float BarTile = UiFrame.Toolbar;

        /// <summary>px: a glyph on a tile of the status bar, an app's or a build tool's, chunky as a game's.</summary>
        public const float BarGlyph = 36f;

        private const int RowLeast = 4;       // apps the row keeps however narrow the strip; the rest wait under All apps
        private const byte RowRegion = 240;   // the d-pad's column of its own: Left and Right walk the row, Up leaves it

        /// <summary>The status bar's centre line, which the row, the pills and the prompts all stand on.</summary>
        public static float BarMiddle(UiFrame frame) => frame.BandY + frame.Band / 2f;

        // The apps the row would show with room enough: a row that ends with All apps is the phone's first page; one without
        // shows what it is given.
        private static int RowApps(UiStatusModel m) => m == null || m.Apps == null ? 0 : m.AllApps ? Math.Min(m.Apps.Length, RowMost) : m.Apps.Length;

        // The row's width with a number of its apps, All apps after them.
        private static float RowWidth(UiStatusModel m, int apps)
        {
            int tiles = apps + (m != null && m.AllApps ? 1 : 0);
            return tiles == 0 ? 0f : tiles * BarTile + (tiles - 1) * TileGap;
        }

        // The row, centred on the strip and on its centre line. Pressable only where the frame answers it (the full menu and the
        // HUD); elsewhere a tile is a picture of where the player is. Its tiles are linked left and right, the All apps tile
        // last, so the d-pad walks the whole row and never jumps up into the page on its way.
        private static void AppRow(UiScene s, UiAt on, float mid, ref UiPillPlan p, UiStatusModel m)
        {
            if (p.RowW <= 0f)
            {
                return;
            }
            UiAt row = on.InRegion(RowRegion);
            float tile = BarTile, ax = p.RowX, ay = mid - tile / 2f;
            int first = s.NodeCount;
            for (int i = 0; i < p.Apps; i++)
            {
                UiRole tint = m.AppTints != null && i < m.AppTints.Length ? m.AppTints[i] : UiRole.Text2;
                int badge = m.AppBadges != null && i < m.AppBadges.Length ? m.AppBadges[i] : 0;
                int picture = m.AppPictures != null && i < m.AppPictures.Length ? m.AppPictures[i] : 0;
                AppTile(s, row, ax, ay, tile, m.Apps[i], tint, i == m.App, badge, m.AppsPress ? AppRowCommand : -1, i, picture);
                ax += tile + TileGap;
            }
            if (m.AllApps)
            {
                int rest = m.AllAppsBadge;
                for (int i = p.Apps; m.AppBadges != null && i < m.AppBadges.Length && i < m.Apps.Length; i++)
                {
                    rest += m.AppBadges[i];   // the apps the row leaves out tell their news on All apps
                }
                AppTile(s, row, ax, ay, tile, UiIcon.Grid, UiIconTints.Of(UiIcon.Grid), m.AllAppsOpen, rest, m.AppsPress ? AllAppsCommand : -1, 0);
            }
            for (int n = first; n < s.NodeCount; n++)
            {
                s.Nodes[n].Left = n > first ? n - 1 : -1;
                s.Nodes[n].Right = n < s.NodeCount - 1 ? n + 1 : -1;
            }
        }

        // One tile, in the pills' slate: the glyph in its colour, the open app's tile in the chosen row's tone (its glyph keeps
        // its colour there), its news counted on a dot at its top right corner, standing over the corner onto the bar.
        // Centred on the strip's line, not on a grid step.
        private static void AppTile(UiScene s, UiAt on, float x, float y, float tile, UiIcon icon, UiRole tint, bool chosen, int badge, int command, int arg, int picture = 0)
        {
            int face = Box(s, on, x, y, tile, tile, chosen ? UiRole.RowSel : UiRole.Pill, UiRole.None, UiTokens.RadiusLarge,
                UiFlags.Chrome | UiFlags.Middle | (chosen ? UiFlags.Selected : UiFlags.None));
            float g = BarGlyph;
            if (picture > 0)
            {
                Picture(s, on.In(face), x + (tile - g) / 2f, y + (tile - g) / 2f, g, picture);   // another mod's app: its own picture, as on the phone
                s.Items[s.ItemCount - 1].Flags |= UiFlags.Chrome;
            }
            else
            {
                Icon(s, on.In(face), x + (tile - g) / 2f, y + (tile - g) / 2f, g, icon, tint, UiFlags.Middle | UiFlags.Chrome, true);
            }
            if (badge > 0)
            {
                CountDot(s, on.In(face), x + tile + UiTokens.S1, y - UiTokens.S1, badge, UiRole.StatusBar);   // UiDraw.HudBar.cs
            }
            if (command >= 0)
            {
                Node(s, on, x, y, tile, tile, command, arg, face);
            }
        }
    }
}
