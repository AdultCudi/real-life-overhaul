using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy;

internal static partial class Program
{
    // Stock that a warehouse says it sent and a shop never got (Economy/Logistics): a full vehicle shared over all the stops, every
    // piece accounted for in every ending of a trip, the day's tally and its line, nothing arriving spoiled at once at any pace,
    // thrown-out food not written as goods sent, and the player told.
    private static void StockSent()
    {
        TripShares();
        TripEndings();
        TripTallyBook();
        SpoilArrival();
        StockSentSource();
    }

    private static int AllotRun(int[] wantBoxes, int size, int pool, int room, int start, int[] pieces)
    {
        int stops = wantBoxes.Length;
        var stopOf = new int[stops];
        var poolOf = new int[stops];
        var wanted = new int[stops];
        var sizes = new int[stops];
        for (int i = 0; i < stops; i++)
        {
            stopOf[i] = i;
            wanted[i] = wantBoxes[i] * size;
            sizes[i] = size;
        }
        return TripRules.Allot(stops, stopOf, poolOf, wanted, sizes, stops, start, room, new[] { pool }, pieces);
    }

    private static void TripShares()
    {
        // Eight shops each wanting ten boxes, a vehicle of sixteen: the first two used to take it all and six got nothing, every day.
        var pieces = new int[8];
        int used = AllotRun(new[] { 10, 10, 10, 10, 10, 10, 10, 10 }, 10, 100000, 16, 0, pieces);
        bool every = used == 16;
        int total = 0;
        for (int i = 0; i < pieces.Length; i++)
        {
            every &= pieces[i] == 20;
            total += pieces[i];
        }
        var old = new int[8];
        int left = 16;
        for (int i = 0; i < old.Length; i++)
        {
            old[i] = TripRules.Fit(100, 10, ref left);
        }
        Check(every && total == 160 && old[0] == 100 && old[1] == 60 && old[2] == 0 && old[7] == 0,
            "stock sent: a full vehicle shares its boxes over every stop of the route, where the old fill gave the first two stops everything and the last six nothing");
        // Fewer boxes than stops: the stop the round begins at moves with the day, so every stop is served in turn.
        var days = new bool[5];
        for (int day = 0; day < 5; day++)
        {
            var p = new int[5];
            AllotRun(new[] { 3, 3, 3, 3, 3 }, 10, 1000, 2, day, p);
            for (int i = 0; i < 5; i++)
            {
                days[i] |= p[i] > 0;
            }
        }
        bool turn = true;
        foreach (bool d in days)
        {
            turn &= d;
        }
        Check(turn, "stock sent: when the boxes are fewer than the stops each stop is first in its turn, so none is left out every day");
        // A shared stock: four stops want 40 each of one product and the warehouse holds 50: none over its want, the stock all given, no stop empty.
        var shared = new int[4];
        int boxes = AllotRun(new[] { 4, 4, 4, 4 }, 10, 50, 100, 0, shared);
        int sum = 0;
        bool fair = boxes == 5;
        for (int i = 0; i < 4; i++)
        {
            sum += shared[i];
            fair &= shared[i] <= 40 && shared[i] >= 10;
        }
        Check(fair && sum == 50, "stock sent: a product the warehouse holds too little of is shared over the stops that want it, none given more than it wants, all of it given");
        // Pieces never over the want, the pool or the room; a partly filled last box counts as a box.
        var part = new int[2];
        int partBoxes = AllotRun(new[] { 1, 1 }, 10, 15, 5, 0, part);
        Check(partBoxes == 2 && part[0] == 10 && part[1] == 5 && TripRules.Allot(0, new int[0], new int[0], new int[0], new int[0], 0, 0, 5, new int[0], new int[0]) == 0
              && AllotRun(new[] { 2 }, 10, 100, 0, 0, new int[1]) == 0,
            "stock sent: a box is never more than its size, a want or the stock, and nothing is loaded with no room");
        // Several products at one stop each take a turn, so the first product does not take the stop's whole share.
        var stopOf = new[] { 0, 0 };
        var poolOf = new[] { 0, 1 };
        var wants = new[] { 100, 100 };
        var sizes = new[] { 10, 10 };
        var got = new int[2];
        TripRules.Allot(2, stopOf, poolOf, wants, sizes, 1, 0, 6, new[] { 1000, 1000 }, got);
        Check(got[0] == 30 && got[1] == 30, "stock sent: a stop's products take the boxes by turns, so a long list of goods is not cut off at its second product");
    }

