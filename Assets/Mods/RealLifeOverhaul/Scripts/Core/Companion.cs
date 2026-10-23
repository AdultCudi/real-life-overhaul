namespace RealLifeOverhaul
{
    /// <summary>
    /// The optional companion API, for other mods that read RLO by reflection (contracts/companion/v1). Called on the main
    /// thread, it never runs a save event, advances the simulation or changes the game it is given. Nothing per frame;
    /// a call costs one capture (<see cref="CompanionGame.Capture"/>).
    /// </summary>
    public static class Companion
    {
        /// <summary>The report as JSON, or null when none can be made: no session, Going Public, or a fault (logged once).</summary>
        public static string CaptureV1(GameInstance game) => CompanionGame.CaptureSafe(game);
    }
}
