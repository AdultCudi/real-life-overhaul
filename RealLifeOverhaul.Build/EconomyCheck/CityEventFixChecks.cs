using System.Collections.Generic;
using System.Text.RegularExpressions;

// City events in the sources (Scripts/Events): the spare hose lies by the parked engine at a fire, stood once and given
// back on every way out; the weather's pace for traffic leaves a Going Public guest's traffic (the host's cars) alone; and
// a street event's arrivals carry on past one who never arrived, with the others due that second.
internal static partial class Program
{
    private static void CityEventFixes()
    {
        string scene = TextsSource("Events/FireScene.cs"), response = TextsSource("Events/FireScene.Response.cs"), props = TextsSource("Events/EventProps.cs");
        string plan = Between(response, "private void PlanCrew(", "private void StandCoil(");
        string stand = Between(response, "private void StandCoil(", "private void ReturnCoil(");
        string depart = Between(response, "private void Depart(", "private bool Left(");
        string end = Between(scene, "public void End(bool trafficRunning)", "_director.Cordon.Open();");
        Check(props.Contains("public const string HoseCoil = \"hose_coil\";") && plan.Contains("StandCoil(unit, modelled, centre, forward, right, side, halfLength, halfWidth);")
              && stand.Contains("_director.Props.Stand(EventProps.HoseCoil, spot, heading)") && stand.Contains("CitySpots.ClearQuietly(spot, 0.35f)"),
            "fire hose coil: a spare hose is stood by the engine once, as it parks, on clear ground only");
        Check(depart.Contains("ReturnCoil();") && end.Contains("ReturnCoil();") && Regex.IsMatch(stand, @"^\s*private void StandCoil\([^)]*\)\s*\{\s*ReturnCoil\(\);"),
            "fire hose coil: it is given back as the crew pack up and on every way the fire ends, and never stood twice");
        Check(!Regex.IsMatch(TextsSource("Events/FireScene.cs") + response, @"void (Update|LateUpdate)\("), "fire hose coil: nothing of it runs a frame");

        string weather = TextsSource("Events/Weather.cs");
        string apply = Between(weather, "private void ApplySpeed()", "private void RestoreSpeed()");
        Check(apply.Contains("float wanted = GoingPublic.TrafficMirrored ? 1f : Weather.TrafficSpeedFactor;"),
            "weather traffic: a Going Public guest's traffic is the host's cars, never slowed here, and put back once the machine is a guest");
        Dictionary<string, string> en = LoadWords(System.IO.Path.Combine(LocalesFolder(), "en.json"));
        Check(AnyChangesRow(en, "spare rolled hose") && AnyChangesRow(en, "speed of the host's traffic"), "fire hose coil, weather traffic: What's new says so");

        string arrivals = Between(TextsSource("Events/StreetEventScene.cs"), "private void Arrivals(", "private void Second(");
        Check(Regex.IsMatch(arrivals, @"Drop\(i, true\);[^\n]*\n\s*i--;[^\n]*\n\s*continue;") && !arrivals.Contains("return;"),
            "street event arrivals: one who never arrived is dropped and the rest due that second are still seen to (the last of the cast, moved into the place, too)");
    }
}
