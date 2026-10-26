using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using RealLifeOverhaul;

// Emergency vehicles on a call never stuck behind a stopped or slow vehicle (a player's report, 26 Oct 2026: "The firetruck is just
// standing there due to the bus blocking it", 6th Avenue; the bus itself stood held by delivery trucks parked into its lane, and
// the engine stood behind the bus for the 85 s the scene gives it, then was stood down). The rules of Traffic/MakeWayRules.cs: when
// a lit unit comes up on a vehicle, how soon a bus held up goes round, when a bus at a stop with no bay stands clear, the ways past
// a held up unit tries and when, when standing traffic is moved on, when a unit stands where it is and its crew walks the rest,
// and the time the tow truck gives up in; a whole held-up run played out tick by tick; and over the sources that each unit asks
// for them, that the test of a stopped bus is in Options > Tests and says PASS or FAIL, and that the texts are in.
internal static partial class Program
{
    private static void MakeWays()
    {
        MakeWayRulesChecks();
        MakeWayRunChecks();
        MakeWaySourceChecks();
        MakeWayTextChecks();
    }

    private static void MakeWayRulesChecks()
    {
        // ---- a lit unit comes up on a vehicle: behind it, near, in its lane or the next, going its way.
        Check(MakeWayRules.UnitComesUp(30f, 0f, 1f) && MakeWayRules.UnitComesUp(30f, 3.3f, 0.9f) && MakeWayRules.UnitComesUp(30f, -3.3f, 0.9f),
            "make way: a lit unit 30 m behind in the same lane or the next, going the same way, comes up on a vehicle");
        Check(MakeWayRules.UnitComesUp(MakeWayRules.ComesUpWithin, 0f, 1f) && !MakeWayRules.UnitComesUp(MakeWayRules.ComesUpWithin + 0.5f, 0f, 1f),
            "make way: a unit comes up on a vehicle from " + MakeWayRules.ComesUpWithin + " m behind it, not from further");
        Check(!MakeWayRules.UnitComesUp(-5f, 0f, 1f) && !MakeWayRules.UnitComesUp(0f, 0f, 1f), "make way: a unit that is ahead of the vehicle, or level with it, is not coming up on it");
        Check(MakeWayRules.UnitComesUp(30f, MakeWayRules.ComesUpAside, 1f) && !MakeWayRules.UnitComesUp(30f, MakeWayRules.ComesUpAside + 0.5f, 1f)
              && !MakeWayRules.UnitComesUp(30f, -MakeWayRules.ComesUpAside - 0.5f, 1f),
            "make way: a unit in a street beside, or across the road, is not coming up on a vehicle either side");
        Check(MakeWayRules.UnitComesUp(30f, 0f, MakeWayRules.ComesUpFacing) && !MakeWayRules.UnitComesUp(30f, 0f, MakeWayRules.ComesUpFacing - 0.05f)
              && !MakeWayRules.UnitComesUp(30f, 0f, 0f) && !MakeWayRules.UnitComesUp(30f, 0f, -1f),
            "make way: a unit across the vehicle's way, or coming the other way, is not coming up on it");
        Check(MakeWayRules.ComesUpAside > 3.3f && MakeWayRules.ComesUpAside < 7f, "make way: the lane beside counts, a lane two over does not by much: " + MakeWayRules.ComesUpAside + " m");

        // ---- a bus held up goes round at once for a unit behind it; a bus at a stop in the lane stands clear.
        Check(MakeWayRules.RoundWaits(RideRules.RoundStillAfter, true) == MakeWayRules.RoundAfter && MakeWayRules.RoundWaits(RideRules.RoundStillAfter, false) == RideRules.RoundStillAfter,
            "make way: a bus held up goes round after " + MakeWayRules.RoundAfter + " s with a unit behind it, " + RideRules.RoundStillAfter + " s without");
        Check(MakeWayRules.RoundWaits(1f, true) == 1f, "make way: a bus that would go round sooner than that anyway still does");
        Check(RideRules.GoRound(false, MakeWayRules.RoundAfter, true, false, false, true) && !RideRules.GoRound(false, MakeWayRules.RoundAfter - 0.1f, true, false, false, true)
              && !RideRules.GoRound(false, MakeWayRules.RoundAfter, true, false, false),
            "make way: behind something standing still a bus goes round after " + MakeWayRules.RoundAfter + " s only when a unit is behind it");
        Check(!RideRules.GoRound(false, 200f, false, false, false, true) && !RideRules.GoRound(false, 200f, true, true, false, true) && !RideRules.GoRound(false, 200f, true, false, true, true)
              && RideRules.GoRound(true, RideRules.RoundClosedAfter, false, false, false, true),
            "make way: a unit behind never makes a bus go round a car that moves, nor one that stands for the bus, nor twice in a hold; a closed lane is as before");
        Check(MakeWayRules.RoundAfter >= RideRules.StandShortAfter, "make way: the bus has held " + RideRules.StandShortAfter + " s (the look that asks) before it goes round: " + MakeWayRules.RoundAfter);
        Check(MakeWayRules.BusStandsClear(false, true) && !MakeWayRules.BusStandsClear(true, true) && !MakeWayRules.BusStandsClear(false, false) && !MakeWayRules.BusStandsClear(true, false),
            "make way: a bus standing in the lane with a unit behind it stands clear; in a bay, or with none behind, it stays");

        // ---- the ways past a held up unit tries.
        Check(!MakeWayRules.Widen(MakeWayRules.WidenAfter - 0.01f) && MakeWayRules.Widen(MakeWayRules.WidenAfter), "make way: a unit looks further for a way past after " + MakeWayRules.WidenAfter + " s held up");
        Check(MakeWayRules.Choices(true, 0f) == 2 && MakeWayRules.Choices(true, MakeWayRules.WidenAfter) == 4 && MakeWayRules.Choices(false, 100f) == 2,
            "make way: a lit unit tries two ways past, four once held up a while; another driver always two (its drive as before)");
        float lane = 3.3f;
        Check(MakeWayRules.Offset(0, lane) == -lane && MakeWayRules.Offset(1, lane) == lane && MakeWayRules.Offset(2, lane) == -2f * lane && MakeWayRules.Offset(3, lane) == 2f * lane,
            "make way: the ways past are a lane left, a lane right, two lanes left and two right, the nearest first");
        Check(MakeWayRules.LaneSnap(1.7f, true, MakeWayRules.WidenAfter) == 1.7f * MakeWayRules.WidenSnap && MakeWayRules.LaneSnap(1.7f, true, 0f) == 1.7f
              && MakeWayRules.LaneSnap(1.7f, false, 100f) == 1.7f,
            "make way: a lit unit held up a while takes a lane its map is less sure of; nobody else, and nobody sooner");

        // ---- what is said, when a unit stands, when traffic is moved on, when the tow truck gives up.
        Check(!MakeWayRules.Tell(MakeWayRules.TellAfter - 0.1f, false) && MakeWayRules.Tell(MakeWayRules.TellAfter, false) && !MakeWayRules.Tell(100f, true),
            "make way: a hold is said once in the log, after " + MakeWayRules.TellAfter + " s");
        Check(MakeWayRules.Stands(MakeWayRules.StandAfter, MakeWayRules.WalkMost) && !MakeWayRules.Stands(MakeWayRules.StandAfter - 0.1f, 10f) && !MakeWayRules.Stands(100f, MakeWayRules.WalkMost + 1f),
            "make way: held up " + MakeWayRules.StandAfter + " s with at most " + MakeWayRules.WalkMost + " m of its route left, a unit stands where it is and its crew walks the rest");
        Check(MakeWayRules.Clear(MakeWayRules.ClearUnseen, false) && !MakeWayRules.Clear(MakeWayRules.ClearUnseen - 0.1f, false) && !MakeWayRules.Clear(MakeWayRules.ClearSeen - 0.1f, true)
              && MakeWayRules.Clear(MakeWayRules.ClearSeen, true),
            "make way: standing traffic in a unit's way is moved on after " + MakeWayRules.ClearUnseen + " s out of view and " + MakeWayRules.ClearSeen + " s in view");
        Check(!MakeWayRules.TowGivesUp(MakeWayRules.TowGivesUpAfter - 0.1f) && MakeWayRules.TowGivesUp(MakeWayRules.TowGivesUpAfter), "make way: the tow truck held up " + MakeWayRules.TowGivesUpAfter + " s gives the wreck up");
        Check(MakeWayRules.RoundAfter < MakeWayRules.WidenAfter && MakeWayRules.WidenAfter < MakeWayRules.TellAfter && MakeWayRules.TellAfter < MakeWayRules.StandAfter
              && MakeWayRules.ClearUnseen < MakeWayRules.WidenAfter + 0.1f && MakeWayRules.ClearSeen <= MakeWayRules.TellAfter && MakeWayRules.StandAfter < 20f,
            "make way: in order, a bus goes round, a unit widens, a hold is said, a unit stands and its crew walks, all inside 20 s");
        Check(MakeWayRules.TowGivesUpAfter < 150f && MakeWayRules.WalkMost >= 100f, "make way: the tow truck gives up well inside its 150 s, a crew may walk a block and more: " + MakeWayRules.WalkMost + " m");
    }

