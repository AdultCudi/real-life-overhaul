using System;
using System.Collections.Generic;
using RealLifeOverhaul.Tech;

// A warehouse taken as a data centre (Tech/Infra: the question when a warehouse is rented or bought, the one fit-out both ways
// reach) and a data centre's crew found by a search firm briefed from headquarters (Tech/Hiring: the crew search, its fee by
// level, its candidates, the hires at that centre, the save's line).
internal static partial class Program
{
    private static void CentreOffer()
    {
        var known = new HashSet<string>(StringComparer.Ordinal);
        var taken = new List<string>();
        Check(InfraRules.Taken(known, new[] { "1 Dock St", "9 Pier Rd" }, true, taken) == 0 && known.Count == 2,
            "data centre question: the first look of a session learns the warehouses the save holds and asks about none");
        Check(InfraRules.Taken(known, new[] { "1 Dock St", "9 Pier Rd" }, false, taken) == 0, "data centre question: nothing new, nothing asked");
        Check(InfraRules.Taken(known, new[] { "1 Dock St", "4 Quay Ln", "9 Pier Rd" }, false, taken) == 1 && taken[0] == "4 Quay Ln",
            "data centre question: a warehouse rented or bought since is asked about");
        Check(InfraRules.Taken(known, new[] { "1 Dock St", "4 Quay Ln", "9 Pier Rd" }, false, taken) == 0, "data centre question: each warehouse is asked about once");
        Check(InfraRules.Taken(known, new[] { "4 Quay Ln" }, false, taken) == 0 && InfraRules.Taken(known, new[] { "4 Quay Ln", "1 Dock St" }, false, taken) == 0,
            "data centre question: one given up and taken back in the same session is not asked about again");
        var a = new HashSet<string>(StringComparer.Ordinal);
        var b = new HashSet<string>(StringComparer.Ordinal);
        var ta = new List<string>();
        var tb = new List<string>();
        InfraRules.Taken(a, new string[0], true, ta);
        InfraRules.Taken(b, new string[0], true, tb);
        InfraRules.Taken(a, new[] { "7 Mill St", "2 Yard Rd" }, false, ta);
        InfraRules.Taken(b, new[] { "7 Mill St", "2 Yard Rd" }, false, tb);
        Check(ta.Count == 2 && ta[0] == tb[0] && ta[1] == tb[1] && ta[0] == "7 Mill St", "data centre question: in the game's order, the same on every machine");
        Check(InfraRules.Taken(new HashSet<string>(), null, false, taken) == 0, "data centre question: no list, nothing asked");

        Check(InfraRules.FitBlock(0, false) == InfraRules.FitFree && InfraRules.FitBlock(InfraCatalogue.CentresMost - 1, false) == InfraRules.FitFree,
            "data centre fit-out: free below the most there may be");
        Check(InfraRules.FitBlock(InfraCatalogue.CentresMost, false) == InfraRules.FitMost && InfraRules.FitBlock(1, true) == InfraRules.FitAlready,
            "data centre fit-out: refused at the most, and for one that is already a data centre");
        Check(InfraRules.SupplyMissing(true, true, true) == 0 && InfraRules.SupplyMissing(false, true, true) == 1 && InfraRules.SupplyMissing(true, false, true) == 2
              && InfraRules.SupplyMissing(true, true, false) == 4 && InfraRules.SupplyMissing(false, false, false) == 7,
            "data centre fit-out: power, water and the connection named a bit each");

        string infra = ModSource(@"Tech\TechDirector.Infra.cs");
        string page = ModSource(@"Tech\Infra\TechApp.Infra.cs");
        string director = ModSource(@"Tech\TechDirector.cs");
        Check(infra.Contains("() => OpenTech(TabInfra, TechApp.FitRow + place)") && page.Contains("_director.FitOut(place, tier)") && Occurs(infra, "internal bool FitOut(") == 1,
            "data centre question sources: yes opens the Infra tab's own fit-out of that warehouse, and one FitOut does the work");
        Check(infra.Contains("InfraRules.FitBlock(_infra.Centres.Count, _infra.CentreAt(place) != null)") && page.Contains("InfraRules.FitBlock(f.Centres.Count, f.CentreAt(place) != null)"),
            "data centre question sources: the page, the question and the fit-out refuse by the same rule");
        Check(infra.Contains("!Settings.TechInfraOffer") && infra.Contains("GameConfirm.TryShow(") && !infra.Contains("HudConfirm.Show(") && infra.Contains("!GameConfirm.IsOpen"),
            "data centre question sources: its option, the game's question box through GameConfirm, never over another question");
        Check(infra.Contains("EventHold.Keep(ref GlobalEvents.onBuildingRegistrationChange, _onRegChange, ref _regChangeSeen)")
              && director.Contains("_onRegChange = Guard.Wrap<Address>(OnRegistrationChange)") && director.Contains("GlobalEvents.onBuildingRegistrationChange -= _onRegChange"),
            "data centre question sources: the game's own event for a building's record, wrapped once by the guard and let go");
        Check(infra.Contains("_warehousesFirst = true;   // what the save already holds is learnt, not asked about"),
            "data centre question sources: loading a save asks about nothing it already holds");
        Check(page.Contains("\"reallife_infra_fit_supply\"") && infra.Contains("TechBridges.SuppliedAt(reg, 1)"), "data centre question sources: power, water and the connection are shown before the money goes");

        string[] keys =
        {
            "reallife_infra_opt_offer", "reallife_opt_tip_tech_infra_offer", "reallife_infra_offer_title", "reallife_infra_offer_body", "reallife_infra_offer_yes",
            "reallife_infra_offer_no", "reallife_infra_supply_power", "reallife_infra_supply_water", "reallife_infra_supply_net", "reallife_infra_supply_missing",
            "reallife_infra_fit_supply", "reallife_infra_fit_already", "reallife_infra_fit_most", "reallife_infra_test_offer", "reallife_infra_test_offer_pass",
            "reallife_infra_test_offer_fail", "reallife_infra_test_offer_off", "reallife_infra_test_offer_option", "reallife_infra_test_offer_none"
        };
        Check(AllIn(keys, out string missing), "data centre question texts: every key in en, pt and zh-cn (" + missing + ")");
        Dictionary<string, string> en = LocaleFile("en");
        Check(en.TryGetValue("reallife_guide_tech_sector", out string guide) && guide.Contains("When you rent or buy a warehouse, a question asks whether to set it up as a data centre"),
            "data centre question texts: the Guide's page on the tech sector tells of the question");
    }

