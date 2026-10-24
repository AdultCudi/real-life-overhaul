using System;
using System.IO;
using RealLifeOverhaul;

// Car tuning in the custom shop (Garage/TuningRules.cs, Garage/GarageCatalogue.cs, Driving/CarModel.cs; a player's
// suggestion of 19 Oct 2026, "car customisation, like a bit of tuning"). The gear change parts: each change quicker but
// never under the floors, the model's 0 to 100 km/h quicker for it on a car of every kind and never slower, and the
// build line of the new slot read back while an older line still reads as before; the figures page's units and how a
// figure stands; what a build fetches when its car is sold, in every condition, and the game's sale told from anything
// else that takes a car; and, over the sources, the sale listened for once through the guard and only for the player's
// own car, the change written and put back only while still the mod's, and every new text in the three languages.
internal static partial class Program
{
    private static void Tunings()
    {
        string scripts = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        TuningShiftParts();
        TuningModelRuns();
        TuningBuildLines();
        TuningFigures();
        TuningSales();
        TuningSources(scripts);
    }

    // ---- the gear change parts and their sums.
    private static void TuningShiftParts()
    {
        PartDef quick = GarageCatalogue.Part("shift_quick"), seq = GarageCatalogue.Part("shift_seq");
        Check(quick != null && seq != null && quick.Slot == PartSlot.Shift && seq.Slot == PartSlot.Shift, "tuning: the quick shift and sequential gearboxes are in the gear change slot");
        Check(GarageCatalogue.Slots == (int)PartSlot.Shift + 1, "tuning: the slots count the gear change slot (" + GarageCatalogue.Slots + ")");
        Check(quick != null && seq != null && seq.Shift < quick.Shift && quick.Shift < 1f && seq.Price > quick.Price && seq.Wear > 0f,
            "tuning: the sequential box changes quicker than the quick shift one, costs more and wears the car");
        Check(quick != null && quick.Accel == 1f && quick.Top == 1f && seq.Accel == 1f && seq.Top == 1f,
            "tuning: a gearbox claims no gain of its own: the model drives the car with the quicker change");
        Check(GarageCatalogue.SlotName(PartSlot.Shift) == "reallife_tuning_slot_shift", "tuning: the gear change slot has its own name");

        var build = new GarageBuild();
        build.Parts[(int)PartSlot.Shift] = "shift_seq";
        GarageModifiers m = build.Modifiers();
        Check(Near(m.Shift, 0.35f, 1e-5f) && m.ChangesPerformance, "tuning: a sequential box makes a change 0.35 of its time and counts as performance (" + m.Shift + ")");
        Check(GarageModifiers.None.Shift == 1f && !GarageModifiers.None.ChangesPerformance && new GarageBuild().Modifiers().Shift == 1f, "tuning: a stock car changes gear as before");

        foreach (CarKind kind in new[] { CarKind.Economy, CarKind.Saloon, CarKind.Luxury, CarKind.Sports, CarKind.Utility, CarKind.Van, CarKind.Truck })
        {
            float standard = CarCatalogue.ShiftSeconds(kind);
            float a = TuningRules.ShiftSeconds(standard, quick.Shift), b = TuningRules.ShiftSeconds(standard, seq.Shift);
            Check(b <= a && a < standard && b >= TuningRules.MinShiftSeconds, "tuning: " + kind + ": a change of " + standard + " s takes " + a + " s and " + b + " s, never under "
                + TuningRules.MinShiftSeconds + " s");
        }
        Check(TuningRules.ShiftSeconds(0.2f, 1f) == 0.2f && TuningRules.ShiftSeconds(0.2f, 1.5f) == 0.2f, "tuning: no factor makes a change slower");
        Check(TuningRules.ShiftSeconds(0.2f, 0.01f) == 0.2f * TuningRules.MinShiftFactor, "tuning: however parts stack, a change takes at least a quarter of its time");
        Check(TuningRules.ShiftSeconds(0.08f, 0.35f) == TuningRules.MinShiftSeconds, "tuning: a change already quick stops at the floor");
        Check(TuningRules.ShiftSeconds(0.03f, 0.35f) == 0.03f, "tuning: a change the game made quicker than the floor is left as it was");
        Check(TuningRules.ShiftSeconds(0f, 0.35f) == 0f && TuningRules.ShiftSeconds(-1f, 0.35f) == -1f, "tuning: an instant box stays instant, and a reading of none stays none");
    }

