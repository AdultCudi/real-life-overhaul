using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using RealLifeOverhaul.Economy;

// Checks for the Managers tab's directory and the bugs found behind it (Scripts/Economy/Company/ManagerDirectoryRules,
// ManagerRules.Decider): how a post stands today agrees with who really decides, a regional manager off sick reviews
// nothing, a decision is counted to whoever made it, the groups, what needs the player, a budget nearly used, a month's
// cost, the counts of a whole company, the order a group is listed in (by district, then name, the same on every machine,
// and in time), the director and the tab going through those rules, the new test button, every new text in every
// language, the Guide page and What's new.
internal static partial class Program
{
    private static void ManagerDirectory()
    {
        ManagerPostStates();
        ManagerCredit();
        ManagerGroupsAndNeeds();
        ManagerDirectoryCounts();
        ManagerDirectoryOrder();
        ManagerDirectoryWiring();
        ManagerDirectoryWords();
    }

    // How a post stands against ManagerRules.Decider, which the decisions themselves ask, over every case.
    private static void ManagerPostStates()
    {
        bool agree = true;
        for (int bits = 0; bits < 16; bits++)
        {
            bool has = (bits & 1) != 0, sick = (bits & 2) != 0, region = (bits & 4) != 0, regionSick = (bits & 8) != 0;
            int by = ManagerRules.Decider(has, sick, 60, region, regionSick, 90, out _, out _);
            PostState state = ManagerDirectoryRules.State(has, sick, region, regionSick);
            agree &= !has ? state == PostState.Empty && by == 0
                : by == 1 ? state == PostState.InPost
                : by == 2 ? state == PostState.Covered
                : state == PostState.Uncovered;
        }
        Check(agree, "managers directory: how a post stands today agrees with who decides there in all 16 cases (in post, standing in, nobody, no manager)");
        // The bug: the panel said the regional manager stands in while they were off sick too; nobody decides then.
        Check(ManagerDirectoryRules.State(true, true, true, true) == PostState.Uncovered && ManagerDirectoryRules.State(true, true, true, false) == PostState.Covered
              && ManagerDirectoryRules.State(true, true, false, false) == PostState.Uncovered,
            "managers directory: a manager off sick is covered only by a regional manager at work; with the regional manager off sick too, nobody stands in");
        // The bug: a regional manager off sick still cut the district's mistakes.
        int by1 = ManagerRules.Decider(true, false, 70, true, true, 80, out int s1, out int r1);
        int by2 = ManagerRules.Decider(true, false, 70, true, false, 80, out int s2, out int r2);
        Check(by1 == 1 && s1 == 70 && r1 == 0 && by2 == 1 && r2 == 80,
            "managers: a regional manager off sick looks over nothing that day (review 0, was " + r1 + "); at work their review stands (" + r2 + ")");
        // The panel's chance of a mistake is the one the day's decisions are made with: standing in, it is the regional manager's from afar.
        ManagerRules.Decider(true, true, 70, true, false, 80, out int skill2, out int review2);
        int today = ManagerRules.MistakeChance(skill2, review2), shownBefore = ManagerRules.MistakeChance(70, 80);
        Check(today == ManagerRules.MistakeChance(ManagerRules.FromAfar(80), 0) && today != shownBefore,
            "managers directory: a manager off sick, the regional manager standing in: today's chance of a mistake is " + today
            + " thousandths, not the " + shownBefore + " the panel showed from the manager's own skill");
    }

    // Who a decision is counted to: the regional manager standing in by their name, else the place's manager.
    private static void ManagerCredit()
    {
        Check(ManagerDirectoryRules.ByRegion("Rae", "Omar", "Rae") && !ManagerDirectoryRules.ByRegion("Omar", "Omar", "Rae")
              && !ManagerDirectoryRules.ByRegion("Sam", "Sam", "Sam") && !ManagerDirectoryRules.ByRegion("Rae", "Omar", null)
              && !ManagerDirectoryRules.ByRegion(null, "Omar", "Rae") && !ManagerDirectoryRules.ByRegion("Rae", null, null)
              && ManagerDirectoryRules.ByRegion("Rae", null, "Rae"),
            "managers: a decision made under the regional manager's name is theirs, under the place manager's (or two of one name) the place manager's");
        // A month of decisions at a place whose manager was off sick for some of them: each is counted to who made it.
        var own = new ManagerFile { Name = "Omar" };
        var region = new ManagerFile { Name = "Rae", Role = ManagerCatalogue.RoleRegion };
        string[] who = { "Omar", "Rae", "Rae", "Omar", "Rae" };
        foreach (string w in who)
        {
            ManagerFile by = ManagerDirectoryRules.ByRegion(w, own.Name, region.Name) ? region : own;
            by.Decided++;
        }
        Check(own.Decided == 2 && region.Decided == 3 && own.Decided + region.Decided == who.Length,
            "managers: five decisions, three standing in: the place manager's month counts 2, the regional manager's 3, the total still 5 (before, 5 and 0)");
    }