    // Units through a trip in every ending. A warehouse, three shops and a trip are modelled with the mod's own rules (Allot, Settle,
    // the trip's stops and the save's line); every piece is in the warehouse, on a stop's shelves or recorded lost, never anywhere else.
    private static void TripEndings()
    {
        string[] endings =
        {
            "arrives", "late", "no driver", "no manager", "full shop", "closed shop", "day ends", "save and load", "option off", "city left", "stale", "stop left",
            "warehouse sold some", "game counted none"
        };
        bool all = true;
        string failed = "none";
        foreach (string ending in endings)
        {
            bool ok = TripEnding(ending, out string why);
            all &= ok;
            failed = ok ? failed : ending + ": " + why;
        }
        Check(all, "stock sent: every piece is in the warehouse, on a shop's shelves or counted lost in every ending (" + failed + ")");
    }

    private static bool TripEnding(string ending, out string why)
    {
        why = string.Empty;
        const int ShopRoom = 1000;
        int warehouse = 500;
        var shop = new[] { 0, 0, 0 };
        int lostPieces = 0;
        var want = new[] { 120, 120, 120 };
        const int room = 20, size = 10;
        int initial = warehouse;
        // The departure.
        bool away = ending == "no driver" || ending == "no manager";
        var tally = new RouteTally();
        tally.Clear(10, "W");
        var trip = new Trip { Plan = "p", From = "w", Slot = 0 };
        if (!away)
        {
            var stopOf = new[] { 0, 1, 2 };
            var pieces = new int[3];
            int[] pool = { warehouse };
            TripRules.Allot(3, stopOf, new[] { 0, 0, 0 }, want, new[] { size, size, size }, 3, 10, room, pool, pieces);
            for (int k = 0; k < 3; k++)
            {
                var st = new TripStop { Place = "s" + k, Arrive = 100 + k * 30, Loaded = pieces[k] };
                st.Items.Add("x");
                st.Pieces.Add(pieces[k]);
                trip.Stops.Add(st);
                tally.Left += want[k] - pieces[k];
            }
            trip.Out = true;
            trip.Depart = 60;
            trip.Back = 300;
            tally.Taken = trip.Pieces();
            tally.Runs = 1;
        }
        else
        {
            tally.Left = want[0] + want[1] + want[2];
            tally.Why = ending == "no driver" ? TripWhy.NoDriver : TripWhy.NoManager;
        }
        if (ending == "save and load" || ending == "option off" || ending == "city left")
        {
            // The line the save keeps, read back: the trip comes back as it was, with what each stop was loaded with.
            var state = new TripState();
            state.Trips.Add(trip);
            TripState back = TripStore.Read(TripStore.Write(state));
            if (back == null || back.Trips.Count != 1 || back.Trips[0].Pieces() != trip.Pieces() || back.Trips[0].LoadedPieces() != trip.LoadedPieces()
                || back.Trips[0].Stops.Count != trip.Stops.Count || back.Trips[0].Carried() != trip.Carried())
            {
                why = "the saved trip is not the trip";
                return false;
            }
            trip = back.Trips[0];
        }
        // The stops.
        for (int k = 0; k < trip.Stops.Count; k++)
        {
            TripStop st = trip.Stops[k];
            if (ending == "day ends" && k == 1)
            {
                tally.Day = 10;   // the day's tally is read at the turn; the stop falls in the next day's
                tally = new RouteTally();
                tally.Clear(11, "W");
            }
            if (ending == "stale")
            {
                break;
            }
            int load = st.Pieces[0], held = warehouse, moved, credited;
            st.Done = true;
            if (ending == "stop left" && k == 1)
            {
                tally.Returned += load;
                st.Pieces[0] = 0;
                continue;
            }
            moved = Math.Min(load, warehouse);
            if (ending == "warehouse sold some" && k == 0)
            {
                warehouse -= 30;   // sold or used between the departure and the stop
                held = warehouse;
                moved = Math.Min(load, warehouse);
            }
            if (ending == "full shop" && k == 2)
            {
                moved = Math.Min(moved, Math.Max(0, ShopRoom - 1000));   // no room on its shelves: the game hands the goods back
            }
            credited = ending == "game counted none" && k == 2 ? 0 : moved;
            TripWhy kind = TripRules.Settle(load, held, moved, credited, out int delivered, out int handed, out int gone);
            if (delivered + handed + gone != load)
            {
                why = "settle does not add up";
                return false;
            }
            if ((handed > 0 && kind == TripWhy.None) || (gone > 0 && (kind & TripWhy.Lost) == 0))
            {
                why = "a failure with no reason";
                return false;
            }
            warehouse -= moved;
            shop[k] += delivered;
            lostPieces += gone;
            tally.Delivered += delivered;
            tally.Returned += handed;
            tally.Lost += gone;
            st.Pieces[0] = delivered;
        }
        if (ending == "stale")
        {
            int carried = trip.Carried();
            tally.Returned += carried;
        }
        int now = warehouse + shop[0] + shop[1] + shop[2] + lostPieces;
        if (ending == "warehouse sold some")
        {
            now += 30;
        }
        if (now != initial)
        {
            why = "pieces " + initial + " before and " + now + " after";
            return false;
        }
        if (!away && ending != "day ends" && TripTally.Unaccounted(tally) != 0)
        {
            why = "the tally is out by " + TripTally.Unaccounted(tally);
            return false;
        }
        return true;
    }

