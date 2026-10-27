using System;
using System.Collections.Generic;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul.Staffing
{
    public static partial class StaffTable
    {
        private static void TechPlaces(List<StaffPlace> into)
        {
            into.Add(DataCentre());
            into.Add(CloudServices());
            into.Add(Hyperscaler());
            into.Add(Firm(FirmKind.Software, "software_studio"));
            into.Add(Firm(FirmKind.Games, "game_studio"));
            into.Add(Firm(FirmKind.Consultancy, "it_consultancy"));
            into.Add(Firm(FirmKind.Security, "cybersecurity_firm"));
            into.Add(Firm(FirmKind.AiLab, "ai_lab"));
            into.Add(Firm(FirmKind.WebAgency, "web_agency"));
            into.Add(Firm(FirmKind.Design, "design_studio"));
            into.Add(ScaleUp());
        }

        // ---------------------------------------------------------------- data centres, the cloud, the hyperscaler

        private static StaffPlace DataCentre()
        {
            StaffRate tech = Tech.InfraRules.TechnicianRate;
            var lines = new List<StaffLine>(8)
            {
                Line(RolesWhere(r => HiringRules.Seats(false, r)), StaffNeed.Required,
                    Note("reallife_staff_dc_tech_does", N("per", InfraCatalogue.RacksPerTechnician)),
                    Note("reallife_staff_dc_tech_count", N("per", InfraCatalogue.RacksPerTechnician), N("racks_a", 20), N("need_a", tech.Need(20)), N("racks_b", 60),
                        N("need_b", tech.Need(60)), N("racks_c", 200), N("need_c", tech.Need(200))),
                    Note("reallife_staff_dc_tech_lack", X("times", 1000 + InfraCatalogue.OutageShortExtra), P("t1", InfraCatalogue.OutageChance[0]), P("t2", InfraCatalogue.OutageChance[1]),
                        P("t3", InfraCatalogue.OutageChance[2]), P("t4", InfraCatalogue.OutageChance[3])), tech),
                Line(Ids("dc_operator"), StaffNeed.Required,
                    Note("reallife_staff_dc_operator_does", N("racks", InfraCatalogue.OperatorRacks), N("gpu", InfraCatalogue.OperatorGpu)),
                    Note("reallife_staff_dc_operator_count", N("least", InfraCatalogue.OperatorsLeast), N("racks", InfraCatalogue.OperatorRacks), N("gpu", InfraCatalogue.OperatorGpu),
                        M("wage", InfraCatalogue.CrewWage[InfraCatalogue.Operators])), Note("reallife_staff_scale_lack", P("working", EndScaleRules.WorkingLeast), X("times",
                            EndScaleRules.OutageMost)),
                    new StaffRate(InfraCatalogue.OperatorRacks, InfraCatalogue.OperatorsLeast)),
                Line(Ids("dc_engineer"), StaffNeed.Required,
                    Note("reallife_staff_dc_engineer_does", N("kw", InfraCatalogue.EngineerKw), N("rack_kw", InfraCatalogue.RackKw), N("gpu_kw", InfraCatalogue.GpuRackKw)),
                    Note("reallife_staff_dc_engineer_count", N("least", InfraCatalogue.EngineersLeast), N("kw", InfraCatalogue.EngineerKw), M("wage",
                        InfraCatalogue.CrewWage[InfraCatalogue.Engineers])),
                    Note("reallife_staff_scale_lack", P("working", EndScaleRules.WorkingLeast), X("times", EndScaleRules.OutageMost)),
                    new StaffRate(InfraCatalogue.EngineerKw, InfraCatalogue.EngineersLeast)),
                Line(Ids("dc_guard"), StaffNeed.Required,
                    Note("reallife_staff_dc_guard_does"),
                    Note("reallife_staff_dc_guard_count", N("least", InfraCatalogue.GuardsLeast), N("sqm", InfraCatalogue.GuardSqm), M("wage", InfraCatalogue.CrewWage[InfraCatalogue.Security])),
                    Note("reallife_staff_scale_lack", P("working", EndScaleRules.WorkingLeast), X("times", EndScaleRules.OutageMost)))
            };
            var also = new[]
            {
                Note("reallife_staff_dc_also_seats"),
                Links("reallife_staff_dc_also_hire", Ids(KeyOf(TalentCatalogue.Technician), KeyOf(TalentCatalogue.NetworkEngineer)),
                    N("most", HiringCatalogue.CrewMost), N("open", HiringCatalogue.CrewSearchesMost), N("days_a", HiringCatalogue.CrewDaysLeast), N("days_b", HiringCatalogue.CrewDaysMost)),
                Note("reallife_staff_dc_also_scale", P("pace", InfraCatalogue.CrewPace)),
                Note("reallife_staff_dc_also_data"),
                Note("reallife_staff_dc_also_site", N("most", InfraCatalogue.CentresMost), N("mbps", InfraCatalogue.NetNeeded))
            };
            return new StaffPlace("data_centre", lines.ToArray(), also);
        }

        private static StaffPlace CloudServices()
        {
            var lines = new[]
            {
                Line(Ids(KeyOf(TalentCatalogue.Sre), KeyOf(TalentCatalogue.DevOps)), StaffNeed.Required,
                    Note("reallife_staff_cloud_eng_does", N("least", InfraCatalogue.CloudEngineersLeast)),
                    Note("reallife_staff_cloud_eng_count", N("least", InfraCatalogue.CloudEngineersLeast)),
                    Note("reallife_staff_cloud_eng_lack", N("least", InfraCatalogue.CloudEngineersLeast))),
                Line(Ids("cloud_ops"), StaffNeed.Required,
                    Note("reallife_staff_cloud_ops_does", N("units", InfraCatalogue.OpsUnits)),
                    Note("reallife_staff_cloud_ops_count", N("units", InfraCatalogue.OpsUnits), M("wage", InfraCatalogue.OpsWage)),
                    Note("reallife_staff_scale_lack", P("working", EndScaleRules.WorkingLeast), X("times", EndScaleRules.OutageMost)),
                    new StaffRate(InfraCatalogue.OpsUnits, 1))
            };
            var also = new[]
            {
                Note("reallife_staff_cloud_also", N("racks", InfraCatalogue.CloudRacksLeast), N("tier", InfraCatalogue.CloudTierLeast + 1), M("cash", InfraCatalogue.CloudCashLeast)),
                Note("reallife_staff_cloud_also_count")
            };
            return new StaffPlace("cloud_services", lines, also);
        }

        private static StaffPlace Hyperscaler()
        {
            var lines = new[]
            {
                Line(Ids(KeyOf(TalentCatalogue.Cto)), StaffNeed.Required,
                    Note("reallife_staff_hyper_cto_does"), Note("reallife_staff_hyper_cto_count"), Note("reallife_staff_hyper_lack")),
                Line(RolesWhere(r => r.Family == Tech.Family.Infrastructure && r.MostLevel >= InfraCatalogue.HyperStaffLevel), StaffNeed.Required,
                    Note("reallife_staff_hyper_staff_does", N("count", InfraCatalogue.HyperStaffInfra), Wd("level", LevelWord(InfraCatalogue.HyperStaffLevel))),
                    Note("reallife_staff_hyper_staff_count", N("count", InfraCatalogue.HyperStaffInfra), Wd("level", LevelWord(InfraCatalogue.HyperStaffLevel))),
                    Note("reallife_staff_hyper_lack"))
            };
            var also = new[]
            {
                Note("reallife_staff_hyper_also", N("regions", InfraCatalogue.HyperRegions), N("racks", InfraCatalogue.HyperRacks), M("cash", InfraCatalogue.HyperCash))
            };
            return new StaffPlace("hyperscaler", lines, also);
        }

        // ---------------------------------------------------------------- tech firms

        private static bool Makes(FirmKind kind, ProductKind product) => FirmsRules.Makes(kind, product);

        private static StaffPlace Firm(FirmKind kind, string id)
        {
            bool game = Makes(kind, ProductKind.Game);
            bool services = Makes(kind, ProductKind.Saas) || Makes(kind, ProductKind.App) || Makes(kind, ProductKind.Licence) || Makes(kind, ProductKind.Model);
            bool makes = FirmsCatalogue.MakesProducts[(int)kind] != 0;
            bool takes = FirmsCatalogue.TakesWork[(int)kind] != 0;
            string[] builders = RolesWhere(r => FirmsRules.WorksFor(r, kind));
            int[] pts = { StaffEffects.BuilderPoints(TalentCatalogue.Junior), StaffEffects.BuilderPoints(TalentCatalogue.Mid), StaffEffects.BuilderPoints(TalentCatalogue.Senior),
                          StaffEffects.BuilderPoints(TalentCatalogue.Principal) };
            int midPts = pts[TalentCatalogue.Mid];
            var lines = new List<StaffLine>(12);
            StaffFigure[] rate =
            {
                N("junior", pts[0]), N("mid", pts[1]), N("senior", pts[2]), N("principal", pts[3]), N("hours", TalentCatalogue.DayHours), N("f0", StaffEffects.FaultsPer100(0)), N("f100",
                    StaffEffects.FaultsPer100(100))
            };
            if (game)
            {
                lines.Add(Line(builders, StaffNeed.Required, Note("reallife_staff_firm_game_builders_does", Concat(rate, N("small", FirmsCatalogue.GameScope[0]), N("mid_size",
                    FirmsCatalogue.GameScope[1]),
                        N("big", FirmsCatalogue.GameScope[2]), P("share", FirmsCatalogue.FocusSplit[(int)Focus.Balanced][0]), P("share_features", FirmsCatalogue.FocusSplit[(int)Focus.Features][0]),
                        P("share_quality", FirmsCatalogue.FocusSplit[(int)Focus.Quality][0]))),
                    Note("reallife_staff_firm_game_builders_count", N("d1", StaffEffects.GameRun(0, 1, TalentCatalogue.Mid, 0, (int)Focus.Balanced).Days),
                        N("d2", StaffEffects.GameRun(0, 2, TalentCatalogue.Mid, 0, (int)Focus.Balanced).Days), N("d4", StaffEffects.GameRun(0, 4, TalentCatalogue.Mid, 0, (int)Focus.Balanced).Days),
                        N("big4", StaffEffects.GameRun(2, 4, TalentCatalogue.Mid, 0, (int)Focus.Balanced).Days), N("big8", StaffEffects.GameRun(2, 8, TalentCatalogue.Mid, 0,
                            (int)Focus.Balanced).Days)),
                    Note("reallife_staff_firm_lack_idle")));
            }
            else if (makes)
            {
                lines.Add(Line(builders, StaffNeed.Required, Note("reallife_staff_firm_builders_does", Concat(rate, N("base", FirmsCatalogue.FeatureBase), N("step", FirmsCatalogue.FeatureStep),
                        P("share", FirmsCatalogue.FocusSplit[(int)Focus.Balanced][0]), N("most", FirmsCatalogue.FeaturesMost), N("launch", FirmsCatalogue.LaunchFeatures))),
                    Note("reallife_staff_firm_builders_count", N("d1", StaffEffects.FeatureDays(0, 1, TalentCatalogue.Mid)), N("d3", StaffEffects.FeatureDays(0, 3, TalentCatalogue.Mid)),
                        N("d6", StaffEffects.FeatureDays(0, 6, TalentCatalogue.Mid)), N("late", FirmsRules.FeatureCost(11)), N("late_days", StaffEffects.FeatureDays(11, 3, TalentCatalogue.Mid))),
                    Note("reallife_staff_firm_lack_idle")));
            }
            if (takes)
            {
                int low = int.MaxValue, high = 0;
                for (int k = 0; k < (int)WorkKind.Count; k++)
                {
                    if (!FirmsRules.Takes(kind, (WorkKind)k) || FirmsCatalogue.WorkDays[k] <= 0)
                    {
                        continue;
                    }
                    int least = FirmsCatalogue.WorkLeast[k] * 12 / 10 / FirmsCatalogue.WorkDays[k],
                        most = (FirmsCatalogue.WorkMost[k] * 12 + 9 * FirmsCatalogue.WorkDays[k]) / 10 / FirmsCatalogue.WorkDays[k];
                    low = Math.Min(low, least);
                    high = Math.Max(high, most);
                }
                if (low == int.MaxValue)
                {
                    low = high = FirmsCatalogue.ManagedPoints;
                }
                lines.Add(Line(builders, makes ? StaffNeed.Optional : StaffNeed.Required,
                    Note("reallife_staff_firm_contract_does", rate),
                    Note("reallife_staff_firm_contract_count", N("low", low), N("high", high), N("n_low", Math.Max(1, StaffEffects.PeopleFor(low, midPts))), N("n_high",
                        StaffEffects.PeopleFor(high, midPts)),
                        N("managed", FirmsCatalogue.ManagedPoints)),
                    Note("reallife_staff_firm_lack_idle")));
            }
            if (makes)
            {
                lines.Add(Line(InFamily(Tech.Family.Design), StaffNeed.Optional,
                    Note(game ? "reallife_staff_firm_game_design_does" : "reallife_staff_firm_design_does", P("most", FirmsCatalogue.DesignAppealMost), N("per", StaffEffects.BuildersPerDesigner)),
                    Note(game ? "reallife_staff_firm_game_design_count" : "reallife_staff_firm_design_count", P("most", FirmsCatalogue.DesignAppealMost), N("per", StaffEffects.BuildersPerDesigner),
                        N("designer", StaffEffects.Points(TalentCatalogue.GraphicDesignerRole, TalentCatalogue.Mid, StaffEffects.MidSkill(TalentCatalogue.Mid))), N("builder", midPts)),
                    Note("reallife_staff_firm_design_lack")));
                lines.Add(Line(Ids(KeyOf(TalentCatalogue.QaTester)), StaffNeed.Optional,
                    Note("reallife_staff_firm_test_does", N("fix", FirmsCatalogue.PointsPerFix), P("lost", 700), N("halve_game", StaffEffects.BugsToHalve(0)), N("halve_service",
                        StaffEffects.BugsToHalve(5))),
                    Note("reallife_staff_firm_test_count", N("per_features", StaffEffects.BuildersPerTester((int)Focus.Features, TalentCatalogue.Mid)),
                        N("tester", StaffEffects.Points(TalentCatalogue.QaTester, TalentCatalogue.Mid, StaffEffects.MidSkill(TalentCatalogue.Mid)))),
                    Note("reallife_staff_firm_test_lack")));
                lines.Add(Line(InFamily(Tech.Family.Product), StaffNeed.Optional,
                    Note("reallife_staff_firm_product_does", P("most", FirmsCatalogue.ProductBoostMost), N("gain", FirmsCatalogue.ProductBoostGain / 1000)),
                    Note("reallife_staff_firm_product_count", P("most", FirmsCatalogue.ProductBoostMost), X("per", StaffEffects.BuildersPerProduct)),
                    Note("reallife_staff_firm_product_lack")));
            }
            if (services)
            {
                lines.Add(Line(InFamily(Tech.Family.Support), StaffNeed.Optional,
                    Note("reallife_staff_firm_support_does", N("tickets", FirmsCatalogue.TicketsPerPerson), P("churn", FirmsCatalogue.SupportChurnMost)),
                    Note("reallife_staff_firm_support_count", N("consumer", StaffEffects.CustomersPerSupport((int)Segment.Consumer)), N("business",
                        StaffEffects.CustomersPerSupport((int)Segment.Business)),
                        N("enterprise", StaffEffects.CustomersPerSupport((int)Segment.Enterprise)), N("tickets", FirmsCatalogue.TicketsPerPerson)),
                    Note("reallife_staff_firm_support_lack", P("churn", FirmsCatalogue.SupportChurnMost))));
            }
            if (Makes(kind, ProductKind.Licence) || Makes(kind, ProductKind.Saas) || Makes(kind, ProductKind.Model) || takes)
            {
                string[] sellers = InFamily(Tech.Family.Commercial);
                if (Makes(kind, ProductKind.Licence))
                {
                    lines.Add(Line(sellers, StaffNeed.Optional,
                        Note("reallife_staff_firm_sell_licence_does", P("inbound", FirmsCatalogue.LicenceInbound), N("most", FirmsCatalogue.LicenceSellersMost / 1000), N("points",
                            FirmsCatalogue.PointsPerDay)),
                        Note("reallife_staff_firm_sell_licence_count", N("most", FirmsCatalogue.LicenceSellersMost / 1000),
                            N("people", StaffEffects.PeopleFor((FirmsCatalogue.LicenceSellersMost - FirmsCatalogue.LicenceInbound) * FirmsCatalogue.PointsPerDay / 1000, midPts))),
                        Note("reallife_staff_firm_sell_licence_lack", P("inbound", FirmsCatalogue.LicenceInbound))));
                }
                if (Makes(kind, ProductKind.Saas) || Makes(kind, ProductKind.Model))
                {
                    int cap = StaffEffects.SellingToCap(FirmsCatalogue.SellingBoostMost, FirmsCatalogue.SellingBoostPerPoint);
                    lines.Add(Line(sellers, StaffNeed.Optional,
                        Note("reallife_staff_firm_sell_subs_does", P("most", FirmsCatalogue.SellingBoostMost), P("each", FirmsCatalogue.SellingBoostPerPoint)),
                        Note("reallife_staff_firm_sell_subs_count", P("most", FirmsCatalogue.SellingBoostMost), N("points", cap), N("people", StaffEffects.PeopleFor(cap, midPts))),
                        Note("reallife_staff_firm_sell_subs_lack")));
                }
                if (takes)
                {
                    int cap = StaffEffects.SellingToCap(FirmsCatalogue.OfferSellingMost, FirmsCatalogue.OfferSellingPerPoint);
                    lines.Add(Line(sellers, StaffNeed.Optional,
                        Note("reallife_staff_firm_sell_offers_does", P("most", FirmsCatalogue.OfferSellingMost), P("per", FirmsCatalogue.OfferSellingPerPoint), P("base", FirmsCatalogue.OfferChance)),
                        Note("reallife_staff_firm_sell_offers_count", P("most", FirmsCatalogue.OfferSellingMost), N("points", cap), N("people", StaffEffects.PeopleFor(cap, midPts))),
                        Note("reallife_staff_firm_sell_offers_lack", P("base", FirmsCatalogue.OfferChance))));
                }
            }
            if (services)
            {
                int midSkill = StaffEffects.MidSkill(TalentCatalogue.Mid);
                lines.Add(Line(Ids(KeyOf(TalentCatalogue.Sre), KeyOf(TalentCatalogue.DevOps)), StaffNeed.Optional,
                    Note("reallife_staff_firm_reliability_does", P("base", FirmsCatalogue.ReliabilityBase), P("most", FirmsCatalogue.ReliabilityMost), P("outage", FirmsCatalogue.OutageChance),
                        P("each", FirmsCatalogue.ReliabilityEach)),
                    Note("reallife_staff_firm_reliability_count", P("most", FirmsCatalogue.ReliabilityMost), N("mid", StaffEffects.ReliabilityPeople(midSkill)),
                        N("top", StaffEffects.ReliabilityPeople(StaffEffects.MidSkill(TalentCatalogue.Principal)))),
                    Note("reallife_staff_firm_reliability_lack", P("outage", FirmsCatalogue.OutageChance))));
                lines.Add(Line(RolesWhere(r => r.Family == Tech.Family.Security), StaffNeed.Optional,
                    Note("reallife_staff_firm_security_does", P("base", FirmsCatalogue.SecurityBase), P("most", FirmsCatalogue.SecurityMost), P("officer", FirmsCatalogue.SecurityOfficer),
                        P("cert", FirmsCatalogue.SecurityCert), P("breach", FirmsCatalogue.BreachChance), M("fine", FirmsCatalogue.FinePerCustomer)),
                    Note("reallife_staff_firm_security_count", P("most", FirmsCatalogue.SecurityMost), N("mid", StaffEffects.SecurityPeople(midSkill)),
                        N("top", StaffEffects.SecurityPeople(StaffEffects.MidSkill(TalentCatalogue.Principal))), P("officer", FirmsCatalogue.SecurityOfficer)),
                    Note("reallife_staff_firm_security_lack", P("breach", FirmsCatalogue.BreachChance))));
            }
            if (FirmsRules.Takes(kind, WorkKind.Website))
            {
                lines.Add(Line(Ids(KeyOf(TalentCatalogue.WebDesigner)), StaffNeed.Optional,
                    Note("reallife_staff_firm_web_does"), Note("reallife_staff_firm_web_count"), Note("reallife_staff_firm_web_lack")));
            }
            var also = new List<StaffNote>(4);
            int[] wanted = new int[8];
            int wantedCount = FirmsRules.Wanted((int)kind, wanted);
            if (wantedCount > 0)
            {
                var names = new string[wantedCount];
                for (int i = 0; i < wantedCount; i++)
                {
                    names[i] = KeyOf(wanted[i]);
                }
                also.Add(Links("reallife_staff_firm_also_wanted", names));
            }
            if (kind == FirmKind.WebAgency || kind == FirmKind.Design)
            {
                also.Add(Note("reallife_staff_firm_also_spare"));
            }
            else
            {
                also.Add(Note("reallife_staff_firm_also_sector"));
            }
            if (kind == FirmKind.AiLab)
            {
                also.Add(Note("reallife_staff_firm_also_compute", P("rent", InfraCatalogue.RentedGpuShare)));
            }
            also.Add(Note("reallife_staff_firm_also_open"));
            return new StaffPlace(id, lines.ToArray(), also.ToArray());
        }

        private static StaffPlace ScaleUp()
        {
            var lines = new[]
            {
                Line(RolesWhere(r => r.Tech), StaffNeed.Required,
                    Note("reallife_staff_scaleup_does", N("people", TechPath.ScaleUpPeople), M("monthly", TechPath.ScaleUpMonthly)),
                    Note("reallife_staff_scaleup_count", N("people", TechPath.ScaleUpPeople)),
                    Note("reallife_staff_scaleup_lack", N("people", TechPath.ScaleUpPeople)), new StaffRate(1, TechPath.ScaleUpPeople))
            };
            return new StaffPlace("scale_up", lines, new[] { Note("reallife_staff_scaleup_also") });
        }

        private static StaffFigure[] Concat(StaffFigure[] first, params StaffFigure[] more)
        {
            var all = new StaffFigure[first.Length + more.Length];
            Array.Copy(first, all, first.Length);
            Array.Copy(more, 0, all, first.Length, more.Length);
            return all;
        }
    }
}
