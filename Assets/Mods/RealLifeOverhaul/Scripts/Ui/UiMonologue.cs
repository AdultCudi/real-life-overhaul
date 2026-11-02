using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>What a spoken line shows: who says it (their picture or a painted face), the line, and how many more the game holds.</summary>
    public sealed class UiMonologueModel
    {
        public string Name;          // "Uncle Fred", a rival's name
        public string Picture;       // their picture as the game holds it (its key, UiPictures); null for a painted face by the name
        public float Hue;
        public string Line;
        public string More;          // "2 more" while the game holds more lines after this one; null for none
    }

    /// <summary>
    /// A line someone says to the player over the world (Uncle Fred's lessons, a rival's taunt), as a notice card at the
    /// top centre: their picture on its tile at the left, their name in the cyan with the lines still to come beside it,
    /// the line wrapped under it, and the close at the top right, which the game's own close answers (the next line comes
    /// as the game's queue sends it). It takes no key: the pointer closes it, and the game's own sound plays the line.
    /// Commands: <see cref="CloseCommand"/>. Game-free. Cost: a layout a line.
    /// </summary>
    public static class UiMonologue
    {
        public const int CloseCommand = 1;
        public const float Width = 560f;      // px: the card
        private const float Face = 64f;       // px: the speaker's picture
        private const float Close = 32f;      // px: the close's quiet button
        private const int LineMost = 6;

        public static void Lay(UiScene s, UiMonologueModel m, UiFrame frame, bool dark)
        {
            s.Begin("monologue", frame.Width, frame.Height, dark);
            s.Pointer = true;   // the keys stay the player's: the card is closed by pointing
            s.Floats = true;
            if (m == null || string.IsNullOrEmpty(m.Line))
            {
                return;
            }
            float h = Height(m);
            float x = UiDraw.SnapDown((frame.Width - Width) / 2f), y = UiTokens.S6;
            int card = UiDraw.Box(s, UiAt.Screen, x, y, Width, h, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt at = UiAt.Screen.In(card);
            float fx = x + UiTokens.S4, fy = y + UiTokens.S4;
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
            float tx = fx + Face + UiTokens.S4, tw = LineRoom;
            float nameH = UiTextRules.LineHeight(UiStyle.Title);
            float moreW = string.IsNullOrEmpty(m.More) ? 0f : UiDraw.Snap(UiTextRules.Width(m.More, UiStyle.Small) + UiTokens.S1);
            UiDraw.Text(s, at, tx, fy, tw - (moreW > 0f ? moreW + UiTokens.S3 : 0f), UiStyle.Title, UiRole.AccentText, m.Name, UiFlags.Truncate | UiFlags.Middle);
            if (moreW > 0f)
            {
                UiDraw.Text(s, at, tx + tw - moreW, fy + (nameH - UiTextRules.LineHeight(UiStyle.Small)) / 2f, moreW, UiStyle.Small, UiRole.Muted, m.More,
                    UiFlags.Right | UiFlags.Middle | UiFlags.Label);
            }
            UiDraw.Text(s, at, tx, fy + UiDraw.Snap(nameH) + UiTokens.S1, tw, UiStyle.Body, UiRole.Text, m.Line, UiFlags.Wrap | UiFlags.Truncate, LineMost);
            UiDraw.IconButton(s, at, x + Width - UiTokens.S3 - Close, y + UiTokens.S3, Close, UiIcon.Close, UiRole.Text2, CloseCommand, 0, false, UiAction.Count, false, true);
        }

        /// <summary>The card's height for its line: the picture's square at least.</summary>
        public static float Height(UiMonologueModel m)
        {
            float words = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Title)) + UiTokens.S1 + UiTextRules.WrappedHeight(m.Line, UiStyle.Body, LineRoom, LineMost);
            return UiDraw.Snap(Math.Max(Face, words) + UiTokens.S4 * 2f);
        }

        // The line's room beside the picture and before the close.
        private static float LineRoom => Width - UiTokens.S4 - Face - UiTokens.S4 - UiTokens.S3 - Close - UiTokens.S2;
    }
}
