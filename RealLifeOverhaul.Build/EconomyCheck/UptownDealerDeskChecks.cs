using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using RealLifeOverhaul.NewBuildings;

// Crest Motors' dealer's desks (fix/crest-desk-chairs). A car mod (Modded Vehicles Integration) turns the two plain desks of the game's luxury
// layout into the game's special employee desks in the game's memory, each with a seat for the player 1.5 m before it (the game's own desk seats
// the player there, pauses, and on the dialog's end stands them up beside the seat). The layout's two chairs set at an angle before each desk then
// stood either side of that seat: the player stood up boxed in by the desk, the chairs, a plant and the glass partitions. The dealership stands in a
// layout of its own now and the loose seats near each desk are taken out of it. Game-free: the rules on the layout as the car mod leaves it, as
// the game ships it, and on small floors made for the purpose; the sources; the self-check line.
internal static partial class Program
{
    // The layout as a car mod leaves it in the game's memory: each plain desk made the game's special employee desk (the employee's seat set on its
    // point 2 stays), and a seat for the player set on its point 3, 1.5 m before it, turned as the desk is (a clone of the employee's seat).
    private static List<FloorPiece> DealerWithDesks()
    {
        List<FloorPiece> plain = DealerPieces();
        var made = new List<FloorPiece>(plain.Count + 2);
        var desks = new List<int>(2);
        foreach (FloorPiece p in plain)
        {
            if (p.Name == "officedesk2left" || p.Name == "officedesk2right")
            {
                desks.Add(made.Count);
                made.Add(new FloorPiece(FloorMeasure.DeskName, p.X, p.Z, p.Yaw));
            }
            else
            {
                made.Add(p);
            }
        }
        foreach (int d in desks)
        {
            FloorPiece desk = made[d];
            FloorPiece staff = made.FirstOrDefault(q => q.Name == "officechair2" && Math.Abs(q.X - desk.X) < 0.5f && Math.Abs(q.Z - desk.Z) < 0.5f);
            if (staff != null)
            {
                staff.Parent = d;
                staff.Slot = 2;
                staff.Fixed = true;
            }
            double yaw = desk.Yaw * Math.PI / 180.0;
            made.Add(new FloorPiece("officechair2", desk.X - 1.5f * (float)Math.Sin(yaw), desk.Z - 1.5f * (float)Math.Cos(yaw), desk.Yaw) { Parent = d, Slot = FloorMeasure.CustomerSlot, Fixed = true });
        }
        return made;
    }

    // What MadeGame does: the loose seats near a desk taken out (the pieces set on others have their parents' places follow), then the seats mended.
    private static ClearVerdict DealerCleared(FloorMeasure measure, List<FloorPiece> pieces, out int removed, out FloorVerdict floor, List<string> gone = null)
    {
        List<int> loose = FloorMeasure.LooseAt(pieces);
        removed = loose.Count;
        gone?.AddRange(loose.Select(i => pieces[i].Name + "@" + pieces[i].X.ToString("F2", CultureInfo.InvariantCulture) + "," + pieces[i].Z.ToString("F2", CultureInfo.InvariantCulture)));
        var map = new int[pieces.Count];
        var kept = new List<FloorPiece>(pieces.Count);
        for (int i = 0; i < pieces.Count; i++)
        {
            map[i] = loose.Contains(i) ? -1 : kept.Count;
            if (map[i] >= 0)
            {
                kept.Add(pieces[i]);
            }
        }
        foreach (FloorPiece p in kept)
        {
            p.Parent = p.Parent >= 0 ? map[p.Parent] : p.Parent;
        }
        pieces.Clear();
        pieces.AddRange(kept);
        floor = measure.Tidy(pieces);
        return measure.Clear(pieces);
    }

