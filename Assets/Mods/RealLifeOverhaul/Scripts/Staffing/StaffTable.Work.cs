using System.Collections.Generic;
using RealLifeOverhaul.Careers;
using RealLifeOverhaul.Construction;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Economy.Dealership;
using RealLifeOverhaul.Shops;
using RealLifeOverhaul.Tech;
using RealLifeOverhaul.Utilities;

namespace RealLifeOverhaul.Staffing
{
    public static partial class StaffTable
    {
        // A line written under one text key: key_does, key_count and key_lack.
        private static StaffLine L(string[] roles, StaffNeed need, string key, StaffFigure[] does, StaffFigure[] counts, StaffFigure[] lack, StaffRate rate = default, string unit = "")
        {
            return Line(roles, need, Note(key + "_does", does), counts == null ? null : Note(key + "_count", counts), lack == null ? null : Note(key + "_lack", lack), rate, unit);
        }

        private static StaffFigure[] F(params StaffFigure[] figures) => figures;

        private static void WorkPlaces(List<StaffPlace> into)
        {
            into.Add(OwnBankPlace());
            into.Add(BranchPlace());
            into.Add(ShowroomPlace());
            into.Add(OnlinePlace());
            into.Add(CleaningPlace());
            into.Add(AgencyPlace());
            into.Add(HotelPlace());
            into.Add(BuilderPlace());
            into.Add(AcademyPlace());
            into.Add(BroadbandPlace());
            into.Add(ExecutivesPlace());
            for (int i = 0; i < ShopsCatalogue.Kinds.Length; i++)
            {
                into.Add(ShopPlace(ShopsCatalogue.Kinds[i]));
            }
        }

        // ---------------------------------------------------------------- money

        private static StaffPlace OwnBankPlace()
        {
            var lines = new[]
            {
                L(Ids("bank_teller"), StaffNeed.Required, "reallife" + "_staff_bank_teller", F(M("each", OwnBankCatalogue.TellerCents)), F(M("each", OwnBankCatalogue.TellerCents), M("wage",
                    OwnBankCatalogue.CrewWage[OwnBankCatalogue.Tellers])),
                    F(P("working", EndScaleRules.WorkingLeast), X("times", EndScaleRules.OutageMost)), new StaffRate(OwnBankCatalogue.TellerCents, 1)),
                L(Ids("bank_relationship_manager"), StaffNeed.Required, "reallife" + "_staff_bank_manager", F(M("each", OwnBankCatalogue.ManagerCents)),
                    F(M("each", OwnBankCatalogue.ManagerCents), M("wage", OwnBankCatalogue.CrewWage[OwnBankCatalogue.Managers])), F(P("working", EndScaleRules.WorkingLeast), X("times",
                        EndScaleRules.OutageMost)),
                    new StaffRate(OwnBankCatalogue.ManagerCents, 1)),
                L(Ids("bank_loan_officer"), StaffNeed.Required, "reallife" + "_staff_bank_officer", F(M("each", OwnBankCatalogue.OfficerCents)),
                    F(M("each", OwnBankCatalogue.OfficerCents), M("wage", OwnBankCatalogue.CrewWage[OwnBankCatalogue.Officers])), F(P("working", EndScaleRules.WorkingLeast), X("times",
                        EndScaleRules.OutageMost)),
                    new StaffRate(OwnBankCatalogue.OfficerCents, 1)),
                L(Ids("bank_compliance"), StaffNeed.Required, "reallife" + "_staff_bank_compliance", F(M("each", OwnBankCatalogue.ComplianceCents)),
                    F(N("t1", BankTiers.ComplianceLeast[0]), N("t2", BankTiers.ComplianceLeast[1]), N("t3", BankTiers.ComplianceLeast[2]), M("each", OwnBankCatalogue.ComplianceCents),
                        M("wage", OwnBankCatalogue.CrewWage[OwnBankCatalogue.Compliance])), F(P("working", EndScaleRules.WorkingLeast), X("times", EndScaleRules.OutageMost)))
            };
            var also = new[]
            {
                Note("reallife_staff_bank_also_hall", M("day", OwnBankCatalogue.StaffDayCents), P("savings", OwnBankCatalogue.StaffOnSavings), P("loans", OwnBankCatalogue.StaffOnLoans)),
                Note("reallife_staff_bank_also_crews", P("pace", OwnBankCatalogue.CrewPace)),
                Links("reallife_staff_bank_also_branch", Ids("branch_teller"))
            };
            return new StaffPlace("own_bank", lines, also);
        }