    private static void TripTallyBook()
    {
        var book = new TripTally();
        RouteTally a = book.Of("a", 5, "North");
        a.Runs = 2;
        a.Taken = 100;
        a.Delivered = 70;
        a.Returned = 20;
        a.Lost = 10;
        a.Left = 40;
        a.Why = TripWhy.Full | TripWhy.ShelvesFull;
        book.Of("b", 6, "South").Runs = 1;
        book.Of("quiet", 5, "Idle");
        var written = new List<KeyValuePair<string, RouteTally>>();
        int n = book.Take(6, false, written);
        string line = TripTally.Line(a);
        Check(n == 2 && written[0].Key == "a" && written[1].Key == "quiet" && book.Peek("b", 6) != null && book.Peek("a", 5) == null && written[1].Value.Quiet && !a.Quiet
              && TripTally.Unaccounted(a) == 0
              && line == "Delivery trips (day 5, North): 2 run(s), taken 100 piece(s), delivered 70, returned 20, lost 10; wanted but not loaded 40; because: vehicle full, shelves and storage full.",
            "stock sent: a route's day is one line, units taken, delivered, returned and lost and what was left behind and why, written once the day has turned");
        RouteTally again = book.Of("b", 7, "South");
        Check(again.Day == 7 && again.Runs == 0 && book.Take(0, true, written) == 1 && book.Peek("b", 7) == null,
            "stock sent: a new day starts a route's tally afresh, and standing down writes every day that is open");
        Check(TripTally.First(TripWhy.Full | TripWhy.NoDriver) == TripWhy.NoDriver && TripTally.First(TripWhy.Full | TripWhy.NoStock) == TripWhy.NoStock && TripTally.First(TripWhy.None) == TripWhy.None
              && TripTally.ReasonKey(TripWhy.ShelvesFull) == "reallife_trips_why_shelves" && TripTally.AdviceKey(TripWhy.Full) == "reallife_trips_fix_full",
            "stock sent: the reason a player is told first is the one they can mend soonest, and each reason has its words and its advice");
        // The log: a run keeps what was loaded, left behind and why; a log from before reads as none of these.
        var s = new TripState();
        s.Log.Add(new TripLog { Day = 3, Plan = "p", Pieces = 40, Loaded = 60, Left = 25, Why = (int)(TripWhy.Full | TripWhy.ShelvesFull) });
        var trip = new Trip { Plan = "p", From = "w", Slot = 0, Out = true, Depart = 60, Back = 200 };
        var stop = new TripStop { Place = "a~1", Arrive = 100, Loaded = 12 };
        stop.Items.Add("x");
        stop.Pieces.Add(9);
        trip.Stops.Add(stop);
        s.Trips.Add(trip);
        TripState back = TripStore.Read(TripStore.Write(s));
        string olderLog = "v=1^L|3|60|p|0|2|40|7|8|5400|60|120|300^D|-1";
        TripState older = TripStore.Read(olderLog);
        Check(back != null && back.Log[0].Loaded == 60 && back.Log[0].Left == 25 && back.Log[0].Why == (int)(TripWhy.Full | TripWhy.ShelvesFull) && back.Trips[0].Stops[0].Loaded == 12
              && TripStore.Write(back) == TripStore.Write(s) && older != null && older.Log[0].Loaded == 0 && older.Log[0].Pieces == 40
              && TripStore.Read("v=1^L|3|60|p|0|2|40|7|8|5400|60|120|300|1|2") == null,
            "stock sent: the save keeps what each stop was loaded with and each run's loaded, left behind and why; a save from before reads as none of them");
    }

