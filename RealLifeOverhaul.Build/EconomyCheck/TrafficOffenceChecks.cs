using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using RealLifeOverhaul;

// A player found the police "extremely zealous with jaywalking" and wanted that one offence off while keeping the rest; one switch,
// Settings.PlayerOffences, held every traffic offence. A player riding in another mod's taxi or private driver was fined as a walker
// all the way, because the foot look asked only the save's active vehicle (PlayerHelper.IsUsingVehicle), which that ride never sets.
// Now each kind of offence has its own option (Crime/OffenceRules.cs, Settings.Offences) that starts from the old switch's saved value,
// and the foot look leaves alone anyone something carries (Crime/PlayerRide.cs). First the rules, then the places that use them, then
// the texts. "Fixed:" is looked for in any What's new row, as the version step moves the next row into its own.
internal static partial class Program
{
    // The kinds, in the order OffenceSwitches takes its switches.
    private static readonly Offence[] OffenceKinds =
        { Offence.RedLight, Offence.Speeding, Offence.Reckless, Offence.WrongSide, Offence.Jaywalk, Offence.CrossOnRed, Offence.DrunkDriving };

    // The ids the options page knows them by, in the same order, and the horn's, which is not an Offence.
    private static readonly string[] OffenceIds =
        { "offence_red_light", "offence_speeding", "offence_pavement", "offence_wrong_way", "offence_jaywalk", "offence_cross_red", "offence_drink" };
    private const string OffenceHornId = "offence_horn";

    private static void TrafficOffences()
    {
        OffenceSwitchRules();
        OffenceSeedRules();
        OffenceRideRules();
        OffenceCost();
        OffenceSources();
        OffenceWords();
    }

    private static OffenceSwitches OffencesWith(int on)   // bit i: kind i of OffenceKinds is on
    {
        return new OffenceSwitches((on & 1) != 0, (on & 2) != 0, (on & 4) != 0, (on & 8) != 0, (on & 16) != 0, (on & 32) != 0, (on & 64) != 0);
    }

    private static RideFacts RideWith(int signs)   // bit i: the i-th sign of a ride is on
    {
        return new RideFacts((signs & 1) != 0, (signs & 2) != 0, (signs & 4) != 0, (signs & 8) != 0, (signs & 16) != 0, (signs & 32) != 0);
    }

    // ---------------------------------------------------------------- one switch for each kind

    private static void OffenceSwitchRules()
    {
        bool each = true;
        for (int on = 0; on < OffenceKinds.Length; on++)
        {
            OffenceSwitches only = OffencesWith(1 << on);
            for (int kind = 0; kind < OffenceKinds.Length; kind++)
            {
                each &= only.Allows(OffenceKinds[kind]) == (kind == on);
            }
        }
        Check(each, "traffic offences: with one switch on, the police act on that kind of offence and on no other");

        OffenceSwitches all = OffencesWith(127);
        bool allOn = true;
        foreach (Offence kind in OffenceKinds)
        {
            allOn &= all.Allows(kind);
        }
        Check(allOn && !all.Allows(Offence.None) && !all.Allows((Offence)99), "traffic offences: with every switch on, all seven kinds count, and nothing that is no offence does");

        OffenceSwitches noJaywalk = OffencesWith(127 & ~16);
        bool rest = true;
        foreach (Offence kind in OffenceKinds)
        {
            rest &= noJaywalk.Allows(kind) == (kind != Offence.Jaywalk);
        }
        Check(rest, "traffic offences: jaywalking off and the other six on (the player's own wish): only jaywalking is let go");

        OffenceSwitches none = OffencesWith(0);
        Check(!none.Any && !none.Driving && !none.OnFoot, "traffic offences: with every switch off nothing is looked for, as with the one switch off before");
        OffenceSwitches drinkOnly = OffencesWith(64), walkOnly = OffencesWith(16), crossOnly = OffencesWith(32), lightOnly = OffencesWith(1);
        Check(drinkOnly.Allows(Offence.DrunkDriving) && !drinkOnly.Any, "traffic offences: drink is reported by Leisure, so it alone gives the timed looks nothing to find");
        Check(walkOnly.OnFoot && walkOnly.Any && !walkOnly.Driving && crossOnly.OnFoot && !crossOnly.Driving,
            "traffic offences: either offence on foot alone makes the foot look worth making and the road map not worth building");
        Check(lightOnly.Driving && lightOnly.Any && !lightOnly.OnFoot, "traffic offences: an offence at the wheel alone builds the road map and makes no foot look");
    }

