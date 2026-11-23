using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using RealLifeOverhaul;
using RealLifeOverhaul.DistrictSave;
using RealLifeOverhaul.NewBuildings;

// The district's buildings findable at the load's first step (Scripts/NewBuildings/EarlyRecordsRules.cs, MadeGame.Early.cs, Uptown/LifeDirector.Records.cs): a player's log showed another mod
// (Retail Expansion) stopping its setup on "A renovated property is missing: reallife:street_uptown_uptownavenue:16" because its city part started before this mod's had declared or made
// Uptown's buildings. Their records and registrations are now put in the game's two lookups by address in the district save's step after a save is read (the main menu's mods unloading),
// before the city's scenes and before any mod's city part; the make behind the loading screen claims them, and takes back what it does not. Game-free for the rules and the ledger; the
// order of the mod's own steps, and that the load does the same work, are proved from the sources. What only the game can show is listed in the in-game tests.
internal static partial class Program
{
    private static void EarlyRecordChecks()
    {
        EarlyRuleChecks();
        EarlyLedgerChecks();
        EarlyOrderChecks();
        EarlyMakeChecks();
    }

    private static MadeSpec EarlySpec(string id, bool life = true, string service = null)
    {
        return new MadeSpec { Id = id, Street = MadeStreets.Prefix + "uptown_uptownavenue", Number = 16, Size = "ba:buildingsize_c", Version = 1, Type = "ba:buildingtype_retail", Life = life, Service = service };
    }

    private static void EarlyRuleChecks()
    {
        Check(EarlyRecordsRules.Wants(EarlySpec("uptown_1_02_0001")) && !EarlyRecordsRules.Wants(EarlySpec("a", false)) && !EarlyRecordsRules.Wants(EarlySpec("b", true, "BankService"))
              && !EarlyRecordsRules.Wants(EarlySpec("")) && !EarlyRecordsRules.Wants(null),
            "early records: an ordinary district building gets its record early; another feature's building, one with a service to copy, one with no id, and nothing, do not");
        MadeSpec s = EarlySpec("x");
        Check(EarlyRecordsRules.Fits(s, "ba:buildingsize_c", 1, "ba:buildingtype_retail") && !EarlyRecordsRules.Fits(s, "ba:buildingsize_d", 1, "ba:buildingtype_retail")
              && !EarlyRecordsRules.Fits(s, "ba:buildingsize_c", 2, "ba:buildingtype_retail") && !EarlyRecordsRules.Fits(s, "ba:buildingsize_c", 1, "ba:buildingtype_office") && !EarlyRecordsRules.Fits(null, "a", 1, "b"),
            "early records: a record made early fits its declaration only while the declaration names the structure it was made with");
        s.Size = "ba:buildingsize_f";
        Check(!EarlyRecordsRules.Fits(s, "ba:buildingsize_c", 1, "ba:buildingtype_retail"), "early records: a declaration that took another structure no longer fits the record made before");
    }

    private sealed class EarlyItem
    {
        public string Id;
    }

    private static void EarlyLedgerChecks()
    {
        var ledger = new EarlyLedger<EarlyItem>();
        var a = new EarlyItem { Id = "a" };
        var b = new EarlyItem { Id = "b" };
        var c = new EarlyItem { Id = "c" };
        Check(ledger.Add("a", a) == null && ledger.Add("b", b) == null && ledger.Add("c", c) == null && ledger.Count == 3 && ledger.Has("b") && ledger.Peek("c") == c && ledger.Peek("z") == null,
            "early records: the ledger holds each record under its declaration's id");
        Check(ledger.Add("b", new EarlyItem { Id = "b2" }) == b && ledger.Count == 3 && ledger.Peek("b").Id == "b2",
            "early records: a declaration made twice hands the first record back to be taken back, none is lost");
        Check(ledger.Add(null, a) == null && ledger.Add("", a) == null && ledger.Add("q", null) == null && ledger.Count == 3, "early records: nothing without an id or a record is held");
        Check(ledger.Take("a", out EarlyItem taken) && taken == a && ledger.Count == 2 && !ledger.Has("a") && !ledger.Take("a", out _) && !ledger.Take(null, out _),
            "early records: a make that claims its record takes it out, and a record cannot be claimed twice");
        var rest = new List<EarlyItem>();
        Check(ledger.Drain(rest) == 2 && rest.Count == 2 && rest[0].Id == "b2" && rest[1] == c && ledger.Count == 0 && ledger.Drain(rest) == 0,
            "early records: what no make claimed is handed back in the order it was added, once, and the ledger is empty after");
        int claimed = 0, handed = 0;
        var big = new EarlyLedger<EarlyItem>();
        for (int i = 0; i < 600; i++)
        {
            big.Add("lot" + i, new EarlyItem { Id = "lot" + i });
        }
        for (int i = 0; i < 600; i += 3)
        {
            claimed += big.Take("lot" + i, out _) ? 1 : 0;
        }
        handed = big.Drain(null);
        Check(claimed == 200 && handed == 400 && big.Count == 0, "early records: of 600 records a third claimed leaves 400 to be taken back, none counted twice (" + claimed + ", " + handed + ")");
    }

