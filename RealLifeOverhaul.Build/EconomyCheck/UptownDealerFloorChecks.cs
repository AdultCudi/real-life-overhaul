using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using RealLifeOverhaul.NewBuildings;

// Crest Motors' showroom floor and its cars (fix/crest-motors-desk; a player stood up from the chairs at its desk and could not move, and its
// car list lacked the cars the car mods add). Game-free: the floor's rules (FloorMeasure) on the game's own luxury layout as it ships (a copy
// of its pieces, DealerFloorData.cs, and the installed game's file when it is on this machine), on small floors made for the purpose, and the
// cars' list (StockRules); then the sources; and the texts, in eleven languages.
internal static partial class Program
{
    private static void UptownDealerFloor()
    {
        List<FloorPiece> shipped = DealerPieces();
        FloorMeasure measure = new FloorMeasure(FloorRoom.M1);

        // The layout as the game ships it: the rules know every piece that stands on the floor, and the seats before its desks and its lounge box a person in.
        bool known = shipped.Count > 100 && shipped.All(p => FloorMeasure.Known(p.Name)) && DealerLayoutNoBox.All(n => !FloorMeasure.Known(n));
        Check(known, "dealer floor: the rules know a footprint of every piece of the luxury layout that stands on the floor, and none of those that do not");
        var boxed = new List<int>();
        FloorVerdict before = measure.Measure(shipped, boxed);
        Check(before.DoorsOpen && before.Seats == 42 && before.Boxed == 6 && !before.Pass,
            "dealer floor: the layout as the game ships it has 42 seats a player may use and boxes in " + before.Boxed + " of them (6 were read): " + FloorMeasure.Words(before));
        var deskChairs = boxed.Where(i => shipped[i].Name == "officechair" && shipped[i].Z > -156f && shipped[i].Z < -155f && shipped[i].X > 1040f && shipped[i].X < 1049f).ToList();
        Check(deskChairs.Count >= 2 && boxed.Any(i => shipped[i].Name == "valenciachair"),
            "dealer floor: the seats boxed in are the chairs set before the two desks (" + deskChairs.Count + ") and a chair of the lounge before its footstool");

        // The tidy: every seat can be left, nothing but seats moved, and each only a step.
        List<FloorPiece> mended = DealerPieces();
        var start = mended.Select(p => new[] { p.X, p.Z }).ToList();
        FloorVerdict after = measure.Tidy(mended);
        int moved = 0;
        bool onlySeats = true, within = true, clear = true, fixedKept = true;
        for (int i = 0; i < mended.Count; i++)
        {
            if (mended[i].X == start[i][0] && mended[i].Z == start[i][1])
            {
                continue;
            }
            moved++;
            onlySeats &= FloorMeasure.IsSeat(mended[i].Name);
            fixedKept &= !mended[i].Fixed;
            float d = (float)Math.Sqrt((mended[i].X - start[i][0]) * (mended[i].X - start[i][0]) + (mended[i].Z - start[i][1]) * (mended[i].Z - start[i][1]));
            within &= d <= FloorMeasure.Reach + 0.001f;
            clear &= !measure.Crowded(mended, i);
        }
        Check(after.Pass && after.Boxed == 0 && after.Moved == moved && moved >= 1 && moved <= before.Boxed,
            "dealer floor: tidied, every seat's place to stand up reaches a door (" + moved + " seats moved of " + before.Boxed + " boxed in): " + FloorMeasure.Words(after));
        Check(onlySeats && within && clear && fixedKept, "dealer floor: only seats are moved, none further than " + FloorMeasure.Reach.ToString("F1", CultureInfo.InvariantCulture) + " m, none into another piece or a wall");
        FloorVerdict again = measure.Tidy(mended);
        Check(again.Pass && again.Moved == 0 && mended.Select(p => p.X + "," + p.Z).SequenceEqual(DealerTidied(measure)),
            "dealer floor: a floor already mended is left as it is (a second load), and the same floor is mended the same way every time");

        // The floor itself: every seat has a way to a door at least a metre wide, measured as the game's mesh keeps it.
        bool wide = true;
        for (int i = 0; i < mended.Count; i++)
        {
            wide &= !FloorMeasure.IsSeat(mended[i].Name) || StandsOnFloor(mended, i);
        }
        Check(wide, "dealer floor: after the tidy no seat's place to stand is inside another piece's box");

        // Small floors made for the purpose.
        var lone = new List<FloorPiece> { new FloorPiece("officechair", 1045f, -160f, 0f) };
        Check(measure.Measure(lone, null).Pass, "dealer floor: a seat alone on open floor can be left");
        var ring = new List<FloorPiece>
        {
            new FloorPiece("officechair", 1045f, -160f, 0f),
            new FloorPiece("bima320showcase", 1045f, -158.2f, 90f) { Fixed = true }, new FloorPiece("bima320showcase", 1045f, -161.8f, 90f) { Fixed = true },
            new FloorPiece("bima320showcase", 1046.8f, -160f, 0f) { Fixed = true }, new FloorPiece("bima320showcase", 1043.2f, -160f, 0f) { Fixed = true }
        };
        FloorVerdict walled = measure.Measure(ring, null);
        Check(walled.Boxed == 1 && !walled.Pass, "dealer floor: a seat walled in by four cars is found boxed in");
        FloorVerdict held = measure.Tidy(ring);
        Check(held.Boxed == 1 && held.Moved == 0 && ring[0].X == 1045f && ring[0].Z == -160f, "dealer floor: a seat that no step of at most " + FloorMeasure.Reach.ToString("F1", CultureInfo.InvariantCulture) + " m frees stays where it is, and is said to be boxed in");
        var blocked = new List<FloorPiece> { new FloorPiece("groundplant04", 1036f, -140.6f, 0f), new FloorPiece("officechair", 1045f, -160f, 0f) };
        Check(!measure.Measure(blocked, null).DoorsOpen && FloorRoom.For("ba:buildingsize_m", 1) == FloorRoom.M1 && FloorRoom.For("ba:buildingsize_k", 1) == null && FloorRoom.For(null, 1) == null && FloorRoom.For("ba:buildingsize_m", 2) == null,
            "dealer floor: a door blocked by a piece is said so, and only the structure the rules were read for has a floor");
        Check(!FloorMeasure.IsSeat("officechair2") && FloorMeasure.IsSeat("officechair") && FloorMeasure.IsSeat("armchair2") && FloorMeasure.IsSeat("valenciachair") && FloorMeasure.IsSeat("regularchair") && !FloorMeasure.IsSeat(null)
              && FloorMeasure.StandAhead("armchair2") < FloorMeasure.StandAhead("officechair"),
            "dealer floor: a member of staff's chair behind a desk is not a seat a player may use, the others are");

        // The cost: a measure is a few milliseconds, a tidy a fraction of a second, once a load.
        var watch = Stopwatch.StartNew();
        for (int i = 0; i < 20; i++)
        {
            measure.Measure(shipped, null);
        }
        double measureMs = watch.Elapsed.TotalMilliseconds / 20.0;
        watch.Restart();
        measure.Tidy(DealerPieces());
        double tidyMs = watch.Elapsed.TotalMilliseconds;
        Check(measureMs < 60.0 && tidyMs < 3000.0, "dealer floor: a measure of the floor takes " + measureMs.ToString("F1", CultureInfo.InvariantCulture) + " ms and a tidy " + tidyMs.ToString("F0", CultureInfo.InvariantCulture) + " ms, once a load");

        // The installed game's own file, when it is on this machine: the copy above is the layout as it is, and the rules know every piece on its floor.
        string file = System.IO.Path.Combine(DesignGameFiles, "BusinessLayouts", "CarDealership", "M1", FloorMeasure.Layout + ".json");
        if (!File.Exists(file))
        {
            Console.WriteLine("dealer floor: the game's luxury layout is not on this machine; the copy of its pieces is measured (SKIPPED)");
        }
        else
        {
            JObject set = ReadLayout(file);
            var live = new List<FloorPiece>();
            var others = new SortedSet<string>(StringComparer.Ordinal);
            foreach (JObject item in Items(set))
            {
                string name = ((string)item["itemName"] ?? string.Empty).Replace("ba:itemname_", string.Empty);
                if (!FloorMeasure.Known(name))
                {
                    others.Add(name);
                    continue;
                }
                JObject q = (JObject)item["rotation"], p = (JObject)item["position"];
                double yaw = 2.0 * Math.Atan2((double)q["y"], (double)q["w"]) * 180.0 / Math.PI;
                yaw = ((yaw % 360.0) + 360.0) % 360.0;
                live.Add(new FloorPiece(name, (float)p["x"], (float)p["z"], (float)yaw) { Fixed = !string.IsNullOrEmpty((string)item["parentId"]) });
            }
            bool same = live.Count == shipped.Count;
            for (int i = 0; same && i < live.Count; i++)
            {
                same = live[i].Name == shipped[i].Name && Math.Abs(live[i].X - shipped[i].X) < 0.002f && Math.Abs(live[i].Z - shipped[i].Z) < 0.002f && Math.Abs(live[i].Yaw - shipped[i].Yaw) < 0.02f && live[i].Fixed == shipped[i].Fixed;
            }
            Check(same, "dealer floor: the copy of the layout's pieces is the installed game's file (" + live.Count + " pieces)");
            Check(others.All(n => DealerLayoutNoBox.Contains(n)),
                "dealer floor: every other item of the installed layout is one with no box on the floor" + (others.Any(n => !DealerLayoutNoBox.Contains(n)) ? "; new: " + string.Join(", ", others.Where(n => !DealerLayoutNoBox.Contains(n))) : string.Empty));
            Check(measure.Measure(live, null).Boxed == before.Boxed && measure.Tidy(live).Pass, "dealer floor: the installed layout is boxed in and mended as its copy is");
        }

        UptownDealerDesk();
        DealerCars();
        DealerFloorSources();
        DealerFloorTexts();
    }

