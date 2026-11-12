using System;
using System.Globalization;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// What the funeral card says of the game's old age backup, as rules with no game types, checked offline (EconomyCheck
    /// FixW2Checks): the game keeps a backup's day (the game's own day count) and the time its file was written, so the card says
    /// both and never a figure it made up. Cost: a format when the card opens.
    /// </summary>
    public static class FuneralRules
    {
        /// <summary>The time a backup was written, as "3 Nov, 21:10" in the player's own language and clock.</summary>
        public static string When(DateTime saved, CultureInfo culture) => saved.ToString("d MMM, HH:mm", culture ?? CultureInfo.InvariantCulture);

        /// <summary>True when the backup the game found says its day: a day of 1 or more (the save's own count starts at 1).</summary>
        public static bool Dated(int day) => day >= 1;
    }
}