    // ---- the model drives the quicker change: 0 to 100 km/h quicker, never slower, on a car of every kind.
    private static void TuningModelRuns()
    {
        var car = new CarPhysics
        {
            Mass = 1400f, WheelRadius = 0.33f, RollingForce = 210f, TractionForce = 1400f * 9.81f * 0.5f * 1.3f * 1.35f, RevLimitRpm = 6500f, IdleRpm = 900f,
            LaunchRpm = 2200f, DriveInertia = 0.35f, WheelInertiaMass = 40f, EngineLoss = 0.25f, Boost = 1f, ForwardGears = 4
        };
        for (int i = 0; i < CarPhysics.CurveSamples; i++)
        {
            float x = i / (float)(CarPhysics.CurveSamples - 1);
            car.Curve[i] = x < 0.8f ? 0.3f + 0.875f * x : 1f - (x - 0.8f) * 1.5f;   // a petrol engine's rise to its best near the limit, and the fall past it
        }
        float peakShare = 0f;
        foreach (float v in car.Curve)
        {
            peakShare = Math.Max(peakShare, v);
        }
        foreach ((CarKind kind, float seconds, float top) in new[] { (CarKind.Economy, 12.5f, 170f), (CarKind.Saloon, 9f, 200f), (CarKind.Sports, 5f, 265f), (CarKind.Van, 16f, 140f), (CarKind.Truck, 20f, 130f) })
        {
            var setup = new CarSetup();
            bool solved = CarModel.Solve(car, kind, seconds, top, true, setup, out CarRun stock);
            Check(solved && stock.To100 > 0f, "tuning: the model drives a " + kind + " car to 100 km/h (" + stock.To100 + " s)");
            if (!solved)
            {
                continue;
            }
            float standard = setup.ShiftSeconds;
            float last = stock.To100;
            setup.ShiftSeconds = 1f;
            bool changes = CarModel.Run(car, setup, true).To100 > stock.To100 + 0.01f;   // a car whose first gear reaches 100 km/h gains nothing to 100
            Check(changes || kind == CarKind.Sports, "tuning: a " + kind + " car changes gear on its way to 100 km/h");
            foreach (string id in new[] { "shift_quick", "shift_seq" })
            {
                setup.ShiftSeconds = TuningRules.ShiftSeconds(standard, GarageCatalogue.Part(id).Shift);
                CarRun run = CarModel.Run(car, setup, true);
                Check(run.To100 > 0f && run.To100 <= last + 1e-4f && (run.To100 < stock.To100 || !changes), "tuning: " + kind + " with the " + id + " box: 0 to 100 km/h " + run.To100 + " s, from " + stock.To100
                    + " s (change " + setup.ShiftSeconds + " s from " + standard + " s)");
                Check(Near(run.TopKmh, stock.TopKmh, 0.5f), "tuning: " + kind + " with the " + id + " box keeps its top speed (" + run.TopKmh + " km/h)");
                last = run.To100;
            }
            setup.ShiftSeconds = standard;
            float peak = CarModel.PeakPower(car, setup.PowerKw), torque = CarModel.PeakTorque(car, setup.PowerKw);
            Check(Near(peak, setup.PowerKw * peakShare, 0.01f) && torque > 0f && torque * 6500f * 0.10471976f / 1000f >= peak * 0.999f,
                "tuning: " + kind + ": the figures page's peak " + peak + " kW and " + torque + " Nm as the game's engine reckons them");
        }
    }

    // ---- the build line: the new slot round trip, an older line as before, a line naming a part in the wrong slot.
    private static void TuningBuildLines()
    {
        var build = new GarageBuild { Paint = 0x1565C0, Spent = 4321 };
        build.Parts[(int)PartSlot.Engine] = "engine_2";
        build.Parts[(int)PartSlot.Shift] = "shift_quick";
        string line = build.Write();
        GarageBuild back = GarageBuild.Read(line);
        Check(back.Parts[(int)PartSlot.Shift] == "shift_quick" && back.Parts[(int)PartSlot.Engine] == "engine_2" && back.Spent == 4321 && back.Paint == 0x1565C0,
            "tuning: a build with a gearbox reads back as written (" + line + ")");
        Check(line.Contains((int)PartSlot.Shift + "=shift_quick;"), "tuning: the gearbox is written under its slot's number, after the slots before it");
        GarageBuild older = GarageBuild.Read("p=1565C0;0=engine_2;v=1800");
        Check(older.Parts[(int)PartSlot.Shift] == null && older.Parts[0] == "engine_2" && older.Spent == 1800, "tuning: a line from before the gear change slot reads as before");
        GarageBuild wrong = GarageBuild.Read("0=shift_seq;8=engine_1;v=10");
        Check(wrong.Parts[0] == null && wrong.Parts[(int)PartSlot.Shift] == null, "tuning: a part named under another slot's number is not fitted");
        GarageBuild newer = GarageBuild.Read("8=shift_seq;9=nitrous;z=1;v=10");
        Check(newer.Parts[(int)PartSlot.Shift] == "shift_seq" && newer.Spent == 10, "tuning: a newer line's slot and field the mod does not know are passed over");
    }

