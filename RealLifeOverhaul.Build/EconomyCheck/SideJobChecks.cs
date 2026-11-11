using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using RealLifeOverhaul;
using RealLifeOverhaul.SideJobs;

// Taxi and delivery shifts in the player's own vehicle (docs/features/side-jobs.md): signed on for at the cab office's and the
// parcel depot's desks, on the game's own work panel, and driven as the ride-hail and courier jobs in desk mode. First the
// desks' gates (licences, seats, room, condition, the tank, the day), then the cab's meter by distance and time and that no
// distance is no fare however long the clock runs, the firm's share, the ride's mark and its tip, a vehicle's room, each
// drop's clock and pay by punctuality, the day's amount, the save's line, the firms' books for their other drivers by the
// same sums, then the sources (the gates in the jobs' director, the jobs reading only the rules, salary income, no per-frame
// work, the option) and the texts.
internal static partial class Program
{
    private const float SideUnit = 22f, SideMetre = 0.195f;        // the ride-hail gig's fare: a fare and a metre (JobCatalogue)
    private const float SideDoor = 50f, SideLeg = 0.085f;          // the courier gig's: a door and a metre

    private static void SideJobShifts()
    {
        SideJobGates();
        SideJobMeter();
        SideJobMark();
        SideJobRuns();
        SideJobDay();
        SideJobSave();
        SideJobBooks();
        SideJobSources();
        SideJobWords();
    }

    private static SideCar SideGood()
    {
        return new SideCar { Found = true, Motor = true, Licensed = true, Endorsed = true, Seats = 5, Cargo = 6, Condition = 100, Fuel = 80 };
    }

    private static void SideJobGates()
    {
        JobDef taxi = JobCatalogue.Find(JobCatalogue.Taxi), courier = JobCatalogue.Find(JobCatalogue.Courier);
        Check(taxi != null && courier != null && taxi.UnitPay == SideUnit && taxi.PayPerMetre == SideMetre && courier.UnitPay == SideDoor && courier.PayPerMetre == SideLeg,
            "side jobs: the checks' figures are the two gigs' own (a fare 22 and 0.195 a metre, a door 50 and 0.085 a metre)");
        SideCar car = SideGood();
        Check(SideJobRules.Refusal(SideKind.Cab, car, 5) == SideRefusal.None && SideJobRules.Refusal(SideKind.Parcels, car, 5) == SideRefusal.None,
            "side jobs: a sound car of the driver's own with the licences is signed on at both desks");
        car.Found = false;
        Check(SideJobRules.Refusal(SideKind.Cab, car, 5) == SideRefusal.NoCar && SideJobRules.Refusal(SideKind.Parcels, car, 5) == SideRefusal.NoCar, "side jobs: no vehicle of the driver's own near, no shift");
        car = SideGood();
        car.Motor = false;
        Check(SideJobRules.Refusal(SideKind.Cab, car, 5) == SideRefusal.NotMotor && SideJobRules.Refusal(SideKind.Parcels, car, 5) == SideRefusal.NotMotor, "side jobs: nothing without an engine");
        car = SideGood();
        car.Licensed = false;
        Check(SideJobRules.Refusal(SideKind.Cab, car, 5) == SideRefusal.Licence && SideJobRules.Refusal(SideKind.Parcels, car, 5) == SideRefusal.Licence,
            "side jobs: both desks ask the licence for the vehicle's own class (a van's is the commercial one)");
        car = SideGood();
        car.Endorsed = false;
        Check(SideJobRules.Refusal(SideKind.Cab, car, 5) == SideRefusal.Endorsement && SideJobRules.Refusal(SideKind.Parcels, car, 5) == SideRefusal.None,
            "side jobs: the cab office asks the taxi endorsement, the depot does not");
        car = SideGood();
        car.Seats = SideJobRules.CabSeats - 1;
        Check(SideJobRules.Refusal(SideKind.Cab, car, 5) == SideRefusal.Seats && SideJobRules.Refusal(SideKind.Parcels, car, 5) == SideRefusal.None, "side jobs: a cab seats four, the driver's included; the depot counts no seats");
        car.Seats = SideJobRules.CabSeats;
        Check(SideJobRules.Refusal(SideKind.Cab, car, 5) == SideRefusal.None, "side jobs: four seats will do for a cab");
        car = SideGood();
        car.Cargo = SideJobRules.CargoLeast - 1;
        Check(SideJobRules.Refusal(SideKind.Parcels, car, 5) == SideRefusal.Room && SideJobRules.Refusal(SideKind.Cab, car, 5) == SideRefusal.None, "side jobs: the depot wants room for parcels, the cab office none");
        car = SideGood();
        car.Condition = SideJobRules.CabCondition - 1;
        Check(SideJobRules.Refusal(SideKind.Cab, car, 5) == SideRefusal.Condition && SideJobRules.Refusal(SideKind.Parcels, car, 5) == SideRefusal.None
              && SideJobRules.CabCondition > SideJobRules.ParcelCondition, "side jobs: a cab must be in better condition than a delivery vehicle");
        car.Condition = SideJobRules.ParcelCondition - 1;
        Check(SideJobRules.Refusal(SideKind.Parcels, car, 5) == SideRefusal.Condition, "side jobs: a delivery vehicle too damaged is not sent out either");
        car = SideGood();
        car.Fuel = SideJobRules.FuelLeast - 1;
        Check(SideJobRules.Refusal(SideKind.Cab, car, 5) == SideRefusal.Fuel && SideJobRules.Refusal(SideKind.Parcels, car, 5) == SideRefusal.Fuel, "side jobs: nobody is signed on with an empty tank");
        car = SideGood();
        Check(SideJobRules.Refusal(SideKind.Cab, car, 0) == SideRefusal.DayDone && SideJobRules.Refusal(SideKind.Parcels, car, 0) == SideRefusal.DayDone, "side jobs: the day's amount driven, the desk has no more");
        Check(SideJobRules.Condition(0f) == 100 && SideJobRules.Condition(0.4f) == 60 && SideJobRules.Condition(1f) == 0 && SideJobRules.Condition(2f) == 0 && SideJobRules.Condition(-1f) == 100,
            "side jobs: condition is what the game's damage leaves, in per cent, inside 0 to 100");
        Check(SideJobRules.FuelShare(30f, 60f) == 50 && SideJobRules.FuelShare(5f, 0f) == 0 && SideJobRules.FuelShare(90f, 60f) == 100 && SideJobRules.FuelShare(-1f, 60f) == 0,
            "side jobs: the tank in per cent, nothing for a vehicle without one");
    }

