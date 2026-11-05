using System;
using System.Text.RegularExpressions;

// A community service shift is not ended by anything but a real reason, and no street scene starts inside it (Scripts/Justice: CommunityService.cs,
// JusticeBridges.cs, JusticeDirector.Prompts.cs; Scripts/Wiring: Scenes.cs). Source checks, as the shift itself needs the game.
internal static partial class Program
{
    private static void CommunityServiceChecks()
    {
        string cs = ModSource(@"Justice\CommunityService.cs");
        string scenes = ModSource(@"Wiring\Scenes.cs");
        string prompts = ModSource(@"Justice\JusticeDirector.Prompts.cs");
        string bridges = ModSource(@"Justice\JusticeBridges.cs");

        Check(cs.Length > 0 && scenes.Length > 0, "community service: the sources are found");
        Check(!Regex.IsMatch(cs, @"_picked\s*>=\s*JusticeLaw\.CommunityShiftPieces\s*\|\|\s*!Lay\("), "community service: a place for the next piece not found at once does not end the shift as a whole one");
        Check(Regex.Matches(cs, @"Stop\(true\)").Count == 1 && Regex.IsMatch(cs, @"if \(_picked >= JusticeLaw\.CommunityShiftPieces\)\s*\{\s*Stop\(true\);"),
            "community service: the shift ends as whole only with every piece picked");
        Check(cs.Contains("_laying") && cs.Contains("LayPatience") && Regex.IsMatch(cs, @"if \(_laying\)\s*\{[^}]*Lay\(player\.transform\.position\)"),
            "community service: with no place yet the shift waits and looks again on its next ticks, for a few seconds");
        Check(Regex.IsMatch(cs, @"Reachable\(from, spot\)\s*\|\|\s*CitySpots\.Reachable\(route, spot\)"), "community service: the route test also starts from the last pile, which is known to be on the walkable mesh");
        Check(cs.Contains("shift ended early (") && Regex.Matches(cs, @"\bEnd\(").Count >= 4, "community service: every early end logs its reason once");

        Check(Regex.IsMatch(scenes, @"Service\s*=\s*16"), "community service: a shift is a scene of its own beside the accident, crime, event and rally");
        Check(Regex.IsMatch(scenes, @"Justice\.ServiceOn\s*\?\s*Scene\.Service"), "community service: the scenes ask whether a shift is on");
        Check(bridges.Contains("public static bool ServiceOn"), "community service: the Justice bridge answers it");
        string[] starts = { @"Traffic\TrafficAccidents.Staging.cs", @"Crime\CrimeDirector.cs", @"Events\EventDirector.cs", @"Wiring\CivicLinks.cs" };
        foreach (string start in starts)
        {
            Check(ModSource(start).Contains("Scenes.AnyBut("), "community service: " + start + " starts a scene only when no other is on, a shift included");
        }
        Check(prompts.Contains("Wiring.Scenes.AnyLargeSceneActive ? JusticeRefusal.Busy"), "community service: a shift is not begun in the middle of a street scene");
    }
}
