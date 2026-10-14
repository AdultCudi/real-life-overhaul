using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy.Dealership;

// The Car Showroom's floor (Scripts/Economy/Dealer/Floor): how many of an hour's visitors are seen and what each does,
// which bay each car stands on, the bays' save line, the limits of the showroom's camera, and its texts.
internal static partial class Program
{
    private static void ShowroomFloor()
    {
        FloorVisitors();
        FloorAway();
        FloorBays();
        FloorBayLine();
        FloorCamera();
        FloorWords();
    }

    // A car in the workshop or out on a test drive is never looked over (the owner, 14 Oct 2026: visitors stopped at a car in the workshop).
    private static void FloorAway()
    {
        var plan = new List<FloorVisit>();
        var offers = new List<int>();
        var away = new List<bool> { false, false, true, false, true, false };
        bool never = true;
        int seen = 0;
        for (int hour = 0; hour < 400; hour++)
        {
            int n = ShowroomFloorRules.Plan(5, 9000 + hour, "Main%20St~12", 9, 4, offers, 6, plan, away);
            for (int i = 0; i < n; i++)
            {
                FloorVisit v = plan[i];
                seen++;
                never &= v.First != 2 && v.First != 4 && v.Second != 2 && v.Second != 4 && v.Third != 2 && v.Third != 4 && v.First >= 0;
            }
        }
        Check(never && seen > 0, "floor: no visitor looks over a car in the workshop or out on a test drive (" + seen + " visits in 400 hours)");
        offers.Add(2);
        offers.Add(1);
        int m = ShowroomFloorRules.Plan(5, 77, "Main%20St~12", 9, 9, offers, 6, plan, away);
        bool offerOnShow = true;
        for (int i = 0; i < m; i++)
        {
            offerOnShow &= !plan[i].Offers || plan[i].First == 1;
        }
        Check(offerOnShow, "floor: an offer on a car that is away is not shown; one on a car on show is");
        var allAway = new List<bool> { true, true, true };
        Check(ShowroomFloorRules.Plan(5, 78, "Main%20St~12", 9, 4, new List<int>(), 3, plan, allAway) == 0 && plan.Count == 0, "floor: with every car away nobody is shown looking");
        var withNull = new List<FloorVisit>();
        var noneAway = new List<bool> { false, false, false, false };
        ShowroomFloorRules.Plan(8, 500, "A~1", 6, 3, new List<int>(), 4, withNull);
        ShowroomFloorRules.Plan(8, 500, "A~1", 6, 3, new List<int>(), 4, plan, noneAway);
        bool same = withNull.Count == plan.Count;
        for (int i = 0; same && i < plan.Count; i++)
        {
            same = withNull[i].First == plan[i].First && withNull[i].Second == plan[i].Second && withNull[i].Third == plan[i].Third && withNull[i].Minute == plan[i].Minute;
        }
        Check(same, "floor: with no car away the plan is the same as it always was");
    }

