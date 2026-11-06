using System;

namespace RealLifeOverhaul.Ui
{
    public static partial class UiDesignerPanels
    {
        public const float ItemsWidth = 520f, TimeWidth = 640f, BlueprintWidth = 640f, TypeWidth = 480f, NoteWidth = 400f;
        public const int ItemRows = 5;             // items in view in a list; the wheel moves them
        private const float ItemRow = UiTokens.Row;

        // ------------------------------------------------------------------ the item lists: Producer, Queue and Security

        private static float ItemsNoteHeight(UiDesignerItems m) =>
            (string.IsNullOrEmpty(m.Note) ? 0f : UiDraw.Snap(UiTextRules.WrappedHeight(m.Note, UiStyle.Body, ItemsWidth - UiTokens.S4 * 2f, 3)) + UiTokens.S2)
            + (m.Meter >= 0 ? UiTokens.PairRow + UiTokens.S2 : 0f);

        /// <summary>The item list's height: its strip, what it says, and the rows in view or the empty state.</summary>
        public static float ItemsHeight(UiDesignerItems m)
        {
            float list = m.Count == 0 ? UiDraw.EmptyStateHeight(ItemsWidth - UiTokens.S4 * 2f, m.EmptyTitle, m.EmptyBody, null) + UiTokens.S4 : Math.Min(m.Count, ItemRows) * ItemRow;
            return UiDraw.Snap(UiFrame.Strip + UiTokens.S3 + ItemsNoteHeight(m) + list + UiTokens.S2);
        }

        /// <summary>The first and the last item in view for a scroll, so the face has their pictures ready.</summary>
        public static bool ItemsVisible(int count, float scroll, out int first, out int last)
        {
            first = last = 0;
            if (count <= 0)
            {
                return false;
            }
            first = Math.Max(0, Math.Min((int)(scroll / ItemRow + 0.5f), Math.Max(0, count - ItemRows)));
            last = Math.Min(count - 1, first + ItemRows - 1);
            return true;
        }

        /// <summary>The scroll an item list takes: a whole row, never past the last.</summary>
        public static float ItemsScrolled(int count, float scroll) => Math.Max(0f, Math.Min((float)Math.Round(scroll / ItemRow) * ItemRow, Math.Max(0, count - ItemRows) * ItemRow));

        /// <summary>One row of the list: how far the wheel moves it a notch.</summary>
        public static float ItemStep => ItemRow;

