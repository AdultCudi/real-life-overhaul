using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Landmarks;
using RealLifeOverhaul.NewBuildings;
using RealLifeOverhaul.Uptown;

// Uptown's special places (Scripts/Uptown/SpecialsRules.cs), on the real plan of its 422 lots: the table clear of open land,
// the leased offices, the landmarks' blocks, the first block's old quarter and the lot kept empty, each place on a lot of a
// class that takes its structure and that structure one the game has the service's layout for, every contact key its own
// but the bank's branch, the importer's own goods (finished goods, never all of one importer's), the same table in any order
// of the plan; every record a place can make covered by the district save's list of the save's places; the sources (the
// declaration, the service attached, the load's step, the stand-down, the kerb spot, the tests); and the texts.
internal static partial class Program
{
    private static void UptownSpecialsChecks()
    {
        List<BuildingsLot> plan = UptownRealPlan();
        UptownSpecialsTable(plan);
        UptownSpecialsRefusals(plan);
        UptownSpecialsGoods();
        UptownSpecialsSaves();
        UptownSpecialsSources();
        UptownSpecialsTexts();
    }

    private static void UptownSpecialsTable(List<BuildingsLot> plan)
    {
        HashSet<string> built = UptownIds(plan, BuildingsRules.Built(plan, LandmarkRules.OwnBlock, 0f, 1f, new List<int>()));
        HashSet<string> leased = UptownIds(plan, BuildingsRules.Leased(plan, new List<int>()));
        HashSet<string> oldQuarter = UptownIds(plan, BuildingsRules.DayOne(plan, 0f, 1f, new List<int>(), LandmarkRules.OwnBlock));
        string why = SpecialsRules.TableRefusal(plan, built, leased, SpecialsRules.All);
        Check(why == null, "uptown specials: every place of the table stands on a lot of the plan built from the first day, of a class that takes its structure, never leased, open or kept empty"
                           + (why != null ? " (" + why + ")" : string.Empty));
        bool clearOfLandmarks = true, clearOfOld = true;
        string where = null;
        foreach (SpecialPlace p in SpecialsRules.All)
        {
            BuildingsLot lot = plan.Find(l => l.Id == p.Lot);
            bool landmark = LandmarkRules.OwnBlock(lot.Block), old = oldQuarter.Contains(p.Lot) || lot.First;
            clearOfLandmarks &= !landmark;
            clearOfOld &= !old;
            where = landmark || old ? where ?? p.Lot : where;
        }
        Check(clearOfLandmarks && clearOfOld, "uptown specials: no place on a landmark's block or in the first block, whose old quarter saves hold records of" + (where != null ? " (" + where + ")" : string.Empty));

        // The kinds the owner asked for, one each, and one importer.
        string[] kinds =
        {
            "ba:businesstype_recruitmentagency", "ba:businesstype_marketingagency", "ba:businesstype_interiorinstallationfirm", "ba:businesstype_officesupplystore",
            "ba:businesstype_appliancestore", "ba:businesstype_furniturestore", "ba:businesstype_supermarket", "ba:businesstype_bank", "ba:businesstype_clinic",
            "ba:businesstype_cardealership", "ba:businesstype_importexport"
        };
        bool each = SpecialsRules.All.Length == kinds.Length;
        foreach (string kind in kinds)
        {
            each &= Array.FindAll(SpecialsRules.All, p => p.ServiceType == kind).Length == 1;
        }
        Check(each, "uptown specials: one of each kind asked for: recruitment, marketing, interior installation, office supplies, appliances, furniture, a supermarket, a bank's branch, a clinic, a car dealership, an importer");

        // The structures the game has each service's layout for (StreamingAssets/BusinessLayouts, Build 3682), and where a real district has them.
        var layouts = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "ba:businesstype_recruitmentagency", "c1 c2" }, { "ba:businesstype_marketingagency", "c2 d2" }, { "ba:businesstype_interiorinstallationfirm", "d2" },
            { "ba:businesstype_officesupplystore", "d2" }, { "ba:businesstype_appliancestore", "c1 d2 m1" }, { "ba:businesstype_furniturestore", "d2 m1" },
            { "ba:businesstype_supermarket", "c1" }, { "ba:businesstype_bank", "a1 d2" }, { "ba:businesstype_clinic", "c2" }, { "ba:businesstype_cardealership", "d2 m1" },
            { "ba:businesstype_importexport", "h1" }
        };
        bool laid = true, avenues = true;
        foreach (SpecialPlace p in SpecialsRules.All)
        {
            string structure = p.Size.Substring(p.Size.LastIndexOf('_') + 1) + p.Version;
            laid &= layouts.TryGetValue(p.ServiceType, out string has) && Array.IndexOf(has.Split(' '), structure) >= 0;
            BuildingsLot lot = plan.Find(l => l.Id == p.Lot);
            bool shop = p.ServiceType == "ba:businesstype_appliancestore" || p.ServiceType == "ba:businesstype_furniturestore" || p.ServiceType == "ba:businesstype_bank"
                        || p.ServiceType == "ba:businesstype_supermarket" || p.ServiceType == "ba:businesstype_cardealership";
            avenues &= !shop || lot.Avenue;
        }
        Check(laid, "uptown specials: each place's structure is one the game has its service's layout for, so it is furnished as the game's own");
        Check(avenues, "uptown specials: the shops, the bank and the dealership stand on the avenues, as a district's main streets have them");
        int bigLots = 0;
        foreach (SpecialPlace p in SpecialsRules.All)
        {
            BuildingsLot lot = plan.Find(l => l.Id == p.Lot);
            bigLots += (p.ServiceType == "ba:businesstype_cardealership" || p.ServiceType == "ba:businesstype_importexport") && (lot.Class == BuildingsClass.Big || lot.Class == BuildingsClass.Warehouse) ? 1 : 0;
        }
        Check(bigLots == 2, "uptown specials: the dealership and the importer stand on the bigger lots, a big store's and a warehouse's");

        // The same table holds in any order of the plan, and the lots' picks do not move with it.
        var rng = new Random(20261024);
        bool same = true;
        for (int round = 0; round < 6; round++)
        {
            var shuffled = new List<BuildingsLot>(plan);
            for (int i = shuffled.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                BuildingsLot t = shuffled[i];
                shuffled[i] = shuffled[j];
                shuffled[j] = t;
            }
            HashSet<string> b2 = UptownIds(shuffled, BuildingsRules.Built(shuffled, LandmarkRules.OwnBlock, 0f, 1f, new List<int>()));
            HashSet<string> l2 = UptownIds(shuffled, BuildingsRules.Leased(shuffled, new List<int>()));
            same &= SpecialsRules.TableRefusal(shuffled, b2, l2, SpecialsRules.All) == null && b2.SetEquals(built) && l2.SetEquals(leased);
        }
        Check(same, "uptown specials: the table holds on the plan in any order of its lots, the same on every machine");
        Check(SpecialsRules.At(SpecialsRules.All[3].Lot) == SpecialsRules.All[3] && SpecialsRules.At("uptown_9_99_99") == null && SpecialsRules.At(null) == null,
            "uptown specials: a lot's place is found by its id alone; a lot of none has none");
        Check(SpecialsRules.Next(-1, 11) == 0 && SpecialsRules.Next(0, 11) == 1 && SpecialsRules.Next(10, 11) == 0 && SpecialsRules.Next(20, 11) == 0 && SpecialsRules.Next(3, 0) == -1,
            "uptown specials: the destination test goes through the places in turn and round again");
    }

    private static void UptownSpecialsRefusals(List<BuildingsLot> plan)
    {
        HashSet<string> built = UptownIds(plan, BuildingsRules.Built(plan, LandmarkRules.OwnBlock, 0f, 1f, new List<int>()));
        HashSet<string> leased = UptownIds(plan, BuildingsRules.Leased(plan, new List<int>()));
        string openLot = plan.Find(l => !built.Contains(l.Id) && BuildingsRules.Clear(l.Id) && l.Class == BuildingsClass.Small).Id;
        string leasedLot = plan.Find(l => leased.Contains(l.Id)).Id;
        // Lots of the structures the made-up places name (a D2 office's, a C2 shop's): a lot of the plan's spaces takes its own size and version alone.
        string officeLot = plan.Find(l => built.Contains(l.Id) && !leased.Contains(l.Id) && l.Class == BuildingsClass.Office && l.Sizes[0] == "ba:buildingsize_d" && l.Version == 2
                                          && SpecialsRules.At(l.Id) == null).Id;
        string smallLot = plan.Find(l => built.Contains(l.Id) && l.Sizes[0] == "ba:buildingsize_c" && l.Version == 2 && SpecialsRules.At(l.Id) == null).Id;
        const string C = "ba:buildingsize_c", Rt = SpecialsRules.Retail, Agency = "ba:businesstype_recruitmentagency";
        string Refuse(params SpecialPlace[] table) => SpecialsRules.TableRefusal(plan, built, leased, table);
        Check(Refuse(new SpecialPlace("uptown_9_99_99", "S", Agency, "A", C, 2, Rt, Rt, SpecialStaff.Desk)) != null, "uptown specials: a lot the plan lacks is refused");
        Check(Refuse(new SpecialPlace(openLot, "S", Agency, "A", C, 2, Rt, Rt, SpecialStaff.Desk)) != null, "uptown specials: open land for the construction game is refused");
        Check(Refuse(new SpecialPlace(leasedLot, "S", Agency, "A", "ba:buildingsize_d", 2, Rt, Rt, SpecialStaff.Desk)) != null, "uptown specials: a leased office is refused");
        Check(Refuse(new SpecialPlace("uptown_1_14_0004", "S", Agency, "A", C, 2, Rt, Rt, SpecialStaff.Desk)) != null, "uptown specials: the lot kept empty for the tree is refused");
        Check(Refuse(new SpecialPlace(officeLot, "S", Agency, "A", C, 2, Rt, Rt, SpecialStaff.Desk)) != null, "uptown specials: a structure the lot's class does not take is refused");
        Check(Refuse(new SpecialPlace(smallLot, "S", Agency, "A", C, 2, Rt, Rt, SpecialStaff.Desk), new SpecialPlace(smallLot, "S", Agency, "B", C, 2, Rt, Rt, SpecialStaff.Desk)) != null,
            "uptown specials: two places on one lot are refused");
        Check(Refuse(new SpecialPlace(smallLot, "S", Agency, null, C, 2, Rt, Rt, SpecialStaff.Desk)) != null && Refuse(new SpecialPlace(smallLot, "S", SpecialsRules.BankType, null, C, 2, Rt, Rt, SpecialStaff.Desk)) == null,
            "uptown specials: only a bank's branch keeps the game's name (and with it the head office's contact and loans)");
        Check(Refuse(new SpecialPlace(smallLot, "S", Agency, "A", C, 2, Rt, Rt, SpecialStaff.Desk), new SpecialPlace(officeLot, "S", Agency, "A", "ba:buildingsize_d", 2, Rt, Rt, SpecialStaff.Desk)) != null
              && Refuse(new SpecialPlace(smallLot, "S", Agency, "A", C, 2, Rt, Rt, SpecialStaff.Desk), new SpecialPlace(officeLot, "S", "ba:businesstype_marketingagency", "A", "ba:buildingsize_d", 2, Rt, Rt, SpecialStaff.Desk)) == null,
            "uptown specials: two places of one name and type are refused, as they would share one contact; one name in two types is two contacts");
        Check(Refuse(new SpecialPlace(smallLot, "S", Agency, "A", C, 2, SpecialsRules.Special, Rt, SpecialStaff.Desk)) != null
              && Refuse(new SpecialPlace(smallLot, "S", Agency, "A", C, 2, Rt, SpecialsRules.Special, SpecialStaff.Desk)) != null,
            "uptown specials: the special type is the importers' alone, and never the type a place stands as without its service");
        Check(Refuse(new SpecialPlace(smallLot, "S", SpecialsRules.ImportType, "A", C, 2, Rt, Rt, SpecialStaff.Desk, null, new string[0])) != null
              && Refuse(new SpecialPlace(smallLot, "S", SpecialsRules.ImportType, "A", C, 2, Rt, Rt, SpecialStaff.Desk, null, new[] { "ba:itemname_beer", "ba:itemname_beer" })) != null
              && Refuse(new SpecialPlace(smallLot, "S", Agency, "A", C, 2, Rt, Rt, SpecialStaff.Desk, null, new[] { "ba:itemname_beer" })) != null,
            "uptown specials: an importer's own goods are a list of distinct goods, never empty, and only an importer has them");
        Check(Refuse(new SpecialPlace(smallLot, null, Agency, "A", C, 2, Rt, Rt, SpecialStaff.Desk)) != null, "uptown specials: a place with no service of the game's to copy is refused");
    }

    private static void UptownSpecialsGoods()
    {
        // The game's importers of finished goods (Build 3682, defs_ImportExportSettings), and its importers of raw materials.
        var finished = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            { "UnitedOceanImport", new[] { "paperbag", "beer", "bottleofwine", "cheeseplatter", "cigar", "cigarette", "classiccheapmaleclothing", "classiccheapfemaleclothing", "moderncheapmaleclothing", "moderncheapfemaleclothing", "margarita", "martini", "sodacan", "whisky" } },
            { "BlueStone Imports", new[] { "paperbag", "cheapgift", "expensivegift", "cheapjewelry", "limitededitionbook", "motivationalbook", "earbuds01", "novel", "picturebook", "headphones01", "sodacan", "technicalmanual", "umbrella", "youngnovel" } },
            { "SeaSideInternationals", new[] { "paperbag", "apple", "banana", "carrot", "lettuce", "pear", "tomato", "cottoncandy", "cheapflower", "expensiveflower", "freshfood", "frozenfood", "haircareproduct", "popcorn", "slushi", "sodacan" } },
            { "JetCargoImports", new[] { "paperbag", "burger", "kabob", "croissant", "cupofcoffee", "cupoftea", "cupcake", "donut", "energydrink", "frenchfries", "hotdog", "icecream", "pizza", "salad", "sodacan" } },
        };
        var all = new HashSet<string>(StringComparer.Ordinal);
        foreach (string[] list in finished.Values)
        {
            foreach (string g in list)
            {
                all.Add("ba:itemname_" + g);
            }
        }
        SpecialPlace importer = Array.Find(SpecialsRules.All, p => p.ServiceType == SpecialsRules.ImportType);
        bool known = importer != null && importer.Goods != null && importer.Goods.Length >= 8, wholeOfOne = false;
        foreach (string g in importer != null && importer.Goods != null ? importer.Goods : new string[0])
        {
            known &= all.Contains(g);
        }
        foreach (KeyValuePair<string, string[]> one in finished)
        {
            int had = 0;
            foreach (string g in one.Value)
            {
                had += Array.IndexOf(importer.Goods, "ba:itemname_" + g) >= 0 ? 1 : 0;
            }
            wholeOfOne |= had * 2 > one.Value.Length;
        }
        Check(known && importer.Service == "BlueStone Imports" && importer.Size == "ba:buildingsize_h" && importer.Type == SpecialsRules.Special && importer.PlainType == SpecialsRules.Warehouse,
            "uptown specials: the importer copies one of the game's importers of finished goods, special-type H1 as the game's own stand (a warehouse would be put up for rent by the game's swap), its goods finished goods the game imports");
        Check(!wholeOfOne, "uptown specials: the importer's own goods take no more than half of any one importer's, so no importer's whole weekly allowance is doubled");
    }

    private static void UptownSpecialsSaves()
    {
        string root = System.IO.Path.GetFullPath(System.IO.Path.Combine(LocalesFolder(), "..", "..", "..", ".."));
        string path = System.IO.Path.Combine(root, "RealLifeOverhaul.Build", "ContractCheck", "SavePlaces.txt");
        var kinds = new Dictionary<string, string>(StringComparer.Ordinal);
        if (File.Exists(path))
        {
            foreach (string raw in File.ReadAllLines(path))
            {
                string[] parts = raw.Split('|');
                if (raw.StartsWith("#", StringComparison.Ordinal) || parts.Length < 3)
                {
                    continue;
                }
                kinds[parts[0].Trim()] = parts[2].Trim();
            }
        }
        // What a special place of each kind writes into a save that names its address (the game's own records).
        string[] made =
        {
            "GameInstance.Contacts[].streetName", "GameInstance.Transactions[].address", "GameInstance.TodoTasks[].address", "GameInstance.customDestination",
            "GameInstance.RecruitmentCampaigns[].agencyAddress", "GameInstance.BuildingRegistrations[].marketingCampaigns[].agencyAddress",
            "GameInstance.interiorInstallationFirmContracts[].interiorInstallationFirmAddress", "GameInstance.FurnitureDeliveryContracts[].fromAddress",
            "GameInstance.JobInstances[].address", "GameInstance.Loans[].bankAddress", "GameInstance.CurrentStreetName", "GameInstance.VehicleInstances[].position",
            "GameInstance.vehicleDeliveryContracts[].vehicleStoreAddress", "GameInstance.importPartnerships[].importAddress", "GameInstance.itemsOrderedThisWeekByImporter{key}",
            "GameInstance.importPartnerships[].products[].assignedWarehouse", "GameInstance.marketEvents[].address", "GameInstance.BuildingRegistrations[].StreetName"
        };
        string missing = null;
        foreach (string field in made)
        {
            bool covered = kinds.TryGetValue(field, out string kind) && kind.Length > 0 && !kind.StartsWith("display", StringComparison.Ordinal) && kind != "obsolete";
            missing = covered ? missing : missing ?? field;
        }
        Check(kinds.Count > 50 && missing == null,
            "uptown specials: every record a special place can make that names its address leaves the save with the district's (SavePlaces.txt), so a save loads without the mod"
            + (missing != null ? " (not covered: " + missing + ")" : string.Empty));
    }

    private static void UptownSpecialsSources()
    {
        string scripts = System.IO.Path.GetFullPath(System.IO.Path.Combine(LocalesFolder(), "..", "Scripts"));
        string Read(string file)
        {
            string p = System.IO.Path.Combine(scripts, file);
            return File.Exists(p) ? CodeAlone(File.ReadAllText(p)) : string.Empty;
        }
        string director = Read(@"Uptown\LifeDirector.cs"), specials = Read(@"Uptown\LifeDirector.Specials.cs"), game = Read(@"Uptown\LifeGame.Specials.cs");
        string guide = Read(@"Uptown\LifeGuide.cs"), rules = Read(@"Uptown\SpecialsRules.cs");
        Check(director.Length > 0 && specials.Length > 0 && game.Length > 0 && rules.Length > 0, "uptown specials: the rules, the director's part and the game's part are there");
        string declare = MemberText(director, "private static void Declare()");
        int finished = declare.IndexOf("if (f != null)", StringComparison.Ordinal), special = declare.IndexOf("if (SpecialStructure(lot, out structure, out place))", StringComparison.Ordinal);
        int lease = declare.IndexOf("bool lease = LeaseOf(lot.Id) >= 0;", StringComparison.Ordinal), serve = declare.IndexOf("LifeGame.Serve(spec, place);", StringComparison.Ordinal);
        int declared = declare.IndexOf("MadeBuildings.Declare(spec);", StringComparison.Ordinal);
        Check(finished > 0 && special > finished && lease > special && serve > lease && declared > serve,
            "uptown specials: a place's lot takes the place's structure with no other (a finished building keeps the construction game's), and its declaration the service before it is declared");
        string give = MemberText(game, "public static void Serve(MadeSpec spec, SpecialPlace place)");
        Check(give.Contains("spec.Service = place.Service;") && give.Contains("spec.ServiceName = place.Name;") && give.Contains("spec.ServiceEmployer = place.Employer;")
              && give.Contains("spec.ServiceGoods = place.Goods;") && give.Contains("spec.PlainType = place.PlainType;"),
            "uptown specials: the declaration carries the service, its name, its job's boss, an importer's goods and the type it stands as without the service");
        string life = MemberText(director, "private static bool FirstLife()");
        Check(life.IndexOf("int specials = Specials();", StringComparison.Ordinal) > life.IndexOf("int leased = Leases(save, out int renamed);", StringComparison.Ordinal)
              && life.Contains("SpecialsCensus(\"the load\", false, out int _);"),
            "uptown specials: at the late load each place is made whole after the leased offices, and the census line is written with Developer diagnostics on");
        string whole = MemberText(game, "public static string Special(MadeBuilding made, SpecialPlace place)"), kerb = MemberText(game, "private static bool Kerb(MadeBuilding made)");
        Check(whole.Contains("MadeGame.ImporterListed(record, made.Registration, true)") && whole.Contains("place.Staff == SpecialStaff.Showroom && !Kerb(made)")
              && kerb.Contains("c.customPositions.Insert(0, spot);") && kerb.Contains("spot.SetParent(c.transform, false);"),
            "uptown specials: an importer is among the game's importers, and a dealership has its kerb spot first in its custom positions (a car bought on its floor stands at the first)");
        string forget = MemberText(director, "private static void Begin()");
        Check(forget.Contains("ForgetSpecials();") && MemberText(director, "private void OnDestroy()").Contains("ForgetSpecials();"),
            "uptown specials: nothing of a place carries over from one load to the next");
        Check(guide.Contains(".Check(\"reallife_uptown_test_specials\", LifeDirector.TestSpecials, \"Uptown check (special places)\")") && guide.Contains(".Test(\"reallife_uptown_test_special_go\", LifeDirector.TestSpecialGo)"),
            "uptown specials: the census and the destination test are in the options' tests, each working when pressed");
        string census = MemberText(specials, "private static bool SpecialsCensus(string when, bool asked, out int whole)");
        Check(census.Contains("LifeGame.SpecialWhy(made, place)") && census.Contains("Logger?.Warn(text);") && census.Contains("\"Uptown check (special places): at \""),
            "uptown specials: the census writes one line with every place's verdict, a FAIL always");
        Check(!Regex.IsMatch(rules + specials + game, @"\bRandom\b|DateTime\.Now|Time\.time\b") && !Regex.IsMatch(specials + game, @"void Update\(|LateUpdate\("),
            "uptown specials: nothing is left to chance or to a machine's clock, and nothing runs a frame");
    }

    private static void UptownSpecialsTexts()
    {
        string folder = LocalesFolder();
        Dictionary<string, string> en = LoadWords(System.IO.Path.Combine(folder, "en.json")), pt = LoadWords(System.IO.Path.Combine(folder, "pt.json"));
        Dictionary<string, string> zh = LoadWords(System.IO.Path.Combine(folder, "zh-cn.json"));
        string[] keys =
        {
            "reallife_uptown_test_specials", "reallife_uptown_test_special_go", "reallife_uptown_test_specials_done", "reallife_uptown_test_specials_none", "reallife_uptown_test_special_go_done"
        };
        bool all = en != null && pt != null && zh != null, short240 = true;
        string missing = null;
        foreach (string key in keys)
        {
            bool there = all && en.ContainsKey(key) && pt.ContainsKey(key) && zh.ContainsKey(key) && en[key].Length > 0 && SameHoles(en[key], pt[key], zh[key]);
            missing = there ? missing : missing ?? key;
            all &= there;
            short240 &= en == null || !en.ContainsKey(key) || en[key].Length <= 240;
        }
        Check(all && short240, "uptown specials: every new text in en, pt and zh-cn with the same {holes}, every notice at most 240 characters (" + (missing ?? keys.Length + " texts") + ")");
        var langs = new[] { "en", "pt", "zh-cn", "de", "fr", "es", "ru", "nl", "tr" };
        string lineMissing = null;
        foreach (string lang in langs)
        {
            Dictionary<string, string> words = LoadWords(System.IO.Path.Combine(folder, lang + ".json"));
            bool found = words != null && words.TryGetValue("reallife_changes_next", out string next) && next.Contains("Vantander Bank");
            lineMissing = found ? lineMissing : lineMissing ?? lang;
        }
        Check(lineMissing == null, "uptown specials: What's new's next row tells of the special places in all nine languages" + (lineMissing != null ? " (not in " + lineMissing + ")" : string.Empty));
    }
}
