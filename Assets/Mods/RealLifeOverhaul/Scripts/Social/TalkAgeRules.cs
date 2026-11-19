namespace RealLifeOverhaul
{
    /// <summary>
    /// How a talk's header says the age of the person talking: their exact age once the player knows them or has asked, else the
    /// band their look gives them. Both come from the person's own age in the mod's data (the one the People page and the
    /// street talk's "ask their age" give), never from the figure on screen, so the header and the answer agree.
    /// Game-free. COST: arithmetic and a switch, on a page of a talk.
    /// </summary>
    public static class TalkAgeRules
    {
        /// <summary>The key of the header's text: "{who}, {age}".</summary>
        public const string HeadKey = "reallife_talk_head";

        /// <summary>The key of an exact age: "{age} years old".</summary>
        public const string ExactKey = "reallife_talk_age_exact";

        /// <summary>The ten years an age falls in, as the key of "in their twenties"; the adults the mod makes are 18 to 85, so anything under 20 is the late teens.</summary>
        public static string BandKey(int years)
        {
            if (years <= 0)
            {
                return null;
            }
            switch (years / 10)
            {
                case 0:
                case 1: return "reallife_talk_age_teens";
                case 2: return "reallife_talk_age_twenties";
                case 3: return "reallife_talk_age_thirties";
                case 4: return "reallife_talk_age_forties";
                case 5: return "reallife_talk_age_fifties";
                case 6: return "reallife_talk_age_sixties";
                case 7: return "reallife_talk_age_seventies";
                case 8: return "reallife_talk_age_eighties";
                default: return "reallife_talk_age_nineties";
            }
        }

        /// <summary>Whether the header gives the age itself: somebody the player knows, or who has said how old they are.</summary>
        public static bool Exact(bool known, bool told)
        {
            return known || told;
        }

        /// <summary>The first letter in capitals, for the kit's line under the name where the phrase stands alone.</summary>
        public static string Sentence(string phrase)
        {
            return string.IsNullOrEmpty(phrase) ? phrase : char.ToUpperInvariant(phrase[0]) + phrase.Substring(1);
        }
    }
}