    private static void CrewSearch()
    {
        CrewSearchRules();
        CrewSearchDelivery();
        CrewSearchSave();
        CrewSearchSources();
    }

    private static void CrewSearchRules()
    {
        Check(HiringRules.CrewRole(TalentCatalogue.Technician) && HiringRules.CrewRole(TalentCatalogue.NetworkEngineer) && !HiringRules.CrewRole(TalentCatalogue.SoftwareEngineer),
            "crew search: technicians and network engineers look after racks; a software engineer is no crew");
        Check(HiringRules.CrewLevel(TalentCatalogue.Technician, TalentCatalogue.Staff) == TalentCatalogue.Role(TalentCatalogue.Technician).MostLevel
              && HiringRules.CrewLevel(TalentCatalogue.NetworkEngineer, TalentCatalogue.Principal) == TalentCatalogue.Staff
              && HiringRules.CrewLevel(TalentCatalogue.Technician, -3) == TalentCatalogue.Junior,
            "crew search: junior to staff level, inside each role's own range");
        int going = 3000;
        int j = HiringRules.CrewFeeEach(going, TalentCatalogue.Junior), m = HiringRules.CrewFeeEach(going, TalentCatalogue.Mid), s = HiringRules.CrewFeeEach(going, TalentCatalogue.Senior),
            st = HiringRules.CrewFeeEach(going, TalentCatalogue.Staff);
        Check(j < m && m < s && s < st && j == (int)((long)going * TalentCatalogue.YearHours * HiringCatalogue.CrewFeeBp[0] / 10000),
            "crew search: the fee a hire is a share of a year's pay at the going rate, larger the more senior");
        Check(HiringRules.CrewFeeEach(going, TalentCatalogue.Principal) == st && HiringRules.CrewFeeEach(-5, 0) == 0, "crew search: the fee stops at staff level and is never below nothing");
        Check(st < HiringRules.SearchFee(going), "crew search: a crew hire costs less than the senior search's fee at the same pay");
        Check(HiringRules.CrewCandidates(1) == 2 && HiringRules.CrewCandidates(2) == 3 && HiringRules.CrewCandidates(4) == 6 && HiringRules.CrewCandidates(HiringCatalogue.CrewMost) == HiringCatalogue.CrewCandidatesMost
              && HiringRules.CrewCandidates(0) == 2 && HiringRules.CrewCandidates(99) == HiringCatalogue.CrewCandidatesMost,
            "crew search: half as many candidates again as asked for, one more at the least, within the cap");
        var ad = new JobAd { Search = true, Crew = 3, Fee = 3000, FeePaid = HiringRules.CrewRetainer(3000) };
        Check(HiringRules.CrewRetainer(3000) == 1000 && HiringRules.CrewRetainer(3001) == 1001 && HiringRules.CrewRetainer(-4) == 0, "crew search: a third of the fee now, rounded up");
        Check(HiringRules.CrewDue(ad) == 0, "crew search: nothing more is owed before anybody starts");
        ad.Hired = 1;
        Check(HiringRules.CrewDue(ad) == 0, "crew search: the retainer covers the first hire's share");
        ad.Hired = 2;
        int due = HiringRules.CrewDue(ad);
        ad.FeePaid += due;
        ad.Hired = 3;
        int last = HiringRules.CrewDue(ad);
        ad.FeePaid += last;
        Check(due == 1000 && last == 1000 && ad.FeePaid == ad.Fee, "crew search: each hire pays its share, the whole fee once the brief is filled");
        ad.Hired = 4;
        Check(HiringRules.CrewDue(ad) == 1000, "crew search: a hire past the brief pays a share too");
        Check(HiringRules.CrewDue(new JobAd { Search = true, Fee = 9000, Hired = 1 }) == 0 && HiringRules.CrewDue(null) == 0, "crew search: a senior search owes nothing by the crew's rule");
        var d = new DataCentre { Racks = 45, Technicians = 1 };
        Check(InfraRules.CrewLacking(d) == 2 && InfraRules.CrewLacking(new DataCentre()) == 1 && InfraRules.CrewLacking(new DataCentre { Racks = 20, Technicians = 3 }) == 1,
            "crew search: the brief starts at what the racks lack, one at the least");
    }

