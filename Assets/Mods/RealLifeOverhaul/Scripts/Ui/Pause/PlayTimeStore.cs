using System.Globalization;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The time played in a city, kept in its save for the pause menu's card: one line of the save's modData under
    /// <see cref="Key"/>, "v=1^seconds". The game keeps no play time of its own. A save without the line starts at nothing
    /// (an older save counts from the day it is first played with this version); a line of a later version, or one that
    /// cannot be read, gives nothing and is kept as it was read. Game-free.
    /// </summary>
    public static class PlayTimeStore
    {
        public const string Key = "reallife:playtime";
        public const int Version = 1;
        public const long Most = 100L * 365L * 24L * 3600L;   // seconds: a century, past any real play, so a damaged figure is refused

        public static string Write(long seconds)
        {
            if (seconds < 0L)
            {
                seconds = 0L;
            }
            return "v=" + Version.ToString(CultureInfo.InvariantCulture) + "^" + (seconds > Most ? Most : seconds).ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>Reads a line; false (and nothing) if it is of another version or cannot be read. An empty line is a save without one: true, nothing.</summary>
        public static bool Read(string text, out long seconds)
        {
            seconds = 0L;
            if (string.IsNullOrEmpty(text))
            {
                return true;
            }
            string[] parts = text.Split('^');
            if (parts.Length < 2 || parts[0] != "v=" + Version.ToString(CultureInfo.InvariantCulture))
            {
                return false;
            }
            if (!long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out long read) || read > Most)
            {
                return false;
            }
            seconds = read;
            return true;
        }
    }
}
