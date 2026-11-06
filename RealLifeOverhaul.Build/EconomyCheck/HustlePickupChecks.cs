using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Hustles;

// Collecting what was bought on the Marketplace (Scripts/Hustles/PickupRules.cs, HustleDirector.Pickup.cs): the days a seller holds
// a thing, what lapses and what it returns, a save's waiting things made right, the save's new field, the doors asked for again
// while the city has no building there, how near the player and the route must be, and the source: the pickup point always found,
// the route ending on it, the parcel, the word to a player in a vehicle, and the new texts in every language.
internal static partial class Program
{
    private static void HustlePickupChecks()
    {
        HustleHoldChecks();
        HustleRepairOnLoadChecks();
        HustleDoorBookChecks();
        HustleReachChecks();
        HustlePickupSource();
        HustlePickupTexts();
    }

    private static Owned HustleWaiting(HustleState s, int day, int paid, bool car = false, bool test = false)
    {
        var l = new Listing { Id = s.Next(), Thing = car ? "ba:vehicletype_bima320" : "laptop", Car = car, Condition = 50, Shown = 50, Asking = paid, Floor = paid, Seller = "Ann", Street = "Elm Road", Number = 12 };
        s.Listings.Add(l);
        Owned o = HustleRules.Buy(s, l, paid, false, 0, day);
        o.Test = test;
        return o;
    }

    // ---------------------------------------------------------------- the hold

    private static void HustleHoldChecks()
    {
        var s = HustleFresh(77);
        Owned o = HustleWaiting(s, 7, 300);
        Check(PickupRules.HoldDays == 3 && PickupRules.UntilFor(7) == 10 && o.State == OwnedState.Waiting && o.Until == 10,
            "hustles: a seller holds what was bought for three days, the last being the day of the purchase plus three");
        Check(!PickupRules.Lapsed(10, 10) && PickupRules.Lapsed(10, 11) && !PickupRules.Lapsed(0, 500) && PickupRules.DaysLeft(10, 10) == 1 && PickupRules.DaysLeft(10, 8) == 3 && PickupRules.DaysLeft(10, 40) == 0,
            "hustles: a thing is held to the end of its last day, one with no last day never lapses, and the days left count the last day");
        var kept = new Owned { Id = s.Next(), Thing = "sofa", State = OwnedState.Kept, Paid = 90, Until = 1 };
        s.Items.Insert(0, kept);
        var car = HustleWaiting(s, 7, 9000, true);
        var test = HustleWaiting(s, 7, 0, false, true);
        var events = new List<HustleEvent>();
        var worths = new List<int> { 100, 300, 9000, 0 };
        HustleRules.Hour(s, 10 * 24 + 23, worths, events);
        Check(s.Items.Count == 4 && !events.Exists(e => e.Kind == HustleEventKind.Expired), "hustles: at the last hour of its last day a thing is still held");
        HustleRules.Hour(s, 11 * 24, worths, events);
        HustleEvent first = events.Find(e => e.Kind == HustleEventKind.Expired && e.Id == o.Id);
        HustleEvent second = events.Find(e => e.Kind == HustleEventKind.Expired && e.Id == car.Id);
        HustleEvent third = events.Find(e => e.Kind == HustleEventKind.Expired && e.Id == test.Id);
        Check(s.Items.Count == 1 && s.Items[0] == kept && kept.State == OwnedState.Kept && first.Amount == 300 && first.Who == "Ann" && first.Thing == "laptop" && !first.Car
              && second.Amount == 9000 && second.Car && third.Amount == 0 && PickupRules.Refund(null) == 0,
            "hustles: a thing not collected by its last day goes back to its seller and what was paid returns, nothing for a test's, and what the player owns stays");
        events.Clear();
        HustleRules.Hour(s, 12 * 24, new List<int> { 100 }, events);
        Check(events.Count == 0, "hustles: a lapsed thing is told once");
        var d = HustleFresh(78);
        Owned home = HustleRules.Buy(d, new Listing { Id = d.Next(), Thing = "sofa", Condition = 40, Shown = 40, Asking = 100, Floor = 100, Seller = "Bo", Street = "S", Number = 1 }, 100, true, 35, 7);
        events.Clear();
        HustleRules.Hour(d, 30 * 24, new List<int> { 0 }, events);
        Check(home.State == OwnedState.Kept && !events.Exists(e => e.Kind == HustleEventKind.Expired), "hustles: goods brought home are not held at a door and never lapse");
    }