    private static void FloorVisitors()
    {
        Check(ShowroomFloorRules.Shown(0) == 0 && ShowroomFloorRules.Shown(3) == 3 && ShowroomFloorRules.Shown(FloorCatalogue.ShownMost) == FloorCatalogue.ShownMost
              && ShowroomFloorRules.Shown(500) == FloorCatalogue.ShownMost && ShowroomFloorRules.Shown(-4) == 0,
            "showroom floor: as many of an hour's visitors are seen as came, up to the most, never fewer than none");
        var plan = new List<FloorVisit>();
        var offers = new List<int>();
        Check(ShowroomFloorRules.Plan(7, 100, "Main%20St~12", 0, 0, offers, 5, plan) == 0 && plan.Count == 0
              && ShowroomFloorRules.Plan(7, 100, "Main%20St~12", 4, 2, offers, 0, plan) == 0 && plan.Count == 0,
            "showroom floor: an hour with no visitors, or a lot with no cars, shows nobody");

        bool counts = true, minutes = true, cars = true, served = true, offered = true;
        string first = null;
        for (int visitors = 1; visitors <= 14; visitors++)
        {
            for (int s = 0; s <= visitors; s++)
            {
                for (int carCount = 1; carCount <= 5; carCount++)
                {
                    offers.Clear();
                    int offersMade = Math.Min(s, 2);
                    for (int o = 0; o < offersMade; o++)
                    {
                        offers.Add((o * 3 + visitors) % carCount);
                    }
                    int n = ShowroomFloorRules.Plan(2026, 4000 + visitors, "Harbour~4", visitors, s, offers, carCount, plan);
                    counts &= n == ShowroomFloorRules.Shown(visitors) && plan.Count == n;
                    int wasServed = 0, wasOffer = 0, last = -1;
                    for (int i = 0; i < plan.Count; i++)
                    {
                        FloorVisit v = plan[i];
                        minutes &= v.Minute >= 0 && v.Minute <= 59 && v.Minute >= last;
                        last = v.Minute;
                        cars &= v.Cars >= FloorCatalogue.CarsLeast && v.Cars <= Math.Min(FloorCatalogue.CarsMost, carCount);
                        for (int a = 0; a < v.Cars; a++)
                        {
                            cars &= v.Car(a) >= 0 && v.Car(a) < carCount;
                            for (int b = 0; b < a; b++)
                            {
                                cars &= v.Car(a) != v.Car(b);
                            }
                        }
                        wasServed += v.Served ? 1 : 0;
                        wasOffer += v.Offers ? 1 : 0;
                        offered &= !v.Offers || (v.Served && offers.Contains(v.First));
                    }
                    int wantServed = ShowroomFloorRules.ShownServed(visitors, s, offers.Count);
                    served &= wasServed == wantServed && wasServed <= n && (s > 0 || wasServed == 0) && wasServed <= Math.Max(s, 0);
                    offered &= wasOffer == Math.Min(offers.Count, wantServed);
                    if (!(counts && minutes && cars && served && offered) && first == null)
                    {
                        first = visitors + " visitor(s), " + s + " served, " + carCount + " car(s)";
                    }
                }
            }
        }
        Check(counts, "showroom floor: the plan holds exactly the visitors seen" + (first != null ? " (first wrong: " + first + ")" : string.Empty));
        Check(minutes, "showroom floor: visitors come in over the hour, in order, within its sixty minutes");
        Check(cars, "showroom floor: each visitor looks over one to three cars of the lot, never one twice");
        Check(served, "showroom floor: a salesperson goes to the hour's share of served visitors, never more than were served, and to nobody when nobody was");
        Check(offered, "showroom floor: one visitor for each offer made that hour (as far as the visitors seen go), first at the car the offer is on");
        Check(ShowroomFloorRules.ShownServed(10, 5, 0) == 3 && ShowroomFloorRules.ShownServed(10, 10, 0) == 6 && ShowroomFloorRules.ShownServed(10, 1, 0) == 1
              && ShowroomFloorRules.ShownServed(10, 1, 2) == 2 && ShowroomFloorRules.ShownServed(3, 0, 0) == 0,
            "showroom floor: the share served is kept when fewer are seen than came (5 of 10 is 3 of 6), with at least one when any was, and the offers among them");

        offers.Clear();
        offers.Add(2);
        offers.Add(9);    // a car the lot no longer has
        offers.Add(-1);
        ShowroomFloorRules.Plan(11, 50, "A~1", 4, 4, offers, 3, plan);
        int offerVisits = 0;
        for (int i = 0; i < plan.Count; i++)
        {
            offerVisits += plan[i].Offers ? 1 : 0;
            Check(!plan[i].Offers || plan[i].First == 2, "showroom floor: an offer on a car the lot no longer has shows nobody at a car that is not there");
        }
        Check(offerVisits == 1, "showroom floor: only the offers on cars still on the lot are shown (got " + offerVisits + ")");

        var again = new List<FloorVisit>();
        offers.Clear();
        offers.Add(1);
        ShowroomFloorRules.Plan(99, 777, "Main%20St~12", 9, 5, offers, 6, plan);
        ShowroomFloorRules.Plan(99, 777, "Main%20St~12", 9, 5, offers, 6, again);
        bool same = plan.Count == again.Count;
        for (int i = 0; same && i < plan.Count; i++)
        {
            same = plan[i].Minute == again[i].Minute && plan[i].First == again[i].First && plan[i].Second == again[i].Second && plan[i].Third == again[i].Third
                   && plan[i].Served == again[i].Served && plan[i].Offers == again[i].Offers && plan[i].Cars == again[i].Cars;
        }
        ShowroomFloorRules.Plan(99, 778, "Main%20St~12", 9, 5, offers, 6, again);
        bool differs = false;
        for (int i = 0; i < plan.Count && i < again.Count; i++)
        {
            differs |= plan[i].Minute != again[i].Minute || plan[i].First != again[i].First;
        }
        Check(same && differs, "showroom floor: the same hour of the same showroom always shows the same visitors, and another hour others");

        Check(ShowroomFloorRules.Phase(20, 10) == 0 && ShowroomFloorRules.Phase(20, 20) == 1 && ShowroomFloorRules.Phase(20, 20 + FloorCatalogue.StayMinutes - 1) == 1
              && ShowroomFloorRules.Phase(20, 20 + FloorCatalogue.StayMinutes) == 2,
            "showroom floor: a visit is still to come, under way for its stay, then over");
        bool looks = true;
        for (int i = 0; i < 200; i++)
        {
            int s = ShowroomFloorRules.LookSeconds(i, i % 6, i % 3);
            looks &= s >= FloorCatalogue.LookLeast && s <= FloorCatalogue.LookMost && s == ShowroomFloorRules.LookSeconds(i, i % 6, i % 3);
        }
        Check(looks, "showroom floor: a visitor stands at a car for the catalogue's seconds, the same each time");
        Check(FloorCatalogue.AtOnce + 1 <= 4 && FloorCatalogue.AtOnce >= 1 && FloorCatalogue.Late > 0 && FloorCatalogue.ShownMost >= FloorCatalogue.AtOnce,
            "showroom floor: at most the people pool's four who are only there to be seen are on the floor at once, the salesperson among them");

        long began = System.Diagnostics.Stopwatch.GetTimestamp();
        offers.Clear();
        offers.Add(0);
        for (int i = 0; i < 20000; i++)
        {
            ShowroomFloorRules.Plan(i, i, "Main%20St~12", 12, 6, offers, 12, plan);
        }
        double ms = (System.Diagnostics.Stopwatch.GetTimestamp() - began) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        Check(ms < 400.0, "showroom floor: 20,000 hours planned in " + ms.ToString("F1") + " ms (one is planned an hour, for the showroom the player stands in)");
    }

