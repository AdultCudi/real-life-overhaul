using System.Collections.Generic;
using Localizor;

namespace RealLifeOverhaul.Health
{
    /// <summary>One piece of the City Ledger about a rival in hospital: its locale key and what fills it.</summary>
    public struct HealthStory
    {
        public string Key;
        public Dictionary<string, string> Data;
    }

    /// <summary>
    /// The words of health that are built from the state: a condition's name, a plan's, a rival's line, a paper piece. Every other text is a locale key written whole where
    /// it is used.
    /// </summary>
    public static class HealthText
    {
        public static string Name(Condition c) => HealthCatalogue.NameKey(c).GetLocalization();

        public static string PlanName(Plan p) => HealthCatalogue.PlanKeys[HealthCatalogue.Clamp((int)p, 0, HealthCatalogue.PlanKeys.Length - 1)].GetLocalization();

        /// <summary>A rival tycoon off sick or in hospital, for the rich list and their panel.</summary>
        public static string RivalLine(int tycoon, RivalHealth r)
        {
            string name = HealthBridges.RivalName != null ? HealthBridges.RivalName(tycoon) : string.Empty;
            return Fill(r.Serious ? "reallife_hc_rival_hospital" : "reallife_hc_rival_line", "rival", name, "illness", Name(r.Kind), "day", (r.Until + 1).ToString());
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

        /// <summary>A rival's stay in hospital as a story of the paper.</summary>
        public static HealthStory Story(HealthNote n)
        {
            string rival = HealthBridges.RivalName != null ? HealthBridges.RivalName(n.Rival) : string.Empty;
            return new HealthStory { Key = "reallife_hc_paper_rival", Data = new Dictionary<string, string> { { "rival", rival }, { "illness", Name(n.Kind) } } };
        }
    }
}