    // ---------------------------------------------------------------- a save's waiting things

    private static void HustleRepairOnLoadChecks()
    {
        var s = HustleFresh(79);
        Owned old = HustleWaiting(s, 40, 250);
        old.Until = 0;   // a save from before things lapsed
        Owned good = HustleWaiting(s, 45, 120);
        Owned lost = HustleWaiting(s, 45, 80);
        lost.Street = string.Empty;
        Owned zero = HustleWaiting(s, 45, 60);
        zero.Number = 0;
        var refunded = new List<Owned>();
        int changed = PickupRules.Repair(s, 50, refunded);
        Check(changed == 3 && old.Until == 53 && good.Until == 48 && s.Items.Count == 2 && refunded.Count == 2 && refunded.Contains(lost) && refunded.Contains(zero)
              && PickupRules.Refund(lost) == 80 && PickupRules.Refund(zero) == 60,
            "hustles: on load a waiting thing with no last day is held three days from today, one with no address to go to leaves and its money returns");
        Check(PickupRules.Repair(s, 50, refunded) == 0 && refunded.Count == 2, "hustles: mending a save twice changes nothing the second time");
        string line = HustleStore.Write(s);
        HustleState back = HustleStore.Read(line, out _);
        Check(back != null && back.Items.Count == 2 && back.Items[0].Until == 53 && back.Items[1].Until == 48 && HustleStore.Write(back) == line,
            "hustles: the last day of a waiting thing is in the save's line and reads back");
        int from = line.IndexOf("^o:", StringComparison.Ordinal), to = line.IndexOf("^b:", StringComparison.Ordinal);
        string section = line.Substring(from, to - from);
        string oldSection = Regex.Replace(section, @"\|\d+(?=;|$)", string.Empty);
        HustleState before = HustleStore.Read(line.Substring(0, from) + oldSection + line.Substring(to), out _);
        Check(before != null && before.Items.Count == 2 && before.Items[0].Until == 0 && before.Items[0].Street == "Elm Road" && before.Items[1].Number == 12,
            "hustles: a save from before the last day was kept reads, with none set, and the repair then sets it");
        if (before != null)
        {
            PickupRules.Repair(before, 60, null);
            Check(before.Items[0].Until == 63 && before.Items[1].Until == 63, "hustles: the repair of an old save holds each waiting thing three days from the day it loads");
        }
    }

    // ---------------------------------------------------------------- the doors

    private sealed class CheckEntry : PickupEntry<string>
    {
    }

    private static void HustleDoorBookChecks()
    {
        var entry = new CheckEntry { Street = "Crest Ave", Number = 35 };
        int asked = 0;
        Func<string, int, string> find = (street, number) => ++asked >= 3 ? "building" : null;
        bool at0 = entry.Seek(0f, false, find);
        bool at1 = entry.Seek(1f, false, find);   // too soon: the city is not asked
        bool at4 = entry.Seek(4f, false, find);
        bool at8 = entry.Seek(8f, false, find);
        bool again = entry.Seek(9f, false, find);
        Check(!at0 && !at1 && !at4 && at8 && again && asked == 3 && entry.Looks == 3 && entry.Building == "building",
            "hustles: a seller's building the city did not have at first is asked for again every few seconds, found once it stands, and not asked for after (" + asked + " asks)");
        var late = new CheckEntry { Street = "S", Number = 1 };
        int calls = 0;
        bool forced = late.Seek(0f, false, (a, b) => { calls++; return null; }) | late.Seek(0.5f, true, (a, b) => { calls++; return "now"; });
        Check(forced && calls == 2, "hustles: a door asked for at once (a test, a route) is not made to wait for the next look");
        var gone = new List<string>();
        var book = new PickupBook<CheckEntry, string>(2, e => gone.Add(e.Street + e.Number));
        CheckEntry a1 = book.Add("A", 1), a2 = book.Add("B", 2);
        CheckEntry a3 = book.Add("C", 3);
        Check(book.Count == 2 && book.Find("A", 1) == null && book.Find("B", 2) == a2 && book.Find("C", 3) == a3 && gone.Count == 1 && gone[0] == "A1" && a1 != a2,
            "hustles: the doors in play are found by address and the oldest leaves when the book is full, giving back what hung on it");
        book.Clear();
        Check(book.Count == 0 && gone.Count == 3, "hustles: clearing the doors gives back each one");
    }

