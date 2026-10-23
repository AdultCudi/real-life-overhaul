using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using RealLifeOverhaul.DistrictSave;

// The district save's rules (Scripts/DistrictSave, docs/features/district-save.md), game-free: whose a place is (the
// mod's own ids and what its features name), what each kind of record does (a business's records leave with it, the
// player and the player's cars stand in), the moves that take records out of a list or a queue and put them back where
// they stood, the store's line and its round trip, the game's line that a save's file is written, and that a game with
// nothing of the mod's costs nothing at a save.
internal static partial class Program
{
    private static void DistrictSaveChecks()
    {
        DistrictSaveIds();
        DistrictSavePlaces();
        DistrictSaveProviders();
        DistrictSaveListMoves();
        DistrictSaveQueueMoves();
        DistrictSaveText();
        DistrictSaveLines();
        DistrictSaveKinds();
        DistrictSaveWritten();
        DistrictSaveIdle();
    }

    private static void DistrictSaveIds()
    {
        Check(DistrictSaveRules.IsModId("reallife:street_airport_road") && DistrictSaveRules.IsModId("reallife_district_x") && DistrictSaveRules.IsModId("reallife-y"),
            "district save: an id that starts reallife and a separator is the mod's");
        Check(!DistrictSaveRules.IsModId("reallife") && !DistrictSaveRules.IsModId("reallifex") && !DistrictSaveRules.IsModId("Reallife:x") && !DistrictSaveRules.IsModId(null)
              && !DistrictSaveRules.IsModId(string.Empty),
            "district save: the prefix alone, another word, another case, none: not the mod's");
        Check(!DistrictSaveRules.IsModId("ba:street_broadway") && !DistrictSaveRules.IsModId("ba:neighborhood_midtown") && !DistrictSaveRules.IsModId("ba:neighborhood_jfk")
              && !DistrictSaveRules.IsModId("dlc:shanghai_bund"),
            "district save: the game's own streets and districts, a future area's and a DLC's, are never the mod's");
    }

    private static void DistrictSavePlaces()
    {
        var empty = new PlaceSet();
        Check(empty.Empty && empty.Addresses == 0 && empty.Streets == 0 && empty.Districts == 0 && empty.Exits == 0 && !empty.FirstExit(out PlaceExit _),
            "district save: a set no feature filled is empty");
        Check(empty.Names("reallife:street_x", 3) && empty.NamesDistrict("reallife:district_x") && !empty.Has("reallife:street_x", 3) && !empty.HasDistrict("reallife:district_x"),
            "district save: an id of the mod's is named for the save even with no feature, but never made: what is kept of it stays kept");
        Check(!empty.Names("ba:street_broadway", 12) && !empty.Names(null, 0) && !empty.Names(string.Empty, 0) && !empty.NamesDistrict("ba:neighborhood_midtown"),
            "district save: the game's own addresses and districts are never named");

        var set = new PlaceSet().Street("reallife:street_airport").Address("ba:street_thirteenthstreet", 905).District("reallife:district_airport")
                                 .Ground(100f, 200f, 0f, 0f).Exit(150f, 0f, 50f, 90f, "ba:neighborhood_industrycity").Exit(-50f, 0f, -50f, 180f, "ba:neighborhood_hamptons");
        Check(set.Has("reallife:street_airport", 1) && set.Has("reallife:street_airport", 9999) && set.Has("ba:street_thirteenthstreet", 905) && !set.Has("ba:street_thirteenthstreet", 904),
            "district save: a whole street of the mod's has every number; an address on a game street is that number alone");
        Check(set.Names("ba:street_thirteenthstreet", 905) && !set.Names("ba:street_thirteenthstreet", 1) && set.HasDistrict("reallife:district_airport"),
            "district save: a feature's address on a game street is named, the street's other numbers are the game's");
        Check(set.OnGround(50f, 100f) && set.OnGround(0f, 0f) && set.OnGround(100f, 200f) && !set.OnGround(101f, 50f) && !set.OnGround(50f, -0.5f),
            "district save: the mod's ground is its rectangle, edges included, corners in any order");
        Check(set.NearestExit(120f, 40f, out PlaceExit near) && near.District == "ba:neighborhood_industrycity" && set.NearestExit(-40f, -40f, out PlaceExit far)
              && far.District == "ba:neighborhood_hamptons" && set.FirstExit(out PlaceExit first) && first.Heading == 90f,
            "district save: the way out nearest a point is taken, by the ground plane");
        var ties = new PlaceSet().Exit(10f, 0f, 0f, 0f, "first").Exit(-10f, 0f, 0f, 0f, "second");
        Check(ties.NearestExit(0f, 0f, out PlaceExit tie) && tie.District == "first", "district save: between two ways out as near, the first named is taken (the same on every machine)");
        Check(!set.Empty && set.Streets == 1 && set.Addresses == 1 && set.Districts == 1 && set.GroundCount == 1 && set.Exits == 2,
            "district save: the set counts what it holds");
        var a = new PlaceSet().District("reallife:b").Street("reallife:a").Address("x", 2);
        var b = new PlaceSet().Address("x", 2).Street("reallife:a").District("reallife:b");
        Check(string.Join(";", a.Describe()) == string.Join(";", b.Describe()) && a.Describe().Count == 3,
            "district save: the set's account is the same in any order it was filled (ordinal)");
        var groundOnly = new PlaceSet().Ground(0f, 0f, 1f, 1f);
        Check(!groundOnly.Empty, "district save: ground alone is something to keep out (the player and the cars on it)");
    }

