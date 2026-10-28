using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using RealLifeOverhaul;
using RealLifeOverhaul.Careers;
using RealLifeOverhaul.Construction;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Staffing;
using RealLifeOverhaul.Tech;
using TechProduct = RealLifeOverhaul.Tech.ProductKind;
using StaffNote = RealLifeOverhaul.Staffing.StaffNote;
using StaffRole = RealLifeOverhaul.Staffing.StaffRole;
using StaffLine = RealLifeOverhaul.Staffing.StaffLine;
using StaffPlace = RealLifeOverhaul.Staffing.StaffPlace;

// The Staff and roles reference (Scripts/Staffing): every business and role the mod staffs, generated from the code's own tables. The checks hold
// the table to the businesses the mod registers, every role to a way of being had that exists, every figure to the rule the game checks, the
// texts to the code's names in en, pt and zh-cn, and the search to every entry's name.
internal static partial class Program
{
    private static readonly string[] StaffLanguages = { "en", "pt", "zh-cn" };

    private static void StaffReference()
    {
        string dump = Environment.GetEnvironmentVariable("STAFF_DUMP");
        if (!string.IsNullOrEmpty(dump))
        {
            StaffDump(dump);
        }
        StaffTableChecks();
        StaffFigureChecks();
        StaffSourceChecks();
        StaffTextChecks();
        StaffSearchChecks();
        StaffWhatsNew();
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("STAFF_ONLY")))
        {
            Console.WriteLine(_failed == 0 ? "STAFF CHECKS PASSED (" + _run + " so far)" : _failed + " of " + _run + " CHECKS FAILED so far");
            Environment.Exit(_failed == 0 ? 0 : 1);
        }
    }

    private static string StaffText(Dictionary<string, string> texts, string key) => texts.TryGetValue(key, out string v) ? v : key;

    private static StaffStyle StaffStyleFor(Dictionary<string, string> texts, bool markdown)
    {
        Func<string, string> text = key => StaffText(texts, key);
        Func<long, string> money = cents => "$" + (cents / 100.0).ToString("0.##", CultureInfo.InvariantCulture);
        return markdown ? StaffPages.MarkdownStyle(text, money) : StaffPages.GuideStyle(text, money, true);
    }

    private static StaffLine StaffLineOf(string place, string role)
    {
        foreach (StaffPlace p in StaffTable.Places)
        {
            if (p.Id != place)
            {
                continue;
            }
            foreach (StaffLine l in p.Lines)
            {
                if (Array.IndexOf(l.Roles, role) >= 0)
                {
                    return l;
                }
            }
        }
        return null;
    }

    private static bool StaffHasFigure(StaffNote note, string name, long value)
    {
        foreach (StaffFigure f in note.Figures)
        {
            if (f.Name == name && f.Value == value)
            {
                return true;
            }
        }
        return false;
    }

    // ---------------------------------------------------------------- the table: every business, every role, every way to get one

    private static void StaffTableChecks()
    {
        Dictionary<string, string> en = LocaleFile("en");
        var problems = new List<string>();
        StaffIndex.Problems(key => StaffText(en, key), problems);
        Check(problems.Count == 0, "staff table: sound (" + (problems.Count == 0 ? "no problem" : string.Join("; ", problems)) + ")");

        var ids = new HashSet<string>();
        bool unique = true;
        foreach (StaffPlace p in StaffTable.Places)
        {
            unique &= ids.Add(p.Id);
        }
        Check(unique && StaffTable.Places.Length >= 24, "staff table: " + StaffTable.Places.Length + " businesses and workplaces, each once");

        // every business the mod registers has an entry
        var expected = new List<string> { "data_centre", "cloud_services", "hyperscaler", "scale_up", "own_bank", "bank_branch", "car_showroom", "online_shop", "cleaning_service",
            "estate_agency", "hotel", "construction_company", "academy", "broadband_company", "company_executives" };
        string[] firms = { "software_studio", "game_studio", "it_consultancy", "cybersecurity_firm", "ai_lab" };
        expected.AddRange(firms);
        expected.AddRange(new[] { "web_agency", "design_studio", "ice_cream_parlour", "tea_bar", "tobacconist" });
        string lacking = string.Empty;
        foreach (string id in expected)
        {
            lacking += ids.Contains(id) ? string.Empty : id + " ";
        }
        Check(lacking.Length == 0 && ProCatalogue.TechSector.Length == firms.Length && FirmKind.Count == (FirmKind)7 && RealLifeOverhaul.Shops.ShopsCatalogue.Kinds.Length == 3,
            "staff table: an entry for each of the five tech business types, the game's two office businesses, the three new shops, the dealership, the bank branch, the construction company and the academy" + (lacking.Length > 0 ? " (lacking " + lacking + ")" : string.Empty));

        // a business type the mod registers with the game, and so staffs, must come with an entry: the files that name its skills are these
        string root = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        var setters = new List<string>();
        foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            if (!file.Contains(@"\Staffing\") && Regex.IsMatch(File.ReadAllText(file), @"employeePrimarySkills = [^=]"))
            {
                setters.Add(System.IO.Path.GetFileName(file));
            }
        }
        setters.Sort(StringComparer.Ordinal);
        string[] known = { "AcademyType.cs", "BranchType.cs", "ConstructionType.cs", "DealerType.cs", "ShopsGame.cs", "TechTypes.cs" };
        Check(string.Join(",", setters) == string.Join(",", known),
            "staff table: the files that make the mod's business types are the six the table covers (" + string.Join(",", setters) + "); a new business type must come with an entry");

        // every role named by a line exists, has an entry, and every professional has one
        bool professionals = true;
        for (int i = 0; i < TalentCatalogue.RoleCount; i++)
        {
            professionals &= StaffIndex.RoleOf(TalentCatalogue.Role(i).Key) != null;
        }
        Check(professionals && StaffTable.Roles.Length >= TalentCatalogue.RoleCount + 20, "staff table: all " + TalentCatalogue.RoleCount + " professional roles and " + (StaffTable.Roles.Length - TalentCatalogue.RoleCount) + " roles of its own");

        // every role a business names has a way to be had that is real and reachable
        string[] noWay = { };
        var unreachable = new List<string>();
        var used = new HashSet<string>();
        foreach (StaffPlace p in StaffTable.Places)
        {
            foreach (StaffLine l in p.Lines)
            {
                foreach (string r in l.Roles)
                {
                    used.Add(r);
                }
            }
        }
        foreach (string id in used)
        {
            StaffRole role = StaffIndex.RoleOf(id);
            if (role == null || role.Gets.Length == 0)
            {
                unreachable.Add(id + " (no way)");
                continue;
            }
            if (role.Talent >= 0)
            {
                RoleDef def = TalentCatalogue.Role(role.Talent);
                bool market = HiringCatalogue.FamilyShare[(int)def.Family] > 0 && HiringRules.RoleOf(def.Family, 0) >= 0 && HiringCatalogue.AdsMost > 0 && def.LeastLevel <= def.MostLevel;
                bool game = !def.InGame || TalentCatalogue.Professional(def.GameSkill);
                if (!market || !game)
                {
                    unreachable.Add(id + " (market " + market + ", skill " + game + ")");
                }
            }
        }
        Check(unreachable.Count == 0, "staff table: every role a business names can be had, through a market, the game's hiring or by itself (" + (unreachable.Count == 0 ? "all " + used.Count : string.Join(", ", unreachable)) + ")");
        _ = noWay;

        // the courses and certificates a way to train or qualify names are in the game's study table, and the skills a game hire needs are the business type's
        bool coursesReal = true, certsReal = true, crewsPaid = true;
        string courseNames = string.Empty;
        foreach (StaffRole role in StaffTable.Roles)
        {
            foreach (StaffNote n in role.Trains)
            {
                if (n.Key != "reallife_staff_train_course")
                {
                    continue;
                }
                bool found = false;
                foreach (StaffFigure f in n.Figures)
                {
                    if (f.Name == "course")
                    {
                        string word = StaffTable.Words[(int)f.Value];
                        foreach (CourseDef c in StudyCatalogue.All)
                        {
                            found |= word == "reallife_study_course_" + c.Key;
                        }
                    }
                }
                coursesReal &= found;
            }
            foreach (StaffNote n in role.Trains)
            {
                if (n.Key == "reallife_staff_t_cert")
                {
                    bool found = false;
                    foreach (StaffFigure f in n.Figures)
                    {
                        if (f.Name == "cert")
                        {
                            for (int c = 0; c < TalentCatalogue.CertCount; c++)
                            {
                                found |= StaffTable.Words[(int)f.Value] == "reallife_talent_cert_" + TalentCatalogue.Certs[c].Key;
                            }
                        }
                    }
                    certsReal &= found;
                }
            }
            foreach (StaffNote n in role.Gets)
            {
                if (n.Key == "reallife_staff_get_auto")
                {
                    foreach (StaffFigure f in n.Figures)
                    {
                        crewsPaid &= f.Name != "pace" || f.Value > 0;
                        crewsPaid &= f.Name != "wage" || f.Value > 0;
                    }
                }
            }
        }
        _ = courseNames;
        Check(coursesReal && certsReal && crewsPaid, "staff table: every course and certificate named is in the study and talent tables, and the crews that take themselves on have a pace and a wage");

        // the skills the game's hiring is told to look for are among the business type's own
        string dealer = ModSource(@"Economy\Dealer\DealerType.cs"), branch = ModSource(@"Economy\OwnBank\Branch\BranchType.cs"), build = ModSource(@"Construction\ConstructionType.cs");
        string academy = ModSource(@"Careers\AcademyType.cs"), shops = ModSource(@"Shops\ShopsGame.cs");
        Check(dealer.Contains("employeePrimarySkills = new[] { CustomerService, Cleaning }") && branch.Contains("employeePrimarySkills = new[] { CustomerService, Cleaning }")
              && build.Contains("employeePrimarySkills = new[] { Designer, CustomerService }") && academy.Contains("employeePrimarySkills = new[] { TalentCatalogue.HrManager, TalentCatalogue.CustomerService }")
              && shops.Contains("employeePrimarySkills = new[] { \"ba:skill_customerservice\" }"),
            "staff table: the skills the table sends a hire for (customer service and cleaning at the showroom and the branch, designer and customer service at the builders, HR and customer service at the academy, customer service at the new shops) are the business types' own");

        // the other side of the market: the rivals hire the families the table staffs
        int[] shares = StaffIndex.RivalFamilyShares;
        int sum = 0;
        bool every = shares.Length == (int)RealLifeOverhaul.Tech.Family.Count;
        foreach (int s in shares)
        {
            sum += s;
            every &= s > 0;
        }
        Check(every && sum == 1000 && ModSource(@"Tech\Hiring\HiringRules.cs").Contains("Pick(Staffing.StaffIndex.RivalFamilyShares, TechRandom.Mille(f.Seed, n, 22))")
              && shares[(int)RealLifeOverhaul.Tech.Family.Infrastructure] > HiringCatalogue.FamilyShare[(int)RealLifeOverhaul.Tech.Family.Infrastructure] / 4,
            "staff table: the rivals' adverts ask for the families the tech businesses' staffing names (the one table), every family still possible, in thousandths");
    }

    // ---------------------------------------------------------------- the figures against the rules the game checks

    private static void StaffFigureChecks()
    {
        // the data centre's technicians
        StaffRate tech = InfraRules.TechnicianRate;
        bool same = true, shown = true;
        for (int racks = 0; racks <= 700; racks++)
        {
            var d = new DataCentre { Racks = racks - racks / 3, GpuRacks = racks / 3 };
            same &= InfraRules.TechniciansNeeded(d) == tech.Need(racks);
        }
        StaffLine techLine = StaffLineOf("data_centre", KeyOfRole(TalentCatalogue.Technician));
        shown &= techLine != null && techLine.Rate.Per == InfraCatalogue.RacksPerTechnician && StaffHasFigure(techLine.Counts, "need_b", tech.Need(60)) && StaffHasFigure(techLine.Counts, "need_c", tech.Need(200));
        Check(same && shown, "staff figures: the technicians a data centre needs, which the reference states (one a " + InfraCatalogue.RacksPerTechnician + " racks, at least one), are what the code counts, from 0 to 700 racks, AI racks included");

        // the crews at the end game's scale
        bool crews = true;
        for (int racks = 0; racks <= 300; racks += 7)
        {
            var d = new DataCentre { Racks = racks, Sqm = racks * 9 };
            crews &= InfraRules.CrewAtScale(d, InfraCatalogue.Operators) == (racks == 0 ? 0 : StaffLineOf("data_centre", "dc_operator").Rate.Need(racks));
            crews &= InfraRules.CrewAtScale(d, InfraCatalogue.Engineers) == (racks == 0 ? 0 : StaffLineOf("data_centre", "dc_engineer").Rate.Need((long)racks * InfraCatalogue.RackKw));
            crews &= InfraRules.CrewAtScale(d, InfraCatalogue.Security) == (racks == 0 ? 0 : InfraCatalogue.GuardsLeast + d.Sqm / InfraCatalogue.GuardSqm);
        }
        Check(crews, "staff figures: a data centre's operators, engineers and guards at the end game's scale equal the code's, 0 to 300 racks");

        // what a short crew costs
        StaffNote lack = techLine.Without;
        Check(StaffHasFigure(lack, "times", 1000 + InfraCatalogue.OutageShortExtra) && ModSource(@"Tech\Infra\InfraRules.cs").Contains("(long)InfraCatalogue.OutageShortExtra / need")
              && StaffHasFigure(StaffLineOf("data_centre", "dc_operator").Without, "times", EndScaleRules.OutageMost) && EndScaleRules.Outages(0) == EndScaleRules.OutageMost
              && EndScaleRules.Working(0) == EndScaleRules.WorkingLeast,
            "staff figures: with no technician a data centre fails " + (1000 + InfraCatalogue.OutageShortExtra) / 1000 + " times as often, and with none of an end game crew a venture does half its work and fails " + EndScaleRules.OutageMost / 1000 + " times as often: as the code does");

        // the cloud and the hyperscaler
        var centre = new List<DataCentre> { new DataCentre { Racks = 100, Tier = 3, Place = "x", District = "a" } };
        int most = InfraCatalogue.CloudEngineersLeast;
        Check((InfraRules.CloudNeeds(centre, most, long.MaxValue / 4, true) & 4) == 0 && (InfraRules.CloudNeeds(centre, most - 1, long.MaxValue / 4, true) & 4) != 0
              && InfraRules.HasChiefs(true, InfraCatalogue.HyperStaffInfra) && !InfraRules.HasChiefs(true, InfraCatalogue.HyperStaffInfra - 1) && !InfraRules.HasChiefs(false, 99)
              && ModSource(@"Tech\TechDirector.Infra.cs").Contains("InfraRules.HasChiefs(cto, staff)") && ModSource(@"Tech\TechDirector.Infra.cs").Contains("InfraCatalogue.HyperStaffLevel"),
            "staff figures: the cloud opens with " + most + " site reliability or DevOps engineers and not with one fewer; a hyperscaler needs a chief technology officer and " + InfraCatalogue.HyperStaffInfra + " infrastructure people at staff level");
        RoleDef[] atStaff = Array.ConvertAll(StaffLineOf("hyperscaler", KeyOfRole(TalentCatalogue.NetworkEngineer)).Roles, id => TalentCatalogue.Role(StaffIndex.RoleOf(id).Talent));
        bool reach = atStaff.Length > 0;
        foreach (RoleDef r in atStaff)
        {
            reach &= r.MostLevel >= InfraCatalogue.HyperStaffLevel && r.Family == RealLifeOverhaul.Tech.Family.Infrastructure;
        }
        Check(reach && !Array.Exists(StaffLineOf("hyperscaler", KeyOfRole(TalentCatalogue.NetworkEngineer)).Roles, id => id == KeyOfRole(TalentCatalogue.Technician)),
            "staff figures: every role listed for a hyperscaler's staff-level infrastructure people can reach staff level, and the technician, who stops at senior, is left out");

        // seats at a data centre: only the infrastructure family
        bool seats = true;
        StaffLine dcLine = StaffTable.Places[0].Lines[0];
        for (int i = 0; i < TalentCatalogue.RoleCount; i++)
        {
            RoleDef r = TalentCatalogue.Role(i);
            seats &= HiringRules.Seats(false, r) == Array.Exists(dcLine.Roles, id => id == r.Key);
        }
        Check(seats, "staff figures: the roles the reference lists for a data centre are exactly those the game's hiring seats at a business with no desks or shifts");

        // tech firms: the lines come from the same rules the day uses
        bool firmsRoles = true;
        foreach (FirmKind kind in new[] { FirmKind.Software, FirmKind.Games, FirmKind.Consultancy, FirmKind.Security, FirmKind.AiLab, FirmKind.WebAgency, FirmKind.Design })
        {
            string id = kind == FirmKind.Software ? "software_studio" : kind == FirmKind.Games ? "game_studio" : kind == FirmKind.Consultancy ? "it_consultancy"
                : kind == FirmKind.Security ? "cybersecurity_firm" : kind == FirmKind.AiLab ? "ai_lab" : kind == FirmKind.WebAgency ? "web_agency" : "design_studio";
            StaffPlace place = Array.Find(StaffTable.Places, p => p.Id == id);
            StaffLine first = place.Lines[0];
            for (int i = 0; i < TalentCatalogue.RoleCount; i++)
            {
                firmsRoles &= FirmsRules.WorksFor(TalentCatalogue.Role(i), kind) == Array.Exists(first.Roles, rid => rid == TalentCatalogue.Role(i).Key);
            }
            firmsRoles &= first.Need == StaffNeed.Required;
        }
        Check(firmsRoles, "staff figures: for each of the seven tech businesses the roles of the first required line are exactly those FirmsRules says can do its work");

        // the game studio
        int mid = StaffEffects.BuilderPoints(TalentCatalogue.Mid);
        StaffLine game = StaffTable.Places[4].Lines[0];
        bool days = true;
        int[] builders = { 1, 2, 4 };
        string[] names = { "d1", "d2", "d4" };
        for (int b = 0; b < builders.Length; b++)
        {
            long perDay = (long)builders[b] * mid * FirmsCatalogue.FocusSplit[(int)Focus.Balanced][0] / 1000;
            int expect = (int)((FirmsCatalogue.GameScope[0] + perDay - 1) / perDay);
            foreach (StaffFigure f in game.Counts.Figures)
            {
                if (f.Name == names[b])
                {
                    days &= Math.Abs(f.Value - expect) <= 1;
                }
            }
        }
        Check(days, "staff figures: a small game's days with 1, 2 and 4 mid-level programmers are its size over their points a day times the balanced focus's share (" + FirmsCatalogue.GameScope[0] + " over " + mid + " a day each), within a day");
        Check(StaffEffects.GameRun(0, 8, TalentCatalogue.Mid, 0, (int)Focus.Balanced).Days < StaffEffects.GameRun(0, 1, TalentCatalogue.Mid, 0, (int)Focus.Balanced).Days
              && StaffEffects.GameRun(0, 4, TalentCatalogue.Mid, 0, (int)Focus.Balanced).Days > 0 && StaffEffects.BuilderPoints(TalentCatalogue.Principal) > StaffEffects.BuilderPoints(TalentCatalogue.Junior),
            "staff figures: more programmers finish a game sooner, and a senior gives more points a day than a junior");

        // designers lift appeal by their points over the engineers', to the cap, and no further
        var firm = new Firm();
        var prod = new Product { Kind = (int)TechProduct.Game, Features = 0, RivalFeatures = 0 };
        int eng = 100;
        long plain = FirmsRules.Appeal(prod, firm, new Crew { Engineering = eng, Design = 0 });
        long fifth = FirmsRules.Appeal(prod, firm, new Crew { Engineering = eng, Design = eng / StaffEffects.BuildersPerDesigner });
        long twice = FirmsRules.Appeal(prod, firm, new Crew { Engineering = eng, Design = 2 * eng / StaffEffects.BuildersPerDesigner });
        long half = FirmsRules.Appeal(prod, firm, new Crew { Engineering = eng, Design = eng / StaffEffects.BuildersPerDesigner / 2 });
        Check(fifth * 1000 / plain >= 1000 + FirmsCatalogue.DesignAppealMost - 10 && fifth * 1000 / plain <= 1000 + FirmsCatalogue.DesignAppealMost && Math.Abs(twice * 1000 / plain - (1000 + FirmsCatalogue.DesignAppealMost)) <= 1
              && half > plain && half < fifth,
            "staff figures: designers lift appeal one for one to " + FirmsCatalogue.DesignAppealMost / 10 + "% at a designer for every " + StaffEffects.BuildersPerDesigner + " builders, and more of them add nothing");

        // product people
        var boost = new Crew { Engineering = (int)(StaffEffects.BuildersPerProduct * 10 / 1000), Product = 10 };
        Check(FirmsRules.ProductBoost(boost) == FirmsCatalogue.ProductBoostMost && FirmsRules.ProductBoost(new Crew { Engineering = boost.Engineering * 2, Product = 10 }) < FirmsCatalogue.ProductBoostMost
              && FirmsRules.ProductBoost(new Crew { Engineering = boost.Engineering / 2, Product = 10 }) == FirmsCatalogue.ProductBoostMost,
            "staff figures: product people's boost reaches its cap of " + FirmsCatalogue.ProductBoostMost / 10 + "% at " + StaffEffects.BuildersPerProduct / 1000.0 + " builders to each and no further");

        // support
        bool support = true;
        for (int seg = 0; seg < 3; seg++)
        {
            long per = StaffEffects.CustomersPerSupport(seg);
            support &= FirmsRules.SupportNeeded(new Product { Segment = seg, Users = per }) == 1 && FirmsRules.SupportNeeded(new Product { Segment = seg, Users = 2 * per }) == 2
                       && FirmsRules.SupportNeeded(new Product { Segment = seg, Users = 10 * per }) == 10;
        }
        Check(support, "staff figures: one support person answers the customers the reference gives for each kind of buyer, two answer twice as many and ten ten times");

        // licence sellers and the caps of selling
        int sellPoints = (FirmsCatalogue.LicenceSellersMost - FirmsCatalogue.LicenceInbound) * FirmsCatalogue.PointsPerDay / 1000;
        Check(FirmsRules.LicenceSellers(sellPoints) >= FirmsCatalogue.LicenceSellersMost - 150 && FirmsRules.LicenceSellers(sellPoints * 3) == FirmsCatalogue.LicenceSellersMost
              && FirmsRules.LicenceSellers(0) == FirmsCatalogue.LicenceInbound,
            "staff figures: licence sellers are worth " + FirmsCatalogue.LicenceInbound / 10 + "% of one on enquiries alone and top out at " + FirmsCatalogue.LicenceSellersMost / 1000 + " sellers");

        // testers: on the balanced focus builders fix what they make, so no tester is needed; on the features focus one tester keeps up with so many builders
        Check(StaffEffects.BuildersPerTester((int)Focus.Balanced, TalentCatalogue.Mid) == 0 && StaffEffects.BuildersPerTester((int)Focus.Features, TalentCatalogue.Mid) > 1, "staff figures: on the balanced focus mid-level builders need no tester and on the features focus a tester keeps up with several builders");
        int perTester = StaffEffects.BuildersPerTester((int)Focus.Features, TalentCatalogue.Mid);
        Check(BugsAfter(perTester * 2, 0) > BugsAfter(perTester * 2, 2), "staff figures: testers fix bugs: " + perTester * 2 + " builders on the features focus leave more bugs than the same with two testers");

        // reliability and security
        Check(StaffEffects.ReliabilityPeople(StaffEffects.MidSkill(TalentCatalogue.Mid)) * Math.Min(FirmsCatalogue.ReliabilityEach, StaffEffects.MidSkill(TalentCatalogue.Mid) * FirmsCatalogue.ReliabilityPerPoint)
              + FirmsCatalogue.ReliabilityBase >= FirmsCatalogue.ReliabilityMost && StaffEffects.SecurityPeople(StaffEffects.MidSkill(TalentCatalogue.Mid)) > 0
              && StaffEffects.ReliabilityPeople(100) < StaffEffects.ReliabilityPeople(30),
            "staff figures: the engineers and analysts the reference gives reach the caps of reliability and security, fewer when they are better");

        // the builders' architect, estimator, engineer and manager
        bool plans = true;
        for (int tier = 0; tier < BuildCatalogue.FirmCount; tier++)
        {
            foreach (StaffFigure f in StaffLineOf("construction_company", "office_architect").Counts.Figures)
            {
                if (f.Name == (tier == 0 ? "beats_a" : tier == 1 ? "beats_b" : "beats_c"))
                {
                    int at = (int)f.Value;
                    plans &= Math.Min(100, BuildCatalogue.StaffQualityBase + at * BuildCatalogue.StaffQualityPermille / 1000) >= BuildCatalogue.FirmQuality[tier]
                             && (at == 0 || Math.Min(100, BuildCatalogue.StaffQualityBase + (at - 1) * BuildCatalogue.StaffQualityPermille / 1000) < BuildCatalogue.FirmQuality[tier]);
                }
            }
        }
        int beats = 0;
        foreach (StaffFigure f in StaffLineOf("construction_company", "office_estimator").Counts.Figures)
        {
            beats = f.Name == "beats_firm" ? (int)f.Value : beats;
        }
        Check(plans && PlansRules.Spread(true, beats, false) < PlansRules.Spread(false, 0, true) && PlansRules.Spread(true, beats - 1, false) >= PlansRules.Spread(false, 0, true)
              && ModSource(@"Construction\PlansRules.cs").Contains("BuildCatalogue.StaffQualityBase + BuildCatalogue.Clamp(skill, 0, 100) * BuildCatalogue.StaffQualityPermille / 1000"),
            "staff figures: the skill at which a construction company's own architect beats each practice, and its estimator the practice's surveyor, is where the code's plans and estimates cross");
        Check(SiteRules.WorkQuality(100, 0, 0) == BuildCatalogue.WorkCrewShare && SiteRules.WorkQuality(0, 100, 0) == BuildCatalogue.WorkManagerShare && SiteRules.WorkQuality(0, 0, 100) == BuildCatalogue.WorkPlansShare
              && SiteRules.PassOdds(50, false, false, true) - SiteRules.PassOdds(50, false, false, false) == BuildCatalogue.PassManagerPermille
              && SiteRules.Pace(new[] { 3, 3, 0, 0, 0 }, new[] { 1, 1, 0, 0, 0 }, 100, 100, 0, false, 0) >= SiteRules.Pace(new[] { 2, 2, 0, 0, 0 }, new[] { 1, 1, 0, 0, 0 }, 100, 100, 0, false, 0)
              && SiteRules.Pace(new[] { 1, 1, 0, 0, 0 }, new[] { 1, 1, 0, 0, 0 }, 100, 100, 0, false, 0) == SiteRules.Pace(new[] { 1, 1, 0, 0, 0 }, new[] { 1, 1, 0, 0, 0 }, 100, 100, 0, false, 0),
            "staff figures: a crew's, a site manager's and the plans' shares of a site's quality, the manager's lift to an inspection and the pace of more crew are as the reference says");

        // the other crews and counts
        bool counts = true;
        for (int staff = 0; staff <= 8; staff++)
        {
            counts &= StudyRules.AcademyStudents(staff, 0) == Math.Max(0, Math.Min(staff * StudyCatalogue.AcademyPerStaff, StudyCatalogue.AcademyDemand + staff * 4));
        }
        counts &= DealerRulesServed(7, 2) == 4 && DealerRulesServed(3, 5) == 3;
        Check(counts, "staff figures: an academy's students per member of staff and a showroom's visitors looked after are what the code works out");
        bool seatsExec = true;
        foreach (ExecSeat seat in ExecutiveHiring.Seats)
        {
            foreach (int role in ExecutiveHiring.SeatRoles[(int)seat])
            {
                seatsExec &= ExecutiveHiring.Fits(seat, role, ExecutiveHiring.LeastLevel) && !ExecutiveHiring.Fits(seat, role, ExecutiveHiring.LeastLevel - 1);
            }
        }
        StaffLine coo = StaffLineOf("company_executives", KeyOfRole(TalentCatalogue.ChiefExecutive));
        Check(seatsExec && coo != null && Array.IndexOf(coo.Roles, KeyOfRole(TalentCatalogue.ChiefExecutive)) >= 0,
            "staff figures: the roles listed for each executive seat are the roles that may hold it, from senior level up");
    }

    private static int DealerRulesServed(int visitors, int staff)
    {
        return RealLifeOverhaul.Economy.Dealership.DealerRules.Served(visitors, staff);
    }

    // What's new tells of the reference, as a feature (no fix mark), in all eleven languages, in some row of the version's (any reallife_changes_ row).
    private static void StaffWhatsNew()
    {
        var starts = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "en", "Staff and roles: a new section of the Guide" }, { "pt", "Equipe e cargos: uma nova seção do Guia" }, { "zh-cn", "员工与岗位：指南新增一节" },
            { "de", "Personal und Rollen: Ein neuer Bereich im Leitfaden" }, { "fr", "Personnel et rôles : une nouvelle section du Guide" }, { "es", "Personal y roles: una nueva sección de la Guía" },
            { "ru", "Персонал и роли: новый раздел" },
            { "nl", "Personeel en rollen: een nieuw onderdeel van de Gids" }, { "tr", "Personel ve roller: Kılavuz'da yazarken" }, { "pl", "Personel i role: nowa sekcja Przewodnika" },
            { "ko", "직원과 역할: 입력하는 동안" }
        };
        bool every = true, clean = true;
        string lacking = string.Empty;
        foreach (KeyValuePair<string, string> pair in starts)
        {
            Dictionary<string, string> texts = LocaleFile(pair.Key);
            bool has = false;
            foreach (KeyValuePair<string, string> row in texts)
            {
                if (row.Key.StartsWith("reallife_changes_", StringComparison.Ordinal) && !row.Key.EndsWith("_title", StringComparison.Ordinal) && row.Value.Contains(pair.Value))
                {
                    has = true;
                    clean &= !row.Value.Contains("  ") && !row.Value.Contains("!") && !row.Value.Contains("—") && !row.Value.Contains("–") && Occurs(row.Value, pair.Value) == 1;
                }
            }
            every &= has;
            lacking += has ? string.Empty : pair.Key + " ";
        }
        Check(starts.Count == 11 && every, "staff texts: What's new tells of Staff and roles, as a feature, in all eleven languages, in some row (" + (lacking.Length > 0 ? "lacking " + lacking : "none lacking") + ")");
        Check(clean, "staff texts: the What's new line is said once in its row, with no double spaces, exclamation marks or dashes");
    }

    private static string KeyOfRole(int talent) => TalentCatalogue.Role(talent).Key;

    // Bugs left after a hundred days of builders and testers on the features focus.
    private static int BugsAfter(int builders, int testers)
    {
        var firm = new Firm { Focus = (int)Focus.Features };
        var product = new Product { Kind = (int)TechProduct.Saas, Features = 30 };
        int pts = StaffEffects.BuilderPoints(TalentCatalogue.Mid);
        var crew = new Crew { Engineering = builders * pts, Testing = testers * StaffEffects.Points(TalentCatalogue.QaTester, TalentCatalogue.Mid, StaffEffects.MidSkill(TalentCatalogue.Mid)),
            Faults = TalentRules.Faults(StaffEffects.Person(TalentCatalogue.SoftwareEngineer, TalentCatalogue.Mid, StaffEffects.MidSkill(TalentCatalogue.Mid)), StaffEffects.MidSkill(TalentCatalogue.Mid)) };
        for (int day = 0; day < 100; day++)
        {
            FirmsRules.Build(firm, product, crew, crew.Engineering, crew.Testing);
            product.Features = Math.Min(product.Features, 30);   // held, so the allowance does not move
        }
        return product.Bugs;
    }

    // ---------------------------------------------------------------- the code reads the same constants

    private static void StaffSourceChecks()
    {
        string firms = ModSource(@"Tech\Firms\FirmsRules.cs"), director = ModSource(@"Tech\TechDirector.Firms.cs"), contracts = ModSource(@"Tech\Firms\FirmsRules.Contracts.cs");
        Check(firms.Contains("FirmsCatalogue.ProductBoostMost") && firms.Contains("FirmsCatalogue.DesignAppealMost") && firms.Contains("FirmsCatalogue.SellingBoostMost") && firms.Contains("FirmsCatalogue.SupportChurnMost")
              && contracts.Contains("FirmsCatalogue.OfferSellingMost") && director.Contains("FirmsCatalogue.ReliabilityEach") && director.Contains("FirmsCatalogue.SecurityEach")
              && director.Contains("FirmsCatalogue.SecurityMost") && director.Contains("FirmsCatalogue.ReliabilityMost"),
            "staff sources: the firms' rules and the day's crew count read the constants the reference states, not figures of their own");
        string site = ModSource(@"Construction\SiteRules.cs"), sites = ModSource(@"Construction\ConstructionDirector.Sites.cs");
        Check(site.Contains("BuildCatalogue.TradeCapTenths") && site.Contains("BuildCatalogue.WorkCrewShare") && sites.Contains("BuildCatalogue.PassManagerSkill") && sites.Contains("BuildCatalogue.ManagerSkillDefault"),
            "staff sources: a construction site's pace, quality and inspection read the constants the reference states");
        string banking = ModSource(@"Economy\OwnBank\OwnBankRules.Day.cs"), infra = ModSource(@"Tech\Infra\InfraRules.cs"), broadband = ModSource(@"Utilities\BroadbandRules.cs");
        Check(banking.Contains("OwnBankCatalogue.TellerCents") && banking.Contains("OwnBankCatalogue.ManagerCents") && banking.Contains("OwnBankCatalogue.OfficerCents") && banking.Contains("OwnBankCatalogue.ComplianceCents")
              && infra.Contains("TechnicianRate.Need(") && infra.Contains("InfraCatalogue.OperatorRacks") && infra.Contains("InfraCatalogue.EngineerKw") && broadband.Contains("BroadbandCatalogue.FieldConnections")
              && broadband.Contains("BroadbandCatalogue.CallConnections") && broadband.Contains("BroadbandCatalogue.OperationsPerDistrict"),
            "staff sources: the bank's, the data centre's and the broadband company's crews are counted by the constants the reference states");
        string guide = ModSource(@"Meta\Guide.cs"), view = ModSource(@"Meta\GuideView.cs"), city = ModSource(@"Meta\MetaCity.cs"), mod = ModSource(@"Core\RealLifeOverhaulMod.cs");
        Check(guide.Contains("new GuideTopic(\"staff\", \"reallife_staff_guide_title\", StaffGuide.Page)") && guide.Contains("StaffGuide.Points(feature.Id)") && view.Contains("StaffApp.Open(null)")
              && city.Contains("StaffApp.Begin(_generation)") && city.Contains("StaffApp.End()") && city.Contains("StaffHelpGame.Begin()") && mod.Contains("Meta.StaffApp.WantPictures()"),
            "staff sources: the Guide has the Staff and roles page with its button, the app and the game's help are begun and ended with the city, and the pictures are asked for");
        string help = ModSource(@"Meta\StaffHelpGame.cs"), contract = ModSource(@"Core\GameContract.cs");
        Check(help.Contains("GameContract.StaffHelp.Off") && help.Contains("GameContract.HelpCategories.Field") && help.Contains("GameContract.HelpLoad.Method") && help.Contains("Guard.Listener<string>(OnPage)")
              && help.Contains("Guard.Wrap(OnLanguage)") && contract.Contains("\"_categories\"") && contract.Contains("\"LoadCategories\""),
            "staff sources: the game's help takes the entries through the contract, behind the guard, after each load of its list");
    }

    // ---------------------------------------------------------------- the texts

    private static void StaffTextChecks()
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        StaffIndex.Keys(keys);
        var notes = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        void Collect(StaffNote n)
        {
            if (n == null)
            {
                return;
            }
            if (!notes.TryGetValue(n.Key, out HashSet<string> set))
            {
                set = new HashSet<string>(StringComparer.Ordinal);
                notes[n.Key] = set;
            }
            foreach (StaffFigure f in n.Figures)
            {
                set.Add(f.Name);
            }
            if (n.Links.Length > 0)
            {
                set.Add("links");
            }
        }
        foreach (StaffPlace p in StaffTable.Places)
        {
            foreach (StaffLine l in p.Lines)
            {
                Collect(l.Does);
                Collect(l.Counts);
                Collect(l.Without);
            }
            foreach (StaffNote a in p.Also)
            {
                Collect(a);
            }
        }
        foreach (StaffRole r in StaffTable.Roles)
        {
            foreach (StaffNote n in r.About)
            {
                Collect(n);
            }
            foreach (StaffNote n in r.Gets)
            {
                Collect(n);
            }
            foreach (StaffNote n in r.Trains)
            {
                Collect(n);
            }
        }
        string absent = string.Empty;
        string digitKeys = string.Empty, pageKeys = string.Empty;
        bool unfilled = true, noDigits = true, plain = true, samePlaces = true, pages = true;
        string firstUnfilled = string.Empty;
        Dictionary<string, string> en = LocaleFile("en");
        foreach (string code in StaffLanguages)
        {
            Dictionary<string, string> texts = LocaleFile(code);
            foreach (string key in keys)
            {
                if (!texts.TryGetValue(key, out string value) || value.Length == 0)
                {
                    absent += code + ":" + key + " ";
                    continue;
                }
                plain &= !value.Contains("  ") && !value.Contains("!") && !value.Contains("—") && !value.Contains("–") && !value.Contains(" - ");
                if (notes.TryGetValue(key, out HashSet<string> names))
                {
                    var found = new HashSet<string>(StringComparer.Ordinal);
                    foreach (Match m in Regex.Matches(value, @"\{([a-z0-9_]+)\}"))
                    {
                        found.Add(m.Groups[1].Value);
                    }
                    bool ok = names.IsSupersetOf(found);
                    if (!ok && firstUnfilled.Length == 0)
                    {
                        firstUnfilled = code + ":" + key;
                    }
                    unfilled &= ok;
                    // the same names in every language: what the code fills, the translation keeps
                    var english = new HashSet<string>(StringComparer.Ordinal);
                    foreach (Match m in Regex.Matches(en.TryGetValue(key, out string e) ? e : string.Empty, @"\{([a-z0-9_]+)\}"))
                    {
                        english.Add(m.Groups[1].Value);
                    }
                    samePlaces &= english.SetEquals(found);
                    if (code == "en")
                    {
                        bool digit = Regex.IsMatch(Regex.Replace(value, @"\{[a-z0-9_]+\}", string.Empty), @"[0-9]");
                        noDigits &= !digit;
                        digitKeys += digit ? key + " " : string.Empty;
                    }
                }
            }
            // every page reads in the language, with nothing left unfilled and no raw key
            StaffStyle style = StaffStyleFor(texts, false);
            foreach (StaffEntry entry in StaffIndex.Entries)
            {
                string page = StaffPages.AsGuideText(StaffPages.Page(entry, style));
                bool good = page.Length > 80 && !page.Contains("{") && !page.Contains("reallife_") && page.Contains("# ");
                pages &= good;
                pageKeys += good ? string.Empty : code + ":" + entry.Key + " ";
            }
        }
        Check(absent.Length == 0, "staff texts: every key the reference reads is in en, pt and zh-cn" + (absent.Length > 0 ? " (missing " + absent.Substring(0, Math.Min(absent.Length, 300)) + ")" : string.Empty));
        Check(unfilled && samePlaces, "staff texts: every {name} in a sentence is a figure or the roles the code fills, and each language keeps the same names as English" + (firstUnfilled.Length > 0 ? " (" + firstUnfilled + ")" : string.Empty));
        Check(noDigits, "staff texts: no sentence of the table has a number written in it (every number is a figure the code fills)" + (digitKeys.Length > 0 ? " (" + digitKeys + ")" : string.Empty));
        Check(plain, "staff texts: no double spaces, exclamation marks or dashes");
        Check(pages, "staff texts: every entry's page reads in en, pt and zh-cn with a heading, no name left unfilled and no raw key" + (pageKeys.Length > 0 ? " (" + pageKeys.Substring(0, Math.Min(pageKeys.Length, 200)) + ")" : string.Empty));
    }

    // ---------------------------------------------------------------- the search

    private static void StaffSearchChecks()
    {
        bool everyName = true, ordered = true, partial = true, none = true, links = true, accents = true, once = true;
        string first = string.Empty;
        var compare = CultureInfo.InvariantCulture.CompareInfo;
        foreach (string code in StaffLanguages)
        {
            Dictionary<string, string> texts = LocaleFile(code);
            Func<string, string> text = key => StaffText(texts, key);
            foreach (bool places in new[] { true, false })
            {
                List<StaffEntry> list = StaffIndex.Sorted(places, text);
                for (int i = 1; i < list.Count; i++)
                {
                    ordered &= compare.Compare(text(list[i - 1].NameKey), text(list[i].NameKey), CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) <= 0;
                }
                int kind = 0;
                foreach (StaffEntry e in StaffIndex.Entries)
                {
                    kind += e.IsPlace == places ? 1 : 0;
                }
                once &= list.Count == kind && new HashSet<StaffEntry>(list).Count == list.Count;
            }
            foreach (StaffEntry entry in StaffIndex.Entries)
            {
                string name = text(entry.NameKey);
                // what the app's lists do on each keystroke: the entry stays in while its own name, or any part of it, is typed, in any case
                bool found = name.Length > 0 && name != entry.NameKey && StaffIndex.Match(name, name) >= 0 && StaffIndex.Match(name, name.ToUpperInvariant()) >= 0 && StaffIndex.Match(name, name.ToLowerInvariant()) >= 0;
                if (!found && first.Length == 0)
                {
                    first = code + ":" + entry.Key;
                }
                everyName &= found;
                if (name.Length >= 3)
                {
                    partial &= StaffIndex.Match(name, name.Substring(1, name.Length - 2)) == 1 && StaffIndex.Match(name, name.Substring(0, 2)) == 0;
                }
                none &= StaffIndex.Match(name, "qzxjv") < 0;
                links &= StaffIndex.FindSlug(entry.NameKey) == entry && StaffIndex.Find(entry.Key) == entry;
                string plainName = RemoveAccents(name);
                accents &= StaffIndex.Match(name, plainName) >= 0 && StaffIndex.Match(plainName, name) >= 0;
            }
        }
        Check(everyName && once, "staff search: every business and role is found by its own name, in capitals or small letters, in en, pt and zh-cn" + (first.Length > 0 ? " (not " + first + ")" : string.Empty));
        Check(partial && none && StaffIndex.Match("Data centre", string.Empty) == 0 && StaffIndex.Match("Data centre", "  ") == 0, "staff search: any part of a name finds it, a nonsense word finds nothing and an empty box shows everything");
        Check(accents, "staff search: accents are ignored both ways, as the game's help ignores them");
        Check(ordered, "staff search: businesses and roles are listed from A to Z in each language");
        Check(links, "staff search: every entry is found again by its key and by its slug, which the pages link by");
        Check(StaffIndex.Match("Engenheiro de dados", "ENGENHEIRO") == 0 && StaffIndex.Match("Conexão", "conexao") == 0, "staff search: capitals and accents do not matter (Conexão is found by conexao)");
    }

    private static string RemoveAccents(string text)
    {
        string d = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(d.Length);
        foreach (char c in d)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    // For writing the texts: every key the table reads, with the names the code fills in its sentences.
    private static void StaffDump(string path)
    {
        var names = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        var keys = new HashSet<string>(StringComparer.Ordinal);
        StaffIndex.Keys(keys);
        foreach (string k in keys)
        {
            names[k] = new SortedSet<string>(StringComparer.Ordinal);
        }
        void Add(StaffNote n)
        {
            if (n == null)
            {
                return;
            }
            foreach (StaffFigure f in n.Figures)
            {
                names[n.Key].Add(f.Name + "=" + StaffPages.Show(f, StaffPages.GuideStyle(k => k, c => "$" + (c / 100.0).ToString("0.##", CultureInfo.InvariantCulture), false)));
            }
            if (n.Links.Length > 0)
            {
                names[n.Key].Add("links");
            }
        }
        foreach (StaffPlace p in StaffTable.Places)
        {
            foreach (StaffLine l in p.Lines)
            {
                Add(l.Does);
                Add(l.Counts);
                Add(l.Without);
            }
            foreach (StaffNote a in p.Also)
            {
                Add(a);
            }
        }
        foreach (StaffRole r in StaffTable.Roles)
        {
            foreach (StaffNote n in r.About)
            {
                Add(n);
            }
            foreach (StaffNote n in r.Gets)
            {
                Add(n);
            }
            foreach (StaffNote n in r.Trains)
            {
                Add(n);
            }
        }
        Dictionary<string, string> en = LocaleFile("en");
        var text = new StringBuilder();
        var sorted = new List<string>(keys);
        sorted.Sort(StringComparer.Ordinal);
        foreach (string k in sorted)
        {
            text.Append(en.ContainsKey(k) ? "have " : "MISSING ").Append(k).Append(" {").Append(string.Join(",", names[k])).Append("}\n");
        }
        File.WriteAllText(path, text.ToString());
        // every page in English, for reading
        var pages = new StringBuilder();
        foreach (StaffEntry entry in StaffIndex.Entries)
        {
            pages.Append("==== ").Append(entry.Key).Append('\n').Append(StaffPages.AsGuideText(StaffPages.Page(entry, StaffStyleFor(en, false)))).Append("\n\n");
        }
        File.WriteAllText(path + ".pages.txt", pages.ToString());
    }
}
