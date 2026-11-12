namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// What the jobs board's filters mean: a post is near when its building is within a short walk or ride of the player's home (or it is worked from
    /// home), and open now when the workplace is open in the game's own schedule (or the work is done from home). The facts are read from the game
    /// by the Career page; the meaning is here, so EconomyCheck holds it. Game-free.
    /// </summary>
    public static class PostFilterRules
    {
        public const int NearMetres = 1200;   // as the crow flies from the home's door

        /// <summary>Near the home: within <see cref="NearMetres"/>, or work done from home. A post whose place is unknown (-1) is not near.</summary>
        public static bool Near(int metres, bool remote) => remote || (metres >= 0 && metres <= NearMetres);

        /// <summary>Open now: done from home, or its workplace is not shut by the game's schedule.</summary>
        public static bool Open(bool remote, bool shutNow) => remote || !shutNow;
    }
}
