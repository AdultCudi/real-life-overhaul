using System;
using RealLifeOverhaul;
using RealLifeOverhaul.Careers;

// A player parked a rented scooter outside Hudson City University, sat a class and came out to no scooter (24 Nov 2026). The game takes a scooter
// away once it has been unseen for its type's minutes (AutoDestroyVehicle, every frame, the player's whereabouts not asked), a building is out of
// its sight, and a degree's class is four hours. Nothing in the mod moved it. Now the mod marks such a scooter seen once a second while the
// player is indoors (ScooterHold). Offline: run with --scooter-hold.
internal static partial class Program
{
    private static void ScooterHold()
    {
        const int Limit = 240;   // the figure the mod's earlier notes give the game's scooter (BikeMount); the rules take it as data

        // The longest class of the catalogue, the player arriving early: the stay that was lost.
        int longest = 0;
        foreach (CourseDef c in StudyCatalogue.All)
        {
            longest = Math.Max(longest, c.Hours * 60 + StudyCatalogue.ArriveEarlyMinutes);
        }
        Check(longest >= Limit, "scooter hold: a class with its early arrival (" + longest + " min) outlasts a scooter's " + Limit + " minutes, which is what the report was");
        Check(ScooterHoldRules.TakenDuring(Limit, longest, 1f, false), "scooter hold: without the hold the game takes a scooter parked outside a class of the longest kind");
        Check(!ScooterHoldRules.TakenDuring(Limit, longest, 1f, true), "scooter hold: with the hold the same stay leaves it");
        Check(!ScooterHoldRules.TakenDuring(Limit, 120f, 1f, false), "scooter hold: a two hour film is no trouble either way, the game's rule is not otherwise changed");
        foreach (float tick in new[] { 1f, 3f, 10f, 30f })
        {
            Check(!ScooterHoldRules.TakenDuring(Limit, 24f * 60f, tick, true), "scooter hold: a whole day indoors at " + tick + " game minutes a second leaves it");
        }
        Check(ScooterHoldRules.TakenDuring(Limit, 24f * 60f, Limit + 1f, true), "scooter hold: a clock put back less often than the limit is no hold (the model can fail)");

        // The game's own test, as AutoDestroyVehicle writes it.
        Check(ScooterHoldRules.GameTakes(240, true, 300f, 60f) && !ScooterHoldRules.GameTakes(240, true, 299f, 60f), "scooter hold: the game takes a vehicle at its limit and not a minute before");
        Check(!ScooterHoldRules.GameTakes(-1, true, 1e6f, 0f) && !ScooterHoldRules.GameTakes(240, false, 1e6f, 0f), "scooter hold: no limit, or nothing counting, and the game takes nothing");

        // Which vehicles the hold touches.
        Check(ScooterHoldRules.Holds(true, true, true, 240, true, true, false, false), "scooter hold: a rented scooter on the street, nobody on it, the player indoors, is held");
        Check(!ScooterHoldRules.Holds(false, true, true, 240, true, true, false, false), "scooter hold: with the option off nothing is held");
        Check(!ScooterHoldRules.Holds(true, false, true, 240, true, true, false, false), "scooter hold: out of doors the game's clock is the game's");
        Check(!ScooterHoldRules.Holds(true, true, false, 240, true, true, false, false), "scooter hold: a car, a van or a hand truck is not a scooter and is left to the game");
        Check(!ScooterHoldRules.Holds(true, true, true, -1, true, true, false, false), "scooter hold: a vehicle the game never takes away has nothing to hold");
        Check(!ScooterHoldRules.Holds(true, true, true, 240, false, true, false, false), "scooter hold: one nothing is counting for (in sight, or never seen) is left as it is");
        Check(!ScooterHoldRules.Holds(true, true, true, 240, true, false, false, false), "scooter hold: a scooter filed at a building's address is the game's to bring back with that building");
        Check(!ScooterHoldRules.Holds(true, true, true, 240, true, true, true, false), "scooter hold: the one the player is on is never touched");
        Check(!ScooterHoldRules.Holds(true, true, true, 240, true, true, false, true), "scooter hold: the mod's own bicycles and motorbikes keep their own rules");

        // Over the sources.
        string hold = ModSource(@"Garage\ScooterHold.cs");
        Check(hold.Contains("save.VehicleInstances") && !hold.Contains("AllPlayerVehicles") && !hold.Contains("FindObjects"), "scooter hold source: only the save's own vehicles are read, never the street's list, so no other player's vehicle is touched");
        Check(hold.Contains("lastSeen.SetCurrentTime()") && !hold.Contains("new Timestamp") && !hold.Contains("TimeHelper.Now()"), "scooter hold source: the clock is written in place, nothing allocated");
        Check(hold.Contains("BuildingManager.IsInsideBuilding") && hold.Contains("GarageState.Bike(") && hold.Contains("save.ActiveVehicleId") && hold.Contains("autoDestroyAfterMinutes"),
            "scooter hold source: asked only indoors, leaves the mod's bikes and the ridden scooter, and the limit is the game's own figure");
        string director = ModSource(@"Garage\GarageDirector.cs");
        Check(director.Contains("ScooterHold.Tick(save)") && director.IndexOf("ScooterHold.Tick(save)", StringComparison.Ordinal) > director.IndexOf("private void Slow(", StringComparison.Ordinal),
            "scooter hold source: it runs from the director's second, not a frame");
        Check(ModSource(@"Core\Settings.cs").Contains("public static bool GarageScooterHold = true;"), "scooter hold source: on by default");
        Check(ModSource(@"Core\RealLifeOverhaulMod.cs").Contains("Settings.GarageScooterHoldId, \"reallife_garage_scooter_hold\"")
              && ModSource(@"Meta\OptionsTree.cs").Contains("\"garage_scooter_hold\"") && ModSource(@"Meta\FeatureList.Life.cs").Contains("Settings.GarageScooterHoldId"),
            "scooter hold source: a toggle on the options page, in its tree and owned by the Garage's feature");
        Check(ModSource(@"Garage\BikeMount.cs").Contains("_autoDestroy.enabled = false;"), "scooter hold source: the mod's own bikes are still kept out of the game's away-clock");
        var en = EnglishOf();
        Check(en.ContainsKey("reallife_garage_scooter_hold") && en.TryGetValue("reallife_opt_tip_garage_scooter_hold", out string tip) && tip.Length <= 190 && tip.Split(' ').Length <= 32,
            "scooter hold texts: a label and a tip of at most 190 characters and 32 words");
        Check(en.TryGetValue("reallife_guide_garage", out string guide) && guide.Contains("Keep a rented scooter while you are indoors"), "scooter hold texts: the Guide says what the option does");
        Check(WhatsNewHas(en, "a scooter rented from the game's stands is no longer taken away while you are in a building"), "scooter hold texts: What's new tells of the fix");
    }
}
