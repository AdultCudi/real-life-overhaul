using System.Collections.Generic;
using Localizor;

namespace RealLifeOverhaul.Dynasty
{
    /// <summary>One piece of the City Ledger about a handover: its locale key, what fills it, and whether it leads the paper.</summary>
    public struct DynastyStory
    {
        public string Key;
        public Dictionary<string, string> Data;
        public bool Lead;
    }

    /// <summary>The words of the dynasty that are built from the state: a reason, a rival's line, a paper piece. Every other text is a locale key written whole where it is used.</summary>
    public static class DynastyText
    {
        private static readonly string[] RetireKeys =
        {
            "reallife_dynasty_retire_ok", "reallife_dynasty_retire_off", "reallife_dynasty_retire_running", "reallife_dynasty_retire_young", "reallife_dynasty_retire_no_heir",
            "reallife_dynasty_retire_session", "reallife_dynasty_retire_law", "reallife_dynasty_retire_elder", "reallife_dynasty_retire_no_will"
        };

        private static readonly string[] WillKeys =
        {
            "reallife_dynasty_will_ok", "reallife_dynasty_will_too_many", "reallife_dynasty_will_share", "reallife_dynasty_will_over", "reallife_dynasty_will_heir",
            "reallife_dynasty_will_age", "reallife_dynasty_will_keep", "reallife_dynasty_will_same", "reallife_dynasty_will_broke", "reallife_dynasty_will_off"
        };

        public static string KeyOf(RetireWhy why) => RetireKeys[DynastyCatalogue.Clamp((int)why, 0, RetireKeys.Length - 1)];

        public static string KeyOf(WillWhy why) => WillKeys[DynastyCatalogue.Clamp((int)why, 0, WillKeys.Length - 1)];

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

        /// <summary>A rival's line: who leads the holding, how old they are, and what the last handover cost.</summary>
        public static string RivalLine(int tycoon, RivalLine r, int day, int yearDays)
        {
            string name = DynastyBridges.RivalName != null ? DynastyBridges.RivalName(tycoon) : string.Empty;
            string age = DynastyRivals.Age(r, day, yearDays).ToString();
            if (r.HandDay < 0)
            {
                return Fill("reallife_dynasty_rival_first", "rival", name, "age", age);
            }
            return Fill(r.HandMode == HandMode.Retire ? "reallife_dynasty_rival_retired" : "reallife_dynasty_rival_died", "rival", name, "age", age, "n", r.Generation.ToString(),
                "day", r.HandDay.ToString(), "tax", DynastyGame.Money(r.HandTax));
        }

        /// <summary>A handover as a story of the paper: the family's or a tycoon's.</summary>
        public static DynastyStory Story(DynastyNote n, bool lead)
        {
            string key = n.Who < 0 ? (n.Mode == HandMode.Retire ? "reallife_dynasty_paper_player_retire" : "reallife_dynasty_paper_player_death")
                : n.Mode == HandMode.Elder ? "reallife_dynasty_paper_rival_elder" : n.Mode == HandMode.Retire ? "reallife_dynasty_paper_rival_retire" : "reallife_dynasty_paper_rival_death";
            return new DynastyStory
            {
                Key = key, Lead = lead,
                Data = new Dictionary<string, string> { { "from", n.From }, { "to", n.To }, { "rival", n.From }, { "tax", DynastyGame.Money(n.Tax) }, { "n", n.Generation.ToString() } }
            };
        }
    }
}
