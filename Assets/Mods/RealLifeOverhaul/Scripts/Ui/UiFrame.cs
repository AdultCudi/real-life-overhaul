using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Where a screen stands: the whole area the host gives it, in px, with the masthead the kit paints at its top (the
    /// app's name, the money, the day and the time) and the prompt band at its foot, and the content box between them
    /// after the frame's margins. A full-screen app takes the game's whole full menu and paints its bars flat in the
    /// City Ledger's colours (no blurred band, no glass); the canvas scales with the screen (3840 x 2160, expanded), so
    /// the area is never narrower than 1920 px and only its height and an ultrawide's width change: the layout keeps its
    /// composition and its lists take what height there is (no reflow). Game-free. Cost: a few sums.
    /// </summary>
    public struct UiFrame
    {
        public const float MostContent = 2240f;    // px: the widest a screen's content runs; wider screens centre it
        public const float FullMenuTop = 130f;     // px: the game's full menu's top bar and its splitter (260 canvas units), now the kit's masthead
        public const float FullMenuBand = 75f;     // px: the game's bottom bar (150 canvas units), now the kit's prompt band

        public float Width;     // px: the area's width
        public float Height;    // px: the area's whole height, masthead and band included
        public float Top;       // px: the masthead's height, 0 for a screen without one
        public float Band;      // px: the prompt band's height
        public string Title;    // the app's name in the masthead, or null

        /// <summary>The frame of an app in the game's full menu on a screen of an aspect (16:9 is 1920 x 1080: a masthead of 130, a band of 75).</summary>
        public static UiFrame FullMenu(float aspect, string title = null)
        {
            float canvasW = Math.Max(3840f, 2160f * aspect), canvasH = Math.Max(2160f, 3840f / aspect);
            return new UiFrame { Width = canvasW / UiTokens.CanvasPerPx, Height = canvasH / UiTokens.CanvasPerPx, Top = FullMenuTop, Band = FullMenuBand, Title = title };
        }

        /// <summary>The first grid line under the masthead (the game's masthead is 130 px, off the 4 px grid).</summary>
        public float ContentTop => (float)Math.Ceiling(Top / UiTokens.S1) * UiTokens.S1;

        /// <summary>The y where the prompt band begins.</summary>
        public float BandY => Height - Band;

        /// <summary>The content box: under the masthead, over the band, the frame's margins taken off, centred where the area is wider than the content may run.</summary>
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
            y = ContentTop + UiTokens.S4;
            h = UiDraw.SnapDown(Height - Band - UiTokens.S4 - y);
        }
    }

    /// <summary>The masthead's right side: the money, the day and the time, as the game's own bar wrote them; the game file sets it.</summary>
    public static class UiChrome
    {
        public static Func<string> Status;
    }
}