    private static void FloorBays()
    {
        var into = new int[6];
        ShowroomFloorRules.Arrange(6, new[] { 0, 1, 2 }, new[] { -1, -1, -1 }, 3, into);
        Check(into[0] == 0 && into[1] == 1 && into[2] == 2, "showroom bays: with no choices every car keeps its bay");
        ShowroomFloorRules.Arrange(6, new[] { 0, 1, 2 }, new[] { -1, 0, -1 }, 3, into);
        Check(into[1] == 0 && into[0] == 1 && into[2] == 2, "showroom bays: a car put on a bay stands there, and the car it moved takes the first bay free (got " + into[0] + ", " + into[1] + ", " + into[2] + ")");
        ShowroomFloorRules.Arrange(6, new[] { 0, 1, 2 }, new[] { 1, 0, -1 }, 3, into);
        Check(into[0] == 1 && into[1] == 0 && into[2] == 2, "showroom bays: two cars that changed places stand on each other's bays");
        ShowroomFloorRules.Arrange(6, new[] { 4, 1, -1 }, new[] { 4, 4, -1 }, 3, into);
        Check(into[0] == 4 && into[1] == 1 && into[2] == 0, "showroom bays: of two cars chosen for one bay the first has it, the other keeps its own, and a new car takes the first free bay");
        ShowroomFloorRules.Arrange(3, new[] { 0, ShowroomFloorRules.Absent, 5, -1, -1 }, new[] { -1, 1, 9, -1, -1 }, 5, into);
        Check(into[0] == 0 && into[1] == -1 && into[2] == 1 && into[3] == 2 && into[4] == -1,
            "showroom bays: a car no longer on the lot takes no bay, a bay beyond the lot is none, and a car for which no bay is left has none (got "
            + into[0] + ", " + into[1] + ", " + into[2] + ", " + into[3] + ", " + into[4] + ")");
        bool unique = true;
        var rnd = new Random(5);
        for (int t = 0; t < 2000; t++)
        {
            int bays = rnd.Next(0, 13), count = rnd.Next(0, 13);
            var cur = new int[count];
            var chosen = new int[count];
            var got = new int[count];
            for (int c = 0; c < count; c++)
            {
                cur[c] = rnd.Next(-2, 14);
                chosen[c] = rnd.Next(-1, 14);
            }
            ShowroomFloorRules.Arrange(bays, cur, chosen, count, got);
            int placedWant = 0, placed = 0;
            for (int c = 0; c < count; c++)
            {
                placedWant += cur[c] == ShowroomFloorRules.Absent ? 0 : 1;
                unique &= got[c] >= -1 && got[c] < bays && (cur[c] != ShowroomFloorRules.Absent || got[c] == -1);
                placed += got[c] >= 0 ? 1 : 0;
                for (int d = 0; d < c; d++)
                {
                    unique &= got[c] < 0 || got[c] != got[d];
                }
            }
            unique &= placed == Math.Min(placedWant, bays);
        }
        Check(unique, "showroom bays: no two cars ever share a bay, and every car on the lot has one while there are bays enough");

        var spots = new List<LotSpot> { new LotSpot { X = 0, Z = 0 }, new LotSpot { X = 400, Z = 0 }, new LotSpot { X = 0, Z = 700 } };
        Check(ShowroomFloorRules.Nearest(spots, 380, 40) == 1 && ShowroomFloorRules.Nearest(spots, -50, 500) == 2 && ShowroomFloorRules.Nearest(spots, 10, 10) == 0
              && ShowroomFloorRules.Nearest(new List<LotSpot>(), 0, 0) == -1 && ShowroomFloorRules.Nearest(null, 0, 0) == -1,
            "showroom bays: the pointer picks the bay nearest it, and none on a lot with no bays");
        Check(ShowroomFloorRules.Yaw(90, false) == 90 && ShowroomFloorRules.Yaw(90, true) == 270 && ShowroomFloorRules.Yaw(270, true) == 90 && ShowroomFloorRules.Yaw(0, true) == 180,
            "showroom bays: a car turned round faces the other way on its bay");
    }