    // ---- the figures page: units and how each figure stands.
    private static void TuningFigures()
    {
        Check(TuningRules.Horsepower(100f) == 134 && TuningRules.Horsepower(0f) == 0 && TuningRules.Horsepower(-1f) == 0, "tuning: 100 kW is 134 hp; nothing for no reading");
        Check(TuningRules.Compare(120f, 120.2f, 120.3f, 0.5f) == FigureChange.Standard, "tuning: the model's rounding is no change");
        Check(TuningRules.Compare(120f, 135f, 135f, 0.5f) == FigureChange.Kept, "tuning: work already paid for shows as now against standard");
        Check(TuningRules.Compare(120f, 120f, 150f, 0.5f) == FigureChange.Changed && TuningRules.Compare(120f, 150f, 120f, 0.5f) == FigureChange.Changed,
            "tuning: this visit's work shows as a change, putting a car back to standard too");
        Check(TuningRules.Compare(-1f, 120f, 150f, 0.5f) == FigureChange.Unknown && TuningRules.Compare(120f, -1f, 150f, 0.5f) == FigureChange.Unknown,
            "tuning: a figure the model cannot read is said so, never shown as a number");
        Check(TuningRules.CompareAny(0f, -0.02f, -0.04f, 0.005f) == FigureChange.Changed && TuningRules.CompareAny(0f, -0.04f, -0.04f, 0.005f) == FigureChange.Kept
              && TuningRules.CompareAny(0f, 0f, 0.001f, 0.005f) == FigureChange.Standard, "tuning: a ride height below standard compares as any other figure");
    }

    // ---- the sale: what the work fetches, and the game's sale told from anything else.
    private static void TuningSales()
    {
        Check(TuningRules.SaleWorth(10000, 20000f, 20000f) == 5000, "tuning: a perfect car's work fetches half of what was spent");
        Check(TuningRules.SaleWorth(10000, 10000f, 20000f) == 2500, "tuning: a car half worn fetches half that");
        Check(TuningRules.SaleWorth(10000, 30000f, 20000f) == 5000, "tuning: never more than the share, whatever the game pays");
        Check(TuningRules.SaleWorth(0, 20000f, 20000f) == 0 && TuningRules.SaleWorth(-5, 20000f, 20000f) == 0, "tuning: nothing spent fetches nothing");
        Check(TuningRules.SaleWorth(10000, 0f, 20000f) == 0, "tuning: a wreck the game pays nothing for fetches nothing for its work");
        Check(TuningRules.SaleWorth(10000, 500f, 0f) == 5000, "tuning: a car with no list price is taken as perfect");
        for (int spent = 0; spent <= 100000; spent += 7919)
        {
            for (float condition = 0f; condition <= 1.2f; condition += 0.1f)
            {
                int worth = TuningRules.SaleWorth(spent, 20000f * condition, 20000f);
                Check(worth >= 0 && worth <= (int)(spent * GarageCatalogue.Resale + 0.5f), "tuning: " + spent + " spent in condition " + condition + " fetches " + worth + ", within the share");
            }
        }
        const string type = "ba:vehicletype_bima320";
        Check(TuningRules.IsSale(TuningRules.SoldType, type, 14000f, type, 14000.4f), "tuning: the game's sale of this car is its sale");
        Check(!TuningRules.IsSale(TuningRules.SoldType, "ba:vehicletype_honzamimic", 14000f, type, 14000f), "tuning: a sale of another kind of car is not");
        Check(!TuningRules.IsSale(TuningRules.SoldType, type, 9000f, type, 14000f), "tuning: a sale at another price (an earlier one) is not");
        Check(!TuningRules.IsSale("ba:transaction_vehiclebought", type, 14000f, type, 14000f) && !TuningRules.IsSale(null, null, 0f, type, 14000f),
            "tuning: anything else that takes a car away (a tow, a hire, a car taken back) is not");
        Check(!TuningRules.IsSale(TuningRules.SoldType, type, 0f, type, 0f) && !TuningRules.IsSale(TuningRules.SoldType, null, 14000f, null, 14000f), "tuning: nothing paid, or no type, is no sale");
    }

