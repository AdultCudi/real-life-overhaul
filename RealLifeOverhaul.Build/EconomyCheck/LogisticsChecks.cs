using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy;

internal static partial class Program
{
    // Delivery trips and food spoilage (Economy/Logistics): the trips' departures, timeline, loads, fuel and wages, the stops
    // shared over a warehouse's vehicles, the rivals' supply costs, the goods' batches and their shelf life, and both save lines.
    private static void Logistics()
    {
        TripTimes();
        TripCosts();
        TripSave();
        SpoilBatches();
        SpoilLives();
        SpoilSave();
        SendNowCap();
        LogisticsTexts();
    }

    // BizMan's Send now, once a game day for each route: a second send on the day is refused, the next day is allowed, a route
    // is counted alone, the save line reads back (its day only), and a save from before it counts every route as not sent today.
    private static void SendNowCap()
    {
        SendNowLimit.Reset();
        bool fresh = SendNowLimit.Allowed("route-a", 40) && SendNowLimit.Allowed("route-b", 40) && SendNowLimit.Allowed(null, 40);
        SendNowLimit.Sent("route-a", 40);
        Check(fresh && !SendNowLimit.Allowed("route-a", 40) && SendNowLimit.Allowed("route-a", 41) && SendNowLimit.Allowed("route-b", 40),
            "send now: a second send of a route on the same day is refused, the next day is allowed, and another route is counted alone");
        SendNowLimit.Sent("route|b^1", 40);
        string line = SendNowLimit.Write(40);
        SendNowLimit.Reset();
        bool read = SendNowLimit.Read(line);
        Check(read && !SendNowLimit.Allowed("route-a", 40) && !SendNowLimit.Allowed("route|b^1", 40) && SendNowLimit.Allowed("route-a", 41) && SendNowLimit.Write(40) == line
              && SendNowLimit.Write(41) == "v=" + SendNowLimit.Version,
            "send now: the save line gives the routes sent today back as they were, and writes nothing for an earlier day");
        var old = new Dictionary<string, string>();
        SendNowLimit.ReadSave(old);
        bool noKey = SendNowLimit.Allowed("route-a", 40);
        SendNowLimit.WriteSave(old, 40);
        var later = new Dictionary<string, string> { { SendNowLimit.Key, "v=9^route-a|40" } };
        SendNowLimit.ReadSave(later);
        bool unread = SendNowLimit.Allowed("route-a", 40);
        SendNowLimit.WriteSave(later, 40);
        var broken = new Dictionary<string, string> { { SendNowLimit.Key, "v=1^route-a|x" } };
        SendNowLimit.ReadSave(broken);
        Check(noKey && old.TryGetValue(SendNowLimit.Key, out string written) && written == "v=1" && unread && later[SendNowLimit.Key] == "v=9^route-a|40"
              && SendNowLimit.Allowed("route-a", 40) && !SendNowLimit.Read("v=1^a|1^b"),
            "send now: a save from before has no line and every route may be sent; a line this version cannot read is kept as it was and allows the send");
        SendNowLimit.Reset();
        string managers = ModSource(@"Ui\BizMan\Ops\BizManScreen.Managers.cs"), trips = ModSource(@"Ui\BizMan\Ops\BizManScreen.Trips.cs");
        Check(managers.Contains("Disabled = today || !CanSend(route)") && managers.Contains("\"reallife_ui_bizops_sent_today\"")
              && MemberText(managers, "private bool SendNow()").Contains("SentToday(route)") && MemberText(managers, "private void Sent(string id)").Contains("SentToday(route)")
              && MemberText(managers, "private void Sent(string id)").Contains("Economy.SendNowLimit.Sent(id, TimeHelper.CurrentDay)")
              && MemberText(trips, "private bool SendTrips(LogisticsManagerPlan route)").Contains("Economy.SendNowLimit.Sent(id, TimeHelper.CurrentDay)")
              && !managers.Contains("RunMainGameTick"),
            "send now: both ways of sending, the game's run and the trips, refuse a route sent today and use the day only when something went out; the 02:00 run is not touched");
        Check(ModSource(@"Core\ModState.cs").Contains("Economy.SendNowLimit.ReadSave(data);") && ModSource(@"Core\ModState.cs").Contains("Economy.SendNowLimit.WriteSave(data, TimeHelper.CurrentDay);")
              && ModSource(@"Core\ModState.cs").Contains("Economy.SendNowLimit.Reset();"),
            "send now: the day each route was sent is read with the save, written with it and let go when there is no save");
    }

