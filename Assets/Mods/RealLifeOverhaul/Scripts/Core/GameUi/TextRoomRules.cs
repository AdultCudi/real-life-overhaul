namespace RealLifeOverhaul.GameUi
{
    /// <summary>Where a text's words stand in its box by height, as far as its room goes.</summary>
    public enum TextRoomSide : byte
    {
        Under = 0,   // laid from the top: room under it moves nothing
        Both,        // centred on its box: as much over as under
        Over,        // laid from the bottom: room over it
        None         // centred by its capitals: the text engine moves it by any room, so it gets none
    }

    /// <summary>
    /// The room a text in the game's font needs so that the text engine does not drop it, game-free. TextMeshPro ends a
    /// text that may be cut at its first character when a line stands taller than its box, and lays a line out by the
    /// font of each character: the game's fonts for Chinese, Japanese and Korean stand <see cref="TallestLineEm"/> from
    /// ascender to descender, its interface font under 1.2 em, which the mod's boxes for one line are sized by. A box
    /// shorter than one line of the tallest font is given the difference as room, through the text's margins, on the side
    /// where its alignment leaves the words where they were (<see cref="TextRoom"/>); a box that holds such a line gets
    /// none, so a text in Latin letters is laid out exactly as before. Held offline against the census of the game's
    /// fonts (EconomyCheck, FontFallbackChecks). Cost: a few sums when a text's box changes size.
    /// </summary>
    public static class TextRoomRules
    {
        public const float TallestLineEm = 1.45f;   // em: a hair over the tallest line among the game's fallback fonts, 1.448 of its Noto fonts for Chinese, Japanese and Korean

        /// <summary>
        /// The room over and under a text's box, in its own units, for a box of a height and a text of a size: nothing for
        /// a box that already holds a line of the tallest font, or for a text centred by its capitals.
        /// </summary>
        public static void Room(float box, float size, TextRoomSide side, out float over, out float under)
        {
            over = under = 0f;
            float lacks = TallestLineEm * size - box;
            if (!(lacks > 0f) || side == TextRoomSide.None)
            {
                return;
            }
            switch (side)
            {
                case TextRoomSide.Under:
                    under = lacks;
                    break;
                case TextRoomSide.Over:
                    over = lacks;
                    break;
                default:
                    over = under = lacks / 2f;
                    break;
            }
        }
    }
}