    private static void SideJobMeter()
    {
        bool near = true, rising = true;
        int last = 0;
        for (int metres = 280; metres <= 950; metres += 10)
        {
            float gig = SideUnit + SideMetre * metres;
            int fare = SideJobRules.Fare(SideUnit, SideMetre, metres, SideJobRules.Par(metres), 1f);
            near &= Math.Abs(fare - gig) <= gig * 0.015f + 1f;
            rising &= fare >= last;
            last = fare;
        }
        Check(near, "side jobs: a ride at par on the meter comes to the gig's fare for its distance, within 1.5%");
        Check(rising, "side jobs: a longer ride never pays less");
        bool none = true;
        for (int metres = 0; metres < (int)SideJobRules.TripLeast; metres += 10)
        {
            none &= SideJobRules.Fare(SideUnit, SideMetre, metres, 0f, 1f) == 0 && SideJobRules.Fare(SideUnit, SideMetre, metres, 100000f, 3f) == 0;
        }
        Check(none && SideJobRules.TripLeast <= 280f, "side jobs: no distance is no fare, however long the clock runs: nothing under " + SideJobRules.TripLeast + " m, and every fare the job plans is longer");
        float par = SideJobRules.Par(600f);
        int atPar = SideJobRules.Fare(SideUnit, SideMetre, 600f, par, 1f), capped = SideJobRules.Fare(SideUnit, SideMetre, 600f, par * SideJobRules.TimeMost, 1f);
        Check(SideJobRules.Fare(SideUnit, SideMetre, 600f, par * 50f, 1f) == capped && SideJobRules.Fare(SideUnit, SideMetre, 600f, 1e9f, 1f) == capped,
            "side jobs: the meter stops counting time at " + SideJobRules.TimeMost + " times the ride's par: standing still earns nothing more");
        Check(capped > atPar && capped <= atPar * 1.09f, "side jobs: the slowest ride pays at most 9% over one at par (" + atPar + " to " + capped + ")");
        Check(SideJobRules.Fare(SideUnit, SideMetre, 600f, 0f, 1f) < atPar && SideJobRules.Fare(SideUnit, SideMetre, 600f, -5f, 1f) == SideJobRules.Fare(SideUnit, SideMetre, 600f, 0f, 1f),
            "side jobs: time is part of the fare, and never counts below nothing");
        Check(Math.Abs(SideJobRules.Fare(SideUnit, SideMetre, 600f, par, 2f) - 2 * atPar) <= 1 && SideJobRules.Fare(SideUnit, SideMetre, 600f, par, 0f) == 0
              && SideJobRules.Fare(SideUnit, SideMetre, 600f, par, -1f) == 0, "side jobs: the meter moves with the driver's rate (level, pay option, the city's price level)");
        bool share = true;
        for (int fare = 0; fare <= 600; fare += 7)
        {
            int kept = SideJobRules.Kept(fare);
            share &= kept >= 0 && kept <= fare && Math.Abs(kept - fare * SideJobRules.CommissionMille / 1000f) <= 0.5f;
        }
        Check(share && SideJobRules.Kept(-10) == 0 && SideJobRules.Kept(100) == 20, "side jobs: the cab office keeps a fifth of the meter, never more than the fare, nothing of nothing");
        Check(SideJobRules.FuelCost(10f, 130) == 13 && SideJobRules.FuelCost(0f, 130) == 0 && SideJobRules.FuelCost(-3f, 130) == 0 && SideJobRules.FuelCost(10f, 0) == 0,
            "side jobs: fuel burned is priced at the pump, a litre in cents");
    }

    private static void SideJobMark()
    {
        float par = SideJobRules.Par(600f);
        Check(SideJobRules.Quality(100, par, par, 0.2f, 0, false) == 100 && SideJobRules.Quality(100, par * 0.8f, par, 0f, 0, false) == 100, "side jobs: a smooth ride on time keeps its mark");
        int slow = SideJobRules.Quality(100, par * 1.2f, par, 0f, 0, false), crawl = SideJobRules.Quality(100, par * 9f, par, 0f, 0, false);
        Check(slow < 100 && crawl == 100 - SideJobRules.LateMost && slow > crawl, "side jobs: a slow ride loses marks by how late, at most " + SideJobRules.LateMost);
        Check(SideJobRules.Quality(100, par, par, 0.9f, 0, false) == 100 - SideJobRules.PickupLate && SideJobRules.Quality(100, par, par, SideJobRules.PickupLateFrom, 0, false) == 100,
            "side jobs: reaching the passenger in the last quarter of their patience costs a little");
        Check(SideJobRules.Quality(100, par, par, 0f, 2, false) == 100 - 2 * SideJobRules.OffenceCost && SideJobRules.Quality(100, par, par, 0f, 1, true) == 100 - SideJobRules.OffenceCost - SideJobRules.FledCost
              && SideJobRules.Quality(100, par, par, 0f, -3, false) == 100, "side jobs: each offence the police book costs marks, driving off from them a good deal more");
        Check(SideJobRules.Quality(10, par * 9f, par, 1f, 9, true) == 0 && SideJobRules.Quality(140, par, par, 0f, 0, false) == 100, "side jobs: the mark stays inside 0 to 100");
        bool falling = true;
        float before = 1f;
        for (int quality = 100; quality >= 0; quality--)
        {
            float tip = SideJobRules.TipShare(quality);
            falling &= tip <= before && tip >= 0f;
            before = tip;
        }
        Check(falling && SideJobRules.TipShare(100) == 0.26f && SideJobRules.TipShare(89) == 0.15f && SideJobRules.TipShare(74) == 0.06f && SideJobRules.TipShare(54) == 0f
              && SideJobRules.TipShare(100) <= 0.30f, "side jobs: the tip follows the mark, the middle of what the gig's passengers give and never above their best");
    }

