namespace RealLifeOverhaul
{
    /// <summary>
    /// Why the city's construction programme set no site out, as the one list of sentences its callers hand each other (the programme,
    /// its wiring and the construction game say them in logs), and the plain text a player is told for each: never a pointer to
    /// the log. A cause not in the list reads as the general text. Game-free.
    /// </summary>
    public static class SiteRefusals
    {
        public const string Off = "the city's construction sites are off";
        public const string NotRunning = "the city's construction programme is not running";
        public const string Host = "the host of the Going Public session runs the city's sites";
        public const string Taken = "a site is there already, or the building has no street door";
        public const string NearPrefix = "another site is already within ";

        /// <summary>The text key a refusal is told with: plain words for the player, whatever the cause.</summary>
        public static string Key(string why)
        {
            if (why == Off || why == NotRunning)
            {
                return "reallife_bld_answer_site_off";
            }
            if (why == Host)
            {
                return "reallife_bld_answer_site_host";
            }
            if (why == Taken || (why != null && why.StartsWith(NearPrefix, System.StringComparison.Ordinal)))
            {
                return "reallife_bld_answer_site_there";
            }
            return "reallife_bld_answer_site_refused";
        }
    }
}
