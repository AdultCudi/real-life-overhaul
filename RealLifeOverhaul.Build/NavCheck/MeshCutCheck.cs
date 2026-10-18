using System;
using System.IO;
using System.Text.RegularExpressions;

// Over the sources as they ship: what the session record names as the mod's last change to the walking mesh
// (Core/MeshCuts) is a constant text that says where the obstacle is, so a long frame's line points at the right thing.
internal static partial class Program
{
    private static void MeshCutLabels()
    {
        string scripts = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        int notes = 0;
        string notConstant = null;
        foreach (string file in Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories))
        {
            foreach (Match note in Regex.Matches(Code(File.ReadAllText(file)), @"MeshCuts\.Note\(((?:""[^""]*""|[^,)""])*)"))
            {
                notes++;
                string what = note.Groups[1].Value;
                if (!what.Contains('"') || what.Contains('+') || what.Contains('$'))   // a text, or a choice of texts; never one built
                {
                    notConstant = (notConstant == null ? "" : notConstant + ", ") + Path.GetFileName(file);
                }
            }
        }
        Check(notes >= 8 && notConstant == null, $"mesh cuts: each of the {notes} changes to the walking mesh is named by a constant text" + (notConstant != null ? " (not: " + notConstant + ")" : ""));

        // The patrol car of a police presence stands at the kerb of a plain lane, never in a junction (PolicePresence.IsPlainLane).
        string presence = Source(scripts, @"People\PolicePresence.cs");
        Check(presence.Contains("MeshCuts.Note(\"a patrol car parked at the kerb\")") && Body(presence, "private static bool IsPlainLane(").Contains("IsInIntersection()"),
            "mesh cuts: the police presence's patrol car is named as parked at the kerb, where it stands, never in a junction");
    }
}
