namespace RealLifeOverhaul.GameUi
{
    /// <summary>What kind of part a page's body holds, for deciding which rows of buttons stand in the card's foot.</summary>
    public enum PartKind
    {
        Heavy,     // a heading, a fact, a list entry, a meter, a need, a switch, a choice, a picture: what the buttons act on
        Light,     // a sentence or a note
        Buttons    // a row of action buttons
    }

    /// <summary>
    /// The rules of the apps' left pane (the white card beside the table): its measures, how a fact's label and value share a
    /// line, when a header's chips go under the title, which rows of buttons stand in the fixed foot, and where the scroll
    /// stands after a page is written again. Game-free; checked offline (ContractCheck LeftPaneChecks). Nothing here runs a frame.
    /// </summary>
    public static class LeftPaneRules
    {
        // The card, in canvas pixels of the game's 3840 x 2160. The game's white box is 949 wide with 80 of padding a side.
        public const int PadLeft = 56;        // the card's edge to its text
        public const int PadRight = 14;       // the card's right edge to the body's: the gutter holds the scroll bar
        public const int PadTop = 40;
        public const int PadBottom = 34;
        public const int Gutter = 36;         // the body's, head's and foot's right padding: where the scroll bar stands, so all three end level
        public const float BarWidth = 12f;    // the scroll bar's width in the gutter
        public const float DefaultRoom = 843f;   // a row's room (card 949 less its padding and gutter) for a card not laid out yet
        public const float RowGap = 24f;      // between a fact's label and its value
        public const float PairGap = 28f;     // between the two facts of a pair
        public const float Slack = 2f;        // added to a measured text so that a rounding never wraps its last letter
        public const float BodySpacing = 14f; // between the parts of the body
        public const float GroupPadX = 24f;   // a section card's padding
        public const float GroupPadTop = 20f;
        public const float GroupPadBottom = 22f;
        public const float GroupSpacing = 12f;
        public const float EntryPadLeft = 18f;   // a list entry's padding (the mark of the current one stands in it)
        public const float EntryPadRight = 6f;
        public const float EntryPadY = 8f;
        public const float MarkWidth = 5f;       // the current entry's mark
        public const float ChipsShare = 0.42f;   // chips wider than this share of the row go under the title
        public const float MinBody = 300f;       // the least height the scrolling body must keep, whatever the head and foot ask

        /// <summary>The width a fact's label is given beside its value in a row of this room; the value takes the rest, wrapping under itself.</summary>
        public static float LabelWidth(float label, float value, float room, float gap)
        {
            float l = label > 0f ? label : 0f, v = value > 0f ? value : 0f;
            if (!(room > 0f))
            {
                return l;   // a card of unknown width: the label as it is
            }
            if (l + v + gap <= room)
            {
                return l;   // both on one line
            }
            float half = room * 0.5f;
            if (l <= half)
            {
                return l;   // a short label stays whole, the value wraps beside it
            }
            if (v <= half)
            {
                return room - v - gap > 0f ? room - v - gap : 0f;   // a short value stays whole, the label wraps
            }
            float each = (room - gap) * 0.5f;
            return each > 0f ? each : 0f;   // both long: half each
        }

        /// <summary>Whether a fact fits on one line in this room, its label and its value side by side.</summary>
        public static bool OneLine(float label, float value, float room, float gap)
        {
            float l = label > 0f ? label : 0f, v = value > 0f ? value : 0f;
            return room > 0f && l + v + gap <= room;
        }

        /// <summary>The room each fact of a pair has.</summary>
        public static float Half(float room)
        {
            float half = (room - PairGap) * 0.5f;
            return half > 0f ? half : 0f;
        }

        /// <summary>Chips this wide go on a line under the title, not at its right, where they would squeeze a long title.</summary>
        public static bool ChipsBelow(float chips, float room)
        {
            return room > 0f && chips > room * ChipsShare;
        }

        /// <summary>
        /// Which rows of buttons stand in the foot: those after the page's last heavy part (a row in the middle of the page acts on
        /// what is round it and stays there). <paramref name="toFoot"/> is set for each of the first <paramref name="count"/> parts;
        /// returns how many rows go.
        /// </summary>
        public static int FootRows(PartKind[] kinds, int count, bool[] toFoot)
        {
            if (kinds == null || toFoot == null)
            {
                return 0;
            }
            if (count > kinds.Length)
            {
                count = kinds.Length;
            }
            if (count > toFoot.Length)
            {
                count = toFoot.Length;
            }
            int last = -1;
            for (int i = 0; i < count; i++)
            {
                if (kinds[i] == PartKind.Heavy)
                {
                    last = i;
                }
            }
            int moved = 0;
            for (int i = 0; i < count; i++)
            {
                bool go = kinds[i] == PartKind.Buttons && i > last;
                toFoot[i] = go;
                if (go)
                {
                    moved++;
                }
            }
            return moved;
        }

        /// <summary>
        /// Where the body is scrolled to once a page is written: the top for another page (another title), the old place, kept
        /// within the new page's length, for the same page written again (a press that changed one figure).
        /// </summary>
        public static float KeepOffset(bool samePage, float offset, float content, float view)
        {
            if (!samePage || !(offset > 0f))
            {
                return 0f;
            }
            float most = content - view;
            if (!(most > 0f))
            {
                return 0f;
            }
            return offset < most ? offset : most;
        }

        /// <summary>The same page: both have a title and it is the same, or neither has one.</summary>
        public static bool SamePage(string last, string now)
        {
            return string.Equals(last ?? string.Empty, now ?? string.Empty, System.StringComparison.Ordinal);
        }

        /// <summary>Whether a scroll reaches the end: with the body at its foot, the last part's bottom is inside the view.</summary>
        public static bool ReachesEnd(float lastBottom, float viewBottom)
        {
            return lastBottom >= viewBottom - 1f;
        }

        /// <summary>Whether two texts' boxes cross: both ranges overlap by more than a pixel (a text and the next one it would be drawn over).</summary>
        public static bool Crosses(float leftA, float rightA, float topA, float bottomA, float leftB, float rightB, float topB, float bottomB)
        {
            const float Touch = 1f;   // canvas pixels: boxes that only touch do not cross
            bool across = leftA < rightB - Touch && leftB < rightA - Touch;
            bool down = bottomA < topB - Touch && bottomB < topA - Touch;
            return across && down;
        }
    }
}