    // Goods that come to a place read at another pace arrive not yet due if they were not due where they came from, at every pace.
    private static void SpoilArrival()
    {
        int[] paces = { SpoilRules.PaceLeast, 50, 75, 100, 150, 200, 300, SpoilRules.PaceMost };
        int[] lives = { 3, 4, 5, 6, 7, 8, 12, 14, 90 };
        bool never = true;
        bool kept = true;
        bool fresh = true;
        string sample = "none";
        foreach (int usual in lives)
        {
            foreach (int from in paces)
            {
                foreach (int to in paces)
                {
                    int was = SpoilRules.Scale(usual, from), now = SpoilRules.Scale(usual, to);
                    for (int age = 0; age <= was + 2; age++)
                    {
                        var batch = new List<Cohort> { new Cohort(100 - age, 7) };
                        SpoilRules.Arrive(batch, usual, from, to, 100);
                        kept &= SpoilRules.Total(batch) == 7;
                        bool due = SpoilRules.Expired(batch, now, 100) > 0;
                        if (age < was && due)
                        {
                            never = false;
                            sample = "life " + usual + " pace " + from + " to " + to + " age " + age;
                        }
                        if (age == 0 && due)
                        {
                            fresh = false;
                        }
                    }
                }
            }
        }
        Check(never && fresh && kept,
            "stock sent: goods arriving at a place that reads age at another pace are not thrown out at once if they were not due, and fresh goods never, at every pace and life (" + sample + ")");
        // The old way used the cold life for goods that came to a warm place: three days of meat at a quarter pace was thrown out on arrival.
        var meat = new List<Cohort> { new Cohort(98, 4) };
        int cold = SpoilRules.UsualLife("ba:itemname_groundbeef", false, true);
        SpoilRules.Rebase(meat, cold, 100, SpoilRules.PaceLeast, 100);
        bool wasThrown = SpoilRules.Expired(meat, SpoilRules.Scale(3, SpoilRules.PaceLeast), 100) > 0;
        var warm = new List<Cohort> { new Cohort(98, 4) };
        SpoilRules.Arrive(warm, 3, 100, SpoilRules.PaceLeast, 100);
        Check(wasThrown && SpoilRules.Expired(warm, SpoilRules.Scale(3, SpoilRules.PaceLeast), 100) == 0,
            "stock sent: goods arriving are aged by the life of the place they come to, not the fridge's, which had thrown meat out the day it came at a short shelf life");
    }