    private static void FloorBayLine()
    {
        var bays = new ShowroomBays();
        bays.Choose("Main%20St~12", "Ab+c/9==", 3, true);
        bays.Choose("Odd^Place|x%", "Zz9", 0, false);
        bays.Choose("Main%20St~12", "Bb1", 11, false);
        bays.Choose("Main%20St~12", "", 2, false);   // no car: not kept
        string line = ShowroomBayStore.Write(bays);
        var back = new ShowroomBays();
        bool read = ShowroomBayStore.Read(line, back, out int version);
        Check(read && version == ShowroomBayStore.Version && back.Count == 3 && ShowroomBayStore.Write(back) == line,
            "showroom bays: the save's line reads back to exactly what was written");
        Check(back.BayOf("Main%20St~12", "Ab+c/9==") == 3 && back.TurnedOf("Main%20St~12", "Ab+c/9==") && back.BayOf("Odd^Place|x%", "Zz9") == 0
              && back.BayOf("Main%20St~12", "Zz9") == -1 && back.BayOf("Main%20St~12", "nobody") == -1,
            "showroom bays: every choice comes back for its own showroom and car, a place with the line's own marks made plain");
        var other = new ShowroomBays();
        other.Choose("Main%20St~12", "Bb1", 11, false);
        other.Choose("Odd^Place|x%", "Zz9", 0, false);
        other.Choose("Main%20St~12", "Ab+c/9==", 3, true);
        Check(ShowroomBayStore.Write(other) == line, "showroom bays: the same choices give the same line, in whatever order they were made");
        Check(ShowroomBayStore.Read(line.Replace("|3|1", "|3|1|later|7").Replace("v=1", "v=2"), back, out version) && version == 2 && back.BayOf("Main%20St~12", "Ab+c/9==") == 3,
            "showroom bays: a line of a later version, with fields added to a record, still loads");
        Check(!ShowroomBayStore.Read("nonsense", back, out version) && back.Count == 0 && !ShowroomBayStore.Read("v=1^a|b|x|0", back, out version) && back.Count == 0
              && !ShowroomBayStore.Read("v=1^a|b|2|7", back, out version) && ShowroomBayStore.Read(null, back, out version) && back.Count == 0
              && ShowroomBayStore.Read("v=1", back, out version) && back.Count == 0,
            "showroom bays: a line that cannot be read gives no choices (and is kept in the save as it was), an empty one none");
        var stock = new HashSet<string>(StringComparer.Ordinal) { "Ab+c/9==", "Zz9" };
        ShowroomBayStore.Read(line, back, out version);
        Check(back.Prune(stock) == 1 && back.Count == 2 && back.BayOf("Main%20St~12", "Bb1") == -1,
            "showroom bays: a car no longer on a lot loses its choice when the save is written");
        back.Clear();
        Check(back.Count == 0 && ShowroomBayStore.Write(back) == "v=1", "showroom bays: with no choices the line is only its version (and the key is left out of the save)");
        Check(ShowroomBayStore.Key == "reallife:showroom_bays" && ShowroomBayStore.Key != DealerStore.Key, "showroom bays: the choices are under a key of their own, apart from the showroom's");
    }