        private static void Items(UiScene s, UiAt at, UiDesignerItems m, float x, float y, float w, float h)
        {
            int panel = UiDraw.Box(s, at, x, y, w, h, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt on = at.In(panel);
            float cy = UiDraw.TitleStrip(s, on, x, y, w, m.Icon, m.Title, false) + UiTokens.S3;
            float ix = x + UiTokens.S4, iw = w - UiTokens.S4 * 2f;
            if (!string.IsNullOrEmpty(m.Note))
            {
                int lines = 3;
                int note = UiDraw.Text(s, on, ix, cy, iw, UiStyle.Body, m.NoteInk, m.Note, UiFlags.Wrap | UiFlags.Truncate, lines);
                cy += UiDraw.Snap(s.Items[note].H) + UiTokens.S2;
            }
            if (m.Meter >= 0)
            {
                UiDraw.Meter(s, on, ix, cy + (UiTokens.PairRow - UiTokens.Segment) / 2f, iw - 96f - UiTokens.S3, UiTokens.Segment, m.Meter, m.MeterTone);
                UiDraw.TextMiddle(s, on, ix + iw - 96f, cy, UiTokens.PairRow, 96f, UiStyle.BodyStrong, UiRole.Text, m.MeterWords, UiFlags.Right | UiFlags.Truncate);
                cy += UiTokens.PairRow + UiTokens.S2;
            }
            if (m.Count == 0)
            {
                UiDraw.EmptyState(s, on, ix, cy, iw, y + h - cy - UiTokens.S2, m.Icon, m.EmptyTitle, m.EmptyBody);
                return;
            }
            float view = Math.Min(m.Count, ItemRows) * ItemRow, content = m.Count * ItemRow;
            float offset = ItemsScrolled(m.Count, m.Scroll);
            m.Scroll = offset;
            short clip = s.AddClip(x, cy, w, view, offset);
            s.Clips[clip].ContentHeight = content;
            var inClip = new UiAt { Parent = -1, Clip = clip, Layer = on.Layer, Order = -1, Region = on.Region };
            int first = (int)(offset / ItemRow + 0.5f), last = Math.Min(m.Count - 1, first + ItemRows - 1), chosen = -1;
            for (int i = 0; i < m.Count; i++)
            {
                chosen = m.Items[i].Chosen ? i : chosen;
            }
            short list = s.AddList(m.Count, first, last - first + 1, clip, chosen);
            for (int i = first; i <= last; i++)
            {
                ItemRowAt(s, inClip, ref m.Items[i], x, i * ItemRow, w, list, i);
            }
            UiDraw.ScrollThumb(s, on, x + w - UiTokens.S2, cy, view, offset, content);
        }

        // An item of the building: its picture on a tile with its state over the corner, its name over where it stands, and the row on the cyan
        // fifth while it is the one the overlay is open on.
        private static void ItemRowAt(UiScene s, UiAt at, ref UiDesignerItem d, float x0, float y, float w, short list, int index)
        {
            float x = 0f;
            int face = UiDraw.Box(s, at, x, y, w - UiTokens.S4, ItemRow, d.Chosen ? UiRole.RowSel : UiRole.None, UiRole.None, 0f, d.Chosen ? UiFlags.Selected : UiFlags.None);
            UiAt on = at.In(face);
            float tile = UiTokens.IconTile, tx = x + UiTokens.S4, ty = y + (ItemRow - tile) / 2f;
            int t = UiDraw.Tile(s, on, tx, ty, tile, UiRole.None, false);
            if (!string.IsNullOrEmpty(d.Picture))
            {
                Picture(s, on.In(t), d.Picture, tx + UiTokens.S1, ty + UiTokens.S1, tile - UiTokens.S2, tile - UiTokens.S2);
            }
            float nx = tx + tile + UiTokens.S3;
            float gw = d.Glyph != UiIcon.None ? UiTokens.Icon + UiTokens.S2 : 0f, nw = x + w - UiTokens.S4 * 2f - nx - gw;
            float nh = Lh(UiStyle.BodyStrong), lh = Lh(UiStyle.Small), by = y + (ItemRow - nh - lh) / 2f;
            UiDraw.Text(s, on, nx, by, nw, UiStyle.BodyStrong, UiRole.Text, d.Name, UiFlags.Truncate | UiFlags.Middle);
            UiDraw.Text(s, on, nx, by + nh, nw, UiStyle.Small, UiRole.Text, d.Line, UiFlags.Truncate | UiFlags.Middle);
            if (d.Glyph != UiIcon.None)
            {
                UiDraw.Icon(s, on, x + w - UiTokens.S4 * 2f - UiTokens.Icon, y + (ItemRow - UiTokens.Icon) / 2f, UiTokens.Icon, d.Glyph, d.GlyphTone, UiFlags.Middle);
            }
            UiDraw.Node(s, at, x, y, w - UiTokens.S4, ItemRow, ItemCommand, d.Index, face, false, list, index);
        }

        // ------------------------------------------------------------------ time of day

        /// <summary>The time of day tool's height.</summary>
        public static float TimeHeight(UiDesignerTime t) =>
            UiDraw.Snap(UiFrame.Strip + UiTokens.S4 + Lh(UiStyle.Display) + UiTokens.S3 + UiTokens.Control + UiTokens.S1 + Lh(UiStyle.Small) + UiTokens.S4
                        + UiDraw.Snap(UiTextRules.WrappedHeight(t.Note ?? string.Empty, UiStyle.Body, TimeWidth - UiTokens.S4 * 2f, 3)) + UiTokens.S4);

        /// <summary>Where the time slider stands for the face: its left edge and its width in px, to turn the pointer's place across it into a time.</summary>
        public static void TimeSlider(UiDesignerModel m, UiFrame frame, out float x, out float w)
        {
            PanelBox(m, frame, out float px, out _, out float pw, out _);
            float ix = px + UiTokens.S4, iw = pw - UiTokens.S4 * 2f;
            float bw = Math.Max(UiDraw.Snap(UiTextRules.Width(m.Time.MoreLabel ?? "+10", UiStyle.Button) + UiTokens.S5 * 2f), UiTokens.S16 - UiTokens.S4);
            x = ix + bw + UiTokens.S3;
            w = iw - (bw + UiTokens.S3) * 2f;
        }

        // The time of day: the light's hour large, what the game's clock stays at, a slider across the day between a step back and a step on,
        // the day's hours under it, and what the tool is for. Nothing here is saved.
        private static void Time(UiScene s, UiAt at, UiDesignerTime t, float x, float y, float w, float h)
        {
            int panel = UiDraw.Box(s, at, x, y, w, h, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt on = at.In(panel);
            float cy = UiDraw.TitleStrip(s, on, x, y, w, UiIcon.Sun, t.Title, false);
            if (!string.IsNullOrEmpty(t.Sub))
            {
                float sw = Math.Min(UiDraw.Snap(UiTextRules.Width(t.Sub, UiStyle.Small) + UiTokens.S1), w * 0.4f);
                UiDraw.TextMiddle(s, on, x + w - UiTokens.S4 - sw, y, UiFrame.Strip, sw, UiStyle.Small, UiRole.Text2, t.Sub, UiFlags.Right | UiFlags.Truncate | UiFlags.Chrome);
            }
            float ix = x + UiTokens.S4, iw = w - UiTokens.S4 * 2f;
            cy += UiTokens.S4;
            float clockW = Math.Min(UiDraw.Snap(UiTextRules.Width(t.Time ?? string.Empty, UiStyle.Display) + UiTokens.S2), iw * 0.4f);
            UiDraw.Text(s, on, ix, cy, clockW, UiStyle.Display, UiRole.Text, t.Time, UiFlags.Truncate);
            UiDraw.Text(s, on, ix + clockW + UiTokens.S4, cy + Lh(UiStyle.Display) - Lh(UiStyle.Small) - UiTokens.S1, iw - clockW - UiTokens.S4, UiStyle.Small, UiRole.Text2, t.Clock,
                UiFlags.Right | UiFlags.Truncate);
            cy += Lh(UiStyle.Display) + UiTokens.S3;
            float bw = UiDraw.Snap(UiTextRules.Width(t.MoreLabel ?? "+10", UiStyle.Button) + UiTokens.S5 * 2f);
            bw = Math.Max(bw, UiTokens.S16 - UiTokens.S4);
            UiDraw.Button(s, on, ix, cy, bw, UiTokens.Control, UiButtonKind.Secondary, t.LessLabel ?? "-10", UiIcon.None, UiAction.Count, null, TimeLessCommand, 0, t.LessOff);
            UiDraw.Button(s, on, ix + iw - bw, cy, bw, UiTokens.Control, UiButtonKind.Secondary, t.MoreLabel ?? "+10", UiIcon.None, UiAction.Count, null, TimeMoreCommand, 0, t.MoreOff);
            float sx = ix + bw + UiTokens.S3, sw2 = iw - (bw + UiTokens.S3) * 2f;
            UiDraw.Slider(s, on, sx, cy, sw2, t.Figure ?? string.Empty, t.Share, TimeSliderCommand, 0, false);
            cy += UiTokens.Control + UiTokens.S1;
            if (t.Marks != null && t.MarkShares != null)
            {
                // The hours stand under the track, each where the slider's knob would stand for it: the track is the slider's width less its figure.
                float fw = Math.Max(UiDraw.Snap(UiTextRules.Width(t.Figure ?? string.Empty, UiStyle.BodyStrong) + UiTokens.S1), UiDraw.SnapDown(sw2 * 0.22f));
                float tx = sx + 8f, tw = Math.Max(UiTokens.S8, sw2 - fw - UiTokens.S4 - 8f);
                for (int i = 0; i < t.Marks.Length && i < t.MarkShares.Length; i++)
                {
                    float mx = tx + tw * t.MarkShares[i];
                    float mw = UiDraw.Snap(UiTextRules.Width(t.Marks[i], UiStyle.Small) + UiTokens.S1);
                    UiDraw.Text(s, on, UiDraw.SnapDown(mx - mw / 2f), cy, mw, UiStyle.Small, UiRole.Muted, t.Marks[i], UiFlags.Centre | UiFlags.Share | UiFlags.Truncate);
                }
            }
            cy += Lh(UiStyle.Small) + UiTokens.S4;
            UiDraw.Box(s, on, ix, cy - UiTokens.S2, iw, UiTokens.Hairline, UiRole.Rule, UiRole.None, 0f, UiFlags.Decor);
            UiDraw.Text(s, on, ix, cy + UiTokens.S1, iw, UiStyle.Body, UiRole.Text2, t.Note, UiFlags.Wrap | UiFlags.Truncate, 3);
        }

        // ------------------------------------------------------------------ save blueprint

        /// <summary>The blueprint panel's height.</summary>
        public static float BlueprintHeight(UiDesignerBlueprint b)
        {
            float h = UiFrame.Strip + UiTokens.S4 + UiTokens.PairRow + UiTokens.Control + UiTokens.S1 + Lh(UiStyle.Small) + UiTokens.S4;
            h += UiTokens.PairRow + ((b.FactCount + 1) / 2) * UiTokens.PairRow + UiTokens.S3;
            h += b.CheckCount * UiTokens.PairRow + UiTokens.S3;
            h += UiTokens.Control + UiTokens.S4;
            return UiDraw.Snap(h);
        }

        // Save blueprint: the name to save under, what the blueprint holds, what must be true to save it and the one button that saves.
        private static void Blueprint(UiScene s, UiAt at, UiDesignerBlueprint b, float x, float y, float w, float h, IUiKeys keys)
        {
            int panel = UiDraw.Box(s, at, x, y, w, h, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt on = at.In(panel);
            float cy = UiDraw.TitleStrip(s, on, x, y, w, UiIcon.Layout, b.Title, false);
            if (!string.IsNullOrEmpty(b.Sub))
            {
                float sw = Math.Min(UiDraw.Snap(UiTextRules.Width(b.Sub, UiStyle.Small) + UiTokens.S1), w * 0.4f);
                UiDraw.TextMiddle(s, on, x + w - UiTokens.S4 - sw, y, UiFrame.Strip, sw, UiStyle.Small, UiRole.Text2, b.Sub, UiFlags.Right | UiFlags.Truncate | UiFlags.Chrome);
            }
            float ix = x + UiTokens.S4, iw = w - UiTokens.S4 * 2f;
            cy += UiTokens.S4;
            UiDraw.TextMiddle(s, on, ix, cy, UiTokens.PairRow, iw, UiStyle.Small, UiRole.Text2, b.NameLabel, UiFlags.Label | UiFlags.Truncate);
            cy += UiTokens.PairRow;
            UiDraw.TextField(s, on, ix, cy, iw, b.Name, b.NamePlaceholder, NameCommand, 0, false);
            cy += UiTokens.Control + UiTokens.S1;
            UiDraw.Text(s, on, ix, cy, iw, UiStyle.Small, b.NameBad ? UiRole.WarnText : UiRole.Muted, b.NameHint, UiFlags.Truncate);
            cy += Lh(UiStyle.Small) + UiTokens.S4;
            UiDraw.TextMiddle(s, on, ix, cy, UiTokens.PairRow, iw, UiStyle.SectionHeader, UiRole.Text2, b.FactsTitle, UiFlags.Label | UiFlags.Truncate);
            cy += UiTokens.PairRow;
            float col = UiDraw.SnapDown((iw - UiTokens.S6) / 2f);
            for (int i = 0; i < b.FactCount; i++)
            {
                float fx = ix + (i % 2) * (col + UiTokens.S6), fy = cy + (i / 2) * UiTokens.PairRow;
                UiDraw.InfoRow(s, on, fx, fy, col, b.FactLabels[i], b.FactValues[i], false, -1, 0, UiRole.Text);
            }
            cy += ((b.FactCount + 1) / 2) * UiTokens.PairRow + UiTokens.S3;
            for (int i = 0; i < b.CheckCount; i++)
            {
                UiDraw.Icon(s, on, ix + UiTokens.S3, cy + (UiTokens.PairRow - UiTokens.Icon) / 2f, UiTokens.Icon, b.CheckMet[i] ? UiIcon.Check : UiIcon.Warning, UiRole.None, UiFlags.Middle, true);
                float sideW = Math.Min(UiDraw.Snap(UiTextRules.Width(b.CheckSide[i] ?? string.Empty, UiStyle.Small) + UiTokens.S1), iw * 0.5f);
                UiDraw.TextMiddle(s, on, ix + UiTokens.S3 + UiTokens.Icon + UiTokens.S2, cy, UiTokens.PairRow, iw - UiTokens.S3 - UiTokens.Icon - UiTokens.S2 - sideW - UiTokens.S3, UiStyle.Body,
                    UiRole.Text, b.CheckWords[i], UiFlags.Label | UiFlags.Truncate);
                UiDraw.TextMiddle(s, on, ix + iw - sideW, cy, UiTokens.PairRow, sideW, UiStyle.Small, UiRole.Muted, b.CheckSide[i], UiFlags.Right | UiFlags.Truncate);
                cy += UiTokens.PairRow;
            }
            cy += UiTokens.S3;
            float bw = Math.Min(iw * 0.4f, UiDraw.Snap(UiDraw.ButtonWidth(UiButtonKind.Primary, b.SaveLabel ?? string.Empty, UiIcon.None, UiAction.Count, keys) + UiTokens.S4));
            float footW = iw - bw - UiTokens.S4;
            UiDraw.Icon(s, on, ix, cy + (UiTokens.Control - UiTokens.Icon) / 2f, UiTokens.Icon, UiIcon.Home, UiRole.Text2, UiFlags.Middle);
            UiDraw.Text(s, on, ix + UiTokens.Icon + UiTokens.S2, cy + (UiTokens.Control - Lh(UiStyle.Small) * 2f) / 2f, footW - UiTokens.Icon - UiTokens.S2, UiStyle.Small, UiRole.Text2, b.Foot,
                UiFlags.Wrap | UiFlags.Truncate | UiFlags.Middle, 2);
            UiDraw.Button(s, on, ix + iw - bw, cy, bw, UiTokens.Control, UiButtonKind.Chosen, b.SaveLabel ?? string.Empty, UiIcon.None, UiAction.Count, keys, SaveCommand, 0, b.SaveOff);
        }

        // ------------------------------------------------------------------ the business's kind, asked as a blueprint is saved from a unit with none

        /// <summary>The business type question's height.</summary>
        public static float TypeHeight(UiDesignerBusinessType t) =>
            UiDraw.Snap(UiFrame.Strip + UiTokens.S4 + UiDraw.Snap(UiTextRules.WrappedHeight(t.Body ?? string.Empty, UiStyle.Body, TypeWidth - UiTokens.S4 * 2f, 3)) + UiTokens.S3
                        + UiTokens.Control + (t.Open ? UiDraw.ChoiceListHeight(t.Count) + UiTokens.S1 : 0f) + UiTokens.S4 + UiTokens.Control + UiTokens.S4);

        private static void BusinessType(UiScene s, UiAt at, UiDesignerBusinessType t, float x, float y, float w, float h, IUiKeys keys)
        {
            int panel = UiDraw.Box(s, at, x, y, w, h, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt on = at.In(panel);
            float cy = UiDraw.TitleStrip(s, on, x, y, w, UiIcon.Building, t.Title, false) + UiTokens.S4;
            float ix = x + UiTokens.S4, iw = w - UiTokens.S4 * 2f;
            int body = UiDraw.Text(s, on, ix, cy, iw, UiStyle.Body, UiRole.Text, t.Body, UiFlags.Wrap | UiFlags.Truncate, 3);
            cy += UiDraw.Snap(s.Items[body].H) + UiTokens.S3;
            string chosen = t.Chosen >= 0 && t.Chosen < t.Count ? t.Options[t.Chosen] : string.Empty;
            UiDraw.Choice(s, on, ix, cy, iw, chosen, TypeFieldCommand, 0, false);
            float fieldY = cy;
            cy += UiTokens.Control + UiTokens.S4;
            if (t.Open)
            {
                cy += UiDraw.ChoiceListHeight(t.Count) + UiTokens.S1;
            }
            float half = UiDraw.SnapDown((iw - UiTokens.S2) / 2f);
            UiDraw.Button(s, on, ix, cy, half, UiTokens.Control, UiButtonKind.Secondary, t.CancelLabel ?? string.Empty, UiIcon.None, UiAction.Count, keys, TypeCancelCommand, 0);
            UiDraw.Button(s, on, ix + half + UiTokens.S2, cy, iw - half - UiTokens.S2, UiTokens.Control, UiButtonKind.Chosen, t.ContinueLabel ?? string.Empty, UiIcon.None, UiAction.Count, keys,
                TypeContinueCommand, 0, t.Chosen < 0);
            if (t.Open)
            {
                UiDraw.ChoiceList(s, on, ix, fieldY + UiTokens.Control + UiTokens.S1, iw, t.Options, t.Count, t.Chosen, t.Offset, TypeOptionCommand);
            }
        }

        // ------------------------------------------------------------------ a tool without a panel of its own

        /// <summary>The note card's height.</summary>
        public static float NoteHeight(UiDesignerNote n) =>
            UiDraw.Snap(UiFrame.Strip + UiTokens.S3 + UiDraw.Snap(UiTextRules.WrappedHeight(n.Body ?? string.Empty, UiStyle.Body, NoteWidth - UiTokens.S4 * 2f, 4)) + UiTokens.S4);

        private static void Note(UiScene s, UiAt at, UiDesignerNote n, float x, float y, float w, float h)
        {
            int panel = UiDraw.Box(s, at, x, y, w, h, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt on = at.In(panel);
            float cy = UiDraw.TitleStrip(s, on, x, y, w, n.Icon, n.Title, false) + UiTokens.S3;
            UiDraw.Text(s, on, x + UiTokens.S4, cy, w - UiTokens.S4 * 2f, UiStyle.Body, UiRole.Text2, n.Body, UiFlags.Wrap | UiFlags.Truncate, 4);
        }
    }
}