    private static List<FloorPiece> DealerPieces()
    {
        var list = new List<FloorPiece>(DealerLayoutPieces.Length);
        foreach (string row in DealerLayoutPieces)
        {
            string[] f = row.Split(',');
            list.Add(new FloorPiece(f[0], float.Parse(f[1], CultureInfo.InvariantCulture), float.Parse(f[2], CultureInfo.InvariantCulture), float.Parse(f[3], CultureInfo.InvariantCulture)) { Fixed = f[4] == "1" });
        }
        return list;
    }

    private static List<string> DealerTidied(FloorMeasure measure)
    {
        List<FloorPiece> p = DealerPieces();
        measure.Tidy(p);
        return p.Select(x => x.X + "," + x.Z).ToList();
    }

    // True when a seat's own place to stand is not inside another piece's box (the tidy puts nobody in a hole).
    private static bool StandsOnFloor(List<FloorPiece> pieces, int seat)
    {
        FloorPiece s = pieces[seat];
        double yaw = s.Yaw * Math.PI / 180.0;
        float x = s.X + FloorMeasure.StandAhead(s.Name) * (float)Math.Sin(yaw), z = s.Z + FloorMeasure.StandAhead(s.Name) * (float)Math.Cos(yaw);
        for (int k = 0; k < pieces.Count; k++)
        {
            float[] shape = k == seat ? null : FloorMeasure.Shape(pieces[k].Name);
            for (int b = 0; shape != null && b + 3 < shape.Length; b += 4)
            {
                double py = pieces[k].Yaw * Math.PI / 180.0, c = Math.Cos(py), sn = Math.Sin(py);
                double cx = pieces[k].X + shape[b] * c + shape[b + 1] * sn, cz = pieces[k].Z - shape[b] * sn + shape[b + 1] * c;
                double wx = x - cx, wz = z - cz, lx = wx * c - wz * sn, lz = wx * sn + wz * c;
                if (Math.Abs(lx) < shape[b + 2] && Math.Abs(lz) < shape[b + 3])
                {
                    return false;
                }
            }
        }
        return true;
    }

