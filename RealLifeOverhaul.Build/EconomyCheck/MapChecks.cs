using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using RealLifeOverhaul;
using RealLifeOverhaul.NewBuildings;
using RealLifeOverhaul.Uptown;

// A building the construction game finishes registers with the city map's filters the moment it stands, and leaves them when it is knocked down
// (Scripts/NewBuildings: MapRules, its CityList, MadeGame.Map.cs, MadeGame.City.cs, NewBuildingsDirector.Retire; Scripts/Core: GameDlc.Join and
// GameDlc.Leave; Scripts/Uptown: LifeDirector.Stand.cs, the first load in LifeDirector.cs, the change of use in LifeDirector.Extend.cs): which rows of the
// map light a building, what a registration does and in what order, what leaving does, the city's list grown and shortened once for each address, then
// over the sources the one rule at every place a building stands, changes its type or goes, nothing per frame, nothing sent, and the text.
internal static partial class Program
{
    private static void MapFilterChecks()
    {
        MapRowChecks();
        MapStepChecks();
        MapListChecks();
        MapSourceChecks();
        MapSearchChecks();
        MapOwnedSourceChecks();
        MapTextChecks();
    }

    private const string MapWarehouse = "ba:buildingtype_warehouse", MapOffice = "ba:buildingtype_office", MapEmpty = "ba:businesstype_empty";

    private static MapOn MapOnRows(bool byYou, params string[] types)
    {
        return new MapOn { ByYou = byYou, Types = new List<string>(types), Businesses = new List<string>() };
    }

