using System.Collections.Generic;
using RealLifeOverhaul.Careers;
using RealLifeOverhaul.Construction;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Tech;
using RealLifeOverhaul.Utilities;

namespace RealLifeOverhaul.Staffing
{
    public static partial class StaffTable
    {
        // The game's skills a role is hired under, as the table names them (reallife_staff_skill_{name}).
        private const string SkCustomer = "reallife_staff_skill_customer_service", SkCleaning = "reallife_staff_skill_cleaning", SkDriver = "reallife_staff_skill_delivery_driver";
        private const string SkDesigner = "reallife_staff_skill_graphic_designer", SkHr = "reallife_staff_skill_hr_manager", SkAny = "reallife_staff_skill_any";

        /// <summary>The key of a skill's name in the table's own texts, for a skill of the game's (null for a skill the table does not name).</summary>
        public static string SkillWord(string gameSkill)
        {
            switch (gameSkill)
            {
                case TalentCatalogue.Programmer: return "reallife_staff_skill_programmer";
                case TalentCatalogue.GraphicDesigner: return SkDesigner;
                case TalentCatalogue.CustomerService: return SkCustomer;
                case TalentCatalogue.HrManager: return SkHr;
                case TalentCatalogue.Headhunter: return "reallife_staff_skill_headhunter";
                case TalentCatalogue.Lawyer: return "reallife_staff_skill_lawyer";
                default: return null;
            }
        }

        private static StaffRole Plain(string id, StaffNote[] about, StaffNote[] gets, StaffNote[] trains) => new StaffRole(id, "reallife" + "_staff_role_" + id, -1, about, gets, trains);

        private static StaffNote About(string id) => Note("reallife" + "_staff_role_about_" + id);

        private static StaffNote Hire(string skillKey) => Note("reallife_staff_get_game", Wd("skill", skillKey));

        private static StaffNote Train(string skillKey) => Note("reallife_staff_train_game", Wd("skill", skillKey));

        private static StaffNote Auto(long wage, int pace) => Note("reallife_staff_get_auto", M("wage", wage), P("pace", pace));

        private static StaffNote NoTraining() => Note("reallife_staff_train_none");