        private static StaffPlace BranchPlace()
        {
            var lines = new[]
            {
                L(Ids("branch_teller"), StaffNeed.Required, "reallife" + "_staff_branch_teller",
                    F(N("serve", BranchCatalogue.ServePerTeller), P("low", BranchCatalogue.SkillLeast), P("high", BranchCatalogue.SkillMost), N("wait", BranchCatalogue.WaitSeconds),
                        N("player", BranchCatalogue.PlayerSkill)),
                    F(N("serve", BranchCatalogue.ServePerTeller), N("player", BranchCatalogue.PlayerSkill)), F(N("wait", BranchCatalogue.WaitSeconds)),
                    new StaffRate(BranchCatalogue.ServePerTeller, 1)),
                L(Ids("cleaner"), StaffNeed.Optional, "reallife" + "_staff_branch_cleaner", F(), null, null)
            };
            return new StaffPlace("bank_branch", lines, new[] { Note("reallife_staff_branch_also") });
        }

        private static StaffPlace ShowroomPlace()
        {
            var lines = new[]
            {
                L(Ids("salesperson"), StaffNeed.Required, "reallife" + "_staff_showroom_sales",
                    F(N("serve", DealerCatalogue.ServePerStaff), P("low", DealerCatalogue.SkillLeast), P("high", DealerCatalogue.SkillMost), P("offer", DealerCatalogue.SkillOffer),
                        P("floor", DealerCatalogue.StaffFloor), P("floor_low", DealerCatalogue.StaffFloorLeast), P("floor_high", DealerCatalogue.StaffFloorMost)),
                    F(N("serve", DealerCatalogue.ServePerStaff)), F(),
                    new StaffRate(DealerCatalogue.ServePerStaff, 1)),
                L(Ids("cleaner"), StaffNeed.Optional, "reallife" + "_staff_showroom_cleaner", F(), null, null)
            };
            return new StaffPlace("car_showroom", lines, new[] { Note("reallife_staff_showroom_also") });
        }

        private static StaffPlace OnlinePlace()
        {
            var lines = new[]
            {
                L(Ids("picker"), StaffNeed.Required, "reallife" + "_staff_online_picker",
                    F(N("per", OnlineCatalogue.PickPerHour), N("from", OnlineCatalogue.WarehouseFrom), N("until", OnlineCatalogue.WarehouseUntil)),
                    F(N("per", OnlineCatalogue.PickPerHour)), F(N("idle", OnlineCatalogue.IdleAlert), N("wait", OnlineCatalogue.WaitMost)),
                    new StaffRate(OnlineCatalogue.PickPerHour, 1)),
                L(Ids("delivery_driver"), StaffNeed.Optional, "reallife" + "_staff_online_driver",
                    F(N("from", OnlineCatalogue.DriveFrom), N("until", OnlineCatalogue.DriveUntil), T("hours", OnlineCatalogue.DriverMinutes * 10 / 60), N("load", OnlineCatalogue.LoadMinutes),
                        N("stop", OnlineCatalogue.StopMinutes), N("van_least", OnlineCatalogue.VanLeast), N("van_most", OnlineCatalogue.VanMost)),
                    F(N("van_most", OnlineCatalogue.VanMost)), F(M("base", OnlineCatalogue.CourierBase), M("km", OnlineCatalogue.CourierPerKm), N("hours", OnlineCatalogue.CourierHours)))
            };
            return new StaffPlace("online_shop", lines, new[] { Note("reallife_staff_online_also", N("hours", OnlineCatalogue.MissingHours), P("refund", OnlineCatalogue.LateRefund)) });
        }

        private static StaffPlace CleaningPlace()
        {
            var lines = new[]
            {
                L(Ids("cleaner"), StaffNeed.Required, "reallife" + "_staff_cleaning_cleaner",
                    F(N("per", 1000 / ServicesCatalogue.ForcePerThousandSqm), N("crew", ServicesCatalogue.CrewMost), N("clients", ServicesCatalogue.ClientsMost), N("visits",
                        ServicesCatalogue.VisitsMost)),
                    F(N("per", 1000 / ServicesCatalogue.ForcePerThousandSqm), N("crew", ServicesCatalogue.CrewMost)),
                    F(P("penalty", ServicesCatalogue.ContractPenaltyPct * 10), N("under", ServicesCatalogue.ContractDaysUnderMost), N("misses", ServicesCatalogue.ContractMissesMost)),
                    new StaffRate(1000 / ServicesCatalogue.ForcePerThousandSqm, 1))
            };
            return new StaffPlace("cleaning_service", lines, new[] { Note("reallife_staff_cleaning_also") });
        }

