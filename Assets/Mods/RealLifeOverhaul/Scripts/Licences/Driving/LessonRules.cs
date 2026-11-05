namespace RealLifeOverhaul
{
    /// <summary>
    /// Who can be taken out in the school's car, game-free. The car is lent out of the traffic this machine runs, so a guest of a
    /// Going Public session, whose traffic is the host's, can never be taken out: the desk says so before it offers the lesson or
    /// takes a fee, in words that name the cause, not the road. The host and a player alone run their own traffic.
    /// COST: nothing per frame; a comparison on a desk's press.
    /// </summary>
    public static class LessonRules
    {
        public const string GuestKey = "reallife_lic_no_guest";   // a guest's traffic is the host's, so the school's car cannot be had
        public const string NoRoadsKey = "reallife_lic_no_roads";   // this machine's traffic is not running on the street just now

        /// <summary>The locale key of why the school cannot take the player out, or null when it can. A guest is told first: waiting never helps them.</summary>
        public static string Refusal(bool guest, bool trafficRuns)
        {
            return guest ? GuestKey : !trafficRuns ? NoRoadsKey : null;
        }
    }
}
