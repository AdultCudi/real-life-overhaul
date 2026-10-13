using System.Globalization;
using System.Text;
using RealLifeOverhaul.Meta;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// The figures the Guide's tech sector pages print: every {name} in their text is filled from the catalogues here, so
    /// a page can never say anything the rules do not do. Called once, when the feature list is built (Meta/FeatureList);
    /// each figure is worked out when the page is opened.
    /// </summary>
    public static class TechGuide
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static void AddPeopleFacts(Feature f)
        {
            f.Fact("talent_roles", Roles)
                .Fact("talent_levels", Levels)
                .Fact("talent_mentor", () => Whole(TalentCatalogue.MentorBonus / 10) + "%")
                .Fact("talent_mentees", () => Whole(TalentCatalogue.MentorsEach))
                .Fact("talent_long", () => Whole(TalentCatalogue.BurnoutLong))
                .Fact("talent_reviews", () => Whole(TalentCatalogue.ReviewsAYear))
                .Fact("talent_rating", () => Whole(TalentCatalogue.PromoteRating))
                .Fact("talent_promote_pay", () => Bp(TalentCatalogue.PromotePayBp))
                .Fact("talent_ask", () => Bp(TalentCatalogue.ReviewRiseBp))
                .Fact("talent_ask_top", () => Bp(TalentCatalogue.ReviewRiseBp * 2))
                .Fact("talent_ask_days", () => Whole(TalentCatalogue.AskDays))
                .Fact("talent_promote_days", () => Whole(TalentCatalogue.Scaled(TalentCatalogue.PromoteDaysInLevel, GameCalendar.DaysPerYear)))
                .Fact("talent_probation", () => Whole(TalentCatalogue.Scaled(TalentCatalogue.ProbationDays, GameCalendar.DaysPerYear)))
                .Fact("talent_probation_notice", () => Whole(TalentCatalogue.ProbationNotice))
                .Fact("talent_notice", () => Whole(TalentCatalogue.NoticePerYear))
                .Fact("talent_notice_most", () => Whole(TalentCatalogue.NoticeMost))
                .Fact("talent_severance", () => Whole(TalentCatalogue.SeveranceWeeksPerYear))
                .Fact("talent_severance_least", () => Whole(TalentCatalogue.SeveranceLeastWeeks))
                .Fact("talent_low_runs", () => Whole(TalentCatalogue.LowRatingsToGo))
                .Fact("talent_clause", () => Bp(TalentCatalogue.NonCompeteBp))
                .Fact("talent_clause_poach", () => Whole(100 - TalentCatalogue.NonCompetePoach / 10) + "%")
                .Fact("talent_poach_premium", () => Bp(TalentCatalogue.PoachPremiumBp))
                .Fact("talent_poach_days", () => Whole(TalentCatalogue.PoachAnswerDays))
                .Fact("talent_course_days", () => Whole(TalentCatalogue.CourseDays))
                .Fact("talent_course_gain", () => Whole(TalentCatalogue.CourseGain))
                .Fact("talent_exam_pass", () => Whole(TalentCatalogue.ExamPassBase) + "%")
                .Fact("talent_certs", Certs);
        }

        // One line per role: its name, its family's word, whether the game draws them and whether it can be done from home.
        private static string Roles()
        {
            var text = new StringBuilder();
            for (int i = 0; i < TalentCatalogue.Roles.Length; i++)
            {
                RoleDef r = TalentCatalogue.Roles[i];
                text.Append("- ").Append(TechText.Fill(r.InGame ? "reallife_guide_tech_role_game" : "reallife_guide_tech_role_payroll", "role", TechText.Role(r.Id),
                    "a", TechText.Skill(r.SkillA), "b", TechText.Skill(r.SkillB), "least", TechText.Level(r.LeastLevel), "most", TechText.Level(r.MostLevel),
                    "remote", TechText.T(r.Remote ? "reallife_guide_tech_role_remote" : "reallife_guide_tech_role_onsite"))).Append('\n');
            }
            return text.ToString();
        }

        private static string Levels()
        {
            var text = new StringBuilder();
            for (int l = 0; l < TalentCatalogue.Levels; l++)
            {
                text.Append("- ").Append(TechText.Fill("reallife_guide_tech_level", "level", TechText.Level(l), "least", Whole(TalentCatalogue.LevelSkillLeast[l]),
                    "most", Whole(TalentCatalogue.LevelSkillMost[l]), "years", Whole(TalentCatalogue.LevelYearsLeast[l]))).Append('\n');
            }
            return text.ToString();
        }

        private static string Certs()
        {
            var text = new StringBuilder();
            for (int c = 0; c < TalentCatalogue.CertCount; c++)
            {
                TalentCatalogue.CertDef cert = TalentCatalogue.Certs[c];
                text.Append("- ").Append(TechText.Fill("reallife_guide_tech_cert", "cert", TechText.Cert(c), "skill", TechText.Skill(cert.Skill), "needs", Whole(cert.Needs),
                    "fee", Guide.Money(Prices.At(cert.Fee)), "pay", Bp(cert.PayBp))).Append('\n');
            }
            return text.ToString();
        }

        internal static string Whole(int value) => value.ToString(Inv);

        internal static string Bp(int bp) => (bp / 100f).ToString("0.#", Inv) + "%";
    }
}
