using System;

namespace RealLifeOverhaul.Nav
{
    /// <summary>
    /// What stands round the minimap, measured in the game HUD's canvas units from the bottom of the screen (it is laid out
    /// for 3840 x 2160 and scaled by its canvases). A field left at its default means that piece is not up.
    /// </summary>
    public struct NavSpace
    {
        public float ScreenHeight;
        public float HintsTop;                       // the game's control hints' upper edge; below 0 for none
        public float TasksBottom;                    // the game's task panel's lower edge; float.MaxValue for none
        public float LabelsLeft, LabelsTop;          // the game's address label (bottom middle); LabelsTop below 0 for none
        public float ItemLeft, ItemTop;              // the game's item panel (bottom middle); ItemTop below 0 for none
        public float BarTop;                         // the kit's bottom bar's upper edge; 0 while the kit's HUD is not up
        public bool HasColumn;                       // the kit's left column (an info panel and the objectives) is up
        public float ColumnRight, ColumnBottom;      // its right and lower edges; the lower edge may be under the bar's top when the column is long
        public float ChipsLeft, ChipsTop;            // the kit's key chips over the bar's right end; ChipsTop below 0 for none

        public static NavSpace Free(float screenHeight)
        {
            return new NavSpace { ScreenHeight = screenHeight, HintsTop = -1f, TasksBottom = float.MaxValue, LabelsTop = -1f, ItemTop = -1f, ChipsTop = -1f };
        }
    }

    /// <summary>
    /// Where the minimap's card stands, game-free so EconomyCheck holds it at every screen and language: in the left column
    /// above the game's hints and the kit's bottom bar and under the game's tasks and the kit's left column, or, where that
    /// leaves too little, beside the column at the bottom, clear of the bar, the game's address label and item panel and the
    /// kit's column. The rest of the kit's HUD pieces keep clear of the card it lands on (UiMinimapBox). Cost: a few sums, on
    /// the slow tick.
    /// </summary>
    public static class NavPlaceRules
    {
        public const float Column = 740f;        // the width of the game's left column (tasks above, control hints below)
        public const float Gap = 16f;            // between the card and the panels above, below and beside it
        public const float LeastMap = 240f;      // a map lower than this is not worth showing
        public const float BesideExtra = 60f;    // a card already beside the column asks this much more room to go back
        public const float TasksTop = 320f;      // the task panel hangs this far below the screen's top edge
        public const float ExpandButton = 100f;  // its button stays there when it is folded away
        public const float Head = 100f;          // the card's headline's height
        public const float Foot = 46f;           // the map's inset from the card's lower edge
        public const float LineHeight = 58f;     // one of the GPS's two lines
        public const float LinesPad = 14f;       // round the two lines
        public const float ViewSide = 32f;       // the map's inset from the card's sides: the box is drawn 14 inside its rectangle, and 6 more frame the map

        /// <summary>The card's width at a size in percent of the game's left column (50 to 100).</summary>
        public static float Width(int sizePercent) => (float)Math.Round(Column * Math.Max(50, Math.Min(100, sizePercent)) / 100f);

        /// <summary>The map's height the size asks for: the card's width less the map's insets; 0 for no map.</summary>
        public static float Wanted(float width, bool map) => map ? width - ViewSide * 2f : 0f;

        /// <summary>The card's height but for the map: headline, foot, and the two lines of the GPS when they show.</summary>
        public static float Frame(bool lines) => Head + Foot + (lines ? LineHeight * 2f + LinesPad : 0f);

        /// <summary>
        /// Chooses the card's lower left corner and its map's height. <paramref name="frame"/> is the height of everything but
        /// the map (headline, foot and the GPS's lines), <paramref name="wanted"/> the map's height the size asks for (0 for
        /// no map). Returns whether the card fits at all; <paramref name="column"/> says which of the two places it took.
        /// </summary>
        public static bool Place(NavSpace s, float width, float wanted, float frame, bool wasBeside, out float x, out float bottom, out float mapHeight, out bool column)
        {
            bool map = wanted > 0f;
            bottom = Math.Max(s.HintsTop >= 0f ? s.HintsTop + Gap : 0f, s.BarTop > 0f ? s.BarTop + Gap : 0f);
            if (s.ChipsTop >= 0f && s.ChipsLeft < width + Gap)
            {
                bottom = Math.Max(bottom, s.ChipsTop + Gap);
            }
            float top = s.ScreenHeight - TasksTop - ExpandButton;
            top = Math.Min(top, s.TasksBottom);
            if (s.HasColumn)
            {
                top = Math.Min(top, s.ColumnBottom);
            }
            float room = top - Gap - bottom - frame;
            x = 0f;
            column = room >= (map ? LeastMap + (wasBeside ? BesideExtra : 0f) : 0f);
            if (!column)
            {
                // Beside the column, at the bottom: as high as the screen under the top bar allows.
                x = Column + Gap;
                if (s.HasColumn)
                {
                    x = Math.Max(x, s.ColumnRight + Gap);
                }
                bottom = s.BarTop > 0f ? s.BarTop + Gap : 0f;
                if (s.LabelsTop >= 0f && s.LabelsLeft < x + width + Gap)
                {
                    bottom = Math.Max(bottom, s.LabelsTop + Gap);
                }
                if (s.ItemTop >= 0f && s.ItemLeft < x + width + Gap)
                {
                    bottom = Math.Max(bottom, s.ItemTop + Gap);
                }
                if (s.ChipsTop >= 0f && s.ChipsLeft < x + width + Gap)
                {
                    bottom = Math.Max(bottom, s.ChipsTop + Gap);   // the keys' prompts stand fixed at the bar's right end: the card goes over them
                }
                room = s.ScreenHeight - TasksTop - Gap - bottom - frame;
            }
            mapHeight = map ? Math.Min(wanted, (float)Math.Floor(room)) : 0f;
            return map ? mapHeight >= LeastMap : room >= 0f;
        }
    }
}
