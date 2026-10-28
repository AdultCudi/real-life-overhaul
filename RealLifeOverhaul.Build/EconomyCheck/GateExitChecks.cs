using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using RealLifeOverhaul.NewBuildings;

// A car put out of a warehouse (NewBuildings/WarehouseExit.cs, GateRules.PushOut, LifeGame.ExitWhy; a beta player, 28 Oct 2026: "now i can enter
// warehouses in a car but can't leave only on foot"). The game puts a car out at the door's spawn point inside the tunnel with the middle of its mesh half a
// length from it, and the door's entrance trigger, 0.32 m past that point, takes any car that touches it into the building. Held here: on Build 3682's
// 62 doors the point lies at least GateRules.ExitClear clear of the trigger for a car whose mesh is centred on its origin, so a game's own car leaves; a car
// whose mesh is centred behind its origin stands into the trigger by just that much, and the rule moves it out by that distance and no more, never
// further than GateRules.ExitMost and never a car that is clear; the doors of a copy are kept in the game's own order; the game's side is read as text:
// the player's own car only, once for each exit, along the door's way, the load's census asking the game's own reading of the exit, the test watching
// that the car stays out.
internal static partial class Program
{
    private static void GateExitChecks()
    {
        string path = System.IO.Path.GetFullPath(System.IO.Path.Combine(LocalesFolder(), "..", "..", "..", "..", "RealLifeOverhaul.Build", "Census", DesignGates));
        if (!File.Exists(path))
        {
            Check(false, "gate exit: the census (" + DesignGates + ") could not be read");
            return;
        }
        int doors = 0;
        double least = double.MaxValue, most = double.MinValue;
        var behind = new List<double>(4);
        using (JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path)))
        {
            foreach (JsonElement row in doc.RootElement.GetProperty("gated").EnumerateArray())
            {
                foreach (JsonElement g in row[3].EnumerateArray())
                {
                    JsonElement at = g.GetProperty("at"), inside = g.GetProperty("inside"), enter = g.GetProperty("enter");
                    double inDistance = Math.Sqrt(Math.Pow(inside[0].GetDouble() - at[0].GetDouble(), 2) + Math.Pow(inside[1].GetDouble() - at[1].GetDouble(), 2));
                    double enterDistance = Math.Sqrt(Math.Pow(enter[0].GetDouble() - at[0].GetDouble(), 2) + Math.Pow(enter[1].GetDouble() - at[1].GetDouble(), 2));
                    double gap = enterDistance - GateRules.EnterBack - GateRules.EnterHalf - inDistance;   // m from the spawn point to the trigger's door-ward edge
                    least = Math.Min(least, gap);
                    most = Math.Max(most, gap);
                    doors++;
                }
            }
        }
        Check(doors == 62 && least >= GateRules.ExitClear - 1e-4 && most < 0.5,
            "gate exit: on Build 3682's " + doors + " garage doors the spawn point inside the tunnel is " + least.ToString("0.00") + " to " + most.ToString("0.00") + " m before the entrance trigger's door-ward edge: a car with its mesh centred on its origin is put out clear of it (wanted at least " + GateRules.ExitClear + " m), and the margin is small enough that a mesh centred behind its origin by more than that is not");
        // The rule: the body's inner end and the trigger's door-ward edge, in metres along the way out from the door (the inside is negative).
        float spawn = -4.55f, trigger = GateRules.TriggerEdge(6.5f);   // the 4.55 m doors: the object 6.5 m in
        Check(Near(trigger, -4.87f, 0.001f) && GateRules.PushOut(spawn, trigger) == 0f, "gate exit: a car put out with its mesh centred on its origin (its tail at the spawn point, " + spawn + " m) is clear of the trigger (its edge at " + trigger + " m) and not moved");
        bool just = true, ordered = true, capped = true;
        float lastPush = 0f;
        for (float offset = 0f; offset <= 1.6f; offset += 0.1f)   // the mesh's centre this far behind the origin: the tail stands that much further in
        {
            float tail = spawn - offset;
            float push = GateRules.PushOut(tail, trigger);
            just &= push == 0f || Near(tail + push, trigger + GateRules.ExitClear, 0.0001f);   // moved to stand exactly ExitClear clear of the trigger
            ordered &= push >= lastPush - 1e-6f;
            capped &= push <= GateRules.ExitMost + 1e-6f;
            lastPush = push;
        }
        Check(just && ordered && capped && GateRules.PushOut(spawn - 0.5f, trigger) > 0.4f && GateRules.PushOut(spawn - 0.5f, trigger) < 0.5f && GateRules.PushOut(spawn - 20f, trigger) == GateRules.ExitMost,
            "gate exit: a car whose mesh is centred 0.1 to 1.6 m behind its origin is moved out by just the distance its tail stands into the trigger plus " + GateRules.ExitClear + " m (0.48 m at 0.5 m), more the further behind it is, never more than " + GateRules.ExitMost + " m");
        Check(GateRules.PushOut(spawn + 1f, trigger) == 0f && GateRules.PushOut(trigger + GateRules.ExitClear, trigger) == 0f && GateRules.PushOut(trigger + GateRules.ExitClear - 0.01f, trigger) > 0f && GateRules.EnterBack == 1.3f && GateRules.EnterHalf == 0.33f,
            "gate exit: a car already out beyond the trigger is never moved, and the edge of the clear margin is exact");

        // The game's side, read as text.
        string exit = ModSource(@"NewBuildings\WarehouseExit.cs"), director = ModSource(@"NewBuildings\NewBuildingsDirector.cs"), copy = ModSource(@"NewBuildings\CopyGame.cs");
        string doorsGame = ModSource(@"Uptown\LifeGame.Doors.cs"), doorsTest = ModSource(@"Uptown\LifeDirector.Doors.cs");
        string left = MemberText(exit, "public static void Left(Address address)"), wire = MemberText(copy, "private static void Wire("), why = MemberText(doorsGame, "public static string ExitWhy(MadeBuilding made)");
        Check(exit.Length > 0 && left.Contains("MadeBuildings.At(address.streetName, address.streetNumber)") && left.Contains("game.selectedVehicle") && left.Contains("spawnInPlayerObject") && left.Contains("made.Controller.driveInEntrances")
              && left.Contains("GateRules.PushOut(near, far)") && left.Contains("vehicle.transform.position += way * push") && left.Contains("Boxes[b].isTrigger")
              && Occurs(exit, "transform.position +=") == 1 && !exit.Contains("Rigidbody") && !exit.Contains("Destroy(") && !exit.Contains("Update(") && !exit.Contains("FindObjects"),
            "gate exit sources: a car just put out of one of the mod's buildings is moved along its door's way by GateRules.PushOut, only when it stands into the door's entrance trigger, the player's own car only, nothing searched and nothing per frame");
        Check(director.Contains("_onExit = Guard.Wrap<Address>(FrameGuard, WarehouseExit.Left);") && director.Contains("EventHold.Keep(ref GlobalEvents.onExitBuilding, _onExit, ref _exitSeen);") && director.Contains("GlobalEvents.onExitBuilding -= _onExit;")
              && director.Contains("MadeBuildings.Count > 0 && Settings.NewBuildings"),
            "gate exit sources: the handler is made once through the guard, kept on the game's event once a made building stands and the option is on, and taken off when the city goes");
        Check(wire.Contains("DriveInEntrance[] listed = c.driveInEntrances;") && wire.Contains("c.driveInEntrances = Ordered.ToArray();") && !wire.Contains("Kept.ToArray()") && wire.IndexOf("Ordered.Add(listed[i]);", StringComparison.Ordinal) < wire.IndexOf("Ordered.Add(Kept[i]);", StringComparison.Ordinal),
            "gate exit sources: a copy's doors are kept in the game's own order (the exit takes the first when no door has its id, and the game's list does not run in door order)");
        Check(why.Contains("GetSpawnPositionInsideOfGarageDoor(stand)") && why.Contains("GetSpawnPositionInFrontOfGarageDoor(stand)") && why.Contains("Physics.CheckBox(inside, half, t.rotation, LayerHelper.VehiclesLayerIndex)")
              && why.Contains("GateRules.PushOut(near, far)") && why.Contains("SceneryTrees.DestroyNow(stand.gameObject)") && why.Contains("Destroy(mesh)") && doorsGame.Contains("return ExitWhy(made);"),
            "gate exit sources: the load's census asks the game's own door where it puts a car and the game's own test for a blocked gate, and whether the car stands clear of the trigger, with a stand-in that is destroyed");
        Check(doorsTest.Contains("DriveRetake") && doorsTest.Contains("leftAgain = leftAgain && !BuildingManager.IsInsideBuilding && !manager.enteringBuilding;"),
            "gate exit sources: the test watches the car stay out for a few seconds after the game's exit, not only the moment it came out");
        DriveFixTexts();
    }

    // The two fix lines of 28 Oct 2026 (the invisible box at the street ends; a car put out of a warehouse), in the next row or the row a release made of it,
    // of each of the eleven languages, once each, after the fix mark.
    private static readonly Dictionary<string, string[]> DriveFixStarts = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        { "en", new[] { "at the ends of the streets and avenues of Uptown and Cedar Heights a car could crash into a barrier that nothing showed", "a car driven out of a warehouse in Uptown or Cedar Heights could be taken straight back in by the door" } },
        { "pt", new[] { "nos fins das ruas e av", "um carro tirado de um " } },
        { "zh-cn", new[] { "在上城和雪松高地的街道与大道尽头，汽车可能撞", "从上城或雪松高地的仓库开出来的汽车，可能会直" } },
        { "de", new[] { "an den Enden der Straß", "ein Auto, das aus eine" } },
        { "fr", new[] { "aux extrémités des rue", "une voiture sortie d'u" } },
        { "es", new[] { "en los extremos de las", "un coche sacado de un " } },
        { "ru", new[] { "в конце улиц и авеню U", "машину, выведенную из " } },
        { "nl", new[] { "aan het einde van de s", "een auto die uit een m" } },
        { "tr", new[] { "Uptown ve Cedar Height", "Uptown veya Cedar Heights'ta bir depodan" } },
        { "pl", new[] { "na końcach ulic i alei", "samochód wyprowadzony " } },
        { "ko", new[] { "Uptown과 Cedar Heights의", "Uptown 또는 Cedar Height" } }
    };

    private static void DriveFixTexts()
    {
        foreach (KeyValuePair<string, string[]> language in DriveFixStarts)
        {
            Dictionary<string, string> own = LoadWords(System.IO.Path.Combine(LocalesFolder(), language.Key + ".json"));
            bool ok = own != null;
            string mark = FixMarks[language.Key];
            for (int i = 0; ok && i < language.Value.Length; i++)
            {
                string line = mark + language.Value[i];
                string row = FixRow(own, line);
                ok &= WhatsNewHas(own, line) && HunterCount(row, line) == 1 && !row.Contains("  ") && !row.Contains(NothingYet[language.Key]);
            }
            Check(ok, "drive fix texts: What's new has the fix lines of the invisible box at the street ends and of the car put out of a warehouse in " + language.Key + ", once each, after its mark, no double space");
        }
    }
}