    private static void TripTimes()
    {
        var hours = new List<int>();
        Check(TripRules.Hours(2, 2, 12, hours) == 2 && hours[0] == 2 && hours[1] == 14
              && TripRules.Hours(20, 3, 4, hours) == 3 && hours[0] == 0 && hours[1] == 4 && hours[2] == 20
              && TripRules.Hours(5, 4, 6, hours) == 4 && hours[0] == 5 && hours[3] == 23 && TripRules.Hours(3, 3, 12, hours) == 2,
            "trips: a route's departure hours are its first and every gap after it, in the day's order, each once");
        int day = TripRules.DayMinutes;
        Check(TripRules.Next(10 * day + 60, 2, 2, 12, hours) == 10 * day + 120 && TripRules.Next(10 * day + 120, 2, 2, 12, hours) == 10 * day + 14 * 60
              && TripRules.Next(10 * day + 14 * 60, 2, 2, 12, hours) == 11 * day + 120 && TripRules.Next(10 * day + 23 * 60 + 59, 2, 1, 12, hours) == 11 * day + 120,
            "trips: the next departure is the first of the route's hours strictly after the last, into the next day");
        TripRules.Default(false, out int f1, out int r1, out int g1);
        TripRules.Default(true, out int f2, out int r2, out int g2);
        Check(f1 == TripRules.GameRunHour && r1 == 1 && f2 == TripRules.GameRunHour && r2 == 2 && g2 == 12 && g1 == 12,
            "trips: with no departures set a route leaves at the game's own 02:00, and twice a day (02:00 and 14:00) when it carries goods that spoil");
        var legs = new[] { 10, 5, 12 };
        var arrive = new int[2];
        int back = TripRules.Timeline(1000, legs, 2, arrive);
        Check(arrive[0] == 1000 + TripRules.LoadMinutes + 10 && arrive[1] == arrive[0] + TripRules.UnloadMinutes + 5
              && back == arrive[1] + TripRules.UnloadMinutes + 12 && TripRules.Timeline(50, new[] { 0 }, 0, new int[0]) == 50 + TripRules.LoadMinutes,
            "trips: a trip loads, drives each leg, unloads at each stop and drives back; with no stop it never leaves the yard");
    }