    private static void MapRowChecks()
    {
        // The owner's warehouse at 2 Maple Street: the player's own (a deed), not to let, empty.
        var owned = new MapBuilding { Listed = true, Type = MapWarehouse, Owned = true, Business = MapEmpty };
        Check(MapRules.Rows(owned, MapOnRows(true)) == MapRow.ByYou, "map rows: a building the player owns is lit by Rented by you, with its deed and no tenant");
        Check(MapRules.Rows(owned, MapOnRows(false, MapWarehouse)) == MapRow.OwnType && MapRules.Rows(owned, MapOnRows(false, MapOffice)) == MapRow.None,
            "map rows: an empty warehouse the player owns and has not put up to let is lit by the Warehouse row (the owner's decision), and not by the Office row");
        Check(MapRules.Rows(owned, MapOnRows(true, MapWarehouse)) == (MapRow.ByYou | MapRow.OwnType) && MapRules.Rows(owned, MapOnRows(false)) == MapRow.None,
            "map rows: with both rows on it is lit by both, and with neither it is not lit");
        var rented = new MapBuilding { Listed = true, Type = MapWarehouse, RentedByPlayer = true, Business = MapEmpty };
        Check(MapRules.Rows(rented, MapOnRows(true)) == MapRow.ByYou && MapRules.Rows(rented, MapOnRows(false, MapWarehouse)) == MapRow.OwnType, "map rows: a building the player rents is lit by Rented by you, and by its type row too");
        var toLet = new MapBuilding { Listed = true, Type = MapWarehouse, AvailableForRent = true, Business = MapEmpty };
        Check(MapRules.Rows(toLet, MapOnRows(false, MapWarehouse)) == MapRow.ForRent && MapRules.Rows(toLet, MapOnRows(true)) == MapRow.None && MapRules.Rows(toLet, MapOnRows(false, MapOffice)) == MapRow.None,
            "map rows: a warehouse to let is lit by the Warehouse row, not by Rented by you and not by the Office row");
        // To let stays as the game has it: the type row lights it, once, whoever holds it.
        var ownToLet = new MapBuilding { Listed = true, Type = MapWarehouse, Owned = true, AvailableForRent = true, Business = MapEmpty };
        Check(MapRules.Rows(ownToLet, MapOnRows(false, MapWarehouse)) == MapRow.ForRent, "map rows: a warehouse the player owns and has put up to let is lit by the Warehouse row as to let, as before, not twice");
        // "Hide closed" hides an owned building that is shut, as the game hides one; a home or a building to let stays.
        var closed = new MapOn { Types = new List<string> { MapWarehouse, "ba:buildingtype_residential" }, Businesses = new List<string>(), HideClosed = true };
        var home = new MapBuilding { Listed = true, Type = "ba:buildingtype_residential", Owned = true, Business = MapEmpty };
        Check(MapRules.Rows(owned, closed) == MapRow.None && MapRules.Rows(new MapBuilding { Listed = true, Type = MapWarehouse, Owned = true, Open = true, Business = MapEmpty }, closed) == MapRow.OwnType
              && MapRules.Rows(home, closed) == MapRow.OwnType && MapRules.Rows(ownToLet, closed) == MapRow.ForRent,
            "map rows: with Hide closed on, an owned empty warehouse is not lit by its type row, an open one and a home are");
        // A building the player neither owns nor rents is lit by its type row only while to let, as before.
        Check(MapRules.Rows(new MapBuilding { Listed = true, Type = MapWarehouse, Business = MapEmpty }, MapOnRows(true, MapWarehouse)) == MapRow.None, "map rows: a warehouse that is nobody's and not to let is lit by no row");
        // A change of use moves it to the new type's row: the filters read the record's type at each look.
        toLet.Type = MapOffice;
        Check(MapRules.Rows(toLet, MapOnRows(false, MapWarehouse)) == MapRow.None && MapRules.Rows(toLet, MapOnRows(false, MapOffice)) == MapRow.ForRent,
            "map rows: a building changed from a warehouse to an office is lit by the Office row and no longer by the Warehouse row");
        // A business row and the buildings for sale.
        var shop = new MapBuilding { Listed = true, Type = "ba:buildingtype_retail", Business = "ba:businesstype_bakery" };
        var on = new MapOn { Types = new List<string>(), Businesses = new List<string> { "ba:businesstype_bakery" } };
        Check(MapRules.Rows(shop, on) == MapRow.Business && MapRules.Rows(new MapBuilding { Listed = true, Type = shop.Type, Business = MapEmpty }, on) == MapRow.None, "map rows: a business row lights the building with that business");
        var sale = new MapBuilding { Listed = true, Type = MapWarehouse, OnSale = true, Business = MapEmpty };
        on = new MapOn { OnSale = true, Types = new List<string>(), Businesses = new List<string>() };
        Check(MapRules.Rows(sale, on) == MapRow.OnSale && MapRules.Rows(new MapBuilding { Listed = true, Type = MapWarehouse, OnSale = true, Owned = true, Business = MapEmpty }, on) == MapRow.None,
            "map rows: a building for sale is lit by the for-sale row, unless the player already owns it");
        // The old knock-down left the controller in the city's list: the filters walk that list, so it was lit by every row that fits it.
        var gone = toLet;
        gone.Listed = false;
        Check(MapRules.Rows(gone, MapOnRows(true, MapOffice)) == MapRow.None && MapRules.Rows(owned, MapOnRows(true, MapWarehouse)) != MapRow.None
              && MapRules.Rows(new MapBuilding { Listed = false, Type = MapWarehouse, Owned = true, Business = MapEmpty }, MapOnRows(true, MapWarehouse)) == MapRow.None,
            "map rows: a building out of the city's list is lit by no row at all, whatever it was: the walk never reaches it");
    }