    private static void DistrictSaveProviders()
    {
        DistrictPlaces.Reset();
        Check(DistrictPlaces.Providers == 0 && DistrictPlaces.Current.Empty && DistrictPlaces.Build().Empty, "district save: no feature, no places");
        var calls = new List<string>();
        DistrictPlaces.Add("first", set =>
        {
            calls.Add("first");
            set.Street("reallife:street_one");
        });
        DistrictPlaces.Add("broken", set =>
        {
            calls.Add("broken");
            set.Street("reallife:street_half");
            throw new InvalidOperationException("its data is not there");
        });
        DistrictPlaces.Add("second", set =>
        {
            calls.Add("second");
            set.District("reallife:district_two");
        });
        PlaceSet built = DistrictPlaces.Build();
        Check(string.Join(",", calls) == "first,broken,second" && built.Has("reallife:street_one", 4) && built.HasDistrict("reallife:district_two") && !built.Has("reallife:street_half", 1),
            "district save: every feature fills in the order it registered; one that throws adds nothing, the others go on");
        Check(DistrictPlaces.LastFaults.Count == 1 && DistrictPlaces.LastFaults[0].StartsWith("broken: InvalidOperationException", StringComparison.Ordinal),
            "district save: the feature that threw is named for the log");
        DistrictPlaces.Add("first", set => set.Street("reallife:street_three"));
        DistrictPlaces.Remove("broken");
        DistrictPlaces.Changed();
        Check(DistrictPlaces.Stale && DistrictPlaces.Providers == 2, "district save: a change asks for the set to be built again; registering again under a name replaces it");
        PlaceSet again = DistrictPlaces.Build();
        Check(!DistrictPlaces.Stale && again.Has("reallife:street_three", 1) && !again.Has("reallife:street_one", 1) && DistrictPlaces.LastFaults.Count == 0
              && ReferenceEquals(DistrictPlaces.Current, again),
            "district save: the set built again has the new fill, and is the current one");
        DistrictPlaces.Reset();
        Check(DistrictPlaces.Providers == 0 && DistrictPlaces.Current.Empty, "district save: the checks leave no feature registered");
    }

