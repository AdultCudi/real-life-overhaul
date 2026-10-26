namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// Whether Uptown may stand at this load on this machine: its option is on, and in a Going Public session every machine
    /// is known to run this mod in this version, since the district's streets, ground and lots must be on every machine of
    /// the session or on none. A host knows it when its Going Public refuses a player whose mods differ from its own; a
    /// guest when Going Public found no difference between its mods and the host's. Read at the load (the session is read
    /// before any feature starts) and by the district's features whenever they ask. Nothing per frame: field reads, and in
    /// a session one read of the other mod's own setting.
    /// </summary>
    public static class GroundSession
    {
        /// <summary>True when the district may stand; else why not, one line in English for the log.</summary>
        public static bool Agrees(out string why)
        {
            if (!Settings.Uptown)
            {
                why = "its option is off";
                return false;
            }
            if (GoingPublic.IsHost && !GoingPublic.RefusesOtherMods)
            {
                why = "this machine hosts a Going Public session that lets in players whose mods differ, who would not have it";
                return false;
            }
            if (GoingPublic.IsGuest && !GoingPublic.ModsAsHost)
            {
                why = "this machine is a guest in a Going Public session whose host is not known to run this mod in this version";
                return false;
            }
            why = null;
            return true;
        }
    }
}
