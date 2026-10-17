using System;
using System.Collections.Generic;
using System.IO;
using RealLifeOverhaul;
using RealLifeOverhaul.Careers;

// Hudson City University's permanent building (Scripts/Careers/Study/CampusRules, Scripts/Core/SpecialNameBook): the fixed
// choice and why it is sound by the game's own data, the reservation that keeps every feature and every market off it,
// the older game's rule (free: it moves there once; taken: it waits, then moves for good), the fallback that always
// stands (a free office, else the door of Manhattan Business School) with its map row, the notice told once, the
// Guide's fact and What's new. And, read as text, that the game's side uses these rules.
internal static partial class Program
{
    private static void CampusHome()
    {
        // ---------------------------------------------------------------- the fixed choice
        Check(CampusRules.IsHome("ba:street_secondavenue", 26) && !CampusRules.IsHome("ba:street_secondavenue", 25) && !CampusRules.IsHome("ba:street_fourthavenue", 26)
              && !CampusRules.IsHome(null, 26),
            "university home: the permanent building is 26 2nd Avenue, one address, the same in every game (no city, no seed, no save in the rule)");
        Check(CampusRules.HomeAddress(null) == "26 2nd Avenue" && CampusRules.HomeAddress("2nd Avenue") == "26 2nd Avenue" && CampusRules.HomeAddress("Second Ave") == "26 Second Ave",
            "university home: its address as the game writes one, from the game's street name or the mod's where the game's streets are not loaded");
        Check(CampusRules.HomeIsAsChosen("ba:buildingtype_office", "ba:buildingsize_c", 2, false)
              && !CampusRules.HomeIsAsChosen("ba:buildingtype_office", "ba:buildingsize_c", 1, false)
              && !CampusRules.HomeIsAsChosen("ba:buildingtype_retail", "ba:buildingsize_c", 2, false)
              && !CampusRules.HomeIsAsChosen("ba:buildingtype_office", "ba:buildingsize_k", 1, false)
              && !CampusRules.HomeIsAsChosen("ba:buildingtype_office", "ba:buildingsize_c", 2, true),
            "university home: the building found must be the one chosen, an office of size C version 2 that is no special service");
        HomeLayouts();

        // ---------------------------------------------------------------- the reservation
        var book = new SpecialNameBook();
        bool uniOn = true;
        Check(book.Reserve(CampusRules.BookId, CampusRules.HomeStreet, CampusRules.HomeNumber, () => uniOn), "university home: the address is reserved for the university");
        Check(book.Holds(CampusRules.HomeStreet, CampusRules.HomeNumber) && book.HolderOf(CampusRules.HomeStreet, CampusRules.HomeNumber) == CampusRules.BookId
              && !book.Holds(CampusRules.HomeStreet, 25) && book.HolderOf(CampusRules.HomeStreet, 25) == null,
            "university home: before the university has moved in, every feature that chooses sees it held (Holds, HolderOf), and only it");
        Check(!book.Set("police_3", CampusRules.HomeStreet, CampusRules.HomeNumber, SpecialKind.Police, "reallife_law_precinct_midtown")
              && !book.Set("techhq_tower_0", CampusRules.HomeStreet, CampusRules.HomeNumber, SpecialKind.Other, "Harbourgate Tower", literal: true)
              && book.Of("police_3") == null && book.Count == 0,
            "university home: no other feature is put in the book there (a police station, a tech tower)");
        Check(!book.Reserve("hotel_1", CampusRules.HomeStreet, CampusRules.HomeNumber) && book.HolderOf(CampusRules.HomeStreet, CampusRules.HomeNumber) == CampusRules.BookId,
            "university home: another feature cannot reserve the same address");
        // The largest free office building, as the police stations, the tech towers, City Utilities and the old rule choose: never the home.
        string[] streets = { "ba:street_seventhstreet", CampusRules.HomeStreet, "ba:street_firstavenue" };
        int[] numbers = { 17, CampusRules.HomeNumber, 22 };
        int[] sizes = { 1000, 99999, 4859 };
        int pick = SpecialNameBook.PickLargest(3, null, i => sizes[i], i => streets[i], i => numbers[i], book.Holds);
        Check(pick == 2, "university home: a feature taking the largest office building passes over the reserved home, however large");
        Check(book.Set(CampusRules.BookId, CampusRules.HomeStreet, CampusRules.HomeNumber, SpecialKind.Campus, "reallife_study_campus_university", "reallife_campus_card_type", () => uniOn),
            "university home: the university itself is put in the book at its reserved address");
        SpecialEntry home = book.Of(CampusRules.BookId);
        Check(book.Refuses(CampusRules.HomeStreet, CampusRules.HomeNumber, false, false) && SpecialNameBook.RefusesEntry(home, false, false),
            "university home: once in, it is off the market: no offer to rent, buy or take it over, out of the real estate list (Core/ClaimedOffers)");
        Check(!book.Refuses(CampusRules.HomeStreet, CampusRules.HomeNumber, true, false) && !book.Refuses(CampusRules.HomeStreet, CampusRules.HomeNumber, false, true),
            "university home: a player who already rents or owns it keeps it (never a player's building)");
        book.Clear();
        Check(book.Holds(CampusRules.HomeStreet, CampusRules.HomeNumber) && book.Count == 0, "university home: the reservation outlives the city's unloading (the book's entries go, the fixed address stays held)");
        uniOn = false;
        Check(!book.Holds(CampusRules.HomeStreet, CampusRules.HomeNumber) && book.HolderOf(CampusRules.HomeStreet, CampusRules.HomeNumber) == null
              && book.Set("police_3", CampusRules.HomeStreet, CampusRules.HomeNumber, SpecialKind.Police, "reallife_law_precinct_midtown"),
            "university home: with the university switched off the building is the game's again, for any feature or player");
        uniOn = true;
        Check(book.HolderOf(CampusRules.HomeStreet, CampusRules.HomeNumber) == CampusRules.BookId && book.FiledId(CampusRules.HomeStreet, CampusRules.HomeNumber) == "police_3",
            "university home: switched on again, the address is the university's first (a feature put there meanwhile moves off at its next choice; the university waits until it has)");
        book.Remove("police_3");
        Check(book.Unreserve(CampusRules.BookId) && !book.Holds(CampusRules.HomeStreet, CampusRules.HomeNumber) && !book.Unreserve(CampusRules.BookId),
            "university home: the reservation is let go of when the careers part is unwired");

        // ---------------------------------------------------------------- the older game's rule
        Check(CampusRules.HomeBlockOf(true, "ba:businesstype_empty", false, false, null, false) == HomeBlock.None
              && CampusRules.HomeBlockOf(true, null, false, false, "", false) == HomeBlock.None,
            "university home: free (no business, nobody's, nothing left inside): the university takes it");
        Check(CampusRules.HomeBlockOf(true, "ba:businesstype_empty", true, false, null, false) == HomeBlock.Players
              && CampusRules.HomeBlockOf(true, "ba:businesstype_lawfirm", false, true, null, false) == HomeBlock.Players,
            "university home: rented or owned by a player (another player's in a session): never taken");
        Check(CampusRules.HomeBlockOf(true, "ba:businesstype_lawfirm", false, false, null, false) == HomeBlock.Business
              && CampusRules.HomeBlockOf(true, "ba:businesstype_empty", false, false, "rival_1", false) == HomeBlock.Business
              && CampusRules.HomeBlockOf(true, "brandsystem:businesstype_franchise", false, false, null, false) == HomeBlock.Business,
            "university home: a business in it (the game's, a rival's, or another mod's own business type) keeps the university out");
        Check(CampusRules.HomeBlockOf(true, "ba:businesstype_empty", false, false, null, true) == HomeBlock.Left && CampusRules.HomeBlockOf(false, null, false, false, null, false) == HomeBlock.Missing,
            "university home: a layout left inside, or the building missing from the city, keeps it out too");
        Check(CampusRules.HomeBlockOf(true, "ba:businesstype_empty", false, false, null, false, true) == HomeBlock.Held
              && CampusRules.AwayKey(HomeBlock.Held) == "reallife_campus_away_held"
              && CampusRules.HomeBlockOf(true, "ba:businesstype_empty", true, false, null, false, true) == HomeBlock.Players,
            "university home: switched on after a police station took the building, the reason is that another service is still there (never \"cannot be used\"); a player's comes first");
        Check(SpecialNameBook.GivesWay(true, null, "police_") && SpecialNameBook.GivesWay(false, CampusRules.BookId, "police_")
              && !SpecialNameBook.GivesWay(false, "police_2", "police_") && !SpecialNameBook.GivesWay(false, null, "police_"),
            "university home: a police station the save kept gives way when a player has the building or another feature holds it (the university), and keeps it otherwise");
        float schoolReach = CampusRules.DoorReachAt(CampusPlace.School, 4.3f, 1.7f);
        Check(schoolReach <= 2.4f + 0.0001f && CampusRules.Apart(4.3f, schoolReach, 1.7f) && !CampusRules.Apart(4.3f, CampusRules.DoorReach, 1.7f)
              && CampusRules.DoorReachAt(CampusPlace.Home, 4.3f, 1.7f) == CampusRules.DoorReach && CampusRules.DoorReachAt(CampusPlace.Office, 4.3f, 1.7f) == CampusRules.DoorReach,
            "university home: at the school's door the class panel's reach stops short of the driving school's desk (2.4 m and 1.7 m, 4.3 m apart, 0.2 m between); elsewhere 3 m");
        Check(CampusRules.SaysMove(true, true) && !CampusRules.SaysMove(true, false) && !CampusRules.SaysMove(false, true),
            "university home: a move is logged only when the save had an address (a new game's first choice is no move)");
        Check(CampusRules.MovesHome(false, true, false, false), "university home: standing elsewhere, it moves there once the building is free");
        Check(!CampusRules.MovesHome(true, true, false, false), "university home: at home it never moves again (once, for good)");
        Check(!CampusRules.MovesHome(false, false, false, false), "university home: while a player or anyone has it, the university stays where it is");
        Check(!CampusRules.MovesHome(false, true, true, false) && !CampusRules.MovesHome(false, true, false, true),
            "university home: never while the player is inside the old building or a class or exam is being sat (it moves at the next look after)");

        // ---------------------------------------------------------------- where it is: always somewhere
        Check(CampusRules.Choose(true, true, true) == CampusPlace.Home && CampusRules.Choose(true, false, false) == CampusPlace.Home,
            "university place: its permanent building whenever that is free");
        Check(CampusRules.Choose(false, true, true) == CampusPlace.Office, "university place: meanwhile the largest free office building");
        Check(CampusRules.Choose(false, false, true) == CampusPlace.School, "university place: with every office building taken, the door of Manhattan Business School");
        Check(CampusRules.Choose(false, false, false) == CampusPlace.None, "university place: nowhere only in a city without the game's own school");
        Check(CampusRules.OnMap(true, true, CampusPlace.Home) && CampusRules.OnMap(true, true, CampusPlace.Office) && CampusRules.OnMap(true, true, CampusPlace.School)
              && !CampusRules.OnMap(true, true, CampusPlace.None) && !CampusRules.OnMap(false, true, CampusPlace.Home) && !CampusRules.OnMap(true, false, CampusPlace.Home),
            "university place: the map row and marker are there wherever it stands, its fallback places too, and only then");
        PlayersSave();

        // ---------------------------------------------------------------- told once
        string atOffice = CampusRules.AwayMark(CampusPlace.Office, "ba:street_fourthavenue", 35, HomeBlock.Players);
        Check(CampusRules.AwayMark(CampusPlace.Home, CampusRules.HomeStreet, CampusRules.HomeNumber, HomeBlock.None) == null && CampusRules.AwayMark(CampusPlace.None, null, 0, HomeBlock.Missing) == null
              && atOffice != null,
            "university notice: nothing to tell at home or nowhere; away, a mark of where and why");
        Check(CampusRules.TellAway(atOffice, null) && !CampusRules.TellAway(atOffice, atOffice)
              && CampusRules.TellAway(CampusRules.AwayMark(CampusPlace.School, "ba:street_fourthavenue", 7, HomeBlock.Players), atOffice)
              && CampusRules.TellAway(CampusRules.AwayMark(CampusPlace.Office, "ba:street_fourthavenue", 35, HomeBlock.Business), atOffice)
              && !CampusRules.TellAway(null, atOffice),
            "university notice: told once per save, again only when where or why changes; never at home");
        Check(CampusRules.AwayKey(HomeBlock.None) == null && CampusRules.AwayNoteKey(CampusPlace.Home) == null && CampusRules.AwayNoteKey(CampusPlace.None) == null,
            "university notice: no away words at home");
        CampusHomeTexts();
        CampusHomeSources();
    }