    private static void DistrictSaveListMoves()
    {
        string[] items = { "a", "M1", "b", "c", "M2", "M3", "d", "M4" };
        var list = new List<string>(items);
        var taken = new List<Taken<string>>();
        int n = DistrictSaveRules.TakeOut(list, s => s.StartsWith("M", StringComparison.Ordinal), taken);
        Check(n == 4 && string.Join(",", list) == "a,b,c,d" && taken.Count == 4 && taken[0].Index == 1 && taken[3].Index == 7,
            "district save: what is taken out leaves its index; the rest close up in their order");
        DistrictSaveRules.PutBack(list, taken);
        Check(string.Join(",", list) == string.Join(",", items), "district save: put back, the list is exactly as it was (the first, the last, side by side)");

        var objects = new List<object> { new object(), new object(), new object() };
        object[] before = objects.ToArray();
        var gone = new List<Taken<object>>();
        DistrictSaveRules.TakeOut(objects, o => ReferenceEquals(o, before[0]) || ReferenceEquals(o, before[2]), gone);
        DistrictSaveRules.PutBack(objects, gone);
        Check(objects.Count == 3 && ReferenceEquals(objects[0], before[0]) && ReferenceEquals(objects[1], before[1]) && ReferenceEquals(objects[2], before[2]),
            "district save: the same objects go back, not copies, in their places");

        var none = new List<string> { "a", "b" };
        var noneTaken = new List<Taken<string>>();
        Check(DistrictSaveRules.TakeOut(none, s => false, noneTaken) == 0 && noneTaken.Count == 0 && none.Count == 2, "district save: nothing to take, nothing changes");
        var all = new List<string> { "M", "M" };
        var allTaken = new List<Taken<string>>();
        DistrictSaveRules.TakeOut(all, s => true, allTaken);
        Check(all.Count == 0 && allTaken.Count == 2, "district save: everything taken leaves the list empty");
        DistrictSaveRules.PutBack(all, allTaken);
        Check(all.Count == 2, "district save: and everything goes back");
        Check(DistrictSaveRules.TakeOut<string>(null, s => true, new List<Taken<string>>()) == 0 && DistrictSaveRules.TakeOut(new List<string>(), s => true, new List<Taken<string>>()) == 0,
            "district save: no list or an empty one: nothing");

        // A list that changed in between (a fallback a frame later): every item goes back, at its index or the end.
        var grown = new List<string> { "a", "M1", "b" };
        var grownTaken = new List<Taken<string>>();
        DistrictSaveRules.TakeOut(grown, s => s == "M1", grownTaken);
        grown.Clear();
        DistrictSaveRules.PutBack(grown, grownTaken);
        Check(grown.Count == 1 && grown[0] == "M1", "district save: into a list emptied in between, what was taken still goes back (at the end)");
        var unordered = new List<Taken<string>> { new Taken<string> { Index = 2, Item = "z" }, new Taken<string> { Index = 0, Item = "x" } };
        var target = new List<string> { "y" };
        DistrictSaveRules.PutBack(target, unordered);
        Check(string.Join(",", target) == "x,y,z", "district save: taken items go back in the order of their indices, whatever order they come in");

        // A large list, the largest a save holds and more: one pass, and no allocation but the taken list.
        var big = new List<int>(200000);
        for (int i = 0; i < 200000; i++)
        {
            big.Add(i);
        }
        var bigTaken = new List<Taken<int>>(2000);
        Predicate<int> mine = v => v % 100 == 7;
        var watch = Stopwatch.StartNew();
        DistrictSaveRules.TakeOut(big, mine, bigTaken);
        DistrictSaveRules.PutBack(big, bigTaken);
        double ms = watch.Elapsed.TotalMilliseconds;
        bool same = big.Count == 200000;
        for (int i = 0; same && i < big.Count; i++)
        {
            same = big[i] == i;
        }
        Check(same, "district save: 2,000 of 200,000 taken out and put back: the list exactly as it was");
        Check(ms < 200.0, "district save: 2,000 of 200,000 taken out and put back in under 200 ms (" + ms.ToString("0.0", CultureInfo.InvariantCulture) + " ms)");
    }

    private static void DistrictSaveQueueMoves()
    {
        var queue = new Queue<string>(new[] { "t1", "M1", "t2", "M2", "t3" });
        var taken = new List<Taken<string>>();
        var scratch = new List<string>();
        int n = DistrictSaveRules.TakeOut(queue, s => s[0] == 'M', taken, scratch);
        Check(n == 2 && string.Join(",", queue.ToArray()) == "t1,t2,t3" && scratch.Count == 0, "district save: a queue keeps its order with what is taken out gone");
        DistrictSaveRules.PutBack(queue, taken, scratch);
        Check(string.Join(",", queue.ToArray()) == "t1,M1,t2,M2,t3", "district save: put back, the queue reads as it did (the transactions' order)");
        var emptyQueue = new Queue<string>();
        Check(DistrictSaveRules.TakeOut(emptyQueue, s => true, new List<Taken<string>>(), scratch) == 0 && emptyQueue.Count == 0, "district save: an empty queue: nothing");
    }