        private static StaffRole[] MakeRoles()
        {
            var roles = new List<StaffRole>(80);
            for (int i = 0; i < TalentCatalogue.RoleCount; i++)
            {
                roles.Add(TalentRole(TalentCatalogue.Role(i)));
            }
            roles.Add(Plain("dc_operator", Ids2("dc_operator"), Ids2(Auto(InfraCatalogue.CrewWage[InfraCatalogue.Operators], InfraCatalogue.CrewPace)), Ids2(NoTraining())));
            roles.Add(Plain("dc_engineer", Ids2("dc_engineer"), Ids2(Auto(InfraCatalogue.CrewWage[InfraCatalogue.Engineers], InfraCatalogue.CrewPace)), Ids2(NoTraining())));
            roles.Add(Plain("dc_guard", Ids2("dc_guard"), Ids2(Auto(InfraCatalogue.CrewWage[InfraCatalogue.Security], InfraCatalogue.CrewPace)), Ids2(NoTraining())));
            roles.Add(Plain("cloud_ops", Ids2("cloud_ops"), Ids2(Auto(InfraCatalogue.OpsWage, InfraCatalogue.CrewPace)), Ids2(NoTraining())));
            roles.Add(Plain("bank_teller", Ids2("bank_teller"), Ids2(Auto(OwnBankCatalogue.CrewWage[OwnBankCatalogue.Tellers], OwnBankCatalogue.CrewPace)), Ids2(NoTraining())));
            roles.Add(Plain("bank_relationship_manager", Ids2("bank_relationship_manager"), Ids2(Auto(OwnBankCatalogue.CrewWage[OwnBankCatalogue.Managers], OwnBankCatalogue.CrewPace)),
                Ids2(NoTraining())));
            roles.Add(Plain("bank_loan_officer", Ids2("bank_loan_officer"), Ids2(Auto(OwnBankCatalogue.CrewWage[OwnBankCatalogue.Officers], OwnBankCatalogue.CrewPace)), Ids2(NoTraining())));
            roles.Add(Plain("bank_compliance", Ids2("bank_compliance"), Ids2(Auto(OwnBankCatalogue.CrewWage[OwnBankCatalogue.Compliance], OwnBankCatalogue.CrewPace)), Ids2(NoTraining())));
            roles.Add(Plain("field_engineer", Ids2("field_engineer"), Ids2(Auto(BroadbandCatalogue.CrewWage[BroadbandCatalogue.Field], BroadbandCatalogue.CrewPace)), Ids2(NoTraining())));
            roles.Add(Plain("call_agent", Ids2("call_agent"), Ids2(Auto(BroadbandCatalogue.CrewWage[BroadbandCatalogue.Calls], BroadbandCatalogue.CrewPace)), Ids2(NoTraining())));
            roles.Add(Plain("network_operator", Ids2("network_operator"), Ids2(Auto(BroadbandCatalogue.CrewWage[BroadbandCatalogue.Operations], BroadbandCatalogue.CrewPace)), Ids2(NoTraining())));
            roles.Add(Plain("branch_teller", Ids2("branch_teller"), Ids2(Hire(SkCustomer)), Ids2(Train(SkCustomer), Course(StudyCatalogue.CustomerService))));
            roles.Add(Plain("salesperson", Ids2("salesperson"), Ids2(Hire(SkCustomer)), Ids2(Train(SkCustomer), Course(StudyCatalogue.CustomerService))));
            roles.Add(Plain("picker", Ids2("picker"), Ids2(Hire(SkCustomer)), Ids2(Train(SkCustomer))));
            roles.Add(Plain("shop_assistant", Ids2("shop_assistant"), Ids2(Hire(SkCustomer)), Ids2(Train(SkCustomer))));
            roles.Add(Plain("agency_agent", Ids2("agency_agent"), Ids2(Hire(SkCustomer)), Ids2(Train(SkCustomer), Course(StudyCatalogue.CustomerService))));
            roles.Add(Plain("cleaner", Ids2("cleaner"), Ids2(Hire(SkCleaning)), Ids2(Train(SkCleaning))));
            roles.Add(Plain("delivery_driver", Ids2("delivery_driver"), Ids2(Hire(SkDriver)), Ids2(Train(SkDriver))));
            roles.Add(Plain("academy_teacher", Ids2("academy_teacher"), Ids2(Note("reallife_staff_get_academy", Wd("skill_a", SkHr), Wd("skill_b", SkCustomer))), Ids2(Train(SkHr))));
            roles.Add(Plain("hotel_reception", Ids2("hotel_reception"), Ids2(Note("reallife_staff_get_level")), Ids2(NoTraining())));
            roles.Add(Plain("hotel_housekeeping", Ids2("hotel_housekeeping"), Ids2(Note("reallife_staff_get_level")), Ids2(NoTraining())));
            roles.Add(Plain("hotel_maintenance", Ids2("hotel_maintenance"), Ids2(Note("reallife_staff_get_level")), Ids2(NoTraining())));
            roles.Add(Plain("site_crew", Ids2("site_crew"),
                Ids2(Note("reallife_staff_get_crew", N("offers", BuildCatalogue.OffersAWeek), N("skill_low", BuildCatalogue.SkillLeast), N("skill_high", BuildCatalogue.SkillMost), N("most",
                    BuildCatalogue.CrewsMost),
                    M("labourer", BuildCatalogue.TradeRate[0] * 100L), M("mason", BuildCatalogue.TradeRate[1] * 100L), M("crane", BuildCatalogue.TradeRate[2] * 100L), M("electrician",
                        BuildCatalogue.TradeRate[3] * 100L),
                    M("plumber", BuildCatalogue.TradeRate[4] * 100L), M("foreman", BuildCatalogue.ForemanRate * 100L))),
                Ids2(Note("reallife_staff_train_crew", P("raise", BuildCatalogue.RaisePermille), N("idle", BuildCatalogue.IdleFrom)))));
            roles.Add(Plain("office_architect", Ids2("office_architect"), Ids2(Hire(SkDesigner), Note("reallife_staff_get_office", N("staff", BuildCatalogue.StaffMost))),
                Ids2(Train(SkDesigner), Course(StudyCatalogue.Architecture), Course(StudyCatalogue.ArchitectLicence))));
            roles.Add(Plain("office_engineer", Ids2("office_engineer"), Ids2(Hire(SkAny), Note("reallife_staff_get_office", N("staff", BuildCatalogue.StaffMost)),
                Note("reallife_staff_get_engineer_self")),
                Ids2(Train(SkAny), Course(StudyCatalogue.CivilEngineering), Course(StudyCatalogue.CharteredEngineer))));
            roles.Add(Plain("office_estimator", Ids2("office_estimator"), Ids2(Hire(SkAny), Note("reallife_staff_get_office", N("staff", BuildCatalogue.StaffMost))), Ids2(Train(SkAny))));
            roles.Add(Plain("site_manager", Ids2("site_manager"), Ids2(Hire(SkCustomer), Note("reallife_staff_get_office", N("staff", BuildCatalogue.StaffMost))),
                Ids2(Train(SkCustomer), Course(StudyCatalogue.ConstructionManagement))));
            return roles.ToArray();
        }

