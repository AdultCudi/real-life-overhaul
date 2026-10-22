using System;
using System.IO;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Offices;

// Faults found in Scripts/Driving, Garage and Offices: the car tuning put the mod's engine figures on whatever car was
// selected, another Going Public player's included, though the handling it carries refused that car; and the empty shops
// made offices took an address off the game's list of addresses its rivals leave alone whoever had put it there.
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

        bool ours = false;
        int add = ConversionRules.IgnoreChange(true, false, ref ours);
        bool oursAfterAdd = ours;
        int off = ConversionRules.IgnoreChange(false, true, ref ours);
        Check(add == 1 && oursAfterAdd && off == -1 && !ours, "offices fix: an address the mod put on the rivals' list is the mod's, and it takes it off");
        ours = false;
        int theirs = ConversionRules.IgnoreChange(true, true, ref ours);
        bool oursAfterTheirs = ours;
        int left = ConversionRules.IgnoreChange(false, true, ref ours);
        Check(theirs == 0 && !oursAfterTheirs && left == 0 && !ours, "offices fix: an address on the list already (the game's own, another mod's) is never the mod's to take off");
        ours = true;
        int gone = ConversionRules.IgnoreChange(false, false, ref ours);
        int again = ConversionRules.IgnoreChange(true, true, ref ours);
        Check(gone == 0 && again == 0 && !ours, "offices fix: the mod's entry taken off by somebody else is no longer the mod's; one listed by another stays theirs");
        string game = Source(scripts, @"Offices\ConversionGame.cs"), conversions = Source(scripts, @"Offices\Conversions.cs");
        Check(Body(game, "public static bool Ignore(Address address, bool on, bool ours)").Contains("ConversionRules.IgnoreChange(on, at >= 0, ref ours)")
              && Regex.Matches(conversions, @"ConversionGame\.Ignore\([^;]*, m\.Ignored\)").Count == 3 && !Regex.IsMatch(conversions, @"ConversionGame\.Ignore\([^,;]*, (false|ignore)\);"),
            "offices fix: every change of the rivals' list goes by the one rule, with the record of whether the entry is the mod's");
    }
}