    private static void DistrictSaveText()
    {
        string[] texts = { "plain", "a|b;c^d,e#f%g~h", "line\nbreak\r", string.Empty, "~", "%41", "über 区域 存档" };
        bool round = true;
        foreach (string s in texts)
        {
            string e = DistrictSaveRules.Escape(s);
            round &= DistrictSaveRules.Unescape(e) == s && e.IndexOf('|') < 0 && e.IndexOf(';') < 0 && e.IndexOf('^') < 0 && e.IndexOf(',') < 0 && e.IndexOf('#') < 0
                     && e.IndexOf('\n') < 0;
        }
        Check(round, "district save: every text, separators, the escape's own mark and the mark of none included, reads back exactly and holds no separator");
        Check(DistrictSaveRules.Escape(null) == "~" && DistrictSaveRules.Unescape("~") == null && DistrictSaveRules.Unescape(DistrictSaveRules.Escape("~")) == "~",
            "district save: none reads back as none, and the text \"~\" as itself");
        Check(DistrictSaveRules.Unescape("a%Zb%4") == "a%Zb%4", "district save: a broken escape is kept as it stands");
        Check(DistrictSaveRules.ReadPlace(DistrictSaveRules.Place("reallife:street#odd,name", -3), out string street, out int number) && street == "reallife:street#odd,name" && number == -3,
            "district save: an address reads back exactly, whatever its street's id holds");
        Check(!DistrictSaveRules.ReadPlace("no number", out string _, out int _) && !DistrictSaveRules.ReadPlace(null, out string _, out int _),
            "district save: what is not an address is not read as one");
        DistrictSaveRules.Yaw(90f, out float y90, out float w90);
        DistrictSaveRules.Yaw(0f, out float y0, out float w0);
        DistrictSaveRules.Yaw(-90f, out float yBack, out float wBack);
        Check(Math.Abs(y90 - 0.70710677f) < 1e-6f && Math.Abs(w90 - 0.70710677f) < 1e-6f && y0 == 0f && w0 == 1f && Math.Abs(yBack + 0.70710677f) < 1e-6f
              && Math.Abs(wBack - 0.70710677f) < 1e-6f,
            "district save: a way out's heading turns as Unity's Euler(0, heading, 0) does");
        Check(DistrictSaveRules.ReadNumber(DistrictSaveRules.Number(-1234.5678f)) == -1234.5678f && DistrictSaveRules.ReadWhole("x", 7) == 7,
            "district save: a position reads back to the bit; a whole number that is not one gives the default");
    }

