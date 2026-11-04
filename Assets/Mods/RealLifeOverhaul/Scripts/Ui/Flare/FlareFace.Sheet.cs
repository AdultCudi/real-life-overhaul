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

        // The chips of a group stand in a grid of equal chips, as many to a row as the room takes and the rows of the same count, so the rows end together and the d-pad's
        // down stays in its column (a plain wrap of chips of their own widths ended each row somewhere else).
        private static void ChipGrid(string[] chips, float room, out float chip, out int columns, out bool lone)
        {
            lone = false;
            int n = Math.Min(chips.Length, GroupStride);
            float widest = 0f, all = 0f;
            for (int i = 0; i < n; i++)
            {
                widest = Math.Max(widest, PillWidth(chips[i]));
                all += PillWidth(chips[i]) + UiTokens.S2;
            }
            if (all - UiTokens.S2 <= room)
            {
                chip = 0f;      // they all fit in one row at their own widths: no grid
                columns = n;
                return;
            }
            chip = Math.Min(room, widest);
            columns = Math.Max(1, Math.Min(n, (int)Math.Floor((room + UiTokens.S2) / (chip + UiTokens.S2))));
            int rows = (n + columns - 1) / columns;
            columns = (n + rows - 1) / rows;   // the rows as even as they can be
            if (columns > 1 && n > columns && n % columns == 1)
            {
                // a lone chip in the last row is an orphan: one more to a row if the room takes it, else the last two rows share it (the row before gives up its last chip), else one column
                if ((columns + 1) * chip + columns * UiTokens.S2 <= room)
                {
                    columns++;
                }
                else if (columns >= 3)
                {
                    lone = true;   // two columns cannot spare one: the row before would be left with a chip of its own
                }
                else
                {
                    columns = 1;
                }
            }
            if (columns == 1)
            {
                chip = room;   // a column of options is as wide as the room: no void beside it
            }
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
                    Line(s, at, x, cy, w, UiStyle.Small, UiRole.Text2, k.Line, UiFlags.Truncate);
                }
                cy += L(UiStyle.Small);
            }
            cy += UiTokens.S3;
            // The review being answered, on the bubble of what others wrote.
            if (k.Stars == 0 && !string.IsNullOrEmpty(k.Words))
            {
                // What a deal is: a plain sentence, not a box of its own.
                float ph = UiDraw.Snap(UiTextRules.WrappedHeight(k.Words, UiStyle.Body, w, 3));
                if (draw)
                {
                    UiDraw.Text(s, at, x, cy, w, UiStyle.Body, UiRole.Text2, k.Words, UiFlags.Wrap | UiFlags.Truncate, 3);
                }
                cy += ph + UiTokens.S3;
            }
            else if (k.Stars > 0 || !string.IsNullOrEmpty(k.Words))
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
                    bool caret = UiDraw.Typing(s, FieldCommand, k.FieldArg);
                    float tx = x + UiTokens.S3, room = fw - UiTokens.S6, lead = caret && !typed ? UiDraw.CaretRoom : 0f, last = 0f;
                    // Typed: the last lines, the ones the caret is on.
                    string shown = caret && typed
                        ? UiTextRules.LastLines(k.FieldText, UiStyle.Body, room - UiDraw.CaretRoom, SheetLines, out last)
                        : typed ? k.FieldText : k.FieldHint;
                    int words = UiDraw.Text(s, at.In(field), tx + lead, cy + UiTokens.S3, room - lead, UiStyle.Body, typed ? UiRole.Text : UiRole.Muted, shown,
                        UiFlags.Wrap | UiFlags.Truncate | (typed ? UiFlags.None : UiFlags.Label), SheetLines);
                    if (!typed)
                    {
                        Hint(s, words);
                    }
                    if (caret)
                    {
                        int line = typed ? Math.Max(0, UiTextRules.Lines(shown, UiStyle.Body, room - UiDraw.CaretRoom, SheetLines, out float _) - 1) : 0;
                        UiDraw.Caret(s, at.In(field), tx + (typed ? last + 1f : 0f), cy + UiTokens.S3 + line * UiTextRules.LineHeight(UiStyle.Body), UiTextRules.LineHeight(UiStyle.Body),
                            UiStyle.Body);
                    }
                    UiDraw.Node(s, at, x, cy, fw, SheetField, FieldCommand, k.FieldArg, field);
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
                        Line(s, at, x, cy + UiTokens.S1, fw, UiStyle.Small, UiRole.Text2, k.FieldCount, UiFlags.Right);
                    }
                    cy += UiTokens.S1 + L(UiStyle.Small);
                }
                cy += UiTokens.S3;
            }
            // The groups of chips, each beside its label, wrapped to the room the label leaves (so a long sheet still fits the window).
            int groups = Math.Min(k.Groups.Length, k.Chips.Length);
            float labelW = 0f;
            for (int g = 0; g < groups; g++)
            {
                labelW = Math.Max(labelW, UiDraw.Snap(UiTextRules.Width(k.Groups[g], UiStyle.SectionHeader) + UiTokens.S4));   // the labels take what they need, the chips the rest
            }
            labelW = Math.Max(UiDraw.SnapDown(w * 0.18f), Math.Min(labelW, UiDraw.SnapDown(w * 0.3f)));
            float chipsX = x + labelW;
            for (int g = 0; g < groups; g++)
            {
                string[] chips = k.Chips[g];
                if (chips == null || chips.Length == 0)
                {
                    continue;
                }
                if (draw)
                {
                    UiDraw.TextMiddle(s, at, x, cy, UiTokens.ControlSmall, labelW - UiTokens.S2, UiStyle.SectionHeader, UiRole.Text2, k.Groups[g], UiFlags.Label | UiFlags.Truncate);
                }
                int chosen = g < k.Chosen.Length ? k.Chosen[g] : -1;
                ChipGrid(chips, x + w - chipsX, out float pw, out int columns, out bool lone);
                int count = Math.Min(chips.Length, GroupStride);
                float natural = chipsX;
                for (int i = 0; i < count; i++)
                {
                    if (draw && pw > 0f)
                    {
                        int row = i / columns, column = i % columns;
                        if (lone && i >= count - 2)
                        {
                            row = (count + columns - 1) / columns - 1;
                            column = i - (count - 2);
                        }
                        Pill(s, at, chipsX + column * (pw + UiTokens.S2), cy + row * (UiTokens.ControlSmall + UiTokens.S2), pw, chips[i],
                            i == chosen ? PillKind.Chosen : PillKind.Line, SheetChipCommand, g * GroupStride + i);
                    }
                    else if (draw)
                    {
                        float cw = PillWidth(chips[i]);
                        Pill(s, at, natural, cy, cw, chips[i], i == chosen ? PillKind.Chosen : PillKind.Line, SheetChipCommand, g * GroupStride + i);
                        natural += cw + UiTokens.S2;
                    }
                }
                cy += ((count + columns - 1) / columns - 1) * (UiTokens.ControlSmall + UiTokens.S2) + UiTokens.ControlSmall + UiTokens.S3;
            }
            // The options, a row each with what it does under its name.
            for (int i = 0; i < k.Options.Length; i++)
            {
                if (draw)
                {
                    bool on = k.Option == i;
                    int row = UiDraw.Box(s, at, x, cy, w, OptionHeight, on ? UiRole.RowSel : UiRole.Field, UiRole.None, UiTokens.RadiusLarge, on ? UiFlags.Selected : UiFlags.None);
                    UiAt inRow = at.In(row);
                    Line(s, inRow, x + UiTokens.S4, cy + UiTokens.S2, w - UiTokens.S8, UiStyle.Title, UiRole.Text, k.Options[i], UiFlags.Truncate);
                    Line(s, inRow, x + UiTokens.S4, cy + UiTokens.S2 + L(UiStyle.Title), w - UiTokens.S8, UiStyle.Small, on ? UiRole.Text : UiRole.Text2, At(k.OptionLines, i),
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
                        Line(s, inTile, tx + UiTokens.S3, cy + UiTokens.S2, bw - UiTokens.S6, UiStyle.Small, UiRole.Text2, k.FigureLabels[i], UiFlags.Truncate);
                        Line(s, inTile, tx + UiTokens.S3, cy + UiTokens.S2 + L(UiStyle.Small), bw - UiTokens.S6, UiStyle.Number, UiRole.Text, k.Figures[i], UiFlags.Truncate);
                    }
                }
                cy += FigureHeight + UiTokens.S3;
            }
            // Where an audience lives: a bar for each district, by its share of the most.
            int bars = Math.Min(k.BarLabels.Length, Math.Min(k.BarShares.Length, k.BarFigures.Length));
            if (bars > 0)
            {
                float rowH = UiTokens.S5;
                if (draw)
                {
                    Line(s, at, x, cy, w, UiStyle.SectionHeader, UiRole.Text2, k.BarsTitle, UiFlags.Label | UiFlags.Truncate);
                }
                cy += L(UiStyle.SectionHeader) + UiTokens.S2;
                float barLabelW = UiDraw.SnapDown(w * 0.3f), figW = UiTokens.S12;
                for (int i = 0; i < bars; i++)
                {
                    if (draw)
                    {
                        UiDraw.TextMiddle(s, at, x, cy, rowH, barLabelW - UiTokens.S2, UiStyle.Small, UiRole.Text, k.BarLabels[i], UiFlags.Truncate);
                        float tx = x + barLabelW, tw = w - barLabelW - figW - UiTokens.S2;
                        int track = UiDraw.Box(s, at, tx, cy + (rowH - UiTokens.S2) / 2f, tw, UiTokens.S2, UiRole.Track, UiRole.None, UiTokens.S1, UiFlags.Middle);
                        if (k.BarShares[i] > 0f)
                        {
                            UiDraw.Box(s, at.In(track), tx, cy + (rowH - UiTokens.S2) / 2f, Math.Max(UiTokens.S2, tw * Math.Min(1f, k.BarShares[i])), UiTokens.S2, UiRole.Chart2, UiRole.None,
                                UiTokens.S1, UiFlags.Middle | UiFlags.Share);
                        }
                        UiDraw.TextMiddle(s, at, x + w - figW, cy, rowH, figW, UiStyle.Small, UiRole.Text2, k.BarFigures[i], UiFlags.Right);
                    }
                    cy += rowH;
                }
                cy += UiTokens.S3;
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