    private static void MapStepChecks()
    {
        MapStep owned = MapRules.Register(false, false, true, false);
        Check(owned == (MapStep.Marker | MapStep.Announce), "map steps: a building the player owns has its marker made (permanent, as a load makes it) and the game's event raised, the filters left to the map's next opening");
        Check(MapRules.Register(false, true, false, false) == (MapStep.Marker | MapStep.Announce), "map steps: a building the player rents is the same");
        Check(MapRules.Register(false, false, false, false) == MapStep.Announce, "map steps: a building of the city's, to let or not, has the event alone: its marker is made when a row lights it");
        Check(MapRules.Register(true, false, false, false) == (MapStep.Marker | MapStep.Announce), "map steps: a building that has a marker has it set again from its registration");
        Check(MapRules.Register(false, false, true, true) == (MapStep.Marker | MapStep.Announce | MapStep.Apply) && MapRules.Register(false, false, false, true) == (MapStep.Announce | MapStep.Apply),
            "map steps: with the map open the filters are applied again, as the game does after a rent and a purchase; with it shut they are not");
        Check(MapRules.Leave(true, true) == (MapStep.Delist | MapStep.Unmark | MapStep.Search) && MapRules.Leave(true, false) == (MapStep.Delist | MapStep.Search) && MapRules.Leave(false, true) == (MapStep.Unmark | MapStep.Search),
            "map steps: a building knocked down leaves the city's list, loses its marker and the search list is made again, each only if there was something to take away");
        Check(MapRules.Leave(false, false) == MapStep.None, "map steps: a building that was never listed and has no marker has nothing to leave");
        // Nothing twice at the load: a building stood in play is marked set up, so the load that stands it again registers it no more.
        var f = new FinishedBuilding { Lot = "uptown_1_04_0004", Size = "ba:buildingsize_h", Version = 1, Type = MapWarehouse, Owner = FinishOwner.Player, Price = 1669767, Day = 45 };
        bool dueBefore = StandRules.FirstLoadDue(f);
        StandRules.SetUp(f);
        Check(dueBefore && !StandRules.FirstLoadDue(f), "map steps: a building registered as it stood is marked set up, so the next load, which registers only a building due its first load, does not register it again");
    }

    private sealed class MapCtl
    {
        public string Address;
        public MapCtl(string address) { Address = address; }
    }

    private static void MapListChecks()
    {
        var a = new MapCtl("2 Maple Street"); var b = new MapCtl("4 Maple Street"); var c = new MapCtl("6 Maple Street");
        MapCtl[] city = { a, b, c };
        MapCtl made = new MapCtl("8 Maple Street");
        MapCtl[] joined = CityList.With(city, made, each => each.Address == made.Address);
        Check(joined.Length == 4 && joined[3] == made && joined[0] == a && !ReferenceEquals(joined, city) && city.Length == 3, "map list: a building that stands joins the end of the city's list in a new array, the game's readers keeping the one they hold");
        MapCtl[] twice = CityList.With(joined, made, each => each.Address == made.Address);
        Check(twice.Length == 4 && Array.IndexOf(twice, made) == 3, "map list: joined again it is still one entry, so the filters light it once");
        var after = new MapCtl("4 Maple Street");   // a building made at the same address after the first was knocked down there
        MapCtl[] replaced = CityList.With(joined, after, each => each.Address == after.Address);
        Check(replaced.Length == 4 && Array.IndexOf(replaced, b) < 0 && Array.IndexOf(replaced, after) == 3, "map list: a building made where one was knocked down takes its place: one entry for the address");
        // The load builds the list again from the scene: one controller for each address, the same list a stand in play leaves.
        MapCtl[] load = { a, b, c, made };
        bool same = joined.Length == load.Length;
        for (int i = 0; i < load.Length; i++)
        {
            same &= Array.IndexOf(joined, load[i]) >= 0;
        }
        Check(same, "map list: a building stood in play leaves the list the next load makes: the same buildings, none twice");
        MapCtl[] without = CityList.Without(joined, made, out bool removed);
        Check(removed && without.Length == 3 && Array.IndexOf(without, made) < 0 && joined.Length == 4 && without[0] == a && without[2] == c, "map list: a building knocked down leaves the list, in a new array");
        MapCtl[] absent = CityList.Without(without, made, out bool removedAgain);
        Check(!removedAgain && ReferenceEquals(absent, without), "map list: one that is not in the list leaves nothing and hands the same array back, so the closest-building job is not made again for nothing");
        Check(CityList.With<MapCtl>(null, made, null) == null && CityList.Without<MapCtl>(null, made, out bool none) == null && !none, "map list: no list, nothing to do");
    }

