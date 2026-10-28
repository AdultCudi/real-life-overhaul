using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using RealLifeOverhaul.NewBuildings;
using RealLifeOverhaul.Uptown;

// Crest Motors, Uptown's car dealership, and every special place's desk (fix/crest-motors; a player sat at its desk, the game froze and its
// purchase panel came up empty). The game seats the player at a special employee's desk and opens the dialog of the building's SERVICE; the
// dealership copies Manhattan Luxury Cars, a service with no dialog and no vehicle store's settings. Game-free: what a desk needs of its
// service (ServiceDeskRules), the dealership's cars for the game's list (StockRules), the look at a panel up on no dialog (DeskWatchRules);
// then, from the installed game's layouts, that the rules know every kind of desk the game has and the dealership's layout has its cars;
// the dealership's district; the sources; and the texts, in nine languages.
internal static partial class Program
{
    private const string DealerFix = "Crest Motors";

    private static void UptownDealerChecks()
    {
        UptownDealerRules();
        UptownDealerLayouts();
        UptownDealerFloor();
        UptownDealerSources();
        UptownDealerTexts();
    }

    private static void UptownDealerRules()
    {
        // What a desk makes of its service's dialog.
        Check(ServiceDeskRules.IsDesk("ba:itemname_specialemployeedesk") && ServiceDeskRules.IsDesk("ba:itemname_specialemployeedesksmall") && !ServiceDeskRules.IsDesk("ba:itemname_officedesk2left")
              && !ServiceDeskRules.IsDesk(null) && !ServiceDeskRules.IsDesk(string.Empty),
            "dealer: the game's special employee desks are told from its ordinary desks by their item name");
        Check(ServiceDeskRules.EmployeeOf("VehicleStore") == "VehicleStore" && ServiceDeskRules.EmployeeOf("Bank,Alice") == "Bank" && ServiceDeskRules.EmployeeOf("  Import ,x") == "Import"
              && ServiceDeskRules.EmployeeOf(null) == string.Empty && ServiceDeskRules.EmployeeOf("   ") == string.Empty,
            "dealer: the kind of person at a desk is the first piece of its custom value, as the game reads it");
        string[] kinds = { "Wholesale", "Furniture", "Import", "Bank", "MarketingAgency", "RecruitmentAgency", "InstallationFirm", "MovingCompany", "HealthInsurance", "VehicleStore", "PrivateDriverService" };
        Check(kinds.All(k => ServiceDeskRules.DialogOf(k) != null) && ServiceDeskRules.DialogOf("VehicleStore") == "VehicleStoreDialog" && ServiceDeskRules.DialogOf("Bank") == "BankDialog"
              && ServiceDeskRules.DialogOf("InstallationFirm") == "InteriorInstallationFirm" && ServiceDeskRules.DialogOf("LongevityDoctor") == null && ServiceDeskRules.DialogOf("") == null,
            "dealer: every kind of person the game seats at a desk has the dialog it speaks (the doctor's is the game's own whatever the service)");
        Check(ServiceDeskRules.Verdict("VehicleStore", "NotImplemented") == DeskVerdict.NoDialog && ServiceDeskRules.Verdict("VehicleStore", string.Empty) == DeskVerdict.NoDialog
              && ServiceDeskRules.Verdict("VehicleStore", null) == DeskVerdict.NoDialog && ServiceDeskRules.Verdict("", "NotImplemented") == DeskVerdict.NoDialog,
            "dealer: a desk on a service with no dialog is told so, whatever kind of person sits at it");
        Check(ServiceDeskRules.Verdict("VehicleStore", "VehicleStoreDialog") == DeskVerdict.Fine && ServiceDeskRules.Verdict("Bank", "BankDialog") == DeskVerdict.Fine
              && ServiceDeskRules.Verdict("", "BankDialog") == DeskVerdict.Fine && ServiceDeskRules.Verdict("LongevityDoctor", "NotImplemented") == DeskVerdict.Fine
              && ServiceDeskRules.Verdict("VehicleStore", "BankDialog") == DeskVerdict.WrongDialog,
            "dealer: a desk on the dialog it speaks is fine, on another kind's it is wrong, and the doctor's desk needs none of the service's");
        Check(ServiceDeskRules.Complete("VehicleStore", "NotImplemented") == "VehicleStoreDialog" && ServiceDeskRules.Complete("Bank", "NotImplemented") == "BankDialog"
              && ServiceDeskRules.Complete("VehicleStore", "VehicleStoreDialog") == null && ServiceDeskRules.Complete("VehicleStore", "BankDialog") == null && ServiceDeskRules.Complete("", "NotImplemented") == null
              && ServiceDeskRules.NeedsStore("VehicleStore") && !ServiceDeskRules.NeedsStore("Bank") && !ServiceDeskRules.NeedsStore(null),
            "dealer: only a service with no dialog is completed for its desk, never one that has another kind's; a vehicle store's desk needs the store's settings");
        Check((ServiceDeskRules.Why("VehicleStore", "NotImplemented", DeskVerdict.NoDialog) ?? string.Empty).Contains("no dialog") && (ServiceDeskRules.Why("VehicleStore", "BankDialog", DeskVerdict.WrongDialog) ?? string.Empty).Contains("BankDialog")
              && ServiceDeskRules.Why("Bank", "BankDialog", DeskVerdict.Fine) == null,
            "dealer: the census says in words what is wrong at a desk, and nothing where nothing is");

        // The split of Uptown (27 Oct 2026) kept the dealership in Uptown: its lot's block is in a column of the east half, east of 5th Street.
        SpecialPlace crest = SpecialsRules.All.First(p => p.Staff == SpecialStaff.Showroom);
        int crestBlock = Block(crest.Lot);
        Check(crest.Name == DealerFix && crestBlock > 0 && DistrictSplit.OfBlock(crestBlock) == DistrictSplit.Uptown && DistrictSplit.IdOf(DistrictSplit.OfBlock(crestBlock)) == DistrictSplit.UptownId
              && crestBlock % 100 > DistrictSplit.LastWestColumn,
            "dealer: Crest Motors is in Uptown after the split, in a column east of 5th Street, with the game's district of that id");

        // The dealership's cars for the game's list.
        var into = new List<string>();
        int added = StockRules.Distinct(new[] { "ba:vehicletype_a", "ba:vehicletype_b", "ba:vehicletype_a", null, string.Empty, "ba:vehicletype_missing", "ba:vehicletype_c" },
            n => n != "ba:vehicletype_missing", into);
        Check(added == 3 && into.SequenceEqual(new[] { "ba:vehicletype_a", "ba:vehicletype_b", "ba:vehicletype_c" }),
            "dealer: a dealership's cars go to the game's list in the layout's order, each once, those the game has");
        var kept = new List<string> { "ba:vehicletype_a" };
        Check(StockRules.Distinct(new[] { "ba:vehicletype_a", "ba:vehicletype_d" }, null, kept) == 1 && kept.Count == 2 && StockRules.Distinct(null, null, kept) == 0 && StockRules.Distinct(new[] { "x" }, null, null) == 0,
            "dealer: a car already in the list is not added again, and a missing list or layout adds none");
        Check(StockRules.Real(9, 14000f) && !StockRules.Real(0, 14000f) && !StockRules.Real(9, 0f) && !StockRules.Real(9, -1f),
            "dealer: a panel's reading is a real one only with cars and a price above nothing");

        // The look at a desk gone wrong.
        float since = -1f;
        bool early = DeskWatchRules.Broken(true, false, ref since, 10f, DeskWatchRules.Grace), late = DeskWatchRules.Broken(true, false, ref since, 10f + DeskWatchRules.Grace, DeskWatchRules.Grace);
        float s2 = -1f;
        DeskWatchRules.Broken(true, false, ref s2, 5f, DeskWatchRules.Grace);
        bool recovered = !DeskWatchRules.Broken(true, true, ref s2, 9f, DeskWatchRules.Grace) && s2 < 0f;
        float s3 = -1f;
        DeskWatchRules.Broken(true, false, ref s3, 5f, DeskWatchRules.Grace);
        bool closedByPlayer = !DeskWatchRules.Broken(false, false, ref s3, 9f, DeskWatchRules.Grace) && s3 < 0f;
        Check(!early && late && recovered && closedByPlayer && DeskWatchRules.Grace > 1f && DeskWatchRules.Grace <= 5f,
            "dealer: a dialog panel up on no dialog is closed once it has stood so for the grace, never earlier, and never one that has a dialog or has shut");
        int looks = 0;
        bool a = DeskWatchRules.Stuck(true, false, ref looks, DeskWatchRules.SeatedLooks), b = DeskWatchRules.Stuck(true, false, ref looks, DeskWatchRules.SeatedLooks), c = DeskWatchRules.Stuck(true, false, ref looks, DeskWatchRules.SeatedLooks);
        int held = 2;
        bool heldOk = !DeskWatchRules.Stuck(true, true, ref held, DeskWatchRules.SeatedLooks) && held == 0;
        int up = 2;
        bool standing = !DeskWatchRules.Stuck(false, false, ref up, DeskWatchRules.SeatedLooks) && up == 0;
        Check(!a && !b && c && heldOk && standing && DeskWatchRules.SeatedLooks >= 2,
            "dealer: a player seated with nothing holding them is stood up after a few looks in a row, never one the game holds or one on their feet");
    }