    // A unit held up behind a bus that never moves, ticked every 0.25 s as the scene does: the order of what happens, and that it ends.
    private static void MakeWayRunChecks()
    {
        const float tick = 0.25f;
        float widened = -1f, told = -1f, stood = -1f, cleared = -1f;
        bool passedByLane = false;
        for (float held = 0f; held <= 120f; held += tick)
        {
            if (widened < 0f && MakeWayRules.Widen(held))
            {
                widened = held;
            }
            if (told < 0f && MakeWayRules.Tell(held, false))
            {
                told = held;
            }
            if (cleared < 0f && MakeWayRules.Clear(held, true))
            {
                cleared = held;
            }
            if (stood < 0f && MakeWayRules.Stands(held, 100f))
            {
                stood = held;
            }
            passedByLane |= MakeWayRules.Choices(true, held) == 4;
        }
        Check(widened == 2.5f && told == 4f && cleared == 4f && stood == 9f && passedByLane,
            "make way: held up behind a bus that never moves, a unit widens at " + widened + " s, is said at " + told + " s, standing traffic goes at " + cleared + " s, it stands at " + stood + " s");
        // 100 m of route left and held for good: the engine's scene gives it 85 s; it has stopped long before.
        Check(stood > 0f && stood < 85f * 0.25f, "make way: a unit held up for good, 100 m short, has stopped and put its crew out inside a quarter of the 85 s the scene gives it: " + stood);
        // Far off (the next street): it is not stood where it is; the scene's own timing decides.
        bool never = true;
        for (float held = 0f; held <= 300f; held += 1f)
        {
            never &= !MakeWayRules.Stands(held, MakeWayRules.WalkMost + 30f);
        }
        Check(never, "make way: held up far from its place (further than " + MakeWayRules.WalkMost + " m by its route) it is not stood where it is");
        // Moving or held only a moment: it never stands.
        Check(!MakeWayRules.Stands(0f, 50f) && !MakeWayRules.Stands(3f, 50f) && !MakeWayRules.Tell(0f, false), "make way: a unit that moves, or is held a moment, is neither stood nor said");
    }

