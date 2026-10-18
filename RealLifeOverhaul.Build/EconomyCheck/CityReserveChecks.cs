using System;
using System.Collections.Generic;
using RealLifeOverhaul;
using RealLifeOverhaul.Careers;

// The city's buildings the mod keeps fixed (Scripts/Core/CityReserves, with Core/SpecialNameBook): the two largest office
// buildings kept for the player (no feature of the mod ever picks them, the player may still rent or buy them), City
// Utilities at 13 7th Street (a residential building, none of the game's own, never a player's) with its move told once
// in an older save and nothing of the mod left on 17 7th Street, and the courthouse where it always was, on the tax
// office. And, read as text, that every picker of the mod asks the book, and the Guide and What's new say so.
internal static partial class Program
{
    private static void CityReserveChecks()
    {
        // ---------------------------------------------------------------- the two kept for the player
        // The city's office buildings that may be rented or bought, largest first, from the game's data (Buildings bundle,
        // no special service): the two the mod keeps are the two largest.
        var offices = new List<(string Street, int Number, int Sqm)>
        {
            ("ba:street_seventhstreet", 17, 102502), ("ba:street_broadwaystreet", 12, 58989), ("ba:street_broadwaystreet", 1, 48567),
            ("ba:street_thirdavenue", 13, 42433), ("ba:street_firstavenue", 1, 39096), ("ba:street_secondavenue", 26, 5971), ("ba:street_thirdstreet", 43, 422)
        };
        CityReserves.TopTwo(offices.Count, null, i => offices[i].Sqm, i => offices[i].Street, i => offices[i].Number, out int first, out int second);
        Check(first == 0 && second == 1 && CityReserves.IsPlayerOffice(offices[first].Street, offices[first].Number) && CityReserves.IsPlayerOffice(offices[second].Street, offices[second].Number)
              && !CityReserves.IsPlayerOffice("ba:street_broadwaystreet", 1) && !CityReserves.IsPlayerOffice(CampusRules.HomeStreet, CampusRules.HomeNumber),
            "kept offices: the two kept for the player are the city's two largest office buildings, 17 7th Street and 12 Broadway, and only they");
        Check(CityReserves.PlayerOfficeAddress(0, null) == "17 7th Street" && CityReserves.PlayerOfficeAddress(1, null) == "12 Broadway",
            "kept offices: their addresses as the game writes them, for the Guide");

        var book = new SpecialNameBook();
        for (int i = 0; i < CityReserves.PlayerOfficeStreets.Length; i++)
        {
            book.Reserve(CityReserves.PlayerOfficeId + (i + 1), CityReserves.PlayerOfficeStreets[i], CityReserves.PlayerOfficeNumbers[i], null);
        }
        bool uniOn = true, utilOn = true;
        book.Reserve(CampusRules.BookId, CampusRules.HomeStreet, CampusRules.HomeNumber, () => uniOn);
        book.Reserve(CityReserves.UtilitiesId, CityReserves.UtilitiesStreet, CityReserves.UtilitiesNumber, () => utilOn);
        int pick = SpecialNameBook.PickLargest(offices.Count, null, i => offices[i].Sqm, i => offices[i].Street, i => offices[i].Number, book.Holds);
        Check(pick == 2, "kept offices: a feature taking the largest office (the university meanwhile, the courthouse's last fallback) takes the third, 1 Broadway");
        bool refused = false;
        foreach (string id in new[] { CityReserves.UtilitiesId, "courthouse", "police_2", "techhq_tower_0", CampusRules.BookId, "hotel_1" })
        {
            for (int i = 0; i < 2; i++)
            {
                refused |= book.Set(id, CityReserves.PlayerOfficeStreets[i], CityReserves.PlayerOfficeNumbers[i], SpecialKind.Civic, "reallife_util_desk_title");
            }
        }
        Check(!refused && book.Count == 0, "kept offices: no feature of the mod is put in the book there (City Utilities, the courthouse, a police station, a tech tower, the university, a hotel)");
        Check(book.HolderOf("ba:street_seventhstreet", 17) == CityReserves.PlayerOfficeId + "1" && book.Holds("ba:street_broadwaystreet", 12)
              && SpecialNameBook.GivesWay(false, book.HolderOf("ba:street_broadwaystreet", 12), "police_")
              && SpecialNameBook.GivesWay(false, book.HolderOf("ba:street_seventhstreet", 17), "techhq_"),
            "kept offices: every picker that asks who holds a building passes them over, and a police station or a tower the save had there gives way");
        Check(book.Holds("ba:street_seventhstreet", 17) && book.FiledId("ba:street_seventhstreet", 17) == null,
            "kept offices: reserved is not filed: no feature is in 17 7th Street, so its business (Barbosa Studios in a player's game) and its jobs are the game's");
        Check(!book.Refuses("ba:street_seventhstreet", 17, false, false) && !book.Refuses("ba:street_broadwaystreet", 12, false, false),
            "kept offices: they are not refused to the player: 17 7th Street is a plain office to rent or buy as the game has it");

        // ---------------------------------------------------------------- City Utilities at 13 7th Street
        Check(CityReserves.IsUtilities("ba:street_seventhstreet", 13) && !CityReserves.IsUtilities("ba:street_seventhstreet", 17) && !CityReserves.IsPlayerOffice(CityReserves.UtilitiesStreet, CityReserves.UtilitiesNumber)
              && CityReserves.UtilitiesAddress(null) == "13 7th Street" && offices.TrueForAll(o => !CityReserves.IsUtilities(o.Street, o.Number)),
            "utilities: 13 7th Street, the same in every game: no office building, not 17 7th Street, not one of the player's offices");
        Check(CityReserves.HostServes("ba:buildingtype_residential", false, false, false)
              && !CityReserves.HostServes("ba:buildingtype_office", false, false, false) && !CityReserves.HostServes("ba:buildingtype_retail", false, false, false)
              && !CityReserves.HostServes("ba:buildingtype_special", true, false, false) && !CityReserves.HostServes("ba:buildingtype_residential", true, false, false),
            "utilities: only an ordinary residential building serves: never an office or a shop, never one of the game's own buildings (a special service: the tax office, a bank, a school, the moving company)");
        Check(!CityReserves.HostServes("ba:buildingtype_residential", false, true, false) && !CityReserves.HostServes("ba:buildingtype_residential", false, false, true),
            "utilities: never a building a player rents or owns, never one another feature holds");
        Check(book.HolderOf(CityReserves.UtilitiesStreet, CityReserves.UtilitiesNumber) == CityReserves.UtilitiesId
              && !book.Set("hotel_1", CityReserves.UtilitiesStreet, CityReserves.UtilitiesNumber, SpecialKind.Hotel, "reallife_travel_hotel_midtown_grand")
              && book.Set(CityReserves.UtilitiesId, CityReserves.UtilitiesStreet, CityReserves.UtilitiesNumber, SpecialKind.Civic, "reallife_util_desk_title")
              && book.Refuses(CityReserves.UtilitiesStreet, CityReserves.UtilitiesNumber, false, false),
            "utilities: 13 7th Street is reserved for it from the start (no hotel or other feature there) and, once in, refused to the player");
        // Meanwhile: the smallest free residential building of Midtown, never the fixed one nor a held one.
        var homes = new List<(string Street, int Number, int Sqm)> { (CityReserves.UtilitiesStreet, CityReserves.UtilitiesNumber, 6980), ("ba:street_ninthstreet", 8, 2115), ("ba:street_sixthstreet", 2, 2215), ("ba:street_ninthstreet", 7, 2215) };
        int meanwhile = CityReserves.PickSmallest(homes.Count, null, i => homes[i].Sqm, i => homes[i].Street, i => homes[i].Number, (s, n) => book.Holds(s, n) || CityReserves.IsUtilities(s, n));
        Check(meanwhile == 1, "utilities: with 13 7th Street a player's home in an older game, it stands meanwhile at the smallest other free residential building of Midtown");
        utilOn = false;
        book.Remove(CityReserves.UtilitiesId);
        Check(!book.Holds(CityReserves.UtilitiesStreet, CityReserves.UtilitiesNumber), "utilities: switched off, 13 7th Street is the game's again");
        utilOn = true;
        book.Clear();
        Check(book.Holds("ba:street_seventhstreet", 17) && book.Holds(CityReserves.UtilitiesStreet, CityReserves.UtilitiesNumber), "kept offices: kept whatever the options, through the city's unloading");

        // ---------------------------------------------------------------- older saves
        string util = CityReserves.Mark(CityReserves.UtilitiesStreet, CityReserves.UtilitiesNumber);
        Check(CityReserves.TellMoved(null, util, true, CityReserves.OldUtilities) && CityReserves.TellMoved(CityReserves.OldUtilities, util, true, CityReserves.OldUtilities),
            "older saves: a save that had City Utilities at 17 7th Street is told once that it has moved to 13 7th Street");
        Check(!CityReserves.TellMoved(util, util, true, CityReserves.OldUtilities), "older saves: ...and never again once the save keeps the new place");
        Check(!CityReserves.TellMoved(null, util, false, CityReserves.OldUtilities), "older saves: a new game is told nothing");

        // ---------------------------------------------------------------- never a player's
        Check(SpecialNameBook.GivesWay(true, null, "police_") && !SpecialNameBook.MaySit(true, false) && !SpecialNameBook.MaySit(false, true),
            "player's buildings: a building any player rents or owns is never taken, and a feature on one moves off it");
        CityReserveTexts();
        CityReserveSources();
    }