    // The dealership's cars: the floor's showcases first, then the cars the car mods listed for the dealership it copies, each once, those the game has.
    private static void DealerCars()
    {
        var floor = new List<string>();
        StockRules.Distinct(new[] { "ba:vehicletype_bima320", "ba:vehicletype_ferdinand112", "ba:vehicletype_vordpony" }, null, floor);
        var theirs = new[] { "ba:vehicletype_ferdinand112", "ba:vehicletype_bmwm4g82", "ba:vehicletype_gone", null, string.Empty, "ba:vehicletype_nissanr35", "ba:vehicletype_bmwm4g82" };
        int added = StockRules.Distinct(theirs, n => n != "ba:vehicletype_gone", floor);
        Check(added == 2 && floor.SequenceEqual(new[] { "ba:vehicletype_bima320", "ba:vehicletype_ferdinand112", "ba:vehicletype_vordpony", "ba:vehicletype_bmwm4g82", "ba:vehicletype_nissanr35" }),
            "dealer cars: the cars the car mods list are added after the floor's own, each once, those the game has, in their order");
        int none = StockRules.Distinct(null, null, floor) + StockRules.Distinct(new string[0], null, floor);
        Check(none == 0 && floor.Count == 5, "dealer cars: a dealership no car mod listed for adds none, and the list is as it was");
        var late = new List<string>(floor);
        Check(StockRules.Distinct(new[] { "ba:vehicletype_audirs6" }, null, late) == 1 && late.Count == 6 && StockRules.Distinct(new[] { "ba:vehicletype_audirs6" }, null, late) == 0,
            "dealer cars: a car a mod lists later is added once when the list is read again");
    }

