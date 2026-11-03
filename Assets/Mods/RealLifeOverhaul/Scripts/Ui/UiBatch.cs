using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// What a review sheet says: a batch of changes on several people (a raise for several picked, hiring several picked) before
    /// anything is changed. The header names it, the table lists each person with what changes for them, and the foot gives
    /// the way back and the yes.
    /// </summary>
    public sealed class UiBatchModel
    {
        public UiIcon Icon = UiIcon.ChartLineUp;     // the emblem on its tile
        public string Title;                         // the header, in capitals
        public string Lead;                          // what it comes to in a sentence or two; null for none
        public UiDataTableModel Table = new UiDataTableModel();   // a person to a row, their totals at the foot; rows are read, never chosen
        public UiActionModel[] Buttons = new UiActionModel[0];    // the way back first, the confirming one last
        public int Focus = -1;                       // the button the keys start on, or -1 for the first target
        public UiPrompt[] Prompts = new UiPrompt[0];
        public int PromptCount;
    }

    /// <summary>
    /// The review sheet, a card over a screen that lists what a batch will do to each person before the player says yes: after
    /// the compact decision card's frame (the world dimmed, an emblem on its tile, the header in cyan capitals, the buttons at
    /// the foot's right, the confirming one last and on F), with the big table in the middle for the people, so a long list
    /// scrolls on its own offset and a few people take a few rows. It lays itself over a window already laid out, as
    /// <see cref="UiCard.LayOver"/> does. Commands: <see cref="ButtonCommand"/> (a button's index) and the table's from
    /// <see cref="TableFirst"/> (its rows are targets that do nothing, so a pad can walk them and scroll the list).
    /// Game-free. Cost: a layout a change, the rows in view only.
    /// </summary>
    public static class UiBatch
    {
        public const int ButtonCommand = 207, TableFirst = 400;
        public const float Width = 1152f;           // px: the sheet at its widest; narrower frames leave the screen's margin
        private const float Emblem = 56f;           // px: the emblem's tile, as the card's
        private const int LeadLines = 3;            // a long translation of the lead wraps this far, never cut mid word
        private const int TitleLines = 2;
        private const int LeastRows = 3;            // rows the table keeps before the sheet gives up its room

        /// <summary>The sheet over the screen already in the scene, on the overlay's layer, as the game stands its box over an app.</summary>
        public static void LayOver(UiScene s, UiBatchModel m, UiFrame frame, IUiKeys keys)
        {
            s.PromptCount = 0;
            s.Pointer = frame.Pointer;
            s.Held = false;   // the window under it was laid held; the layer over it has the keys
            var root = new UiAt { Parent = -1, Clip = -1, Layer = 1, Order = -1 };
            UiDraw.Box(s, root, 0f, 0f, frame.Width, frame.BandY, UiRole.Scrim, UiRole.None, 0f, UiFlags.Decor);   // the world dimmed, the status bar left clear
            float w = UiDraw.SnapDown(Math.Min(Width, frame.Width - UiTokens.S16 * 2f));
            float inner = w - UiTokens.S6 * 2f;
            float textW = inner - Emblem - UiTokens.S5;
            float titleH = UiDraw.Snap(UiTextRules.WrappedHeight(m.Title, UiStyle.AppTitle, textW, TitleLines));
            bool lead = !string.IsNullOrEmpty(m.Lead);
            float leadH = lead ? UiDraw.Snap(UiTextRules.WrappedHeight(m.Lead, UiStyle.Body, textW, LeadLines)) : 0f;
            float headH = Math.Max(Emblem, titleH + (lead ? UiTokens.S2 + leadH : 0f));
            float rowH = UiDataTable.RowHeight(m.Table);
            float chrome = UiDataTable.Chrome(m.Table);
            float fixedH = UiTokens.S6 + headH + UiTokens.S5 + UiTokens.S5 + UiTokens.Control + UiTokens.S6;
            float room = frame.BandY - UiTokens.S6 * 2f - fixedH - chrome;   // the table's rows get what the sheet leaves, a margin kept above and below
            int rows = Math.Max(Math.Min(LeastRows, m.Table.RowCount), Math.Min(m.Table.RowCount, (int)Math.Floor(room / rowH)));
            float tableH = UiDraw.Snap(chrome + Math.Max(1, rows) * rowH);
            float h = UiDraw.Snap(fixedH + tableH);
            float x = UiDraw.SnapDown((frame.Width - w) / 2f), y = UiDraw.SnapDown(Math.Max(UiTokens.S6, (frame.BandY - h) / 2f));
            int sheet = UiDraw.Box(s, root, x, y, w, h, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt at = root.In(sheet);
            float ex = x + UiTokens.S6, ey = y + UiTokens.S6;
            int tile = UiDraw.Tile(s, at, ex, ey, Emblem, UiIconTints.Of(m.Icon), false);
            float g = UiDraw.Snap(Emblem * 0.57f);
            UiDraw.Icon(s, at.In(tile), ex + (Emblem - g) / 2f, ey + (Emblem - g) / 2f, g, m.Icon, UiRole.None, UiFlags.Middle | UiFlags.Centre, true);
            float tx = ex + Emblem + UiTokens.S5, ty = headH > titleH + (lead ? UiTokens.S2 + leadH : 0f) ? ey + UiDraw.SnapDown((headH - titleH - (lead ? UiTokens.S2 + leadH : 0f)) / 2f) : ey;
            UiDraw.Text(s, at, tx, ty, textW, UiStyle.AppTitle, UiRole.AccentText, m.Title, UiFlags.Label | UiFlags.Wrap, TitleLines);
            if (lead)
            {
                UiDraw.Text(s, at, tx, ty + titleH + UiTokens.S2, textW, UiStyle.Body, UiRole.Text2, m.Lead, UiFlags.Label | UiFlags.Wrap, LeadLines);
            }
            float tableY = ey + headH + UiTokens.S5;
            UiDataTable.Lay(s, at, m.Table, ex, tableY, inner, tableH, keys, TableFirst);
            float by = tableY + tableH + UiTokens.S5;
            float bx = x + w - UiTokens.S6;
            for (int i = m.Buttons.Length - 1; i >= 0; i--)
            {
                ref UiActionModel b = ref m.Buttons[i];
                float bw = UiDraw.ButtonWidth(b.Kind, b.Label, b.Icon, b.Key, keys);
                bx -= bw;
                int node = UiDraw.Button(s, at, bx, by, bw, UiTokens.Control, b.Kind, b.Label, b.Icon, b.Key, keys, ButtonCommand, i, b.Disabled);
                if (i == m.Focus)
                {
                    s.FocusFirst = node;
                }
                bx -= UiTokens.S3;
            }
            for (int i = 0; i < m.PromptCount; i++)
            {
                s.AddPrompt(m.Prompts[i].Action, m.Prompts[i].Label);
            }
            UiDraw.PromptBar(s, frame, keys);
        }
    }
}