        private static StaffPlace AgencyPlace()
        {
            int sale = Economy.AgencyRules.Hours(true, 100), let = Economy.AgencyRules.Hours(false, 100), day = Economy.AgencyRules.Capacity(40, 50);
            var lines = new[]
            {
                L(Ids("agency_agent"), StaffNeed.Required, "reallife" + "_staff_agency_agent",
                    F(N("sale", EstateCatalogue.SaleHours), N("sale_sqm", EstateCatalogue.SaleSqmPerHour), N("let", EstateCatalogue.LetHours), N("let_sqm", EstateCatalogue.LetSqmPerHour)),
                    F(T("sale", sale), T("let", let), T("day", day), N("sqm", 100), N("weekly", 40), N("skill", 50)), F())
            };
            return new StaffPlace("estate_agency", lines, new[] { Note("reallife_staff_agency_also") });
        }

        private static StaffPlace HotelPlace()
        {
            var lines = new[]
            {
                L(Ids("hotel_reception"), StaffNeed.Required, "reallife" + "_staff_hotel_reception",
                    F(M("w0", EstateCatalogue.ReceptionWage[0] * 100L), M("w1", EstateCatalogue.ReceptionWage[1] * 100L), M("w2", EstateCatalogue.ReceptionWage[2] * 100L)), null,
                    F(P("s0", EstateCatalogue.StaffScore[0]), P("s1", EstateCatalogue.StaffScore[1]), P("s2", EstateCatalogue.StaffScore[2]))),
                L(Ids("hotel_housekeeping"), StaffNeed.Required, "reallife" + "_staff_hotel_housekeeping",
                    F(M("h0", EstateCatalogue.HousekeepingPerRoom[0] * 100L), M("h1", EstateCatalogue.HousekeepingPerRoom[1] * 100L), M("h2", EstateCatalogue.HousekeepingPerRoom[2] * 100L),
                        P("full", EstateCatalogue.FullHouseShare)),
                    null, F(P("c0", EstateCatalogue.CleanTarget[0]), P("c1", EstateCatalogue.CleanTarget[1]), P("c2", EstateCatalogue.CleanTarget[2]))),
                L(Ids("hotel_maintenance"), StaffNeed.Required, "reallife" + "_staff_hotel_maintenance",
                    F(M("m0", EstateCatalogue.MaintenancePerRoom[0] * 100L), M("m1", EstateCatalogue.MaintenancePerRoom[1] * 100L), M("m2", EstateCatalogue.MaintenancePerRoom[2] * 100L)), null, null)
            };
            return new StaffPlace("hotel", lines, new[] { Note("reallife_staff_hotel_also") });
        }

        // ---------------------------------------------------------------- building