    // A player's save: an older game where every office building has a business (the game's, rivals' and
    // other mods'), the university had no building and no map row. After the load: the university and its row exist.
    private static void PlayersSave()
    {
        string[] types = { "ba:businesstype_lawfirm", "smartrent:businesstype_office", "ba:businesstype_webdevelopmentagency", "brandsystem:businesstype_franchise" };
        string[] owners = { "rival_1", null, "rival_2", null };
        int[] sqm = { 102502, 58989, 15657, 5971 };
        bool[] players = { false, true, false, false };
        // The old rule: the largest office building with no business in it, nobody's.
        int office = SpecialNameBook.PickLargest(types.Length, i => !players[i] && CampusRules.NoBusinessIn(types[i], players[i], false, owners[i]), i => sqm[i], i => "s" + i, i => i, null);
        HomeBlock block = CampusRules.HomeBlockOf(true, types[3], false, false, owners[3], false);   // (26 2nd Avenue, with another mod's business in it)
        CampusPlace before = office >= 0 ? CampusPlace.Office : CampusPlace.None;   // 0.26.0: no free office, no building, no row
        CampusPlace after = CampusRules.Choose(block == HomeBlock.None, office >= 0, true);
        Check(office < 0 && before == CampusPlace.None && !CampusRules.OnMap(false, false, before),
            "the player's save: every office building has a business, so 0.26.0 found the university no building and gave it no map row");
        Check(block == HomeBlock.Business && after == CampusPlace.School && CampusRules.OnMap(true, true, after),
            "the player's save: on the next load the university stands at the door of Manhattan Business School, and its map row and marker are there");
        string told = null;
        string mark = CampusRules.AwayMark(after, "ba:street_fourthavenue", 7, block);
        Check(CampusRules.TellAway(mark, told), "the player's save: the player is told once why it is there");
        told = mark;
        Check(!CampusRules.TellAway(CampusRules.AwayMark(after, "ba:street_fourthavenue", 7, block), told), "the player's save: ...and not again on the next load");
        // The other mod's business closes: the permanent building is free.
        HomeBlock freed = CampusRules.HomeBlockOf(true, "ba:businesstype_empty", false, false, null, false);
        Check(CampusRules.MovesHome(false, freed == HomeBlock.None, false, false) && CampusRules.Choose(freed == HomeBlock.None, false, true) == CampusPlace.Home
              && CampusRules.OnMap(true, true, CampusPlace.Home) && CampusRules.AwayMark(CampusPlace.Home, CampusRules.HomeStreet, CampusRules.HomeNumber, freed) == null,
            "the player's save: once the building is free the university moves into it for good, its row follows, and nothing more is told of being away");
    }

