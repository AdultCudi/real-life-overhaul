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
        public const float ScreenHeight = 1080f;   // px: the canvas's reference height, the least any screen gives (16:9)

        public float Width;     // px: the area's width
        public float Height;    // px: the area's whole height, masthead and band included
        public float Top;       // px: the masthead's height, 0 for a screen without one
        public float Band;      // px: the prompt band's height
        public string Title;    // the app's name in the masthead, or null

        /// <summary>
        /// The masthead's height: the app's name on its line with a step of air above and below and the column rule
        /// under it. The game's own bar was 130 px, and the 54 px it held over the name were taken from every page.
        /// </summary>
        public static float MastheadHeight => UiDraw.Snap(UiTextRules.LineHeight(UiStyle.AppTitle) + UiTokens.S3 * 2f + UiTokens.FrameRule);

        /// <summary>The prompt band's height: the masthead's, so a page sits between two even bars.</summary>
        public static float BandHeight => MastheadHeight;

        /// <summary>The frame of an app in the game's full menu on a screen of an aspect (16:9 is 1920 x 1080).</summary>
        public static UiFrame FullMenu(float aspect, string title = null)
        {
            float canvasW = Math.Max(3840f, 2160f * aspect), canvasH = Math.Max(2160f, 3840f / aspect);
            return new UiFrame { Width = canvasW / UiTokens.CanvasPerPx, Height = canvasH / UiTokens.CanvasPerPx, Top = MastheadHeight, Band = BandHeight, Title = title };
        }

        /// <summary>The first grid line under the masthead.</summary>
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

        /// <summary>
        /// The room a page is judged against (RLO-UI-016): the content box, no taller than a 16:9 screen's. A taller
        /// screen's extra height is room its lists take, never a hole a page of fixed parts has to fill.
        /// </summary>
        public void Room(out float x, out float y, out float w, out float h)
        {
            Content(out x, out y, out w, out h);
            h = Math.Min(h, UiDraw.SnapDown(ScreenHeight - Band - UiTokens.S4 - y));
        }
    }

    /// <summary>The masthead's right side: the money, the day and the time, as the game's own bar wrote them; the game file sets it.</summary>
    public static class UiChrome
    {
        public static Func<string> Status;
    }
}
