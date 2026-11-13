using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>Which further buttons an account's profile has, by its key, as the old profile decides them. Game-free.</summary>
    public static class FlareScreenRules
    {
        /// <summary>A player of the session ("p:name").</summary>
        public static bool IsPlayer(string key) => key != null && key.Length > 2 && key.StartsWith("p:", StringComparison.Ordinal);

        /// <summary>Whether the account can be written to: a player, an official, an influencer, or a resident of the city (a key of "c" and a number).</summary>
        public static bool CanMessage(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }
            return key.StartsWith("p:", StringComparison.Ordinal) || key.StartsWith("o:", StringComparison.Ordinal) || key.StartsWith("i:", StringComparison.Ordinal)
                || (key.Length > 1 && key[0] == 'c' && key[1] != ':');
        }
    }
}
