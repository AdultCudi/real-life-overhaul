using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// What a side panel shows: its title and glyph, up to five rows (a link moves the camera or opens the thing), a meter or
    /// a timing gauge, and one or two actions; collapsed, only its title strip. For the game's current job widget, the mod's
    /// ShiftCard and the current building.
    /// </summary>
    public sealed class UiSidePanelModel
    {
        public const int MostRows = 5, MostActions = 2;

        public string Title;                               // "Shift at Bloom Café"
        public UiIcon Icon = UiIcon.Briefcase;
        public UiInfoRow[] Rows = new UiInfoRow[0];
        public bool HasProgress;
        public UiProgressModel Progress;                   // the shift's hours, a delivery's way
        public bool HasTiming;
        public UiTimingModel Timing;                       // a trade's timing
        public UiActionModel[] Actions = new UiActionModel[0];   // their Command is ignored: the panel's own commands carry which
        public bool Collapsed;
    }

    /// <summary>
    /// Side panels: compact slate panels docked at the top right over the running game, stacked a grid step apart, each
    /// with its title strip (its glyph, its title, a chevron that folds it to the strip alone), up to five rows on its band,
    /// a meter or a timing gauge, and one or two quiet buttons. Commands: <see cref="FoldCommand"/> (a panel's index),
    /// <see cref="LinkCommand"/> (panel * 8 + row) and <see cref="ActionCommand"/> (panel * 4 + action). Game-free. Cost: a
    /// layout a change.
    /// </summary>
    public static class UiSidePanel
    {
        public const int FoldCommand = 210, LinkCommand = 211, ActionCommand = 212;
        public const float Width = 360f;   // px: narrower than an info panel, as it stays up while the player walks
        private const byte Region = 210;   // the panels' own column for the d-pad

        /// <summary>One panel's height, folded or open.</summary>
        public static float Height(UiSidePanelModel m)
        {
            if (m.Collapsed)
            {
                return UiFrame.Strip;
            }
            float h = UiFrame.Strip;
            int rows = Math.Min(UiSidePanelModel.MostRows, m.Rows.Length);
            if (rows > 0)
            {
                h += UiTokens.S2 * 2f + rows * UiTokens.PairRow;
            }
            if (m.HasProgress)
            {
                h += UiTokens.S3 + UiDraw.ProgressHeight(ref m.Progress);
            }
            if (m.HasTiming)
            {
                h += UiTokens.S3 + UiDraw.TimingHeight;
            }
            if (m.Actions.Length > 0)
            {
                h += UiTokens.S3 + UiTokens.ControlSmall;
            }
            return UiDraw.Snap(h + UiTokens.S3);
        }

        /// <summary>Lays the panels down from a top right corner (px of the layout); returns the y under the last.</summary>
        public static float LayStack(UiScene s, UiAt at, UiSidePanelModel[] panels, int count, float right, float top, IUiKeys keys)
        {
            float y = top;
            UiAt column = at.InRegion(Region);
            for (int i = 0; i < count && i < panels.Length; i++)
            {
                y = Lay(s, column, panels[i], i, right - Width, y, keys) + UiTokens.S2;
            }
            return y - (count > 0 ? UiTokens.S2 : 0f);
        }

        // One panel: its strip with the fold, then its rows, its meter or gauge and its buttons. Returns the y under it.
        private static float Lay(UiScene s, UiAt at, UiSidePanelModel m, int index, float x, float y, IUiKeys keys)
        {
            float h = Height(m);
            int panel = UiDraw.Box(s, at, x, y, Width, h, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt on = at.In(panel);
            UiDraw.TitleStrip(s, on, x, y, Width, m.Icon, m.Title, false);
            // The fold stands where a close would, in the room the strip keeps clear at its right, so the title stays centred.
            float b = UiTokens.MinTarget + UiTokens.S2;
            UiDraw.IconButton(s, on, x + Width - UiTokens.S2 - b, y + (UiFrame.Strip - b) / 2f, b, m.Collapsed ? UiIcon.Down : UiIcon.Up, UiRole.Text,
                FoldCommand, index, false, UiAction.Count, false, true);
            if (m.Collapsed)
            {
                return y + h;
            }
            float cy = y + UiFrame.Strip;
            float ix = x + UiTokens.S4, iw = Width - UiTokens.S4 * 2f;
            int rows = Math.Min(UiSidePanelModel.MostRows, m.Rows.Length);
            if (rows > 0)
            {
                float bh = UiTokens.S2 * 2f + rows * UiTokens.PairRow;
                UiDraw.Band(s, on, x, cy, Width, bh);
                float ry = cy + UiTokens.S2;
                for (int r = 0; r < rows; r++)
                {
                    ref UiInfoRow row = ref m.Rows[r];
                    ry += UiDraw.InfoRow(s, on, ix, ry, iw, row.Label, row.Value, row.Link, LinkCommand, index * 8 + r, row.Ink == UiRole.None ? UiRole.Text : row.Ink);
                }
                cy += bh;
            }
            if (m.HasProgress)
            {
                cy += UiTokens.S3;
                cy += UiDraw.Progress(s, on, ix, cy, iw, ref m.Progress);
            }
            if (m.HasTiming)
            {
                cy += UiTokens.S3;
                cy += UiDraw.Timing(s, on, ix, cy, iw, ref m.Timing);
            }
            int actions = Math.Min(UiSidePanelModel.MostActions, m.Actions.Length);
            if (actions > 0)
            {
                cy += UiTokens.S3;
                float bw = actions == 1 ? iw : UiDraw.SnapDown((iw - UiTokens.S2) / 2f);
                for (int a = 0; a < actions; a++)
                {
                    ref UiActionModel act = ref m.Actions[a];
                    // Quiet buttons: the panel stays up while the player walks, so nothing on it outshouts the world's one main action.
                    UiDraw.Button(s, on, ix + a * (bw + UiTokens.S2), cy, bw, UiTokens.ControlSmall, act.Kind == UiButtonKind.Destructive ? UiButtonKind.Destructive : UiButtonKind.Secondary,
                        act.Label, act.Icon, UiAction.Count, keys, ActionCommand, index * 4 + a, act.Disabled);
                }
            }
            return y + h;
        }
    }
}