    private static void MapSourceChecks()
    {
        string map = ModSource(@"NewBuildings\MadeGame.Map.cs"), rules = ModSource(@"NewBuildings\MapRules.cs"), city = ModSource(@"NewBuildings\MadeGame.City.cs"), builder = ModSource(@"NewBuildings\NewBuildingsDirector.cs");
        string stand = ModSource(@"Uptown\LifeDirector.Stand.cs"), director = ModSource(@"Uptown\LifeDirector.cs"), extend = ModSource(@"Uptown\LifeDirector.Extend.cs"), dlc = ModSource(@"Core\GameDlc.cs");
        Check(map.Length > 0 && rules.Length > 0 && city.Length > 0 && builder.Length > 0 && stand.Length > 0 && director.Length > 0 && extend.Length > 0 && dlc.Length > 0, "map sources: the rules, the game's part, the stand, the load, the change of use and the city's list are there");
        // Every building the construction game finishes is stood by the one call, which registers it; a rival's or the city's development and a redevelopment's new build go through it too.
        string standOne = MethodText(stand, "private static bool StandOne(");
        int settle = standOne.IndexOf("LifeGame.Settle(made, f.Owner, f.Price, SaveGameManager.Current)", StringComparison.Ordinal), mapped = standOne.IndexOf("Mapped(made, \"stood\")", StringComparison.Ordinal);
        Check(settle > 0 && mapped > settle && standOne.Contains("listed && Mapped("), "map sources: a building stood in play is registered with the map's filters after its deed or its letting is written, once it is in the city's list");
        Check(ModSource(@"Wiring\Wiring.ConstructionLinks.cs").Contains("DistrictBuildings.StandNow(lot, out _)"), "map sources: every finished building the construction game hands over is stood by that one call");
        // A building that cannot stand in play (a session, a black screen to wait for) is registered at its first load by the same call, once.
        string first = MethodText(director, "private static bool FirstLife(");
        int due = first.IndexOf("if (!StandRules.FirstLoadDue(f))", StringComparison.Ordinal), setUp = first.IndexOf("StandRules.SetUp(f);", StringComparison.Ordinal), load = first.IndexOf("Mapped(made, \"set up at its first load\")", StringComparison.Ordinal);
        Check(due > 0 && setUp > due && load > setUp, "map sources: a building set up at its first load is registered by the same call, only when its first load is due, so the load registers it once");
        string convert = MethodText(extend, "public static bool Convert(");
        Check(convert.Contains("made.Building.BuildingType = type;") && convert.IndexOf("Mapped(made, \"changed in use\")", StringComparison.Ordinal) > convert.IndexOf("made.Building.BuildingType = type;", StringComparison.Ordinal),
            "map sources: a change of use is registered after the record's type changes, so the map reads the new type");
        string retire = MethodText(builder, "internal void Retire(");
        int leave = retire.IndexOf("MadeGame.Leave(old.Building);", StringComparison.Ordinal), leaveCity = retire.IndexOf("MadeGame.LeaveCity(old, out string mapWhy)", StringComparison.Ordinal);
        Check(leave > 0 && leaveCity > leave && ModSource(@"Uptown\LifeDirector.Cleared.cs").Contains("NewBuildingsDirector.Instance?.Retire(made);"),
            "map sources: a building knocked down leaves the city's list and the map's filters in the same call that lets its address go");
        // What registering does, in the rules' order, with the game's own calls.
        string register = MethodText(map, "public static bool Register(");
        Check(register.Contains("MapRules.Register(c.poi != null, reg.RentedByPlayer, reg.BuildingOwnedByPlayer, CityMap.IsOpen)") && register.Contains("c.UpdatePoi();")
              && register.Contains("GlobalEvents.onBuildingRegistrationChange?.Invoke(made.Building.Address);") && register.Contains("Refilter();") && register.Contains("if (!InCityList(made, out _))"),
            "map sources: registering sets the marker as the load does, raises the game's event for a registration that changed, and applies the filters while the map is open, for a building in the city's list");
        Check(map.Contains("MapPlaces.Refilter(uis.mapFilters);") && !map.Contains(".ApplyFilters("), "map sources: the filters are applied again by the mod's one place for it, which keeps to the open map and a city that is there");
        string leaving = MethodText(map, "private static bool LeaveCityNow(");
        Check(map.Contains("return LeaveCityNow(old, out why);") && leaving.Contains("MapRules.Leave(listed, c.poi != null)") && leaving.Contains("GameDlc.Leave(city, c);") && leaving.Contains("lookup.Remove(address);") && leaving.Contains("ResetClosest();")
              && leaving.Contains("Unmark(city, c);") && leaving.Contains("RemakeSearch();") && leaving.Contains("c.SetHighlight(false);") && leaving.Contains("(lookup == null || GameContract.ClosestReset.Method == null)"),
            "map sources: leaving takes the controller out of the list and the lookup by address, makes the closest-building job again, puts its outline and marker out and makes the search list again, touching nothing where the game's pieces are missing");
        string unmark = MethodText(map, "private static void Unmark(");
        Check(unmark.Contains("hidden.RemoveAll(each => each == poi);") && unmark.IndexOf("RemoveAll", StringComparison.Ordinal) < unmark.IndexOf("MapPlaces.Drop(poi);", StringComparison.Ordinal)
              && !unmark.Contains("Destroy(") && ModSource(@"Core\GameContract.cs").Contains("\"_permanentPoisHidden\""),
            "map sources: a marker is taken out of the filters' list of the ones they hid (a shut map shows those again) before the mod's one way of taking a marker off the map destroys it");
        Check(dlc.Contains("CityList.With(was, made, each => each.building != null && each.building.Address == address)") && dlc.Contains("CityList.Without(was, gone, out bool removed)")
              && city.Contains("GameDlc.Join(city, c, address);"), "map sources: the city's list is grown and shortened by the one rule the checks hold, in GameDlc, the only reader of the game's list");
        // Nothing per frame, nothing sent, no Harmony, no search of the scene.
        Check(!Regex.IsMatch(map + rules, @"void Update\(|void LateUpdate\(|void FixedUpdate\(|FindObjectsOfType|FindObjectsByType|FindObjectOfType|using System\.Linq|\.Where\(|\.Select\(|Harmony"),
            "map sources: nothing per frame, no search of the scene, no LINQ, no Harmony");
        Check(!Regex.IsMatch(map + rules, @"GoingPublic\.Send|Network|Rpc|SharedCity"), "map sources: nothing is sent: the map is this machine's own");
        Check(!Regex.IsMatch(rules, @"using (UnityEngine|Buildings|Entities|Helpers)\b|\bRandom\.|DateTime|TickCount|\bTime\."), "map sources: the rules are game-free");
        Check(ModSource(@"Core\GameContract.cs").Contains("public static readonly GameMember MapHidden = Optional(Field(OwnBuildings, Game, \"CityMapFilters\", \"_permanentPoisHidden\""), "map sources: the one private field of the game's the leaving needs is an optional line of the contract");
    }