    private static void DistrictSaveLines()
    {
        var reg = new StoredRecord(SaveKind.Registration, 12, null) { Slot = 0 };
        reg.Addresses.Add(new PlaceKey("reallife:street_a", 4));
        var demand = new StoredRecord(SaveKind.Demand, 3, "ba:itemname_apple|odd;key") { Slot = 1 };
        demand.Districts.Add("reallife:district_a");
        var player = new StoredRecord(SaveKind.Player, 0, null);
        player.Addresses.Add(new PlaceKey("reallife:street_a", 4));
        player.Values.AddRange(new[] { "reallife:street_a", "4", "1.5", "0", "-2.25", "0", "0.7071", "0", "0.7071", "inside" });
        var nul = new StoredRecord(SaveKind.Guider, 0, null);
        nul.Values.Add(null);
        string line = DistrictSaveStore.Write(new List<StoredRecord> { reg, demand, player, nul }, "UEFZTE9BRA==");
        StoredLine back = DistrictSaveStore.Read(line, out int version);
        Check(back != null && version == DistrictSaveStore.Version && back.Records.Count == 4 && back.Payload == "UEFZTE9BRA==", "district save: a line reads back whole");
        StoredRecord r0 = back.Records[0], r1 = back.Records[1], r2 = back.Records[2];
        Check(r0.Known && r0.Kind == SaveKind.Registration && r0.Index == 12 && r0.Parent == null && r0.Slot == 0 && r0.Addresses.Count == 1
              && r0.Addresses[0].Equals(new PlaceKey("reallife:street_a", 4)),
            "district save: a whole record's kind, index, places and slot read back");
        Check(r1.Kind == SaveKind.Demand && r1.Parent == "ba:itemname_apple|odd;key" && r1.Districts.Count == 1 && r1.Districts[0] == "reallife:district_a" && r1.Slot == 1,
            "district save: an element's record of the game's (its key, separators and all) reads back");
        Check(r2.Kind == SaveKind.Player && r2.Slot == -1 && string.Join("/", r2.Values) == "reallife:street_a/4/1.5/0/-2.25/0/0.7071/0/0.7071/inside"
              && back.Records[3].Values.Count == 1 && back.Records[3].Values[0] == null,
            "district save: a stand-in's real values read back, none as none");

        string later = line.Replace("v=" + DistrictSaveStore.Version + "^", "v=" + (DistrictSaveStore.Version + 1) + "^");
        Check(DistrictSaveStore.Read(later, out int laterVersion) == null && laterVersion == DistrictSaveStore.Version + 1,
            "district save: a later version's line is not read (its records may mean what this one does not know): it is kept as read");
        Check(DistrictSaveStore.Read("v=1^r:reg|x|~||||0^p:", out int _) == null && DistrictSaveStore.Read("garbage", out int _) == null && DistrictSaveStore.Read(null, out int _) == null,
            "district save: a damaged line, one that is not the store's, none: nothing read");
        string unknown = DistrictSaveStore.Write(new List<StoredRecord> { new StoredRecord { Code = "newkind", Index = 1, Slot = 0 } }, "x");
        StoredLine u = DistrictSaveStore.Read(unknown, out int _);
        Check(u != null && u.Records.Count == 1 && !u.Records[0].Known && u.Records[0].Code == "newkind",
            "district save: a record of a kind this version does not know is read as unknown and kept, never put back");
        string grown = line.Replace("^p:", "|extra field^p:");
        StoredLine g = DistrictSaveStore.Read(grown, out int _);
        Check(g != null && g.Records[3].Values.Count == 2 && g.Records[3].Values[1] == "extra field",
            "district save: a field a later version adds at the end of a record is read and kept");
        Check(DistrictSaveStore.Read(DistrictSaveStore.Write(new List<StoredRecord>(), string.Empty), out int _)?.Records.Count == 0,
            "district save: a line with no records reads as none");

        var made = new PlaceSet().Street("reallife:street_a").District("reallife:district_a");
        var halfMade = new PlaceSet().Street("reallife:street_a");
        Check(DistrictSaveStore.PlacesMade(reg, made.Has, made.HasDistrict) && DistrictSaveStore.PlacesMade(demand, made.Has, made.HasDistrict) && DistrictSaveStore.PlacesMade(player, made.Has, made.HasDistrict),
            "district save: a record goes back when every place it names is made this session");
        Check(!DistrictSaveStore.PlacesMade(demand, halfMade.Has, halfMade.HasDistrict) && !DistrictSaveStore.PlacesMade(reg, new PlaceSet().Has, new PlaceSet().HasDistrict)
              && !DistrictSaveStore.PlacesMade(null, made.Has, made.HasDistrict) && !DistrictSaveStore.PlacesMade(reg, null, made.HasDistrict),
            "district save: one place not made, and it stays kept (a feature off, another city)");

        Check(DistrictSaveStore.KeepUnread(null, "one") == "one" && DistrictSaveStore.KeepUnread("one", "one") == "one" && DistrictSaveStore.KeepUnread("one", "two") == "one\ntwo"
              && DistrictSaveStore.KeepUnread("one", null) == "one",
            "district save: a line no version here can read is kept once, beside the others");
        Check(DistrictSaveStore.Key.StartsWith("reallife:", StringComparison.Ordinal) && DistrictSaveStore.UnreadKey.StartsWith("reallife:", StringComparison.Ordinal)
              && DistrictSaveStore.Key != DistrictSaveStore.UnreadKey,
            "district save: its keys are the mod's own (reallife:)");
    }

