using System;
using System.Collections.Generic;
using System.IO;
using RealLifeOverhaul.DistrictSave;
using RealLifeOverhaul.Uptown;

// Uptown planned again as Manhattan is built (the owner, 24 Oct 2026: "Like Manhattan", "Keep the open lots, build half the
// squares"), and a save of the first plan retired cleanly: the district save lets go what it keeps for Uptown's streets when
// the save's plan is an earlier one (its businesses would stand at addresses that are now other buildings, of other sizes),
// Uptown's own line carries the plan's generation from the first load on, its finished buildings and opening marks start
// afresh, and the construction game gives back land on a lot of the first plan; the district's buildings may stand side by
// side. The lots themselves are NavCheck's (UptownCheck.cs); where the buildings stand on them is UptownPlaceChecks'.
internal static partial class Program
{
    private static void UptownDenseChecks()
    {
        UptownDenseRetire();
        UptownDenseLine();
        UptownDenseSources();
    }

    private static void UptownDenseRetire()
    {
        StoredRecord Of(params string[] streets)
        {
            var r = new StoredRecord(SaveKind.Registration, 0, null);
            foreach (string s in streets)
            {
                r.Addresses.Add(new PlaceKey(s, 1));
            }
            return r;
        }
        var records = new List<StoredRecord>
        {
            Of("reallife:street_uptown_mapleavenue"), Of("reallife:street_provinglane"), Of("reallife:street_provinglane", "reallife:street_uptown_sixth"), Of(), Of("ba:street_firstavenue")
        };
        var items = new List<object> { "a", "b", "c", "d", "e" };
        int gone = DistrictSaveStore.Retire(records, items, LayoutStreets.Prefix);
        Check(gone == 2 && records.Count == 3 && items.Count == 3 && (string)items[0] == "b" && (string)items[1] == "d" && (string)items[2] == "e"
              && DistrictSaveStore.Retire(records, items, LayoutStreets.Prefix) == 0 && DistrictSaveStore.Retire(null, items, "x") == 0 && DistrictSaveStore.Retire(records, items, null) == 0,
            "uptown dense: the district save lets go every kept record naming one of Uptown's streets, its item with it, the others kept in their order");
    }

    private static void UptownDenseLine()
    {
        var fresh = new LifeState { Opened = true };
        string line = LifeStore.Write(fresh);
        LifeState back = LifeStore.Read(line, out int _);
        LifeState first = LifeStore.Read("v=1^f:^o:1", out int _);
        var data = new Dictionary<string, string> { { LifeStore.Key, line } };
        Check(line.Contains("^g:" + LayoutRules.PlanGeneration) && back != null && back.Plan == LayoutRules.PlanGeneration && first != null && first.Plan == 1 && first.Opened
              && LifeStore.PlanOf(null) == 1 && LifeStore.PlanOf(new Dictionary<string, string>()) == 1 && LifeStore.PlanOf(data) == LayoutRules.PlanGeneration
              && LayoutRules.PlanGeneration == 3 && new LifeState().Plan == LayoutRules.PlanGeneration,
            "uptown dense: the district's line says its plan's generation; a line without one, or no line, is of the first plan");
        Check(UptownLots.OfEarlierPlan("uptown_2_07_05") && !UptownLots.OfEarlierPlan(UptownLots.All[0].Id) && !UptownLots.OfEarlierPlan("uptown_place_fuel"),
            "uptown dense: a lot of the first plan is told apart from this plan's lots and places");
    }

    private static void UptownDenseSources()
    {
        string scripts = System.IO.Path.GetFullPath(System.IO.Path.Combine(LocalesFolder(), "..", "Scripts"));
        string Read(string file)
        {
            string path = System.IO.Path.Combine(scripts, file);
            return File.Exists(path) ? File.ReadAllText(path).Replace("\r\n", "\n") : string.Empty;
        }
        string life = Read("Uptown/LifeDirector.cs"), save = Read("DistrictSave/DistrictSaveDirector.cs"), retire = Read("DistrictSave/DistrictSaveDirector.Retire.cs");
        string construction = Read("Construction/ConstructionDirector.cs"), wiring = Read("Wiring/Wiring.ConstructionLinks.cs"), ground = Read("NewBuildings/CopyGame.Ground.cs");
        string early = MemberText(life, "private static void Early()"), awake = MemberText(life, "private void Awake()"), state = MemberText(life, "private static void ReadState()");
        Check(early.IndexOf("RetireEarlierPlan(SaveGameManager.Current)", StringComparison.Ordinal) > early.IndexOf("Begin();", StringComparison.Ordinal)
              && awake.Contains("RetireEarlierPlan(SaveGameManager.Current)") && awake.IndexOf("RetireEarlierPlan(", StringComparison.Ordinal) < awake.IndexOf("ReadState();", StringComparison.Ordinal)
              && MemberText(life, "private static void RetireEarlierPlan(GameInstance save)").Contains("DistrictSaveDirector.Retire(save, LayoutStreets.Prefix,")
              && state.Contains("_state.Plan < LayoutRules.PlanGeneration") && state.Contains("_state = new LifeState();") && state.TrimEnd().EndsWith("WriteState();   // the line says the plan's generation from this load on, whatever else happens\n        }", StringComparison.Ordinal),
            "uptown dense: Uptown retires a save's records of an earlier plan before they go back (its early step, or a load without one), starts its own line afresh and writes its plan at once");
        Check(MemberText(save, "private static void Pass(GameInstance save, PlaceSet places, bool city)").Contains("Retired(save);") && retire.Contains("DistrictSaveStore.Retire(KeptRecords, KeptItems, _retirePrefix)")
              && retire.Contains("!DistrictSaveRules.IsModId(streetPrefix)"),
            "uptown dense: the district save lets them go at the pass over what it keeps, for that save alone, and never for one of the game's streets");
        Check(MemberText(construction, "private void Load()").Contains("Retire(state);") && MemberText(construction, "private void Retire(BuildState state)").Contains("ConstructionGame.Give(back, \"reallife_transaction_bld_land_back\"")
              && wiring.Contains("ConstructionBridges.LotRetired = UptownLots.OfEarlierPlan;") && wiring.Contains("ConstructionBridges.LotRetired = null;"),
            "uptown dense: the construction game gives back land on a lot of the first plan at its price and lets go its developments and open tenders there");
        Check(ground.Contains("const float In = -NewBuildingsRules.Touch;") && ground.Contains("&& !OfMade(hit)") && MemberText(ground, "private static bool OfMade(Collider hit)").Contains("MadeBuildings.Of(c) != null"),
            "uptown dense: the district's buildings stand side by side: two made footprints sharing an edge do not meet, and a made neighbour's walls are never something standing on a plot");
    }
}