    private static void OffenceSeedRules()
    {
        Check(!OffenceRules.Seeded(true, false, true, true, true), "traffic offences: a switch the player turned off stays off whatever the old switch says");
        Check(OffenceRules.Seeded(true, true, true, false, false), "traffic offences: a switch the player turned on stays on whatever the old switch says");
        Check(!OffenceRules.Seeded(false, true, true, false, true), "traffic offences: a switch never touched starts as the old one was left, off (a player who had traffic offences off keeps them all off)");
        Check(OffenceRules.Seeded(false, false, true, true, false), "traffic offences: a switch never touched starts as the old one was left, on");
        Check(OffenceRules.Seeded(false, false, false, false, true) && !OffenceRules.Seeded(false, true, false, true, false),
            "traffic offences: with neither saved, a switch takes the default");
        int agree = 0;
        for (int bits = 0; bits < 32; bits++)
        {
            bool hasOwn = (bits & 1) != 0, own = (bits & 2) != 0, hasOlder = (bits & 4) != 0, older = (bits & 8) != 0, fallback = (bits & 16) != 0;
            bool want = hasOwn ? own : hasOlder ? older : fallback;
            agree += OffenceRules.Seeded(hasOwn, own, hasOlder, older, fallback) == want ? 1 : 0;
        }
        Check(agree == 32, "traffic offences: in all 32 states of what is saved, the own choice beats the old switch, which beats the default (" + agree + " of 32)");
    }

    // ---------------------------------------------------------------- nobody carried is walking

    private static void OffenceRideRules()
    {
        Check(!OffenceRules.Carried(RideWith(0)), "ride: a player with no sign of a ride is on foot");
        string[] signNames = { "the game's own vehicle", "a taxi trip", "a seat", "the game's walking blocker", "a vehicle among the parents", "a speed no walker reaches" };
        var seen = new HashSet<string>(StringComparer.Ordinal);
        bool alone = true;
        for (int sign = 0; sign < signNames.Length; sign++)
        {
            RideFacts ride = RideWith(1 << sign);
            bool carried = OffenceRules.Carried(ride);
            alone &= carried;
            string said = OffenceRules.Signs(ride);
            seen.Add(said);
            Check(carried, "ride: " + signNames[sign] + " alone is a ride, so a taxi or private driver of any mod that sets just that one is not taken for a walker");
        }
        Check(alone && seen.Count == signNames.Length && !seen.Contains(OffenceRules.Signs(RideWith(0))), "ride: each sign is named on its own in the log, and none of them reads as no sign");
        Check(OffenceRules.Signs(RideWith(0)) == "no sign of a ride", "ride: with no sign the log line says so, which is what a ride nobody saw will show");
        bool counted = true;
        for (int signs = 0; signs < 64; signs++)
        {
            counted &= OffenceRules.Carried(RideWith(signs)) == (signs != 0);
        }
        Check(counted, "ride: of all 64 combinations of signs, every one but none at all is a ride");
        Check(OffenceRules.Signs(RideWith(63)).Split(',').Length == 6, "ride: with every sign on the log names all six");

        // The speed: a walker 1.5 m/s, a runner 6 m/s (the most the game's own movement gives, WalkRules.MostForward), a car 10 m/s, over a look half a second old.
        Check(!OffenceRules.TooFast(0.75f * 0.75f, 0.5f), "ride: a walker's half second (0.75 m) is not too fast");
        Check(!OffenceRules.TooFast(3f * 3f, 0.5f), "ride: a runner's half second (3 m) is not too fast");
        Check(OffenceRules.TooFast(5f * 5f, 0.5f), "ride: a car's half second at 10 m/s (5 m) is faster than anyone on foot goes");
        Check(!OffenceRules.TooFast(4f * 4f, 0.5f) && OffenceRules.TooFast(4.1f * 4.1f, 0.5f), "ride: the line is " + OffenceRules.FootMost + " m/s, and exactly on it is still a walker");
        Check(!OffenceRules.TooFast(25f, 0f) && !OffenceRules.TooFast(25f, -1f), "ride: with no earlier look there is no speed");
        Check(OffenceRules.TooFast(10000f, OffenceRules.LookStale) && !OffenceRules.TooFast(10000f, OffenceRules.LookStale + 0.01f),
            "ride: a look older than " + OffenceRules.LookStale + " s (a menu, a cooldown) measures nothing, so a teleport after a pause is not a ride");
        string walk = ModSource(@"Walking\WalkRules.cs");
        Match most = Regex.Match(walk, @"public const float MostForward = ([0-9.]+)f;");
        Check(most.Success && OffenceRules.FootMost > float.Parse(most.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture),
            "ride: the speed no walker reaches is above the most the game's own movement gives (" + (most.Success ? most.Groups[1].Value : "not read") + " m/s)");
    }

