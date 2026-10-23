using System;
using System.IO;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Offices;

// Faults found in Scripts/Driving, Garage and Offices: the car tuning put the mod's engine figures on whatever car was
// selected, another Going Public player's included, though the handling it carries refused that car; the empty shops
// made offices took an address off the game's list of addresses its rivals leave alone whoever had put it there, and
// then (fixed with the held addresses) could take almost no shop, as nearly every building has a rival landlord.
internal static partial class Program
{
    private static void DrivingOfficeFixes()
    {
        string scripts = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        string tuning = Source(scripts, @"Driving\CarTuning.cs");
        string apply = Body(tuning, "private void Apply(CarController car, NwhVehicle vehicle)");
        int mine = apply.IndexOf("if (!GoingPublic.MineVehicle(car))", StringComparison.Ordinal);
        int power = apply.IndexOf("engine.maxPower = ", StringComparison.Ordinal);
        int claim = apply.IndexOf("GarageTuning.Claim(", StringComparison.Ordinal);
        Check(mine > 0 && power > mine && claim > mine, "driving fix: the car tuning's engine figures go only on the player's own car, as the handling's (Going Public)");
        // Every other place in Driving and Garage that writes a car's figures asks the same: the handling, the Garage's walk
        // over the player's cars, and the custom shop's refusal by the save's own list.
        string handling = Source(scripts, @"Driving\CarHandling.cs"), director = Source(scripts, @"Garage\GarageDirector.cs"), shop = Source(scripts, @"Garage\GarageShop.cs");
        Check(Body(handling, "public bool Apply(").Contains("GoingPublic.MineVehicle(car)") && director.Contains("!GoingPublic.MineVehicle(vehicle)")
              && Body(shop, "public static string Refusal(VehicleController vehicle)").Contains("save.VehicleInstances.Contains(vehicle.vehicleInstance)"),
            "driving fix: the handling, the Garage's builds and the custom shop all leave another player's car alone");

        // The rule of the game's ignore list (only the mod's own entry is ever taken off) is the held addresses' now
        // (Core/HeldUnitsRules, checked in EconomyCheck's HeldUnitsChecks): the conversion holds its shops through them.
        string game = Source(scripts, @"Offices\ConversionGame.cs"), conversions = Source(scripts, @"Offices\Conversions.cs"), rules = Source(scripts, @"Offices\ConversionRules.cs");
        Check(Regex.Matches(conversions, @"HeldUnits\.Hold\(m\.Holder, ").Count == 1 && Body(conversions, "private static void Restore(Made m)").Contains("HeldUnits.Release(m.Holder)")
              && !(game + conversions + rules).Contains("IgnoredAddresses") && !(game + conversions + rules).Contains("RivalsHelper") && !rules.Contains("IgnoreChange"),
            "offices fix: a shop made an office is held through the held addresses from the moment it is one until it is a shop again, and nothing of the conversion's own touches the game's ignore list");
        Check(!conversions.Contains("NoLandlord") && !game.Contains("NoLandlord"),
            "offices fix: a shop with a rival landlord may be made an office (nearly every building has one); the landlord is never touched, the held addresses keep the game's letting off it");
    }
}
