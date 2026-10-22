namespace RealLifeOverhaul
{
    /// <summary>
    /// Optional companion API. Reflection callers use CaptureV1(GameInstance) on the main thread. It never runs a
    /// save event, advances the simulation or edits the supplied game. There is no work per frame.
    /// </summary>
    public static class Companion
    {
        public static string CaptureV1(GameInstance game) => CompanionGame.Capture(game);
    }

}