    // The order of the mod's own steps in a load: the game's (UI.Load.LoadingScreen, SaveGameManager) and the mod's, proved from the mod's own sources.
    private static void EarlyOrderChecks()
    {
        Check(EarlyRecordsRules.Chosen < EarlyRecordsRules.Registered && EarlyRecordsRules.Registered < EarlyRecordsRules.ScenesBuilt && EarlyRecordsRules.ScenesBuilt < EarlyRecordsRules.FirstCityPart
              && EarlyRecordsRules.FirstCityPart < EarlyRecordsRules.OwnCityPart && EarlyRecordsRules.OwnCityPart < EarlyRecordsRules.Made && EarlyRecordsRules.Made < EarlyRecordsRules.GameLoaded
              && EarlyRecordsRules.Steps.Length == EarlyRecordsRules.GameLoaded + 1,
            "early records: the records are put in the lookups at the step before the scenes are built and before every mod's city part, the buildings are made inside the mod's own city part");
        string save = ModSource(@"DistrictSave\DistrictSaveDirector.cs"), entry = ModSource(@"DistrictSave\DistrictSaveEntry.cs"), places = ModSource(@"DistrictSave\DistrictPlaces.cs");
        string early = MemberText(save, "public static void Early()");
        int step = early.IndexOf("DistrictPlaces.Early();", StringComparison.Ordinal), pass = early.IndexOf("Pass(save, places, false);", StringComparison.Ordinal);
        int recorded = early.IndexOf("DistrictPlaces.Recorded();", StringComparison.Ordinal), flush = early.IndexOf("Flush();", StringComparison.Ordinal);
        Check(step > 0 && pass > step && recorded > pass && flush > recorded,
            "early records: in the district save's step after a save is read, the records come after the early pass, so what the pass puts back is what it always was, and before anything is built");
        Check(entry.Contains("[ModEntryMainMenu]") && MemberText(entry, "public Task OnUnloadAsync()").Contains("DistrictSaveDirector.Early();"),
            "early records: the district save's step is the main menu's mods unloading, which the game runs before it builds the city's scenes and before any mod's city part");
        Check(MemberText(places, "public static void Recorded()").Contains("Recordeds[i]?.Invoke();") && MemberText(places, "public static void Add(string owner, Action<PlaceSet> fill, Action early = null, Action recorded = null)").Contains("Recordeds[at] = recorded;"),
            "early records: a feature may give a step to run after the early pass, and a fault in one is named, never thrown");
        var order = new List<string>();
        DistrictPlaces.Add("zz-early-check", set => { }, () => order.Add("early"), () => order.Add("recorded"));
        DistrictPlaces.Add("zz-early-check-broken", set => { }, null, () => throw new InvalidOperationException("its data is missing"));
        DistrictPlaces.Add("zz-early-check-late", set => { }, null, () => order.Add("recorded, a second feature"));
        DistrictPlaces.Early();
        order.Add("pass");
        DistrictPlaces.Recorded();
        bool named = false;
        foreach (string fault in DistrictPlaces.LastFaults)
        {
            named |= fault.Contains("zz-early-check-broken records (early)");
        }
        DistrictPlaces.Remove("zz-early-check");
        DistrictPlaces.Remove("zz-early-check-broken");
        DistrictPlaces.Remove("zz-early-check-late");
        Check(string.Join(",", order) == "early,pass,recorded,recorded, a second feature" && named,
            "early records: each feature's early step runs, then the pass, then each feature's records step in the order they registered; one that throws is named and the rest still run");
        string life = ModSource(@"Uptown\LifeDirector.cs"), records = ModSource(@"Uptown\LifeDirector.Records.cs");
        Check(life.Contains("DistrictPlaces.Add(PlacesOwner, Fill, Early, Recorded);"), "early records: Uptown gives the district save its records step");
        string recorded2 = MemberText(records, "private static void Recorded()");
        int declare = recorded2.IndexOf("Declare();", StringComparison.Ordinal), flag = recorded2.IndexOf("_declaredEarly = true;", StringComparison.Ordinal), put = recorded2.IndexOf("LifeGame.RecordsEarly(Declared, out int found)", StringComparison.Ordinal);
        Check(recorded2.Contains("if (!_known || save == null || !ReferenceEquals(save, _earlyFor))") && declare > 0 && flag > declare && put > flag && recorded2.Contains("ForgetDeclared();")
              && recorded2.Contains("catch (Exception e)"),
            "early records: the buildings are declared, then marked so the city's load does not declare them again, then their records are made; a fault leaves the city's load to declare as it always did");
        string awake = MemberText(life, "private void Awake()");
        Check(Regex.IsMatch(awake, @"if \(!_declaredEarly\)\s*\{[^}]*ReadState\(\);[^}]*Declare\(\);") && Occurs(life, "Declare();") == 1 && Occurs(records, "Declare();") == 1,
            "early records: the declarations are made once a load, at the first step or, where there was none (a load that did not pass the main menu), in the city's load as before");
        Check(MemberText(life, "private static void Begin()").Contains("_declaredEarly = false;") && MemberText(life, "private static void Begin()").Contains("LifeGame.RecordsBack(")
              && MemberText(life, "public static void Settle()").IndexOf("LifeGame.RecordsBack(", StringComparison.Ordinal) is int back && back > 0 && back < MemberText(life, "public static void Settle()").IndexOf("if (!_known)", StringComparison.Ordinal),
            "early records: a new load forgets the mark and takes back the last load's records, and the step that stands the district takes back any no make claimed, whether or not its director started");
        string mod = ModSource(@"Core\RealLifeOverhaulMod.cs");
        int prewarm = mod.IndexOf("ScenePrewarm.Begin()", StringComparison.Ordinal), settle = mod.IndexOf("Uptown.LifeDirector.Settle()", StringComparison.Ordinal);
        Check(prewarm > 0 && settle > prewarm && mod.IndexOf("DistrictSaveDirector.City(_root)", StringComparison.Ordinal) > settle,
            "early records: the mod's own city part still makes the buildings (the scene prewarm), then stands the district, then the district save's city step, in the order it did");
    }