    private static void SideJobRuns()
    {
        Check(SideJobRules.Drops(0, false) == 0 && SideJobRules.Drops(1, false) == 0 && SideJobRules.Drops(2, false) == 2 && SideJobRules.Drops(4, false) == 3 && SideJobRules.Drops(14, false) == 5
              && SideJobRules.Drops(500, false) == SideJobRules.DropsMost, "side jobs: a vehicle's room sets the run: none without a boot, two for the smallest, eight at most");
        bool grows = true;
        for (int cargo = 0; cargo < 80; cargo++)
        {
            grows &= SideJobRules.Drops(cargo + 1, false) >= SideJobRules.Drops(cargo, false) && SideJobRules.Drops(cargo, true) <= SideJobRules.FoodMost
                     && SideJobRules.Drops(cargo, true) <= SideJobRules.Drops(cargo, false);
        }
        Check(grows && SideJobRules.DropsMost <= 10, "side jobs: more room never means fewer drops, meals are three a run at most, and a run fits the courier's ten places");
        Check(SideJobRules.IsFood("ba:businesstype_fastfood") && SideJobRules.IsFood("ba:businesstype_coffeeshop") && !SideJobRules.IsFood("ba:businesstype_supermarket")
              && !SideJobRules.IsFood("ba:businesstype_giftshop") && !SideJobRules.IsFood(null) && !SideJobRules.IsFood(string.Empty),
            "side jobs: a place that cooks sends meals, a shop parcels");
        Check(SideJobRules.DueIn(300f, 1, false) > SideJobRules.DueIn(300f, 0, false) && SideJobRules.DueIn(600f, 0, false) > SideJobRules.DueIn(300f, 0, false)
              && SideJobRules.DueIn(300f, 0, true) < SideJobRules.DueIn(300f, 0, false) && SideJobRules.DueIn(-50f, 0, false) == SideJobRules.DueIn(0f, 0, false),
            "side jobs: a drop further along the run has longer, a meal less than a parcel");
        // The last drop of a round of five over 1,500 m has about the round's own window (70 + route / 4.5 + 38 a door).
        float whole = 70f + 1500f / 4.5f + 38f * 5, lastDrop = SideJobRules.DueIn(1500f, 4, false);
        Check(Math.Abs(lastDrop - whole) <= 25f, "side jobs: a run's last drop is due about when the gig's whole round is (" + lastDrop.ToString("F0") + " s against " + whole.ToString("F0") + ")");
        float window = 200f;
        Check(SideJobRules.Punctual(-window * SideJobRules.EarlyShare, window) == SideJobRules.EarlyMille && SideJobRules.Punctual(-1f, window) == 1000 && SideJobRules.Punctual(0f, window) == 1000
              && SideJobRules.Punctual(60f, window) == 750 && SideJobRules.Punctual(SideJobRules.LateFull, window) == SideJobRules.LateLeastMille
              && SideJobRules.Punctual(99999f, window) == SideJobRules.LateLeastMille, "side jobs: early pays a tenth more, on time the pay, late less by the second down to half");
        bool never = true;
        int was = SideJobRules.EarlyMille;
        for (float late = -window; late <= 400f; late += 2.5f)
        {
            int mille = SideJobRules.Punctual(late, window);
            never &= mille <= was && mille >= SideJobRules.LateLeastMille && mille <= SideJobRules.EarlyMille;
            was = mille;
        }
        Check(never, "side jobs: a later drop never pays more, and punctuality stays between half and eleven tenths");
        float[] legs = { 240f, 180f, 310f, 95f, 420f };
        int sum = 0;
        float route = 0f;
        foreach (float leg in legs)
        {
            sum += SideJobRules.DropPay(SideDoor, SideLeg, leg, 1f, 1000, false);
            route += leg;
        }
        float gig = SideDoor * legs.Length + SideLeg * route;
        Check(Math.Abs(sum - gig) <= legs.Length, "side jobs: a run of parcels all on time pays what the gig's round does for its doors and its distance (" + sum + " against " + gig.ToString("F0") + ")");
        int onTime = SideJobRules.DropPay(SideDoor, SideLeg, 300f, 1f, 1000, false);
        Check(SideJobRules.DropPay(SideDoor, SideLeg, 300f, 1f, SideJobRules.EarlyMille, false) > onTime && SideJobRules.DropPay(SideDoor, SideLeg, 300f, 1f, 750, false) < onTime
              && SideJobRules.DropPay(SideDoor, SideLeg, 300f, 1f, 1000, true) > onTime && SideJobRules.DropPay(SideDoor, SideLeg, 600f, 1f, 1000, false) > onTime
              && SideJobRules.DropPay(SideDoor, SideLeg, -40f, 1f, 1000, false) == SideJobRules.DropPay(SideDoor, SideLeg, 0f, 1f, 1000, false)
              && SideJobRules.DropPay(SideDoor, SideLeg, 300f, 0f, 1000, false) == 0, "side jobs: a drop pays by its leg and its punctuality, a meal more, and nothing at no rate");
        bool margin = true;
        for (int pay = 0; pay <= 900; pay += 13)
        {
            int paid = SideJobRules.ClientPaid(pay);
            margin &= paid >= pay && Math.Abs((paid - pay) - paid * SideJobRules.MarginMille / 1000f) <= 1f;
        }
        Check(margin && SideJobRules.ClientPaid(0) == 0 && SideJobRules.ClientPaid(-5) == 0, "side jobs: the depot's client pays the driver's pay and the depot's quarter on top");
        Check(SideJobRules.RunOvertime > 1f && SideJobRules.SignOnMinutes > 0 && SideJobRules.CarWithin >= 60f, "side jobs: a run is called back only after its own time, signing on takes time, and the car may stand a little way off");
    }

