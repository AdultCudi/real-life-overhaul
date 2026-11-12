using System;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Where the game's log is, game-free. Windows keeps Player.log in the game's own data folder; a Mac keeps it apart,
    /// under the user's Library/Logs, with the game's data under Library/Application Support. Everything the mod says is
    /// written "next to Player.log" (a performance report, the character report, their screenshots) goes to the folder
    /// this gives, and the Guide's "Where the files are" shows it, so the words are true on every system.
    /// Checked offline (EconomyCheck, PlatformChecks). Cost: a few string operations when a report is written.
    /// </summary>
    public static class LogPlaceRules
    {
        public const string Name = "Player.log";   // the engine's name for a player's log, the same on every system

        /// <summary>
        /// The folder that holds the game's log: the folder of the path the engine gives for it, or the game's data folder
        /// where the engine names none (a game started without a log).
        /// </summary>
        public static string Folder(string log, string dataFolder)
        {
            if (string.IsNullOrEmpty(log))
            {
                return dataFolder;
            }
            int cut = Math.Max(log.LastIndexOf('/'), log.LastIndexOf('\\'));
            return cut > 0 ? log.Substring(0, cut) : dataFolder;
        }
    }
}