    private static void ManagerGroupsAndNeeds()
    {
        Check(ManagerDirectoryRules.GroupOf(ManagedKind.Shop, true) == ManagerGroup.Shop && ManagerDirectoryRules.GroupOf(ManagedKind.Works, true) == ManagerGroup.Works
              && ManagerDirectoryRules.GroupOf(ManagedKind.Warehouse, true) == ManagerGroup.Warehouse
              && ManagerDirectoryRules.GroupOf(ManagedKind.Shop, false) == ManagerGroup.Vacant && ManagerDirectoryRules.GroupOf(ManagedKind.Warehouse, false) == ManagerGroup.Vacant,
            "managers directory: a place with a manager is listed under its kind, one without under the places with no manager");
        bool kinds = true;
        foreach (ManagedKind kind in new[] { ManagedKind.Shop, ManagedKind.Works, ManagedKind.Warehouse })
        {
            kinds &= ManagerDirectoryRules.KindOf(ManagerDirectoryRules.KindGroup(kind), out ManagedKind back) && back == kind;
        }
        kinds &= !ManagerDirectoryRules.KindOf(ManagerGroup.Regional, out _) && !ManagerDirectoryRules.KindOf(ManagerGroup.Vacant, out _) && !ManagerDirectoryRules.KindOf(ManagerGroup.Needs, out _);
        Check(kinds, "managers directory: each kind of place has a group of its own and comes back from it; the other groups are no kind's");

        Check(!ManagerDirectoryRules.BudgetNear(799, 1000) && ManagerDirectoryRules.BudgetNear(800, 1000) && ManagerDirectoryRules.BudgetNear(1500, 1000)
              && !ManagerDirectoryRules.BudgetNear(0, 0) && !ManagerDirectoryRules.BudgetNear(10, 0) && !ManagerDirectoryRules.BudgetNear(-5, 1000)
              && ManagerDirectoryRules.BudgetNear(int.MaxValue, int.MaxValue),
            "managers directory: a budget is nearly used from " + ManagerDirectoryRules.BudgetNearShare + " thousandths of it; a budget of nothing never is, and large ones do not overflow");
        var line = new PostLine { Kind = ManagedKind.Shop, Has = true, Budget = 1000 };
        bool needs = ManagerDirectoryRules.NeedsOf(line) == 0;
        line.OpenMistakes = 2;
        needs &= ManagerDirectoryRules.NeedsOf(line) == ManagerDirectoryRules.NeedMistake;
        line.Sick = true;
        needs &= ManagerDirectoryRules.NeedsOf(line) == (ManagerDirectoryRules.NeedMistake | ManagerDirectoryRules.NeedUncovered);
        line.RegionThere = true;
        needs &= ManagerDirectoryRules.NeedsOf(line) == ManagerDirectoryRules.NeedMistake;
        line.Spent = 900;
        needs &= ManagerDirectoryRules.NeedsOf(line) == (ManagerDirectoryRules.NeedMistake | ManagerDirectoryRules.NeedBudget)
                 && ManagerDirectoryRules.Reasons(ManagerDirectoryRules.NeedsOf(line)) == 2;
        var gone = new PostLine { Kind = ManagedKind.Works, Has = false, OpenMistakes = 1, Sick = true, Spent = 900, Budget = 1000 };
        needs &= ManagerDirectoryRules.NeedsOf(gone) == ManagerDirectoryRules.NeedMistake;
        Check(needs, "managers directory: what needs the player: a mistake still open (also one a manager stood down since made), off sick with nobody standing in, a budget nearly used");
        Check(ManagerDirectoryRules.FilterOf(PostState.InPost, 0) == ManagerDirectoryRules.FilterFine && ManagerDirectoryRules.FilterOf(PostState.Covered, 0) == ManagerDirectoryRules.FilterSick
              && ManagerDirectoryRules.FilterOf(PostState.InPost, ManagerDirectoryRules.NeedBudget) == ManagerDirectoryRules.FilterNeeds
              && ManagerDirectoryRules.StateOrder(PostState.InPost, 1) < ManagerDirectoryRules.StateOrder(PostState.Uncovered, 0)
              && ManagerDirectoryRules.StateOrder(PostState.Uncovered, 0) < ManagerDirectoryRules.StateOrder(PostState.Covered, 0)
              && ManagerDirectoryRules.StateOrder(PostState.Covered, 0) < ManagerDirectoryRules.StateOrder(PostState.InPost, 0)
              && ManagerDirectoryRules.StateOrder(PostState.InPost, 0) < ManagerDirectoryRules.StateOrder(PostState.Empty, 0),
            "managers directory: the state filter and order: what needs you first, then off sick, then in order");
        Check(ManagerDirectoryRules.MonthCost(110, 28) == 3080 && ManagerDirectoryRules.MonthCost(0, 28) == 0 && ManagerDirectoryRules.MonthCost(-4, 28) == 0
              && ManagerDirectoryRules.MonthCost(50, 0) == 50 && ManagerDirectoryRules.MonthCost(int.MaxValue, 400) == int.MaxValue / 4,
            "managers directory: a manager's month costs their allowance a day times the month's days; nothing, a month of no days and the largest are bounded");
    }