        private static StaffPlace BuilderPlace()
        {
            int a = BuildCatalogue.StaffQualityBase, per = BuildCatalogue.StaffQualityPermille;
            string[] gangRoles = Ids("site_crew");
            var lines = new[]
            {
                L(gangRoles, StaffNeed.Required, "reallife" + "_staff_builder_crew",
                    F(T("cap", BuildCatalogue.TradeCapTenths), P("most", BuildCatalogue.PaceMostPermille), P("missing", BuildCatalogue.TradeMissingPermille), P("skill",
                        BuildCatalogue.SkillPacePermille),
                        P("skill_each", BuildCatalogue.SkillPacePerPoint), P("morale", BuildCatalogue.MoralePacePermille), P("morale_each", BuildCatalogue.MoralePacePerPoint),
                        P("work", BuildCatalogue.WorkCrewShare * 10L)),
                    F(N("offers", BuildCatalogue.OffersAWeek), N("most", BuildCatalogue.CrewsMost), N("skill_low", BuildCatalogue.SkillLeast), N("skill_high", BuildCatalogue.SkillMost),
                        N("strike", BuildCatalogue.StrikeAt), N("leave", BuildCatalogue.LeaveAt)),
                    F(P("least", BuildCatalogue.PaceLeastPermille))),
                L(Ids("office_architect"), StaffNeed.Optional, "reallife" + "_staff_builder_architect",
                    F(N("base", a), P("per", per), N("days", BuildCatalogue.StaffDays), N("firm_a", BuildCatalogue.FirmQuality[0]), N("firm_b", BuildCatalogue.FirmQuality[1]), N("firm_c",
                        BuildCatalogue.FirmQuality[2])),
                    F(N("beats_a", SkillToReach(BuildCatalogue.FirmQuality[0])), N("beats_b", SkillToReach(BuildCatalogue.FirmQuality[1])), N("beats_c", SkillToReach(BuildCatalogue.FirmQuality[2])),
                        P("fee_a", BuildCatalogue.FirmFeePermille[0]), P("fee_c", BuildCatalogue.FirmFeePermille[2]), N("days_a", BuildCatalogue.FirmDays[0]), N("days_c", BuildCatalogue.FirmDays[2])),
                    F(P("fee_a", BuildCatalogue.FirmFeePermille[0]), P("fee_c", BuildCatalogue.FirmFeePermille[2]))),
                L(Ids("office_engineer"), StaffNeed.Required, "reallife" + "_staff_builder_engineer",
                    F(N("storeys", BuildCatalogue.EngineerFromStoreys), N("days", BuildCatalogue.EngineerStaffDays)),
                    F(N("storeys", BuildCatalogue.EngineerFromStoreys)),
                    F(P("fee", BuildCatalogue.EngineerFirmFeePermille), N("days", BuildCatalogue.EngineerFirmDays), N("staff_days", BuildCatalogue.EngineerStaffDays))),
                L(Ids("office_estimator"), StaffNeed.Optional, "reallife" + "_staff_builder_estimator",
                    F(P("none", BuildCatalogue.EstimateSpreadNone), P("firm", BuildCatalogue.EstimateSpreadFirm), P("base", BuildCatalogue.EstimateSpreadBase), P("least",
                        BuildCatalogue.EstimateSpreadLeast),
                        P("per", BuildCatalogue.EstimateSpreadPerSkill)),
                    F(N("beats_firm", SkillToEstimate(PlansRules.Spread(false, 0, true))), P("best", PlansRules.Spread(true, 100, false))),
                    F(P("none", BuildCatalogue.EstimateSpreadNone))),
                L(Ids("site_manager"), StaffNeed.Optional, "reallife" + "_staff_builder_manager",
                    F(P("work", BuildCatalogue.WorkManagerShare * 10L), N("default", BuildCatalogue.ManagerSkillDefault), P("pass", BuildCatalogue.PassManagerPermille), N("skill",
                        BuildCatalogue.PassManagerSkill)),
                    F(N("skill", BuildCatalogue.PassManagerSkill), P("pass", BuildCatalogue.PassManagerPermille)),
                    F(N("default", BuildCatalogue.ManagerSkillDefault)))
            };
            return new StaffPlace("construction_company", lines, new[] { Note("reallife_staff_builder_also", N("staff", BuildCatalogue.StaffMost)) });
        }

        // ---------------------------------------------------------------- the rest

        // The least skill of an architect on the staff whose plans are of a quality (PlansRules draws them at the base plus a share of the skill).
        private static int SkillToReach(int quality)
        {
            for (int skill = 0; skill <= 100; skill++)
            {
                if (BuildCatalogue.Clamp(BuildCatalogue.StaffQualityBase + skill * BuildCatalogue.StaffQualityPermille / 1000, 0, 100) >= quality)
                {
                    return skill;
                }
            }
            return 100;
        }

        // The least skill of an estimator whose estimate is out by less than a spread.
        private static int SkillToEstimate(int spread)
        {
            for (int skill = 0; skill <= 100; skill++)
            {
                if (PlansRules.Spread(true, skill, false) < spread)
                {
                    return skill;
                }
            }
            return 100;
        }