    private static void DistrictSaveKinds()
    {
        int kinds = Enum.GetValues(typeof(SaveKind)).Length;
        bool codes = DistrictSaveRules.KindCount == kinds;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (SaveKind k in Enum.GetValues(typeof(SaveKind)))
        {
            string code = DistrictSaveRules.Code(k);
            codes &= seen.Add(code) && DistrictSaveRules.KindOf(code, out SaveKind back) && back == k;
        }
        Check(codes, "district save: every kind has its own code, and the code reads back as the kind");
        Check(!DistrictSaveRules.KindOf("newkind", out SaveKind _) && !DistrictSaveRules.KindOf(null, out SaveKind _), "district save: a code no kind has is not read as one");
        Check(DistrictSaveRules.Code(SaveKind.Registration) == "reg" && DistrictSaveRules.Code(SaveKind.Transaction) == "tx" && DistrictSaveRules.Code(SaveKind.Player) == "player"
              && DistrictSaveRules.Code(SaveKind.HeadhunterHrPlan) == "headhr",
            "district save: the codes saves already hold never change");
        Check(DistrictSaveRules.Way(SaveKind.Registration) == SaveWay.Leaves && DistrictSaveRules.Way(SaveKind.Employee) == SaveWay.Leaves
              && DistrictSaveRules.Way(SaveKind.InsuranceOffer) == SaveWay.Leaves,
            "district save: a business's records leave with it");
        Check(DistrictSaveRules.Way(SaveKind.Campaign) == SaveWay.LeavesFromGame && DistrictSaveRules.Way(SaveKind.Shift) == SaveWay.LeavesFromGame
              && DistrictSaveRules.Way(SaveKind.Demand) == SaveWay.LeavesFromGame,
            "district save: an element of a record of the game's leaves it, and the record stays");
        Check(DistrictSaveRules.Way(SaveKind.Player) == SaveWay.StandsIn && DistrictSaveRules.Way(SaveKind.Vehicle) == SaveWay.StandsIn
              && DistrictSaveRules.Way(SaveKind.DriverVehicle) == SaveWay.StandsIn && DistrictSaveRules.Way(SaveKind.ContactAddress) == SaveWay.StandsIn,
            "district save: the player and the player's cars and contacts are never lost: they stand in");
        Check(DistrictSaveRules.Kept(false) && !DistrictSaveRules.Kept(true), "district save: the player's own records are kept; another player's are put back after the write, never kept");
    }

    private static void DistrictSaveWritten()
    {
        string temp = "C:/Users/someone/AppData/Local/Temp/Hovgaard Games/Big Ambitions\\tempUncompressedSave";
        string prefix = DistrictSaveRules.WrittenPrefix(temp);
        Check(DistrictSaveRules.IsWrittenLine("Game " + temp + " saved in 99 ms", prefix), "district save: the game's own line that the file is written is heard (Player.log's words)");
        Check(DistrictSaveRules.IsWrittenLine("SaveGame serialization failed:", prefix) && DistrictSaveRules.IsWrittenLine("Save game serialization failed:", prefix),
            "district save: so is its line that it could not be written: the records go back either way");
        Check(!DistrictSaveRules.IsWrittenLine("Compressed to " + temp + " in 126 ms", prefix) && !DistrictSaveRules.IsWrittenLine("Game other\\file saved in 5 ms", prefix)
              && !DistrictSaveRules.IsWrittenLine("Game over", prefix) && !DistrictSaveRules.IsWrittenLine(null, prefix),
            "district save: no other line puts the records back before the file is written");
        Check(DistrictSaveRules.WrittenPrefix(null) == null && DistrictSaveRules.IsWrittenLine("Game x saved in 5 ms", null) && !DistrictSaveRules.IsWrittenLine("Game x saved", null),
            "district save: with the game's temporary file unknown, any line of its words is taken");
    }

    private static void DistrictSaveIdle()
    {
        var empty = new PlaceSet();
        Check(DistrictSaveRules.Idle(empty, 0) && DistrictSaveRules.Idle(null, 0), "district save: no places and nothing kept: the save is left as the game has it");
        Check(!DistrictSaveRules.Idle(empty, 1) && !DistrictSaveRules.Idle(new PlaceSet().District("reallife:d"), 0),
            "district save: something kept from the load, or a place of the mod's: the save is gone through");
        bool idle = true;
        var watch = new Stopwatch();
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        watch.Start();
        for (int i = 0; i < 1000000; i++)
        {
            idle &= DistrictSaveRules.Idle(empty, 0);
        }
        double ms = watch.Elapsed.TotalMilliseconds;
        allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
        Check(idle && ms < 50.0, "district save: a million saves with nothing of the mod's decided in under 50 ms (" + ms.ToString("0.0", CultureInfo.InvariantCulture) + " ms)");
        Check(allocated == 0, "district save: deciding that a save has nothing of the mod's allocates nothing (" + allocated + " bytes)");
    }
}