    private static void MapSearchChecks()
    {
        string line = MapRules.SearchLine("Rosa's Bakery", "2 Maple Street", "Retail", out string word);
        Check(line == "<b><noparse>Rosa's Bakery</noparse></b><br><noparse>2 Maple Street</noparse>" && word == "Rosa's Bakery", "map search: a business is listed as the game lists it, its name over its address, and found by the name");
        string empty = MapRules.SearchLine(null, "4 Maple Street", "Warehouse", out string emptyWord);
        Check(empty == "<b><noparse>4 Maple Street</noparse></b><br><noparse>Warehouse</noparse>" && emptyWord == "4 Maple Street", "map search: an empty building is listed by its address over its type, and found by the address");
        string blank = MapRules.SearchLine("  ", "6 Maple Street", null, out string blankWord);
        Check(blank == "<b><noparse>6 Maple Street</noparse></b>" && blankWord == "6 Maple Street", "map search: a blank business name counts as none, and a building with no type name is listed by its address alone");
    }

    private static void MapOwnedSourceChecks()
    {
        string places = ModSource(@"Core\MapPlaces.cs"), owned = ModSource(@"Core\MapPlaces.Owned.cs"), driver = ModSource(@"Core\MapPlaces.Driver.cs"), names = ModSource(@"Core\SpecialNames.cs"), contract = ModSource(@"Core\GameContract.cs");
        Check(owned.Length > 0 && places.Length > 0 && names.Length > 0, "map owned sources: the owned lighting, the map places, the search are there");
        string light = MethodText(places, "private static void Light(");
        int over = light.IndexOf("OwnedDue();", StringComparison.Ordinal), pass = light.IndexOf("LightOwned();", StringComparison.Ordinal), loop = light.IndexOf("for (int i = 0; i < Lights.Count; i++)", StringComparison.Ordinal);
        Check(light.Contains("_gameLooking && !looking") && over > 0 && pass > over && loop > pass && light.Contains("if (_ownedDue && !looking)"),
            "map owned sources: the owner's buildings are lit once the game's look at every building is over, never while it runs, and only when a look ran or the map opened");
        string open = MethodText(places, "private static void OnMap(");
        Check(open.Contains("OwnedDue();"), "map owned sources: opening the map has the owner's buildings lit after the game's look");
        string walk = MethodText(owned, "private static void LightOwnedNow(");
        Check(walk.Contains("MapRules.Rows(building, on)") && walk.Contains("MapRow.OwnType") && walk.Contains("GameDlc.Open(city)") && walk.Contains("RentedByPlayer") && !Regex.IsMatch(walk, @"\.AvailableForRent\s*="),
            "map owned sources: which building is lit under its type row is MapRules' decision alone, from the city's list, and no rent flag is written");
        Check(!Regex.IsMatch(owned, @"void Update\(|void LateUpdate\(|FindObjectsOfType|FindObjectsByType|using System\.Linq|\.Where\(|\.Select\(|Harmony|GoingPublic|Network"), "map owned sources: nothing per frame, no scene search, no LINQ, no Harmony, nothing sent");
        Check(driver.Contains("|| _mapOpen;") && contract.Contains("Optional(Field(MapPlaces, Game, \"CityMapFilters\", \"_visibleBuildingTypes\"") && contract.Contains("Optional(Field(MapPlaces, Game, \"CityMapFilters\", \"_hideClosed\""),
            "map owned sources: the driver runs while the map is open, and the two private rows read are optional lines of the contract");
        string search = MethodText(names, "private void Search(");
        Check(search.Contains("MapRules.SearchLine(name, address,") && !search.Substring(search.IndexOf("FoundAs(building)", StringComparison.Ordinal), search.IndexOf("MapRules.SearchLine(", StringComparison.Ordinal) - search.IndexOf("FoundAs(building)", StringComparison.Ordinal)).Contains("continue;") && ModSource(@"Core\SpecialBuildings.cs").Contains("internal static bool Searching => true;"),
            "map search sources: every building of the city's list gets a line, named or not, by the one rule, and the mod always makes the search list");
        Check(names.Contains("EventHold.Keep(ref GlobalEvents.onBuildingRegistrationChange, _onRegistration, ref _registrationSeen);") && names.Contains("SearchSoon()") && ModSource(@"NewBuildings\MadeGame.Map.cs").Contains("SpecialNames.SearchAgain();"),
            "map search sources: the list is made again a frame after the game makes its own anew, while the map is open");
    }