        private static StaffPlace AcademyPlace()
        {
            var lines = new[]
            {
                L(Ids("academy_teacher"), StaffNeed.Required, "reallife" + "_staff_academy_teacher",
                    F(N("per", StudyCatalogue.AcademyPerStaff), N("demand", StudyCatalogue.AcademyDemand), M("fee", StudyCatalogue.AcademyFeeCents), N("extra", 4)),
                    F(N("s1", StudyRules.AcademyStudents(1, 0)), N("s2", StudyRules.AcademyStudents(2, 0)), N("s3", StudyRules.AcademyStudents(3, 0)), N("s5", StudyRules.AcademyStudents(5, 0)),
                        N("demand", StudyCatalogue.AcademyDemand)),
                    F(), new StaffRate(StudyCatalogue.AcademyPerStaff, 1))
            };
            return new StaffPlace("academy", lines, new[] { Note("reallife_staff_academy_also", P("share", StudyCatalogue.AcademyStaffCourseBp / 10)) });
        }

        private static StaffPlace BroadbandPlace()
        {
            var lines = new[]
            {
                L(Ids("field_engineer"), StaffNeed.Required, "reallife" + "_staff_broadband_field", F(N("per", BroadbandCatalogue.FieldConnections)),
                    F(N("per", BroadbandCatalogue.FieldConnections), M("wage", BroadbandCatalogue.CrewWage[BroadbandCatalogue.Field])), F(P("working", EndScaleRules.WorkingLeast), X("times",
                        EndScaleRules.OutageMost)),
                    new StaffRate(BroadbandCatalogue.FieldConnections, 1)),
                L(Ids("call_agent"), StaffNeed.Required, "reallife" + "_staff_broadband_call", F(N("per", BroadbandCatalogue.CallConnections)),
                    F(N("per", BroadbandCatalogue.CallConnections), M("wage", BroadbandCatalogue.CrewWage[BroadbandCatalogue.Calls])), F(P("working", EndScaleRules.WorkingLeast), X("times",
                        EndScaleRules.OutageMost)),
                    new StaffRate(BroadbandCatalogue.CallConnections, 1)),
                L(Ids("network_operator"), StaffNeed.Required, "reallife" + "_staff_broadband_ops", F(N("per", BroadbandCatalogue.OperationsPerDistrict)),
                    F(N("per", BroadbandCatalogue.OperationsPerDistrict), M("wage", BroadbandCatalogue.CrewWage[BroadbandCatalogue.Operations])), F(P("working", EndScaleRules.WorkingLeast),
                        X("times", EndScaleRules.OutageMost)),
                    new StaffRate(1, BroadbandCatalogue.OperationsPerDistrict))
            };
            return new StaffPlace("broadband_company", lines, new[] { Note("reallife_staff_broadband_also", P("pace", BroadbandCatalogue.CrewPace)) });
        }

        private static StaffPlace ExecutivesPlace()
        {
            var lines = new List<StaffLine>(3);
            string[] keys = { "coo", "chro", "cfo" };
            for (int s = 0; s < ExecutiveHiring.Seats.Length; s++)
            {
                int[] roles = ExecutiveHiring.SeatRoles[(int)ExecutiveHiring.Seats[s]];
                var ids = new string[roles.Length];
                for (int i = 0; i < roles.Length; i++)
                {
                    ids[i] = KeyOf(roles[i]);
                }
                lines.Add(L(ids, StaffNeed.Optional, "reallife" + "_staff_exec_" + keys[s], F(Wd("level", LevelWord(ExecutiveHiring.LeastLevel)), P("premium", HierarchyRules.ExecutivePremium),
                    N("days", HierarchyRules.ProposalDays)),
                    F(Wd("level", LevelWord(ExecutiveHiring.LeastLevel))), F()));
            }
            return new StaffPlace("company_executives", lines.ToArray(), new[] { Note("reallife_staff_exec_also") });
        }

        private static StaffPlace ShopPlace(ShopKind kind)
        {
            string id = kind.Type.Substring(ShopsCatalogue.Prefix.Length);
            string donor = kind.Donor.Substring("ba:businesstype_".Length);
            var lines = new[]
            {
                L(Ids("shop_assistant"), StaffNeed.Required, "reallife" + "_staff_shop_assistant", F(Wd("donor", "reallife" + "_staff_donor_" + donor)), null, F())
            };
            return new StaffPlace(id == "icecreamparlour" ? "ice_cream_parlour" : id == "teabar" ? "tea_bar" : id, lines, new[] { Note("reallife_staff_shop_also", Wd("donor",
                "reallife" + "_staff_donor_" + donor)) });
        }
    }
}
