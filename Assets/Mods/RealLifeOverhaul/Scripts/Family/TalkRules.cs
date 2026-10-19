namespace RealLifeOverhaul.Family
{
    /// <summary>
    /// When the two last talked, as family life counts it for warmth and for "we have not really talked in days" (HomeLife.Daily): its own
    /// talks and time together, and, while Social supplies the partner, Social's last call, text, outing or meeting with them, which never
    /// reach family life's own record. Game-free (EconomyCheck, PartnerTalkChecks.cs).
    /// </summary>
    public static class TalkRules
    {
        /// <summary>The latest of the three days; a contact day of 0 or less is none known.</summary>
        public static int LastTalked(int talkDay, int sharedDay, int socialContactDay)
        {
            int last = talkDay > sharedDay ? talkDay : sharedDay;
            return socialContactDay > last ? socialContactDay : last;
        }
    }
}