        private static StaffNote[] Ids2(params StaffNote[] notes) => notes;

        private static StaffNote[] Ids2(string id) => new[] { About(id) };

        private static StaffNote Course(int id)
        {
            CourseDef c = StudyCatalogue.Find(id);
            return Note("reallife_staff_train_course", Wd("course", "reallife" + "_study_course_" + c.Key), M("fee", c.Fee * 100L), N("sessions", c.Sessions), N("homework", c.Homework));
        }

        // ---------------------------------------------------------------- the professionals

        private static StaffRole TalentRole(RoleDef r)
        {
            string family = "reallife" + "_talent_family_" + r.Family.ToString().ToLowerInvariant();
            var about = new List<StaffNote>(4)
            {
                Note(r.InGame ? "reallife_staff_t_about_game" : "reallife_staff_t_about_payroll", Wd("family", family), Wd("skill_a", "reallife" + "_talent_skill_" + TalentCatalogue.Skills[r.SkillA]),
                    Wd("skill_b", "reallife" + "_talent_skill_" + TalentCatalogue.Skills[r.SkillB]), Wd("least", LevelWord(r.LeastLevel)), Wd("most", LevelWord(r.MostLevel)),
                    N("skill_least", TalentCatalogue.LevelSkillLeast[r.LeastLevel]), N("skill_most", TalentCatalogue.LevelSkillMost[r.MostLevel]), P("pay", r.Pay),
                    r.InGame ? Wd("game", SkillWord(r.GameSkill)) : Wd("game", SkAny)),
                Note(r.Remote ? "reallife_staff_t_remote" : "reallife_staff_t_onsite"),
                Note("reallife_staff_t_output", N("junior", StaffEffects.Points(r.Id, TalentCatalogue.Junior, StaffEffects.MidSkill(TalentCatalogue.Junior))),
                    N("mid", StaffEffects.Points(r.Id, TalentCatalogue.Mid, StaffEffects.MidSkill(TalentCatalogue.Mid))),
                    N("senior", StaffEffects.Points(r.Id, TalentCatalogue.Senior, StaffEffects.MidSkill(TalentCatalogue.Senior))),
                    P("junior_out", TalentCatalogue.LevelOutput[0]), P("principal_out", TalentCatalogue.LevelOutput[4]))
            };
            var gets = new List<StaffNote>(8)
            {
                Note("reallife_staff_t_market", P("share", HiringCatalogue.FamilyShare[(int)r.Family]), P("scarce", HiringCatalogue.FamilyScarcity[(int)r.Family]), N("ad_a",
                    HiringCatalogue.AdDays[0]),
                    N("ad_b", HiringCatalogue.AdDays[1]), N("ad_c", HiringCatalogue.AdDays[2]), M("cost", HiringCatalogue.AdDayCost * 100L), N("week", HiringCatalogue.AdWeekMost), N("ads",
                        HiringCatalogue.AdsMost),
                    P("level_a", HiringCatalogue.LevelShare[r.LeastLevel]), P("level_b", HiringCatalogue.LevelShare[r.MostLevel]), Wd("least", LevelWord(r.LeastLevel)), Wd("most",
                        LevelWord(r.MostLevel)))
            };
            if (r.MostLevel >= HiringCatalogue.SearchLeastLevel)
            {
                gets.Add(Note("reallife_staff_t_search", P("fee", HiringCatalogue.SearchFeeBp / 10), P("retainer", HiringCatalogue.SearchRetainerShare), N("days_a", HiringCatalogue.SearchDaysLeast),
                    N("days_b", HiringCatalogue.SearchDaysMost), N("candidates", HiringCatalogue.SearchCandidates), N("open", HiringCatalogue.SearchesMost), Wd("level",
                        LevelWord(HiringCatalogue.SearchLeastLevel))));
            }
            if (HiringRules.CrewRole(r.Id))
            {
                gets.Add(Note("reallife_staff_t_crew", P("fee_a", HiringCatalogue.CrewFeeBp[0] / 10), P("fee_d", HiringCatalogue.CrewFeeBp[3] / 10), N("most", HiringCatalogue.CrewMost),
                    N("open", HiringCatalogue.CrewSearchesMost), N("candidates", HiringCatalogue.CrewCandidatesMost), N("days_a", HiringCatalogue.CrewDaysLeast), N("days_b",
                        HiringCatalogue.CrewDaysMost),
                    Wd("from", LevelWord(HiringCatalogue.CrewLeastLevel)), Wd("to", LevelWord(HiringCatalogue.CrewMostLevel))));
            }
            if (r.InGame)
            {
                gets.Add(Note("reallife_staff_t_game", Wd("skill", SkillWord(r.GameSkill)), N("more", HiringCatalogue.OfficeBonusLeast), P("share", HiringCatalogue.OfficeBonusShare),
                    N("floor", HiringCatalogue.OfficeSkillFloor)));
                var same = new List<string>(8);
                for (int i = 0; i < TalentCatalogue.RoleCount; i++)
                {
                    RoleDef o = TalentCatalogue.Role(i);
                    if (o.Id != r.Id && o.GameSkill == r.GameSkill)
                    {
                        same.Add(o.Key);
                    }
                }
                gets.Add(Links("reallife_staff_t_retrain", same.ToArray(), Wd("skill", SkillWord(r.GameSkill))));
            }
            else
            {
                gets.Add(Note("reallife_staff_t_payroll"));
            }
            gets.Add(Note("reallife_staff_t_pay", P("pay", r.Pay), M("least", TalentCatalogue.RateLeast), P("short_a", HiringCatalogue.ShortageLeast), P("short_b", HiringCatalogue.ShortageMost),
                P("employed", HiringCatalogue.EmployedPremiumBp / 10), P("unemployed", HiringCatalogue.UnemployedDiscountBp / 10)));
            var trains = new List<StaffNote>(8)
            {
                Note("reallife_staff_t_course", Wd("skill_a", "reallife" + "_talent_skill_" + TalentCatalogue.Skills[r.SkillA]), Wd("skill_b",
                    "reallife" + "_talent_skill_" + TalentCatalogue.Skills[r.SkillB]),
                    N("days", TalentCatalogue.CourseDays), N("gain", TalentRules.CourseGain(0)), M("fee_low", TalentRules.CourseFee(0) * 100L), M("fee_high", TalentRules.CourseFee(50) * 100L)),
                Note("reallife_staff_t_degree", N("days", TalentCatalogue.DegreeDays), N("gain", TalentCatalogue.DegreeGain))
            };
            for (int c = 0; c < TalentCatalogue.CertCount; c++)
            {
                TalentCatalogue.CertDef cert = TalentCatalogue.Certs[c];
                if (cert.Skill == r.SkillA || cert.Skill == r.SkillB)
                {
                    trains.Add(Note("reallife_staff_t_cert", Wd("cert", "reallife" + "_talent_cert_" + cert.Key), N("needs", cert.Needs), M("fee", cert.Fee * 100L), P("pay", cert.PayBp / 10),
                        N("exam", TalentCatalogue.ExamDays)));
                }
            }
            if (r.LeastLevel <= TalentCatalogue.Mid)
            {
                trains.Add(Note("reallife_staff_t_mentor", P("bonus", TalentCatalogue.MentorBonus), N("each", TalentCatalogue.MentorsEach), Wd("level", LevelWord(TalentCatalogue.Senior))));
            }
            for (int p = 0; p < ProCatalogue.All.Length; p++)
            {
                Profession pro = ProCatalogue.All[p];
                for (int k = 0; k < pro.Ranks.Length; k++)
                {
                    if (pro.Ranks[k].Role == r.Id)
                    {
                        trains.Add(Note("reallife_staff_t_career", Wd("pro", "reallife" + "_pro_name_" + pro.Key), N("rank", k + 1), N("need", pro.Ranks[k].Need), M("pay", pro.Ranks[k].Pay)));
                        for (int q = 0; q < pro.Ranks[k].Quals.Length && k == 0; q++)
                        {
                            trains.Add(Course(pro.Ranks[k].Quals[q]));
                        }
                        p = ProCatalogue.All.Length;
                        break;
                    }
                }
            }
            return new StaffRole(r.Key, "reallife" + "_talent_role_" + r.Key, r.Id, about.ToArray(), gets.ToArray(), trains.ToArray());
        }
    }
}
