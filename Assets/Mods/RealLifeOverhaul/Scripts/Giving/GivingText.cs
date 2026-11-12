using System.Collections.Generic;
using Localizor;

namespace RealLifeOverhaul.Giving
{
    /// <summary>One piece of the City Ledger about a gift: its locale key, what fills it, and whether it leads the paper.</summary>
    public struct GivingStory
    {
        public string Key;
        public Dictionary<string, string> Data;
        public bool Lead;
    }

    /// <summary>The words of giving that are built from the state: a charity's name, a rival's line, a paper piece. Every other text is a locale key written whole where it is used.</summary>
    public static class GivingText
    {
        public static string Charity(int c) => GivingCatalogue.NameKeys[GivingCatalogue.Clamp(c, 0, GivingCatalogue.NameKeys.Length - 1)].GetLocalization();

        public static string CharityAbout(int c) => GivingCatalogue.AboutKeys[GivingCatalogue.Clamp(c, 0, GivingCatalogue.AboutKeys.Length - 1)].GetLocalization();

        /// <summary>What a rival tycoon has given: in all this tax year, and the latest gift.</summary>
        public static string RivalLine(int tycoon, RivalGiving r)
        {
            string name = GivingBridges.RivalName != null ? GivingBridges.RivalName(tycoon) : string.Empty;
            return Fill("reallife_giving_rival_line", "rival", name, "year", GivingGame.Money(r.Year), "last", GivingGame.Money(r.Last), "charity", Charity(r.LastCharity));
        }

        /// <summary>A text with its names filled in: pairs of a name and what goes in its place.</summary>
        public static string Fill(string key, params string[] pairs)
        {
            string text = key.GetLocalization();
            for (int i = 0; i + 1 < pairs.Length; i += 2)
            {
                text = text.Replace("{" + pairs[i] + "}", pairs[i + 1] ?? string.Empty);
            }
            return text;
        }

        /// <summary>A gift as a story of the paper: the player's or a tycoon's, to a charity.</summary>
        public static GivingStory Story(GivingNote n, bool lead)
        {
            string giver = n.Giver < 0 ? string.Empty : GivingBridges.RivalName != null ? GivingBridges.RivalName(n.Giver) : string.Empty;
            return new GivingStory
            {
                Key = n.Giver < 0 ? "reallife_giving_paper_player" : "reallife_giving_paper_rival",
                Lead = lead,
                Data = new Dictionary<string, string> { { "rival", giver }, { "amount", GivingGame.Money(n.Amount) }, { "charity", Charity(n.Charity) } }
            };
        }
    }
}
