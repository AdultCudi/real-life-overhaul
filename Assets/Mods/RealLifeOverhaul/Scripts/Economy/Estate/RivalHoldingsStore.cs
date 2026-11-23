using System.Collections.Generic;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The one thing the rivals' holdings keep in a save: that the game was dealt by <see cref="RivalHoldingsRules"/> in its first days, so
    /// the default option step goes on moving its buildings gently as the ranking changes. A save without the line is a game the plan has
    /// not dealt: it is left as it is unless the option says every game. Game-free.
    /// </summary>
    public static class RivalHoldingsStore
    {
        public const string Key = "reallife:rival_holdings";
        private const string Dealt = "1";

        /// <summary>True if the save's line says the game was dealt at its start.</summary>
        public static bool Read(IDictionary<string, string> data)
        {
            return data != null && data.TryGetValue(Key, out string line) && line == Dealt;
        }

        /// <summary>Writes the line for a game that was dealt, and nothing for one that was not.</summary>
        public static void Write(IDictionary<string, string> data, bool dealt)
        {
            if (data == null)
            {
                return;
            }
            if (dealt)
            {
                data[Key] = Dealt;
            }
            else
            {
                data.Remove(Key);
            }
        }
    }
}