    // The installed game's own layouts (StreamingAssets/BusinessLayouts), when the game is on this machine.
    private static void UptownDealerLayouts()
    {
        string root = System.IO.Path.Combine(DesignGameFiles, "BusinessLayouts");
        if (!Directory.Exists(root))
        {
            Console.WriteLine("dealer: the game's layouts are not on this machine; its desks and showcases are not read (SKIPPED)");
            return;
        }
        var unknown = new SortedSet<string>(StringComparer.Ordinal);
        int desks = 0;
        foreach (string file in Directory.EnumerateFiles(root, "*.json", SearchOption.AllDirectories))
        {
            JObject set = ReadLayout(file);
            foreach (JObject item in Items(set))
            {
                if (!ServiceDeskRules.IsDesk((string)item["itemName"]))
                {
                    continue;
                }
                desks++;
                string kind = ServiceDeskRules.EmployeeOf((string)item["customValue"]);
                if (kind.Length > 0 && ServiceDeskRules.DialogOf(kind) == null && kind != ServiceDeskRules.DoctorEmployee)
                {
                    unknown.Add(kind);
                }
            }
        }
        Check(desks > 0 && unknown.Count == 0,
            "dealer: the rules know every kind of person the game's layouts seat at a desk (" + desks + " desks read)" + (unknown.Count > 0 ? "; unknown: " + string.Join(", ", unknown) : string.Empty));

        // The dealership copies Manhattan Luxury Cars, whose layout (MurrayHillCarDealershipLuxury, structure M1) is a showroom of showcases and has no desk
        // of its own: its desk and its panel come from the copy's store, so the cars the panel lists are the showcases' (Build 3682).
        SpecialPlace dealer = SpecialsRules.All.First(p => p.Staff == SpecialStaff.Showroom);
        string lux = System.IO.Path.Combine(root, "CarDealership", "M1", "MurrayHillCarDealershipLuxury.json");
        string trucks = System.IO.Path.Combine(root, "CarDealership", "M1", "IndustryCityCarDealershipTrucks.json");
        if (File.Exists(lux) && File.Exists(trucks))
        {
            JObject set = ReadLayout(lux);
            var cars = Items(set).Where(i => i["playerItemPurchaserSettings"] is JObject p && (bool?)p["enabled"] == true && ((string)p["itemName"] ?? string.Empty).EndsWith("showcase", StringComparison.Ordinal))
                .Select(i => (string)i["playerItemPurchaserSettings"]["itemName"]).Distinct().ToList();
            Check(dealer.ServiceType == "ba:businesstype_cardealership" && dealer.Service == "Manhattan Luxury Cars" && dealer.Size == "ba:buildingsize_m" && dealer.Version == 1 && cars.Count >= 5,
                "dealer: the dealership copies the luxury showroom, whose layout has " + cars.Count + " different cars on its showcases for the panel's list");
            Check(!Items(set).Any(i => ServiceDeskRules.IsDesk((string)i["itemName"])) && Items(ReadLayout(trucks)).Any(i => ServiceDeskRules.IsDesk((string)i["itemName"]) && ServiceDeskRules.NeedsStore(ServiceDeskRules.EmployeeOf((string)i["customValue"]))),
                "dealer: the game's own vehicle store desk is the trucks dealership's, not the luxury showroom's: the copy must carry the store itself, as the game's luxury service has none");
        }
        else
        {
            Console.WriteLine("dealer: the dealership layouts are not in the game's files on this machine (SKIPPED)");
        }
    }