    private static void CityReserveTexts()
    {
        string folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Locales"));
        Dictionary<string, string> en = LoadWords(System.IO.Path.Combine(folder, "en.json")), pt = LoadWords(System.IO.Path.Combine(folder, "pt.json")), zh = LoadWords(System.IO.Path.Combine(folder, "zh-cn.json"));
        if (en == null || pt == null || zh == null)
        {
            Check(false, "kept offices: the texts are at " + folder);
            return;
        }
        var missing = new List<string>();
        foreach ((string key, string[] names) in new[] { ("reallife_util_note_moved", new[] { "{address}" }),
                     ("reallife_guide_office_space", new[] { "{offices_kept_1}", "{offices_kept_2}", "{util_address}", "{offices_district}" }) })
        {
            foreach (Dictionary<string, string> lang in new[] { en, pt, zh })
            {
                if (!lang.TryGetValue(key, out string text) || Array.Exists(names, n => text.IndexOf(n, StringComparison.Ordinal) < 0))
                {
                    missing.Add(key);
                }
            }
        }
        Check(missing.Count == 0, "kept offices: the move notice and the Guide's office page with the two kept offices and City Utilities' address are in en, pt and zh-cn" + (missing.Count > 0 ? ": " + string.Join(", ", missing) : string.Empty));
        Check(!en.ContainsKey("reallife_justice_note_court_moved") && !en.ContainsKey("reallife_util_sign"), "courthouse: no notice of a move (it stays on the tax office), and no utilities sign text left unread");
        Check(CampusHomeSaid(en, "City Utilities moves from 17 7th Street to 13 7th Street"), "kept offices: What's new says so in en");
        Check(CampusHomeSaid(pt, "A City Utilities se muda da 17 7th Street para a 13 7th Street"), "kept offices: What's new says so in pt");
        Check(CampusHomeSaid(zh, "City Utilities 从 17 7th Street 搬到 13 7th Street"), "kept offices: What's new says so in zh-cn");
    }

