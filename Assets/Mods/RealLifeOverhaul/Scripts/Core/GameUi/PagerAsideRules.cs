namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// When the phone's pager leaves a grid to another mod, and how a phone another mod made taller is kept on the screen.
    /// Only a mod that pages the grid takes it over: one of the known pagers on the grid, or a hand that shows a button of
    /// the game's or this mod's on a page that is not showing. Another mod showing its own button again (Hotelier and
    /// Restaurateur's, which grows the phone to fit every button showing) has it join the pages, and a script of another
    /// mod that pages nothing is no pager. Game-free; checked offline (ContractCheck PagerChecks).
    /// </summary>
    public static class PagerAsideRules
    {
        public const int Keep = 0, Join = 1, Aside = 2;   // what a pass does: pages as it was, the buttons shown again join the pages, the grid is left to another mod

        public const float FightGap = 1f;   // seconds, unscaled: another mod's button shown again sooner than this after the last time is part of one run
        public const int FightRun = 3;      // passes in such a run: beyond it the button is held shown by its mod (every frame or tick), and paging it would cost a pass a frame

        public const float FitTolerance = 0.5f;   // canvas units: a smaller growth or overrun is none

        /// <summary>The grids' scripts of the mods known to page them (BizSuite's pager, AlunaPhonePages' swipe), by full name.</summary>
        public static readonly string[] KnownPagers = { "BizSuite.PhoneAppsPager", "AlunaPhonePages.PhoneSwipeHandler" };

        /// <summary>True for a known pager's script; any other script of another mod is no pager by itself.</summary>
        public static bool Pager(string fullName)
        {
            if (string.IsNullOrEmpty(fullName))
            {
                return false;
            }
            for (int i = 0; i < KnownPagers.Length; i++)
            {
                if (string.Equals(fullName, KnownPagers[i], System.StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>The run of passes that found other mods' buttons shown again, counting this one: 1 when the last was <see cref="FightGap"/> ago or more (or never).</summary>
        public static int Run(float now, float last, int run)
        {
            return now - last < FightGap && run > 0 ? run + 1 : 1;   // NaN (never) is no run
        }

        /// <summary>
        /// One pass: <paramref name="ownShown"/> buttons of the game's or this mod's and <paramref name="othersShown"/> of other mods'
        /// were found shown on pages not showing, <paramref name="run"/> as <see cref="Run"/> counts it (when othersShown is above 0),
        /// and whether a known pager's script is on the grid.
        /// </summary>
        public static int Verdict(int ownShown, int othersShown, int run, bool pagerOnGrid)
        {
            if (pagerOnGrid || ownShown > 0)
            {
                return Aside;   // only a pager shows the game's buttons or this mod's
            }
            if (othersShown > 0)
            {
                return run > FightRun ? Aside : Join;
            }
            return Keep;
        }

        /// <summary>
        /// The height a part of the phone is set to so it stays on the screen: what another mod added to it beyond
        /// <paramref name="ownHeight"/> (its height as the game built it) is taken back as far as it runs past the screen's top
        /// (<paramref name="overTop"/>) or bottom (<paramref name="overBottom"/>), in its own units; never below its own height.
        /// Its pivot says how much of a cut moves each edge. Unchanged when it has not grown, fits, or anything is unknown.
        /// </summary>
        public static float FitHeight(float height, float ownHeight, float pivotY, float overTop, float overBottom)
        {
            if (!(height - ownHeight > FitTolerance) || float.IsNaN(pivotY) || float.IsNaN(overTop) || float.IsNaN(overBottom))
            {
                return height;
            }
            float cut = 0f;
            if (overTop > FitTolerance && pivotY < 1f)
            {
                cut = overTop / (1f - pivotY);
            }
            if (overBottom > FitTolerance && pivotY > 0f)
            {
                float under = overBottom / pivotY;
                cut = under > cut ? under : cut;
            }
            if (!(cut > FitTolerance))
            {
                return height;
            }
            float fitted = height - cut;
            return fitted > ownHeight ? fitted : ownHeight;
        }
    }
}
