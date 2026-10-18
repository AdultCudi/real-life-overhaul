using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Law;

// A police station's door and front desk (Law/StationReach.cs): the panel and its key from the doorstep, as the game lets
// the player through one of its own doors, and never from the kerb or the road; and over the mod's sources, a distance
// in metres from JobSpots.Flat never compared with a square nor rooted as if it were one.
internal static partial class Program
{
    private static void StationReaches()
    {
        // A street as the precincts are chosen on: the door's point on the wall, a pavement 3.5 m deep, then the road.
        // Straight out from the door: z metres; along the wall: x metres.
        Check(StationReach.AtDoor(0f, 0f, 0.7f) && StationReach.AtDoor(0.9f, 0f, 1.2f) && StationReach.AtDoor(0f, 0.15f, 1.9f),
            "station: the door is taken from the doorstep, 0.7 to 1.9 m out (the game lets the player in from under 2 m)");
        Check(!StationReach.AtDoor(0f, 0f, 2.1f) && !StationReach.AtDoor(1.6f, 0f, 1.6f),
            "station: the door is not taken from 2.1 m out or 2.3 m off along the wall (more than the game's own 2 m)");
        Check(!StationReach.AtDoor(0f, -0.15f, 3.5f) && !StationReach.AtDoor(0f, -0.15f, 5f) && !StationReach.AtDoor(0f, -0.15f, 7f)
              && !StationReach.AtDoor(3f, -0.15f, 20f), "station: the door is not taken from the kerb 3.5 m out, nor from the road 5, 7 or 20 m out");
        Check(!StationReach.AtDoor(0f, 3.2f, 0.5f) && !StationReach.AtDoor(0f, -3.2f, 0.5f),
            "station: the door is not taken from a floor above or below it, 3.2 m up or down");
        Check(StationReach.Door == 2f && StationReach.Door * StationReach.Door == 4f,
            "station: the door's reach is the game's own, a squared distance under 4 (CityBuildingController.IsPlayerNearEntranceDoor)");
        Check(StationReach.AtDesk(1.2f) && StationReach.AtDesk(2.2f) && !StationReach.AtDesk(2.4f) && !StationReach.AtDesk(5f),
            "station: the front desk answers from 2.2 m on the flat and not from 2.4 or 5 m");
        Check(Math.Abs(StationReach.Metres(1.2f, 1.6f) - 2f) < 0.001f, "station: the self-check line's distance on the flat is in metres (1.2 by 1.6 is 2 m)");

        FlatMetres();
    }

    // Over every source of the mod: JobSpots.Flat gives metres, so a call compared straight away is never compared with a
    // square (the same term multiplied by itself, or a name that says it is a square) and is never rooted.
    private static void FlatMetres()
    {
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        var call = new Regex(@"\bJobSpots\.Flat\(");
        var square = new Regex(@"^\(?\s*([A-Za-z_][\w.]*)\s*\*\s*\1\s*\)?$");
        int compared = 0;
        var wrong = new List<string>();
        foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            string code = Code(File.ReadAllText(file));
            foreach (Match m in call.Matches(code))
            {
                string where = Path.GetFileName(file) + ":" + (Count(code, m.Index) + 1);
                int before = m.Index - 1;
                while (before >= 0 && char.IsWhiteSpace(code[before]))
                {
                    before--;
                }
                if (before >= 4 && code.Substring(before - 4, 5) == "Sqrt(")
                {
                    wrong.Add(where + " rooted");
                }
                int end = Closing(code, m.Index + m.Length - 1);
                foreach (string other in new[] { ComparedAfter(code, end), ComparedBefore(code, m.Index) })
                {
                    if (other == null)
                    {
                        continue;
                    }
                    compared++;
                    if (square.IsMatch(other) || other.IndexOf("sqr", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        wrong.Add(where + " against '" + other + "'");
                    }
                }
            }
        }
        Check(compared > 50 && wrong.Count == 0, "sources: every distance in metres from JobSpots.Flat (" + compared + " compared) is compared with metres, never a square"
                                                 + (wrong.Count > 0 ? "; not: " + string.Join(", ", wrong) : string.Empty));
    }
}
