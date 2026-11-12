using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// What a spoken line shows: who says it (their picture or a painted face), the line, where it stands among the lines
    /// the game holds, how far it has been said, and the buttons that end it.
    /// </summary>
    public sealed class UiMonologueModel
    {
        public string Name;          // who says it, where the game names them; null for the picture alone, as the game's own card
        public string Picture;       // their picture as the game holds it (its key, UiPictures); null for a painted face by the name
        public float Hue;
        public string Line;
        public string More;          // "1 of 2" while the game holds more lines in this run; null for a line alone
        public int Progress = -1;    // how far the line has been said, 0 to 100; -1 where that is not known (no bar)
        public string Next;          // the button that ends this line ("Next", "Close" for the last); null for the close's cross alone
        public string Skip;          // the button that ends this line and every line waiting after it; null while none waits
    }

    /// <summary>
    /// A line someone says to the player over the world (Uncle Fred's lessons, a rival's taunt), as a notice card at the
    /// top centre: their picture on its tile at the left, their name in the cyan where it is known with where the line
    /// stands among those to come at the right of that row, the line wrapped under it, and under that how far it has been
    /// said with Skip and Next at its end, which the game's own close answers (the next line comes as the game's queue
    /// sends it). The pointer answers it, and so do two keys while it shows: Interact is Next and Cancel is Skip (Cancel closes
    /// the line when no Skip shows), the others staying the player's to walk with; the game's own sound plays the line.
    /// Commands: <see cref="CloseCommand"/> (Next, or the cross) and <see cref="SkipCommand"/>. Game-free. Cost: a
    /// layout a line and one a step of its bar.
    /// </summary>
    public static class UiMonologue
    {
        public const int CloseCommand = 1, SkipCommand = 2;
        public const float Width = 820f;      // px: the card
        private const float Face = 76f;       // px: the speaker's picture
        private const float Close = 32f;      // px: the close's quiet button, where the card has no buttons
        private const float Bar = 4f;         // px: the bar of how far the line has been said
        private const int LineMost = 6;

        public static void Lay(UiScene s, UiMonologueModel m, UiFrame frame, bool dark, IUiKeys keys = null)
        {
            s.Begin("monologue", frame.Width, frame.Height, dark);
            s.Pointer = true;   // the pointer answers it; the keys, where the host gives them (keys not null), are Interact for Next and Cancel for Skip, and walking keeps the rest
            s.Floats = true;
            if (m == null || string.IsNullOrEmpty(m.Line))
            {
                return;
            }
            float h = Height(m);
            float x = UiDraw.SnapDown((frame.Width - Width) / 2f), y = UiTokens.S4;
            int card = UiDraw.Box(s, UiAt.Screen, x, y, Width, h, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt at = UiAt.Screen.In(card);
            float fx = x + UiTokens.S4, fy = y + UiDraw.SnapDown((h - Face) / 2f);   // the face held in the middle of the card's height
            if (!string.IsNullOrEmpty(m.Picture))
            {
                int tile = UiDraw.Tile(s, at, fx, fy, Face, UiRole.None, false);
                var picture = new UiItem
                {
                    Kind = UiKind.Picture, Flags = UiFlags.Middle | UiFlags.Centre, X = fx + UiTokens.S1, Y = fy + UiTokens.S1, W = Face - UiTokens.S2, H = Face - UiTokens.S2,
                    Text = m.Picture, Value = 1f, Clip = -1, Node = -1, Layer = 0, Order = -1, Parent = tile, Alpha = 1f
                };
                s.Add(ref picture);
            }
            else
            {
                UiDraw.Portrait(s, at, fx, fy, Face, m.Name, m.Hue, UiRole.None);
            }
            float tx = fx + Face + UiTokens.S4, tw = LineRoom(m), ty = y + UiTokens.S4;
            float headH = HeadHeight(m);
            if (headH > 0f)
            {
                float nameH = UiTextRules.LineHeight(UiStyle.SectionHeader);
                float moreW = string.IsNullOrEmpty(m.More) ? 0f : UiDraw.Snap(UiTextRules.Width(m.More, UiStyle.Small) + UiTokens.S1);
                if (!string.IsNullOrEmpty(m.Name))
                {
                    UiDraw.Text(s, at, tx, ty, tw - (moreW > 0f ? moreW + UiTokens.S3 : 0f), UiStyle.SectionHeader, UiRole.AccentText, m.Name, UiFlags.Truncate | UiFlags.Middle);
                }
                if (moreW > 0f)
                {
                    UiDraw.Text(s, at, tx + tw - moreW, ty + (nameH - UiTextRules.LineHeight(UiStyle.Small)) / 2f, moreW, UiStyle.Small, UiRole.Muted, m.More,
                        UiFlags.Right | UiFlags.Middle | UiFlags.Label);
                }
            }
            float lineH = UiTextRules.WrappedHeight(m.Line, UiStyle.Body, tw, LineMost);
            UiDraw.Text(s, at, tx, ty + headH, tw, UiStyle.Body, UiRole.Text, m.Line, UiFlags.Wrap | UiFlags.Truncate, LineMost);
            if (string.IsNullOrEmpty(m.Next))
            {
                UiDraw.IconButton(s, at, x + Width - UiTokens.S3 - Close, y + UiTokens.S3, Close, UiIcon.Close, UiRole.Text2, CloseCommand, 0, false, UiAction.Count, false, true);
                return;
            }
            // Under the line: how far it has been said, then Skip (while more wait) and Next at the card's right.
            float by = ty + headH + UiDraw.Snap(lineH) + UiTokens.S3, bh = UiTokens.ControlSmall;
            float right = x + Width - UiTokens.S4;
            UiAction nextKey = keys != null ? UiAction.Alt : UiAction.Count, skipKey = keys != null ? UiAction.Back : UiAction.Count;
            float nextW = UiDraw.ButtonWidth(UiButtonKind.Primary, m.Next, UiIcon.None, nextKey, keys);
            float bx = right - nextW;
            UiDraw.Button(s, at, bx, by, nextW, bh, UiButtonKind.Primary, m.Next, UiIcon.None, nextKey, keys, CloseCommand, 0);
            if (!string.IsNullOrEmpty(m.Skip))
            {
                float skipW = UiDraw.ButtonWidth(UiButtonKind.Secondary, m.Skip, UiIcon.None, skipKey, keys);
                bx -= skipW + UiTokens.S3;
                UiDraw.Button(s, at, bx, by, skipW, bh, UiButtonKind.Secondary, m.Skip, UiIcon.None, skipKey, keys, SkipCommand, 0);
            }
            if (m.Progress >= 0 && bx - UiTokens.S4 > tx)
            {
                UiDraw.Meter(s, at, tx, by + UiDraw.SnapDown((bh - Bar) / 2f), bx - UiTokens.S4 - tx, Bar, m.Progress, UiRole.Accent);
            }
        }

        /// <summary>The card's height for its line: the picture's square at least.</summary>
        public static float Height(UiMonologueModel m)
        {
            float words = HeadHeight(m) + UiDraw.Snap(UiTextRules.WrappedHeight(m.Line, UiStyle.Body, LineRoom(m), LineMost));
            if (!string.IsNullOrEmpty(m.Next))
            {
                words += UiTokens.S3 + UiTokens.ControlSmall;   // the bar and the buttons under the line
            }
            return UiDraw.Snap(Math.Max(Face, words) + UiTokens.S4 * 2f);
        }

        // The row over the line: the name and where the line stands, when there is either; none for the picture and the line alone.
        private static float HeadHeight(UiMonologueModel m) =>
            string.IsNullOrEmpty(m.Name) && string.IsNullOrEmpty(m.More) ? 0f : UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S1;

        // The line's room beside the picture: to the card's padding where the buttons sit under it, else before the close.
        private static float LineRoom(UiMonologueModel m) =>
            Width - UiTokens.S4 - Face - UiTokens.S4 - (string.IsNullOrEmpty(m.Next) ? UiTokens.S3 + Close + UiTokens.S2 : UiTokens.S4);
    }
}