    private static void SideJobDay()
    {
        var s = new SideJobState();
        Check(s.NewDay(10) && !s.NewDay(10) && s.Left(SideKind.Cab) == SideJobRules.CabDay && s.Left(SideKind.Parcels) == SideJobRules.ParcelDay, "side jobs: a day starts with the desk's whole amount");
        for (int fare = 0; fare < SideJobRules.CabDay + 5; fare++)
        {
            s.Book(SideKind.Cab, 1, 140, 28, 20, 132);
        }
        Check(s.Left(SideKind.Cab) == 0 && s.Left(SideKind.Parcels) == SideJobRules.ParcelDay, "side jobs: the day's fares driven, none are left, and the depot's day is its own");
        Check(SideJobRules.Refusal(SideKind.Cab, SideGood(), s.Left(SideKind.Cab)) == SideRefusal.DayDone, "side jobs: no endless fares: the desk signs nobody on once the day's amount is driven");
        SideTally cab = s.At(SideKind.Cab);
        int units = cab.UnitsToday, net = cab.NetToday;
        Check(cab.GrossToday == units * 140 && cab.KeptToday == units * 28 && cab.TipsToday == units * 20 && cab.Units == units && cab.Net == net, "side jobs: every fare is on the day and on the save");
        s.Book(SideKind.Parcels, -3, -50, 900, -1, -20);
        SideTally depot = s.At(SideKind.Parcels);
        Check(depot.UnitsToday == 0 && depot.GrossToday == 0 && depot.KeptToday == 0 && depot.NetToday == 0, "side jobs: figures below nothing count as nothing, and a firm never keeps more than was paid");
        s.Fuel(SideKind.Cab, 31);
        s.Fuel(SideKind.Cab, -8);
        Check(cab.FuelToday == 31, "side jobs: the fuel's cost is kept with the day");
        Check(s.NewDay(11) && cab.UnitsToday == 0 && cab.NetToday == 0 && cab.FuelToday == 0 && cab.UnitsBefore == units && cab.NetBefore == net && cab.Units == units
              && s.Left(SideKind.Cab) == SideJobRules.CabDay, "side jobs: at the day's turn today becomes the day before and the desk has its whole amount again");
        s.Book(SideKind.Cab, 2, 280, 56, 0, 224);
        Check(s.NewDay(15) && cab.UnitsBefore == 0 && cab.NetBefore == 0 && cab.Units == units + 2, "side jobs: days missed leave no day before, and the save's totals stay");
    }

    private static void SideJobSave()
    {
        var s = new SideJobState { Seed = 778899, Day = 42 };
        s.Book(SideKind.Cab, 7, 990, 198, 120, 912);
        s.Book(SideKind.Parcels, 11, 1040, 260, 30, 810);
        s.Fuel(SideKind.Cab, 44);
        s.At(SideKind.Cab).Shifts = 3;
        s.At(SideKind.Cab).UnitsBefore = 9;
        s.At(SideKind.Parcels).NetBefore = 512;
        string line = SideJobStore.Write(s);
        SideJobState r = SideJobStore.Read(line, out int version);
        Check(version == SideJobStore.Version && r != null && r.Seed == 778899 && r.Day == 42, "side jobs: the save's line is read back with its version, seed and day");
        bool same = r != null;
        for (int k = 0; same && k < 2; k++)
        {
            SideTally a = s.Of[k], b = r.Of[k];
            same = a.UnitsToday == b.UnitsToday && a.GrossToday == b.GrossToday && a.KeptToday == b.KeptToday && a.TipsToday == b.TipsToday && a.NetToday == b.NetToday && a.FuelToday == b.FuelToday
                   && a.UnitsBefore == b.UnitsBefore && a.NetBefore == b.NetBefore && a.Shifts == b.Shifts && a.Units == b.Units && a.Net == b.Net && a.Kept == b.Kept;
        }
        Check(same && SideJobStore.Write(r) == line, "side jobs: every figure of both desks comes back as it was written");
        Check(SideJobStore.Key == "reallife:side_jobs" && line.StartsWith("v=1^h:", StringComparison.Ordinal), "side jobs: the line is under the mod's own key and begins with its version");
        Check(SideJobStore.Read("nonsense", out _) == null && SideJobStore.Read(string.Empty, out _) == null && SideJobStore.Read("v=x^h:1|2", out _) == null, "side jobs: a line that is not the feature's is not read");
        SideJobState old = SideJobStore.Read("v=1^h:5|3^c:2|300", out _);
        Check(old != null && old.Seed == 5 && old.Day == 3 && old.At(SideKind.Cab).UnitsToday == 2 && old.At(SideKind.Cab).GrossToday == 300 && old.At(SideKind.Cab).Net == 0
              && old.At(SideKind.Parcels).UnitsToday == 0, "side jobs: a shorter line still loads, with nothing for what it lacks");
        SideJobState later = SideJobStore.Read(line + "|77|88^z:1|2|3", out _);
        Check(later != null && later.At(SideKind.Parcels).Kept == s.At(SideKind.Parcels).Kept, "side jobs: fields and parts a later version adds are passed over");
        SideJobState bad = SideJobStore.Read("v=1^h:5|-9^c:-4|100|900|-1|-1|-1|-1|-1|-1|-1|-1|-1", out _);
        Check(bad != null && bad.Day == -1 && bad.At(SideKind.Cab).UnitsToday == 0 && bad.At(SideKind.Cab).KeptToday == 100 && bad.At(SideKind.Cab).Net == 0, "side jobs: every figure read is put inside its bounds");
        var data = new Dictionary<string, string>();
        Check(SideJobStore.Load(data, out _) == null && SideJobStore.Load(null, out _) == null, "side jobs: a save from before the feature has no line and starts afresh");
        data[SideJobStore.Key] = line;
        SideJobState loaded = SideJobStore.Load(data, out int v);
        Check(loaded != null && v == 1 && loaded.At(SideKind.Cab).Shifts == 3, "side jobs: the save's line loads through the store");
    }

