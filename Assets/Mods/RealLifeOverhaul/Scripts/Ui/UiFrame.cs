using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Where a screen stands: the area the host gives it and the band its prompts stand in, in px, and the content box
    /// inside after the frame's margins. A full-screen app stands in the game's full menu between its top bar and its
    /// bottom bar (that bar is the prompt band); its canvas scales with the screen (3840 x 2160, expanded), so the area is
    /// never narrower than 1920 px and only its height and an ultrawide's width change: the layout keeps its composition
    /// and its lists take what height there is (no reflow). Game-free. Cost: a few sums.
    /// </summary>
    public struct UiFrame
    {
        public const float MostContent = 2240f;    // px: the widest a screen's content runs; wider screens centre it
        public const float FullMenuTop = 130f;     // px: the game's full menu's top bar and its splitter (260 canvas units)
        public const float FullMenuBand = 75f;     // px: the game's bottom bar, where the prompts stand (150 canvas units)

        public float Width;     // px: the area's width
        public float Height;    // px: the area's height, the prompt band not counted
        public float Band;      // px: the prompt band under the area

        /// <summary>The frame of an app in the game's full menu on a screen of an aspect (16:9 is 1920 x 875 and a band of 75).</summary>
        public static UiFrame FullMenu(float aspect)
        {
            float canvasW = Math.Max(3840f, 2160f * aspect), canvasH = Math.Max(2160f, 3840f / aspect);
            return new UiFrame { Width = canvasW / UiTokens.CanvasPerPx, Height = canvasH / UiTokens.CanvasPerPx - FullMenuTop - FullMenuBand, Band = FullMenuBand };
        }

        /// <summary>The content box: the frame's margins taken off, centred where the area is wider than the content may run.</summary>
        public void Content(out float x, out float y, out float w, out float h)
        {
            float side = UiTokens.S6;
            w = Width - side * 2f;
            if (w > MostContent)
            {
                side = UiDraw.SnapDown((Width - MostContent) / 2f);
                w = Width - side * 2f;
            }
            x = side;
            y = UiTokens.S4;
            h = UiDraw.SnapDown(Height - UiTokens.S4 * 2f);
        }
    }
}