    private static void UptownDealerDesk()
    {
        FloorMeasure measure = new FloorMeasure(FloorRoom.M1);
        string Pos(FloorPiece p) => p.Name + "@" + p.X.ToString("F2", CultureInfo.InvariantCulture) + "," + p.Z.ToString("F2", CultureInfo.InvariantCulture);

        // The footprint of the dealer's desk and its seats, read from the game's prefabs (Build 3682): the desk's NavMeshObstacle is a box 3 m by 1 m behind its origin; its seat
        // points are 0.25 m behind it (the employee's, turned to face the player) and 1.5 m before it (the player's), the seats' places to stand up from at 0.75 m ahead and 0.5 m to each side.
        float[] shape = FloorMeasure.Shape(FloorMeasure.DeskName);
        Check(FloorMeasure.Known(FloorMeasure.DeskName) && shape.Length == 4 && shape[1] == -0.5f && shape[2] == 1.5f && shape[3] == 0.5f && FloorMeasure.IsDesk(FloorMeasure.DeskName) && !FloorMeasure.IsDesk("officedesk2right")
              && FloorMeasure.CustomerSlot == 3 && FloorMeasure.SideStep == 0.5f && FloorMeasure.StandAhead("officechair") == 0.75f,
            "dealer desk: the rules know the dealer's desk's box, its player's seat point (the fourth, 1.5 m before it) and where a person stands up from a seat");

        // The layout as a car mod leaves it: two dealer's desks, each with its employee's seat on one point and the player's on another, and four loose chairs round them.
        List<FloorPiece> made = DealerWithDesks();
        int desks = made.Count(p => FloorMeasure.IsDesk(p.Name));
        List<int> loose = FloorMeasure.LooseAt(made);
        var names = loose.Select(i => Pos(made[i])).OrderBy(n => n, StringComparer.Ordinal).ToList();
        Check(desks == 2 && made.Count == DealerPieces().Count + 2 && loose.Count == 4 && loose.All(i => made[i].Name == "officechair" && made[i].Parent == -1),
            "dealer desk: the layout as a car mod leaves it has " + desks + " dealer's desks and " + loose.Count + " loose chairs within " + FloorMeasure.DeskRadius.ToString("F1", CultureInfo.InvariantCulture) + " m of them (" + string.Join(", ", names) + "), none set on a desk");
        Check(made.Where(p => p.Name == "officechair2").All(p => p.Parent >= 0 && (p.Slot == 2 || p.Slot == 3)) && made.Count(p => p.Name == "officechair2" && p.Slot == 3) == 2 && FloorMeasure.LooseAt(made).All(i => made[i].Name != "officechair2"),
            "dealer desk: the dealer's own seat (the desk's second point) and the player's (its fourth) are set on the desk, so they are not loose and are never taken out");
        FloorPiece customer = made.First(p => p.Slot == FloorMeasure.CustomerSlot);
        FloorPiece deskOfIt = made[customer.Parent];
        Check(Math.Abs(customer.Z - (deskOfIt.Z + 1.5f)) < 0.01f && Math.Abs(customer.X - deskOfIt.X) < 0.01f && Math.Abs(customer.Yaw - deskOfIt.Yaw) < 0.01f && Math.Abs(deskOfIt.Yaw - 180f) < 0.01f,
            "dealer desk: the player's seat is 1.5 m before a desk that faces the showroom, and faces it (" + Pos(customer) + " for the desk at " + Pos(deskOfIt) + ")");

        // As it stands, the beta-6 mending (the layout's seats moved a step) leaves the player boxed in at the desk: the seat sits between two chairs a metre and a half from the desk.
        List<FloorPiece> mended = DealerWithDesks();
        FloorVerdict beta6 = measure.Tidy(mended);
        ClearVerdict stuck = measure.Clear(mended);
        Check(!stuck.Pass && stuck.Desks == 2 && stuck.Behind < 2,
            "dealer desk: with the seats only moved a step, as the first mending did, the player is still not clear at the dealer's desk (the chairs are moved to stand behind the seat): " + FloorMeasure.Words(stuck) + " (the floor's seats, " + beta6.Moved + " moved: " + (beta6.Pass ? "PASS" : "FAIL") + ")");

        // The fix: the loose seats near the desks are taken out of the dealership's own copy, then the seats mended.
        List<FloorPiece> own = DealerWithDesks();
        int had = own.Count;
        var gone = new List<string>();
        ClearVerdict clear = DealerCleared(measure, own, out int removed, out FloorVerdict floor, gone);
        clear.Removed = removed;
        Check(clear.Pass && removed == 4 && clear.Desks == 2 && clear.Unseated == 0 && clear.Loose == 0 && clear.Sides == 4 && clear.Behind == 2 && floor.Pass,
            "dealer desk: with the loose seats out, the player can walk away from each dealer's desk on every side: " + removed + " removed, " + FloorMeasure.Words(clear) + "; the floor's seats " + FloorMeasure.Words(floor));
        Check(gone.Count == 4 && own.Count == had - 4 && gone.All(g => g.StartsWith("officechair@", StringComparison.Ordinal)) && own.Count(p => FloorMeasure.IsDesk(p.Name)) == 2 && own.Count(p => p.Name == "officechair2") == 4
              && own.Count(p => FloorMeasure.IsSeat(p.Name)) == 42 - 4 && own.All(p => FloorMeasure.LooseAt(own).Count == 0),
            "dealer desk: only the four loose chairs are gone (" + string.Join(", ", gone.OrderBy(g => g, StringComparer.Ordinal)) + "); both desks, both dealer's seats and both player's seats stay, and every other seat of the floor");
        Check(own.Where(p => p.Parent >= 0).All(p => FloorMeasure.IsDesk(own[p.Parent].Name)) && own.Count(p => p.Slot == FloorMeasure.CustomerSlot) == 2 && own.Count(p => p.Slot == 2) == 2,
            "dealer desk: what is set on a desk is still set on it after the seats between are taken out (the places of the pieces move up in the list, the links follow)");
        // Where each player's seat is let out to: a metre and a fifth behind it, and each side, the floor is open and joined to a door by a way a person can walk.
        List<FloorPiece> fresh = DealerWithDesks();
        FloorVerdict ignored;
        ClearVerdict noSeats = DealerCleared(measure, fresh, out _, out ignored);
        Check(noSeats.Pass && FloorMeasure.Words(noSeats) == FloorMeasure.Words(clear), "dealer desk: the same layout is cleared the same way every time, and a layout already cleared is left as it is");
        ClearVerdict again = measure.Clear(own);
        List<int> second = FloorMeasure.LooseAt(own);
        Check(again.Pass && second.Count == 0, "dealer desk: a second load over a cleared copy takes out nothing more");

        // The plain layout as the game ships it, with no car mod: no dealer's desk, nothing to clear, nothing taken out.
        List<FloorPiece> plain = DealerPieces();
        ClearVerdict none = measure.Clear(plain);
        Check(none.Pass && none.Desks == 0 && none.Removed == 0 && FloorMeasure.LooseAt(plain).Count == 0 && FloorMeasure.Words(none).Contains("no dealer's desk"),
            "dealer desk: the layout as the game ships it has no dealer's desk, so there is nothing to keep clear and no chair is taken out");

        // The checks are alive: each of the three ways a desk is not clear is found, on floors made for the purpose.
        List<FloorPiece> stand = new List<FloorPiece> { new FloorPiece(FloorMeasure.DeskName, 1047.5f, -157.5f, 180f), new FloorPiece("officechair2", 1047.5f, -156.0f, 180f) { Parent = 0, Slot = 3, Fixed = true } };
        Check(measure.Clear(stand).Pass, "dealer desk: a desk and its seat alone on open floor are clear");
        stand.Add(new FloorPiece("officechair", 1048.5f, -155.5f, 225f));
        ClearVerdict crowded = measure.Clear(stand);
        Check(!crowded.Pass && crowded.Loose == 1 && crowded.First != null && crowded.First.Contains("loose"), "dealer desk: a loose chair within reach of a desk is found (" + crowded.First + ")");
        stand.RemoveAt(2);
        stand.Add(new FloorPiece("groundplant04", 1047.5f, -154.8f, 0f));
        ClearVerdict shut = measure.Clear(stand);
        Check(!shut.Pass && shut.Behind == 0 && shut.First != null, "dealer desk: floor behind the player's seat shut by a planter is found (" + shut.First + ")");
        stand.RemoveAt(2);
        stand.Add(new FloorPiece("windowedroomdivider", 1046.4f, -156.0f, 90f));
        stand.Add(new FloorPiece("windowedroomdivider", 1048.6f, -156.0f, 90f));
        ClearVerdict sides = measure.Clear(stand);
        Check(!sides.Pass && sides.Sides < 2 && sides.First != null, "dealer desk: a seat boxed in at its two sides by partitions is found (" + sides.First + ")");
        List<FloorPiece> noSeat = new List<FloorPiece> { new FloorPiece(FloorMeasure.DeskName, 1047.5f, -157.5f, 180f) };
        ClearVerdict unseated = measure.Clear(noSeat);
        Check(unseated.Unseated == 1 && unseated.Desks == 1 && unseated.First != null && unseated.First.Contains("no seat"), "dealer desk: a desk with no seat for the player is said so (" + unseated.First + ")");
        Check(FloorMeasure.Words((ClearVerdict)null) == "not measured" && FloorMeasure.LooseAt(null).Count == 0 && !FloorMeasure.IsDesk(null) && !FloorMeasure.IsLoose(null) && FloorMeasure.IsLoose("armchair2") && FloorMeasure.IsLoose("officechair2") && !FloorMeasure.IsLoose("computer"),
            "dealer desk: the words of a verdict that was not measured, and which pieces are loose seats");

        // The installed game's own file, when it is on this machine: the layout as it ships has the desks plain and the staff's seat set on each, and a car mod's change of it is cleared the same.
        string file = System.IO.Path.Combine(DesignGameFiles, "BusinessLayouts", "CarDealership", "M1", FloorMeasure.Layout + ".json");
        if (!File.Exists(file))
        {
            Console.WriteLine("dealer desk: the game's luxury layout is not on this machine; the copy of its pieces is measured (SKIPPED)");
        }
        else
        {
            JObject set = ReadLayout(file);
            JObject[] listed = Items(set).ToArray();
            var ids = new Dictionary<string, JObject>();
            foreach (JObject item in listed)
            {
                ids[(string)item["id"] ?? string.Empty] = item;
            }
            int plainDesks = 0, staffSeats = 0;
            foreach (JObject item in listed)
            {
                string name = ((string)item["itemName"] ?? string.Empty).Replace("ba:itemname_", string.Empty);
                if (name != "officedesk2left" && name != "officedesk2right")
                {
                    continue;
                }
                plainDesks++;
                foreach (JObject child in ((JArray)item["stackedItems"]).OfType<JObject>())
                {
                    string childName = (string)child["childItemName"];
                    staffSeats += (int)child["attachmentIndex"] == 2 && childName == "ba:itemname_officechair2" && ids.ContainsKey((string)child["childId"]) ? 1 : 0;
                }
            }
            Check(plainDesks == 2 && staffSeats == 2 && !listed.Any(i => ServiceDeskRules.IsDesk((string)i["itemName"])),
                "dealer desk: the installed layout has two plain desks, each with its staff's seat set on its second point, and no dealer's desk of its own (a car mod makes them), as the copy of its pieces has");
        }

        DealerDeskSources();
    }