    private static void CrewSearchDelivery()
    {
        HiringFile f = NewLabour(21, 5), g = NewLabour(21, 5);
        JobAd ad = CrewAd(f, TalentCatalogue.Technician, TalentCatalogue.Junior, 4), same = CrewAd(g, TalentCatalogue.Technician, TalentCatalogue.Junior, 4);
        int before = f.Applicants.Count;
        int made = HiringRules.DeliverCrew(f, ad, 7, FlatRate, 1000, new[] { "d1", "d2" });
        HiringRules.DeliverCrew(g, same, 7, FlatRate, 1000, new[] { "d1", "d2" });
        int fit = 0;
        for (int i = before; i < f.Applicants.Count; i++)
        {
            Applicant a = f.Applicants[i];
            Seeker s = f.SeekerOf(a.Seeker);
            fit += a.Ad == ad.Id && a.Stage == (int)ApplyStage.Shortlisted && a.Refs && s != null && s.P.Role == TalentCatalogue.Technician && s.P.Level == TalentCatalogue.Junior
                   && s.Passive && s.Claim == 0 && s.Until == 7 + HiringCatalogue.ApplicantDays ? 1 : 0;
        }
        Check(made == 6 && fit == 6 && ad.Came == 6, "crew search: four technicians asked for bring six vetted candidates, shortlisted for the brief");
        Seeker fa = f.Seekers[f.Seekers.Count - 1], ga = g.Seekers[g.Seekers.Count - 1];
        Check(fa.Id == ga.Id && fa.Ask == ga.Ask && fa.P.Main == ga.P.Main && fa.District == ga.District, "crew search: the same save gives the same candidates on every machine");
        HiringFile h = NewLabour(8, 5);
        JobAd staff = CrewAd(h, TalentCatalogue.NetworkEngineer, TalentCatalogue.Staff, 1);
        HiringRules.DeliverCrew(h, staff, 7, FlatRate, 1000, null);
        Seeker last = h.Seekers[h.Seekers.Count - 1];
        Check(last.P.Role == TalentCatalogue.NetworkEngineer && last.P.Level == TalentCatalogue.Staff && staff.Came == 2, "crew search: a staff-level network engineer can be found");
        HiringFile full = NewLabour(9, 5);
        while (full.Seekers.Count < HiringCatalogue.SeekersMost + HiringCatalogue.SearchSeekersMost)
        {
            full.Seekers.Add(new Seeker { Id = full.Next() });
        }
        Check(HiringRules.DeliverCrew(full, CrewAd(full, TalentCatalogue.Technician, 0, 2), 7, FlatRate, 1000, null) == 0, "crew search: a full market takes no more candidates");
    }