    // The game's own data (StreamingAssets/BusinessLayouts): no office business of the game's has a layout of the home's
    // size, so neither the city's competition nor a rival can open a business there. A layout of size C2 is allowed only
    // for the shops the game's AI opens (retail buildings) and three named businesses that are no AI's.
    private static void HomeLayouts()
    {
        string root = @"G:\Steam\steamapps\common\Big Ambitions\Big Ambitions_Data\StreamingAssets\BusinessLayouts";
        if (!Directory.Exists(root))
        {
            Check(false, "university home: the game's business layouts are at " + root);
            return;
        }
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Bookstore", "ClothingStore", "CoffeeShop", "ElectronicsStore", "Empty", "FastFoodRestaurant", "Florist", "FruitAndVegetableStore", "GiftShop", "Gym",
            "Hairdresser", "JewelryStore", "LiquorStore", "Nightclub", "Supermarket",   // shops: their C2 layouts are for retail buildings
            "Clinic", "MarketingAgency", "RecruitmentAgency"                             // the game's own named businesses (IndustryCityClinic, HellsKitchenMarketingAgencyInternet, LowerManhattanRecruitmentAgency)
        };
        var unexpected = new List<string>();
        int officeTypes = 0;
        foreach (string dir in Directory.GetDirectories(root))
        {
            string type = System.IO.Path.GetFileName(dir);
            if (Directory.Exists(System.IO.Path.Combine(dir, "C2")) && !allowed.Contains(type))
            {
                unexpected.Add(type);
            }
        }
        foreach (string office in new[] { "LawFirm", "TravelAgency", "EventPlanningAgency", "WebDevelopmentAgency", "GraphicDesigner" })
        {
            officeTypes += Directory.Exists(System.IO.Path.Combine(root, office)) ? 1 : 0;
            if (Directory.Exists(System.IO.Path.Combine(root, office, "C2")))
            {
                unexpected.Add(office);
            }
        }
        Check(unexpected.Count == 0 && officeTypes == 5,
            "university home: by the game's own layouts no office business of the game's fits a C2 building, so no business of the city's or a rival's can open in 26 2nd Avenue"
            + (unexpected.Count > 0 ? ": now a C2 layout for " + string.Join(", ", unexpected) + " (choose again)" : officeTypes != 5 ? ": the game's office business types are not where they were" : string.Empty));
    }

    private static void CampusHomeTexts()
    {
        string folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Locales"));
        Dictionary<string, string> en = LoadWords(System.IO.Path.Combine(folder, "en.json")), pt = LoadWords(System.IO.Path.Combine(folder, "pt.json")), zh = LoadWords(System.IO.Path.Combine(folder, "zh-cn.json"));
        if (en == null || pt == null || zh == null)
        {
            Check(false, "university home: the texts are at " + folder);
            return;
        }
        var words = new (string Key, string[] Names)[]
        {
            ("reallife_campus_away_players", new[] { "{home}" }), ("reallife_campus_away_held", new[] { "{home}" }), ("reallife_campus_away_business", new[] { "{home}" }), ("reallife_campus_away_missing", new[] { "{home}" }),
            ("reallife_campus_note_away_office", new[] { "{why}", "{address}" }), ("reallife_campus_note_away_school", new[] { "{why}", "{address}" }),
            ("reallife_campus_note_home", new[] { "{address}" }), ("reallife_guide_careers_degrees", new[] { "{degrees_address}" })
        };
        var missing = new List<string>();
        foreach ((string key, string[] names) in words)
        {
            foreach (Dictionary<string, string> lang in new[] { en, pt, zh })
            {
                if (!lang.TryGetValue(key, out string text) || string.IsNullOrWhiteSpace(text) || Array.Exists(names, n => text.IndexOf(n, StringComparison.Ordinal) < 0))
                {
                    missing.Add(key);
                }
            }
        }
        Check(missing.Count == 0, "university home: every new text, and the Guide's degrees page with its address fact, is in en, pt and zh-cn with its {names}" + (missing.Count > 0 ? ": " + string.Join(", ", missing) : string.Empty));
        foreach (HomeBlock block in new[] { HomeBlock.Players, HomeBlock.Business, HomeBlock.Missing, HomeBlock.Held })
        {
            foreach (CampusPlace place in new[] { CampusPlace.Office, CampusPlace.School })
            {
                if (!en.TryGetValue(CampusRules.AwayKey(block), out string why) || !en.TryGetValue(CampusRules.AwayNoteKey(place), out string note))
                {
                    continue;
                }
                string told = note.Replace("{why}", why.Replace("{home}", "26 2nd Avenue")).Replace("{address}", "102 Fourth Avenue");
                Check(told.Length <= 240, "university home: the notice of where it is fits a toast (" + block + " at " + place + ": " + told.Length + " characters)");
            }
        }
        Check(en.TryGetValue("reallife_campus_note_home", out string homeNote) && homeNote.Replace("{address}", "26 2nd Avenue").Length <= 240, "university home: the notice of the move home fits a toast");
        Check(CampusHomeSaid(en, "Hudson City University has a permanent building of its own at 26 2nd Avenue"), "university home: What's new says so in en");
        Check(CampusHomeSaid(pt, "A Hudson City University tem um prédio permanente só seu na 26 2nd Avenue"), "university home: What's new says so in pt");
        Check(CampusHomeSaid(zh, "哈德逊城市大学在中城的 26 2nd Avenue 拥有自己的永久大楼"), "university home: What's new says so in zh-cn");
    }

    // Every reallife_changes_ row, anywhere in the row (a row joins its entries with spaces), never only the next row:
    // the release turns the next row into the version's own.
    private static bool CampusHomeSaid(Dictionary<string, string> texts, string words)
    {
        foreach (KeyValuePair<string, string> pair in texts)
        {
            if (pair.Key.StartsWith("reallife_changes_", StringComparison.Ordinal) && !pair.Key.EndsWith("_title", StringComparison.Ordinal)
                && pair.Value != null && pair.Value.IndexOf(words, StringComparison.Ordinal) >= 0)
            {
                return true;
            }
        }
        return false;
    }

    // The game's side, read as text: it uses these rules and holds the building as they say.
    private static void CampusHomeSources()
    {
        string scripts = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        string Read(string file) => File.Exists(System.IO.Path.Combine(scripts, file)) ? File.ReadAllText(System.IO.Path.Combine(scripts, file)) : string.Empty;
        string links = Read(@"Wiring\CareersLinks.cs"), building = Read(@"Careers\CampusBuilding.cs"), map = Read(@"World\MapLinks.cs"), precincts = Read(@"Law\Precincts.cs"),
               court = Read(@"Justice\Courthouse.cs"), brands = Read(@"Economy\Brands\BrandsGame.cs"), interior = Read(@"Careers\CampusInterior.cs"), guide = Read(@"Careers\CareersGuide.cs"),
               floors = Read(@"Careers\CareersDirector.Floors.cs"), list = Read(@"Meta\FeatureList.cs");
        Check(links.Contains("SpecialBuildings.Reserve(CampusRules.BookId, CampusRules.HomeStreet, CampusRules.HomeNumber") && links.Contains("SpecialBuildings.Unreserve(CampusRules.BookId)"),
            "university home: the careers wiring reserves the building at start, before any feature chooses, and lets it go when unwired");
        Check(building.Contains("CampusRules.Choose(") && building.Contains("CampusRules.HomeBlockOf(") && building.Contains("CampusRules.MovesHome(") && building.Contains("RivalsHelper.IsBuildingSuitableFor"),
            "university home: the building picks by CampusRules (home, office, school), moves home by its rule, and checks the game's rivals once a city");
        Check(building.Contains("Dev.Always(_logger, \"Careers: the university is "), "university home: where the university is, and why, is one log line whatever the diagnostics switch says");
        Check(CampusCount(map, "Careers.CampusRules.OnMap(uni.Usable, uni.Building != null, uni.Place)") == 2,
            "university home: the map's row and its marker both follow CampusRules.OnMap, so the row is there in the fallback places too");
        Check(court.Contains("SpecialBuildings.ReservedFor(") && brands.Contains("SpecialBuildings.Holds(building.StreetName, building.StreetNumber)"),
            "university home: the courthouse and the brands' franchise sites pass over it too");
        Check(interior.Contains("campus.Place == CampusPlace.School"), "university home: at the game's school no floors are made (the school inside stays the game's)");
        Check(guide.Contains("Fact(\"degrees_address\", CareersGame.HomeAddress)"), "university home: the Guide's {degrees_address} is filled from the code");
        Check(floors.Contains("CampusRules.TellAway(") && list.Contains("CareersStore.CampusToldKey"), "university home: told once through the save's mark, and the mark is listed among the feature's save keys");
        Check(building.Contains("filed != null && filed != CampusRules.BookId)") && !building.Contains("&& (filed == null || filed == CampusRules.BookId)"),
            "university home: another feature filed in the building is a reason of its own, not a building missing from the city");
        Check(precincts.Contains("SpecialNameBook.GivesWay(SpecialBuildings.Players(found), SpecialBuildings.HolderOf(found), \"police_\")") && precincts.Contains("|| (!had && gaveWay.Contains(district.Key))"),
            "university home: a district whose saved station gave way (to the university or a player) has its new station named in the precincts-moved notice");
        string desks = Read(@"Licences\Driving\DrivingSchool.cs");
        float aside = CampusConst(desks, "Aside"), deskReach = CampusConst(desks, "Reach");
        Check(aside > 0f && deskReach > 0f && CampusRules.Apart(aside, CampusRules.DoorReachAt(CampusPlace.School, aside, deskReach), deskReach)
              && building.Contains("CampusRules.DoorReachAt(CampusPlace.School, LicenceDesks.Aside, LicenceDesks.Reach)") && building.Contains("Place == CampusPlace.School ? ReachAtSchool"),
            "university home: the driving school's own desk figures (" + aside + " m along, " + deskReach + " m reach) keep the school-door panels apart, and the building uses them");
        Check(floors.Contains("CampusRules.SaysMove(moved, _campusKept)"), "university home: the move line goes through SaysMove");
        Check(list.Contains("\"reallife_degrees_test_away\"") && Read(@"Core\RealLifeOverhaulMod.cs").Contains("AddButton(\"reallife_degrees_test_away\", () => Careers.CareersDirector.Instance?.TestAway())"),
            "university home: Options > Tests has the places meanwhile (a free office, the school's door, back home) without an older save");
    }

    // A float constant of a source ("internal const float Aside = 4.3f;"), or -1.
    private static float CampusConst(string source, string name)
    {
        var m = System.Text.RegularExpressions.Regex.Match(source, "const float " + name + " = ([0-9.]+)f;");
        return m.Success ? float.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : -1f;
    }

    private static int CampusCount(string text, string what)
    {
        int n = 0;
        for (int at = text.IndexOf(what, StringComparison.Ordinal); at >= 0; at = text.IndexOf(what, at + what.Length, StringComparison.Ordinal))
        {
            n++;
        }
        return n;
    }
}