    private static void OffenceCost()
    {
        OffenceLoop(64, out _);   // warmed: the methods past the runtime's tiering, whose own stubs would be counted
        OffenceLoop(1000000, out long bytes);
        OffenceLoopTime(1000000, out double ms);
        Check(bytes < 1024, "traffic offences: a million looks at the switches and the signs allocate " + bytes + " bytes (wanted: under 1 KB; a log line allocates, once an event)");
        Check(ms < 250.0, "traffic offences: a million looks at the switches and the signs take " + ms.ToString("0.0") + " ms (wanted: under 250)");
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static int OffenceLoop(int count, out long bytes)
    {
        OffenceSwitches on = OffencesWith(0x55);
        int hits = 0;
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < count; i++)
        {
            hits += on.Allows(OffenceKinds[i % 7]) ? 1 : 0;
            hits += OffenceRules.Carried(new RideFacts((i & 1) != 0, (i & 2) != 0, (i & 4) != 0, (i & 8) != 0, (i & 16) != 0, OffenceRules.TooFast(i & 31, 0.5f))) ? 1 : 0;
        }
        bytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
        return hits;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static void OffenceLoopTime(int count, out double ms)
    {
        var watch = Stopwatch.StartNew();
        OffenceLoop(count, out _);
        ms = watch.Elapsed.TotalMilliseconds;
    }

    // ---------------------------------------------------------------- where the rules are used

    private static void OffenceSources()
    {
        string settings = ModSource(@"Core\Settings.cs"), mod = ModSource(@"Core\RealLifeOverhaulMod.cs"), list = ModSource(@"Meta\FeatureList.cs");
        string tree = ModSource(@"Meta\OptionsTree.cs"), attention = ModSource(@"Crime\PoliceAttention.cs"), stop = ModSource(@"Crime\PoliceAttention.Stop.cs");
        string offences = ModSource(@"Crime\PlayerOffences.cs"), ride = ModSource(@"Crime\PlayerRide.cs"), horn = ModSource(@"People\HornReactions.cs");

        // One option for each kind: saved under its id, on by default, started from the old switch, on the page, in the presets.
        string[] fields = { "OffenceRedLight", "OffenceSpeeding", "OffencePavement", "OffenceWrongWay", "OffenceJaywalk", "OffenceCrossRed", "OffenceDrink", "OffenceHorn" };
        string[] ids = { OffenceIds[0], OffenceIds[1], OffenceIds[2], OffenceIds[3], OffenceIds[4], OffenceIds[5], OffenceIds[6], OffenceHornId };
        string[] labels = { "red_light", "speeding", "pavement", "wrong_way", "jaywalk", "cross_red", "drink", "horn" };
        string missing = string.Empty;
        for (int i = 0; i < fields.Length; i++)
        {
            string f = fields[i];
            bool whole = settings.Contains("public const string " + f + "Id = \"" + ids[i] + "\";") && settings.Contains("public static bool " + f + " = true;")
                         && settings.Contains(f + " = OffenceSwitch(modId, " + f + "Id, " + f + ");")
                         && mod.Contains("AddToggle(Settings." + f + "Id, \"reallife_offence_" + labels[i] + "\", Settings." + f + ",") && mod.Contains("v => Settings." + f + " = v)")
                         && list.Contains(".Preset(Settings." + f + "Id, 0, 1, 1)") && tree.Contains("\"" + ids[i] + "\"");
            missing += whole ? string.Empty : " " + ids[i];
        }
        Check(missing.Length == 0, "traffic offences: each of the eight kinds is saved under its id, on by default, started from the old switch, on the page, in the presets and in the tree:" + missing);
        Check(settings.Contains("new OffenceSwitches(OffenceRedLight, OffenceSpeeding, OffencePavement, OffenceWrongWay, OffenceJaywalk, OffenceCrossRed, OffenceDrink)"),
            "traffic offences: Settings.Offences hands the looks the seven switches in the order the rules take them");
        Check(settings.Contains("private const string OffencesOlderSwitch = \"player_offences\";") && !settings.Contains("PlayerOffencesId") && !settings.Contains("public static bool PlayerOffences"),
            "traffic offences: the old switch is no longer an option of the page: only its saved key is read, to start the new ones from it");
        string seed = MemberText(settings, "private static bool OffenceSwitch(string modId, string id, bool fallback)");
        Check(seed.Contains("OffenceRules.Seeded(") && seed.Contains("HasKey(Key(modId, id))") && seed.Contains("HasKey(Key(modId, OffencesOlderSwitch))") && !seed.Contains("SetInt"),
            "traffic offences: a switch asks whether it has its own saved value and whether the old switch has one, and writes nothing");
        string readers = string.Empty;   // (PlayerOffences is also the name of the class that finds the offences: it is the switch that is gone)
        foreach (KeyValuePair<string, string> source in new[]
                 { new KeyValuePair<string, string>("Settings", settings), new KeyValuePair<string, string>("the mod's page", mod), new KeyValuePair<string, string>("the feature list", list),
                   new KeyValuePair<string, string>("the tree", tree), new KeyValuePair<string, string>("PoliceAttention", attention), new KeyValuePair<string, string>("the stop", stop),
                   new KeyValuePair<string, string>("PlayerOffences", offences), new KeyValuePair<string, string>("the horn", horn) })
        {
            // (Settings alone keeps the old key as a constant, to start the new switches from it)
            bool reads = source.Value.Contains("Settings.PlayerOffences") || source.Value.Contains("PlayerOffencesId")
                         || (source.Value.Contains("\"player_offences\"") && !source.Value.Contains("OffencesOlderSwitch"));
            readers += reads ? " " + source.Key : string.Empty;
        }
        Check(readers.Length == 0, "traffic offences: nothing reads the old switch as an option any more:" + readers);
        int fromOffences = tree.IndexOf("S(\"offences\")", StringComparison.Ordinal);
        string section = fromOffences < 0 ? string.Empty : tree.Substring(fromOffences);
        string opts = Between(section, ".Opts(", ")");   // the section's rows: each id once (the search words further on name them again)
        string policeSection = Between(tree, "S(\"police\")", "S(\"offences\")");
        bool placed = opts.Length > 0;
        foreach (string id in ids)
        {
            placed &= Times(opts, "\"" + id + "\"") == 1;
        }
        Check(section.StartsWith("S(\"offences\").Guide(\"crime\").Need(\"police\")", StringComparison.Ordinal) && placed && policeSection.Length > 0
              && !policeSection.Contains("player_offences") && !tree.Contains("\"player_offences\""),
            "traffic offences: they stand in a section of their own after Police, needing Police on, each id once, and Police no longer lists the old switch");

        // The looks: every kind is asked about at the place it is found.
        Check(attention.Contains("if (Settings.Offences.Any && ModState.Loaded && TrafficTools.Ready)") && attention.Contains("if (!on.Driving || !RoadMap.Ensure())"),
            "traffic offences: the tick looks only if some kind is on, and the road map is built only for a kind at the wheel");
        Check(attention.Contains("_offences.Driving(_body, now, on)") && stop.Contains("_offences.Driving(_body, now, Settings.Offences)"),
            "traffic offences: the first sighting and the extra offences booked during a stop both honour the switches");
        string report = MemberText(attention, "internal static bool Report(Offence offence)");
        Check(report.Contains("!Settings.Offences.Allows(offence)") && MemberText(stop, "private void Breathalyse()").Contains("Settings.Offences.Allows(Offence.DrunkDriving)"),
            "traffic offences: a drink report and the breath test at a stop ask the drink switch");
        string driving = MemberText(offences, "public Offence Driving(Rigidbody body, float now, OffenceSwitches on)");
        string slow = MemberText(offences, "private Offence Slow(Vector3 position, Vector3 velocity, float speed, float now, OffenceSwitches on)");
        Check(driving.Contains("if (on.RedLight)") && slow.Contains("if (on.Speeding && speed * 3.6f") && slow.Contains("if (on.Pavement && speed > 8f") && slow.Contains("if (on.WrongWay && speed > 10f")
              && slow.Contains("if (!on.Speeding && !on.Pavement && !on.WrongWay)"),
            "traffic offences: the red light, speeding, the pavement and the wrong way are each looked for only under their own switch");
        string foot = MemberText(offences, "public Offence OnFoot(PlayerController player, float now, OffenceSwitches on)");
        Check(foot.Contains("on.CrossRed && !mayWalk") && foot.Contains("bool onRoad = on.Jaywalk &&") && foot.Contains("if (!on.OnFoot || Carried(player, position, now))"),
            "traffic offences: crossing on red and walking in the road are each found only under their own switch");
        Check(horn.Contains("!Settings.Police || !Settings.OffenceHorn || now < _nextPenalty"), "traffic offences: the horn's warning and fine ask the horn switch");

        // The foot look first asks whether something carries the player, before any officer is looked for or any physics query is made.
        int carried = foot.IndexOf("Carried(player, position, now)", StringComparison.Ordinal);
        int officer = foot.IndexOf("OfficerNear(position", StringComparison.Ordinal);
        int query = foot.IndexOf("Physics.OverlapSphereNonAlloc", StringComparison.Ordinal);
        Check(carried > 0 && officer > carried && query > officer, "ride: the foot look asks whether something carries the player before it looks for an officer or makes its physics query");
        Check(foot.Contains("Rest(position, now);") && MemberText(offences, "private void Rest(Vector3 position, float now)").Contains("_roadSince = -1f;"),
            "ride: while carried the road timer is dropped, so the first step off a ride does not inherit a second in the road");
        Check(attention.Contains("_offences.OnFoot(player, now, on)") && !attention.Contains("PlayerHelper.IsUsingVehicle || now < _nextFootOffence"),
            "ride: the foot look is no longer left to the save's active vehicle alone");
        Check(ride.Contains("PlayerHelper.IsUsingVehicle") && ride.Contains("TaxiSystem.IsTraveling") && ride.Contains("isSittingOn != null") && ride.Contains("player.NavigationDisabled")
              && ride.Contains("TryGetComponent(out VehicleComponent _)") && ride.Contains("TryGetComponent(out VehicleController _)") && ride.Contains("OffenceRules.TooFast(movedSqr, seconds)"),
            "ride: the game is read for all six signs: its vehicle, its taxi trip, a seat, its walking blocker, a vehicle among the parents, and the speed");
        Check(!ride.Contains("FindObject") && !ride.Contains("GetComponentsIn") && !ride.Contains("new List") && !ride.Contains("Physics.") && !ride.Contains("System.Linq"),
            "ride: the signs are read with no search, no physics query and no allocation");
        Check(offences.Contains("if (Meta.Dev.Verbose)") && offences.Contains("PoliceAttention.Logger?.Info(carried") && attention.Contains("Meta.Dev.Always(Logger, \"Police attention: \" + offence + \" found on foot")
              && attention.Contains("OffenceRules.Signs(_offences.Ride)"),
            "ride: a log line (with the diagnostics on) says when a ride begins and what showed it, and a foot offence always writes the signs it found, so a ride the signs missed shows in a player's Player.log");

        // The rides of the mod's own that the player sits in are among those the signs tell: each sets the game's walking blocker.
        string bus = ModSource(@"Transport\BusRider.cs"), busRide = ModSource(@"Transport\BusRider.Ride.cs"), transit = ModSource(@"Transport\TransitRide.cs");
        Check(bus.Contains("private const NavigationBlocker Blocker = NavigationBlocker.Vehicle;") && busRide.Contains("_player.SetNavigationBlocker(Blocker);")
              && transit.Contains("_player.SetNavigationBlocker(NavigationBlocker.Taxi);"),
            "ride: the mod's own bus and taxi rides set the game's walking blocker, which the foot look reads as a ride");
    }

    // ---------------------------------------------------------------- the words

    private static void OffenceWords()
    {
        Dictionary<string, string> en = LoadWords(System.IO.Path.Combine(LocalesFolder(), "en.json"));
        if (en == null)
        {
            Check(false, "traffic offences: en.json is where the checks look for it (" + LocalesFolder() + ")");
            return;
        }
        string lacking = string.Empty;
        string[] labels = { "red_light", "speeding", "pavement", "wrong_way", "jaywalk", "cross_red", "drink", "horn" };
        foreach (string name in labels)
        {
            bool label = en.TryGetValue("reallife_offence_" + name, out string text) && text.StartsWith("Police ", StringComparison.Ordinal) && !text.EndsWith(".", StringComparison.Ordinal);
            bool tip = en.TryGetValue("reallife_opt_tip_offence_" + name, out string sentence) && sentence.EndsWith(".", StringComparison.Ordinal) && sentence.Contains(" Off, ") && !sentence.Contains("!");
            lacking += label && tip ? string.Empty : " " + name;
        }
        Check(lacking.Length == 0, "traffic offences: each switch has a label that names the police and no full stop, and one sentence that ends with what off does:" + lacking);
        Check(en.TryGetValue("reallife_opt_sec_offences", out string title) && title == "Traffic offences", "traffic offences: the section is called Traffic offences");
        Check(en.TryGetValue("reallife_offence_jaywalk", out string jay) && jay.Contains("jaywalking") && en.TryGetValue("reallife_offence_cross_red", out string cross) && cross.Contains("crossing on red"),
            "traffic offences: jaywalking and crossing on red are two switches, each named for what it is");
        Check(en.TryGetValue("reallife_guide_crime", out string guide) && guide.Contains("<b>Traffic offences</b>") && !guide.Contains("Police stop you for traffic offences")
              && guide.Contains("Each kind has its own switch under") && guide.Contains("None of this applies while a vehicle carries you, whoever drives it")
              && guide.Contains("A ride from another mod is told by what the game shows of it"),
            "traffic offences: the Crime page of the Guide names the section, says each kind has its own switch, says a vehicle that carries you counts for nothing on foot, and states the limit of how a ride is told");
        Check(AnyChangesRow(en, "Each traffic offence now has its own switch in the options, under Traffic offences")
              && AnyChangesRow(en, "Fixed: You are no longer fined for jaywalking or crossing on red while a taxi, a private driver or any other vehicle carries you, including one from another mod."),
            "traffic offences: What's new has the feature's line and the fix, each in a row (any row, as the version step moves the next row into its own)");
    }
}