    private static JObject ReadLayout(string file)
    {
        return JObject.Parse(File.ReadAllText(file).TrimStart('﻿'));
    }

    private static IEnumerable<JObject> Items(JObject set)
    {
        return set["Items"] is JArray items ? items.OfType<JObject>() : Enumerable.Empty<JObject>();
    }

    private static void UptownDealerSources()
    {
        string scripts = System.IO.Path.GetFullPath(System.IO.Path.Combine(LocalesFolder(), "..", "Scripts"));
        string Read(string file)
        {
            string p = System.IO.Path.Combine(scripts, file);
            return File.Exists(p) ? CodeAlone(File.ReadAllText(p)) : string.Empty;
        }
        string specials = Read(@"Uptown\LifeGame.Specials.cs"), watch = Read(@"Uptown\LifeGame.Dealer.cs"), director = Read(@"Uptown\LifeDirector.cs"), dealerDirector = Read(@"Uptown\LifeDirector.Dealer.cs");
        string services = Read(@"NewBuildings\MadeGame.Services.cs"), desks = Read(@"NewBuildings\MadeGame.Desks.cs"), guide = Read(@"Uptown\LifeGuide.cs"), rules = Read(@"NewBuildings\ServiceDeskRules.cs");
        Check(specials.Length > 0 && watch.Length > 0 && director.Length > 0 && dealerDirector.Length > 0 && services.Length > 0 && desks.Length > 0 && guide.Length > 0 && rules.Length > 0,
            "dealer: the rules, the made buildings' completion, the district's look and tests are there");

        Check(MemberText(specials, "public static void Serve(MadeSpec spec, SpecialPlace place)").Contains("spec.ServiceStore = place.Staff == SpecialStaff.Showroom;"),
            "dealer: the dealership's declaration says its copy is a vehicle store's");
        string copy = MemberText(services, "private static SpecialService Serve(MadeSpec spec, BuildingRegistration registration, bool fresh, out string why, out bool held)");
        int added = copy.IndexOf("Served.Add(copy);", StringComparison.Ordinal), completed = copy.IndexOf("Complete(copy, spec);", StringComparison.Ordinal), filled = copy.IndexOf("FillAsGame(registration, copy, spec);", StringComparison.Ordinal);
        Check(added > 0 && completed > added && filled > completed,
            "dealer: every service copy is completed for its desks and cars, before the registration is filled from it");
        string complete = MemberText(services, "private static void Complete(SpecialService copy, MadeSpec spec)");
        Check(complete.Contains("CompleteDesks(copy, spec)") && complete.Contains("catch (Exception e)") && complete.Contains("as the game has it"),
            "dealer: the completion is behind a try: a build without one of the game members it reads leaves the copy as the game has it, and says so");
        string stand = MemberText(services, "private static void DropServices()");
        Check(stand.Contains("DropStock();") && MemberText(desks, "public static void Unstock(Address address)").Contains("ContractItemsForSaleService.RemoveAddress(address);")
              && MemberText(desks, "public static void Unstock(Address address)").Contains("ContractItemsForSaleService.RemoveContact(contact);"),
            "dealer: the dealership's cars leave the game's list when the city goes, by address and by contact, as they went in");
        string store = MemberText(desks, "private static string Store(SpecialService copy, MadeSpec spec)");
        Check(store.Contains("CallDialogType.VehicleStoreDialog") && store.Contains("ScriptableObject.CreateInstance<VehicleStoreSettings>()") && store.Contains("own.canDeliver = false;")
              && store.Contains("ContactCategoryName.FurnitureAndEquipment") && store.Contains("Served.Add(own);"),
            "dealer: the copy gets the vehicle store's dialog, settings (the floor's panel offers no delivery, as before) and contact category the game's delivery looks up, and the settings go away with the city");
        string stock = MemberText(desks, "public static bool Stock(Address address, string contact, List<string> cars)");
        Check(stock.Contains("ContractItemsForSaleService.SetVehiclesForAddress(address, cars);") && stock.Contains("ContractItemsForSaleService.SetVehiclesForContact(contact, cars);")
              && stock.Contains("TryGetVehiclesForAddress"),
            "dealer: the cars go into the game's list of a vehicle store's cars by address and by contact, and are read back");

        string whole = MemberText(specials, "public static string Special(MadeBuilding made, SpecialPlace place)"), why = MemberText(specials, "public static string SpecialWhy(MadeBuilding made, SpecialPlace place)");
        Check(whole.Contains("return Stock(made, place);") && MemberText(specials, "private static string Stock(MadeBuilding made, SpecialPlace place)").Contains("catch (System.Exception e)")
              && why.Contains("DesksWhy(layout, service)") && why.Contains("StoreWhy(made, service)"),
            "dealer: a dealership is made whole with its cars in the game's list, and the census asks every place's desks for their dialog and the dealership for its store");
        Check(MemberText(specials, "private static string DesksWhy(BusinessLayoutSet layout, SpecialService service)").Contains("ServiceDeskRules.Verdict(kind, dialog)")
              && MemberText(specials, "private static string StoreWhy(MadeBuilding made, SpecialService service)").Contains("MadeGame.Listed(made.Building.Address)"),
            "dealer: the census reads each desk's dialog and the dealership's cars from the game's own lists");

        string look = MemberText(watch, "public static string Watch(float now)");
        Check(look.Contains("DeskWatchRules.Broken(") && look.Contains("ui.FinishDialog();") && look.Contains("DeskWatchRules.Stuck(") && look.Contains("body.Reset();") && look.Contains("PlayerActivityUI.IsPanelOpen"),
            "dealer: a panel up on no dialog is closed with the game's own FinishDialog and a seated player is stood up with its Reset, never one at an activity");
        string onUpdate = MemberText(director, "private void OnUpdate()");
        int lives = onUpdate.IndexOf("if (!Lives || _standingDown)", StringComparison.Ordinal), look2 = onUpdate.IndexOf("WatchDesk(now);", StringComparison.Ordinal);
        Check(lives > 0 && look2 > lives && onUpdate.IndexOf("if (now < _nextSlow)", StringComparison.Ordinal) < lives,
            "dealer: the look is taken once a second, after the director's time comparison, and only while the district lives");
        string watchDesk = MemberText(dealerDirector, "private static void WatchDesk(float now)");
        Check(watchDesk.Contains("LifeGame.PlayerInside(Standing)") && watchDesk.Contains("LifeGame.WatchReset();") && !Regex.IsMatch(dealerDirector + watch + desks + rules, @"void Update\(|LateUpdate\(|FixedUpdate\(|FindObjectsByType|FindObjectOfType|Physics\."),
            "dealer: the look reads only while the player is in one of the district's buildings, and nothing here runs a frame or searches the scene");
        Check(!Regex.IsMatch(rules + desks + watch + dealerDirector, @"\bRandom\b|DateTime\.Now|Time\.time\b"), "dealer: nothing is left to chance or to a machine's clock");
        Check(guide.Contains(".Check(\"reallife_uptown_test_dealer\", LifeDirector.TestDealer, \"Uptown check (dealership)\")") && guide.Contains(".Test(\"reallife_uptown_test_dealer_sit\", LifeDirector.TestDealerSit)")
              && dealerDirector.Contains("\"Uptown check (dealership): \"") && dealerDirector.Contains("\"Uptown check (dealership seat): \"") && dealerDirector.Contains("(pass ? \"PASS\" : \"FAIL\")"),
            "dealer: the panel test and the seat test are in the options' tests, each writing its PASS or FAIL line to Player.log");
        string panel = MemberText(dealerDirector, "private IEnumerator ReadPanel(MadeBuilding made, SpecialPlace place)");
        Check(panel.Contains("LifeGame.OpenStore(made, out string why)") && panel.Contains("LifeGame.ReadStore(") && panel.Contains("StockRules.Real(cars, price) && fee > 0f") && panel.Contains("LifeGame.CloseDialog();")
              && panel.Contains("finally"),
            "dealer: the panel test opens the game's own panel by the service's dialog, reads its cars, price and fee, and shuts it the game's way whatever happens");
        string seat = MemberText(dealerDirector, "private IEnumerator SitAndStand(string name, MadeBuilding made, Transform seat)");
        Check(seat.Contains("LifeGame.SitAtDesk(seat)") && seat.Contains("LifeGame.StandFromDesk()") && seat.Contains("LifeGame.OpenNothing()") && seat.Contains("finally") && Regex.Matches(seat, @"LifeGame\.StandFromDesk\(\)").Count >= 2,
            "dealer: the seat test sits and stands the player, then leaves the game's panel up on no dialog for the look to close, and never leaves the player held");
    }