    private static JobAd CrewAd(HiringFile f, int role, int level, int crew)
    {
        var ad = new JobAd { Id = f.Next(), Place = "Dock%20St~4", Role = role, Level = level, Search = true, Crew = crew, From = 5, To = 5 + HiringCatalogue.ApplicantDays, Due = 7 };
        f.Ads.Add(ad);
        return ad;
    }

    private static void CrewSearchSave()
    {
        HiringFile f = NewLabour(31, 2);
        JobAd crew = CrewAd(f, TalentCatalogue.Technician, TalentCatalogue.Mid, 5);
        crew.Fee = 50000;
        crew.FeePaid = 16650;
        crew.Hired = 2;
        JobAd senior = new JobAd { Id = f.Next(), Place = "Main%20St~1", Role = TalentCatalogue.Cto, Level = TalentCatalogue.Staff, Search = true, Fee = 90000, FeePaid = 30000, Due = 6 };
        f.Ads.Add(senior);
        HiringFile back = HiringStore.Read(HiringStore.Write(f), out int version);
        JobAd c2 = back.AdOf(crew.Id), s2 = back.AdOf(senior.Id);
        Check(version == HiringStore.Version && HiringStore.Version == 2, "crew search save: the line is version 2");
        Check(c2 != null && c2.CrewSearch && c2.Crew == 5 && c2.Hired == 2 && c2.Fee == 50000 && c2.FeePaid == 16650 && c2.Place == "Dock%20St~4",
            "crew search save: a crew search comes back with its brief, its hires and its fee");
        Check(s2 != null && !s2.CrewSearch && s2.Crew == 0 && s2.Search, "crew search save: a senior search stays a senior search");
        // A line from before the crew search: its ads end at the day a search delivers.
        string v1 = HiringStore.Write(f).Replace("v=2", "v=1");
        v1 = v1.Replace("|5|2;", ";");   // the crew search is the first ad, the senior one after it
        HiringFile old = HiringStore.Read(v1, out int oldVersion);
        JobAd o2 = old.AdOf(crew.Id);
        Check(oldVersion == 1 && o2 != null && o2.Crew == 0 && o2.Hired == 0 && o2.Fee == 50000, "crew search save: a version 1 line loads, its searches senior ones");
        Check(HiringCatalogue.SearchSeekersMost == HiringCatalogue.SearchCandidates * HiringCatalogue.SearchesMost + HiringCatalogue.CrewCandidatesMost * HiringCatalogue.CrewSearchesMost,
            "crew search save: the caps hold every open search's candidates");
    }

