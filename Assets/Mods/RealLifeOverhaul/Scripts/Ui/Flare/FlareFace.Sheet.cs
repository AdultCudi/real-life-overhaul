using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Flare's sheet over the window: to post, to hire, to answer a review. A card of the slate over the dimmed window with its
    /// title and the way to close it; the words being answered; the field with the picture beside it; its groups of chips;
    /// the options; the figures and what they forecast; Cancel and the orange button. Measured once, then laid. Game-free.
    /// </summary>
    public static partial class FlareFace
    {
        public const float SheetWidth = 720f, SheetField = 104f, OptionHeight = 56f, FigureHeight = 64f;
        public const int GroupStride = 16;   // SheetChipCommand's argument: group * GroupStride + chip
        private const int SheetLines = 4;    // the compose box's lines of words

        private static void Sheet(UiScene s, FlareFaceModel m, float wx, float wy, float ww, float wh, IUiKeys keys)
        {
            if (m.Sheet == PhotosSheet)
            {
                PhotoSheet(s, m, wx, wy, ww, wh, keys);
                return;
            }
            FlareSheetModel k = m.Sheet0;
            UiDraw.Scrim(s, wx, wy, ww, wh, 1);
            float cw = Math.Min(SheetWidth, UiDraw.SnapDown(ww - UiTokens.S16 * 2f));
            float inner = cw - UiTokens.S6 * 2f;
            var at = new UiAt { Parent = -1, Clip = -1, Layer = 1, Order = -1, Region = 15 };
            float ch = UiDraw.Snap(SheetBody(s, at, k, 0f, 0f, inner, false) + UiTokens.S6 * 2f);
            float cx = wx + UiDraw.SnapDown((ww - cw) / 2f), cy = wy + UiDraw.SnapDown(Math.Max(UiTokens.S4, (wh - ch) / 2f));
            int card = UiDraw.Box(s, at, cx, cy, cw, ch, UiRole.Panel, UiRole.PanelEdge, UiTokens.RadiusPanel, UiFlags.Panel);   // its edge parts it from the dimmed page
            SheetBody(s, at.In(card), k, cx + UiTokens.S6, cy + UiTokens.S6, inner, true);
        }

        // The sheet's parts from the top: measured with draw false, laid with it true. Returns the height they take.
        private static float SheetBody(UiScene s, UiAt at, FlareSheetModel k, float x, float y, float w, bool draw)
        {
            float cy = y;
            if (draw)
            {
                UiDraw.TextMiddle(s, at, x, cy, UiTokens.Control, w - UiTokens.Control - UiTokens.S2, UiStyle.Number, UiRole.Text, k.Title, UiFlags.Truncate);
                int close = UiDraw.Box(s, at, x + w - UiTokens.Control, cy, UiTokens.Control, UiTokens.Control, UiRole.None, UiRole.None, UiTokens.RadiusLarge);
                UiDraw.Icon(s, at.In(close), x + w - UiTokens.Control + (UiTokens.Control - UiTokens.Icon) / 2f, cy + (UiTokens.Control - UiTokens.Icon) / 2f, UiTokens.Icon, UiIcon.Close,
                    UiRole.Text2, UiFlags.Middle | UiFlags.Centre);
                UiDraw.Node(s, at, x + w - UiTokens.Control, cy, UiTokens.Control, UiTokens.Control, SheetCancelCommand, 0, close);
            }
            cy += UiTokens.Control;
            if (!string.IsNullOrEmpty(k.Line))
            {
                if (draw)
                {
                    UiDraw.Text(s, at, x, cy, w, UiStyle.Small, UiRole.Text2, k.Line, UiFlags.Truncate);
                }
                cy += L(UiStyle.Small);
            }
            cy += UiTokens.S3;
            // The review being answered, on the bubble of what others wrote.
            if (k.Stars > 0 || !string.IsNullOrEmpty(k.Words))
            {
                float th = string.IsNullOrEmpty(k.Words) ? 0f : UiDraw.Snap(UiTextRules.WrappedHeight(k.Words, UiStyle.Body, w - UiTokens.S8, 4));
                float bh = UiTokens.S3 + (k.Stars > 0 ? UiTokens.IconSmall + UiTokens.S2 : 0f) + th + UiTokens.S3;
                if (draw)
                {
                    int bubble = UiDraw.Box(s, at, x, cy, w, bh, UiRole.Bubble, UiRole.None, UiTokens.RadiusLarge);
                    UiAt inBubble = at.In(bubble);
                    float by = cy + UiTokens.S3;
                    if (k.Stars > 0)
                    {
                        UiDraw.Stars(s, inBubble, x + UiTokens.S4, by, UiTokens.IconSmall, Math.Min(10, k.Stars * 2));
                        by += UiTokens.IconSmall + UiTokens.S2;
                    }
                    if (th > 0f)
                    {
                        UiDraw.Text(s, inBubble, x + UiTokens.S4, by, w - UiTokens.S8, UiStyle.Body, UiRole.Text, k.Words, UiFlags.Wrap | UiFlags.Truncate, 4);
                    }
                }
                cy += bh + UiTokens.S3;
            }
            // The field, the picture beside it.
            if (k.Field)
            {
                bool pic = !string.IsNullOrEmpty(k.Picture);
                float pw = pic ? UiDraw.SnapDown(SheetField * PictureAspect) : 0f;
                float fw = w - (pic ? pw + UiTokens.S3 : 0f);
                if (draw)
                {
                    int field = UiDraw.Box(s, at, x, cy, fw, SheetField, UiRole.Field, UiRole.None, UiTokens.RadiusLarge);
                    bool typed = !string.IsNullOrEmpty(k.FieldText);
                    bool caret = UiDraw.Typing(s, FieldCommand, ComposeField);
                    float tx = x + UiTokens.S3, room = fw - UiTokens.S6, lead = caret && !typed ? UiDraw.CaretRoom : 0f, last = 0f;
                    // Typed: the last lines, the ones the caret is on.
                    string shown = caret && typed
                        ? UiTextRules.LastLines(k.FieldText, UiStyle.Body, room - UiDraw.CaretRoom, SheetLines, out last)
                        : typed ? k.FieldText : k.FieldHint;
                    UiDraw.Text(s, at.In(field), tx + lead, cy + UiTokens.S3, room - lead, UiStyle.Body, typed ? UiRole.Text : UiRole.Text2, shown,
                        UiFlags.Wrap | UiFlags.Truncate | (typed ? UiFlags.None : UiFlags.Label), SheetLines);
                    if (caret)
                    {
                        int line = typed ? Math.Max(0, UiTextRules.Lines(shown, UiStyle.Body, room - UiDraw.CaretRoom, SheetLines, out float _) - 1) : 0;
                        UiDraw.Caret(s, at.In(field), tx + (typed ? last + 1f : 0f), cy + UiTokens.S3 + line * UiTextRules.LineHeight(UiStyle.Body), UiTextRules.LineHeight(UiStyle.Body),
                            UiStyle.Body);
                    }
                    UiDraw.Node(s, at, x, cy, fw, SheetField, FieldCommand, ComposeField, field);
                    if (pic)
                    {
                        int tile = UiDraw.Box(s, at, x + w - pw, cy, pw, SheetField, UiRole.Tile, UiRole.None, UiTokens.RadiusLarge);
                        var item = new UiItem
                        {
                            Kind = UiKind.Picture, Flags = UiFlags.Middle | UiFlags.Centre, X = x + w - pw, Y = cy, W = pw, H = SheetField,
                            Text = PictureKey(k.Picture, pw / SheetField), Value = pw / SheetField,
                            Clip = -1, Node = -1, Layer = at.Layer, Order = -1, Parent = tile, Alpha = 1f
                        };
                        s.Add(ref item);
                    }
                }
                cy += SheetField;
                if (!string.IsNullOrEmpty(k.FieldCount))
                {
                    if (draw)
                    {
                        UiDraw.Text(s, at, x, cy + UiTokens.S1, fw, UiStyle.Small, UiRole.Text2, k.FieldCount, UiFlags.Right);
                    }
                    cy += UiTokens.S1 + L(UiStyle.Small);
                }
                cy += UiTokens.S3;
            }
            // The groups of chips, each under its label, wrapped to the width.
            int groups = Math.Min(k.Groups.Length, k.Chips.Length);
            for (int g = 0; g < groups; g++)
            {
                string[] chips = k.Chips[g];
                if (chips == null || chips.Length == 0)
                {
                    continue;
                }
                if (draw)
                {
                    UiDraw.Text(s, at, x, cy, w, UiStyle.SectionHeader, UiRole.Text2, k.Groups[g], UiFlags.Label | UiFlags.Truncate);
                }
                cy += L(UiStyle.SectionHeader) + UiTokens.S2;
                float px = x;
                int chosen = g < k.Chosen.Length ? k.Chosen[g] : -1;
                for (int i = 0; i < chips.Length && i < GroupStride; i++)
                {
                    float pw = Math.Min(w, PillWidth(chips[i]));
                    if (px + pw > x + w && px > x)
                    {
                        px = x;
                        cy += UiTokens.ControlSmall + UiTokens.S2;
                    }
                    if (draw)
                    {
                        Pill(s, at, px, cy, pw, chips[i], i == chosen ? PillKind.Chosen : PillKind.Line, SheetChipCommand, g * GroupStride + i);
                    }
                    px += pw + UiTokens.S2;
                }
                cy += UiTokens.ControlSmall + UiTokens.S3;
            }
            // The options, a row each with what it does under its name.
            for (int i = 0; i < k.Options.Length; i++)
            {
                if (draw)
                {
                    bool on = k.Option == i;
                    int row = UiDraw.Box(s, at, x, cy, w, OptionHeight, on ? UiRole.RowSel : UiRole.Field, UiRole.None, UiTokens.RadiusLarge, on ? UiFlags.Selected : UiFlags.None);
                    UiAt inRow = at.In(row);
                    UiDraw.Text(s, inRow, x + UiTokens.S4, cy + UiTokens.S2, w - UiTokens.S8, UiStyle.Title, UiRole.Text, k.Options[i], UiFlags.Truncate);
                    UiDraw.Text(s, inRow, x + UiTokens.S4, cy + UiTokens.S2 + L(UiStyle.Title), w - UiTokens.S8, UiStyle.Small, on ? UiRole.Text : UiRole.Text2, At(k.OptionLines, i),
                        UiFlags.Truncate);
                    UiDraw.Node(s, at, x, cy, w, OptionHeight, OptionCommand, i, row);
                }
                cy += OptionHeight + UiTokens.S2;
            }
            if (k.Options.Length > 0)
            {
                cy += UiTokens.S1;
            }
            // The figures, side by side on their tiles.
            int figures = Math.Min(k.Figures.Length, k.FigureLabels.Length);
            if (figures > 0)
            {
                if (draw)
                {
                    float tw = UiDraw.SnapDown((w - UiTokens.S2 * (figures - 1)) / figures);
                    for (int i = 0; i < figures; i++)
                    {
                        float tx = x + i * (tw + UiTokens.S2);
                        float bw = i == figures - 1 ? x + w - tx : tw;
                        int tile = UiDraw.Box(s, at, tx, cy, bw, FigureHeight, UiRole.Tile, UiRole.None, UiTokens.RadiusLarge);
                        UiAt inTile = at.In(tile);
                        UiDraw.Text(s, inTile, tx + UiTokens.S3, cy + UiTokens.S2, bw - UiTokens.S6, UiStyle.Small, UiRole.Text2, k.FigureLabels[i], UiFlags.Truncate);
                        UiDraw.Text(s, inTile, tx + UiTokens.S3, cy + UiTokens.S2 + L(UiStyle.Small), bw - UiTokens.S6, UiStyle.Number, UiRole.Text, k.Figures[i], UiFlags.Truncate);
                    }
                }
                cy += FigureHeight + UiTokens.S3;
            }
            if (!string.IsNullOrEmpty(k.Note))
            {
                float nh = UiDraw.Snap(UiTextRules.WrappedHeight(k.Note, UiStyle.Body, w, 2));
                if (draw)
                {
                    UiDraw.Text(s, at, x, cy, w, UiStyle.Body, k.NoteBad ? UiRole.DangerText : UiRole.Text2, k.Note, UiFlags.Wrap | UiFlags.Truncate, 2);
                }
                cy += nh + UiTokens.S3;
            }
            // Cancel and the orange button, at the foot's right.
            float ow = Math.Min(w / 2f, ButtonWidth(k.Confirm)), qw = Math.Min(w / 2f - UiTokens.S3, ButtonWidth(k.Cancel));
            if (draw)
            {
                Orange(s, at, x + w - ow, cy, ow, UiTokens.Control, k.Confirm, SheetConfirmCommand, 0, k.ConfirmOff);
                if (!string.IsNullOrEmpty(k.Cancel))
                {
                    float qx = x + w - ow - UiTokens.S3 - qw;
                    int cancel = UiDraw.Box(s, at, qx, cy, qw, UiTokens.Control, UiRole.None, UiRole.Line, UiTokens.Control / 2f, UiFlags.Circle);
                    UiDraw.TextMiddle(s, at.In(cancel), qx, cy, UiTokens.Control, qw, UiStyle.Button, UiRole.Text, k.Cancel, UiFlags.Centre | UiFlags.Label);
                    UiDraw.Node(s, at, qx, cy, qw, UiTokens.Control, SheetCancelCommand, 1, cancel);
                }
            }
            cy += UiTokens.Control;
            return cy - y;
        }
    }
}