    private static void TripCosts()
    {
        Check(TripRules.Drive(22000, TripVehicle.Van) == 60 && TripRules.Drive(1, TripVehicle.Van) == 1 && TripRules.Drive(0, TripVehicle.Truck) == 1
              && TripRules.Drive(18000, TripVehicle.Truck) == 60 && TripRules.Drive(26000, TripVehicle.Car) == 60 && TripRules.Road(1000) == 1350,
            "trips: drive time by the vehicle's pace through the city, a minute at least; an unmeasured road is the straight line and 35 per cent");
        Check(TripRules.Fuel(100000, TripVehicle.Van, 130) == 1560 && TripRules.Fuel(100000, TripVehicle.Truck, 130) == 3380 && TripRules.Fuel(0, TripVehicle.Van, 130) == 0
              && TripRules.Fuel(10000, TripVehicle.Car, 0) == 0,
            "trips: fuel is the vehicle's litres per 100 km over the distance at the pump's price (100 km in a van at 1.30: 15.60)");
        Check(TripRules.Wage(2000, 90) == 3000 && TripRules.Wage(2000, 0) == 0 && TripRules.Wage(-5, 60) == 0,
            "trips: wages are the driver's hourly wage over the trip's minutes");
        int left = 3;
        int a = TripRules.Fit(25, 10, ref left), afterA = left;
        int b = TripRules.Fit(25, 10, ref left);
        int c = TripRules.Fit(5, 10, ref left);
        Check(TripRules.Boxes(25, 10) == 3 && TripRules.Boxes(0, 10) == 0 && TripRules.Boxes(3, 0) == 3 && a == 25 && afterA == 0 && b == 0 && c == 0,
            "trips: goods go in whole boxes, never more than wanted, and nothing once the vehicle is full");
        left = 2;
        Check(TripRules.Fit(25, 10, ref left) == 20 && left == 0, "trips: a load the vehicle cannot hold whole takes the boxes left");
        var shares = new int[3];
        Check(TripRules.Shares(new[] { 2, 0, 3 }, 3, 5, shares) == 5 && shares[0] == 2 && shares[1] == 0 && shares[2] == 3
              && TripRules.Shares(new[] { 2, 0, 3 }, 3, 7, shares) == 7 && shares[0] == 3 && shares[1] == 0 && shares[2] == 4
              && TripRules.Shares(new[] { 2, 0, 3 }, 3, 3, shares) == 3 && shares[0] == 2 && shares[2] == 1
              && TripRules.Shares(new[] { 0, 0, 0 }, 3, 4, shares) == 0,
            "trips: a route's stops are shared over the warehouse's vehicles by the stops each serves, the manager's extra handed out from the first, none to a slot with no driver");
        int trip = TripRules.RivalTrip(2000, 130);
        Check(trip > 0 && TripRules.RivalTrip(4000, 130) > trip && TripRules.RivalDelay(0) == 0 && TripRules.RivalDelay(30000) == 6 && TripRules.RivalDelay(2000000) == 40
              && TripRules.RivalDay(100000, trip, 2, 10) == trip + 1200 && TripRules.RivalDay(1000, 900, 0, 0) == 250 && TripRules.RivalDay(0, trip, 5, 5) == 0
              && TripRules.RivalDay(-500, trip, 5, 5) == 0,
            "trips: a rival shop's day pays a van's round trip, the hours on the road and its spoiled share, never over a quarter of the day nor on a day it made nothing");
        var log = new List<TripLog>
        {
            new TripLog { Day = 3, Plan = "a", Pieces = 700, FuelCents = 70, WageCents = 140 },
            new TripLog { Day = 9, Plan = "a", Pieces = 70, FuelCents = 700, WageCents = 1400 },
            new TripLog { Day = 10, Plan = "b", Pieces = 7, FuelCents = 7, WageCents = 7 },
        };
        TripRules.Week(log, "a", 9, out int runs, out int pieces, out int fuel, out int wages);
        TripRules.Trim(log, 10);
        Check(runs == 2 && pieces == 110 && fuel == 110 && wages == 220 && log.Count == 2 && log[0].Day == 9,
            "trips: the week's figures are a day's average over seven days of a route's runs; the log keeps seven days");
    }

    private static void TripSave()
    {
        var s = new TripState { RivalDay = 41 };
        RouteTimes r = s.Route("plan|1^x;y");
        r.First = 6;
        r.Runs = 2;
        r.Gap = 8;
        r.Last = 41 * TripRules.DayMinutes + 360;
        r.Set = true;
        s.Route("plan2");
        var t = new Trip { Plan = "plan|1^x;y", From = "Main%20St~3", Slot = 1, Vehicle = "v,1", Driver = "d=2", Depart = 59400, Back = 59460, Out = true, Metres = 5400,
            Boxes = 7, Room = 8, FuelCents = 120, WageCents = 300, Kind = TripVehicle.Truck };
        var st = new TripStop { Place = "Side/St~9", Arrive = 59430, Done = true };
        st.Items.Add("ba:itemname_apple");
        st.Pieces.Add(40);
        st.Items.Add("odd:name");
        st.Pieces.Add(3);
        t.Stops.Add(st);
        t.Stops.Add(new TripStop { Place = "Empty~1", Arrive = 59440 });
        s.Trips.Add(t);
        s.Trips.Add(new Trip { Plan = "plan2", From = "W~1", Slot = 0, Depart = 60000 });
        s.Log.Add(new TripLog { Day = 41, Minute = 360, Plan = "plan2", Slot = 0, Stops = 2, Pieces = 43, Boxes = 7, Room = 8, Metres = 5400, Minutes = 60, FuelCents = 120, WageCents = 300 });
        string line = TripStore.Write(s);
        TripState back = TripStore.Read(line);
        Trip bt = back != null && back.Trips.Count == 2 ? back.Trips[0] : null;
        Check(back != null && TripStore.Write(back) == line && back.RivalDay == 41 && back.Routes.Count == 2 && back.Routes["plan|1^x;y"].Set && back.Routes["plan|1^x;y"].Gap == 8
              && !back.Routes["plan2"].Set && bt != null && bt.Out && bt.Kind == TripVehicle.Truck && bt.Vehicle == "v,1" && bt.Driver == "d=2" && bt.Stops.Count == 2
              && bt.Stops[0].Place == "Side/St~9" && bt.Stops[0].Done && bt.Stops[0].Items[1] == "odd:name" && bt.Stops[0].Pieces[0] == 40 && bt.Stops[1].Items.Count == 0
              && !back.Trips[1].Out && back.Log.Count == 1 && back.Log[0].Pieces == 43 && back.Busy("W~1", 0) == back.Trips[1] && back.Busy("W~1", 1) == null,
            "trips' save: routes, trips on the road with their stops and goods, and the week's log read back to the same line, their marks escaped");
        var other = new TripState { RivalDay = 41 };
        other.Route("plan2");
        RouteTimes r2 = other.Route("plan|1^x;y");
        r2.First = 6;
        r2.Runs = 2;
        r2.Gap = 8;
        r2.Last = r.Last;
        r2.Set = true;
        other.Trips.AddRange(back.Trips);
        other.Log.AddRange(back.Log);
        Check(TripStore.Write(other) == line, "trips' save: the same state writes the same line whatever order its routes were made in");
        TripState empty = TripStore.Read(null);
        Check(empty != null && empty.Trips.Count == 0 && empty.Routes.Count == 0 && TripStore.Read(string.Empty) != null && empty.RivalDay == -1,
            "trips' save: a save from before has no line and loads with no trip on the road");
        Check(TripStore.Read("v=" + (TripStore.Version + 1) + "^D|3") == null && TripStore.Read("v=1^R|p|25|1|12|0|1") == null && TripStore.Read("v=1^T|p|w|x") == null
              && TripStore.Read("v=1^Q|1") == null && TripStore.Read("v=1^L|1|2|p|0|1|2|3|4|5|6|7") == null,
            "trips' save: another version or a broken line reads as none, so it is kept as it was and the trips start afresh");
    }

