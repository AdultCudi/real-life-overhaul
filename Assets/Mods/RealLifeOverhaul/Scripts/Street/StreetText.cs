using System.Collections.Generic;
using Localizor;

namespace RealLifeOverhaul.Street
{
    /// <summary>Street talk's words in the player's language. COST: a look-up.</summary>
    public static class StreetText
    {
        public static string T(string key) => key.GetLocalization();

        public static string T(string key, Dictionary<string, string> data) => SocialLife.T(key, data);
    }
}