    // ---------------------------------------------------------------- how near

    private static void HustleReachChecks()
    {
        Check(PickupRules.Reaches(4.4f, 2.9f) && PickupRules.Reaches(0f, -3f) && !PickupRules.Reaches(4.6f, 0f) && !PickupRules.Reaches(0f, 3.1f),
            "hustles: a player within 4.5 m of the pickup point and 3 m up or down is at it");
        Check(PickupRules.RouteArrives(3f) && !PickupRules.RouteArrives(3.2f) && PickupRules.RouteEnd < PickupRules.Reach && PickupRules.Reach < PickupRules.StageWithin && PickupRules.StageWithin < PickupRules.UnstageBeyond
              && PickupRules.RefineWithin < PickupRules.MeasureWithin && PickupRules.MarkerLift > 0f,
            "hustles: a route that ends within 3 m of the point arrives, which is inside the reach, and the parcel is stood well before the player is at it");
        Check(PickupRules.Why(true, true) == PickupWhy.GetOut && PickupRules.Why(false, true) == PickupWhy.None && PickupRules.Why(true, false) == PickupWhy.NotStanding
              && PickupRules.Why(false, false) == PickupWhy.NotStanding,
            "hustles: a player at the point in a vehicle is told to get out, and a building the city lacks is said so");
    }

    // ---------------------------------------------------------------- the source

    private static void HustlePickupSource()
    {
        string folder = HustleScripts();
        string rules = System.IO.File.ReadAllText(System.IO.Path.Combine(folder, "PickupRules.cs"));
        Check(!rules.Contains("using UnityEngine") && !rules.Contains("SaveGameManager") && !rules.Contains("Random.") && !rules.Contains("DateTime"),
            "hustles: the pickup rules are game-free and draw no unseeded chance");
        string game = System.IO.File.ReadAllText(System.IO.Path.Combine(folder, "HustleGame.cs"));
        Check(game.Contains("JobSpots.Front(building, out spot)") && game.Contains("StreetSpots.OnPavement(door, 14f, out spot)") && game.Contains("NavMesh.SamplePosition(door, out NavMeshHit hit, 8f, NavMesh.AllAreas)")
              && game.Contains("StreetSpots.Ground(door, out float ground)") && !game.Contains("public static void Guide("),
            "hustles: a seller's door always has a pickup point once the city has the building: the pavement, a wider pavement, the walking mesh, the ground under the door");
        string pickup = System.IO.File.ReadAllText(System.IO.Path.Combine(folder, "HustleDirector.Pickup.cs"));
        Check(pickup.Contains("d.Seek(Time.unscaledTime, now, FindBuilding)") && pickup.Contains("HustleGame.PickupSpot(d.Building") && pickup.Contains("_props?.Stand(ParcelFor(o), d.Front, facing)")
              && pickup.Contains("HustleGame.Route(street, number, have, spot)") && pickup.Contains("PickupRules.Why(true, d.Building != null)") && pickup.Contains("PickupRules.Repair(_state, HustleGame.Day, gone)"),
            "hustles: the director asks the city again for a building it lacked, stands a parcel at the point, ends the route on it, tells a player in a vehicle and mends a save on load");
        var routed = new List<string>();
        foreach (string file in System.IO.Directory.GetFiles(folder, "*.cs"))
        {
            string text = System.IO.File.ReadAllText(file);
            if (text.Contains("HustleGame.Guide(") || text.Contains("SetGuiderTarget(new Address"))
            {
                if (System.IO.Path.GetFileName(file) != "HustleGame.cs")
                {
                    routed.Add(System.IO.Path.GetFileName(file));
                }
            }
        }
        Check(routed.Count == 0, "hustles: every route the Marketplace sets goes through the one that ends on the pickup point (" + string.Join(", ", routed) + ")");
        string places = System.IO.File.ReadAllText(System.IO.Path.Combine(folder, "HustleDirector.Places.cs"));
        Check(places.Contains("Stage(here);") && places.Contains("Retarget(here);") && places.Contains("Nudge(here);") && !places.Contains("_fronts") && !places.Contains("private struct FrontSpot"),
            "hustles: each second the parcels are stood, a route kept across a load is moved onto the point and a player in a vehicle is told, and no door is remembered as not found");
        string tests = System.IO.File.ReadAllText(System.IO.Path.Combine(folder, "HustleDirector.Tests.cs"));
        Check(tests.Contains("public void TestPickup()") && tests.Contains("NavMesh.SamplePosition(d.Front") && tests.Contains("PickupRules.RouteArrives(gap)") && tests.Contains("Collect(door.Street, door.Number)"),
            "hustles: the pickup test buys a thing, checks the point is on the walking mesh and the route ends on it, stands the parcel, asks the door for Collect, and collects");
    }

