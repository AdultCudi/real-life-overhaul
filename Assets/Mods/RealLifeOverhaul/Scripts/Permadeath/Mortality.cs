namespace RealLifeOverhaul.Permadeath
{
    /// <summary>
    /// The one door the features where a life can be in danger call: a crash (Traffic/CrashConsequences), being hit on foot
    /// (Ragdoll/PlayerKnockdown), a mugging refused (Crime/Scenes/PlayerMuggingScene), the private lender's men
    /// (Wiring/DebtLinks) and a flu left untreated (Home/HomeHealth). Going without food needs no door: the director reads
    /// the game's hunger bar itself (Permadeath/PermadeathDirector.Hunger). Each asks <see cref="Judge"/> at the moment it would
    /// otherwise hurt the player, and does as it always did unless the answer is a near miss (it hurts as before and calls
    /// <see cref="NearMiss"/>, which says so) or a death (it hurts nobody and calls <see cref="Die"/>, which takes over).
    /// Safe to call at any time: with Permadeath off, in a Going Public session, during the game's tutorial or one of its
    /// missions, or with the feature not running, every answer is "nothing" and the feature is exactly as without it.
    /// A call costs a few field reads and one random number; none is made per frame.
    /// </summary>
    public static class Mortality
    {
        /// <summary>What a dangerous moment comes to. <paramref name="measure"/> is the speed in m/s for a crash or a hit; ignored otherwise.</summary>
        public static Verdict Judge(DeathCause cause, float measure = 0f)
        {
            PermadeathDirector director = PermadeathDirector.Instance;
            return director != null ? director.Judge(cause, measure) : Verdict.None;
        }

        /// <summary>The first moment of its kind: remembered, and a notice that says the next one could be fatal. Call it with the injury, as the injury is shown.</summary>
        public static void NearMiss(DeathCause cause)
        {
            PermadeathDirector.Instance?.NearMiss(cause);
        }

        /// <summary>The character's life ends now: the fade, the save, the obituary, the main menu. False if it could not begin (then hurt the player as before).</summary>
        public static bool Die(DeathCause cause)
        {
            PermadeathDirector director = PermadeathDirector.Instance;
            return director != null && director.Die(cause);
        }

        /// <summary>
        /// One game hour of the player's illness, from Home's health: whether it is a flu, when it began and whether it has
        /// been treated. Shows the warnings in their turn and may end the life once all three have been shown. True while a
        /// flu no longer clears up by itself (it is then held on by the caller).
        /// </summary>
        public static bool FluHour(bool flu, float beganMinutes, bool treated)
        {
            PermadeathDirector director = PermadeathDirector.Instance;
            return director != null && director.FluHour(flu, beganMinutes, treated);
        }

        /// <summary>The mugger's demand: the warning's version while Permadeath can end a life, otherwise the one given.</summary>
        public static string MuggingBody(string bodyKey)
        {
            PermadeathDirector director = PermadeathDirector.Instance;
            return director != null && director.Warns ? "reallife_pd_mugged_body" : bodyKey;
        }
    }
}