    private static void SpoilBatches()
    {
        var b = new List<Cohort>();
        SpoilRules.Add(b, 5, 10);
        SpoilRules.Add(b, 5, 5);
        SpoilRules.Add(b, 7, 3);
        SpoilRules.Add(b, 6, 2);
        Check(b.Count == 3 && b[0].Day == 5 && b[0].Count == 15 && b[1].Day == 6 && b[2].Day == 7 && SpoilRules.Total(b) == 20,
            "spoilage: stock come in on a day is a batch of that day, merged with the same day's, oldest first");
        var taken = new List<Cohort>();
        Check(SpoilRules.Take(b, 16, taken) == 16 && b.Count == 2 && b[0].Day == 6 && b[0].Count == 1 && taken.Count == 2 && taken[0].Count == 15 && taken[1].Day == 6
              && SpoilRules.Take(b, 50, null) == 4 && b.Count == 0,
            "spoilage: what is sold, used or moved is taken from the oldest batch first");
        var many = new List<Cohort>();
        for (int d = 1; d <= SpoilRules.CohortsMost + 3; d++)
        {
            SpoilRules.Add(many, d * 2, 10);
        }
        bool rising = true;
        for (int i = 1; i < many.Count; i++)
        {
            rising &= many[i].Day > many[i - 1].Day;
        }
        Check(many.Count == SpoilRules.CohortsMost && SpoilRules.Total(many) == (SpoilRules.CohortsMost + 3) * 10 && rising && many[0].Day <= 6,
            "spoilage: past " + SpoilRules.CohortsMost + " batches the two oldest fold into one at their mean day, never younger, every piece kept");
        var m = new List<Cohort> { new Cohort(3, 10), new Cohort(4, 10) };
        Check(SpoilRules.Match(m, 15, 9) == 0 && SpoilRules.Total(m) == 15 && m[0].Day == 3 && m[0].Count == 5 && SpoilRules.Match(m, 20, 9) == 5 && m[m.Count - 1].Day == 9,
            "spoilage: a place's batches follow what it holds: less is sold from the oldest, more came in today");
        var e = new List<Cohort> { new Cohort(3, 4), new Cohort(5, 6), new Cohort(8, 1) };
        Check(SpoilRules.Expired(e, 3, 8) == 10 && SpoilRules.Expired(e, 3, 7) == 4 && SpoilRules.Expired(e, 0, 99) == 0 && SpoilRules.SpoilsIn(e, 3, 5) == 1
              && SpoilRules.SpoilsIn(e, 3, 3) == 3 && SpoilRules.SpoilsIn(e, 0, 3) == -1 && SpoilRules.SpoilsIn(new List<Cohort>(), 3, 3) == -1,
            "spoilage: goods stocked on a day are thrown out at the turn into that day and their life; the oldest batch says when the next spoil");
        var place = new SpoilPlace();
        place.Note(10, 2, 40, 300);
        place.Note(10, 1, 0, 100);
        place.Note(17, 5, 10, 50);
        place.Sum(17, false, out int sp, out int came, out int cents);
        place.Sum(17, true, out int spToday, out int cameToday, out int _);
        place.Sum(16, false, out int spOld, out int _, out int _);
        Check(sp == 5 && came == 10 && cents == 50 && spToday == 5 && cameToday == 10 && spOld == 0 && SpoilRules.Percent(5, 10) == 50 && SpoilRules.Percent(1, 0) == -1,
            "spoilage: a place's seven days of pieces in and spoiled, a day's slot cleared when a new day takes it");
    }

