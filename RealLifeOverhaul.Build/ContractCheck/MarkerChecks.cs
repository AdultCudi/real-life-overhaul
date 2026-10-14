using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

// The mod's markers on the game's city map (Core/MapPlaces: CanMark, Drop). A marker the game's map is given
// (CityMap.AddPoi) while the map is switched off (its time machine: sleeping, skipping time) is never woken by the engine,
// so its own OnDestroy, which takes it out of CityMap.pois, never runs; destroyed like that it stays in the game's list
// dead, and the game's CityMap.LateUpdate threw a NullReferenceException on it every frame (0.16.0). So every marker the
// mod takes off goes through MapPlaces.Drop, which takes it out of the game's list first: no source destroys a marker
// itself, and MapPlaces makes none while the map is off.
internal static partial class Program
{
    private static readonly Regex MarkerDestroy = new Regex(@"Destroy(Immediate)?\s*\(\s*[\w\.\[\]]*(poi|Poi|Marker)\w*\s*\.\s*gameObject", RegexOptions.Compiled);

    private static void Markers(string scripts)
    {
        Section("the mod's markers on the game's city map (Core/MapPlaces.Drop)");
        if (!Directory.Exists(scripts))
        {
            Check(false, "the mod's scripts are not at " + scripts);
            return;
        }
        int makers = 0;
        var direct = new List<string>();
        foreach (string file in Directory.EnumerateFiles(scripts, "*.cs", SearchOption.AllDirectories))
        {
            string[] lines = File.ReadAllLines(file);
            string name = Path.GetRelativePath(scripts, file).Replace('\\', '/');
            bool makes = false;
            for (int n = 0; n < lines.Length; n++)
            {
                string line = lines[n].TrimStart();
                if (line.StartsWith("//", StringComparison.Ordinal) || line.StartsWith("///", StringComparison.Ordinal))
                {
                    continue;
                }
                makes |= line.Contains(".AddPoi(");
                if (MarkerDestroy.IsMatch(line) && !(name == "Core/MapPlaces.cs" && line.Contains("UnityEngine.Object.Destroy(marker.gameObject)")))
                {
                    direct.Add(name + ":" + (n + 1));
                }
            }
            makers += makes ? 1 : 0;
        }
        Check(makers >= 3, "fewer sources give the game's map a marker than the mod has (" + makers + "): the reading is broken");
        Check(direct.Count == 0, "a marker of the mod's is destroyed without MapPlaces.Drop, so one never woken would stay in the game's list dead: " + string.Join(", ", direct));
        string places = File.ReadAllText(Path.Combine(scripts, "Core", "MapPlaces.cs"));
        Check(places.Contains("city.cityMap.pois.Remove(marker)") && places.Contains("city.UpdatePointOfInterests()") && places.Contains("cityMap.gameObject.activeInHierarchy"),
            "MapPlaces takes a marker out of the game's list before it goes, and makes none while the map is switched off");
        Check(MarkerDestroy.IsMatch("UnityEngine.Object.Destroy(place.Marker.gameObject);") && MarkerDestroy.IsMatch("Destroy(_poi.gameObject);")
              && !MarkerDestroy.IsMatch("MapPlaces.Drop(_poi);"),
            "the reading finds a marker destroyed directly, and not one dropped");
        Console.WriteLine("   " + makers + " source(s) give the game's map markers; " + direct.Count + " destroy one directly.");
    }
}
