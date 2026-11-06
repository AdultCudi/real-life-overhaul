using System;
using System.Text;
using RealLifeOverhaul;

// Checks for the map check's verdict (Diagnostics/MapCheckRules.cs, used by Diagnostics/SessionWatch.Map;
// docs/standards/performance.md, RLO-PERF-022): the player's camera is part of it, so a camera not as before the map
// fails the check whatever the parts say (0.61.0 beta 6 wrote "its draw distances by layer differ" and then PASS);
// a camera that cannot be set against the one before is not held against it; an opening of the map made while the last
// one was still shutting keeps the camera of the first, since the game's shutting coroutine was stopped before its word
// that the camera is back. Compiled as it ships.
internal static partial class Program
{
    private static void MapChecksVerdict()
    {
        string Verdict(bool cut, bool back, int lingering, float within, MapCameraSeen camera)
        {
            var line = new StringBuilder(80);
            MapCheckRules.Verdict(line, cut, back, lingering, within, 2f, camera);
            return line.ToString();
        }

        Check(Verdict(false, true, 0, 0.01f, MapCameraSeen.Same) == "restored, 0 lingering: PASS", "map check: every part back at once and the camera as before passes");
        Check(Verdict(false, true, 0, 0.01f, MapCameraSeen.NotKnown) == "restored, 0 lingering: PASS",
            "map check: a camera that could not be kept as the map opened is not held against it");
        string camera = Verdict(false, true, 0, 0.01f, MapCameraSeen.Differs);
        Check(camera.StartsWith("FAIL", StringComparison.Ordinal) && camera.Contains("the player's camera not as before the map") && !camera.Contains("PASS"),
            "map check: the player's camera not as before the map fails it, every part back or not: " + camera);
        string slow = Verdict(false, true, 0, 3.4f, MapCameraSeen.Same);
        Check(slow == "FAIL, restored only after 3.4 s", "map check: a part back only after the time wanted fails it: " + slow);
        string both = Verdict(false, true, 0, 3.4f, MapCameraSeen.Differs);
        Check(both == "FAIL, restored only after 3.4 s, the player's camera not as before the map", "map check: both failings are said: " + both);
        string lingering = Verdict(false, false, 2, 15f, MapCameraSeen.Same);
        Check(lingering == "FAIL, 2 lingering", "map check: a part still holding something fails it: " + lingering);
        string lingeringCamera = Verdict(false, false, 1, 15f, MapCameraSeen.Differs);
        Check(lingeringCamera == "FAIL, 1 lingering, the player's camera not as before the map", "map check: a part still holding something and the camera both said: " + lingeringCamera);
        Check(Verdict(true, false, 1, 0.5f, MapCameraSeen.Differs) == "not judged, the map was opened again first",
            "map check: opened again before the parts were back is not judged");
        Check(Verdict(true, true, 0, 0.5f, MapCameraSeen.Differs).StartsWith("FAIL", StringComparison.Ordinal),
            "map check: opened again after the parts were back is judged, the camera with it");
        Check(MapCheckRules.Passes(true, 0, 2f, 2f, MapCameraSeen.Same) && !MapCheckRules.Passes(true, 0, 2.01f, 2f, MapCameraSeen.Same),
            "map check: back within the time wanted, to its end, passes");

        // Which camera an opening is set against: its own, unless the last opening was still shutting.
        Check(MapCheckRules.TakesCamera(false), "map check: an opening from the street keeps the camera as it is then");
        Check(!MapCheckRules.TakesCamera(true), "map check: an opening while the last one was still shutting keeps the camera of the first");
    }
}