    private static void SideJobBooks()
    {
        SideBooks a = SideJobRules.Books(4242, 17, SideKind.Cab, 1000, SideUnit, SideMetre, JobCatalogue.LevelBonus);
        SideBooks b = SideJobRules.Books(4242, 17, SideKind.Cab, 1000, SideUnit, SideMetre, JobCatalogue.LevelBonus);
        Check(a.Drivers == b.Drivers && a.Units == b.Units && a.Gross == b.Gross && a.Kept == b.Kept && a.Best == b.Best, "side jobs: a firm's books for a day are the same whenever and wherever they are asked");
        bool cabs = true, depots = true, differ = false;
        for (int day = 1; day <= 60; day++)
        {
            SideBooks c = SideJobRules.Books(99 + day * 7, day, SideKind.Cab, 1000, SideUnit, SideMetre, JobCatalogue.LevelBonus);
            cabs &= c.Drivers >= 6 && c.Drivers <= 12 && c.Units >= c.Drivers * 8 && c.Units <= c.Drivers * SideJobRules.CabDay && c.Gross > 0 && c.Best > 0 && c.Best < c.Gross
                    && Math.Abs(c.Kept - c.Gross * SideJobRules.CommissionMille / 1000f) <= c.Units;
            // No NPC fare is outside what the meter can give a player for the job's own distances at the best level.
            cabs &= c.Gross <= c.Units * SideJobRules.Fare(SideUnit, SideMetre, 950f, 1e9f, 1f + JobCatalogue.LevelBonus * 3) && c.Gross >= c.Units * SideJobRules.Fare(SideUnit, SideMetre, 280f, 0f, 1f);
            SideBooks p = SideJobRules.Books(99 + day * 7, day, SideKind.Parcels, 1000, SideDoor, SideLeg, JobCatalogue.LevelBonus);
            depots &= p.Drivers >= 5 && p.Drivers <= 10 && p.Units >= p.Drivers * 10 && p.Units <= p.Drivers * SideJobRules.ParcelDay && p.Gross > 0 && p.Best > 0
                      && Math.Abs(p.Kept - p.Gross * SideJobRules.MarginMille / 1000f) <= p.Units;
            depots &= p.Gross - p.Kept <= p.Units * SideJobRules.DropPay(SideDoor, SideLeg, 520f, 1f + JobCatalogue.LevelBonus * 3, SideJobRules.EarlyMille, false)
                      && p.Gross - p.Kept >= p.Units * SideJobRules.DropPay(SideDoor, SideLeg, 110f, 1f, SideJobRules.LateLeastMille, false);
            differ |= c.Gross != a.Gross;
        }
        Check(cabs, "side jobs: the cab office's other drivers work by the player's meter, share and day: six to twelve of them, eight fares or more each, never past the day's amount, a fifth kept");
        Check(depots, "side jobs: the depot's other drivers work by the player's drop pay, punctuality and day: five to ten of them, a quarter kept");
        Check(differ, "side jobs: another day is another day's books");
        SideBooks dear = SideJobRules.Books(4242, 17, SideKind.Cab, 2000, SideUnit, SideMetre, JobCatalogue.LevelBonus);
        Check(dear.Drivers == a.Drivers && dear.Units == a.Units && Math.Abs(dear.Gross - 2 * a.Gross) <= a.Units, "side jobs: at twice the city's prices the same drivers take twice as much");
        Check(JobCatalogue.LevelBonus == 0.08f, "side jobs: the other drivers' levels pay as the player's do (8% a level)");
    }