    private static void SpoilLives()
    {
        Check(SpoilRules.Of("ba:itemname_apple", false) == SpoilClass.Produce && SpoilRules.Of("ba:itemname_milk", false) == SpoilClass.Dairy
              && SpoilRules.Of("ba:itemname_croissant", false) == SpoilClass.Bakery && SpoilRules.Of("ba:itemname_groundbeef", false) == SpoilClass.Meat
              && SpoilRules.Of("ba:itemname_burger", false) == SpoilClass.Prepared && SpoilRules.Of("ba:itemname_frozenfood", false) == SpoilClass.Frozen
              && SpoilRules.Of("ba:itemname_sodacan", false) == SpoilClass.Packaged && SpoilRules.Of("ba:itemname_somethingnew", true) == SpoilClass.Produce
              && SpoilRules.Of(null, false) == SpoilClass.Packaged,
            "spoilage: a product's kind by its name; anything weighed at the till is produce; packaged goods and the rest never spoil");
        bool longer = true;
        for (int c = (int)SpoilClass.Produce; c <= (int)SpoilClass.Frozen; c++)
        {
            longer &= SpoilRules.Life((SpoilClass)c, false) > 0 && SpoilRules.Life((SpoilClass)c, true) > SpoilRules.Life((SpoilClass)c, false);
        }
        Check(longer && SpoilRules.Life(SpoilClass.Packaged, false) == 0 && SpoilRules.Life(SpoilClass.Packaged, true) == 0 && SpoilRules.Life("ba:itemname_milk", false, true) == 14,
            "spoilage: every kind of fresh goods keeps longer in a fridge or a freezer; packaged goods never spoil");
        Check(SpoilRules.IsCold("ba:itemname_industrialfridge") && SpoilRules.IsCold("ba:itemname_smallindustrialfreezer") && SpoilRules.IsCold("ba:itemname_drinksfridge")
              && !SpoilRules.IsCold("ba:itemname_storageshelf") && !SpoilRules.IsCold(null),
            "spoilage: a fridge or a freezer of any kind keeps goods cold");
        Check(SpoilRules.OrderCap(2000, 4) == 8 && SpoilRules.OrderCap(2000, 0) == int.MaxValue && SpoilRules.OrderCap(2000, 90) == int.MaxValue && SpoilRules.OrderCap(1, 3) == 1,
            "spoilage: a manager orders no more of a short-lived product than sells before it spoils; goods that keep longer have no cap");
        Check(SpoilRules.RivalLoss("ba:businesstype_fruitandvegetablestore") == 900 * (SpoilRules.RivalHandlingMille + 0) / 1000
              && SpoilRules.RivalLoss("ba:businesstype_fastfoodrestaurant") > 0 && SpoilRules.RivalLoss("ba:businesstype_jewelrystore") == 0 && SpoilRules.RivalLoss(null) == 0
              && SpoilRules.RivalLoss("ba:businesstype_cafe") >= SpoilRules.RivalFresh("ba:businesstype_cafe") * SpoilRules.RivalHandlingMille / 1000,
            "spoilage: a rival shop loses its fresh share of sales to handling, and more where its goods outlive their life between deliveries");
    }

