using System.Globalization;
using System.Text;
using RealLifeOverhaul.Economy;

namespace RealLifeOverhaul.Construction
{
    /// <summary>The construction game's words and figures, the same in the app, the call, the notices and the paper.</summary>
    public static class ConstructionText
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly StringBuilder Words = new StringBuilder(96);

        public static string T(string key) => FinanceText.T(key);

        public static string T(string key, string name, string value) => FinanceText.T(key, name, value);

        public static string T(string key, string name, string value, string name2, string value2) => FinanceText.T(key, name, value, name2, value2);

        public static string T(string key, string name, string value, string name2, string value2, string name3, string value3) => FinanceText.T(key, name, value, name2, value2, name3, value3);

        /// <summary>A text with any number of its names filled in: name, value, name, value.</summary>
        public static string Fill(string key, params string[] pairs) => FinanceText.Fill(key, pairs);

        public static string Money(long amount) => FinanceText.Money(amount);

        public static string Day(int day) => FinanceText.Day(day);

        /// <summary>An hour of the game's clock (day x 24 + hour) as its day and time: "day 34, 14:00".</summary>
        public static string When(int stamp) => T("reallife_bld_value_when", "day", Day(stamp / 24), "hour", (stamp % 24).ToString("00", Inv) + ":00");

        public static string N(int value) => value.ToString(Inv);

        /// <summary>Thousandths as a per cent: "15%", "2.5%".</summary>
        public static string Share(int thousandths) => (thousandths / 10f).ToString("0.#", Inv) + "%";

        /// <summary>A rate in hundredths of a per cent a year: "9.75%".</summary>
        public static string Rate(int bp) => (bp / 100f).ToString("0.##", Inv) + "%";

        /// <summary>The contractor's licence as it stands, in words.</summary>
        public static string LicenceWords(FirmFile f)
        {
            switch (f.Licence)
            {
                case LicenceState.Applied: return T("reallife_bld_licence_applied");
                case LicenceState.Granted: return T("reallife_bld_licence_granted", "day", Day(f.LicenceUntil));
                case LicenceState.Lapsed: return T("reallife_bld_licence_lapsed");
                case LicenceState.Refused: return T("reallife_bld_licence_refused");
                default: return T("reallife_bld_licence_none");
            }
        }

        /// <summary>The uses a lot is zoned for, in words, joined.</summary>
        public static string ZonesWords(int zones)
        {
            Words.Clear();
            for (int u = 0; u < (int)BuildUse.Count; u++)
            {
                if ((zones & BuildCatalogue.Zone((BuildUse)u)) != 0)
                {
                    Words.Append(Words.Length > 0 ? ", " : string.Empty).Append(T(BuildCatalogue.UseKeys[u]));
                }
            }
            return Words.Length > 0 ? Words.ToString() : T("reallife_bld_zones_none");
        }

        /// <summary>Why the council refused, in words, joined.</summary>
        public static string Reasons(int reasons)
        {
            Words.Clear();
            string[] keys =
            {
                "reallife_bld_refused_exits", "reallife_bld_refused_parking", "reallife_bld_refused_height", "reallife_bld_refused_engineer", "reallife_bld_refused_zoning",
                "reallife_bld_refused_objections", "reallife_bld_refused_quality"
            };
            for (int b = 0; b < keys.Length; b++)
            {
                if ((reasons & (1 << b)) != 0)
                {
                    Words.Append(Words.Length > 0 ? "; " : string.Empty).Append(T(keys[b]));
                }
            }
            return Words.Length > 0 ? Words.ToString() : T("reallife_bld_refused_objections");
        }

        /// <summary>Where a project stands, in words.</summary>
        public static string StageWords(ProjectFile p)
        {
            switch (p.Stage)
            {
                case ProjectStage.Design: return T(p.PlansDone ? "reallife_bld_stage_plans_done" : p.PlansDay > 0 ? "reallife_bld_stage_plans_drawn" : "reallife_bld_stage_design");
                case ProjectStage.Planning: return T(p.Planning == PlanningState.Appealed ? "reallife_bld_stage_appeal" : "reallife_bld_stage_planning");
                case ProjectStage.Ready: return T("reallife_bld_stage_ready");
                case ProjectStage.Building:
                    return T(p.Contractor >= 0 && !p.Hired ? "reallife_bld_stage_contractor"
                        : p.Kind == ProjectKind.Refit ? "reallife_bld_stage_refit" : BuildCatalogue.StageKeys[System.Math.Max(0, p.StageSeen)]);
                case ProjectStage.Finished: return T(p.After == AfterUse.SellFlats && p.FlatsSold < p.Flats ? "reallife_bld_stage_selling" : "reallife_bld_stage_finished");
                default: return T("reallife_bld_stage_abandoned");
            }
        }

        /// <summary>A crew's people by trade, in words: "4 labourers, a mason".</summary>
        public static string GangWords(int gang)
        {
            int[] people = BuildCatalogue.Gangs[BuildCatalogue.Clamp(gang, 0, BuildCatalogue.GangCount - 1)];
            Words.Clear();
            for (int t = 0; t < people.Length; t++)
            {
                if (people[t] > 0)
                {
                    Words.Append(Words.Length > 0 ? ", " : string.Empty).Append(T("reallife_bld_people", "count", N(people[t]), "trade", T(BuildCatalogue.TradeKeys[t])));
                }
            }
            return Words.ToString();
        }
    }
}
