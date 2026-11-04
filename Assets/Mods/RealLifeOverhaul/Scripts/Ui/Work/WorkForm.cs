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
            Column(s, at, m.Left, actions, amounts, x, y, cw, bottom, keys);
            if (two)
            {
                Column(s, at, m.Right, actions, amounts, x + cw + gap, y, w - cw - gap, bottom, keys);
            }
        }

        private static void Column(UiScene s, UiAt at, List<WorkFormBand> bands, UiActionModel[] actions, WorkAmount[] amounts, float x, float y, float w, float bottom, IUiKeys keys)
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
                case WorkField.Text: return UiTextRules.WrappedHeight(r.Value ?? string.Empty, UiStyle.Body, w, 4) + UiTokens.S2;
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
                float bw = Math.Min(iw, UiDraw.ButtonWidth(a.Kind, a.Label, a.Icon, UiAction.Count, keys));
                UiDraw.Button(s, at, ix + iw - bw, y + h - UiTokens.S2 - UiTokens.Control, bw, UiTokens.Control, a.Kind, a.Label, a.Icon, UiAction.Count, keys, a.Command, a.Arg, a.Disabled);
            }
        }

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
                        float lw = w - (a.Words != null ? sel : ControlWidth) - UiTokens.S3;
                        UiDraw.TextMiddle(s, at, x, y, UiTokens.Control, lw, UiStyle.Body, UiRole.Text, r.Label, UiFlags.Label | UiFlags.Truncate);
                        if (a.Words != null)
                        {
                            UiDraw.Selector(s, at, x + w - sel, y, sel, string.Empty, a.Figure, WorkCmd.Pick, r.Stepper);
                        }
                        else
                        {
                            UiDraw.Stepper(s, at, x + w - ControlWidth, y, ControlWidth, a.Figure, a.CanLess, a.CanMore, WorkCmd.Less, WorkCmd.Type, WorkCmd.More, r.Stepper, false);
                        }
                    }
                    break;
                case WorkField.Switch:
                    UiDraw.TextMiddle(s, at, x, y, UiTokens.Control, w - UiTokens.S16, UiStyle.Body, UiRole.Text, r.Label, UiFlags.Label | UiFlags.Truncate);
                    if (r.ActAt >= 0 && r.ActAt < actions.Length)
                    {
                        UiActionModel a = actions[r.ActAt];
                        UiDraw.Switch(s, at, x + w - UiTokens.S16, y, UiTokens.S16, r.On, null, a.Command, a.Arg, a.Disabled);
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
                            used += UiDraw.ButtonWidth(u.Kind, u.Label, u.Icon, UiAction.Count, keys) + (k > 0 ? UiTokens.S2 : 0f);
                        }
                        float lw = Math.Max(0f, w - used - UiTokens.S3);
                        if (!string.IsNullOrEmpty(r.Label))
                        {
                            UiDraw.TextMiddle(s, at, x, y, UiTokens.Control, lw, UiStyle.Body, UiRole.Text, r.Label, UiFlags.Label | UiFlags.Truncate);
                        }
                        float right = x + w;
                        for (int k = r.More.Count; k >= 0; k--)
                        {
                            UiActionModel b = k == 0 ? a : r.ActAt + k < actions.Length ? actions[r.ActAt + k] : a;
                            float bw = Math.Min(right - x, UiDraw.ButtonWidth(b.Kind, b.Label, b.Icon, UiAction.Count, keys));
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
                case WorkField.Input:
                    UiDraw.TextMiddle(s, at, x, y, UiTokens.Control, UiDraw.SnapDown(w * 0.38f), UiStyle.Body, UiRole.Text, r.Label, UiFlags.Label | UiFlags.Truncate);
                    UiDraw.TextField(s, at, x + UiDraw.SnapDown(w * 0.4f), y, w - UiDraw.SnapDown(w * 0.4f), r.Value, r.Hint, WorkCmd.Field, Math.Max(0, r.Field2), false);
                    break;
                case WorkField.Bar:
                    UiDraw.TextMiddle(s, at, x, y, UiTokens.PairRow, UiDraw.SnapDown(w * 0.34f), UiStyle.Body, UiRole.Text, r.Bar.Label, UiFlags.Truncate);
                    float label = UiDraw.SnapDown(w * 0.34f), value = UiTextRules.Width("100", UiStyle.BodyStrong) + UiTokens.S1;
                    UiDraw.Meter(s, at, x + label, y + (UiTokens.PairRow - UiTokens.S1) / 2f, w - label - value - UiTokens.S2, UiTokens.S1, r.Bar.Value, UiTokens.Grade(r.Bar.Value));
                    UiDraw.TextMiddle(s, at, x + w - value, y, UiTokens.PairRow, value, UiStyle.BodyStrong, UiTokens.Grade(r.Bar.Value),
                        r.Bar.Value.ToString(System.Globalization.CultureInfo.InvariantCulture), UiFlags.Right);
                    break;
                case WorkField.Tile:
                    float tile = UiTokens.ControlSmall, ty = y + (h - tile) / 2f;
                    int t = UiDraw.Tile(s, at, x, ty, tile, UiRole.None, false);
                    UiDraw.Icon(s, at.In(t), x + (tile - UiTokens.Icon) / 2f, ty + (tile - UiTokens.Icon) / 2f, UiTokens.Icon, r.Icon, UiRole.None, UiFlags.Middle | UiFlags.Centre, true);
                    float vw = string.IsNullOrEmpty(r.Value) ? 0f : Math.Min(w * 0.3f, UiTextRules.Width(r.Value, UiStyle.BodyStrong) + UiTokens.S1);
                    float tx = x + tile + UiTokens.S3, tw = w - tile - UiTokens.S3 - vw - (vw > 0f ? UiTokens.S2 : 0f);
                    float lh = UiTextRules.LineHeight(UiStyle.Title), sh = UiTextRules.LineHeight(UiStyle.Small), top = y + (h - lh - sh) / 2f;
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
