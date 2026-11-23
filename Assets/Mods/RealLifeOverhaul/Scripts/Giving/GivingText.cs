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

        /// <summary>What a gift that took the giver's name is called: the cause's word (a kitchen, a wing, a scholarship) with the giver's name and, for a district, its name.</summary>
        public static string Naming(GivingNaming n)
        {
            string district = n.Tag >= 0 && n.Tag < GivingState.Districts ? DistrictOf(n.Tag) : string.Empty;
            string key = GivingCatalogue.NamingKeys[GivingCatalogue.Clamp(n.Charity, 0, GivingCatalogue.NamingKeys.Length - 1)];
            return Fill(key, "name", string.IsNullOrEmpty(n.Name) ? Fill("reallife_giving_naming_anon") : n.Name, "charity", Charity(n.Charity), "district", district);
        }

        private static string DistrictOf(int tag)
        {
            var list = new List<string>(GivingState.Districts);
            GivingGame.Districts(list);
            return tag < list.Count ? GivingGame.DistrictName(list[tag]) : string.Empty;
        }

        /// <summary>A gift as a story of the paper: the player's or a tycoon's, to a charity, or the player's naming gift with what it is called.</summary>
        public static GivingStory Story(GivingNote n, bool lead)
        {
            string giver = n.Giver < 0 ? string.Empty : GivingBridges.RivalName != null ? GivingBridges.RivalName(n.Giver) : string.Empty;
            var data = new Dictionary<string, string> { { "rival", giver }, { "amount", GivingGame.Money(n.Amount) }, { "charity", Charity(n.Charity) } };
            string key = n.Giver < 0 ? "reallife_giving_paper_player" : "reallife_giving_paper_rival";
            if (n.Giver < 0 && n.Naming == 1)
            {
                key = "reallife_giving_paper_naming";
                data["naming"] = Naming(new GivingNaming { Charity = n.Charity, Tag = n.Tag, Name = GivingGame.PlayerName });
            }
            return new GivingStory { Key = key, Lead = lead, Data = data };
        }
    }
}