    // A whole company: two businesses, a factory, a warehouse and three districts.
    private static void ManagerDirectoryCounts()
    {
        var lines = new[]
        {
            new PostLine { Kind = ManagedKind.Shop, Has = true, Sick = true, Open = 3, OpenMistakes = 1, Cost = 3000, Budget = 1000 },          // off sick, nobody, a mistake
            new PostLine { Kind = ManagedKind.Shop, Has = false, Open = 1, OpenMistakes = 1 },                                                 // stood down, a mistake left
            new PostLine { Kind = ManagedKind.Works, Has = true, Sick = true, RegionThere = true, Cost = 2000, Decided = 4 },                   // covered
            new PostLine { Kind = ManagedKind.Warehouse, Has = true, Spent = 950, Budget = 1000, Cost = 1500, Open = 2 }                       // a budget nearly used
        };
        var c = new DirectoryCounts();
        for (int pass = 0; pass < 2; pass++)
        {
            c.Clear();
            foreach (PostLine l in lines)
            {
                c.Add(l);
            }
            c.AddRegion(true, false, 5000);
            c.AddRegion(true, true, 4000);
            c.AddRegion(false, false, 0);
        }
        Check(c.Managers[(int)ManagerGroup.Shop] == 1 && c.Places[(int)ManagerGroup.Shop] == 2 && c.Managers[(int)ManagerGroup.Works] == 1
              && c.Managers[(int)ManagerGroup.Warehouse] == 1 && c.Places[(int)ManagerGroup.Vacant] == 1 && c.AllPlaces == 4 && c.PlaceManagers == 3
              && c.Managers[(int)ManagerGroup.Regional] == 2 && c.Districts == 3 && c.AllManagers == 5,
            "managers directory: a company's counts: 3 place managers of 4 places, 1 with none, 2 regional managers of 3 districts, the same filled twice");
        Check(c.AllCost == 3000 + 2000 + 1500 + 5000 + 4000 && c.Cost[(int)ManagerGroup.Regional] == 9000 && c.AllOpen == 6 && c.AllSick == 3
              && c.Uncovered == 1 && c.Covered == 1,
            "managers directory: what they cost a month, the open decisions (one left by a manager stood down too), those off sick (a regional one too), and who is covered");
        Check(c.Needs[(int)ManagerGroup.Needs] == 4 && c.Needs[(int)ManagerGroup.Shop] == 2 && c.Needs[(int)ManagerGroup.Vacant] == 1
              && c.Needs[(int)ManagerGroup.Warehouse] == 1 && c.Needs[(int)ManagerGroup.Works] == 0,
            "managers directory: what needs the player, one line a place and reason: 4 over the company, counted under each group");
        var empty = new DirectoryCounts();
        Check(empty.AllManagers == 0 && empty.AllPlaces == 0 && empty.AllCost == 0 && empty.Districts == 0,
            "managers directory: a company with no place has nothing to count and nothing fails");
    }