    private static void DealerFloorSources()
    {
        string scripts = System.IO.Path.GetFullPath(System.IO.Path.Combine(LocalesFolder(), "..", "Scripts"));
        string Read(string file)
        {
            string p = System.IO.Path.Combine(scripts, file);
            return File.Exists(p) ? CodeAlone(File.ReadAllText(p)) : string.Empty;
        }
        string floor = Read(@"NewBuildings\MadeGame.Floor.cs"), services = Read(@"NewBuildings\MadeGame.Services.cs"), specials = Read(@"Uptown\LifeGame.Specials.cs"), watch = Read(@"Uptown\LifeDirector.Dealer.cs"),
            rules = Read(@"NewBuildings\FloorMeasure.cs");
        Check(floor.Length > 0 && services.Length > 0 && specials.Length > 0 && watch.Length > 0 && rules.Length > 0, "dealer floor: the floor's rules, its game side, the load's call and the visit's look are there");

        string complete = MemberText(services, "private static void Complete(SpecialService copy, MadeSpec spec)");
        int store = complete.IndexOf("if (!spec.ServiceStore)", StringComparison.Ordinal), call = complete.IndexOf("CompleteFloor(copy, spec)", StringComparison.Ordinal);
        Check(store > 0 && call > store && Regex.Matches(complete, @"catch \(Exception e\)").Count == 2,
            "dealer floor: only a vehicle store's copy has its floor measured, at the load, in a try of its own that leaves the desks' completion alone");
        string of = MemberText(floor, "private static string CompleteFloor(SpecialService copy, MadeSpec spec)");
        Check(of.Contains("FloorMeasure.Layout") && of.Contains("measure.Tidy(pieces)") && of.Contains("items[i].position = at;") && !Regex.IsMatch(of, @"shipped\.Items\.(Add|Remove|Insert|Clear)") && of.Contains("Copy(shipped, name)"),
            "dealer floor: the layout's seats are moved in the dealership's own copy of it, only for the layout the rules were read for, and the game's own layout is never changed");
        Check(MemberText(specials, "public static string Special(MadeBuilding made, SpecialPlace place)").Contains("FloorLine(made, place);")
              && MemberText(specials, "private static void FloorLine(MadeBuilding made, SpecialPlace place)").Contains("\"Uptown check (dealership floor): \"")
              && MemberText(specials, "private static void FloorLine(MadeBuilding made, SpecialPlace place)").Contains("(v.Pass ? \"PASS\" : \"FAIL\")")
              && MemberText(specials, "public static string SpecialWhy(MadeBuilding made, SpecialPlace place)").Contains("FloorWhy(made, service)"),
            "dealer floor: the load writes one self-check line for the floor, PASS or FAIL, and the census asks it again");
        string stock = MemberText(specials, "private static string Stock(MadeBuilding made, SpecialPlace place)");
        Check(stock.Contains("MadeGame.FloorCars(layout, cars)") && stock.Contains("MadeGame.ModdedCars(place.Service, cars)") && stock.IndexOf("ModdedCars", StringComparison.Ordinal) > stock.IndexOf("FloorCars", StringComparison.Ordinal)
              && stock.IndexOf("MadeGame.Stock(", StringComparison.Ordinal) > stock.IndexOf("ModdedCars", StringComparison.Ordinal)
              && MemberText(floor, "public static int ModdedCars(string copied, List<string> into)").Contains("TryGetVehiclesForContact(copied, out List<string> theirs)"),
            "dealer cars: the dealership's list is the floor's cars and then the car mods' for the dealership it copies, read as the mods file them (by its business name), before it goes into the game's list");
        string restock = MemberText(watch, "private static void RestockDealer()");
        Check(restock.Contains("if (_restocked)") && restock.Contains("_restocked = true;") && restock.Contains("LifeGame.PlayerIn(SpecialUp[at])") && watch.Contains("_restocked = false;") && MemberText(watch, "private static void WatchDesk(float now)").Contains("RestockDealer();"),
            "dealer cars: the list is read again against the car mods' once a visit, as the player walks into the dealership, inside the once-a-second look");
        Check(!Regex.IsMatch(rules + floor, @"void Update\(|LateUpdate\(|FixedUpdate\(|FindObjectsByType|FindObjectOfType|Physics\.|\bRandom\b|DateTime\.Now|Time\.time\b") && !rules.Contains("UnityEngine") && !rules.Contains("System.Linq"),
            "dealer floor: the rules are game-free, left to no chance or clock, and nothing here runs a frame or searches the scene");
    }

