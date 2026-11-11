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
//
// The reviewers' repair. (1) The old switch's value was kept in memory only: the game's toggle on the options page reads an option with no
// saved value as the shipped "on" and sets it live, so opening Options > Mods turned all eight back on, the next start turned them off
// again, a player who then set one row kept the other seven following a stale key, and the game's reset, which deletes the eight keys and
// not the old one, was undone a frame later. It is written now as each option's own saved value, once, and the old key is deleted
// (OffenceRules.TakeOlderSwitch, Settings.MoveOffences); an older version's settings file with a line for it sets all eight
// (Presets.Import). The sequence is played here against a pretend PlayerPrefs and a pretend options page (OffenceMoveRules), the first
// version of the fix included, to show that it is caught. (2) The checks of the foot look read the text of the call, so a part of
// the change could be lost with them passing: each step from the game to the decision is now read in its own body (PlayerRide.Read,
// its helpers, PlayerOffences.Carried, Rest, Reset), and each is shown to fail when changed (CatchesChange).
internal static partial class Program
{
    // The kinds, in the order OffenceSwitches takes its switches.
    private static readonly Offence[] OffenceKinds =
        { Offence.RedLight, Offence.Speeding, Offence.Reckless, Offence.WrongSide, Offence.Jaywalk, Offence.CrossOnRed, Offence.DrunkDriving };

    // The ids the options page knows them by, in the same order, and the horn's, which is not an Offence.
    private static readonly string[] OffenceIds =
        { "offence_red_light", "offence_speeding", "offence_pavement", "offence_wrong_way", "offence_jaywalk", "offence_cross_red", "offence_drink" };
    private const string OffenceHornId = "offence_horn";
    private const string OffenceOlder = "player_offences";   // the one switch an older version had for all of them

    // The eight options in the order Settings.OffenceIds hands them to OffenceRules.TakeOlderSwitch, the horn last.
    private static readonly string[] OffenceAllIds =
        { OffenceIds[0], OffenceIds[1], OffenceIds[2], OffenceIds[3], OffenceIds[6], OffenceIds[4], OffenceIds[5], OffenceHornId };

    private static void TrafficOffences()
    {
        OffenceSwitchRules();
        OffenceMoveRules();
        OffenceRideRules();
        OffenceCost();
        OffenceSources();
        OffenceMovePins();
        OffenceRidePins();
        OffenceWords();
    }

    private static OffenceSwitches OffencesWith(int on)   // bit i: kind i of OffenceKinds is on
    {
        return new OffenceSwitches((on & 1) != 0, (on & 2) != 0, (on & 4) != 0, (on & 8) != 0, (on & 16) != 0, (on & 32) != 0, (on & 64) != 0);
    }