    private static void ManagerDirectoryOrder()
    {
        string[] districts = { "Midtown", "Garment District", "midtown", "Garment District", "Hell's Kitchen" };
        string[] names = { "Zed's", "Burger Barn", "apple store", "Burger Barn", "Corner Shop" };
        string[] ids = { "a~1", "b~2", "c~3", "a~0", "d~4" };
        int[] order = new int[5];
        ManagerDirectoryRules.Order(districts, names, ids, 5, order);
        string got = string.Join(",", Array.ConvertAll(order, i => ids[i]));
        Check(got == "a~0,b~2,d~4,c~3,a~1",
            "managers directory: a group is first listed by district, then by name, ignoring case, two of one name by their place (" + got + ")");
        // The same places read in any order give the same list.
        int[] perm = { 4, 2, 0, 3, 1 };
        string[] d2 = new string[5], n2 = new string[5], i2 = new string[5];
        for (int k = 0; k < 5; k++)
        {
            d2[k] = districts[perm[k]];
            n2[k] = names[perm[k]];
            i2[k] = ids[perm[k]];
        }
        int[] order2 = new int[5];
        ManagerDirectoryRules.Order(d2, n2, i2, 5, order2);
        Check(string.Join(",", Array.ConvertAll(order2, i => i2[i])) == got, "managers directory: the same places read in another order are listed the same way, on every machine");
        int[] none = new int[1];
        ManagerDirectoryRules.Order(new string[1], new string[1], new string[1], 0, none);
        ManagerDirectoryRules.Order(new string[] { null }, new string[] { null }, new string[] { null }, 1, none);
        Check(none[0] == 0, "managers directory: no place, or one with nothing known of it, is listed without a fault");
        double ms = OrderTime(2000);
        Check(ms < 400, "managers directory: 2,000 orders of 64 places take " + ms.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)
                        + " ms, under 400 (a page is written only when it changes)");
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static double OrderTime(int times)
    {
        const int n = 64;
        var districts = new string[n];
        var names = new string[n];
        var ids = new string[n];
        var order = new int[n];
        var random = new Random(4);
        for (int i = 0; i < n; i++)
        {
            districts[i] = "District " + random.Next(8);
            names[i] = "Place " + random.Next(1000);
            ids[i] = "id~" + i;
        }
        var watch = Stopwatch.StartNew();
        for (int t = 0; t < times; t++)
        {
            ManagerDirectoryRules.Order(districts, names, ids, n, order);
        }
        return watch.Elapsed.TotalMilliseconds;
    }