    private static void StockSentSource()
    {
        string trips = ModSource(@"Economy\Logistics\LogisticsDirector.Trips.cs"), tell = ModSource(@"Economy\Logistics\LogisticsDirector.Tell.cs");
        string spoilGame = ModSource(@"Economy\Logistics\LogisticsGame.Spoil.cs"), game = ModSource(@"Economy\Logistics\LogisticsGame.cs");
        string spoil = ModSource(@"Economy\Logistics\LogisticsDirector.Spoil.cs"), page = ModSource(@"Ui\BizMan\Ops\BizManScreen.Trips.cs");
        string leave = MemberText(trips, "private void Leave(Trip t)"), arrive = MemberText(trips, "private void Arrive(Trip t, TripStop stop)");
        Check(leave.Contains("TripRules.Allot(") && !leave.Contains("TripRules.Fit(") && leave.Contains("Behind(plan, from, null, wantedAll,")
              && leave.Contains("Behind(plan, from, LogisticsGame.Key(d.deliveryTargetAddress), behind, why)") && leave.Contains("tally.Taken += loadedAll"),
            "stock sent: a trip's load is handed round the stops, and a trip that cannot leave, or leaves goods behind, says so");
        Check(arrive.Contains("TripRules.Settle(") && arrive.Contains("Missed(plan, from, stop.Place, back + lost, why)") && arrive.Contains("LogisticsGame.Deliver(plan, from, stop, _moved, _credited, _wasHeld)")
              && MemberText(game, "public static bool Deliver(").Contains("credited.Add(") && MemberText(game, "public static bool Deliver(").Contains("finally"),
            "stock sent: what a stop took is counted from the stop's own count, what it did not take is handed back and told, and the targets are put back whatever happens");
        Check(!spoilGame.Contains("deliveryTransactions") && !spoilGame.Contains("DeliveryTransaction"),
            "stock sent: food thrown out for age is not written into a warehouse's record of goods sent");
        Check(spoil.Contains("SpoilRules.Arrive(_taken, DestinationLife(toKey, item)") && !spoil.Contains("SpoilRules.UsualLife(item, LogisticsGame.Weighed(item), true), fromPace"),
            "stock sent: goods a trip moves are aged by the place they come to");
        Check(tell.Contains("Notifications.Show(NotificationType.Warning, \"reallife_trips_fail_note\"") && tell.Contains("if (t.Told)") && tell.Contains("Logger?.Info(TripTally.Line(t))")
              && MemberText(trips, "private void TripsSecond(int now)").Contains("TallyWrite(now / TripRules.DayMinutes, false)")
              && MemberText(trips, "private void TripsStop()").Contains("TallyWrite(0, true)") && trips.Contains("Abandoned(t);"),
            "stock sent: a failed delivery is told once a day for a route with its reason and advice, and Player.log gets one line for each route each day");
        Check(page.Contains("t.Carried()") && page.Contains("LastOutcome(") && page.Contains("NotSent(trips, route)") && page.Contains("reallife_trips_parked_away"),
            "stock sent: BizMan's route and warehouse pages say how many pieces a vehicle carries and when it is back, what the last run delivered and why the rest did not arrive");
        Dictionary<string, string> en = LocaleFile("en");
        string[] keys =
        {
            "reallife_trips_fail_note", "reallife_trips_its_stops", "reallife_trips_why_full", "reallife_trips_why_stock", "reallife_trips_why_shelves", "reallife_trips_why_stop",
            "reallife_trips_why_driver", "reallife_trips_why_manager", "reallife_trips_why_stale", "reallife_trips_why_lost", "reallife_trips_why_unknown", "reallife_trips_fix_full",
            "reallife_trips_fix_stock", "reallife_trips_fix_shelves", "reallife_trips_fix_stop", "reallife_trips_fix_driver", "reallife_trips_fix_manager", "reallife_trips_fix_stale",
            "reallife_trips_fix_lost", "reallife_trips_on_route_load", "reallife_trips_parked_away", "reallife_trips_stop_short", "reallife_trips_road_carry", "reallife_trips_last_moved",
            "reallife_trips_last_left", "reallife_trips_today_left"
        };
        string missing = string.Empty;
        foreach (string k in keys)
        {
            missing += en.ContainsKey(k) ? string.Empty : k + " ";
        }
        int longest = 0;
        foreach (TripWhy w in new[] { TripWhy.Full, TripWhy.NoStock, TripWhy.ShelvesFull, TripWhy.StopGone, TripWhy.NoDriver, TripWhy.NoManager, TripWhy.Stale, TripWhy.Lost })
        {
            if (en.TryGetValue("reallife_trips_fail_note", out string note) && en.TryGetValue(TripTally.ReasonKey(w), out string r) && en.TryGetValue(TripTally.AdviceKey(w), out string a))
            {
                longest = Math.Max(longest, note.Replace("{warehouse}", new string('w', 30)).Replace("{count}", "99999").Replace("{place}", new string('p', 30)).Replace("{reason}", r).Replace("{advice}", a).Length);
            }
        }
        Check(missing.Length == 0 && longest > 0 && longest <= 240,
            "stock sent: every text is in English and the longest notice, with a 30 letter warehouse and shop, fits 240 characters (" + longest + " " + missing + ")");
    }
}