    private static void MapTextChecks()
    {
        Dictionary<string, string>[] words = LanguageWords(out string[] codes);
        var fix = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "en", "registers with the city map's filters the moment it stands" }, { "pt", "se registra nos filtros do mapa da cidade no momento em que fica de pé" }, { "zh-cn", "就会登记到城市地图的筛选里" },
            { "de", "bei den Filtern der Stadtkarte angemeldet" }, { "fr", "s'inscrit désormais dans les filtres de la carte de la ville" }, { "es", "se registra en los filtros del mapa de la ciudad" },
            { "ru", "регистрируется в фильтрах карты города" }, { "nl", "aangemeld bij de filters van de stadskaart" }, { "tr", "şehir haritasının filtrelerine kaydoluyor" }
        };
        var owns = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "en", "also shows under its own type row on the city map" }, { "pt", "também aparece na linha do seu tipo no mapa da cidade" }, { "zh-cn", "也会显示在城市地图上它自己类型的那一行" }, { "de", "auch unter der Zeile seines Typs" }, { "fr", "apparaît aussi dans la ligne de son type sur la carte de la ville" }, { "es", "aparece también en la fila de su tipo en el mapa de la ciudad" }, { "ru", "и в строке своего типа" }, { "nl", "ook onder de rij van zijn type" }, { "tr", "kendi türünün satırında da görünüyor" }
        };
        bool fixes = true, plain = true;
        for (int l = 0; l < words.Length; l++)
        {
            if (words[l] == null || !fix.TryGetValue(codes[l], out string phrase))
            {
                continue;
            }
            int count = 0;
            foreach (KeyValuePair<string, string> row in words[l])
            {
                if (row.Key.StartsWith("reallife_changes_", StringComparison.Ordinal) && !row.Key.EndsWith("_title", StringComparison.Ordinal))
                {
                    count += Regex.Matches(row.Value, Regex.Escape(phrase)).Count;
                }
            }
            fixes &= count == 1;
            if (owns.TryGetValue(codes[l], out string phrase2))
            {
                int count2 = 0;
                foreach (KeyValuePair<string, string> row in words[l])
                {
                    if (row.Key.StartsWith("reallife_changes_", StringComparison.Ordinal) && !row.Key.EndsWith("_title", StringComparison.Ordinal))
                    {
                        count2 += Regex.Matches(row.Value, Regex.Escape(phrase2)).Count;
                    }
                }
                fixes &= count2 == 1;
            }
        }
        // The English line, wherever the release has put it (the next row, then a version's row): no exclamation mark and no dash.
        if (words.Length > 0 && words[0] != null)
        {
            foreach (KeyValuePair<string, string> row in words[0])
            {
                int at = row.Key.StartsWith("reallife_changes_", StringComparison.Ordinal) && !row.Key.EndsWith("_title", StringComparison.Ordinal)
                    ? row.Value.IndexOf("registers with the city map's filters", StringComparison.Ordinal) : -1;
                if (at >= 0)
                {
                    int from = row.Value.LastIndexOf("Fixed: ", at, StringComparison.Ordinal);
                    int end = row.Value.IndexOf("Fixed: ", at, StringComparison.Ordinal);
                    string mine = row.Value.Substring(Math.Max(0, from), (end < 0 ? row.Value.Length : end) - Math.Max(0, from));
                    plain = from >= 0 && !mine.Contains("!") && !mine.Contains("—") && !mine.Contains("–") && mine.IndexOf(" - ", StringComparison.Ordinal) < 0;
                }
            }
        }
        Check(fixes, "map texts: the fix line is in What's new once, in all nine languages (in any row, so the release's version row holds it too)");
        Check(plain, "map texts: the English line has no exclamation mark and no dash");
    }
}