    // The director and the tab go through the rules, and the bugs found stay fixed.
    private static void ManagerDirectoryWiring()
    {
        string managers = ModSource(@"Economy\Company\CompanyDirector.Managers.cs");
        string decisions = ModSource(@"Economy\Company\CompanyDirector.ManagerDecisions.cs");
        string directory = ModSource(@"Economy\Company\CompanyDirector.ManagerDirectory.cs");
        string tests = ModSource(@"Economy\Company\CompanyDirector.ManagerTests.cs");
        string app = ModSource(@"Economy\Company\CompanyApp.Managers.cs");
        string groups = ModSource(@"Economy\Company\CompanyApp.ManagerGroups.cs");
        string rota = ModSource(@"Economy\Company\RotaGame.cs");
        string mod = ModSource(@"Core\RealLifeOverhaulMod.cs");
        string features = ModSource(@"Meta\FeatureList.Economy.cs");
        string record = MemberText(managers, "private Decision Record(");
        Check(record.Contains("MadeBy(mp, who)") && !record.Contains("mp.Own.Decided++") && MemberText(directory, "private ManagerFile MadeBy(").Contains("ManagerDirectoryRules.ByRegion("),
            "managers: a decision is counted to whoever made it, the regional manager standing in too (was always the place manager)");
        Check(MemberText(decisions, "public void ManagerUndo(int id)").Contains("CountUndone(d)") && MemberText(decisions, "private void CountUndone(").Contains("MadeBy(")
              && MemberText(tests, "public void TestManagerUndoAll()").Contains("CountUndone(d)"),
            "managers: an undone decision is counted against whoever made it (was the manager of the place now)");
        string place = MemberText(app, "private void WritePlace(InfoBox box, ManagedPlace mp)");
        Check(place.Contains("PostState post = _director.PostOf(mp)") && place.Contains("if (post == PostState.Covered)") && place.Contains("_director.ChanceToday(mp)")
              && !place.Contains("ManagerRules.MistakeChance(m.Skill") && MemberText(directory, "internal int ChanceToday(").Contains("Decider(mp, out skill, out review, out who)")
              && MemberText(directory, "internal PostState PostOf(").Contains("ManagerDirectoryRules.State("),
            "managers: a manager's panel says who decides today and with what chance of a mistake, as the decisions are made (was the manager's own, and a stand-in who was off sick)");
        Check(MemberText(app, "private static string RegionState(").Contains("CompanyDirector.Sick(r)") && MemberText(app, "private void WritePlaceRows(").Contains("RegionState(r)")
              && MemberText(groups, "private void WriteRegions(").Contains("RegionState(r)") && MemberText(app, "private void WriteRegion(InfoBox box, string district)").Contains("RegionState(r)"),
            "managers: a regional manager off sick is shown off sick (was always in post)");
        Check(MemberText(app, "private void WriteOffices(").Contains("_director.RegionHeldBy(h.Id)") && MemberText(app, "private void WriteRegionHand(").Contains("reallife_mgr_region_moves"),
            "managers: who could be a regional manager says who already is one of another district, and appointing them says that district is left with none");
        Check(MemberText(app, "partial void WriteManagers(").Contains("ManagerRules.Has(ManagerRules.Kinds(mp.Kind), (ManagerTask)task)"),
            "managers: a kind's page for a kind its place no longer has opens the manager's panel instead of a switch that does nothing");
        Check(MemberText(rota, "public static void ReadPlaces(").Contains("LeftOut++") && MemberText(groups, "private void WriteDirectory(").Contains("RotaGame.LeftOut"),
            "managers: places past the most the company follows are counted and the tab says so (were left out without a word)");
        Check(MemberText(rota, "public static void ReadPlaces(").Contains("!GoingPublic.Mine(reg)") && !directory.Contains("BuildingRegistrations") && !directory.Contains("EmployeeInstances")
              && MemberText(directory, "internal void ManagerLook(").Contains("_managed") && MemberText(tests, "public void TestManagerFill()").Contains("GoingPublic.Mine(mp.Place.Reg)")
              && !groups.Contains("BuildingRegistrations") && !groups.Contains("EmployeeInstances"),
            "managers directory: only the player's own places are listed or changed (Going Public: another player's never)");
        Check(Occurs(app, "_director.ManagerLook(") == 1 && !app.Contains("void Update(") && !groups.Contains("void Update(") && !groups.Contains("LateUpdate"),
            "managers directory: the places are read once for each page written, and nothing runs per frame");
        Check(mod.Contains(".AddButton(\"reallife_mgr_test_fill\", () => Economy.CompanyDirector.Instance?.TestManagerFill())") && features.Contains("\"reallife_mgr_test_fill\""),
            "managers directory: Options > Tests has the button that fills a company with managers of every kind");
    }