    private static void FloorCamera()
    {
        ShowroomFloorRules.Reach(4.6f, out float least, out float most);
        Check(least >= 2.3f + FloorCatalogue.NearLeast - 0.001f && most > least && most <= 2.3f + FloorCatalogue.FarMost + 0.001f,
            "showroom camera: round a car it comes no nearer than its edge and a step, and goes no further than a few metres");
        ShowroomFloorRules.Reach(0.5f, out float tinyLeast, out _);
        Check(tinyLeast >= 1f + FloorCatalogue.NearLeast - 0.001f, "showroom camera: a car of no length is taken as a small car, so the camera never sits in it");
        ShowroomFloorRules.FloorReach(2f, out float floorLeast, out float floorMost);
        Check(floorLeast > 0f && floorMost > floorLeast, "showroom camera: the floor seen from above has a nearest and a furthest, however small the floor");
        float d = 5f;
        bool zoom = true;
        for (int i = 0; i < 100; i++)
        {
            float next = ShowroomFloorRules.Zoom(d, 1f, least, most);
            zoom &= next <= d && next >= least;
            d = next;
        }
        zoom &= Math.Abs(d - least) < 0.001f;
        for (int i = 0; i < 100; i++)
        {
            d = ShowroomFloorRules.Zoom(d, -1f, least, most);
        }
        zoom &= Math.Abs(d - most) < 0.001f && ShowroomFloorRules.Zoom(5f, 0f, least, most) == 5f;
        Check(zoom, "showroom camera: the wheel brings it nearer and takes it back, never past its reach");
        Check(ShowroomFloorRules.Step(10f, 1) == 90f && ShowroomFloorRules.Step(90f, 1) == 180f && ShowroomFloorRules.Step(90f, -1) == 0f && ShowroomFloorRules.Step(-30f, -1) == -90f,
            "showroom camera: the game's rotate keys turn it to the next quarter, as the game's own cameras turn");
        bool orbit = true;
        for (int yaw = -360; yaw <= 360; yaw += 45)
        {
            for (int pitch = 0; pitch <= 90; pitch += 15)
            {
                ShowroomFloorRules.Orbit(yaw, pitch, 6f, out float x, out float y, out float z);
                orbit &= Math.Abs(Math.Sqrt(x * x + y * y + z * z) - 6.0) < 0.001 && y >= -0.0001f;
            }
        }
        ShowroomFloorRules.Orbit(0f, 0f, 3f, out float x0, out float y0, out float z0);
        ShowroomFloorRules.Orbit(90f, 0f, 3f, out float x9, out _, out float z9);
        orbit &= Math.Abs(z0 - 3f) < 0.001f && Math.Abs(x0) < 0.001f && Math.Abs(y0) < 0.001f && Math.Abs(x9 - 3f) < 0.001f && Math.Abs(z9) < 0.001f;
        Check(orbit, "showroom camera: it goes round what it looks at at the distance asked, never under it");
        Check(FloorCatalogue.PitchLeast > 0f && FloorCatalogue.PitchLeast < FloorCatalogue.PitchStart && FloorCatalogue.PitchStart < FloorCatalogue.PitchMost && FloorCatalogue.PitchMost < 90f
              && FloorCatalogue.FloorPitchLeast < FloorCatalogue.FloorPitchStart && FloorCatalogue.FloorPitchStart < FloorCatalogue.FloorPitchMost && FloorCatalogue.FloorPitchMost <= 90f,
            "showroom camera: it starts inside its own tilt, which never looks up from under a car nor past the top");

        float px = 14f, py = 9f, pz = 2f;
        bool moved = ShowroomFloorRules.KeepInside(5f, 2f, ref px, ref py, ref pz, 0f, 0f, 10f, -5f, 5f);
        Check(moved && px <= 10f - FloorCatalogue.Wall + 0.001f && Math.Abs(pz - 2f) < 0.001f && py <= FloorCatalogue.CeilingMost + 0.001f,
            "showroom camera: round a car it is kept inside the walls (it comes nearer along its line) and under the ceiling");
        px = 6f; py = 2f; pz = 1f;
        Check(!ShowroomFloorRules.KeepInside(5f, 2f, ref px, ref py, ref pz, 0f, 0f, 10f, -5f, 5f) && px == 6f && py == 2f && pz == 1f,
            "showroom camera: inside the showroom it is left where it is");
        px = -20f; py = 1f; pz = -20f;
        ShowroomFloorRules.KeepInside(1f, 1f, ref px, ref py, ref pz, 0f, 0f, 10f, -5f, 5f);
        Check(px >= 0f + FloorCatalogue.Wall - 0.001f && pz >= -5f + FloorCatalogue.Wall - 0.001f, "showroom camera: past a corner it comes back inside both walls");

        float e = 0f;
        bool eases = true;
        for (int i = 0; i < 300; i++)
        {
            float next = ShowroomFloorRules.Ease(e, 10f, 1f / 60f);
            eases &= next >= e && next <= 10f;
            e = next;
        }
        eases &= Math.Abs(e - 10f) < 0.01f && ShowroomFloorRules.Ease(3f, 10f, 0f) == 3f;
        float a = ShowroomFloorRules.EaseAngle(350f, 10f, 1f / 60f);
        Check(eases && a > 350f && a < 370f, "showroom camera: it eases to what the controls ask without overshooting, and turns the short way round");
    }