    private static void MakeWaySourceChecks()
    {
        string scripts = System.IO.Path.GetFullPath(System.IO.Path.Combine(LocalesFolder(), "..", "Scripts"));
        string Read(params string[] parts) => System.IO.File.ReadAllText(System.IO.Path.Combine(scripts, System.IO.Path.Combine(parts)));
        string sense = Read("Traffic", "EmergencyDriver.Sense.cs");
        string way = Read("Traffic", "EmergencyDriver.Way.cs");
        string driver = Read("Traffic", "EmergencyDriver.cs");
        string clear = Read("Traffic", "MakeWay.cs");
        string response = Read("Events", "FireScene.Response.cs");
        string blocked = Read("Events", "FireScene.Blocked.cs");
        string fire = Read("Events", "FireScene.cs");
        string tests = Read("Events", "EventDirector.Tests.cs");
        string held = Read("Transport", "TransitBus.Held.cs");
        string round = Read("Transport", "TransitBus.Round.cs");
        string buses = Read("Transport", "TransitDirector.Buses.cs");
        string tow = Read("Traffic", "Tow", "TowService.cs");
        string mod = Read("Core", "RealLifeOverhaulMod.cs");
        string accidents = Read("Traffic", "TrafficAccidents.Staging.cs");

        // The ways past: one place makes the choice, every unit that drives through EmergencyDriver has it.
        Check(sense.Contains("if (look && TryPass(middle, forward, roadRight))") && !sense.Contains("CanPass(") && way.Contains("MakeWayRules.Choices(Lit, held)")
              && way.Contains("MakeWayRules.LaneSnap(LaneSnap, Lit, held)") && way.Contains("MakeWayRules.Offset(choice, PassOffset)"),
            "make way source: the ambulance, the patrol car and the fire engine pick a way past in the one place, by the rules, widening only for a lit unit");
        Check(way.Contains("if (between && IsBlocked(middle + roadRight * (offset * 0.5f), forward))"), "make way source: a way two lanes over is only taken when the lane between it is clear");
        Check(driver.Contains("TellWay(position, roadRight);") && driver.Contains("ResetWay();") && way.Contains("MakeWayRules.Tell(held, _wayTold)") && way.Contains("Emergency way: the ")
              && way.Contains("is moving again"), "make way source: a hold is written once, with what holds it and the ways past, and so is its end");
        Check(way.Contains("MakeWayRules.UnitComesUp(") && way.Contains("driver._reverse") && way.Contains("LitAt(i)"), "make way source: a unit coming up is a lit one that drives, not one that backs or stands");
        Check(clear.Contains("TrafficAccidents.IsUsable(car)") && clear.Contains("TrafficTools.Ready") && clear.Contains("MakeWayRules.Clear(heldFor, StreetSpots.IsInView(car.transform.position))")
              && clear.Contains("car.GetCurrentSpeed() > 1f") && accidents.Contains("internal static bool IsUsable(VehicleComponent car)"),
            "make way source: only a standing car of the traffic the mod's scenes do not leave alone is moved on, never in a Going Public session, by the rule");

        // The fire.
        Check(response.Contains("MakeWayRules.Stands(driver.HeldFor, away)") && response.Contains("MakeWayRules.StandAfter") && !response.Contains("driver.HeldFor > 9f && away < 45f"),
            "make way source: a fire engine held up near enough stands where it is and its crew walks the rest, by the rule");
        Check(response.Contains("ClearTheWay(driver, \"the fire engine\", ref _engineMoved, now, ref _engineWayAt);") && response.Contains("ClearTheWay(driver, \"the patrol car\", ref _patrolMoved, now, ref _patrolWayAt);")
              && blocked.Contains("MakeWay.ClearStanding(driver.Blocker, held, unit, ref moved, ClearMost)"), "make way source: the engine and the patrol car at a fire have standing traffic moved on");
        Check(response.Contains("BlockedArrival(engine, now, away, held);") && response.Contains("BlockedFailed(") && blocked.Contains("PASS") && blocked.Contains("FAIL")
              && blocked.Contains("Fire check (stopped bus)") && fire.Contains("RemoveBlockingBus();") && fire.Contains("ResetBlocked(blockEngine);") && response.Contains("PlaceBlockingBus(engine, now);"),
            "make way source: the test of a stopped bus puts it in the engine's lane as it is sent, says PASS or FAIL when the engine stops or is stood down, and takes the bus away");
        Check(fire.Contains("(blockEngine ? 0f : DispatchAfter)") && tests.Contains("public void TestFireBlocked()") && tests.Contains("_fire.Stage(kind, player.transform.position, Ahead(player, 14f), true, Time.time, blocked)"),
            "make way source: the test sends the engine at once, on a bin fire the way the other tests stage one");
        Check(mod.Contains("\"reallife_events_test_fire_blocked\", () => Events.EventDirector.Instance?.TestFireBlocked()"), "make way source: the test is a button in Options > Tests");

        // The bus.
        Check(round.Contains("RideRules.GoRound(closed, still, holderStill, yields, _hold.Rounded, unitBehind)") && round.Contains("bool unitBehind = !closed && UnitBehind();")
              && held.Contains("EmergencyDriver.UnitComingUp(centre, forward)") && held.Contains("MakeWayRules.BusStandsClear(_bay, UnitBehind())"),
            "make way source: a bus held up asks whether a lit unit is behind it, and one standing in the lane at its stop asks too");
        Check(buses.Contains("if (!busy && !held && _bus.StandsInTheWay())") && buses.Contains("Transit check (make way)") && buses.Contains("Leave(now);"),
            "make way source: a bus standing at a stop with no bay pulls away for a lit unit, unless somebody is boarding or the player is choosing");

        // The tow truck.
        Check(tow.Contains("MakeWay.ClearStanding(by != null ? by.attachedRigidbody : null, held, \"the tow truck\", ref _moved, MoveMost)") && tow.Contains("MakeWayRules.TowGivesUp(held)") && tow.Contains("_moved = 0;"),
            "make way source: the tow truck has standing traffic in its way moved on and gives a wreck up when held for good");
    }