    // Every picker of the mod asks the book (Holds, HolderOf or ReservedFor), so the reservations hold for all of them;
    // City Utilities picks by the residential rule; the courthouse is as it always was; nothing of the mod is left on 17 7th Street.
    private static void CityReserveSources()
    {
        string scripts = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        string Read(string file) => System.IO.File.Exists(System.IO.Path.Combine(scripts, file)) ? System.IO.File.ReadAllText(System.IO.Path.Combine(scripts, file)) : string.Empty;
        string links = Read(@"Wiring\OfficeLinks.cs"), utilLinks = Read(@"Wiring\UtilityLinks.cs"), court = Read(@"Justice\Courthouse.cs"), util = Read(@"Utilities\UtilitiesOffice.cs"),
               host = Read(@"Core\CivicHost.cs"), fit = Read(@"FitOut\FitOutDirector.cs");
        Check(links.Contains("SpecialBuildings.Reserve(CityReserves.PlayerOfficeId + (i + 1), CityReserves.PlayerOfficeStreets[i], CityReserves.PlayerOfficeNumbers[i], null)")
              && utilLinks.Contains("SpecialBuildings.Reserve(CityReserves.UtilitiesId, CityReserves.UtilitiesStreet, CityReserves.UtilitiesNumber"),
            "kept offices: the two offices and 13 7th Street are reserved at every city load before any feature chooses");
        var pickers = new (string File, string Asks)[]
        {
            (@"Core\CivicHost.cs", "SpecialBuildings.Holds(s, n)"),
            (@"Justice\Courthouse.cs", "SpecialBuildings.ReservedFor("),
            (@"Law\Precincts.cs", "SpecialBuildings.HolderOf(building)"),
            (@"Tech\Sites\Headquarters.cs", "SpecialBuildings.HolderOf(building)"),
            (@"Careers\CampusBuilding.cs", "SpecialBuildings.Holds)"),
            (@"Offices\Conversions.cs", "!SpecialBuildings.Holds(c)"),
            (@"Economy\Brands\BrandsGame.cs", "SpecialBuildings.Holds(building.StreetName, building.StreetNumber)"),
            (@"Travel\Hotels.cs", "SpecialBuildings.Holds(b.StreetName, b.StreetNumber)"),
        };
        var deaf = new List<string>();
        foreach ((string file, string asks) in pickers)
        {
            if (!Read(file).Contains(asks))
            {
                deaf.Add(file);
            }
        }
        Check(deaf.Count == 0, "kept offices: every picker of a building asks the book, so none takes the kept ones" + (deaf.Count > 0 ? ": not " + string.Join(", ", deaf) : string.Empty));
        Check(util.Contains("CivicHost.Pick(buildings, out bool fixedOne)") && host.Contains("CityReserves.HostServes(b.building.BuildingType, b.building.SpecialService != null, SpecialBuildings.Players(b), other)")
              && !util.Contains("buildingtype_office"),
            "utilities: it picks 13 7th Street (or meanwhile a residential building) by the rule above, never an office or a building of the game's");
        Check(util.Contains("PlaceMoves.Chosen(CityReserves.UtilitiesKey, UtilitiesStore.Key, CityReserves.OldUtilities") && !court.Contains("PlaceMoves") && !court.Contains("CityReserves"),
            "older saves: City Utilities tells its move once through the save; the courthouse does not move and keeps its old rule (the tax office first)");
        Check(court.Contains("case \"ba:businesstype_irs\": return 9;") && court.Contains("SpecialBuildings.ReservedFor("),
            "courthouse: on the tax office as before; only its fallback (a city with none) passes over the reserved buildings");
        // Nothing of the mod is left on 17 7th Street: the utilities office stands no sign, no furnished floor and no name there.
        Check(!util.Contains("RealLifeOverhaul Utilities Sign") && !fit.Contains("case \"utilities_office\":") && util.Contains("SpecialBuildings.Remove(CityReserves.UtilitiesId)"),
            "17 7th Street: no utilities sign, no furnished floor and no name of the mod's are made any more, and the name goes with the office");
        Check(fit.Contains("/// Nothing is saved.") && Read(@"Core\FrontSigns.cs").Contains("nothing of it saved"),
            "17 7th Street: what the mod stood or fitted there before (sign, lit name, furnished floor) was never saved, so an older save loads it as the game's own office");
        Check(court.Contains("SpecialBuildings.Players(building)") && host.Contains("SpecialBuildings.Players(b)"), "player's buildings: the courthouse and City Utilities never choose a player's building");
        // A bare reservation is not the mod being there: a career job at a business in a kept office (Barbosa Studios at
        // 17 7th Street) has the game's own desk, and only a building a feature is in counts (SpecialBuildings.Filed).
        string desk = Read(@"Careers\CareersDirector.Desk.cs"), civic = Read(@"Civic\CivicDirector.cs") + Read(@"Civic\CivicDirector.Street.cs");
        Check(!desk.Contains("SpecialBuildings.Holds(") && CampusCount(desk, "SpecialBuildings.Filed(j.Post.Street, j.Post.Number)") == 2
              && !civic.Contains("SpecialBuildings.Holds(") && civic.Contains("SpecialBuildings.Filed(building)"),
            "kept offices: a career's desk and the canvasser's door knock ask whether a feature is in a building, never whether one is only reserved");
        Check(Read(@"Travel\Hotels.cs").Contains("Func<bool> first = TravelBridges.UtilitiesPicked;") && utilLinks.Contains("TravelBridges.UtilitiesPicked = () =>"),
            "utilities: the hotels choose after City Utilities, so the two never race for a residential building on any machine");
    }
}