    private static RideFacts RideWith(int signs)   // bit i: the i-th sign of a ride is on (own vehicle, taxi, seat, blocker, agent, parents, speed)
    {
        return new RideFacts(ownVehicle: (signs & 1) != 0, taxiTrip: (signs & 2) != 0, seated: (signs & 4) != 0, blocked: (signs & 8) != 0,
            agentOff: (signs & 16) != 0, underVehicle: (signs & 32) != 0, tooFast: (signs & 64) != 0);
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

    // ---------------------------------------------------------------- the older switch becomes the eight

    // The saved values, by id, as the game's PlayerPrefs hold them (the key format is Settings' own: OffenceMovePins).
    private sealed class PretendOffencePrefs : IOffenceStore
    {
        public readonly Dictionary<string, int> Kept = new Dictionary<string, int>(StringComparer.Ordinal);
        public int Writes;

        public bool Has(string id) => Kept.ContainsKey(id);

        public int Get(string id) => Kept.TryGetValue(id, out int value) ? value : 0;

        public void Set(string id, int value)
        {
            Kept[id] = value;
            Writes++;
        }

        public void Clear(string id)
        {
            Kept.Remove(id);
        }
    }

    // One run of the game: the eight Settings.Offence* fields, the saved values, and what the game's options page does to them.
    // seedIsWritten false is the first version of the fix, whose seed lived in memory only (Settings.Load read the older key as a
    // fallback and wrote nothing): it stays here so that the sequences below are shown to catch it.
    private sealed class PretendOffenceGame
    {
        private readonly PretendOffencePrefs _prefs;
        private readonly bool _seedIsWritten;
        public readonly bool[] Field = new bool[8];   // Settings.OffenceRedLight ... OffenceHorn, in OffenceAllIds' order
        public readonly bool[] Shown = new bool[8];   // the page's rows

        public PretendOffenceGame(PretendOffencePrefs prefs, bool seedIsWritten)
        {
            _prefs = prefs;
            _seedIsWritten = seedIsWritten;
            for (int i = 0; i < Field.Length; i++)
            {
                Field[i] = true;   // the shipped values, until the first read
            }
            Load();
        }

        // Settings.Load for the eight: the older switch is taken over first; then each reads its saved value, else keeps what it holds.
        public void Load()
        {
            if (_seedIsWritten)
            {
                OffenceRules.TakeOlderSwitch(_prefs, OffenceOlder, OffenceAllIds);
            }
            for (int i = 0; i < Field.Length; i++)
            {
                string id = OffenceAllIds[i];
                if (_prefs.Has(id))
                {
                    Field[i] = _prefs.Get(id) != 0;
                }
                else if (!_seedIsWritten && _prefs.Has(OffenceOlder))
                {
                    Field[i] = _prefs.Get(OffenceOlder) != 0;
                }
            }
        }

        // Options > Mods opened: the game's ModOptionsToggleControl.Initialize on every row. The mod registers each row with the shipped
        // "on" as its default (SettingsTable.WithTrueDefault), so the row shows ModOptionPrefs.Load(id, true): its saved value, else on;
        // and then calls OnValueChanged with it, which sets the field.
        public void OpenPage()
        {
            for (int i = 0; i < Field.Length; i++)
            {
                bool on = _prefs.Has(OffenceAllIds[i]) ? _prefs.Get(OffenceAllIds[i]) != 0 : true;
                Shown[i] = on;
                Field[i] = on;
            }
        }

        // A click on a row: ModOptionPrefs.Save, then OnValueChanged.
        public void Click(string id, bool on)
        {
            int i = Array.IndexOf(OffenceAllIds, id);
            _prefs.Set(id, on ? 1 : 0);
            Shown[i] = on;
            Field[i] = on;
        }

        // The game's reset with the page open: ClearOptionPrefs deletes the value of every registered option (the eight; the older key
        // is no option), the rows are drawn again at their defaults, and a frame later the mod reads its options again (GameReset).
        public void GameReset()
        {
            foreach (string id in OffenceAllIds)
            {
                _prefs.Clear(id);
            }
            OpenPage();
            Load();
        }

        // What the player sees is what the police act on.
        public bool Agrees()
        {
            return SameValues(Field, Shown);
        }
    }

    private static bool AllEqual(bool[] values, bool wanted)
    {
        foreach (bool value in values)
        {
            if (value != wanted)
            {
                return false;
            }
        }
        return true;
    }

    private static bool SameValues(bool[] a, bool[] b)
    {
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i] != b[i])
            {
                return false;
            }
        }
        return a.Length == b.Length;
    }

    private static bool AllSaved(PretendOffencePrefs prefs, int value)
    {
        foreach (string id in OffenceAllIds)
        {
            if (!prefs.Has(id) || prefs.Get(id) != value)
            {
                return false;
            }
        }
        return true;
    }

    // Only the one row on (or off): the others are what the rest are.
    private static bool OnlyThisOne(bool[] fields, string id, bool thisOne)
    {
        for (int i = 0; i < fields.Length; i++)
        {
            if (fields[i] != (OffenceAllIds[i] == id ? thisOne : !thisOne))
            {
                return false;
            }
        }
        return true;
    }

    // What Presets.Import does with the lines of a file, as far as the traffic offences go: a line for an offence sets that option, a line
    // for the older switch sets all eight, each at its place in the file (the last line for an option stands).
    private static void ImportLines(PretendOffencePrefs prefs, params string[] lines)
    {
        foreach (string line in lines)
        {
            string[] part = line.Split('=');
            int value = int.Parse(part[1]) != 0 ? 1 : 0;
            if (part[0] == OffenceOlder)
            {
                foreach (string id in OffenceAllIds)
                {
                    prefs.Set(id, value);
                }
            }
            else if (Array.IndexOf(OffenceAllIds, part[0]) >= 0)
            {
                prefs.Set(part[0], value);
            }
        }
    }

    private static void OffenceMoveRules()
    {
        string jaywalk = OffenceIds[4];

        // The reviewers' sequence. An old install that had traffic offences off: player_offences = 0 and none of the eight saved.
        var off = new PretendOffencePrefs();
        off.Kept[OffenceOlder] = 0;
        var game = new PretendOffenceGame(off, true);
        bool offAtStart = AllEqual(game.Field, false);
        bool[] atStart = (bool[])game.Field.Clone();
        game.OpenPage();
        Check(offAtStart && SameValues(game.Field, atStart) && AllEqual(game.Shown, false) && game.Agrees(),
            "older switch off: the start reads all eight off, and opening Options > Mods leaves them off, as the rows show: nothing flips back on");
        Check(AllSaved(off, 0) && !off.Has(OffenceOlder),
            "older switch off: each of the eight is saved off at the first read and the older key is gone, so no row follows a stale key");

        // Then the player sets only one row. It has to keep: the other seven were saved off with it.
        game.Click(jaywalk, true);
        var next = new PretendOffenceGame(off, true);   // the next start of the game
        bool kept = OnlyThisOne(next.Field, jaywalk, true);
        next.OpenPage();
        Check(kept && OnlyThisOne(next.Field, jaywalk, true) && next.Agrees(),
            "older switch off, then jaywalking on: at every start and every opening of the page, jaywalking is on and the other seven are off");

        // The game's own reset. It deletes the eight, not the older key; the rows come back on; a frame later the mod reads its options.
        next.GameReset();
        var after = new PretendOffenceGame(off, true);
        Check(AllEqual(next.Field, true) && AllEqual(next.Shown, true) && next.Agrees() && AllEqual(after.Field, true) && !off.Has(OffenceOlder),
            "older switch off, then the game's reset: all eight are on, as the rows show, and stay on at the next start: no stale key seeds them off again");

        // The same install with the older switch on, the common one: it is the default, and one row can still be turned off.
        var onPrefs = new PretendOffencePrefs();
        onPrefs.Kept[OffenceOlder] = 1;
        var onGame = new PretendOffenceGame(onPrefs, true);
        onGame.OpenPage();
        bool allOn = AllEqual(onGame.Field, true) && AllSaved(onPrefs, 1) && !onPrefs.Has(OffenceOlder);
        onGame.Click(jaywalk, false);
        var onNext = new PretendOffenceGame(onPrefs, true);
        onNext.OpenPage();
        Check(allOn && OnlyThisOne(onNext.Field, jaywalk, false) && onNext.Agrees(),
            "older switch on: all eight start on, and jaywalking off with the rest on (the wish that began this) holds at every start");

        // A new player, or any start after the first: nothing to take over, nothing written.
        var fresh = new PretendOffencePrefs();
        var freshGame = new PretendOffenceGame(fresh, true);
        freshGame.OpenPage();
        Check(AllEqual(freshGame.Field, true) && fresh.Writes == 0 && fresh.Kept.Count == 0 && OffenceRules.TakeOlderSwitch(fresh, OffenceOlder, OffenceAllIds) == 0,
            "no older switch: all eight are on, and neither the start nor the page writes anything");
        int writes = off.Writes;
        Check(OffenceRules.TakeOlderSwitch(off, OffenceOlder, OffenceAllIds) == 0 && off.Writes == writes,
            "the older switch is taken over once: a second read writes nothing and changes nothing");

        // A value the player saved is never changed, whichever way the older switch stood.
        var mixedOn = new PretendOffencePrefs();
        mixedOn.Kept[OffenceOlder] = 1;
        mixedOn.Kept[jaywalk] = 0;
        var mixedOnGame = new PretendOffenceGame(mixedOn, true);
        var mixedOff = new PretendOffencePrefs();
        mixedOff.Kept[OffenceOlder] = 0;
        mixedOff.Kept[jaywalk] = 1;
        var mixedOffGame = new PretendOffenceGame(mixedOff, true);
        Check(OnlyThisOne(mixedOnGame.Field, jaywalk, false) && mixedOn.Writes == 7 && !mixedOn.Has(OffenceOlder)
              && OnlyThisOne(mixedOffGame.Field, jaywalk, true) && mixedOff.Writes == 7 && !mixedOff.Has(OffenceOlder),
            "an option the player saved keeps its value whatever the older switch said: only the seven others take it over");

        // A saved number that is not 0 or 1 is on, as the game reads any number.
        var odd = new PretendOffencePrefs();
        odd.Kept[OffenceOlder] = 2;
        var oddGame = new PretendOffenceGame(odd, true);
        var below = new PretendOffencePrefs();
        below.Kept[OffenceOlder] = -1;
        var belowGame = new PretendOffenceGame(below, true);
        Check(AllEqual(oddGame.Field, true) && AllSaved(odd, 1) && AllEqual(belowGame.Field, true) && AllSaved(below, 1),
            "the older switch saved as 2 or -1 is on, and is written to the eight as 1");

        // An older version's settings file. There the Relaxed preset was applied: the file keeps the value before (player_offences=1),
        // PlayerPrefs the value after (0). After the update Import undoes it: the file's line sets all eight, and no stale key is left.
        var undo = new PretendOffencePrefs();
        undo.Kept[OffenceOlder] = 0;
        var undoGame = new PretendOffenceGame(undo, true);
        ImportLines(undo, "player_offences=1");
        undoGame.Load();
        undoGame.OpenPage();
        Check(AllEqual(undoGame.Field, true) && undoGame.Agrees() && AllSaved(undo, 1) && !undo.Has(OffenceOlder),
            "an older version's settings file: its player_offences line sets all eight, so Import undoes a preset made before the update");
        var mixedFile = new PretendOffencePrefs();
        ImportLines(mixedFile, "player_offences=0", "offence_jaywalk=1");
        var mixedFileBack = new PretendOffencePrefs();
        ImportLines(mixedFileBack, "offence_jaywalk=1", "player_offences=0");
        Check(OnlyThisOne(new PretendOffenceGame(mixedFile, true).Field, jaywalk, true) && AllSaved(mixedFileBack, 0),
            "a file with both: a line stands where it is, so a later line for an option beats the older switch's, and the older switch's beats an earlier one");

        // The first version of the fix, kept to show that these sequences catch it: its seed was never written.
        var seedOff = new PretendOffencePrefs();
        seedOff.Kept[OffenceOlder] = 0;
        var seedOnly = new PretendOffenceGame(seedOff, false);
        bool seededOff = AllEqual(seedOnly.Field, false);
        seedOnly.OpenPage();
        Check(seededOff && AllEqual(seedOnly.Field, true) && seedOff.Writes == 0,
            "the seed kept in memory only (the first version): the page turns all eight back on at once, which the sequence above would show");
        var seedReset = new PretendOffenceGame(seedOff, false);
        seedReset.GameReset();
        Check(AllEqual(seedReset.Shown, true) && AllEqual(seedReset.Field, false) && !seedReset.Agrees(),
            "the seed kept in memory only (the first version): after the game's reset the rows say on and the police act on off, which the reset sequence would show");
    }

    // ---------------------------------------------------------------- nobody carried is walking

    private static void OffenceRideRules()
    {
        Check(!OffenceRules.Carried(RideWith(0)), "ride: a player with no sign of a ride is on foot");
        string[] signNames = { "the game's own vehicle", "a taxi trip", "a seat", "the game's walking blocker", "the NavMeshAgent switched off", "a vehicle among the parents", "a speed no walker reaches" };
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
        for (int signs = 0; signs < 128; signs++)
        {
            counted &= OffenceRules.Carried(RideWith(signs)) == (signs != 0);
        }
        Check(counted, "ride: of all 128 combinations of signs, every one but none at all is a ride");
        Check(OffenceRules.Signs(RideWith(127)).Split(',').Length == 7, "ride: with every sign on the log names all seven");

        // Each name given to the constructor lands in the field of that name: a sign answered into another's place would still be a ride.
        bool wired = true;
        for (int sign = 0; sign < signNames.Length; sign++)
        {
            RideFacts ride = RideWith(1 << sign);
            bool[] fields = { ride.OwnVehicle, ride.TaxiTrip, ride.Seated, ride.Blocked, ride.AgentOff, ride.UnderVehicle, ride.TooFast };
            for (int i = 0; i < fields.Length; i++)
            {
                wired &= fields[i] == (i == sign);
            }
        }
        Check(wired, "ride: each sign given to the constructor is the field of its name, and no other");

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
            hits += OffenceRules.Carried(new RideFacts((i & 1) != 0, (i & 2) != 0, (i & 4) != 0, (i & 8) != 0, (i & 16) != 0, (i & 32) != 0, OffenceRules.TooFast(i & 31, 0.5f))) ? 1 : 0;
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
        string presets = ModSource(@"Meta\Presets.cs");

        // One option for each kind: saved under its id, on by default, read from its own saved value, on the page, in the presets.
        string[] fields = { "OffenceRedLight", "OffenceSpeeding", "OffencePavement", "OffenceWrongWay", "OffenceJaywalk", "OffenceCrossRed", "OffenceDrink", "OffenceHorn" };
        string[] ids = { OffenceIds[0], OffenceIds[1], OffenceIds[2], OffenceIds[3], OffenceIds[4], OffenceIds[5], OffenceIds[6], OffenceHornId };
        string[] labels = { "red_light", "speeding", "pavement", "wrong_way", "jaywalk", "cross_red", "drink", "horn" };
        string missing = string.Empty;
        for (int i = 0; i < fields.Length; i++)
        {
            string f = fields[i];
            bool whole = settings.Contains("public const string " + f + "Id = \"" + ids[i] + "\";") && settings.Contains("public static bool " + f + " = true;")
                         && settings.Contains(f + " = Bool(modId, " + f + "Id, " + f + ");")
                         && mod.Contains("AddToggle(Settings." + f + "Id, \"reallife_offence_" + labels[i] + "\", Settings." + f + ",") && mod.Contains("v => Settings." + f + " = v)")
                         && list.Contains(".Preset(Settings." + f + "Id, 0, 1, 1)") && tree.Contains("\"" + ids[i] + "\"");
            missing += whole ? string.Empty : " " + ids[i];
        }
        Check(missing.Length == 0, "traffic offences: each of the eight kinds is saved under its id, on by default, read from its own saved value, on the page, in the presets and in the tree:" + missing);
        Check(settings.Contains("new OffenceSwitches(OffenceRedLight, OffenceSpeeding, OffencePavement, OffenceWrongWay, OffenceJaywalk, OffenceCrossRed, OffenceDrink)"),
            "traffic offences: Settings.Offences hands the looks the seven switches in the order the rules take them");
        Check(settings.Contains("internal const string OffencesOlderSwitch = \"player_offences\";") && !settings.Contains("PlayerOffencesId") && !settings.Contains("public static bool PlayerOffences"),
            "traffic offences: the old switch is no longer an option of the page: only its saved key is read, to hand its value to the new ones");
        string readers = string.Empty;   // (PlayerOffences is also the name of the class that finds the offences: it is the switch that is gone)
        foreach (KeyValuePair<string, string> source in new[]
                 { new KeyValuePair<string, string>("Settings", settings), new KeyValuePair<string, string>("the mod's page", mod), new KeyValuePair<string, string>("the feature list", list),
                   new KeyValuePair<string, string>("the tree", tree), new KeyValuePair<string, string>("PoliceAttention", attention), new KeyValuePair<string, string>("the stop", stop),
                   new KeyValuePair<string, string>("PlayerOffences", offences), new KeyValuePair<string, string>("the horn", horn), new KeyValuePair<string, string>("Presets", presets) })
        {
            // (Settings alone keeps the old key as a constant, and Presets reads the constant: they take the old key's value to the new switches)
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
        Check(attention.Contains("_offences.OnFoot(player, now, on)") && !attention.Contains("PlayerHelper.IsUsingVehicle || now < _nextFootOffence"),
            "ride: the foot look is no longer left to the save's active vehicle alone");
        Check(!ride.Contains("FindObject") && !ride.Contains("GetComponentsIn") && !ride.Contains("new List") && !ride.Contains("Physics.") && !ride.Contains("System.Linq"),
            "ride: the signs are read with no search, no physics query and no allocation");
        Check(attention.Contains("Meta.Dev.Always(Logger, \"Police attention: \" + offence + \" found on foot")
              && attention.Contains("OffenceRules.Signs(_offences.Ride)"),
            "ride: a foot offence always writes the signs it found, so a ride the signs missed shows in a player's Player.log");

        // The rides of the mod's own that the player sits in are among those the signs tell: each sets the game's walking blocker.
        string bus = ModSource(@"Transport\BusRider.cs"), busRide = ModSource(@"Transport\BusRider.Ride.cs"), transit = ModSource(@"Transport\TransitRide.cs");
        Check(bus.Contains("private const NavigationBlocker Blocker = NavigationBlocker.Vehicle;") && busRide.Contains("_player.SetNavigationBlocker(Blocker);")
              && transit.Contains("_player.SetNavigationBlocker(NavigationBlocker.Taxi);"),
            "ride: the mod's own bus and taxi rides set the game's walking blocker, which the foot look reads as a ride");
    }

    // ---------------------------------------------------------------- the older switch, as the game's code does it

    // The code with every run of white space made one space, so that a line break or an indent changes nothing in what a check asks of it.
    private static string Squeezed(string code)
    {
        return Regex.Replace(code, @"\s+", " ").Trim();
    }

    // Whether the statements stand in the code in this order, each after the one before, white space aside. Empty code holds none.
    private static bool StatementsInOrder(string code, params string[] statements)
    {
        string text = Squeezed(code);
        int from = 0;
        foreach (string statement in statements)
        {
            string wanted = Squeezed(statement);
            int at = text.IndexOf(wanted, from, StringComparison.Ordinal);
            if (at < 0)
            {
                return false;
            }
            from = at + wanted.Length;
        }
        return true;
    }

    // True when the check passes the source as it is and fails it with one piece of it changed. The code these checks read cannot run
    // here (it names the game), so each is shown to fail on the change it is there to catch: a check that cannot fail checks nothing.
    private static bool CatchesChange(Func<string, bool> check, string source, string from, string to)
    {
        return source.Contains(from) && check(source) && !check(source.Replace(from, to));
    }

    // Settings.Load takes the older switch over before it reads any of the eight, and reads each from its own saved value.
    private static bool LoadTakesItOverFirst(string settings, string[] fields)
    {
        string load = Squeezed(MethodText(settings, "private static void Load(string modId)"));
        int move = load.IndexOf("MoveOffences(modId);", StringComparison.Ordinal);
        if (move < 0)
        {
            return false;
        }
        foreach (string f in fields)
        {
            if (load.IndexOf(f + " = Bool(modId, " + f + "Id, " + f + ");", StringComparison.Ordinal) < move)
            {
                return false;
            }
        }
        return true;
    }

    private static bool MoveOffencesHandsItOver(string settings)
    {
        return StatementsInOrder(MemberText(settings, "private static void MoveOffences(string modId)"),
            "OffenceRules.TakeOlderSwitch(new OffencePrefs(modId), OffencesOlderSwitch, OffenceIds);");
    }

    // The store the rule writes through is PlayerPrefs under the game's own key, whole: has, get, set and delete.
    private static bool OffencePrefsAreThePlayerPrefs(string settings)
    {
        return StatementsInOrder(MemberText(settings, "private readonly struct OffencePrefs : IOffenceStore"),
            "public bool Has(string id) => UnityEngine.PlayerPrefs.HasKey(Key(_modId, id));",
            "public int Get(string id) => UnityEngine.PlayerPrefs.GetInt(Key(_modId, id), 0);",
            "public void Set(string id, int value) => UnityEngine.PlayerPrefs.SetInt(Key(_modId, id), value);",
            "public void Clear(string id) => UnityEngine.PlayerPrefs.DeleteKey(Key(_modId, id));");
    }

    // OffenceIds holds the eight, each once.
    private static bool OffenceIdsHoldEight(string settings, string[] fields)
    {
        Match initial = Regex.Match(settings, @"internal static readonly string\[\] OffenceIds =\s*\{([^}]*)\};");
        if (!initial.Success)
        {
            return false;
        }
        string[] named = initial.Groups[1].Value.Split(',');
        int found = 0;
        foreach (string f in fields)
        {
            int times = 0;
            foreach (string name in named)
            {
                times += name.Trim() == f + "Id" ? 1 : 0;
            }
            found += times == 1 ? 1 : 0;
        }
        return found == fields.Length && named.Length == fields.Length;
    }

    // An older version's settings file: its line for the one switch sets each of the eight, before the line for any other option is read.
    private static bool ImportTakesTheOlderLine(string presets)
    {
        string import = MemberText(presets, "public static string Import()");
        string put = MemberText(presets, "private static void Put(OptionInfo info, int value, List<string> reload)");
        return StatementsInOrder(import,
                   "if (keyIds.Contains(id))",
                   "if (id == Settings.OffencesOlderSwitch)",
                   "foreach (string offence in Settings.OffenceIds)",
                   "OptionInfo each = SettingsTable.Get(offence);",
                   "if (each != null)",
                   "Put(each, value, reload);",
                   "continue;",
                   "OptionInfo info = SettingsTable.Get(id);",
                   "Put(info, value, reload);")
               && StatementsInOrder(put, "value = SettingsTable.Clamp(info, value);", "SettingsTable.Write(info, value);");
    }

    private static void OffenceMovePins()
    {
        string settings = ModSource(@"Core\Settings.cs"), presets = ModSource(@"Meta\Presets.cs");
        string[] fields = { "OffenceRedLight", "OffenceSpeeding", "OffencePavement", "OffenceWrongWay", "OffenceJaywalk", "OffenceCrossRed", "OffenceDrink", "OffenceHorn" };

        Check(LoadTakesItOverFirst(settings, fields) && !settings.Contains("OffenceSwitch(") && !settings.Contains("Seeded"),
            "older switch: Settings.Load takes it over before it reads the eight, and each is then read from its own saved value, with no seed kept in memory");
        Check(CatchesChange(s => LoadTakesItOverFirst(s, fields), settings, "MoveOffences(modId);   // before", "// before")
              && CatchesChange(s => LoadTakesItOverFirst(s, fields), settings, "OffenceHorn = Bool(modId, OffenceHornId, OffenceHorn);", "OffenceHorn = OffenceHorn;"),
            "older switch: that check fails when the take-over is dropped from the read, and when one of the eight is no longer read");
        Check(MoveOffencesHandsItOver(settings) && CatchesChange(MoveOffencesHandsItOver, settings, "OffencesOlderSwitch, OffenceIds);", "OffencesOlderSwitch, new string[0]);"),
            "older switch: the take-over hands the rule the older key's id and the eight ids");
        Check(OffencePrefsAreThePlayerPrefs(settings)
              && CatchesChange(OffencePrefsAreThePlayerPrefs, settings, "SetInt(Key(_modId, id), value);", "SetInt(Key(_modId, id), 0);")
              && CatchesChange(OffencePrefsAreThePlayerPrefs, settings, "DeleteKey(Key(_modId, id));", "HasKey(Key(_modId, id));")
              && CatchesChange(OffencePrefsAreThePlayerPrefs, settings, "public bool Has(string id) => UnityEngine.PlayerPrefs.HasKey(", "public bool Has(string id) => !UnityEngine.PlayerPrefs.HasKey("),
            "older switch: the store it writes through is PlayerPrefs under the game's own key (has, get, set, delete), and each of those is shown to matter");
        Check(OffenceIdsHoldEight(settings, fields) && CatchesChange(s => OffenceIdsHoldEight(s, fields), settings, "OffenceDrinkId, OffenceJaywalkId", "OffenceJaywalkId, OffenceJaywalkId"),
            "older switch: the list handed to the rule holds the ids of all eight options, each once");
        Check(ImportTakesTheOlderLine(presets)
              && CatchesChange(ImportTakesTheOlderLine, presets, "if (id == Settings.OffencesOlderSwitch)", "if (id == \"not_it\")")
              && CatchesChange(ImportTakesTheOlderLine, presets, "foreach (string offence in Settings.OffenceIds)", "foreach (string offence in new string[0])")
              && CatchesChange(ImportTakesTheOlderLine, presets, "Put(each, value, reload);", "value = 0;")
              && CatchesChange(ImportTakesTheOlderLine, presets, "SettingsTable.Write(info, value);", "value = 0;"),
            "older switch: Import reads an older version's player_offences line as that value for each of the eight, and each part of that is shown to matter");
    }

    // ---------------------------------------------------------------- the foot look, step by step

    // PlayerRide.Read: one named line for each sign, each answered by its own read of the game.
    private static bool RideReadsEverySign(string ride)
    {
        return StatementsInOrder(MemberText(ride, "public static RideFacts Read(PlayerController player, float movedSqr, float seconds)"),
            "return new RideFacts(",
            "ownVehicle: PlayerHelper.IsUsingVehicle,",
            "taxiTrip: TaxiTrip(),",
            "seated: Seated(player),",
            "blocked: Blocked(player),",
            "agentOff: AgentOff(player),",
            "underVehicle: UnderVehicle(player.transform),",
            "tooFast: OffenceRules.TooFast(movedSqr, seconds));");
    }

    // One helper of PlayerRide: its signature, what it holds in order, and the one answer that, made false, is a sign lost.
    private sealed class RideHelper
    {
        public readonly string Signature, Answer;
        public readonly string[] Statements;

        public RideHelper(string signature, string answer, params string[] statements)
        {
            Signature = signature;
            Answer = answer;
            Statements = statements;
        }
    }

    // The helpers of PlayerRide, each what it says.
    private static readonly RideHelper[] RideHelpers =
    {
        new RideHelper("private static bool TaxiTrip()", "return InstanceBehavior<TaxiSystem>.IsInitialized && TaxiSystem.IsTraveling;",
            "return InstanceBehavior<TaxiSystem>.IsInitialized && TaxiSystem.IsTraveling;"),
        new RideHelper("private static bool Seated(PlayerController player)", "return player.Character != null && player.Character.isSittingOn != null;",
            "return player.Character != null && player.Character.isSittingOn != null;"),
        new RideHelper("private static bool Blocked(PlayerController player)", "return player.NavigationDisabled;",
            "return player.NavigationDisabled;"),
        new RideHelper("private static bool AgentOff(PlayerController player)", "return agent != null && !agent.enabled;",
            "NavMeshAgent agent = player.Character != null ? player.Character.navmeshAgent : null;", "return agent != null && !agent.enabled;"),
        new RideHelper("private static bool UnderVehicle(Transform body)", "return true;",
            "for (Transform up = body.parent; up != null; up = up.parent)",
            "if (up.TryGetComponent(out VehicleComponent _) || up.TryGetComponent(out VehicleController _))", "return true;", "return false;")
    };

    private static bool RideHelperIs(string ride, RideHelper helper)
    {
        return StatementsInOrder(MemberText(ride, helper.Signature), helper.Statements);
    }

    // PlayerOffences.Carried: the time since the last look, the signs read, the decision from them, the change noted and logged, the answer.
    private static bool CarriedDecidesFromTheSigns(string offences)
    {
        return StatementsInOrder(MemberText(offences, "private bool Carried(PlayerController player, Vector3 position, float now)"),
            "float seconds = _footAt < 0f ? 0f : now - _footAt;",
            "RideFacts ride = PlayerRide.Read(player, Flat(position - _lastFoot), seconds);",
            "Ride = ride;",
            "bool carried = OffenceRules.Carried(ride);",
            "if (carried != _carried)",
            "_carried = carried;",
            "if (Meta.Dev.Verbose)",
            "PoliceAttention.Logger?.Info(carried",
            "return carried;");
    }

    // OnFoot: the gate comes first and a carried player is let rest, with no officer sought and nothing found.
    private static bool OnFootAsksFirst(string offences)
    {
        return StatementsInOrder(MemberText(offences, "public Offence OnFoot(PlayerController player, float now, OffenceSwitches on)"),
            "Vector3 position = player.transform.position;",
            "if (!on.OnFoot || Carried(player, position, now))",
            "Rest(position, now);",
            "return Offence.None;",
            "Witness = OfficerNear(position, OfficerSight);");
    }

    // What the look keeps through a ride: the road timer is dropped; the place and time are kept, so the speed is measured through it.
    private static bool RestKeepsPlaceAndTime(string offences)
    {
        return StatementsInOrder(MemberText(offences, "private void Rest(Vector3 position, float now)"),
            "_roadSince = -1f;", "_lastFoot = position;", "_footAt = now;", "Witness = null;");
    }

    // A reset forgets the time of the last look and the ride it found, so the first look after it measures no speed.
    private static bool ResetForgetsTheLook(string offences)
    {
        return StatementsInOrder(MemberText(offences, "public void Reset()"), "_roadSince = -1f;", "_footAt = -1f;", "_carried = false;", "Witness = null;");
    }

    private static void OffenceRidePins()
    {
        string ride = ModSource(@"Crime\PlayerRide.cs"), offences = ModSource(@"Crime\PlayerOffences.cs");

        // The path from the game to the decision, in PlayerRide: the arguments of Read, and what each helper answers with.
        bool reads = RideReadsEverySign(ride);
        string[] signs = { "ownVehicle: PlayerHelper.IsUsingVehicle,", "taxiTrip: TaxiTrip(),", "seated: Seated(player),", "blocked: Blocked(player),", "agentOff: AgentOff(player),",
                           "underVehicle: UnderVehicle(player.transform),", "tooFast: OffenceRules.TooFast(movedSqr, seconds));" };
        bool each = reads;
        foreach (string sign in signs)
        {
            string name = sign.Substring(0, sign.IndexOf(':'));
            each &= CatchesChange(RideReadsEverySign, ride, sign, name + ": false" + (sign.EndsWith(";", StringComparison.Ordinal) ? ");" : ","));
        }
        Check(reads && each, "ride: PlayerRide.Read gives the constructor all seven signs, each from its own read of the game, and answering any one of them with false is caught");
        bool helpers = true;
        string unsound = string.Empty;
        foreach (RideHelper helper in RideHelpers)
        {
            bool sound = RideHelperIs(ride, helper) && CatchesChange(s => RideHelperIs(s, helper), ride, helper.Answer, "return false;");
            unsound += sound ? string.Empty : " " + helper.Signature.Substring(helper.Signature.IndexOf("bool ", StringComparison.Ordinal) + 5);
            helpers &= sound;
        }
        Check(helpers, "ride: each of the game's signs is read as the game keeps it (its taxi trip, a seat, its walking blocker, the NavMeshAgent, a vehicle among the parents), and a helper answering false is caught:" + unsound);

        // The decision in PlayerOffences: the first look after a reset has no time, the signs are read, OffenceRules.Carried decides, the answer is the decision.
        Check(CatchesChange(CarriedDecidesFromTheSigns, offences, "bool carried = OffenceRules.Carried(ride);", "bool carried = false;")
              && CatchesChange(CarriedDecidesFromTheSigns, offences, "RideFacts ride = PlayerRide.Read(player, Flat(position - _lastFoot), seconds);", "RideFacts ride = default(RideFacts);")
              && CatchesChange(CarriedDecidesFromTheSigns, offences, "return carried;", "return false;")
              && CatchesChange(CarriedDecidesFromTheSigns, offences, "float seconds = _footAt < 0f ? 0f : now - _footAt;", "float seconds = 0f;"),
            "ride: PlayerOffences.Carried passes the time since the last look and the signs to the rule and answers with its decision, and a decision of false, no signs or no time is caught");
        Check(CatchesChange(OnFootAsksFirst, offences, "if (!on.OnFoot || Carried(player, position, now))", "if (!on.OnFoot)")
              && CatchesChange(OnFootAsksFirst, offences, "if (!on.OnFoot || Carried(player, position, now))", "if (!on.OnFoot || false)")
              && CatchesChange(OnFootAsksFirst, offences, "Rest(position, now);", string.Empty),
            "ride: the foot look asks first, and a carried player is let rest and nothing is found, and a gate left open, never asked or without its rest is caught");
        Check(CatchesChange(RestKeepsPlaceAndTime, offences, "_footAt = now;", string.Empty) && CatchesChange(RestKeepsPlaceAndTime, offences, "_lastFoot = position;", string.Empty),
            "ride: a ride keeps the place and time of each look, so that its speed is measured through it, and losing either is caught");
        Check(CatchesChange(ResetForgetsTheLook, offences, "_carried = false;", string.Empty) && CatchesChange(ResetForgetsTheLook, offences, "_footAt = -1f;", string.Empty),
            "ride: a reset forgets the time of the last look and the ride it found, and keeping either is caught");
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
