using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>One row of a bar's popup: a dance to start, an accessory with its switch.</summary>
    public struct UiHudPopupRow
    {
        public UiIcon Icon;        // a glyph on a small tile at the left (an accessory's slot); None for none
        public string Text;        // "Swing"
        public string Sub;         // a quieter word under it ("Hand"); null for none
        public string Chip;        // a state at the right ("Dancing", "Dance studio"); null for none
        public UiRole ChipTone;    // Accent for what is going on, None for the quiet grey
        public bool HasSwitch;     // a switch at the right (an accessory shown or hidden)
        public bool On;
        public bool Press;         // the row is a target (a dance starts)
        public bool Disabled;      // greyed: the game will not take it now, or it is not known yet
    }

    /// <summary>A short panel rising from one of the bar's small buttons: the dances, the accessories.</summary>
    public sealed class UiHudPopupModel
    {
        public string Title;                         // "Dances", set in capitals
        public string Meta;                          // "9 of 9 known"
        public UiHudPopupRow[] Rows = new UiHudPopupRow[0];
        public int RowCount;
        public bool Numbered;                        // each row's number on a small tile at the left (the dances)
        public string Empty;                         // the sentence when there are no rows
        public string Action;                        // a button at its foot: "Stop dancing", "Take all off"; null for none
        public bool ActionDisabled;
        public float AnchorX;                        // px: the middle of the button it rises from
    }

    /// <summary>
    /// The bar's popups: short slate panels standing a grid step over the status bar above the button that opened them, each
    /// with its strip (its title in capitals in the cyan, a quiet count at the right), its rows (a numbered tile or a glyph,
    /// the words and a quieter word under them, a state chip or a switch at the right) and a button at its foot. Two may
    /// stand side by side; the second keeps clear of the first. They take no key; the pointer presses. Commands:
    /// <see cref="RowCommand"/> and <see cref="SwitchCommand"/> (popup * 32 + row), <see cref="ActionCommand"/> (a popup's
    /// index). Game-free. Cost: a layout a change.
    /// </summary>
    public static class UiHudPopup
    {
        public const int RowCommand = 240, SwitchCommand = 241, ActionCommand = 242;
        public const float Width = 300f;
        public const int MostRows = 12;
        private const float Number = UiTokens.MinTarget;   // px: a row's numbered tile

        /// <summary>A row's height: one line, or its words over a quieter word.</summary>
        private static float RowHeight(UiHudPopupModel m, ref UiHudPopupRow row) => string.IsNullOrEmpty(row.Sub) ? UiTokens.Control : UiTokens.Control + UiTokens.S2;

        private static int Rows(UiHudPopupModel m) => Math.Min(MostRows, Math.Min(m.RowCount, m.Rows.Length));

        /// <summary>One popup's height.</summary>
        public static float Height(UiHudPopupModel m)
        {
            float h = UiFrame.Strip + UiTokens.S1;
            int rows = Rows(m);
            if (rows == 0)
            {
                h += UiTokens.S3 + UiTextRules.WrappedHeight(m.Empty ?? string.Empty, UiStyle.Body, Width - UiTokens.S4 * 2f, 3) + UiTokens.S3;
            }
            for (int r = 0; r < rows; r++)
            {
                h += RowHeight(m, ref m.Rows[r]);
            }
            if (!string.IsNullOrEmpty(m.Action))
            {
                h += UiTokens.S2 + UiTokens.ControlSmall;
            }
            return UiDraw.Snap(h + UiTokens.S3);
        }

        /// <summary>Where a popup stands: over its button, inside the frame, clear of one laid before it (px of the layout).</summary>
        public static void Place(UiHudPopupModel m, float frameWidth, float bandY, float leftEdge, out float x, out float y)
        {
            x = UiDraw.SnapDown(Math.Max(UiHud.Left, Math.Min(frameWidth - UiHud.Left - Width, Math.Max(leftEdge, m.AnchorX - Width / 2f))));
            y = UiDraw.SnapDown(bandY - UiTokens.S2 - Height(m));
        }

        /// <summary>Lays the popups over the bar, the first over its button and each next one to the right of the one before.</summary>
        public static void LayAll(UiScene s, UiAt at, UiHudPopupModel[] popups, int count, float frameWidth, float bandY)
        {
            float edge = 0f;
            for (int i = 0; i < count && i < popups.Length; i++)
            {
                Place(popups[i], frameWidth, bandY, edge, out float x, out float y);
                Lay(s, at, popups[i], i, x, y);
                edge = x + Width + UiTokens.S2;
            }
        }

        private static void Lay(UiScene s, UiAt at, UiHudPopupModel m, int index, float x, float y)
        {
            float h = Height(m);
            int panel = UiDraw.Box(s, at, x, y, Width, h, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt on = at.In(panel);
            UiDraw.Box(s, on, x, y, Width, UiFrame.Strip, UiRole.Header, UiRole.None, -UiTokens.RadiusPanel, UiFlags.Decor | UiFlags.Chrome);
            float ix = x + UiTokens.S4, iw = Width - UiTokens.S4 * 2f;
            float mw = string.IsNullOrEmpty(m.Meta) ? 0f : Math.Min(iw * 0.5f, UiDraw.Snap(UiTextRules.Width(m.Meta, UiStyle.Small) + UiTokens.S1));
            if (mw > 0f)
            {
                UiDraw.TextMiddle(s, on, ix + iw - mw, y, UiFrame.Strip, mw, UiStyle.Small, UiRole.Text2, m.Meta, UiFlags.Right | UiFlags.Truncate);
            }
            UiDraw.TextMiddle(s, on, ix, y, UiFrame.Strip, iw - mw - UiTokens.S3, UiStyle.SectionHeader, UiRole.AccentText, m.Title, UiFlags.Label | UiFlags.Truncate);
            float cy = y + UiFrame.Strip + UiTokens.S1;
            int rows = Rows(m);
            if (rows == 0)
            {
                UiDraw.Text(s, on, ix, cy + UiTokens.S3, iw, UiStyle.Body, UiRole.Text2, m.Empty ?? string.Empty, UiFlags.Wrap, 3);
            }
            for (int r = 0; r < rows; r++)
            {
                cy += Row(s, on, m, ref m.Rows[r], index * 32 + r, r, x, cy, ix, iw);
            }
            if (!string.IsNullOrEmpty(m.Action))
            {
                float by = y + h - UiTokens.S3 - UiTokens.ControlSmall;
                UiDraw.Button(s, on, ix, by, iw, UiTokens.ControlSmall, UiButtonKind.Secondary, m.Action, UiIcon.None, UiAction.Count, null, ActionCommand, index, m.ActionDisabled);
            }
        }

        // A row: its numbered tile or glyph, its words over a quieter word, a chip or a switch at the right; the whole row a
        // target where it presses (a dance), the switch its own target (an accessory).
        private static float Row(UiScene s, UiAt at, UiHudPopupModel m, ref UiHudPopupRow row, int arg, int number, float x, float y, float ix, float iw)
        {
            float h = RowHeight(m, ref row);
            int face = UiDraw.Box(s, at, x, y, Width, h, row.Chip != null && row.ChipTone == UiRole.Accent ? UiRole.RowSel : UiRole.None, UiRole.None, 0f,
                row.Disabled ? UiFlags.Disabled : UiFlags.None);
            UiAt on = at.In(face);
            int first = s.ItemCount;
            float tx = ix;
            if (m.Numbered)
            {
                int tile = UiDraw.Box(s, on, ix, y + (h - Number) / 2f, Number, Number, UiRole.Tile, UiRole.None, UiTokens.RadiusLarge);
                UiDraw.TextMiddle(s, on.In(tile), ix, y + (h - Number) / 2f, Number, Number, UiStyle.SmallStrong, UiRole.Text, (number + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
                    UiFlags.Centre);
                tx += Number + UiTokens.S3;
            }
            else if (row.Icon != UiIcon.None)
            {
                int tile = UiDraw.Box(s, on, ix, y + (h - UiTokens.ControlSmall) / 2f, UiTokens.ControlSmall, UiTokens.ControlSmall, UiRole.Tile, UiRole.None, UiTokens.RadiusLarge);
                UiDraw.Icon(s, on.In(tile), ix + (UiTokens.ControlSmall - UiTokens.Icon) / 2f, y + (h - UiTokens.Icon) / 2f, UiTokens.Icon, row.Icon, UiRole.None, UiFlags.Middle | UiFlags.Centre);
                tx += UiTokens.ControlSmall + UiTokens.S3;
            }
            float right = ix + iw;
            if (row.HasSwitch)
            {
                float w = UiDraw.Snap(UiTokens.S16);
                UiDraw.Switch(s, on, right - w, y + (h - UiTokens.Control) / 2f, w, row.On, null, SwitchCommand, arg, row.Disabled);
                right -= w + UiTokens.S2;
            }
            else if (!string.IsNullOrEmpty(row.Chip))
            {
                float cw = UiSidePanel.ToneChipWidth(row.Chip);
                UiSidePanel.ToneChip(s, on, right - cw, y + (h - UiTokens.Badge - UiTokens.S1) / 2f, row.Chip, row.ChipTone);
                right -= cw + UiTokens.S2;
            }
            float tw = right - tx;
            if (string.IsNullOrEmpty(row.Sub))
            {
                UiDraw.TextMiddle(s, on, tx, y, h, tw, UiStyle.Body, row.Disabled ? UiRole.Muted : UiRole.Text, row.Text, UiFlags.Truncate);
            }
            else
            {
                float line = UiTextRules.LineHeight(UiStyle.Body), small = UiTextRules.LineHeight(UiStyle.Small);
                float ty = y + (h - line - small) / 2f;
                UiDraw.TextMiddle(s, on, tx, ty, line, tw, UiStyle.Body, row.Disabled ? UiRole.Muted : UiRole.Text, row.Text, UiFlags.Truncate);
                UiDraw.TextMiddle(s, on, tx, ty + line, small, tw, UiStyle.Small, UiRole.Text2, row.Sub, UiFlags.Truncate);
            }
            if (row.Disabled)
            {
                UiDraw.Dim(s, first);
            }
            if (row.Press)
            {
                UiDraw.Node(s, at, x, y, Width, h, RowCommand, arg, face, row.Disabled);   // sideways: the popup beside it, if one stands there
            }
            return h;
        }
    }
}
