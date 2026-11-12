using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>A photo being edited: the preview picture, the filters, the frames and the shapes to cut to, the turn and the way out.</summary>
    public sealed class UiPhotoEditModel
    {
        public string Picture;            // the preview's picture key, or null while it is made
        public string Title;
        public string Caption;            // what the edit will do, in words ("Saved as a new photo; the original stays")
        public string FilterTitle, FrameTitle, ShapeTitle;
        public string[] Filters = new string[0];
        public int Filter;
        public string[] Frames = new string[0];
        public int Frame;
        public string[] Shapes = new string[0];
        public int Shape;
        public string TurnLabel;
        public string SaveLabel, CancelLabel, ResetLabel;
        public bool Busy;                 // the preview or the save is being made: Save waits
    }

    /// <summary>
    /// The Gallery's editor: the photo large at the left as the edit will make it, and at the right its filter, frame and shape as
    /// chips, a button to turn it, and Save as a new photo, Reset and Cancel. Commands: <see cref="Filter"/>, <see cref="Frame"/>,
    /// <see cref="Shape"/> (the chip's number), <see cref="Turn"/>, <see cref="Save"/>, <see cref="Reset"/>, <see cref="Cancel"/>. Game-free.
    /// Cost: a layout a change.
    /// </summary>
    public static class UiPhotoEdit
    {
        public const int Filter = 100, Frame = 101, Shape = 102, Turn = 103, Save = 104, Reset = 105, Cancel = 106;
        private const float Side = 360f;

        public static void Lay(UiScene s, UiAt at, UiPhotoEditModel m, float x, float y, float w, float h, IUiKeys keys)
        {
            float side = Math.Min(Side, UiDraw.SnapDown(w * 0.34f));
            float gap = UiTokens.S6;
            float photoW = w - side - gap;
            int tile = UiDraw.Box(s, at, x, y, photoW, h, UiRole.Tile, UiRole.None, UiTokens.RadiusMedium, UiFlags.Decor);
            if (!string.IsNullOrEmpty(m.Picture))
            {
                var item = new UiItem
                {
                    Kind = UiKind.Picture, Flags = UiFlags.Middle | UiFlags.Centre, X = x, Y = y, W = photoW, H = h, Text = m.Picture, Value = 1f,
                    Clip = at.Clip, Node = -1, Layer = at.Layer, Order = at.Order, Parent = tile, Alpha = 1f
                };
                s.Add(ref item);
            }
            else
            {
                float g = UiTokens.S16;
                UiDraw.Icon(s, at.In(tile), x + (photoW - g) / 2f, y + (h - g) / 2f, g, UiIcon.Image, UiRole.None, UiFlags.Middle | UiFlags.Centre, true);
            }
            float px = x + photoW + gap, cy = y;
            float foot = UiTokens.Control * 2f + UiTokens.S3 + (string.IsNullOrEmpty(m.Caption) ? 0f : UiTokens.PairRow * 2f);
            float room = h - foot;
            if (!string.IsNullOrEmpty(m.Title))
            {
                UiDraw.Text(s, at, px, cy, side, UiStyle.SectionHeader, UiRole.Muted, m.Title, UiFlags.Label | UiFlags.Truncate);
                cy += UiTokens.PairRow;
            }
            cy += Section(s, at, m.FilterTitle, m.Filters, m.Filter, Filter, px, cy, side, keys) + UiTokens.S3;
            cy += Section(s, at, m.FrameTitle, m.Frames, m.Frame, Frame, px, cy, side, keys) + UiTokens.S3;
            cy += Section(s, at, m.ShapeTitle, m.Shapes, m.Shape, Shape, px, cy, side, keys) + UiTokens.S3;
            if (!string.IsNullOrEmpty(m.TurnLabel))
            {
                float tw = UiDraw.ButtonWidth(UiButtonKind.Secondary, m.TurnLabel, UiIcon.Rotate, UiAction.Count, keys);
                UiDraw.Button(s, at, px, cy, Math.Min(tw, side), UiTokens.ControlSmall, UiButtonKind.Secondary, m.TurnLabel, UiIcon.Rotate, UiAction.Count, keys, Turn, 0);
                cy += UiTokens.ControlSmall + UiTokens.S3;
            }
            float fy = y + h - foot;
            if (cy > fy)
            {
                fy = cy;   // the chips ran long in a long translation: the buttons follow them
            }
            if (!string.IsNullOrEmpty(m.Caption))
            {
                UiDraw.Text(s, at, px, fy, side, UiStyle.Small, UiRole.Muted, m.Caption, UiFlags.Label | UiFlags.Wrap, 2);
                fy += UiTokens.PairRow * 2f;
            }
            float sw = UiDraw.ButtonWidth(UiButtonKind.Primary, m.SaveLabel ?? string.Empty, UiIcon.Check, UiAction.Alt, keys);
            UiDraw.Button(s, at, px, fy, Math.Min(side, Math.Max(sw, side)), UiTokens.Control, UiButtonKind.Primary, m.SaveLabel, UiIcon.Check, UiAction.Alt, keys, Save, 0, m.Busy);
            fy += UiTokens.Control + UiTokens.S3;
            float rw = Math.Min((side - UiTokens.S2) / 2f, UiDraw.ButtonWidth(UiButtonKind.Secondary, m.ResetLabel ?? string.Empty, UiIcon.Reset, UiAction.Count, keys));
            float cw = Math.Min(side - rw - UiTokens.S2, UiDraw.ButtonWidth(UiButtonKind.Secondary, m.CancelLabel ?? string.Empty, UiIcon.Close, UiAction.Back, keys));
            UiDraw.Button(s, at, px, fy, rw, UiTokens.Control, UiButtonKind.Secondary, m.ResetLabel, UiIcon.Reset, UiAction.Count, keys, Reset, 0);
            UiDraw.Button(s, at, px + rw + UiTokens.S2, fy, cw, UiTokens.Control, UiButtonKind.Secondary, m.CancelLabel, UiIcon.Close, UiAction.Back, keys, Cancel, 0);
        }

        // A title over chips that wrap; the chosen one filled. Returns the height.
        private static float Section(UiScene s, UiAt at, string title, string[] labels, int chosen, int command, float x, float y, float w, IUiKeys keys)
        {
            if (labels == null || labels.Length == 0)
            {
                return 0f;
            }
            float cy = y;
            if (!string.IsNullOrEmpty(title))
            {
                UiDraw.Text(s, at, x, cy, w, UiStyle.SectionHeader, UiRole.Muted, title, UiFlags.Label | UiFlags.Truncate);
                cy += UiTokens.PairRow;
            }
            float cx = x, h = UiTokens.Chip;
            for (int i = 0; i < labels.Length; i++)
            {
                bool on = i == chosen;
                float bw = Math.Min(w, UiDraw.ButtonWidth(on ? UiButtonKind.Primary : UiButtonKind.Chip, labels[i], UiIcon.None, UiAction.Count, keys));
                if (cx > x && cx + bw > x + w)
                {
                    cx = x;
                    cy += h + UiTokens.S2;
                }
                if (on)
                {
                    int face = UiDraw.Box(s, at, cx, cy, bw, h, UiRole.Accent, UiRole.None, UiTokens.RadiusMedium, UiFlags.Selected);
                    UiDraw.TextMiddle(s, at.In(face), cx, cy, h, bw, UiStyle.Button, UiRole.OnAccent, labels[i], UiFlags.Centre | UiFlags.Label | UiFlags.Truncate);
                    UiDraw.Node(s, at, cx, cy, bw, h, command, i, face);
                }
                else
                {
                    UiDraw.Button(s, at, cx, cy, bw, h, UiButtonKind.Chip, labels[i], UiIcon.None, UiAction.Count, keys, command, i);
                }
                cx += bw + UiTokens.S2;
            }
            return cy + h - y;
        }
    }
}