    private static void HustlePickupTexts()
    {
        Dictionary<string, string>[] words = LanguageWords(out string[] codes);
        if (!LanguagesRead("hustle pickups", words, codes))
        {
            return;
        }
        var parts = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "en", "a thing bought on the Marketplace now has a clear way to be collected" }, { "pt", "uma coisa comprada no Mercado agora tem um jeito claro de ser retirada" },
            { "zh-cn", "在二手市场买下的东西，现在有了明确的取货方式" }, { "de", "Eine im Marketplace gekaufte Sache hat jetzt einen klaren Abholweg" },
            { "fr", "un objet acheté sur le Marketplace a maintenant un moyen clair d'être récupéré" }, { "es", "una cosa comprada en el Marketplace ahora tiene una forma clara de recogerse" },
            { "ru", "вещь, купленная в Marketplace, теперь получает ясный путь к получению" }, { "nl", "een ding dat je op de Marketplace koopt, heeft nu een duidelijke manier om opgehaald te worden" },
            { "tr", "Marketplace'ten alınan bir eşyanın artık net bir teslim alma yolu var" }
        };
        for (int l = 0; l < words.Length; l++)
        {
            bool said = parts.TryGetValue(codes[l], out string part) && AnyChangesRow(words[l], part);
            bool clean = true;
            foreach (KeyValuePair<string, string> row in words[l])
            {
                clean &= !(row.Key.StartsWith("reallife_changes_", StringComparison.Ordinal) && !row.Key.EndsWith("_title", StringComparison.Ordinal) && row.Value.Contains(part ?? "\u0001") && row.Value.Contains("  "));
            }
            Check(said && clean, "hustles: What's new says a bought thing can be collected, in " + codes[l] + ", in one row with no double space");
        }
        foreach (string key in new[] { "reallife_hustle_note_bought_hold", "reallife_hustle_note_get_out", "reallife_hustle_note_refunded", "reallife_hustle_note_lapsed_free", "reallife_hustle_note_way_missing",
                     "reallife_hustle_test_pickup_pass", "reallife_hustle_test_pickup_fail" })
        {
            Check(words[0].TryGetValue(key, out string text) && text.Length <= 240, "hustles: " + key + " is a notice of at most 240 characters");
        }
    }
}
