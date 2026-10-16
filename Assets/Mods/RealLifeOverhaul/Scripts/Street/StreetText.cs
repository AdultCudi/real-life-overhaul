using System.Collections.Generic;
using Localizor;

namespace RealLifeOverhaul.Street
{
    /// <summary>Street talk's words in the player's language (every key is in StreetLines or written out where it is used). COST: a look-up.</summary>
    public static class StreetText
    {
        public static string T(string key) => key.GetLocalization();

        public static string T(string key, Dictionary<string, string> data) => SocialLife.T(key, data);
    }
}