    private static void UptownDealerTexts()
    {
        string folder = LocalesFolder();
        Dictionary<string, string> en = LoadWords(System.IO.Path.Combine(folder, "en.json")), pt = LoadWords(System.IO.Path.Combine(folder, "pt.json")), zh = LoadWords(System.IO.Path.Combine(folder, "zh-cn.json"));
        string[] keys =
        {
            "reallife_uptown_test_dealer", "reallife_uptown_test_dealer_sit", "reallife_uptown_test_dealer_done", "reallife_uptown_test_dealer_fail", "reallife_uptown_test_dealer_none",
            "reallife_uptown_test_dealer_away", "reallife_uptown_test_dealer_sit_done", "reallife_uptown_test_dealer_sit_fail"
        };
        bool all = en != null && pt != null && zh != null, short240 = true, plain = true;
        string missing = null;
        foreach (string key in keys)
        {
            bool there = all && en.ContainsKey(key) && pt.ContainsKey(key) && zh.ContainsKey(key) && en[key].Length > 0 && SameHoles(en[key], pt[key], zh[key]);
            missing = there ? missing : missing ?? key;
            all &= there;
            short240 &= en == null || !en.ContainsKey(key) || en[key].Length <= 240;
            plain &= en == null || !en.ContainsKey(key) || (!en[key].Contains("  ") && !en[key].Contains("!") && !en[key].Contains("–") && !en[key].Contains("—"));
        }
        Check(all && short240 && plain, "dealer: every new text in en, pt and zh-cn with the same {holes}, every notice at most 240 characters, plain British words (" + (missing ?? keys.Length + " texts") + ")");

        string[] langs = { "en", "pt", "zh-cn", "de", "fr", "es", "ru", "nl", "tr" };
        string[] marks = { "Fixed: ", "Corrigido: ", "已修复：", "Behoben: ", "Corrigé : ", "Corregido: ", "Исправлено: ", "Opgelost: ", "Düzeltildi: " };
        string lineMissing = null, repeated = null, spaced = null, unmarked = null;
        for (int i = 0; i < langs.Length; i++)
        {
            Dictionary<string, string> words = LoadWords(System.IO.Path.Combine(folder, langs[i] + ".json"));
            string row = words != null ? FixRow(words, DealerFix) : string.Empty;
            bool found = words != null && WhatsNewHas(words, DealerFix) && row.Contains(DealerFix);
            lineMissing = found ? lineMissing : lineMissing ?? langs[i];
            if (!found)
            {
                continue;
            }
            int first = row.IndexOf(DealerFix, StringComparison.Ordinal);
            repeated = row.IndexOf(DealerFix, first + 1, StringComparison.Ordinal) >= 0 && Regex.Matches(row, Regex.Escape(marks[i]) + "[^.]*" + DealerFix).Count > 1 ? repeated ?? langs[i] : repeated;
            spaced = row.Contains("  ") ? spaced ?? langs[i] : spaced;
            unmarked = Regex.IsMatch(row, Regex.Escape(marks[i]) + "[^.]*" + DealerFix) ? unmarked : unmarked ?? langs[i];
        }
        Check(lineMissing == null && unmarked == null, "dealer: What's new has the fix line, with its fix mark, in all nine languages" + (lineMissing != null ? " (not in " + lineMissing + ")" : unmarked != null ? " (no mark in " + unmarked + ")" : string.Empty));
        Check(repeated == null && spaced == null, "dealer: the fix line stands once in each row, and no row has a double space" + (repeated != null ? " (twice in " + repeated + ")" : spaced != null ? " (double space in " + spaced + ")" : string.Empty));
    }
}