    private static void DealerDeskSources()
    {
        string scripts = System.IO.Path.GetFullPath(System.IO.Path.Combine(LocalesFolder(), "..", "Scripts"));
        string Read(string file)
        {
            string p = System.IO.Path.Combine(scripts, file);
            return File.Exists(p) ? CodeAlone(File.ReadAllText(p)) : string.Empty;
        }
        string floor = Read(@"NewBuildings\MadeGame.Floor.cs"), desk = Read(@"NewBuildings\FloorMeasure.Desk.cs"), specials = Read(@"Uptown\LifeGame.Specials.cs");
        Check(floor.Length > 0 && desk.Length > 0 && specials.Length > 0, "dealer desk: the desk's rules, the game's side of the floor and the load's line are there");

        string complete = MemberText(floor, "private static string CompleteFloor(SpecialService copy, MadeSpec spec)");
        int copied = complete.IndexOf("Copy(shipped, name)", StringComparison.Ordinal), cleared = complete.IndexOf("FloorMeasure.LooseAt(pieces)", StringComparison.Ordinal), taken = complete.IndexOf("own.Items.Remove(gone)", StringComparison.Ordinal),
            tidied = complete.IndexOf("measure.Tidy(pieces)", StringComparison.Ordinal), filed = complete.IndexOf("Filed(own, name, spec)", StringComparison.Ordinal), named = complete.IndexOf("copy.layout = name;", StringComparison.Ordinal);
        Check(copied > 0 && cleared > copied && taken > cleared && tidied > taken && filed > tidied && named > filed && !Regex.IsMatch(complete, @"shipped\.Items\.(Add|Remove|Insert|Clear)|shipped\.Items\[[^\]]*\]\s*\.\w+\s*=|Pieces\(shipped")
              && complete.Contains("items[i].position = at;") && complete.Contains("gone.stackedItems == null || gone.stackedItems.Count == 0"),
            "dealer desk: the dealership's own copy of the layout is made first, the loose seats taken out of it and the seats mended in it, then filed and named; the game's layout is never changed, so its own Manhattan Luxury Cars is as it was");
        Check(MemberText(floor, "private static BusinessLayoutSet Copy(BusinessLayoutSet from, string name)").Contains("Copy(from.Items[i])") && MemberText(floor, "private static LayoutItem Copy(LayoutItem s)").Contains("stackedItems = stacked")
              && MemberText(floor, "private static bool Filed(BusinessLayoutSet own, string name, MadeSpec spec)").Contains("GetOrLoadBusinessLayoutSet(own.BusinessType, new BuildingSizeInfo(spec.Size, spec.Version), name, false) == own")
              && MemberText(floor, "private static bool Filed(BusinessLayoutSet own, string name, MadeSpec spec)").Contains("all.Remove(key);"),
            "dealer desk: the copy has every item its own (a car mod changes the game's desks in place), and is filed in the game's book of layouts and found there by the game's own lookup, or taken out again");
        Check(MemberText(floor, "public static string FloorName(MadeSpec spec)").Contains("\"RealLife \" + spec.Id") && complete.Contains("string.Equals(copy.layout, name, StringComparison.OrdinalIgnoreCase)"),
            "dealer desk: the layout is named by the mod and the declaration (no business of the game's names it), and a copy made at an earlier load of this run is made again from the game's");
        Check(!Regex.IsMatch(desk + floor, @"void Update\(|LateUpdate\(|FixedUpdate\(|FindObjectsByType|FindObjectOfType|Physics\.|\bRandom\b|DateTime\.Now|Time\.time\b") && !desk.Contains("UnityEngine") && !desk.Contains("System.Linq"),
            "dealer desk: the desk's rules are game-free, left to no chance or clock, and nothing here runs a frame or searches the scene");
        string line = MemberText(specials, "private static void FloorLine(MadeBuilding made, SpecialPlace place)");
        Check(line.Contains("\"Uptown check (dealership desk): \"") && line.Contains("chair(s) removed near the dealer's desk (") && line.Contains("walk away from the desk on every side") && line.Contains("(d.Pass ? \"PASS\" : \"FAIL\")")
              && line.Contains("MadeGame.DeskVerdictOf(made.Spec.Id)") && MemberText(specials, "private static string FloorWhy(MadeBuilding made, SpecialService service)").Contains("d.Pass"),
            "dealer desk: the load writes one self-check line for the desk, PASS or FAIL, and the census asks it again");
    }
}