    private static void SideJobSources()
    {
        string director = ModSource(@"Jobs\JobDirector.cs"), street = ModSource(@"Jobs\JobDirector.Street.cs"), pay = ModSource(@"Jobs\JobDirector.Pay.cs"), job = ModSource(@"Jobs\Job.cs");
        string take = MemberText(director, "public bool TakeDesk(JobDef def, DeskShift desk, out string reasonKey)");
        Check(take.Contains("def.Id == JobCatalogue.Taxi && !Licences.Driving.Has(LicenceClass.Taxi)") && take.Contains("reallife_job_no_taxi_licence") && take.Contains("desk.Left() <= 0")
              && take.Contains("JobState.Sacked(def, TimeHelper.CurrentDay)") && take.Contains("JobCatalogue.MinEnergy") && take.Contains("!Free(out reasonKey, def)")
              && take.Contains("StartJob(def, desk.Test, out reasonKey, desk: desk)") && !take.Contains("_rental") && !take.Contains("CarFor("),
            "side jobs: the jobs' director asks a desk shift the taxi endorsement, the desk's own day, the energy and a free player, and never hires a van for it");
        Check(director.Contains("desk == null && !test && !JobCatalogue.IsTrade(def)") && job.Contains("public bool FullShift => Test || Shift != null || Gig != null || Desk != null;")
              && ModSource(@"Jobs\JobDirector.Ending.cs").Contains("_context.Desk = null;"),
            "side jobs: a desk shift is worked at the table's figures, keeps its own day, and is let go of however the shift ends");
        Check(MemberText(director, "private void OnStopped(int booked, bool fled)").Contains("_job.OnOffences(offences, fled)") && director.Contains("PoliceAttention.Stopped += OnStopped;")
              && director.Contains("PoliceAttention.Stopped -= OnStopped;"), "side jobs: the job on hears what the police booked at a stop, and the listener is taken off with the director");
        Check(pay.Contains("new TransactionInfo(def.TransactionKey, SalaryCategory)") && pay.Contains("SalaryCategory = \"ba:transactioncategory_salaryincome\""),
            "side jobs: every fare and run is booked as salary income under the gig's own name, so the game's statements and tax count it");
        Check(MemberText(street, "public static CarController NearestCar(float within)").Contains("GoingPublic.MineVehicle(car)"),
            "side jobs: the vehicle a desk reads is the player's own, never another Going Public player's");

        string taxi = ModSource(@"Jobs\Kinds\TaxiJob.cs"), taxiDesk = ModSource(@"Jobs\Kinds\TaxiJob.Desk.cs");
        string settle = MemberText(taxi, "private void Settle(float now)");
        Check(settle.Contains("int fare = Ctx.Desk != null ? DeskMeter(seconds, par, ref comfort, ref slip, out kept) : slip.Base + slip.Distance;") && settle.Contains("Ctx.Desk != null ? DeskTip(comfort)")
              && settle.Contains("DeskBooked(slip, fare, kept);"), "side jobs: a desk shift's fare is the meter's, its tip the mark's, and the office's books hear of it as it is paid");
        Check(taxiDesk.Contains("SideJobRules.Fare(Def.UnitPay, Def.PayPerMetre, _metres, seconds, Ctx.Rate)") && taxiDesk.Contains("SideJobRules.Kept(fare)")
              && taxiDesk.Contains("SideJobRules.Quality(comfort, seconds, par, _pickupUsed, _offences, _fled)") && taxiDesk.Contains("SideJobRules.TipShare(quality)")
              && !taxiDesk.Contains("Random") && !taxiDesk.Contains("Rental") && !taxiDesk.Contains("CarFor("),
            "side jobs: the cab job reads the meter, the share, the mark and the tip from the rules alone, draws nothing and hires nothing");
        Check(taxiDesk.Contains("JobDirector.NearestCar(SideJobRules.CarWithin)") && taxiDesk.Contains("!Ctx.Desk.Fits(car)") && MemberText(taxi, "private void ToPickup(float now)").Contains("if (DeskOtherCar())")
              && MemberText(taxi, "private bool NextFare(float now)").Contains("TripMin") && taxi.Contains("private const float TripMin = 280f;"),
            "side jobs: a cab shift is driven in the car signed on with, and every fare it plans is longer than the meter's least");
        Check(MemberText(taxi, "private void Between(float now)").Contains("if (DeskDayDone())") && taxiDesk.Contains("Ctx.Desk.Left() > 0"), "side jobs: the cab shift ends when the desk's day is driven");

        string courier = ModSource(@"Jobs\Kinds\CourierJob.cs"), courierDesk = ModSource(@"Jobs\Kinds\CourierJob.Desk.cs");
        Check(courierDesk.Contains("SideJobRules.Punctual(late, _drops[drop].DueIn)") && courierDesk.Contains("SideJobRules.DropPay(Def.UnitPay, Def.PayPerMetre, _drops[drop].Leg, Ctx.Rate, mille, _food)")
              && courierDesk.Contains("SideJobRules.DueIn(_route, place, _food)") && courierDesk.Contains("SideJobRules.IsFood(business.businessTypeName)")
              && courierDesk.Contains("JobSites.AnyClient") && !courierDesk.Contains("Random.") && !courierDesk.Contains("Rental") && !courierDesk.Contains("CarFor("),
            "side jobs: a delivery run is collected at an open business of the city's, each drop timed and paid by the rules alone, and nothing is hired");
        Check(courier.Contains("CarController wheels = Ctx.Desk != null ? DeskCar(out noCar)") && courierDesk.Contains("!Ctx.Desk.Fits(car)") && courierDesk.Contains("Mathf.Min(Ctx.Desk.Drops, Ctx.Desk.Left())")
              && MemberText(courier, "private void Conclude(float now, bool late)").Contains("if (all && DeskNextRun(now))")
              && MemberText(courier, "private bool Owed(bool beatTheClock, out PaySlip slip)").Contains("DeskSlip(ref slip);"),
            "side jobs: a run is as big as the vehicle signed on with has room for and the day has left, is paid by its drops, and the next follows a whole one");
        Check(courier.Contains("public override bool QuitIsFailure => !DeskIdle && _delivered * 2 < Mathf.Max(1, _count);") && courierDesk.Contains("_step == Step.ToDepot"),
            "side jobs: signing off between runs leaves nothing undone; in the middle of one it counts as giving up does");

        string side = ModSource(@"SideJobs\SideJobDirector.cs"), tests = ModSource(@"SideJobs\SideJobDirector.Tests.cs"), desks = ModSource(@"SideJobs\SideJobDesks.cs");
        string rules = ModSource(@"SideJobs\SideJobRules.cs"), game = ModSource(@"SideJobs\SideJobGame.cs");
        Check(side.Length > 0 && !Regex.IsMatch(side + tests + desks + game, @"void (Update|LateUpdate|FixedUpdate|OnGUI)\(") && side.Contains("new WaitForSecondsRealtime(1f)")
              && side.Contains("StartCoroutine(Guard.Routine(Tick()))"), "side jobs: nothing of it runs a frame: one guarded look a second");
        Check(side.Contains("_skip = ShiftSkip.Offer(this, sign);") && side.Contains("_panel.Buttons(on ? \"reallife_job_desk_button_off\" : \"reallife_job_desk_button_on\", _onPress, true, null, null, false);")
              && !(side + tests + desks + game).Contains("PhoneApps") && !(side + tests + desks + game).Contains("AppShell"),
            "side jobs: a shift is signed on for at the desk, on the game's own work panel, never from an app");
        Check(side.Contains("SideJobRules.Refusal(kind, facts, _state.Left(kind))") && game.Contains("Licences.Driving.ClassOf(car, out LicenceClass needs) || Licences.Driving.Has(needs)")
              && game.Contains("Licences.Driving.Has(LicenceClass.Taxi)") && game.Contains("CarCatalogue.Seats(") && game.Contains("type.maxCargoCapacity")
              && game.Contains("SideJobRules.Condition(car.vehicleInstance.damage)"), "side jobs: the desk reads the vehicle's licence class, seats, room, condition and tank and asks the rules");
        Check(desks.Contains("ModCulling.Add(CullRules.Kind.JobBoard") && desks.Contains("ModCulling.Remove(") && desks.Contains("): PASS") && desks.Contains(": FAIL")
              && ModSource(@"Performance\CullRules.cs").Contains("SideJobs/SideJobDesks.cs"), "side jobs: each desk is checked as it is stood, drawn no further than the far band, and taken in again");
        Check(!rules.Contains("Random") && !rules.Contains("DateTime") && !rules.Contains("UnityEngine") && rules.Contains("private static uint Next(ref uint state)"),
            "side jobs: the rules draw nothing but the firms' books, from the save's seed and the day: the same on every machine");
        Check(side.Contains("SideJobRules.Books(_state.Seed, day, kind, Prices.Level, def.UnitPay, def.PayPerMetre, JobCatalogue.LevelBonus)"),
            "side jobs: the firms' books are worked at the gigs' own figures and the city's price level");
        Check(side.Contains("SideJobRules.FuelCost(_burned, SideJobBridges.PumpCents())") && game.Contains("return car.GetCurrentFuel();"), "side jobs: the fuel a shift burned is read from the tank and priced at the game's pump");

        string settings = ModSource(@"Core\Settings.cs"), mod = ModSource(@"Core\RealLifeOverhaulMod.cs"), tree = ModSource(@"Meta\OptionsTree.cs"), cities = ModSource(@"Meta\FeatureCities.cs");
        string list = ModSource(@"Meta\FeatureList.cs"), text = ModSource(@"SideJobs\SideJobText.cs"), wiring = ModSource(@"Wiring\Wiring.cs"), links = ModSource(@"Wiring\Wiring.SideJobLinks.cs");
        Check(settings.Contains("SideJobsId = \"side_jobs\"") && settings.Contains("public static bool SideJobs = true;") && settings.Contains("SideJobs = Bool(modId, SideJobsId, SideJobs);")
              && mod.Contains("AddToggle(Settings.SideJobsId, \"reallife_job_desk_enabled\", Settings.SideJobs,") && tree.Contains("S(\"sidejobs\").For(\"side_jobs\").Opt(\"side_jobs\", \"jobs\")")
              && cities.Contains("new Line(\"side_jobs\", CityScope.AnyCity,") && list.Contains("SideJobs.SideJobGuide.Register();") && text.Contains(".Owns(Settings.SideJobsId)")
              && text.Contains(".Data(SideJobStore.Key)") && mod.Contains("SideJobs.SideJobDirector.Attach(_root, context.Logger)")
              && ModSource(@"Core\FeatureOwners.cs").Contains("\"RealLifeOverhaul.SideJobs\", \"side_jobs\""),
            "side jobs: the option is saved, on by default, under the jobs on the options page, with its feature, its city line and its save key");
        Check(side.Contains("if (!Settings.SideJobs || !Settings.Jobs)") && MemberText(side, "public void PutAside()").Contains("SideJobBridges.CallOff();")
              && MemberText(side, "public void PutAside()").Contains("_desks.Dispose();") && MemberText(director, "public void QuitDesk()").Contains("EndJob(JobEnd.Interrupted, \"reallife_job_end_off\")"),
            "side jobs: switched off, a shift that is on is called off and paid, and the desks are taken in");
        Check(mod.Contains("AddButton(\"reallife_job_desk_test_cab\"") && mod.Contains("AddButton(\"reallife_job_desk_test_parcels\"") && mod.Contains("AddButton(\"reallife_job_desk_test_books\"")
              && tests.Contains("public void TestSignOn(bool cab)") && tests.Contains("public void TestBooks()") && text.Contains(".Test(\"reallife_job_desk_test_cab\", \"reallife_job_desk_test_parcels\", \"reallife_job_desk_test_books\")"),
            "side jobs: its three tests are on the options page");
        Check(wiring.Contains("SideJobLinks.Connect") && wiring.Contains("SideJobLinks.Disconnect") && links.Contains("SideJobBridges.Reset();") && links.Contains("CareerCatalogue.Cab : CareerCatalogue.Courier")
              && links.Contains("SideJobBridges.PumpCents = LogisticsGame.PumpCents;"), "side jobs: the wiring gives it the firms' boards, the jobs' director and the pump's price, and takes them back");
        Check(ModSource(@"World\NearbyInteraction.cs").Contains("SideJobs.SideJobDirector.TakesInteractKey"), "side jobs: the mod's own door and car key stands aside for the desk's button");
    }

