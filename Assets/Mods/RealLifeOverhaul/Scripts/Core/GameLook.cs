namespace RealLifeOverhaul
{
    /// <summary>
    /// The game's own screen effects the RLO interface turns off under its screens: the time machine's blur under the time
    /// skip's veil (the veil dims the world flat itself). Kept out of the kit's folder, whose files name no soft effect.
    /// Game side. Cost: one call a skip.
    /// </summary>
    internal static class GameLook
    {
        /// <summary>The game's blur off, as the game's own end of a skip turns it off.</summary>
        public static void Clear() => Player.BlurEffect.Disable();
    }
}
