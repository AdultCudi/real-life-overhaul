using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using FilePath = System.IO.Path;
using RealLifeOverhaul;
using RealLifeOverhaul.Careers;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Leisure;
using RealLifeOverhaul.Ui;

// The fixes of the logic audit of wave 2 (activity, bizops, buy, dialog, hud, world), each through its rule with the state read back:
// the part-day slider of every shift that can stop part way (the units it covers, what it pays, the stress and skill the card
// states, the hour the strip is at), the funeral's backup line, the job offer's interview line, the clothes shop's New filter
// and its saved set, the vehicle's fuel use and seats, the cargo's move to a warehouse, a factory table's day, the nearest
// rival's crew, the manager's double booking, Fill the gaps through RotaLedger, the time skip's pace, the ten unused texts and
// the cost lines. FixW2Run then plays 84 days: the day turn's work for a clothes shop, the Sunday rota check of a manager and a
// shift's stress and pay, as the director's calls would, with a save in the middle.
internal static partial class Program
{
    private static string W2Root => FilePath.GetFullPath(FilePath.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "Assets", "Mods", "RealLifeOverhaul"));

    private static void FixW2Audit()
    {
        int before = _run, failedBefore = _failed;
        var watch = System.Diagnostics.Stopwatch.StartNew();
        W2Shifts();
        W2Funeral();
        W2OfferLine();
        W2Clothes();
        W2Vehicle();
        W2Cargo();
        W2Factory();
        W2Rival();
        W2RotaChecks();
        W2TimeSkip();
        W2Texts();
        W2CostLines();
        W2Run();
        Console.WriteLine("  Wave 2 audit fixes: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed, " + watch.ElapsedMilliseconds + " ms");
    }

    private static string W2Source(string relative) => File.ReadAllText(FilePath.Combine(W2Root, "Scripts", relative.Replace('/', FilePath.DirectorySeparatorChar)));

    // ------------------------------------------------------------------------------------------------- shifts

    private static void W2Shifts()
    {
        // The slider: from one hour to the whole shift, the right end the whole shift, its part hour counted.
        Check(ShiftFactsRules.MostHours(240) == 4 && ShiftFactsRules.MostHours(241) == 5 && ShiftFactsRules.MostHours(1) == 1 && ShiftFactsRules.MostHours(0) == 1,
            "shift slider: the right end is the shift in hours, its part hour counted, never under one");
        Check(ShiftFactsRules.Chosen(0, 4) == 0 && ShiftFactsRules.Chosen(4, 4) == 0 && ShiftFactsRules.Chosen(9, 4) == 0 && ShiftFactsRules.Chosen(2, 4) == 2,
            "shift slider: the right end (or past it) is the whole shift (0), a value inside it is kept");
        // 12 units of 20 minutes (a shift of four hours): two hours cover six units; the whole is twelve; at least one while any are left.
        Check(ShiftFactsRules.Units(2, 12, 20) == 6 && ShiftFactsRules.Units(0, 12, 20) == 12 && ShiftFactsRules.Units(1, 12, 20) == 3 && ShiftFactsRules.Units(3, 5, 90) == 2
              && ShiftFactsRules.Units(1, 5, 90) == 1 && ShiftFactsRules.Units(2, 0, 20) == 0 && ShiftFactsRules.Units(2, 1, 20) == 1,
            "shift slider: the units the hours cover are whole, at least one, never more than are left");
        // A stop part way pays what was done: two hours chosen of four, stopped after 90 minutes, is four units, paid as four.
        int each = 20, left = ShiftFactsRules.Units(2, 12, each), open = int.MaxValue;
        Check(ClockRules.Units(90, false, left, each, open) == 4 && ClockRules.Units(120, true, left, each, open) == 6 && ClockRules.Units(500, false, left, each, open) == 6,
            "part-day shift: stopped at 90 minutes of a 2 hour choice it has done 4 units; to its end 6, never the 12 of the whole shift");
        float unit = 18f;
        Check(ShiftFactsRules.Pay(unit, ClockRules.Units(90, false, left, each, open), 0.85f) == (int)Math.Round(4 * 18f * 0.85f, MidpointRounding.ToEven)
              && ShiftFactsRules.Pay(unit, 0, 0.85f) == 0 && ShiftFactsRules.Pay(unit, 12, 0.85f) == 184,
            "part-day shift: the pay is the pay of the units done (4 units 61, the whole 12 units 184), none for none");
        // The card and the strip say the sums the shift settles with: PayFor(minutes) is Pay of the units of those minutes.
        bool same = true;
        for (int m = 0; m <= 240; m += 7)
        {
            same &= ShiftFactsRules.Pay(unit, ClockRules.Units(m, false, 12, each, open), 0.85f) == ShiftFactsRules.Pay(unit, Math.Min(12, m / each), 0.85f);
        }
        Check(same, "shift strip: what the strip says is earned at any minute is the pay of the units done by then");
        // Stress: the card's figure is the hourly upkeep's, easy needs halving a rise; no rate, no stress.
        Check(Math.Abs(ShiftFactsRules.Stress(HomeBalance.StressJobShift, 240, false) - 12f) < 0.001f && Math.Abs(ShiftFactsRules.Stress(HomeBalance.StressJobShift, 240, true) - 6f) < 0.001f
              && ShiftFactsRules.Stress(0f, 240, true) == 0f && Math.Abs(ShiftFactsRules.Stress(HomeBalance.StressJobShift, 60, true) - HomeBalance.StressJobShift * HomeBalance.Easy) < 0.001f,
            "shift card: four hours add 12 stress, 6 on easy needs; one hour on easy halves the rate; a rate of none adds none");
        Check(Math.Abs(HomeBalance.Added(-5f, 2f, true, false) - -10f) < 0.001f && Math.Abs(HomeBalance.Added(3f, 2f, true, true) - 3f) < 0.001f,
            "home upkeep: what restores a need is not halved on easy, what wears it down is");
        // Skill: the job's own, or the employer's for a lead shift; the points are the unit's times the units.
        JobDef vendor = JobCatalogue.Find(JobCatalogue.Vendor), lead = JobCatalogue.Find(JobCatalogue.Lead);
        Employer led = null;
        foreach (Employer e in CareerCatalogue.All)
        {
            led = led ?? (e.LeadSkill != null ? e : null);
        }
        Check(ShiftFactsRules.SkillKey(vendor, led) == vendor.Skill && led != null && ShiftFactsRules.SkillKey(lead, led) == led.LeadSkill && ShiftFactsRules.SkillKey(lead, null) == lead.Skill
              && ShiftFactsRules.SkillKey(null, led) == null,
            "shift card: a lead shift trains its employer's skill, any other job its own, and no job none");
        Check(Math.Abs(ShiftFactsRules.Skill(vendor.UnitSkill, 12) - 12 * vendor.UnitSkill) < 0.0001f && ShiftFactsRules.Skill(0f, 12) == 0f && ShiftFactsRules.Skill(0.04f, 0) == 0f,
            "shift card: the skill points are the job's points a unit times the units, none for a job that trains none or a run of none");
        // The strip's hour: the first hour is 1, the last never past the hours of the shift.
        Check(ShiftFactsRules.HourOf(0, 240) == 1 && ShiftFactsRules.HourOf(59, 240) == 1 && ShiftFactsRules.HourOf(60, 240) == 2 && ShiftFactsRules.HourOf(240, 240) == 4
              && ShiftFactsRules.HourOf(5000, 240) == 4 && ShiftFactsRules.HoursOf(150) == 3 && ShiftFactsRules.HourOf(10, 0) == 1,
            "shift strip: Hour N of M starts at 1 and ends at the shift's last hour");
        // Which shifts slide and which cannot, by the sources: every ISkippableShift is one or says why not.
        string shiftClock = W2Source("Jobs/ShiftClock.cs"), vendorClock = W2Source("Jobs/Kinds/VendorJob.Clock.cs"), vendorJob = W2Source("Jobs/Kinds/VendorJob.cs");
        string counter = W2Source("Economy/OwnBank/Branch/OwnBankDirector.Branches.cs"), ownDesk = W2Source("Tech/TechDirector.OwnDesk.cs");
        Check(shiftClock.Contains("class ShiftClock : ISlidingShift, IShiftFacts") && shiftClock.Contains("ShiftFactsRules.Units(_hours, _shift.ClockLeft, _each)")
              && shiftClock.Contains("ShiftFactsRules.Pay(_shift.ClockPay, count, ShiftRates.Share)") && shiftClock.Contains("int ISlidingShift.SlideLeast => 1;"),
            "part-day shifts: every job the clock does (flyers, courier, cleaner, stocker, busker, dispatch, lead, the building trades) slides, and settles with the units done");
        Check(vendorJob.Contains("VendorJob : Job, ISlidingShift, IShiftFacts") && vendorClock.Contains("ShiftFactsRules.Units(_skipHours, LeftToServe, _minutesEach)")
              && vendorClock.Contains("ShiftFactsRules.Pay(PayEach, count, ShiftRates.Share)"),
            "part-day shifts: the drinks cart slides, and settles with the customers served");
        Check(counter.Contains("OwnBankDirector : ISlidingShift") && counter.Contains("ShiftFactsRules.Minutes(_counterChosen, CounterMinutes)") && ownDesk.Contains("class OwnStint : ISlidingShift"),
            "part-day shifts: the bank counter and the own desk stint slide");
        var cannot = new Dictionary<string, string>
        {
            { "Sitting", "a class, an interview or a desk day: the desk's own slider (Careers/DeskWork) and a fixed length for the rest" },
            { "HustleWork", "a repair, a clean, a wait or a client's visit is one piece of work that is done or not" },
            { "EstateVisitGame", "a tenant's repair is one piece of work that is done or not; stopped early it waits for another go" },
        };
        foreach (string file in Directory.GetFiles(FilePath.Combine(W2Root, "Scripts"), "*.cs", SearchOption.AllDirectories))
        {
            string text = File.ReadAllText(file);
            foreach (Match m in Regex.Matches(text, @"class\s+(\w+)\s*(?:<[^>]*>)?\s*:[^{]*\bISkippableShift\b"))
            {
                Check(cannot.ContainsKey(m.Groups[1].Value), "part-day shifts: " + m.Groups[1].Value + " implements ISkippableShift only as one that cannot stop part way, with its reason");
            }
        }
        string facts = W2Source("Ui/Activity/ActivityFacts.cs");
        Check(facts.Contains("ShiftFactsRules.Stress(facts.StressRate, minutes, Settings.HomeEasy)") && facts.Contains("reallife_ui_activity_stress") && facts.Contains("reallife_ui_activity_skill_day")
              && facts.Contains("facts.PayFor(done)") && facts.Contains("ShiftFactsRules.HourOf(done, total)") && facts.Contains("reallife_ui_activity_before_tax"),
            "activity card: the start card states stress and skill, the sum before tax and the finish; the strip the hour and what is earned");
        Check(W2Source("Home/HomeHealth.cs").Contains("HomeBalance.Added(stress, hours, Settings.HomeEasy, stress > 0f)"),
            "activity card: the stress the card says is the very sum the hourly upkeep adds");
    }

    // ------------------------------------------------------------------------------------------------- funeral, offer line

    private static void W2Funeral()
    {
        var saved = new DateTime(2026, 11, 3, 6, 10, 0);
        string when = FuneralRules.When(saved, System.Globalization.CultureInfo.InvariantCulture);
        Check(when == "3 Nov, 06:10" && FuneralRules.Dated(211) && !FuneralRules.Dated(0) && !FuneralRules.Dated(-1),
            "funeral: the backup's line names the day it was made on and the time its file was written, none for a day not known");
        string screen = W2Source("Ui/Activity/FuneralScreen.cs");
        Check(!screen.Contains("BackUpSaveAge") && screen.Contains("GameContract.FuneralBackupMeta") && screen.Contains("meta.day") && screen.Contains("meta.lastPlayedDate")
              && screen.Contains("reallife_ui_funeral_backup_when"),
            "funeral: the card no longer prints the constant 70, it reads the backup the game found");
    }

    private static void W2OfferLine()
    {
        Check(OfferLineRules.Band((int)ApplyStep.Offered, ProRules.OfferPass + 15) == OfferLineRules.Liked && OfferLineRules.Band((int)ApplyStep.Accepted, ProRules.OfferPass) == OfferLineRules.Fine
              && OfferLineRules.Band((int)ApplyStep.Offered, ProRules.OfferPass - 1) == OfferLineRules.Narrow && OfferLineRules.Band((int)ApplyStep.Sent, 100) == OfferLineRules.None
              && OfferLineRules.Band((int)ApplyStep.Rejected, 100) == OfferLineRules.None && OfferLineRules.Band((int)ApplyStep.Interview, 100) == OfferLineRules.None,
            "job offer: the interview's line is warm by the score the employer gave, and only for an application that reached an offer");
        var applications = new List<RealLifeOverhaul.Careers.Application>();
        RealLifeOverhaul.Careers.Application Make(string street, int number, ApplyStep stage, int score)
        {
            var a = new RealLifeOverhaul.Careers.Application { Stage = (int)stage, Score = score };
            a.Post.Street = street;
            a.Post.Number = number;
            return a;
        }
        applications.Add(Make("Elm Street", 12, ApplyStep.Offered, 62));
        applications.Add(Make("Elm Street", 12, ApplyStep.Accepted, 90));
        applications.Add(Make("Oak Road", 3, ApplyStep.Offered, 80));
        applications.Add(Make("Elm Street", 12, ApplyStep.Rejected, 20));
        RealLifeOverhaul.Careers.Application found = OfferLineRules.Find(applications, "Elm Street", 12);
        Check(found != null && found.Score == 90 && OfferLineRules.Find(applications, "Oak Road", 3).Score == 80 && OfferLineRules.Find(applications, "Elm Street", 13) == null
              && OfferLineRules.Find(null, "Elm Street", 12) == null && OfferLineRules.Find(new List<RealLifeOverhaul.Careers.Application>(), "x", 1) == null,
            "job offer: the newest interviewed application for the very building, none for another building or an empty file");
        Check(OfferLineRules.Key(OfferLineRules.Liked) == "reallife_ui_world_offer_liked" && OfferLineRules.Key(OfferLineRules.Fine) == "reallife_ui_world_offer_fine"
              && OfferLineRules.Key(OfferLineRules.Narrow) == "reallife_ui_world_offer_narrow" && OfferLineRules.Key(OfferLineRules.None) == null,
            "job offer: each band has its text, and none says nothing");
        string kit = W2Source("Ui/World/JobOfferKit.cs");
        Check(kit.Contains("_m.Body = building != null ? Line(building.Address) : null;") && kit.Contains("OfferLineRules.Find(careers.Work.Applications") && kit.Contains("mine.fired"),
            "job offer: the card sets its body from the interview, else the game's record of a firing there, else nothing");
    }

    // ------------------------------------------------------------------------------------------------- clothes

    private static void W2Clothes()
    {
        ClothesSeen.Reset();
        // The first look at a category makes nothing new; once the shop closes its garments are seen; a garment that comes later is new.
        Check(!ClothesSeen.IsNew("1.a", "shirt"), "clothes New: on a first visit nothing is new, whatever the shop shows");
        ClothesSeen.Show("1.a", "shirt");
        ClothesSeen.Show("1.a", "coat");
        Check(!ClothesSeen.IsNew("1.a", "shirt"), "clothes New: a garment shown on this visit is not new on it");
        ClothesSeen.Commit();
        Check(!ClothesSeen.IsNew("1.a", "shirt") && !ClothesSeen.IsNew("1.a", "coat") && ClothesSeen.IsNew("1.a", "scarf"),
            "clothes New: after the shop closed what it showed is seen, and a garment it got since is new");
        Check(!ClothesSeen.IsNew("2.b", "scarf"), "clothes New: a category never looked at has nothing new, so no one is told their whole rack is");
        // The line round trip, an old save (no line), another version, the cap.
        string line = ClothesSeen.Write();
        ClothesSeen.Reset();
        Check(!ClothesSeen.IsNew("1.a", "scarf"), "clothes New: a save without the line sees nothing as new");
        Check(ClothesSeen.Read(line) && ClothesSeen.IsNew("1.a", "scarf") && !ClothesSeen.IsNew("1.a", "coat") && ClothesSeen.Write() == line,
            "clothes New: the seen set is saved and read back exactly, and writes the same line again");
        Check(!ClothesSeen.Read("v=9^x") && ClothesSeen.Read(string.Empty) && ClothesSeen.Read(null), "clothes New: a line of another version is refused, an empty one is no state");
        ClothesSeen.Reset();
        for (int i = 0; i < ClothesSeen.Most + 400; i++)
        {
            ClothesSeen.Show("3.c", "g" + i);
            if (i % 200 == 0)
            {
                ClothesSeen.Commit();
            }
        }
        ClothesSeen.Commit();
        Check(ClothesSeen.Write().Split('^').Length - 1 <= ClothesSeen.Most, "clothes New: the saved set never holds more than " + ClothesSeen.Most + " ids");
        ClothesSeen.Reset();
        string wardrobe = W2Source("Ui/Buy/WardrobeScreen.cs"), shop = W2Source("Leisure/Shops/ClothesScreen.cs"), state = W2Source("Core/ModState.cs");
        Check(wardrobe.Contains("_filterOf[pills++] = Fresh;") && wardrobe.Contains("reallife_ui_buy_filter_new") && wardrobe.Contains("(_filterKind != Fresh || _fresh[i])")
              && wardrobe.Contains("Leisure.ClothesSeen.IsNew(category, id)") && shop.Contains("ClothesSeen.Commit();")
              && state.Contains("Leisure.ClothesSeen.ReadSave(data)") && state.Contains("Leisure.ClothesSeen.WriteSave(data)") && state.Contains("Leisure.ClothesSeen.Reset()"),
            "clothes New: the shop's filter lists the new garments, the seen set is committed when the shop closes and saved with the mod's lines");
    }

    // ------------------------------------------------------------------------------------------------- vehicle, cargo

    private static void W2Vehicle()
    {
        // The item panel's range and the purchase's litres per 100 km are one sum: a car of 150 power, the game's tier of 80 km/h.
        float perKm = FuelUseRules.PerKm(150f, 0.45f, 0.1f, 1f, 80f);
        Check(Math.Abs(perKm - 150f / 10f * 0.55f * 0.1f * 5.5f / 80f) < 0.00001f && Math.Abs(FuelUseRules.Per100(150f, 80f) - perKm * 100f) < 0.0001f && FuelUseRules.Per100(150f, 80f) > 4f
              && FuelUseRules.Per100(150f, 80f) < 7f,
            "vehicle: litres per 100 km is the item panel's fuel sum times 100 (150 power at 80 km/h is about 5.7)");
        Check(FuelUseRules.Per100(0f, 80f) == 0f && FuelUseRules.Per100(150f, 0f) == 0f && FuelUseRules.PerKm(150f, 2f, 0.1f, 1f, 80f) == 0f && FuelUseRules.MilesAGallon(0f) == 0f
              && Math.Abs(FuelUseRules.MilesAGallon(7.84f) - 30f) < 0.1f,
            "vehicle: a car with no engine or no tier has no figure, a perfect engine burns nothing, and miles a gallon is 235 over the litres");
        // Seats: every car the mod knows has between one and eight, a sports car two, a limousine eight.
        string[] known = { "honzamimic", "vordtiaravic", "bima320", "mersaidis500", "missamvillian", "petrollsfanton", "limo", "vordpony", "ferdinand112", "mersaidimgagt", "anselmoaf90",
            "umcnunavut", "vordv150", "umcdesert", "umcdesertdeliveryjob", "mersaididash", "deliverytruck", "freighttruckt1", "empiremustanggt:vehicletype_mustanggt", "empirebmwg82:vehicletype_empirebmwg82",
            "electricscooter", "handtruck", "flatbed", "unknown_car" };
        bool inRange = true;
        foreach (string name in known)
        {
            CarSpec spec = CarCatalogue.For("ba:vehicletype_" + name, new CarTraits(true, false, 70, 8, false, 1200f));
            int seats = CarCatalogue.Seats("ba:vehicletype_" + name, spec.Kind);
            inRange &= seats >= 1 && seats <= 8;
        }
        CarKind kind = CarCatalogue.For("ba:vehicletype_ferdinand112", default).Kind;
        Check(inRange && CarCatalogue.Seats("ba:vehicletype_limo", CarKind.Luxury) == 8 && CarCatalogue.Seats("ba:vehicletype_ferdinand112", kind) == 2
              && CarCatalogue.Seats("ba:vehicletype_honzamimic", CarKind.Economy) == 5 && CarCatalogue.Seats(null, CarKind.Untouched) == 1,
            "vehicle: every car's seats are between one and eight; a sports car seats two, a limousine eight, an untouched ride one");
        string buy = W2Source("Ui/Buy/VehicleBuyScreen.cs"), panel = W2Source("Ui/Buy/ItemPanelKit.cs");
        Check(buy.Contains("FuelUseRules.Per100(t.enginePower, t.maxSpeed)") && buy.Contains("CarCatalogue.Seats(t.vehicleTypeName, spec.Kind)") && buy.Contains("reallife_ui_buy_fuel_use")
              && buy.Contains("reallife_ui_buy_seats") && panel.Contains("FuelUseRules.PerKm(body.powertrain.engine.maxPower, fuel.efficiency, fuel.idleConsumption, fuel.consumptionMultiplier, car.vehicleType.maxSpeed)"),
            "vehicle: the purchase states fuel use and seats, the item panel's range uses the same fuel rule");
        Check(panel.Contains("_m.Owner = car != null ? UiText.T(mine ? \"reallife_ui_buy_yours\" : \"reallife_ui_buy_not_yours\") : null;") && panel.Contains("GoingPublic.MineVehicle(car)"),
            "item panel: the Yours or Not yours chip is set from Going Public's own question of whose car it is");
    }

    private static void W2Cargo()
    {
        Check(CargoMoveRules.Can(true, true, false, true) == CargoMoveState.Ready && CargoMoveRules.Can(false, true, false, true) == CargoMoveState.NoWarehouse
              && CargoMoveRules.Can(true, false, false, true) == CargoMoveState.Unpaid && CargoMoveRules.Can(true, true, true, true) == CargoMoveState.Sealed
              && CargoMoveRules.Can(true, true, false, false) == CargoMoveState.Full,
            "cargo move: offered only at the player's warehouse, for goods paid for, not sealed, with room; full is drawn disabled");
        Check(!CargoMoveRules.Shown(CargoMoveState.NoWarehouse) && !CargoMoveRules.Shown(CargoMoveState.Unpaid) && !CargoMoveRules.Shown(CargoMoveState.Sealed)
              && CargoMoveRules.Shown(CargoMoveState.Ready) && CargoMoveRules.Shown(CargoMoveState.Full)
              && CargoMoveRules.LabelKey(CargoMoveState.Full) == "reallife_ui_buy_move_full" && CargoMoveRules.LabelKey(CargoMoveState.Ready) == "reallife_ui_buy_move_to",
            "cargo move: the button is drawn for ready and full only, each with its words");
        Check(CargoMoveRules.NoticeKey(3, 3) == "reallife_ui_buy_moved" && CargoMoveRules.NoticeKey(2, 3) == "reallife_ui_buy_moved_part" && CargoMoveRules.NoticeKey(0, 3) == "reallife_ui_buy_move_none"
              && CargoMoveRules.Room(0, 1) && CargoMoveRules.Room(2, 0) && !CargoMoveRules.Room(0, 0),
            "cargo move: the notice says all, part or none; room is a free slot or a part box to fill");
        string move = W2Source("Ui/Buy/CargoMove.cs"), screen = W2Source("Ui/Buy/CargoScreen.cs");
        Check(move.Contains("reg is Warehouse w && GoingPublic.Mine(reg)") && move.Contains("Shelves[s].MergeIntoCargo(pack)") && move.Contains("Shelves[s].TryToAddToCargo(pack)")
              && move.Contains("holder.RemoveFromCargo(pack)") && move.Contains("!pack.paid || pack.IsSealed"),
            "cargo move: a stack goes by the game's own hand-over between holders, only paid goods, only to the player's own warehouse, and leaves the vehicle only when it is on a shelf");
        Check(screen.Contains("CargoMoveRules.Can(") && screen.Contains("CargoMove.Move(_ui.currentCargoHolder, _chosen, _to)") && screen.Contains("!_actions[arg].Disabled && Again(Moved())"),
            "cargo move: the panel draws the button and presses the move, a disabled one does nothing");
    }

    // ------------------------------------------------------------------------------------------------- factory, rival

    private static void W2Factory()
    {
        var worked = new bool[BizOpsRules.Hours];
        for (int h = 8; h < 14; h++)
        {
            worked[h] = true;
        }
        Check(BizOpsRules.TodayHours(worked, 5, true) == 0 && BizOpsRules.TodayHours(worked, 6, true) == 1 && BizOpsRules.TodayHours(worked, 6, false) == 0
              && BizOpsRules.TodayHours(worked, 12, false) == 4 && BizOpsRules.TodayHours(worked, 12, true) == 5 && BizOpsRules.TodayHours(worked, 20, false) == 6
              && BizOpsRules.TodayHours(worked, 24, false) == 6 && BizOpsRules.TodayHours(null, 12, true) == 1,
            "factory table today: the hours counted are those somebody stood at it from 06:00 to now, and this hour if it is running");
        Check(BizOpsRules.TodayMade(5, 12) == 60 && BizOpsRules.TodayMade(0, 12) == 0 && BizOpsRules.TodayMade(5, 0) == 0 && BizOpsRules.TodayMade(-1, 12) == 0
              && BizOpsRules.TodayWorth(60, 3.5f) == 210L && BizOpsRules.TodayWorth(0, 3.5f) == 0L && BizOpsRules.TodayWorth(60, 0f) == 0L,
            "factory table today: made is the hours times the pace, worth is made times the wholesale price, none for none");
        string plant = W2Source("Ui/BizMan/Ops/BizManScreen.Plant.cs");
        Check(plant.Contains("Today(item, made, reg, perHour);") && plant.Contains("EmployeeHelper.GetEmployeeAtStationAndHour(reg, item.id, h)") && plant.Contains("item.IsWorkstationActive(reg)")
              && plant.Contains("reallife_ui_bizops_today_line"),
            "factory table today: the machine's page states what it made today, said as about, from the table's worker hours and pace");
        string managers = W2Source("Ui/BizMan/Ops/BizManScreen.Managers.cs");
        Check(managers.Contains("RunHour.GetFormattedTime(0f)") && managers.Contains("reallife_ui_bizops_run_at"),
            "delivery log: a run of an earlier day is stated with the game's fixed run hour; the record keeps the day only");
    }

    private static void W2Rival()
    {
        var metres = new float[] { 300f, 80f, -1f, 150f, 40f };
        var staff = new int[] { 4, 0, 6, 3, 5 };
        Check(BizOpsRules.NearestCrew(metres, staff, 5) == 4 && BizOpsRules.NearestCrew(metres, staff, 4) == 3 && BizOpsRules.NearestCrew(metres, new int[5], 5) == -1
              && BizOpsRules.NearestCrew(metres, staff, 0) == -1 && BizOpsRules.NearestCrew(new[] { -1f }, new[] { 4 }, 1) == -1,
            "rival crew: the nearest rival of the kind with a crew, skipping one with no staff or no place in the city; none when none");
        string schedule = W2Source("Ui/BizMan/Ops/BizManScreen.Schedule.cs"), crew = W2Source("Ui/BizMan/Ops/RivalCrew.cs");
        Check(schedule.Contains("RivalCrew.Nearest(b.buildingRegistration, out string rival, out int rivalStaff)") && crew.Contains("reg.aiEmployees.Count") && crew.Contains("BizOpsRules.NearestCrew("),
            "rival crew: the schedule states the nearest rival's head count as the game keeps it (its AI employees), not an hourly rota the game does not keep");
    }

    // ------------------------------------------------------------------------------------------------- rota

    // A plan of 2 stations and 3 workers on a day: cells are day * 24 + hour, At[cell * stations + station] the worker or -1.
    private static RotaPlan W2Plan(int stations)
    {
        var plan = new RotaPlan();
        plan.Size(stations);
        for (int i = 0; i < plan.At.Length; i++)
        {
            plan.At[i] = -1;
        }
        return plan;
    }

    private static void W2RotaChecks()
    {
        // Double booking: the same person on two stations in one hour, counted by the hour and named by the first.
        RotaPlan plan = W2Plan(2);
        Check(ManagerRules.DoubleBooked(plan, 2, 3, out int none, out int noCell) == 0 && none == -1 && noCell == -1, "manager rota check: an empty week has no double booking");
        int cell = 1 * RotaCatalogue.Hours + 10;   // Tuesday 10:00
        plan.At[cell * 2 + 0] = 1;
        plan.At[cell * 2 + 1] = 1;
        plan.At[(cell + 1) * 2 + 0] = 1;
        plan.At[(cell + 1) * 2 + 1] = 2;   // two different people: not a double booking
        plan.At[(cell + 5) * 2 + 0] = 0;
        plan.At[(cell + 5) * 2 + 1] = 0;
        int doubles = ManagerRules.DoubleBooked(plan, 2, 3, out int worker, out int first);
        Check(doubles == 2 && worker == 1 && first == cell, "manager rota check: a person on two stations at once is counted by the hour (2) and the first is named (worker 1, Tuesday 10:00)");
        Check(ManagerRules.DoubleBooked(plan, 2, 1, out int w2, out int c2) == 1 && w2 == 0 && c2 == cell + 5,
            "manager rota check: a worker the planner does not read (past its list) is not named");
        // The kit's schedule names the same: the day's problem list reads the same shape (a person in two shifts overlapping in hours).
        string screen = W2Source("Ui/BizMan/Ops/BizManScreen.Schedule.cs"), director = W2Source("Economy/Company/CompanyDirector.ManagerDecisions.cs");
        Check(screen.Contains("reallife_ui_bizops_problem_double") && director.Contains("ManagerRules.DoubleBooked(sheet.Now, sheet.Problem.Stations, sheet.Problem.Workers, out int worker, out int cell)")
              && director.Contains("reallife_mgr_rota_double") && director.Contains("NameDoubleBooking(mp, sheet, who);"),
            "manager rota check: the manager names a double booking at the weekly check, as BizMan's problem list does for the day shown");

        // Fill the gaps through RotaLedger: a day open 09:00 to 17:00, the station's lane busy 12 to 14, the person busy 10 to 11.
        var open = new bool[24];
        var stationBusy = new bool[24];
        var personFree = new bool[24];
        for (int h = 0; h < 24; h++)
        {
            open[h] = h >= 9 && h < 17;
            personFree[h] = h != 10;
        }
        stationBusy[12] = stationBusy[13] = true;
        var starts = new int[24];
        var ends = new int[24];
        int runs = RotaLedger.GapRuns(9, 17, false, open, stationBusy, personFree, starts, ends);
        Check(runs == 3 && starts[0] == 9 && ends[0] == 10 && starts[1] == 11 && ends[1] == 12 && starts[2] == 14 && ends[2] == 17,
            "fill the gaps: the plan's 09:00 to 17:00 shift is written in the three runs left free (09-10, 11-12, 14-17), the placed hours untouched");
        Check(RotaLedger.GapRuns(7, 19, false, open, stationBusy, personFree, starts, ends) == 3 && starts[0] == 9 && ends[2] == 17,
            "fill the gaps: hours outside the opening hours are never written");
        Check(RotaLedger.GapRuns(7, 9, true, open, stationBusy, personFree, starts, ends) == 1 && starts[0] == 7 && ends[0] == 9 && RotaLedger.GapRuns(7, 9, false, open, stationBusy, personFree, starts, ends) == 0,
            "fill the gaps: a cleaning shift may stand outside the opening hours, a work shift may not");
        Array.Clear(stationBusy, 0, 24);
        Array.Fill(personFree, false);
        Check(RotaLedger.GapRuns(9, 17, false, open, stationBusy, personFree, starts, ends) == 0 && RotaLedger.GapRuns(17, 9, false, open, stationBusy, personFree, starts, ends) == 0,
            "fill the gaps: a person with no free hour and an empty range write nothing");
        // The whole fill: the plan's shifts for a day, the present week kept for those the planner leaves as they are.
        RotaPlan fill = W2Plan(2);
        for (int h = 9; h < 17; h++)
        {
            fill.At[(0 * 24 + h) * 2 + 0] = 0;   // Monday: worker 0 on station 0 from 9 to 17
            fill.At[(0 * 24 + h) * 2 + 1] = (short)(h < 13 ? 1 : 2);   // station 1: worker 1 to 13, worker 2 after
        }
        var dayOpen = new[] { true, true, true, true, true, true, true };
        var there = new[] { true, true };
        var workers = new[] { true, true, true };
        var fixedWorker = new[] { false, false, true };   // worker 2 holds a desk: the planner leaves them as they are
        var shifts = new List<RotaShift>();
        RotaLedger.Shifts(fill, dayOpen, there, workers, fixedWorker, shifts);
        bool worker2 = false;
        int mondayShifts = 0;
        foreach (RotaShift s in shifts)
        {
            worker2 |= s.Worker == 2;
            mondayShifts += s.Day == 0 ? 1 : 0;
        }
        Check(!worker2 && mondayShifts == 2 && shifts.Count == 2, "fill the gaps: the plan gives a shift for each run of a person at a station, none for somebody fixed, none for a day with no plan");
        var week = new List<RotaShift> { new RotaShift { Day = 0, From = 9, To = 17, Worker = 2, Station = 1 }, new RotaShift { Day = 0, From = 9, To = 11, Worker = 0, Station = 1 } };
        RotaLedger.Write(week, fill, dayOpen, there, workers, fixedWorker, new List<RotaShift>());
        bool keptFixed = false;
        foreach (RotaShift s in week)
        {
            keptFixed |= s.Worker == 2;
        }
        Check(keptFixed && week.Count == 3, "fill the gaps: a fill keeps the shifts of somebody fixed and writes the plan's for the rest");
        Check(screen.Contains("Economy.RotaLedger.GapRuns(g.From, g.To, g.Cleaning, open, GapStation, GapPerson, GapFrom, GapTo)") && screen.Contains("Place(g, GapFrom[r], GapTo[r])"),
            "fill the gaps: BizMan writes only the runs RotaLedger gives, through the game's own schedule");
    }

    // ------------------------------------------------------------------------------------------------- time skip

    private static void W2TimeSkip()
    {
        Check(UiTimeSkip.Speed(60f, true, 0f) == 60 && UiTimeSkip.Speed(60f, false, 143.6f) == 144 && UiTimeSkip.Speed(0.2f, true, 0f) == 1 && UiTimeSkip.Speed(60f, false, -3f) == 1,
            "time skip: the pace is the whole distance a second when constant, else the curve's value, at least 1");
        Check(Math.Abs(UiTimeSkip.Along(100f, 100f) - 0f) < 0.0001f && Math.Abs(UiTimeSkip.Along(100f, 25f) - 0.75f) < 0.0001f && UiTimeSkip.Along(100f, 0f) == 1f && UiTimeSkip.Along(0f, 5f) == 1f
              && UiTimeSkip.Along(100f, 500f) == 0f,
            "time skip: how far along the run is, 0 at the start to 1 at the goal, held inside");
        string skip = W2Source("Ui/Activity/TimeSkipScreen.cs");
        Check(skip.Contains("reallife_ui_skip_pace") && skip.Contains("reallife_ui_skip_pace_until") && skip.Contains("UiTimeSkip.Speed(distance, constant, curve)") && skip.Contains("_machine.timeSpeedCurve.Evaluate"),
            "time skip: the pace line says Fast forward xN from the game's own speed curve");
    }

    // ------------------------------------------------------------------------------------------------- texts

    private static Dictionary<string, string> W2Locale(string code)
    {
        string text = File.ReadAllText(FilePath.Combine(W2Root, "Locales", code + ".json")).TrimStart('﻿');
        return JsonSerializer.Deserialize<Dictionary<string, string>>(text);
    }

    private static readonly string[] W2NewKeys =
    {
        "reallife_ui_skip_pace", "reallife_ui_skip_pace_until", "reallife_ui_funeral_backup_when", "reallife_ui_funeral_backup_line_plain", "reallife_ui_world_offer_liked",
        "reallife_ui_world_offer_fine", "reallife_ui_world_offer_narrow", "reallife_ui_world_offer_let_go", "reallife_ui_buy_fuel_use", "reallife_ui_buy_l100", "reallife_ui_buy_mpg",
        "reallife_ui_buy_seats", "reallife_ui_buy_move_to", "reallife_ui_buy_move_full", "reallife_ui_buy_moved", "reallife_ui_buy_moved_part", "reallife_ui_buy_move_none",
        "reallife_ui_buy_filter_new", "reallife_ui_bizops_today", "reallife_ui_bizops_today_line", "reallife_ui_bizops_today_not_yet", "reallife_ui_bizops_run_at", "reallife_ui_bizops_rival_crew",
        "reallife_ui_bizops_rival_crew_value", "reallife_mgr_rota_double", "reallife_agency_cost_hire", "reallife_agency_cost_board", "reallife_social_says_minutes",
        "reallife_street_says_turn", "reallife_street_says_invite", "reallife_fame_says_press", "reallife_fame_says_part", "reallife_fame_says_promise"
    };

    private static void W2Texts()
    {
        Dictionary<string, string> en = W2Locale("en"), pt = W2Locale("pt"), zh = W2Locale("zh-cn");
        bool all = true;
        string missing = string.Empty;
        foreach (string key in W2NewKeys)
        {
            bool ok = en.TryGetValue(key, out string e) && pt.TryGetValue(key, out string p) && zh.TryGetValue(key, out string z) && e.Length > 0 && p.Length > 0 && z.Length > 0;
            if (ok)
            {
                // The same {names} in all three: a translation that loses or invents one prints the braces.
                var names = new HashSet<string>(Regex.Matches(en[key], @"\{(\w+)\}").Cast2());
                ok = names.SetEquals(Regex.Matches(pt[key], @"\{(\w+)\}").Cast2()) && names.SetEquals(Regex.Matches(zh[key], @"\{(\w+)\}").Cast2());
            }
            all &= ok;
            missing += ok ? string.Empty : " " + key;
        }
        Check(all, "wave 2 audit texts: every new key is in en, pt and zh-cn with the same {names}" + (all ? string.Empty : ", failing:" + missing));
        // The ten texts the audit found unused: each is used by the code now, or gone from every language.
        string[] ten = { "reallife_ui_activity_stress", "reallife_ui_activity_skill", "reallife_ui_activity_skill_day", "reallife_ui_activity_shift_hours", "reallife_ui_activity_stop",
            "reallife_ui_buy_yours", "reallife_ui_buy_not_yours", "reallife_ui_dialog_deliver_to", "reallife_ui_bizops_free", "reallife_ui_bizops_hours_left" };
        var used = new Dictionary<string, int>();
        foreach (string file in Directory.GetFiles(FilePath.Combine(W2Root, "Scripts"), "*.cs", SearchOption.AllDirectories))
        {
            if (FilePath.GetFileName(file).StartsWith("UiScreens.", StringComparison.Ordinal))
            {
                continue;   // a screen's list of its words is not a use of one
            }
            string text = File.ReadAllText(file);
            foreach (string key in ten)
            {
                if (text.Contains("\"" + key + "\""))
                {
                    used[key] = used.TryGetValue(key, out int n) ? n + 1 : 1;
                }
            }
        }
        foreach (string key in ten)
        {
            bool inUse = used.ContainsKey(key), inText = en.ContainsKey(key) || pt.ContainsKey(key) || zh.ContainsKey(key);
            Check(inUse || !inText, "wave 2 audit texts: " + key + (inUse ? " is used by the code" : " is in no language") + ", never an orphan");
        }
    }

    private static IEnumerable<string> Cast2(this MatchCollection matches)
    {
        foreach (Match m in matches)
        {
            yield return m.Groups[1].Value;
        }
    }

    // ------------------------------------------------------------------------------------------------- cost lines

    private static void W2CostLines()
    {
        // Each dialog that has an answer costing money or time states it under that answer, through the same figure the deal uses.
        var expect = new (string File, string[] Needles, string Why)[]
        {
            ("Careers/AgencyDesk.cs", new[] { "reallife_agency_cost_hire", "reallife_agency_cost_board" }, "agency desk: the jobs board's days and vacancies, the hiring page's fees"),
            ("Family/FamilyPhone.Choices.cs", new[] { "FamilyBalance.HoneymoonCost", "FamilyBalance.PocketMoney((s().Children[child].Pocket + 1)" }, "family phone: the honeymoon and the pocket money"),
            ("Family/Children.cs", new[] { "reallife_ui_dialog_cost_month", "FamilyBalance.PocketMoney((c.Pocket +" }, "family talk: the nursery and the nanny a month, the pocket money a week"),
            ("Social/PersonDialog.cs", new[] { "reallife_social_says_minutes", "reallife_ui_dialog_cost_on_the_day" }, "person dialog: an outing's share and minutes"),
            ("Street/StreetDirector.Talk.cs", new[] { "reallife_street_says_turn", "reallife_street_says_invite" }, "street talk: the turn an answer uses, the share of a coffee"),
            ("Fame/FameReporter.cs", new[] { "FamePress.Answer(ask, 0, FameVenue.Street)", "reallife_fame_says_press", "reallife_fame_says_promise" }, "fame reporter: the recognition, the standing and the promise of an answer"),
            ("Ui/Dialog/DialogForm.Costs.cs", new[] { "reallife_ui_dialog_deliver_to" }, "delivery forms: where it is delivered to"),
        };
        foreach (var item in expect)
        {
            string text = W2Source(item.File);
            bool ok = true;
            foreach (string needle in item.Needles)
            {
                ok &= text.Contains(needle);
            }
            Check(ok, "cost lines, " + item.Why);
        }
        // The lines under one answer stay within what a row holds, and a dialog that costs nothing in money or time states none.
        string answers = W2Source("Core/GameUi/DialogAnswers.cs");
        Check(answers.Contains("public const int LinesMost = 3;"), "cost lines: an answer holds at most three lines");
        var free = new[] { "Civic/CityHallPhone.cs", "Law/RecordPhone.cs", "Social/PeopleDialog.cs", "Travel/TravelPhone.cs", "Home/HomePhone.cs" };
        foreach (string file in free)
        {
            string text = W2Source(file);
            Check(!Regex.IsMatch(text, @"ChangeMoney|BizBooks\.Book|\.Pay\(|MoneyOut"), "cost lines: " + file + " asks no money of the player in any answer, so it states no cost");
        }
    }

    // ------------------------------------------------------------------------------------------------- 84 days

    // Eighty-four game days (twelve weeks), as the director's day turn would play them for what these fixes touch: a clothes shop the
    // player visits each Saturday where a garment is added on day 40, the manager's rota check each Sunday at 21 on a week that
    // gets a double booking on day 30 and is mended on day 37, and a vendor shift of the player's each weekday that the player
    // cuts to two hours on days 50 to 56. A save is written on day 42 and read back; the same days then play out the same, and a save of
    // before the new line loads and plays on.
    private static void W2Run()
    {
        ClothesSeen.Reset();
        var shop = new List<string> { "shirt", "coat", "scarf" };
        var after = new List<string>();
        int newShown = 0, newAfterVisit = 0, doublesSeen = 0, weeksChecked = 0, stressDays = 0, partDays = 0;
        float stress = 0f;
        long earned = 0;
        string savedLine = null;
        RotaPlan plan = W2Plan(2);
        bool doubled = false;
        int each = 20, perShift = 12;
        for (int day = 1; day <= 84; day++)
        {
            if (day == 40)
            {
                shop.Add("hat");   // the shop gets a garment
            }
            if (day == 30)
            {
                plan.At[(2 * RotaCatalogue.Hours + 11) * 2 + 0] = 0;
                plan.At[(2 * RotaCatalogue.Hours + 11) * 2 + 1] = 0;
                doubled = true;
            }
            if (day == 37)
            {
                plan.At[(2 * RotaCatalogue.Hours + 11) * 2 + 1] = -1;
                doubled = false;
            }
            if (day == 43 && savedLine != null)
            {
                ClothesSeen.Reset();
                Check(ClothesSeen.Read(savedLine), "84 days: the line written on day 42 reads back on day 43");
            }
            if (day % 7 == 6)   // the player visits the shop on Saturdays
            {
                int fresh = 0;
                foreach (string id in shop)
                {
                    fresh += ClothesSeen.IsNew("1.a", id) ? 1 : 0;
                    ClothesSeen.Show("1.a", id);
                }
                newShown += fresh;
                ClothesSeen.Commit();
                foreach (string id in shop)
                {
                    newAfterVisit += ClothesSeen.IsNew("1.a", id) ? 1 : 0;
                }
                if (day == 41)
                {
                    savedLine = ClothesSeen.Write();
                }
                if (day >= 41 && day < 47)
                {
                    Check(day != 41 || fresh == 1, "84 days: on day 41 the garment the shop got on day 40 is the one new garment");
                }
            }
            if (day % 7 == 0)   // Sunday: the manager's rota check at hour 21
            {
                for (int hour = 0; hour < 24; hour++)
                {
                    if (!ManagerRules.Due(day, hour, ManagerTask.Rota))
                    {
                        continue;
                    }
                    weeksChecked++;
                    int doubles = ManagerRules.DoubleBooked(plan, 2, 3, out int who, out int at);
                    doublesSeen += doubles;
                    Check((doubles > 0) == doubled && (doubles == 0 || (who == 0 && at == 2 * RotaCatalogue.Hours + 11)),
                        "84 days: the Sunday check of day " + day + " names the double booking exactly while there is one");
                }
            }
            if (day % 7 != 6 && day % 7 != 0)   // a working day with the cart: whole, or two hours from day 50 to 56
            {
                int chosen = day >= 50 && day <= 56 ? 2 : 0;
                int units = ShiftFactsRules.Units(chosen, perShift, each);
                int doneMinutes = units * each;
                stress += ShiftFactsRules.Stress(HomeBalance.StressJobShift, doneMinutes, false);
                earned += ShiftFactsRules.Pay(18f, ClockRules.Units(doneMinutes, true, units, each, int.MaxValue), 0.85f);
                stressDays++;
                partDays += chosen > 0 ? 1 : 0;
                Check(units == (chosen > 0 ? 6 : perShift), "84 days: the choice of day " + day + " covers " + (chosen > 0 ? 6 : perShift) + " units");
            }
        }
        Check(newShown == 1, "84 days: over twelve weeks of Saturdays one garment was new, the one added on day 40 (saw " + newShown + ")");
        Check(newAfterVisit == 0, "84 days: after each visit closes nothing is new");
        Check(weeksChecked == 12, "84 days: the manager's rota check ran on each of the twelve Sundays (" + weeksChecked + ")");
        Check(doublesSeen == 1 * 1, "84 days: the double booking was named on the Sunday of day 35 and not before or after (" + doublesSeen + " hours in all)");
        Check(partDays == 5 && stressDays == 60, "84 days: five part days among sixty working days (" + partDays + " of " + stressDays + ")");
        // 55 whole days of 12 units at 18 x 0.85 = 183.6 (rounded 184), 5 part days of 6 units = 92.
        Check(earned == 55L * 184 + 5L * 92 && Math.Abs(stress - (55 * 4 * HomeBalance.StressJobShift + 5 * 2 * HomeBalance.StressJobShift)) < 0.01f,
            "84 days: the pay and the stress of the part days are the part's, of the whole days the whole's (earned " + earned + ")");
        // A save from before the line: no line in the save, nothing new, and the days play on.
        ClothesSeen.Reset();
        Check(ClothesSeen.Read(null) && !ClothesSeen.IsNew("1.a", "hat"), "84 days: a save from before the clothes line loads with nothing seen and nothing new");
        ClothesSeen.Reset();
    }
}