    // ---- over the sources.
    private static void TuningSources(string scripts)
    {
        string sale = Source(scripts, @"Garage\GarageSaleGame.cs");
        string tuning = Source(scripts, @"Garage\GarageTuning.cs");
        string driving = Source(scripts, @"Driving\CarTuning.cs");
        string director = Source(scripts, @"Garage\GarageDirector.cs");
        Check(sale.Length > 0 && tuning.Length > 0 && driving.Length > 0 && director.Length > 0, "tuning: the sources are there to read");
        Check(sale.Contains("_onGone = _onGone ?? Guard.Listener<VehicleController>(Gone)") && sale.Contains("onVehicleDestroyed.AddListener(_onGone)")
              && sale.Contains("onVehicleDestroyed?.RemoveListener(_onGone)"), "tuning: the sale is listened for through the guard, with one delegate on and off");
        Check(Count(sale, "AddListener(") == 1 && sale.Contains("if (_listening"), "tuning: the sale is listened for once");
        Check(director.Contains("GarageSaleGame.Start()") && director.Contains("GarageSaleGame.Stop()"), "tuning: the Garage starts and stops the sale's listener");
        Check(sale.Contains("GoingPublic.MineVehicle(vehicle)") && sale.Contains("Settings.GarageResale") && sale.Contains("TuningRules.IsSale("),
            "tuning: only the player's own car, with the option on, and only the game's own sale fetches anything");
        Check(sale.Contains("GarageState.Set(instance.id, null)"), "tuning: the work goes with the car, so it is never paid twice");
        Check(Body(sale, "private static void Gone(").IndexOf("GarageState.Set(instance.id, null)", StringComparison.Ordinal)
              < Body(sale, "private static void Gone(").IndexOf("Crime.Give(", StringComparison.Ordinal), "tuning: the build is forgotten before the money is given");
        Check(tuning.Contains("shiftBox.shiftDuration == _shiftWritten") && tuning.Contains("shiftBox.shiftDuration = _shiftBefore"),
            "tuning: the gear change written by the Garage is put back only while it is still the Garage's");
        Check(driving.Contains("TuningRules.ShiftSeconds(_setup.ShiftSeconds, garage.Shift)") && driving.Contains("box.shiftDuration = _setup.ShiftSeconds")
              && driving.Contains("box.shiftDuration = _stock.ShiftSeconds"), "tuning: car performance writes the build's change and puts the game's back");
        Check(tuning.Contains("_claimed && Settings.CarTuning"), "tuning: with car performance switched off later, the Garage applies the parts itself again");
        Check(tuning.Contains("private void LateUpdate()") && !tuning.Contains("private void Update()"),
            "tuning: the Garage looks at the car after car performance's Update, so neither records the other's change or limiter as the game's");

        string locales = Path.GetFullPath(Path.Combine(scripts, @"..\Locales"));
        string pages = Source(scripts, @"Garage\GarageShop.Pages.cs") + Source(scripts, @"Garage\GarageShop.cs") + Source(scripts, @"Garage\GarageDirector.Tests.cs") + sale
                       + Source(scripts, @"Garage\GarageCatalogue.cs") + Source(scripts, @"Core\RealLifeOverhaulMod.cs");
        var keys = new System.Collections.Generic.HashSet<string>();
        foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(pages, "\"(reallife_tuning_[a-z0-9_]+|reallife_garage_resale|reallife_transaction_garage_resale)\""))
        {
            keys.Add(match.Groups[1].Value);
        }
        Check(keys.Count >= 40, "tuning: the texts the tuning names are found in the sources (" + keys.Count + ")");
        foreach (string language in new[] { "en", "pt", "zh-cn" })
        {
            string file = Path.Combine(locales, language + ".json");
            string text = File.Exists(file) ? File.ReadAllText(file) : string.Empty;
            foreach (string key in keys)
            {
                Check(text.Contains("\"" + key + "\":"), "tuning: " + key + " is in " + language + ".json");
            }
            Check(text.Contains("\"reallife_opt_tip_garage_resale\":") && text.Contains("\"reallife_transaction_garage_resale_label\":"),
                "tuning: the resale option's sentence and the transaction's label are in " + language + ".json");
        }
    }
}
