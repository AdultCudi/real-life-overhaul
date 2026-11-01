using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The status bar's app row, the way between apps while the full menu shows: the phone's first home page in the
    /// player's order as glyph tiles (the open app filled cyan, a badge on a tile whose app has news), then the All apps
    /// tile, carrying the badges of the apps not in the row. Its targets belong to the frame, not the page (UiHost hands
    /// them to its Chrome, as it does the window's close), so their commands sit in a range no screen uses. Game-free.
    /// Cost: a few boxes a layout.
    /// </summary>
    public static partial class UiDraw
    {
        /// <summary>An app's tile in the row: arg is its index in UiStatusModel.Apps.</summary>
        public const int AppRowCommand = 9010;

        /// <summary>The All apps tile at the row's end, and anything that opens the All apps panel.</summary>
        public const int AllAppsCommand = 9011;

        private const int ChromeFirst = 9010, ChromeLast = 9019;   // the frame's own commands: the app row, All apps and what may join them
        private const int RowMost = 10;                            // tiles before All apps: the phone's first page is eight, with room for a long one

        /// <summary>True for a command of the frame's (the app row, All apps), which the host hands to its Chrome.</summary>
        public static bool IsChrome(int command) => command >= ChromeFirst && command <= ChromeLast;

        /// <summary>How many of the model's apps the row shows: those that fit beside the keys' prompts, at most ten.</summary>
        public static int AppsShown(UiStatusModel m, float width)
        {
            if (m == null || m.Apps == null)
            {
                return 0;
            }
            float tile = UiTokens.IconTile + TileGap;
            int room = (int)((width - UiTokens.S16 * 2f - PromptRoom * 2f) / tile) - (m.AllApps ? 1 : 0);
            int most = m.AllApps ? RowMost : m.Apps.Length;   // a row that ends with All apps is the phone's first page; one without shows what it is given
            return Math.Max(0, Math.Min(Math.Min(m.Apps.Length, most), room));
        }

        // px the keys' prompts may take at the bar's right while the keys lead; the row stays centred clear of them on both sides.
        private const float PromptRoom = 520f;

        // The row, centred in the bar's upper row. Pressable only where the frame answers it (the full menu); elsewhere a
        // tile is a picture of where the player is.
        private static void AppRow(UiScene s, UiAt on, float y, float w, UiStatusModel m)
        {
            int shown = AppsShown(m, w);
            if (shown == 0 && !m.AllApps)
            {
                return;
            }
            float tile = UiTokens.IconTile;
            int count = shown + (m.AllApps ? 1 : 0);
            float total = count * tile + (count - 1) * TileGap;
            float ax = SnapDown((w - total) / 2f), ay = y + (UiFrame.Toolbar - tile) / 2f;
            for (int i = 0; i < shown; i++)
            {
                UiRole tint = m.AppTints != null && i < m.AppTints.Length ? m.AppTints[i] : UiRole.Text2;
                int badge = m.AppBadges != null && i < m.AppBadges.Length ? m.AppBadges[i] : 0;
                AppTile(s, on, ax, ay, tile, m.Apps[i], tint, i == m.App, badge, m.AppsPress ? AppRowCommand : -1, i);
                ax += tile + TileGap;
            }
            if (m.AllApps)
            {
                int rest = m.AllAppsBadge;
                for (int i = shown; m.AppBadges != null && i < m.AppBadges.Length && i < m.Apps.Length; i++)
                {
                    rest += m.AppBadges[i];   // the apps the row leaves out tell their news on All apps
                }
                AppTile(s, on, ax, ay, tile, UiIcon.Grid, UiIconTints.Of(UiIcon.Grid), m.AllAppsOpen, rest, m.AppsPress ? AllAppsCommand : -1, 0);
            }
        }

        // One tile: the glyph in its colour on slate (dark on the cyan when chosen), its badge at the top right corner.
        private static void AppTile(UiScene s, UiAt on, float x, float y, float tile, UiIcon icon, UiRole tint, bool chosen, int badge, int command, int arg)
        {
            int face = Tile(s, on, x, y, tile, tint, chosen);
            s.Items[face].Flags |= UiFlags.Chrome;
            float g = UiTokens.S8;   // 32 px: a tool's glyph, chunky as a game's, on the grid in its 40 px tile
            Icon(s, on.In(face), x + (tile - g) / 2f, y + (tile - g) / 2f, g, icon, chosen ? UiRole.OnAccent : tint, UiFlags.Middle | UiFlags.Chrome, true);
            if (badge > 0)
            {
                // On the tile's corner as the phone wears it, a step out, still inside the bar and short of the next tile.
                Badge(s, on.In(face), x + tile + UiTokens.S1, y - UiTokens.S1, badge, !chosen);   // on the chosen tile's cyan a cyan badge would not show
                s.Items[s.ItemCount - 1].Flags |= UiFlags.Overflow | UiFlags.Chrome;
                s.Items[s.ItemCount - 2].Flags |= UiFlags.Overflow | UiFlags.Chrome;
            }
            if (command >= 0)
            {
                Node(s, on, x, y, tile, tile, command, arg, face);
            }
        }
    }
}
