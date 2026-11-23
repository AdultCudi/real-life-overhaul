namespace RealLifeOverhaul
{
    /// <summary>
    /// When Mom may get in touch by herself: a text, a call, a visit, an introduction or a story of the day. One rule for every
    /// feature that speaks for her (the social life, the life events, the family): the option, and one such contact a day.
    /// Game-free: EconomyCheck runs it. No cost a frame: asked on an event, when somebody is about to be picked or a text sent.
    /// </summary>
    public static class MomRules
    {
        public const int MostUnpromptedPerDay = 1;   // contacts a day she may start herself; before this rule every feature had its own cap and they stacked

        /// <summary>
        /// Whether she may start a contact on <paramref name="day"/>: the option is on and she has not already done so that day.
        /// What the player starts (a call to her, a plan made with her, her answer to news the player gave) is not asked of this.
        /// </summary>
        public static bool May(bool optionOn, int day, int lastContactDay)
        {
            return optionOn && day > 0 && lastContactDay != day;
        }
    }
}
