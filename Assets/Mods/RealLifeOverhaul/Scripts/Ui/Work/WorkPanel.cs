using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The chosen row's detail as the mockups draw it, one panel for every tab that has a table or a catalogue: its tile, its title and a chip, a
    /// grid of big figures, then sections of rows, check lines, graded bars, people and notes, and its buttons at the foot. What does not fit the
    /// room is left out from the last section up, never run over the foot. Every part is the kit's own. Game-free. Cost: a layout a change.
    /// </summary>
    public static class WorkPanel
    {
        public const float Least = 400f, Most = 480f, Share = 0.3f;
        private const float PersonLine = 48f;

        public static float WidthFor(float w) => Math.Max(Least, Math.Min(Most, UiDraw.SnapDown(w * Share)));

        /// <summary>Lays the panel into its box; the buttons come from the page's actions that are not chips.</summary>
        public static void Lay(UiScene s, UiAt at, WorkRow r, UiActionModel[] actions, float x, float y, float w, float h, IUiKeys keys, WorkAmount[] amounts, string why = null)
        {
            UiDraw.Box(s, at, x, y, w, h, UiRole.Band, UiRole.None, UiTokens.RadiusLarge, UiFlags.Decor);
            float ix = x + UiTokens.S4, iw = w - UiTokens.S4 * 2f, iy = y + UiTokens.S4;
            float footer = y + h - UiTokens.S4;
            bool buttons = false;
            for (int k = 0; k < actions.Length; k++)
            {
                buttons |= actions[k].Kind != UiButtonKind.Chip;
            }
            float whyH = buttons && !string.IsNullOrEmpty(why) ? UiTextRules.WrappedHeight(why, UiStyle.Small, iw, 2) + UiTokens.S2 : 0f;
            if (buttons)
            {
                footer -= UiTokens.Control + UiTokens.S3 + whyH;
            }
            iy = Head(s, at, r, ix, iy, iw);
            if (!string.IsNullOrEmpty(r.Sentence))
            {
                int id = UiDraw.Text(s, at, ix, iy, iw, UiStyle.Body, UiRole.Text2, r.Sentence, UiFlags.Wrap | UiFlags.Truncate, 3);
                iy = UiDraw.Snap(s.Items[id].Y + s.Items[id].H) + UiTokens.S3;
            }
            if (r.Figures.Count > 0)
            {
                iy = Figures(s, at, r, ix, iy, iw) + UiTokens.S3;
            }
            for (int i = 0; i < r.Sections.Count; i++)
            {
                float need = Height(r.Sections[i], iw);
                if (iy + need > footer)
                {
                    continue;   // a section that does not fit is left out whole, and a later, shorter one may still stand
                }
                iy = Section(s, at, r.Sections[i], ix, iy, iw, amounts, keys) + UiTokens.S3;
            }
            if (!string.IsNullOrEmpty(r.Hint) && iy + NoteHeight(r.HintLead, r.Hint, iw) <= footer)
            {
                Note(s, at, r.HintIcon, r.HintLead, r.Hint, ix, iy, iw);
            }
            if (buttons)
            {
                float by = y + h - UiTokens.S4 - UiTokens.Control, right = ix + iw;
                if (whyH > 0f)
                {
                    UiDraw.Text(s, at, ix, by - whyH, iw, UiStyle.Small, UiRole.WarnText, why, UiFlags.Wrap | UiFlags.Truncate, 2);
                }
                for (int k = 0; k < actions.Length; k++)
                {
                    UiActionModel a = actions[k];
                    if (a.Kind == UiButtonKind.Chip)
                    {
                        continue;
                    }
                    float bw = Math.Min(iw, UiDraw.ButtonWidth(a.Kind, a.Label, a.Icon, a.Key, keys));
                    right -= bw;
                    UiDraw.Button(s, at, right, by, bw, UiTokens.Control, a.Kind, a.Label, a.Icon, a.Key, keys, a.Command, a.Arg, a.Disabled);
                    right -= UiTokens.S3;
                }
            }
        }

        // The head: a tile with the thing's glyph (or the person's face), the title and a chip under it, the line the title stands on.
        private static float Head(UiScene s, UiAt at, WorkRow r, float x, float y, float w)
        {
            float face = UiTokens.IconTile;
            string title = string.IsNullOrEmpty(r.Title) ? r.Name : r.Title;
            if (!string.IsNullOrEmpty(r.Person))
            {
                UiDraw.Portrait(s, at, x, y, face, r.Person, UiColour.HueOf(r.Person), r.Ring);
            }
            else
            {
                int tile = UiDraw.Tile(s, at, x, y, face, UiRole.None, false);
                if (!string.IsNullOrEmpty(r.Picture))
                {
                    var item = new UiItem
                    {
                        Kind = UiKind.Picture, Flags = UiFlags.Middle | UiFlags.Centre, X = x + UiTokens.Hairline, Y = y + UiTokens.Hairline, W = face - UiTokens.Hairline * 2f,
                        H = face - UiTokens.Hairline * 2f, Text = r.Picture, Value = 1f, Clip = at.Clip, Node = -1, Layer = at.Layer, Order = at.Order, Parent = tile, Alpha = 1f
                    };
                    s.Add(ref item);
                }
                else
                {
                    UiDraw.Icon(s, at.In(tile), x + UiDraw.SnapDown((face - UiTokens.Icon) / 2f), y + UiDraw.SnapDown((face - UiTokens.Icon) / 2f), UiTokens.Icon, r.Icon, r.Tint,
                        UiFlags.Middle | UiFlags.Centre, true);
                }
            }
            float tx = x + face + UiTokens.S3, tw = w - face - UiTokens.S3;
            bool two = !string.IsNullOrEmpty(r.Sub) || !string.IsNullOrEmpty(r.Chip);
            float lh = UiTextRules.LineHeight(UiStyle.Number), sh = UiTextRules.LineHeight(UiStyle.Small);
            float top = y + (face - lh - (two ? sh + UiTokens.S1 : 0f)) / 2f;
            UiDraw.Text(s, at, tx, UiDraw.Snap(top), tw, UiStyle.Number, UiRole.Text, UiTextRules.Cut(title ?? string.Empty, UiStyle.Number, tw), UiFlags.Truncate | UiFlags.Middle);
            if (!string.IsNullOrEmpty(r.Chip))
            {
                float cw = UiDraw.Chip(s, at, tx, UiDraw.Snap(top + lh + UiTokens.S1), r.Chip, r.ChipTone, tw);
                if (!string.IsNullOrEmpty(r.Sub) && cw + UiTokens.S2 < tw)
                {
                    UiDraw.Text(s, at, tx + cw + UiTokens.S2, UiDraw.Snap(top + lh + UiTokens.S1), tw - cw - UiTokens.S2, UiStyle.Small, UiRole.Text2, r.Sub, UiFlags.Truncate | UiFlags.Middle);
                }
            }
            else if (!string.IsNullOrEmpty(r.Sub))
            {
                UiDraw.Text(s, at, tx, UiDraw.Snap(top + lh + UiTokens.S1), tw, UiStyle.Small, UiRole.Text2, r.Sub, UiFlags.Truncate | UiFlags.Middle);
            }
            return y + face + UiTokens.S4;
        }

        // The big figures, two to a row: their small capital label over the value, on a darker field.
        private static float Figures(UiScene s, UiAt at, WorkRow r, float x, float y, float w)
        {
            int n = r.Figures.Count, rows = (n + 1) / 2;
            float lh = UiTextRules.LineHeight(UiStyle.SectionHeader), vh = UiTextRules.LineHeight(UiStyle.Number);
            float cell = UiDraw.Snap(lh + vh + UiTokens.S1 + UiTokens.S2);
            float total = rows * cell + UiTokens.S3 * 2f + (rows - 1) * UiTokens.S1;
            UiDraw.Box(s, at, x, y, w, total, UiRole.Field, UiRole.None, UiTokens.RadiusLarge, UiFlags.Decor);
            float half = UiDraw.SnapDown((w - UiTokens.S4 * 2f) / 2f);
            for (int i = 0; i < n; i++)
            {
                float cx = x + UiTokens.S4 + (i % 2) * half, cy = y + UiTokens.S3 + (i / 2) * (cell + UiTokens.S1);
                UiDraw.Text(s, at, cx, cy, half - UiTokens.S2, UiStyle.SectionHeader, UiRole.Text2, r.Figures[i].Key, UiFlags.Label | UiFlags.Truncate);
                UiDraw.Text(s, at, cx, cy + lh + UiTokens.S1, half - UiTokens.S2, UiStyle.Number, UiRole.Text, r.Figures[i].Value, UiFlags.Truncate);
            }
            return y + total;
        }

        // ------------------------------------------------------------------------------------------------- sections

        public static float Height(WorkSection sec, float w)
        {
            float head = string.IsNullOrEmpty(sec.Title) ? 0f : UiTokens.PairRow + UiTokens.S1;
            switch (sec.Part)
            {
                case WorkPart.Pairs: return head + sec.Pairs.Count * (UiTokens.PairRow + UiTokens.S1);
                case WorkPart.Checks: return head + sec.Checks.Count * (UiTokens.PairRow + UiTokens.S1);
                case WorkPart.Bars: return head + sec.Bars.Count * (UiTokens.PairRow + UiTokens.S1);
                case WorkPart.People: return head + sec.People.Count * PersonLine;
                case WorkPart.Amount: return head + UiTokens.Control;
                case WorkPart.Note: return NoteHeight(sec.Lead, sec.Text, w);
                default: return head + UiTextRules.WrappedHeight(sec.Text ?? string.Empty, UiStyle.Body, w, 4);
            }
        }

        private static float Section(UiScene s, UiAt at, WorkSection sec, float x, float y, float w, WorkAmount[] amounts, IUiKeys keys)
        {
            if (sec.Part == WorkPart.Note)
            {
                Note(s, at, sec.NoteIcon, sec.Lead, sec.Text, x, y, w);
                return y + NoteHeight(sec.Lead, sec.Text, w);
            }
            if (!string.IsNullOrEmpty(sec.Title))
            {
                y += SectionHead(s, at, x, y, w, sec.Title, sec.Note);
            }
            switch (sec.Part)
            {
                case WorkPart.Pairs:
                    for (int i = 0; i < sec.Pairs.Count; i++)
                    {
                        y += UiDraw.InfoRow(s, at, x, y, w, sec.Pairs[i].Key, sec.Pairs[i].Value) + UiTokens.S1;
                    }
                    break;
                case WorkPart.Checks:
                    for (int i = 0; i < sec.Checks.Count; i++)
                    {
                        WorkNeed n = sec.Checks[i];
                        float have = string.IsNullOrEmpty(n.Have) ? 0f : Math.Min(w * 0.4f, UiTextRules.Width(n.Have, UiStyle.Body) + UiTokens.S2);
                        UiDraw.CheckLine(s, at, x, y, w - have - (have > 0f ? UiTokens.S2 : 0f), n.Words, n.Met);
                        if (have > 0f)
                        {
                            UiDraw.TextMiddle(s, at, x + w - have, y, UiTokens.PairRow, have, UiStyle.Body, UiRole.Text2, n.Have, UiFlags.Right | UiFlags.Truncate);
                        }
                        y += UiTokens.PairRow + UiTokens.S1;
                    }
                    break;
                case WorkPart.Bars:
                    float label = 0f;
                    for (int i = 0; i < sec.Bars.Count; i++)
                    {
                        label = Math.Max(label, UiTextRules.Width(sec.Bars[i].Label ?? string.Empty, UiStyle.Body));
                    }
                    label = Math.Min(UiDraw.Snap(label + UiTokens.S2), w * 0.34f);
                    float value = UiTextRules.Width("100", UiStyle.BodyStrong) + UiTextRules.Width("+00", UiStyle.Small) + UiTokens.S3;
                    for (int i = 0; i < sec.Bars.Count; i++)
                    {
                        WorkBar b = sec.Bars[i];
                        UiDraw.TextMiddle(s, at, x, y, UiTokens.PairRow, label, UiStyle.Body, UiRole.Text, b.Label, UiFlags.Truncate);
                        float bw = w - label - value - UiTokens.S2;
                        UiRole grade = UiTokens.Grade(b.Value);
                        UiDraw.Meter(s, at, x + label, y + (UiTokens.PairRow - UiTokens.S1) / 2f, bw, UiTokens.S1, b.Value, UiRole.Accent);
                        UiDraw.TextMiddle(s, at, x + w - value, y, UiTokens.PairRow, UiTextRules.Width("100", UiStyle.BodyStrong), UiStyle.BodyStrong, grade, b.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            UiFlags.Right);
                        if (!string.IsNullOrEmpty(b.Note))
                        {
                            UiDraw.TextMiddle(s, at, x + w - UiTextRules.Width("+00", UiStyle.Small) - UiTokens.S1, y, UiTokens.PairRow, UiTextRules.Width("+00", UiStyle.Small) + UiTokens.S1, UiStyle.Small,
                                UiRole.OkText, b.Note, UiFlags.Right);
                        }
                        y += UiTokens.PairRow + UiTokens.S1;
                    }
                    break;
                case WorkPart.People:
                    for (int i = 0; i < sec.People.Count; i++)
                    {
                        y += PersonRow(s, at, sec.People[i], x, y, w);
                    }
                    break;
                case WorkPart.Amount:
                    if (sec.Stepper >= 0 && amounts != null && sec.Stepper < amounts.Length)
                    {
                        WorkAmount a = amounts[sec.Stepper];
                        UiDraw.Stepper(s, at, x, y, w, a.Figure, a.CanLess, a.CanMore, WorkCmd.Less, WorkCmd.Type, WorkCmd.More, sec.Stepper, false);
                    }
                    y += UiTokens.Control;
                    break;
                default:
                    int id = UiDraw.Text(s, at, x, y, w, UiStyle.Body, UiRole.Text2, sec.Text, UiFlags.Wrap | UiFlags.Truncate, 4);
                    y = UiDraw.Snap(s.Items[id].Y + s.Items[id].H);
                    break;
            }
            return y;
        }

        /// <summary>A capitals label at the left and a quiet note at the right, the way a section opens.</summary>
        public static float SectionHead(UiScene s, UiAt at, float x, float y, float w, string title, string note)
        {
            float nw = string.IsNullOrEmpty(note) ? 0f : Math.Min(w * 0.5f, UiTextRules.Width(note, UiStyle.Small) + UiTokens.S1);
            UiDraw.TextMiddle(s, at, x, y, UiTokens.PairRow, w - nw - UiTokens.S2, UiStyle.SectionHeader, UiRole.Text2, title, UiFlags.Label | UiFlags.Truncate);
            if (nw > 0f)
            {
                UiDraw.TextMiddle(s, at, x + w - nw, y, UiTokens.PairRow, nw, UiStyle.Small, UiRole.Text2, note, UiFlags.Right | UiFlags.Truncate);
            }
            return UiTokens.PairRow + UiTokens.S1;
        }

        /// <summary>A person in a list: their face, name and a line under it, and what stands at the right (a date, a rating).</summary>
        public static float PersonRow(UiScene s, UiAt at, WorkPerson p, float x, float y, float w)
        {
            float face = UiTokens.AvatarRow;
            float ty = y + (PersonLine - face) / 2f;
            if (p.Icon != UiIcon.None)
            {
                int tile = UiDraw.Tile(s, at, x, ty, face, UiRole.None, false);
                UiDraw.Icon(s, at.In(tile), x + (face - UiTokens.Icon) / 2f, ty + (face - UiTokens.Icon) / 2f, UiTokens.Icon, p.Icon, UiRole.None, UiFlags.Middle | UiFlags.Centre, true);
            }
            else
            {
                UiDraw.Portrait(s, at, x, ty, face, p.Name, UiColour.HueOf(p.Name ?? string.Empty), p.Ring);
            }
            float right = string.IsNullOrEmpty(p.Right) ? 0f : Math.Min(w * 0.3f, UiTextRules.Width(p.Right, UiStyle.BodyStrong) + UiTokens.S1);
            float tx = x + face + UiTokens.S3, tw = w - face - UiTokens.S3 - right - (right > 0f ? UiTokens.S2 : 0f);
            float lh = UiTextRules.LineHeight(UiStyle.Title), sh = UiTextRules.LineHeight(UiStyle.Small);
            float top = y + (PersonLine - lh - sh) / 2f;
            UiDraw.Text(s, at, tx, top, tw, UiStyle.Title, UiRole.Text, p.Name, UiFlags.Truncate | UiFlags.Middle);
            UiDraw.Text(s, at, tx, top + lh, tw, UiStyle.Small, UiRole.Text2, p.Role, UiFlags.Truncate | UiFlags.Middle);
            if (right > 0f)
            {
                UiDraw.TextMiddle(s, at, x + w - right, y, PersonLine, right, UiStyle.BodyStrong, UiRole.Text, p.Right, UiFlags.Right | UiFlags.Truncate);
            }
            return PersonLine;
        }

        public static float NoteHeight(string lead, string text, float w)
        {
            float tw = w - UiTokens.S3 * 2f - UiTokens.S3 - (UiTokens.Control - UiTokens.S2);
            float h = (string.IsNullOrEmpty(lead) ? 0f : UiTextRules.LineHeight(UiStyle.BodyStrong) + UiTokens.S1) + UiTextRules.WrappedHeight(text ?? string.Empty, UiStyle.Body, tw, 4);
            return UiDraw.Snap(Math.Max(UiTokens.Control - UiTokens.S2, h) + UiTokens.S3 * 2f);
        }

        /// <summary>A quiet aside on a darker field after its glyph, the first words in the strong cut.</summary>
        public static void Note(UiScene s, UiAt at, UiIcon icon, string lead, string text, float x, float y, float w)
        {
            float h = NoteHeight(lead, text, w);
            UiDraw.Box(s, at, x, y, w, h, UiRole.Field, UiRole.None, UiTokens.RadiusLarge, UiFlags.Decor);
            float tile = UiTokens.Control - UiTokens.S2;
            int t = UiDraw.Tile(s, at, x + UiTokens.S3, y + UiTokens.S3, tile, UiRole.None, false);
            UiDraw.Icon(s, at.In(t), x + UiTokens.S3 + (tile - UiTokens.Icon) / 2f, y + UiTokens.S3 + (tile - UiTokens.Icon) / 2f, UiTokens.Icon, icon, UiRole.None,
                UiFlags.Middle | UiFlags.Centre, true);
            float tx = x + UiTokens.S3 * 2f + tile, tw = w - (tx - x) - UiTokens.S3, ty = y + UiTokens.S3;
            if (!string.IsNullOrEmpty(lead))
            {
                UiDraw.Text(s, at, tx, ty, tw, UiStyle.BodyStrong, UiRole.Text, lead, UiFlags.Truncate);
                ty += UiTextRules.LineHeight(UiStyle.BodyStrong) + UiTokens.S1;
            }
            UiDraw.Text(s, at, tx, ty, tw, UiStyle.Body, UiRole.Text2, text, UiFlags.Wrap | UiFlags.Truncate, 4);
        }
    }

    /// <summary>The command numbers of the work apps' own parts (a block nobody else uses: 1200 to 1219; the kit's are not unique across parts, so every handler tests the page it belongs to first).</summary>
    public static class WorkCmd
    {
        public const int Less = 1201, Type = 1202, More = 1203, Field = 1204, Search = 1205, Card = 1206, PageTab = 1207, Aside = 1208, Sort = 1209, Close = 1210, Locate = 1211, Pick = 1212, Diary = 1213, Day = 1214;
    }
}
