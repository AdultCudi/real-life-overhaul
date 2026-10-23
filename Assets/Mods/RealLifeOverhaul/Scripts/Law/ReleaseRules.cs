namespace RealLifeOverhaul.Law
{
    // Game-free: EconomyCheck compiles this file as it is. Every locale key is written out in full.

    /// <summary>
    /// Which line tells the player they are let out of a precinct's cell. In a Going Public session the clock is
    /// everyone's, so a term is counted as served at once (Custody.Cell): its line names no hours held, as none passed.
    /// A bail is only ever posted from the cell, so the bail lines stay as they are.
    /// </summary>
    public static class ReleaseRules
    {
        /// <param name="court">held by a court's order rather than a charge at the station</param>
        /// <param name="bailed">let out on bail</param>
        /// <param name="atDesk">signed out at the front desk rather than let out at the door</param>
        /// <param name="servedAtOnce">a Going Public session served the term at once: no time passed</param>
        public static string Key(bool court, bool bailed, bool atDesk, bool servedAtOnce)
        {
            if (bailed)
            {
                return court ? (atDesk ? "reallife_law_court_released_desk_bail" : "reallife_law_court_released_bail")
                    : atDesk ? "reallife_law_released_desk_bail" : "reallife_law_released_bail";
            }
            if (servedAtOnce)
            {
                return court ? (atDesk ? "reallife_law_court_released_desk_shared" : "reallife_law_court_released_shared")
                    : atDesk ? "reallife_law_released_desk_shared" : "reallife_law_released_shared";
            }
            return court ? (atDesk ? "reallife_law_court_released_desk" : "reallife_law_court_released")
                : atDesk ? "reallife_law_released_desk" : "reallife_law_released";
        }
    }
}
