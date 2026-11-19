namespace RealLifeOverhaul
{
    /// <summary>
    /// What a client must be, for the Gig Line to offer an indoor gig and for the gig to start: one rule, read by both. Game-free.
    /// </summary>
    public static class ClientRules
    {
        public const string NoneNear = "reallife_job_no_client_near";     // no business of the kind within reach
        public const string NoneOpen = "reallife_job_no_client_hours";    // some near, none open long enough
        public const string NoneFound = "reallife_job_no_client";         // some open, none with a place to work

        /// <summary>Whether a refusal is one of the three that say no client could be had.</summary>
        public static bool IsNoClient(string key) => key == NoneNear || key == NoneOpen || key == NoneFound;

        /// <summary>Game minutes a client must stay open: the walk plus the longer of the work by hand and the clock's, and a margin.</summary>
        public static float Needed(float seconds, float speed, float walkMetres, float secondsPerMetre, int units, int minutesEach, int margin)
        {
            float walk = walkMetres * secondsPerMetre * speed;
            float byHand = seconds * speed + walk;
            float byClock = walk + units * minutesEach;
            return (byHand > byClock ? byHand : byClock) + margin;
        }

        /// <summary>The key of the real reason no client was found, from how far the candidates got.</summary>
        public static string Reason(int near, int open)
        {
            return near <= 0 ? NoneNear : open <= 0 ? NoneOpen : NoneFound;
        }
    }
}