    private static readonly string[] SideJobKeys =
    {
        "reallife_meta_title_side_jobs", "reallife_opt_sec_sidejobs", "reallife_job_desk_enabled", "reallife_opt_tip_side_jobs", "reallife_guide_side_jobs_about", "reallife_guide_side_jobs",
        "reallife_job_desk_test_cab", "reallife_job_desk_test_parcels", "reallife_job_desk_test_books", "reallife_job_desk_test_off", "reallife_job_desk_test_books_note",
        "reallife_job_desk_button_on", "reallife_job_desk_button_off", "reallife_job_desk_sign_title", "reallife_job_desk_sign_cab_car", "reallife_job_desk_sign_cab_terms",
        "reallife_job_desk_sign_parcel_car", "reallife_job_desk_sign_parcel_terms", "reallife_job_desk_sign_cab_books", "reallife_job_desk_sign_parcel_books", "reallife_job_desk_sign_costs",
        "reallife_job_desk_no_car", "reallife_job_desk_no_motor", "reallife_job_desk_no_licence", "reallife_job_desk_no_seats", "reallife_job_desk_no_room", "reallife_job_desk_no_condition",
        "reallife_job_desk_no_fuel", "reallife_job_desk_no_day", "reallife_job_desk_no_car_near", "reallife_job_desk_obj_other_car", "reallife_job_desk_day_done", "reallife_job_desk_run_food",
        "reallife_job_desk_run_parcels", "reallife_job_desk_drop_late", "reallife_job_desk_no_run", "reallife_job_desk_started_cab", "reallife_job_desk_started_parcels",
        "reallife_job_desk_shift_cab", "reallife_job_desk_shift_parcels", "reallife_job_desk_shift_none"
    };