    private static void MakeWayTextChecks()
    {
        Dictionary<string, string>[] words = LanguageWords(out string[] codes);
        if (!LanguagesRead("make way texts", words, codes))
        {
            return;
        }
        for (int l = 0; l < 3 && l < words.Length; l++)
        {
            Check(words[l].TryGetValue("reallife_events_test_fire_blocked", out string text) && text.Length > 10 && text.Length <= 240 && !text.Contains('!') && !text.Contains((char)0x2014) && !text.Contains((char)0x2013),
                "make way texts: the test button's text is in " + codes[l] + ", short, without an exclamation mark or dash");
        }
        var lines = new Dictionary<string, string>
        {
            { "en", "Fixed: Fire engines, ambulances, patrol cars and tow trucks on a call no longer wait behind a stopped bus or car for ever." },
            { "pt", "Corrigido: Caminhões de bombeiros, ambulâncias, viaturas e guinchos em chamada não ficam mais esperando para sempre atrás de um ônibus ou carro parado." },
            { "zh-cn", "已修复：执行任务的消防车、救护车、巡逻车和拖车不再被停着的公交车或小汽车永远挡在后面。" },
            { "de", "Behoben: Löschfahrzeuge, Krankenwagen, Streifenwagen und Abschleppwagen im Einsatz warten nicht mehr ewig hinter einem haltenden Bus oder Auto." },
            { "fr", "Corrigé : Les camions de pompiers, ambulances, voitures de patrouille et dépanneuses en intervention n'attendent plus indéfiniment derrière un bus ou une voiture à l'arrêt." },
            { "es", "Corregido: Los camiones de bomberos, ambulancias, coches patrulla y grúas en servicio ya no esperan para siempre detrás de un autobús o un coche parado." },
            { "ru", "Исправлено: Пожарные машины, скорые, патрульные машины и эвакуаторы на вызове больше не ждут вечно за стоящим автобусом или машиной." },
            { "nl", "Opgelost: Brandweerwagens, ambulances, politieauto's en takelwagens op een melding wachten niet meer eindeloos achter een stilstaande bus of auto." },
            { "tr", "Düzeltildi: Çağrıdaki itfaiye araçları, ambulanslar, devriye arabaları ve çekiciler artık duran bir otobüsün ya da arabanın arkasında sonsuza kadar beklemiyor." }
        };
        for (int l = 0; l < codes.Length; l++)
        {
            Check(lines.TryGetValue(codes[l], out string wanted) && WhatsNewHas(words[l], wanted), "make way texts: What's new has the fix in some row in " + codes[l]);
            int times = 0;
            foreach (KeyValuePair<string, string> row in words[l])
            {
                if (row.Key.StartsWith("reallife_changes_", StringComparison.Ordinal) && !row.Key.EndsWith("_title", StringComparison.Ordinal) && lines.ContainsKey(codes[l]))
                {
                    times += Regex.Matches(row.Value, Regex.Escape(lines[codes[l]])).Count;
                }
            }
            Check(times == 1, "make way texts: its What's new line is in " + codes[l] + " once, never repeated: " + times);
            string row0 = null;
            foreach (KeyValuePair<string, string> row in words[l])
            {
                if (row.Key.StartsWith("reallife_changes_", StringComparison.Ordinal) && lines.ContainsKey(codes[l]) && row.Value.Contains(lines[codes[l]]))
                {
                    row0 = row.Value;
                }
            }
            int at = row0 != null ? row0.IndexOf(lines[codes[l]], StringComparison.Ordinal) : -1;
            string mine = at >= 0 ? row0.Substring(at) : string.Empty;
            Check(mine.Length > 0 && !mine.Contains('!') && !mine.Contains((char)0x2014) && !mine.Contains((char)0x2013) && !mine.Contains(" - ") && !mine.Contains("  "),
                "make way texts: its What's new line has no exclamation mark, dash or double space in " + codes[l]);
        }
        Check(codes.Length == 9, "make way texts: What's new is read in all nine languages: " + codes.Length);
    }
}
