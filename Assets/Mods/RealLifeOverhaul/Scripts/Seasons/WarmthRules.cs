using System;

namespace RealLifeOverhaul.Seasons
{
    /// <summary>
    /// Which pieces of clothing are warm, by the name of the piece: the one list the street's people are dressed from
    /// (<see cref="SeasonGame"/>) and the cold's energy cost on the player reads (Events.Weather.Wardrobe). Game-free.
    /// </summary>
    public static class WarmthRules
    {
        // The game's warm pieces, by their variants' names (MaleSweater, FemHoodie, SuitJacket, HighNeckShirt), and the mod's bomber:
        // the game has no coats.
        public static readonly string[] WarmTops = { "Sweater", "Hoodie", "Jacket", "HighNeck", "Coat", "Cardigan", "Blazer", "Bomber" };

        public static bool IsWarmTop(string name)
        {
            return Has(name, WarmTops);
        }

        /// <summary>Whether the name holds any of the words, whatever the case.</summary>
        public static bool Has(string name, string[] words)
        {
            for (int i = 0; name != null && i < words.Length; i++)
            {
                if (name.IndexOf(words[i], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