    private static void SideJobWords()
    {
        Dictionary<string, string>[] langs = LanguageWords(out string[] codes);
        if (!LanguagesRead("side jobs", langs, codes))
        {
            return;
        }
        Dictionary<string, string> en = langs[0];
        string lacking = string.Empty, other = string.Empty, marks = string.Empty, longNotes = string.Empty;
        foreach (string key in SideJobKeys)
        {
            if (!en.TryGetValue(key, out string words) || string.IsNullOrWhiteSpace(words))
            {
                lacking += " " + key;
                continue;
            }
            if (words.Contains("!") || words.Contains((char)0x2014) || words.Contains((char)0x2013) || words.Contains(" - "))
            {
                marks += " " + key;
            }
            bool notice = key.StartsWith("reallife_job_desk_", StringComparison.Ordinal) && !key.Contains("_sign_") && !key.Contains("_button_") && !key.Contains("_test_cab")
                          && !key.Contains("_test_parcels") && !key.EndsWith("_test_books", StringComparison.Ordinal) && key != "reallife_job_desk_enabled";
            if (notice && words.Length > 240)
            {
                longNotes += " " + key;
            }
            // A language that has the text names the same things in curly brackets as the English; one that lacks it reads in English until it is translated.
            for (int l = 1; l < langs.Length; l++)
            {
                if (langs[l].TryGetValue(key, out string theirs) && !string.IsNullOrWhiteSpace(theirs) && NamesOf(theirs) != NamesOf(words))
                {
                    other += " " + key + " (" + codes[l] + ")";
                }
            }
        }
        Check(lacking.Length == 0, "side jobs: every text is in en.json:" + lacking);
        Check(marks.Length == 0, "side jobs: no dash and no exclamation mark in a text:" + marks);
        Check(longNotes.Length == 0, "side jobs: every notice is 240 characters or under:" + longNotes);
        Check(other.Length == 0, "side jobs: a translated text names the same things in curly brackets as the English:" + other);
        Check(en.TryGetValue("reallife_job_desk_enabled", out string label) && label == "Taxi and delivery shifts in your own car"
              && en.TryGetValue("reallife_opt_tip_side_jobs", out string tip) && tip.EndsWith(".", StringComparison.Ordinal) && tip.Contains("Off, "),
            "side jobs: the option is named and its sentence says what off does");
        foreach (string test in new[] { "reallife_job_desk_test_cab", "reallife_job_desk_test_parcels", "reallife_job_desk_test_books" })
        {
            Check(en.TryGetValue(test, out string button) && !button.StartsWith("Test", StringComparison.OrdinalIgnoreCase), "side jobs: the test button " + test + " does not begin with 'Test'");
        }
        Check(en.TryGetValue("reallife_job_desk_shift_cab", out string cab) && NamesOf(cab) == "{firm},{fuel},{gross},{kept},{net},{tips},{units}"
              && en.TryGetValue("reallife_job_desk_shift_parcels", out string depot) && NamesOf(depot) == NamesOf(cab),
            "side jobs: the notice at signing off names the firm, the work, what was paid, kept, tipped and cleared, and the fuel");
        // Every fact the Guide page names is one the feature's entry fills in, and every one it fills in is named.
        string entry = ModSource(@"SideJobs\SideJobText.cs");
        string page = en.TryGetValue("reallife_guide_side_jobs", out string guide) ? guide : string.Empty;
        string unfilled = string.Empty, unnamed = string.Empty;
        foreach (Match m in Regex.Matches(page, @"\{([a-z0-9_]+)\}"))
        {
            if (!entry.Contains(".Fact(\"" + m.Groups[1].Value + "\","))
            {
                unfilled += " " + m.Groups[1].Value;
            }
        }
        foreach (Match m in Regex.Matches(entry, "\\.Fact\\(\"([a-z0-9_]+)\","))
        {
            if (!page.Contains("{" + m.Groups[1].Value + "}"))
            {
                unnamed += " " + m.Groups[1].Value;
            }
        }
        Check(page.Length > 0 && unfilled.Length == 0, "side jobs: every figure of the Guide page is filled from the rules:" + unfilled);
        Check(unnamed.Length == 0, "side jobs: every fact the entry fills is on the Guide page:" + unnamed);
        Check(WhatsNewHas(en, "Taxi and delivery shifts in your own car: sign on at the desk outside Five Borough Cabs or Hudson Parcel Co."), "side jobs: What's new says so");
    }
}