    private static void FloorWords()
    {
        string folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Locales"));
        var langs = new[] { LoadWords(System.IO.Path.Combine(folder, "en.json")), LoadWords(System.IO.Path.Combine(folder, "pt.json")), LoadWords(System.IO.Path.Combine(folder, "zh-cn.json")) };
        if (langs[0] == null || langs[1] == null || langs[2] == null)
        {
            Check(false, "showroom floor: en.json, pt.json and zh-cn.json are where the checks look for them (" + folder + ")");
            return;
        }
        var need = new Dictionary<string, string[]>
        {
            { "reallife_showroom_floor_enabled", new string[0] }, { "reallife_showroom_test_floor", new string[0] }, { "reallife_showroom_test_not_inside", new string[0] },
            { "reallife_showroom_test_off", new string[0] }, { "reallife_showroom_test_no_door", new string[0] }, { "reallife_showroom_button_look", new string[0] },
            { "reallife_showroom_button_bay", new string[0] }, { "reallife_showroom_car_away", new string[0] }, { "reallife_showroom_hints_look", new string[0] },
            { "reallife_showroom_hints_place", new string[0] }, { "reallife_showroom_hint_drag", new[] { "{0}" } }, { "reallife_showroom_hint_zoom", new string[0] },
            { "reallife_showroom_hint_bay", new[] { "{0}" } }, { "reallife_showroom_hint_put", new string[0] }, { "reallife_showroom_hint_turn_car", new[] { "{0}" } },
            { "reallife_showroom_note_keys", new string[0] }, { "reallife_showroom_refuse_away", new string[0] }, { "reallife_showroom_refuse_camera", new string[0] },
            { "reallife_showroom_refuse_inside", new string[0] }, { "reallife_guide_economy_dealer", new[] { "{showroom_shown}", "{showroom_at_once}" } }
        };
        var missing = new List<string>();
        foreach (KeyValuePair<string, string[]> k in need)
        {
            for (int l = 0; l < langs.Length; l++)
            {
                bool ok = langs[l].TryGetValue(k.Key, out string words) && !string.IsNullOrWhiteSpace(words);
                for (int p = 0; ok && p < k.Value.Length; p++)
                {
                    ok = words.Contains(k.Value[p]);
                }
                if (!ok)
                {
                    missing.Add(k.Key + (l == 0 ? " (en)" : l == 1 ? " (pt)" : " (zh-cn)"));
                }
            }
        }
        Check(missing.Count == 0, "showroom floor: every text of the floor, its camera and its bays is in all three locales with the keys it shows (" + need.Count + " keys"
                                  + (missing.Count > 0 ? "; missing " + string.Join(", ", missing) : string.Empty) + ")");
        Check(langs[0].TryGetValue("reallife_showroom_note_keys", out string note) && note.Length <= 240,
            "showroom floor: the notice of the camera's keys is at most 240 characters in English (" + (note ?? string.Empty).Length + ")");
    }
}