    private static void DealerFloorTexts()
    {
        string folder = LocalesFolder();
        string[] langs = { "en", "pt", "zh-cn", "de", "fr", "es", "ru", "nl", "tr", "pl", "ko" };
        string[] marks = { "Fixed: ", "Corrigido: ", "已修复：", "Behoben: ", "Corrigé : ", "Corregido: ", "Исправлено: ", "Opgelost: ", "Düzeltildi: ", "Naprawiono: ", "수정: " };
        string missing = null, unmarked = null, plain = null;
        for (int i = 0; i < langs.Length; i++)
        {
            Dictionary<string, string> words = LoadWords(System.IO.Path.Combine(folder, langs[i] + ".json"));
            int lines = 0;
            bool marked = false, tidy = true;
            foreach (KeyValuePair<string, string> row in words ?? new Dictionary<string, string>())
            {
                if (!row.Key.StartsWith("reallife_changes_", StringComparison.Ordinal) || row.Key.EndsWith("_title", StringComparison.Ordinal))
                {
                    continue;
                }
                foreach (string line in row.Value.Split('\n'))
                {
                    if (!line.Contains("Crest Motors"))
                    {
                        continue;
                    }
                    lines++;
                    marked |= Regex.IsMatch(line, Regex.Escape(marks[i]) + "[^.。\\n]*Crest Motors");   // a fix sentence of its own, wherever the row puts it
                    tidy &= !line.Contains("  ") && !line.Contains("!") && !line.Contains("–") && !line.Contains("—");
                }
            }
            missing = lines >= 2 ? missing : missing ?? langs[i];
            unmarked = marked ? unmarked : unmarked ?? langs[i];
            plain = tidy ? plain : plain ?? langs[i];
        }
        Check(missing == null && unmarked == null && plain == null,
            "dealer floor: What's new has a second fix line naming Crest Motors (this one, besides 0.58.0's), marked as a fix, plain, in all eleven languages" + (missing != null ? " (not in " + missing + ")" : unmarked != null ? " (no mark in " + unmarked + ")" : plain != null ? " (a dash, a double space or a ! in " + plain + ")" : string.Empty));
    }
}