    // The load does the same work: the record and registration are moved earlier, not made twice, and nothing the make needs a scene for is done early.
    private static void EarlyMakeChecks()
    {
        string made = ModSource(@"NewBuildings\MadeGame.Early.cs"), game = ModSource(@"NewBuildings\MadeGame.cs"), director = ModSource(@"NewBuildings\NewBuildingsDirector.cs");
        string register = MemberText(made, "public static int RegisterEarly(IList<MadeSpec> specs, out int found)");
        int drop = register.IndexOf("DropEarly(", StringComparison.Ordinal), wants = register.IndexOf("EarlyRecordsRules.Wants(spec)", StringComparison.Ordinal), refusal = register.IndexOf("NewBuildingsRules.Refusal(", StringComparison.Ordinal);
        int has = register.IndexOf("GameHas(", StringComparison.Ordinal), reg = register.IndexOf("MakeRegistration(spec, out bool fresh, false)", StringComparison.Ordinal), rec = register.IndexOf("MakeRecord(spec, 0, registration, fresh, out _)", StringComparison.Ordinal);
        int enter = register.IndexOf("EnterLookups(record, registration, out string why)", StringComparison.Ordinal);
        Check(drop > 0 && wants > drop && refusal > wants && has > refusal && reg > has && rec > reg && enter > rec,
            "early records: what an early record is made for is the make's own order: a declaration refused or already held by the game gets none, then its registration, its record, the two lookups");
        Check(register.Contains("NewBuildingsRules.Refusal(spec, StructureTakes, DistrictListed, true)") && !register.Contains("CityCheck") && !register.Contains("HomesLeftOut()")
              && MemberText(game, "public static bool DistrictListed(string district)").Contains("NeighborhoodHelper.Neighborhoods") && !MemberText(game, "public static bool DistrictListed(string district)").Contains("CityCheck"),
            "early records: at the first step the district is asked of the game's own list of districts, not of the city check, which still holds the last city's");
        Check(!register.Contains("ServiceListed(") && !register.Contains("BuildingRegistrations") && !register.Contains("Instantiate") && !register.Contains("CityManager") && !register.Contains("SceneFinds")
              && !register.Contains("FindObject") && !register.Contains("CopyGame."),
            "early records: nothing is done early that needs a scene, the city's list, a copy of the game's service or the save's list; those stay with the make");
        string lookups = MemberText(game, "public static bool EnterLookups(");
        Check(lookups.IndexOf("registrations[address] = registration;", StringComparison.Ordinal) is int r && r > 0 && lookups.IndexOf("buildings[address] = record;", StringComparison.Ordinal) > r && !lookups.Contains("ServiceListed(")
              && MemberText(game, "public static bool Enter(").Contains("EnterLookups(record, registration, out why)") && MemberText(game, "public static bool Enter(").Contains("ServiceListed(record, true);"),
            "early records: the two lookups, the registration first, are all that is entered early; the special services' list is entered with the make, as before");
        string make = MemberText(director, "private bool Make(MadeSpec spec, bool inPlay = false)");
        int take = make.IndexOf("MadeGame.TakeEarly(spec, designArea, out registration, out record, out fresh)", StringComparison.Ordinal), working = make.IndexOf("CopyGame.MakeWorking(", StringComparison.Ordinal);
        int made2 = make.IndexOf("MadeGame.MakeRegistration(spec, out fresh);", StringComparison.Ordinal), made3 = make.IndexOf("MadeGame.MakeRecord(spec, designArea, registration, fresh, out why);", StringComparison.Ordinal);
        Check(make.Contains("if (!inPlay && MadeGame.TakeEarly(") && take > 0 && made2 > take && made3 > made2 && working > made3 && make.Contains("MadeGame.Leave(record);") && make.Contains("MadeGame.Forsake(registration, fresh);"),
            "early records: the make claims the record made early, makes its own only where there is none (so a record is made once), and gives both back if the copy cannot be made");
        string claim = MemberText(made, "public static bool TakeEarly(MadeSpec spec, int designArea, out BuildingRegistration registration, out Building record, out bool fresh)");
        Check(claim.Contains("EarlyRecordsRules.Fits(spec, e.Size, e.Version, e.Type)") && claim.Contains("Withdraw(e);") && claim.Contains("e.Record.totalSqm = designArea;")
              && claim.Contains("SaveGameManager.Current?.BuildingRegistrations?.Add(e.Registration);") && claim.Contains("if (e.Fresh)"),
            "early records: a record that no longer fits is taken back, a design's floor area is set when the design is chosen, and a registration new at this load joins the save's list as it always did, when it stands");
        string all = MemberText(director, "private bool MakeAll()");
        Check(all.Contains("return MakeAllSpecs();") && all.Contains("finally") && all.Contains("MadeGame.DropEarly("), "early records: whatever no make claimed is taken back at the end of the make, even after a fault");
        Check(MemberText(game, "public static bool GameHas(string street, int number)").Contains("!IsEarly(there)") && MemberText(game, "private static void Drop()").Contains("DropEarly("),
            "early records: the game's own check for a building already at an address does not take the mod's early record for another mod's, and the city's end takes back any left");
        Check(MemberText(game, "public static int AddStreets()").Contains("there.streetLocalization = Named(street);") && register.Contains("AddStreets();"),
            "early records: the mod's streets are among the game's when the records are, their names read again when the city part runs");
        Check(!Regex.IsMatch(made, @"void (Update|LateUpdate|FixedUpdate)\(") && !Regex.IsMatch(made + ModSource(@"Uptown\LifeDirector.Records.cs"), @"\bSend\w*\(|Network|Rpc"),
            "early records: nothing runs a frame and nothing is sent, so a Going Public session is as before");
        Check(ModSource(@"Uptown\LifeDirector.Records.cs").Contains("Uptown check (records at the load's first step)") && ModSource(@"Uptown\LifeDirector.Records.cs").Contains("found == made ? \"PASS\" : \"FAIL\""),
            "early records: with Developer diagnostics on, the load says how many records the game's own look-ups give back, PASS or FAIL");
    }
}
