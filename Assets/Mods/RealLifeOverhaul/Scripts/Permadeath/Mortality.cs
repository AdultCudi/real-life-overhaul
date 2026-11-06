namespace RealLifeOverhaul.Permadeath
{
    /// <summary>
    /// The one door the features where a life can be in danger call (a crash, being hit on foot, a mugging, the lender's
    /// men, a flu). Each asks <see cref="Judge"/> where it would hurt the player and does as it always did, unless the
    /// answer is a near miss (hurt as before, then <see cref="NearMiss"/>) or a death (<see cref="Die"/> takes over).
    /// Safe at any time: with the feature off or not allowed every answer is nothing. A few field reads and one random number.
    /// </summary>
    public static class Mortality
    {
        /// <summary>What a dangerous moment comes to; <paramref name="measure"/> is the speed in m/s of a crash or a hit.</summary>
        public static Verdict Judge(DeathCause cause, float measure = 0f)
        {
            PermadeathDirector director = PermadeathDirector.Instance;
            return director != null ? director.Judge(cause, measure) : Verdict.None;
        }

        /// <summary>The first moment of its kind: remembered, and a notice that the next could be fatal. Call it as the injury is shown.</summary>
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
        /// One game hour of the player's illness, from Home's health: shows the warnings in turn and may end the life after
        /// the third. True while a flu no longer clears up by itself (the caller then holds it on).
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