    private static readonly string[] ManagerDirectoryKeys =
    {
        "reallife_mgr_all_kinds", "reallife_mgr_all_needs", "reallife_mgr_all_states", "reallife_mgr_chance_waits", "reallife_mgr_chip_needs", "reallife_mgr_col_by",
        "reallife_mgr_col_count", "reallife_mgr_col_covers", "reallife_mgr_col_group", "reallife_mgr_col_kind", "reallife_mgr_col_month_cost", "reallife_mgr_col_open",
        "reallife_mgr_col_regional", "reallife_mgr_col_standing", "reallife_mgr_col_why", "reallife_mgr_decided_standing", "reallife_mgr_dir_appoint", "reallife_mgr_dir_empty",
        "reallife_mgr_dir_look", "reallife_mgr_dir_need", "reallife_mgr_dir_needs", "reallife_mgr_dir_of", "reallife_mgr_dir_regional", "reallife_mgr_dir_shop",
        "reallife_mgr_dir_sick", "reallife_mgr_dir_vacant", "reallife_mgr_dir_warehouse", "reallife_mgr_dir_what", "reallife_mgr_dir_works", "reallife_mgr_empty_group",
        "reallife_mgr_filter_fine", "reallife_mgr_filter_needs", "reallife_mgr_filter_sick", "reallife_mgr_group_need_what", "reallife_mgr_group_places_what",
        "reallife_mgr_group_regional_what", "reallife_mgr_group_vacant", "reallife_mgr_group_vacant_what", "reallife_mgr_left_out", "reallife_mgr_need_budget",
        "reallife_mgr_need_kind_budget", "reallife_mgr_need_kind_mistake", "reallife_mgr_need_kind_uncovered", "reallife_mgr_need_mistake", "reallife_mgr_need_mistakes",
        "reallife_mgr_need_uncovered", "reallife_mgr_needs_none", "reallife_mgr_over_cost", "reallife_mgr_over_open", "reallife_mgr_over_places", "reallife_mgr_over_sick",
        "reallife_mgr_over_uncovered", "reallife_mgr_pay", "reallife_mgr_post_covered", "reallife_mgr_post_uncovered", "reallife_mgr_region_empty", "reallife_mgr_region_moves",
        "reallife_mgr_region_no_log", "reallife_mgr_region_sick", "reallife_mgr_region_their_log", "reallife_mgr_region_their_places", "reallife_mgr_sec_directory",
        "reallife_mgr_sec_district", "reallife_mgr_sec_needs", "reallife_mgr_since", "reallife_mgr_standing_in", "reallife_mgr_standing_skill", "reallife_mgr_test_fill",
        "reallife_mgr_test_filled", "reallife_mgr_vacant_nobody", "reallife_mgr_vacant_none", "reallife_mgr_vacant_staff", "reallife_mgr_place_review_off"
    };

    private static void ManagerDirectoryWords()
    {
        Dictionary<string, string>[] langs = LanguageWords(out string[] codes);
        if (!LanguagesRead("managers directory", langs, codes))
        {
            return;
        }
        Dictionary<string, string> en = langs[0];
        var missing = new List<string>();
        foreach (string key in ManagerDirectoryKeys)
        {
            string lacking = LanguagesLacking(langs, codes, key);
            if (lacking.Length > 0)
            {
                missing.Add(key + " (" + lacking + ")");
            }
        }
        Check(missing.Count == 0, "managers directory: every new text is in every language with the same {names} (" + ManagerDirectoryKeys.Length + " keys"
                                  + (missing.Count > 0 ? "; " + string.Join(", ", missing) : string.Empty) + ")");
        Check(en.TryGetValue("reallife_mgr_test_filled", out string filled) && filled.Length <= 240, "managers directory: the test's notice is at most 240 characters in English");
        bool guide = Array.TrueForAll(langs, w => w.TryGetValue("reallife_guide_economy_managers", out string g) && g.Contains("{mgr_budget_near}")
                                                  && Named(w, g, "reallife_mgr_dir_need") && Named(w, g, "reallife_mgr_dir_regional") && Named(w, g, "reallife_mgr_dir_vacant"));
        Check(guide, "managers directory: the Guide page names Needs you, Regional managers and Places with no manager as the tab does, with the budget's share, in each language");
        string[] lines =
        {
            "Managers have a directory of their own on the Company app's Managers tab",
            "Fixed: a decision a regional manager makes standing in for a manager off sick is counted as theirs",
            "Fixed: a regional manager off sick no longer looks over the district's decisions",
            "Fixed: the list of who could be a regional manager says who already is one of another district",
            "Fixed: a kind of decision a place no longer has",
            "Fixed: with more places than the company follows, the Managers tab says how many are not listed"
        };
        bool all = true;
        foreach (string line in lines)
        {
            all &= AnyChangesRow(en, line);
        }
        Check(all, "managers directory: What's new has the directory's line and each of its five fixes (in the next row, or its version's row once it is made)");
        bool feature = false;
        foreach (KeyValuePair<string, string> row in en)
        {
            int at = row.Key.StartsWith("reallife_changes_", StringComparison.Ordinal) ? row.Value.IndexOf(lines[0], StringComparison.Ordinal) : -1;
            if (at >= 0)
            {
                int fix = row.Value.IndexOf("Fixed: ", StringComparison.Ordinal);
                feature = fix < 0 || at < fix;   // the change note puts every line before the first fix under New
                break;
            }
        }
        Check(feature, "managers directory: the directory's line stands with the new things, before the row's first fix");
    }

    private static bool Named(Dictionary<string, string> words, string guide, string key)
    {
        return words.TryGetValue(key, out string name) && guide.Contains("<b>" + name + "</b>");
    }
}