    private static void CrewSearchSources()
    {
        string hiring = ModSource(@"Tech\TechDirector.Hiring.cs");
        string page = ModSource(@"Tech\People\RolesApp.Crew.cs");
        string infra = ModSource(@"Tech\Infra\TechApp.Infra.cs");
        string guide = ModSource(@"Tech\TechGuide.cs");
        Check(hiring.Contains("HiringGame.Headquarters()") && hiring.Contains("HiringGame.Pay(retainer, HiringGame.SearchKey, hq, TalentGame.NameOf(hq))"),
            "crew search sources: headquarters briefs the firm and pays the retainer");
        Check(hiring.Contains("|| ad.CrewSearch;") && hiring.Contains("ad.Hired++;") && hiring.Contains("PayCrewFee(ad, at);"),
            "crew search sources: a crew hire starts on the payroll at the centre and pays its share of the fee");
        Check(hiring.Contains("open += ad.Search && !ad.CrewSearch && (ad.Came == 0 || AnyLive(ad.Id))"), "crew search sources: the senior searches keep their own room");
        Check(hiring.IndexOf("HiringRules.DeliverCrew(", StringComparison.Ordinal) > 0 && hiring.IndexOf("ad.CrewSearch && ad.Came == 0 && day >= ad.Due", StringComparison.Ordinal)
              < hiring.IndexOf("else if (ad.Search && ad.Came == 0 && day >= ad.Due)", StringComparison.Ordinal),
            "crew search sources: a crew search delivers by its own rule, never as a senior search");
        Check(infra.Contains("_director.BriefCrew(place)") && page.Contains("InfraRules.CrewLacking(d)") && page.Contains("_director.StartCrewSearch(place, role, level, asked)"),
            "crew search sources: briefed from a data centre's page or the Search tab, asking for what it lacks, by one call");
        Check(guide.Contains("\"crew_fee_least\"") && guide.Contains("\"crew_days_most\"") && guide.Contains("\"crew_most\""), "crew search sources: the Guide's figures come from the catalogue");
        string[] keys =
        {
            "reallife_crew_off", "reallife_crew_no_centre", "reallife_crew_no_hq", "reallife_crew_most", "reallife_crew_note_found", "reallife_crew_row_new", "reallife_crew_row",
            "reallife_crew_new_title", "reallife_crew_no_centres", "reallife_crew_count", "reallife_crew_centre", "reallife_crew_cost", "reallife_crew_from_hq", "reallife_crew_about",
            "reallife_crew_search_title", "reallife_crew_terms", "reallife_crew_button_brief", "reallife_crew_test", "reallife_crew_test_pass", "reallife_crew_test_fail", "reallife_crew_test_none"
        };
        Check(AllIn(keys, out string missing), "crew search texts: every key in en, pt and zh-cn (" + missing + ")");
        Dictionary<string, string> en = LocaleFile("en");
        Check(Fits(en, "reallife_crew_note_found") && Fits(en, "reallife_crew_no_hq") && Fits(en, "reallife_crew_most"), "crew search texts: the notices are 240 characters or fewer");
        Check(en.TryGetValue("reallife_guide_tech_jobs", out string page2) && page2.Contains("A data centre's crew has a search of its own, briefed from your headquarters")
              && page2.Contains("{crew_fee_least} to {crew_fee_most}"), "crew search texts: the Guide's page on hiring tells of the crew search");
        Check(WhatsNewHas(en, "Give several employees a raise at once") && WhatsNewHas(en, "set it up as a data centre")
              && WhatsNewHas(en, "brief a search firm for a data centre's crew"), "the three asks: What's new tells of each (any of its rows)");
    }
}
