using System.Globalization;
using Extensions;
using RealLifeOverhaul.Economy;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// The tech sector's words: every figure it shows is written here, the same way in its apps, the notifications and
    /// the Guide. Every text is a key of en.json; money as the game writes it. Called when something is shown, never
    /// otherwise.
    /// </summary>
    public static class TechText
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const string Mod = "reallife";   // (keys made of parts are put together from this, so the text check reads only whole keys)

        public static string T(string key) => FinanceText.T(key);

        public static string T(string key, string name, string value) => FinanceText.T(key, name, value);

        public static string T(string key, string name, string value, string name2, string value2) => FinanceText.T(key, name, value, name2, value2);

        public static string Fill(string key, params string[] pairs) => FinanceText.Fill(key, pairs);

        public static string Pair(string labelKey, string value) => FinanceText.Pair(labelKey, value);

        /// <summary>Whole money, as the Finance app writes a day's figure.</summary>
        public static string Money(long whole) => FinanceText.Money(whole);

        /// <summary>A sum kept in cents, in whole units.</summary>
        public static string Cents(long cents) => FinanceText.Money(cents / 100f);

        /// <summary>A rate kept in cents, with its cents: "$34.50".</summary>
        public static string Rate(int cents) => (cents / 100f).ToCurrencyFormat();

        public static string Percent(int whole) => whole.ToString(Inv) + "%";

        /// <summary>Thousandths of a point as a whole per cent: "62%".</summary>
        public static string Score(int thousandths) => (thousandths / 1000).ToString(Inv) + "%";

        /// <summary>Basis points as a per cent: "12%", "1.5%".</summary>
        public static string Bp(int bp) => (bp / 100f).ToString("0.#", Inv) + "%";

        /// <summary>Thousandths as a per cent: "65%".</summary>
        public static string Mille(int thousandths) => (thousandths / 10f).ToString("0", Inv) + "%";

        public static string Whole(long n) => n.ToString("N0", Inv);

        public static string Role(int role) => T(Mod + "_talent_role_" + TalentCatalogue.Role(role).Key);

        public static string Level(int level) => T(Mod + "_talent_level_" + TalentCatalogue.LevelKeys[TalentCatalogue.Clamp(level, 0, TalentCatalogue.Levels - 1)]);

        public static string Skill(int skill) => T(Mod + "_talent_skill_" + TalentCatalogue.Skills[TalentCatalogue.Clamp(skill, 0, TalentCatalogue.SkillCount - 1)]);

        public static string School(int school) => T(Mod + "_talent_school_" + TalentCatalogue.SchoolKeys[TalentCatalogue.Clamp(school, 0, TalentCatalogue.Schools - 1)]);

        public static string Cert(int cert) => T(Mod + "_talent_cert_" + TalentCatalogue.Certs[TalentCatalogue.Clamp(cert, 0, TalentCatalogue.CertCount - 1)].Key);

        /// <summary>reallife_talent_family_{name}, the family's name in lower case.</summary>
        public static string FamilyName(Family family) => T(Mod + "_talent_family_" + family.ToString().ToLowerInvariant());

        public static string FirmName(FirmKind kind) => T(Mod + "_tech_firm_" + kind.ToString().ToLowerInvariant());

        public static string ProductName(ProductKind kind) => T(Mod + "_tech_product_" + kind.ToString().ToLowerInvariant());

        public static string SegmentName(Segment segment) => T(Mod + "_tech_segment_" + segment.ToString().ToLowerInvariant());

        public static string FocusName(Focus focus) => T(Mod + "_tech_focus_" + focus.ToString().ToLowerInvariant());

        public static string Work(WorkKind kind) => T(Mod + "_tech_work_" + kind.ToString().ToLowerInvariant());

        public static string StageName(WorkStage stage) => T(Mod + "_tech_stage_" + stage.ToString().ToLowerInvariant());

        /// <summary>A price tier, 0 to 2: "low", "standard", "premium".</summary>
        public static string Tier(int tier) => T(Mod + "_tech_tier_" + TalentCatalogue.Clamp(tier, 0, 2).ToString(Inv));

        /// <summary>A marketing level, 0 to 4, with its spend a day.</summary>
        public static string MarketingLevel(int level)
        {
            level = TalentCatalogue.Clamp(level, 0, FirmsCatalogue.MarketingDay.Length - 1);
            return T(Mod + "_tech_marketing_" + level.ToString(Inv), "amount", Money(Prices.At(FirmsCatalogue.MarketingDay[level] / 100)));
        }

        /// <summary>A game's size, 0 to 2.</summary>
        public static string Scope(int scope) => T(Mod + "_tech_scope_" + TalentCatalogue.Clamp(scope, 0, 2).ToString(Inv));

        public static string VentureName(Venture v) => T(Mod + "_vc_sector_" + v.ToString().ToLowerInvariant());

        public static string RoundName(RoundStage r) => T(Mod + "_vc_round_" + r.ToString().ToLowerInvariant());

        public static string StartupStateName(StartupState s) => T(Mod + "_vc_state_" + s.ToString().ToLowerInvariant());

        public static string Work(int work) => T(work == (int)Arrangement.Remote ? "reallife_talent_work_remote" : work == (int)Arrangement.Hybrid ? "reallife_talent_work_hybrid" : "reallife_talent_work_onsite");

        /// <summary>Years and days of a 365-day year as whole years: "7 years", "1 year", "under a year".</summary>
        public static string Years(int days)
        {
            int years = days / 365;
            return years <= 0 ? T("reallife_talent_years_none") : years == 1 ? T("reallife_talent_years_one") : T("reallife_talent_years", "years", years.ToString(Inv));
        }

        /// <summary>The certificates held, joined by commas; "none" for none.</summary>
        public static string Certs(Profile p)
        {
            var b = new System.Text.StringBuilder(64);
            for (int c = 0; c < TalentCatalogue.CertCount; c++)
            {
                if (p.Has(c))
                {
                    if (b.Length > 0)
                    {
                        b.Append(", ");
                    }
                    b.Append(Cert(c));
                }
            }
            return b.Length > 0 ? b.ToString() : T("reallife_talent_none");
        }

        /// <summary>A game day as the Finance app writes one.</summary>
        public static string Day(int day) => FinanceText.Day(day);
    }
}