    private static void SpoilSave()
    {
        var s = new SpoilState { Told = 30 };
        SpoilPlace p = s.Place("Main%20St~3");
        SpoilRules.Add(p.Of("ba:itemname_milk"), 28, 12);
        SpoilRules.Add(p.Of("ba:itemname_milk"), 30, 4);
        SpoilRules.Add(p.Of("odd;name=x"), 29, 7);
        p.Of("gone");
        p.Turned = 30;
        p.Note(30, 3, 16, 450);
        s.Place("Side|St~1").Turned = 29;
        string line = SpoilStore.Write(s);
        SpoilState back = SpoilStore.Read(line);
        Check(back != null && SpoilStore.Write(back) == line && back.Told == 30 && back.Places.Count == 2 && back.Places["Main%20St~3"].Batches["ba:itemname_milk"].Count == 2
              && back.Places["Main%20St~3"].Batches["odd;name=x"][0].Count == 7 && !back.Places["Main%20St~3"].Batches.ContainsKey("gone") && back.Places["Side|St~1"].Turned == 29,
            "spoilage's save: every place's batches and its seven days read back to the same line, its marks escaped, an empty product left out");
        Check(SpoilStore.Read(null) != null && SpoilStore.Read(null).Places.Count == 0 && SpoilStore.Read("v=" + (SpoilStore.Version + 1) + "^N|1") == null
              && SpoilStore.Read("v=1^P|a|1|0,0|0|0|0|") == null && SpoilStore.Read("v=1^P|a|1|0,0,0,0,0,0,0|0,0,0,0,0,0,0|0,0,0,0,0,0,0|0,0,0,0,0,0,0|x=5:2,4:1") == null,
            "spoilage's save: a save from before has none (all it holds is stocked on the day it loads); another version or batches out of order read as none");
    }

    private static void LogisticsTexts()
    {
        string[] keys =
        {
            "reallife_trips_enabled", "reallife_spoil_enabled", "reallife_trips_test", "reallife_spoil_test", "reallife_trips_test_done", "reallife_trips_test_off",
            "reallife_spoil_test_done", "reallife_spoil_test_off", "reallife_meta_title_trips", "reallife_guide_trips_about", "reallife_guide_trips",
            "reallife_transaction_trips_fuel", "reallife_trips_departures", "reallife_trips_first", "reallife_trips_runs", "reallife_trips_gap",
            "reallife_trips_gap_value", "reallife_trips_next", "reallife_trips_round", "reallife_trips_minutes", "reallife_trips_moved", "reallife_trips_costs",
            "reallife_trips_costs_value", "reallife_trips_vehicles", "reallife_trips_load", "reallife_trips_on_route", "reallife_trips_waiting", "reallife_trips_parked",
            "reallife_trips_no_driver", "reallife_trips_stop_at", "reallife_trips_stop_done", "reallife_trips_road", "reallife_trips_last", "reallife_trips_none_yet",
            "reallife_trips_note", "reallife_trips_send_body", "reallife_trips_sent", "reallife_trips_sent_none", "reallife_spoil_col", "reallife_spoil_tonight",
            "reallife_spoil_days", "reallife_spoil_today", "reallife_spoil_factory", "reallife_spoil_share", "reallife_spoil_note",
            "reallife_trips_tomorrow", "reallife_trips_on_day", "reallife_ui_bizops_sent_today"
        };
        Check(AllIn(keys, out string missing), "trips and spoilage texts: every key in English, Portuguese and Chinese (" + missing + ")");
        Dictionary<string, string> en = LocaleFile("en");
        bool told = false;
        foreach (KeyValuePair<string, string> row in en)
        {
            told |= row.Key.StartsWith("reallife_changes_", StringComparison.Ordinal) && !row.Key.EndsWith("_title", StringComparison.Ordinal)
                    && row.Value.Contains("delivery trips") && row.Value.Contains("spoil");
        }
        Check(told, "trips and spoilage texts: What's new tells of delivery trips and food spoilage");
        Check(en.TryGetValue("reallife_spoil_note", out string note) && note.Length <= 240 && en.TryGetValue("reallife_guide_trips", out string guide)
              && guide.Contains("{spoil_produce}") && guide.Contains("{trips_unload}") && guide.Contains("<b>Delivery trips</b>") && guide.Contains("<b>Food spoilage</b>"),
            "trips and spoilage texts: the notice fits a toast; the Guide's page names both options and the rules' figures");
    }
}
