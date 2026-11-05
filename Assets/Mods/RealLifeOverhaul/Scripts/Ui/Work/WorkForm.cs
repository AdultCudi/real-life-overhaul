using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// A form of one or two columns of bands, as the mockups' pages of settings and briefs are laid: each band a darker field under a capitals label
    /// with its count, its rows a label at the left and its control at the right (a stepper for a figure, a selector for a few words, a switch, a
    /// button), people with their faces, graded bars and plain lines. For the management ladder's policy, own work, a search brief and a new ad. Every
    /// part is the kit's own. Game-free. Cost: a layout a change.
    /// </summary>
    public static class WorkForm
    {
        private const float ControlWidth = 200f, SelectorWidth = 440f;

        public static void Lay(UiScene s, UiAt at, WorkFormModel m, UiActionModel[] actions, WorkAmount[] amounts, float x, float y, float w, float bottom, IUiKeys keys)
        {
            if (m.Head != null)
            {
                y = UiAppWindow.Profile(s, at, m.Head, null, x, y, w, keys);
            }
            if (!string.IsNullOrEmpty(m.Lead))
            {
                int id = UiDraw.Text(s, at, x, y, w, UiStyle.Body, UiRole.Text2, m.Lead, UiFlags.Wrap | UiFlags.Truncate, 3);
                y = UiDraw.Snap(s.Items[id].Y + s.Items[id].H) + UiTokens.S3;
            }
            bool two = m.Right.Count > 0;
            float gap = UiTokens.S6, cw = two ? UiDraw.SnapDown((w - gap) / 2f) : w;
            float end = Column(s, at, m.Left, actions, amounts, x, y, cw, bottom, keys);
            if (two)
            {
                end = Math.Max(end, Column(s, at, m.Right, actions, amounts, x + cw + gap, y, w - cw - gap, bottom, keys));
            }
            if (m.Below.Count > 0)
            {
                Strip(s, at, m.Below, x, end, w, bottom, keys);   // what fills the foot under both columns: related lists and meters, drawn only where they fit
            }
        }

        /// <summary>
        /// Bands side by side under a page's main part, in at most two rows, each row as high as its tallest band so the bottoms agree. The columns follow the
        /// width (two, three, or four on a wide one) and the bands are spread over the rows evenly, so a row never stands nearly empty beside a full one. A row
        /// that does not fit the room is left out whole. Returns the y under the last row laid.
        /// </summary>
        public static float Strip(UiScene s, UiAt at, List<WorkFormBand> bands, float x, float y, float w, float bottom, IUiKeys keys)
        {
            return Rows(s, at, bands, 0, bands == null ? 0 : bands.Count, x, y, w, bottom, ColumnsFor(w), 2, keys);
        }

        /// <summary>The columns of a row of bands for a width: wide enough for a band to read in two, three or four.</summary>
        public static int ColumnsFor(float w) => w >= 1400f ? 4 : w >= 880f ? 3 : 2;

        /// <summary>How many bands each row of <paramref name="n"/> holds under <paramref name="cols"/> columns: as few rows as fit, the bands shared evenly between them.</summary>
        public static int[] RowCounts(int n, int cols)
        {
            if (n <= 0)
            {
                return new int[0];
            }
            int rows = (n + cols - 1) / cols;
            var counts = new int[rows];
            for (int r = 0; r < rows; r++)
            {
                counts[r] = n / rows + (r < n % rows ? 1 : 0);
            }
            return counts;
        }

        // Bands [from, to) in balanced rows from y down; a row that does not fit, and the rows after it, are left out. Returns the y under the last row.
        private static float Rows(UiScene s, UiAt at, List<WorkFormBand> bands, int from, int to, float x, float y, float w, float bottom, int cols, int mostRows, IUiKeys keys)
        {
            float end = y;
            if (bands == null || to <= from)
            {
                return end;
            }
            int[] counts = RowCounts(to - from, Math.Min(cols, to - from));
            var none = new UiActionModel[0];
            var noAmounts = new WorkAmount[0];
            int at0 = from;
            for (int row = 0; row < counts.Length && row < mostRows; row++)
            {
                int n = counts[row];
                float gap = n == 2 ? UiTokens.S6 : UiTokens.S4, cw = UiDraw.SnapDown((w - gap * (n - 1)) / n), h = 0f;   // two bands stand under a form's two columns
                for (int k = 0; k < n; k++)
                {
                    h = Math.Max(h, Height(bands[at0 + k], cw));
                }
                if (y + h > bottom + 0.5f)
                {
                    if (bottom - y < LeastBand)
                    {
                        break;
                    }
                    h = bottom - y;   // a row that is a little too tall for the room stands as high as the room is, and each band shows the rows that fit
                }
                for (int k = 0; k < n; k++)
                {
                    float bx = x + k * (cw + gap);
                    Band(s, at, bands[at0 + k], none, noAmounts, bx, y, k == n - 1 ? x + w - bx : cw, h, keys);   // the last band reaches the right edge the list above reaches
                }
                end = y + h;
                y = end + UiTokens.S4;
                at0 += n;
            }
            return end;
        }

        /// <summary>
        /// The bands of a page that has a detail panel at its right. Plan one: the rows that fit stand beside the panel in the list's column while the panel runs on, and
        /// what is left stands across the whole width under both. Plan two, when plan one cannot lay every band whole (the rest would have no room under a panel that
        /// reaches low): all the bands in rows across the width under the lower of the list and the panel. <paramref name="y"/> is where the list's own parts end,
        /// <paramref name="panelBottom"/> the panel's foot (or its top where there is none). Returns the lowest y used.
        /// </summary>
        public static float Flow(UiScene s, UiAt at, List<WorkFormBand> bands, float x, float y, float w, float fullX, float fullW, float panelBottom, float bottom, IUiKeys keys)
        {
            if (bands == null || bands.Count == 0)
            {
                return y;
            }
            // plan one, measured before it is drawn
            int cols = ColumnsFor(w);
            // plan one, measured before it is drawn: first with the left rows ending no more than a little past the panel's foot, then with more room
            foreach (float slack in new[] { Slack, Slack * 2f })
            {
                int taken = 0;
                float py = y;
                while (taken < bands.Count && panelBottom > py)
                {
                    int n = Math.Min(cols, bands.Count - taken);
                    float h = RowHeight(bands, taken, n, w);
                    if (py + h > bottom + 0.5f || py + h > panelBottom + slack)
                    {
                        break;
                    }
                    py += h + UiTokens.S4;
                    taken += n;
                }
                if (!Fits(bands, taken, Math.Max(py, panelBottom + UiTokens.S4), fullW, bottom))
                {
                    continue;
                }
                float end = y;
                int next = 0;
                py = y;
                while (next < taken)
                {
                    int n = Math.Min(cols, taken - next);
                    float h = RowHeight(bands, next, n, w);
                    DrawRow(s, at, bands, next, n, x, py, w, h, keys);
                    end = py + h;
                    py = end + UiTokens.S4;
                    next += n;
                }
                if (taken < bands.Count)
                {
                    end = Math.Max(end, Rows(s, at, bands, taken, bands.Count, fullX, Math.Max(py, panelBottom + UiTokens.S4), fullW, bottom, ColumnsFor(fullW), 2, keys));
                }
                return end;
            }
            // plan two: every band in rows across the width, under the lower of the list and the panel
            return Rows(s, at, bands, 0, bands.Count, fullX, Math.Max(y, panelBottom + UiTokens.S4), fullW, bottom, ColumnsFor(fullW), 2, keys);
        }

        // Whether the bands from index first, laid in balanced rows across a width from y down (at most two rows), all stand whole above the bottom.
        private static bool Fits(List<WorkFormBand> bands, int first, float y, float w, float bottom)
        {
            if (first >= bands.Count)
            {
                return true;
            }
            int[] counts = RowCounts(bands.Count - first, Math.Min(ColumnsFor(w), bands.Count - first));
            int at0 = first;
            for (int row = 0; row < counts.Length && row < 2; row++)
            {
                y += RowHeight(bands, at0, counts[row], w);
                if (y > bottom + 0.5f)
                {
                    return false;
                }
                y += UiTokens.S4;
                at0 += counts[row];
            }
            return at0 == bands.Count;
        }

        // The height of a row of n bands from index first under a width: the tallest of them, so the bottoms agree.
        private static float RowHeight(List<WorkFormBand> bands, int first, int n, float w)
        {
            float gap = n == 2 ? UiTokens.S6 : UiTokens.S4, cw = UiDraw.SnapDown((w - gap * (n - 1)) / n), h = 0f;
            for (int k = 0; k < n; k++)
            {
                h = Math.Max(h, Height(bands[first + k], cw));
            }
            return h;
        }

        private static void DrawRow(UiScene s, UiAt at, List<WorkFormBand> bands, int first, int n, float x, float y, float w, float h, IUiKeys keys)
        {
            float gap = n == 2 ? UiTokens.S6 : UiTokens.S4, cw = UiDraw.SnapDown((w - gap * (n - 1)) / n);
            var none = new UiActionModel[0];
            var noAmounts = new WorkAmount[0];
            for (int k = 0; k < n; k++)
            {
                float bx = x + k * (cw + gap);
                Band(s, at, bands[first + k], none, noAmounts, bx, y, k == n - 1 ? x + w - bx : cw, h, keys);   // the last band reaches the right edge the list above reaches
            }
        }

        public const float LeastBand = 104f;   // px: a band's title and one tile, the least that is worth drawing

        /// <summary>How far past a detail panel's foot a row of bands in the list's column may end: a panel and its neighbour agree to within about two rows.</summary>
        public const float Slack = 96f;

        /// <summary>Bands one under another in a column down to the bottom, each as high as its rows; returns how many stood (a band that does not fit, and those after it, are left out).</summary>
        public static int Stack(UiScene s, UiAt at, List<WorkFormBand> bands, float x, float y, float w, float bottom, IUiKeys keys)
        {
            var none = new UiActionModel[0];
            var noAmounts = new WorkAmount[0];
            int stood = 0;
            for (int i = 0; bands != null && i < bands.Count; i++)
            {
                float h = Height(bands[i], w);
                if (y + h > bottom + 0.5f)
                {
                    break;
                }
                Band(s, at, bands[i], none, noAmounts, x, y, w, h, keys);
                y += h + UiTokens.S3;
                stood++;
            }
            return stood;
        }

        private static float Column(UiScene s, UiAt at, List<WorkFormBand> bands, UiActionModel[] actions, WorkAmount[] amounts, float x, float y, float w, float bottom, IUiKeys keys)
        {
            for (int i = 0; i < bands.Count; i++)
            {
                WorkFormBand b = bands[i];
                float room = bottom - y;
                float h = b.Grow ? room : Math.Min(room, Height(b, w));
                if (h < UiTokens.S12)
                {
                    break;
                }
                Band(s, at, b, actions, amounts, x, y, w, h, keys);
                y += h + UiTokens.S4;
            }
            return y;
        }

        public static float Height(WorkFormBand b, float w)
        {
            float h = UiTokens.S4 + (string.IsNullOrEmpty(b.Title) ? 0f : UiTokens.PairRow + UiTokens.S2);
            for (int i = 0; i < b.Rows.Count; i++)
            {
                h += RowHeight(b.Rows[i], w - UiTokens.S4 * 2f);
            }
            if (!string.IsNullOrEmpty(b.Note))
            {
                h += UiTextRules.WrappedHeight(b.Note, UiStyle.Small, w - UiTokens.S4 * 2f, 3) + UiTokens.S2;
            }
            if (b.Foot != null)
            {
                h += UiTokens.Control + UiTokens.S2;
            }
            return UiDraw.Snap(h + UiTokens.S2);
        }

        private static float RowHeight(WorkFormRow r, float w)
        {
            switch (r.Field)
            {
                case WorkField.Person: return 48f;
                case WorkField.Bar: return UiTokens.PairRow + UiTokens.S1;
                case WorkField.Label: return UiTokens.PairRow + UiTokens.S1;
                case WorkField.Text: return UiDraw.Snap(UiTextRules.WrappedHeight(r.Value ?? string.Empty, UiStyle.Body, w, 4) + UiTokens.S2);
                case WorkField.Tile: return 48f;
                case WorkField.Check: return UiTokens.PairRow + UiTokens.S1;
                case WorkField.Nudge: return UiTokens.Control + UiTokens.S1;
                default: return UiTokens.Control + UiTokens.S1;
            }
        }

        private static void Band(UiScene s, UiAt at, WorkFormBand b, UiActionModel[] actions, WorkAmount[] amounts, float x, float y, float w, float h, IUiKeys keys)
        {
            UiDraw.Box(s, at, x, y, w, h, UiRole.Band, UiRole.None, UiTokens.RadiusLarge, UiFlags.Decor);
            float ix = x + UiTokens.S4, iw = w - UiTokens.S4 * 2f, iy = y + UiTokens.S3, end = y + h - UiTokens.S2;
            if (!string.IsNullOrEmpty(b.Title))
            {
                iy += WorkPanel.SectionHead(s, at, ix, iy, iw, b.Title, b.Count) + UiTokens.S1;
            }
            _buttonWidth = 0f;   // the single buttons of a band stand in one width, so their left edges agree
            for (int i = 0; i < b.Rows.Count; i++)
            {
                WorkFormRow br = b.Rows[i];
                if (br.Field == WorkField.Button && br.More.Count == 0 && br.ActAt >= 0 && br.ActAt < actions.Length)
                {
                    UiActionModel ba = actions[br.ActAt];
                    _buttonWidth = Math.Max(_buttonWidth, UiDraw.Snap(Math.Min(iw * 0.5f, UiDraw.ButtonWidth(ba.Kind, ba.Label, ba.Icon, UiAction.Count, keys))));
                }
            }
            for (int i = 0; i < b.Rows.Count; i++)
            {
                float rh = RowHeight(b.Rows[i], iw);
                if (iy + rh > end - (b.Foot != null ? UiTokens.Control : 0f))
                {
                    break;
                }
                Row(s, at, b.Rows[i], actions, amounts, ix, iy, iw, rh, keys);
                iy += rh;
            }
            if (!string.IsNullOrEmpty(b.Note) && iy + UiTextRules.WrappedHeight(b.Note, UiStyle.Small, iw, 3) <= end - (b.Foot != null ? UiTokens.Control : 0f))
            {
                int id = UiDraw.Text(s, at, ix, iy + UiTokens.S1, iw, UiStyle.Small, UiRole.Text2, b.Note, UiFlags.Wrap | UiFlags.Truncate, 3);
                iy = UiDraw.Snap(s.Items[id].Y + s.Items[id].H) + UiTokens.S2;
            }
            if (b.Foot != null && b.FootAt >= 0 && b.FootAt < actions.Length)
            {
                UiActionModel a = actions[b.FootAt];
                float bw = UiDraw.Snap(Math.Min(iw, UiDraw.ButtonWidth(a.Kind, a.Label, a.Icon, UiAction.Count, keys)));
                if (!string.IsNullOrEmpty(b.FootWhy) && iw - bw - UiTokens.S3 > UiTokens.S16)
                {
                    UiDraw.TextMiddle(s, at, ix, y + h - UiTokens.S2 - UiTokens.Control, UiTokens.Control, iw - bw - UiTokens.S6, UiStyle.Small, UiRole.WarnText, b.FootWhy, UiFlags.Truncate);
                }
                UiDraw.Button(s, at, ix + iw - bw - UiTokens.S3, y + h - UiTokens.S2 - UiTokens.Control, bw, UiTokens.Control, a.Kind, a.Label,
                    a.Icon, UiAction.Count, keys, a.Command, a.Arg, a.Disabled);
            }
        }

        private static float _buttonWidth;

        private static void Row(UiScene s, UiAt at, WorkFormRow r, UiActionModel[] actions, WorkAmount[] amounts, float x, float y, float w, float h, IUiKeys keys)
        {
            switch (r.Field)
            {
                case WorkField.Label:
                    UiDraw.InfoRow(s, at, x, y, w, r.Label, r.Value);
                    break;
                case WorkField.Text:
                    UiDraw.Text(s, at, x, y, w, UiStyle.Body, UiRole.Text2, r.Value, UiFlags.Wrap | UiFlags.Truncate, 4);
                    break;
                case WorkField.Stepper:
                    if (r.Stepper >= 0 && r.Stepper < amounts.Length)
                    {
                        WorkAmount a = amounts[r.Stepper];
                        float sel = Math.Min(SelectorWidth, UiDraw.SnapDown(w * 0.7f));
                        float lw = w - (a.Words != null ? sel : ControlWidth + UiTokens.S3) - UiTokens.S3;
                        UiDraw.TextMiddle(s, at, x, y, UiTokens.Control, lw, UiStyle.Body, UiRole.Text, r.Label, UiFlags.Label | UiFlags.Truncate);
                        if (a.Words != null)
                        {
                            UiDraw.Selector(s, at, x + w - sel, y, sel, string.Empty, a.Figure, WorkCmd.Pick, r.Stepper);
                        }
                        else
                        {
                            UiDraw.Stepper(s, at, x + w - ControlWidth - UiTokens.S3, y, ControlWidth, a.Figure, a.CanLess, a.CanMore, WorkCmd.Less, WorkCmd.Type, WorkCmd.More, r.Stepper, false);
                        }
                    }
                    break;
                case WorkField.Switch:
                    UiDraw.TextMiddle(s, at, x, y, UiTokens.Control, w - UiTokens.S16, UiStyle.Body, UiRole.Text, r.Label, UiFlags.Label | UiFlags.Truncate);
                    if (r.ActAt >= 0 && r.ActAt < actions.Length)
                    {
                        UiActionModel a = actions[r.ActAt];
                        UiDraw.Switch(s, at, x + w - UiTokens.S16 - UiTokens.S3, y, UiTokens.S16, r.On, null, a.Command, a.Arg, a.Disabled);
                    }
                    break;
                case WorkField.Button:
                    if (r.ActAt >= 0 && r.ActAt < actions.Length)
                    {
                        UiActionModel a = actions[r.ActAt];
                        float used = 0f;
                        for (int k = 0; k <= r.More.Count; k++)
                        {
                            UiActionModel u = k == 0 ? a : r.ActAt + k < actions.Length ? actions[r.ActAt + k] : a;
                            used += (r.More.Count == 0 ? Math.Max(_buttonWidth, UiDraw.ButtonWidth(u.Kind, u.Label, u.Icon, UiAction.Count,
                                keys)) : UiDraw.ButtonWidth(u.Kind, u.Label, u.Icon, UiAction.Count, keys)) + (k > 0 ? UiTokens.S2 : 0f);
                        }
                        float lw = Math.Max(0f, w - used - UiTokens.S3);
                        float said = 0f;
                        if (!string.IsNullOrEmpty(r.Label))
                        {
                            said = Math.Min(lw, UiDraw.Snap(UiTextRules.Width(r.Label, UiStyle.Body) + UiTokens.S4));
                            UiStyle ink = UiTextRules.Width(r.Label,
                                UiStyle.Body) > lw ? UiStyle.Small : UiStyle.Body;   // a label the long buttons beside it leave no room for stands in the small cut
                            UiDraw.TextMiddle(s, at, x, y, UiTokens.Control, string.IsNullOrEmpty(r.Why) ? lw : said, ink, UiRole.Text, r.Label, UiFlags.Label | UiFlags.Truncate);
                        }
                        if (!string.IsNullOrEmpty(r.Why) && lw - said > UiTokens.S16)
                        {
                            UiDraw.TextMiddle(s, at, x + said, y, UiTokens.Control, lw - said, UiStyle.Small, UiRole.WarnText, r.Why, UiFlags.Truncate);   // why the button is dimmed, beside it
                        }
                        float right = x + w - UiTokens.S3;   // the buttons end where a choice field's chooser does
                        for (int k = r.More.Count; k >= 0; k--)
                        {
                            UiActionModel b = k == 0 ? a : r.ActAt + k < actions.Length ? actions[r.ActAt + k] : a;
                            float bw = UiDraw.Snap(Math.Min(right - x, r.More.Count == 0 ? Math.Max(_buttonWidth, UiDraw.ButtonWidth(b.Kind, b.Label, b.Icon,
                                UiAction.Count, keys)) : UiDraw.ButtonWidth(b.Kind, b.Label, b.Icon, UiAction.Count, keys)));
                            right -= bw;
                            UiDraw.Button(s, at, right, y, bw, UiTokens.Control, b.Kind, b.Label, b.Icon, UiAction.Count, keys, b.Command, b.Arg, b.Disabled);
                            right -= UiTokens.S2;
                        }
                    }
                    break;
                case WorkField.Person:
                    if (r.Person != null)
                    {
                        WorkPanel.PersonRow(s, at, r.Person, x, y, w);
                        if (r.ActAt >= 0 && r.ActAt < actions.Length)
                        {
                            UiActionModel a = actions[r.ActAt];
                            UiDraw.Node(s, at, x, y, w, h, a.Command, a.Arg, -1);
                        }
                    }
                    break;
                case WorkField.Nudge:
                    if (r.ActAt >= 0 && r.ActAt + 1 < actions.Length)
                    {
                        UiDraw.TextMiddle(s, at, x, y, UiTokens.Control, w - ControlWidth - UiTokens.S3, UiStyle.Body, UiRole.Text, r.Label, UiFlags.Label | UiFlags.Truncate);
                        UiDraw.Stepper(s, at, x + w - ControlWidth, y, ControlWidth, r.Value, !actions[r.ActAt].Disabled,
                            !actions[r.ActAt + 1].Disabled, WorkCmd.NudgeLess, WorkCmd.Read, WorkCmd.NudgeMore, r.ActAt, false);
                    }
                    break;
                case WorkField.Check:
                    UiDraw.CheckLine(s, at, x, y, w, r.Label, r.On);
                    break;
                case WorkField.Bar:
                    UiDraw.TextMiddle(s, at, x, y, UiTokens.PairRow, UiDraw.SnapDown(w * 0.34f), UiStyle.Body, UiRole.Text, r.Bar.Label, UiFlags.Truncate);
                    float label = UiDraw.SnapDown(w * 0.34f), value = UiTextRules.Width("100", UiStyle.BodyStrong) + UiTokens.S1;
                    UiDraw.Meter(s, at, x + label, y + (UiTokens.PairRow - UiTokens.S1) / 2f, UiDraw.SnapDown(w - label - value - UiTokens.S2), UiTokens.S1, r.Bar.Value, UiTokens.Grade(r.Bar.Value));
                    UiDraw.TextMiddle(s, at, x + w - value, y, UiTokens.PairRow, value, UiStyle.BodyStrong, UiTokens.Grade(r.Bar.Value),
                        r.Bar.Value.ToString(System.Globalization.CultureInfo.InvariantCulture), UiFlags.Right);
                    break;
                case WorkField.Tile:
                    float tile = UiTokens.ControlSmall, ty = y + (h - tile) / 2f;
                    int t = UiDraw.Tile(s, at, x, ty, tile, UiRole.None, false);
                    UiDraw.Icon(s, at.In(t), x + (tile - UiTokens.Icon) / 2f, ty + (tile - UiTokens.Icon) / 2f, UiTokens.Icon, r.Icon, UiRole.None, UiFlags.Middle | UiFlags.Centre, true);
                    // a leaving and its reference are long in some languages: the figure may take nearly half the row before the words on its left give way
                    float vw = string.IsNullOrEmpty(r.Value) ? 0f : Math.Min(w * 0.45f, UiTextRules.Width(r.Value, UiStyle.BodyStrong) + UiTokens.S1);
                    float tx = x + tile + UiTokens.S3, tw = w - tile - UiTokens.S3 - vw - (vw > 0f ? UiTokens.S2 : 0f);
                    float lh = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Title)), sh = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small)), top = y + (h - lh - sh) / 2f;
                    UiDraw.Text(s, at, tx, top, tw, UiStyle.Title, UiRole.Text, r.Label, UiFlags.Truncate | UiFlags.Middle);
                    UiDraw.Text(s, at, tx, top + lh, tw, UiStyle.Small, UiRole.Text2, r.Hint, UiFlags.Truncate | UiFlags.Middle);
                    if (vw > 0f)
                    {
                        UiDraw.TextMiddle(s, at, x + w - vw, y, h, vw, UiStyle.BodyStrong, UiRole.Text, r.Value, UiFlags.Right | UiFlags.Truncate);
                    }
                    break;
            }
        }
    }
}
