using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

// A building registration whose building the city lacks (Core/GameRegistrations). The game's own
// BuildingRegistration.Neighborhood and GetBuildingType read BuildingCached without a test, so on such a record (a player's
// save held one, left by another mod) each throws; the tech sector's hiring read Neighborhood directly and switched itself off
// at every load (0.52.1). So no source reads a registration's district, building type or building but through
// GameRegistrations, which gives nothing for such a one and names it once in the log.
internal static partial class Program
{
    private static readonly Regex RegistrationRead = new Regex(@"\.(Neighborhood\b|GetBuildingType\s*\(|BuildingCached\b)", RegexOptions.Compiled);
    private const string RegistrationHelper = "Core/GameRegistrations.cs";

    private static void Registrations(string scripts)
    {
        Section("a building registration's district, type and building, read only through Core/GameRegistrations");
        if (!Directory.Exists(scripts))
        {
            Check(false, "the mod's scripts are not at " + scripts);
            return;
        }
        int users = 0;
        var direct = new List<string>();
        foreach (string file in Directory.EnumerateFiles(scripts, "*.cs", SearchOption.AllDirectories))
        {
            string name = Path.GetRelativePath(scripts, file).Replace('\\', '/');
            if (name == RegistrationHelper)
            {
                continue;
            }
            string[] lines = File.ReadAllLines(file);
            bool uses = false;
            for (int n = 0; n < lines.Length; n++)
            {
                string line = lines[n].TrimStart();
                if (line.StartsWith("//", StringComparison.Ordinal))
                {
                    continue;
                }
                uses |= line.Contains("GameRegistrations.");
                if (RegistrationRead.IsMatch(line))
                {
                    direct.Add(name + ":" + (n + 1));
                }
            }
            users += uses ? 1 : 0;
        }
        Check(users >= 30, "fewer sources read registrations through GameRegistrations than the mod has (" + users + "): the reading is broken");
        Check(direct.Count == 0, "a source reads a registration's Neighborhood, GetBuildingType or BuildingCached itself, which throws on one whose building the city lacks: "
                                 + string.Join(", ", direct));
        string helper = Path.Combine(scripts, "Core", "GameRegistrations.cs");
        string text = File.Exists(helper) ? File.ReadAllText(helper) : string.Empty;
        Check(text.Contains("Building building = reg.BuildingCached;") && text.Contains("return building != null ? building.Neighbourhood : null;")
              && text.Contains("return building != null ? building.BuildingType : null;") && text.Contains("Told.Add(where)"),
            "GameRegistrations reads the building once, gives null without it and names such a registration once a session");
        Check(RegistrationRead.IsMatch("string n = all[i] != null ? all[i].Neighborhood : null;") && RegistrationRead.IsMatch("r.GetBuildingType() == Residential")
              && RegistrationRead.IsMatch("if (reg.BuildingCached == null)") && !RegistrationRead.IsMatch("string n = GameRegistrations.District(all[i]);")
              && !RegistrationRead.IsMatch("b.building.Neighbourhood == Hamptons"),
            "the reading finds a registration's district, type and building read directly, and not through the helper nor a building's own Neighbourhood");
        Console.WriteLine("   " + users + " source(s) read registrations through GameRegistrations; " + direct.Count + " read one directly.");
    }
}
