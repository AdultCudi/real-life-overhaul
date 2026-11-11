using Localizor;

namespace RealLifeOverhaul.Insurance
{
    /// <summary>The words of insurance that are built from the state: a cover's name, a tier's, a rival's line. Every other text is a locale key written whole where it is used.</summary>
    public static class InsuranceText
    {
        public static readonly string[] LineKeys = { "reallife_ins_line_car", "reallife_ins_line_health", "reallife_ins_line_home", "reallife_ins_line_business" };

        public static string Line(CoverLine line) => LineKeys[InsuranceCatalogue.Clamp((int)line, 0, LineKeys.Length - 1)].GetLocalization();

        public static string CarTier(int tier) => InsuranceCatalogue.CarTierKeys[InsuranceCatalogue.Tier(tier)].GetLocalization();

        public static string HomeTier(int tier) => InsuranceCatalogue.HomeTierKeys[InsuranceCatalogue.Tier(tier)].GetLocalization();

        /// <summary>The health plan a rival tycoon holds and what it has paid of their hospital bills this year, for the rich list and their panel.</summary>
        public static string RivalLine(int tycoon, RivalCover c)
        {
            string name = InsuranceBridges.RivalName != null ? InsuranceBridges.RivalName(tycoon) : string.Empty;
            string plan = InsuranceBridges.PlanName != null ? InsuranceBridges.PlanName(c.Plan) : string.Empty;
            return Fill(c.PaidYear > 0 ? "reallife_ins_rival_line_paid" : "reallife_ins_rival_line", "rival", name, "plan", plan, "amount", InsuranceGame.Money(c.PaidYear));
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
    }
}
