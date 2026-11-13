using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Uptown;

// The districts the mod adds must be known to the game wherever it asks for a district by name (a player's log, 14 Nov 2026: the game's
// NeighborhoodHelper.GetData threw for reallife_neighborhood_uptown and _cedarheights while a city ended, because the mod took the
// records out of the game's lookup before the other features had put their figures back and the game drew customers again).
// Game-free: every district name the mod's sources give a building or a table is one of DistrictSplit.Ids, which is what the set-up
// puts in the game's lookup, list and array; and the sources read as text: the set-up registers every id, the city's end keeps them
// in the lookup, the customers' draw refuses an unknown district once, with its name.
internal static partial class Program
{
    private static void UptownKnownChecks()
    {
        string scripts = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        var names = new SortedSet<string>(StringComparer.Ordinal);
        var where = new Dictionary<string, string>(StringComparer.Ordinal);
        if (Directory.Exists(scripts))
        {
            foreach (string file in Directory.EnumerateFiles(scripts, "*.cs", SearchOption.AllDirectories))
            {
                foreach (Match m in Regex.Matches(File.ReadAllText(file), "\"(reallife_neighborhood_[a-z0-9_]*)\""))
                {
                    if (names.Add(m.Groups[1].Value))
                    {
                        where[m.Groups[1].Value] = System.IO.Path.GetFileName(file);
                    }
                }
            }
        }
        string unknown = string.Join(", ", names.Where(n => !DistrictSplit.Ids.Contains(n)).Select(n => n + " (" + where[n] + ")"));
        Check(names.Count >= 2 && unknown.Length == 0,
            "districts known: every neighbourhood name the mod's sources give a building or a table is one the set-up registers" + (unknown.Length > 0 ? " (not registered: " + unknown + ")" : string.Empty));
        Check(DistrictSplit.Ids.Distinct().Count() == DistrictSplit.Count && Enumerable.Range(0, DistrictSplit.Count).All(d => DistrictSplit.Ids[d].StartsWith("reallife_", StringComparison.Ordinal)),
            "districts known: the table of districts has each id once, none of the game's own");
        bool lots = true;
        foreach (Lot l in LayoutRules.Lots)
        {
            lots &= l.District >= 0 && l.District < DistrictSplit.Count && DistrictSplit.Ids[l.District] == DistrictSplit.IdOf(l.District);
        }
        Check(lots && LayoutRules.Lots.Count > 0, "districts known: every lot's district is a registered one");

        string game = SplitSource(@"Uptown\LifeGame.cs"), director = SplitSource(@"Uptown\LifeDirector.cs");
        string know = MemberText(game, "public static bool Know(out string why)"), unload = MemberText(game, "public static void Unload(bool cityEnds = false)");
        Check(know.Contains("for (int d = 0; d < DistrictSplit.Count; d++)") && know.Contains("lookup[id] = Records[d];") && know.Contains("list.Add(id);") && know.Contains("setArray.Invoke(")
              && know.Contains("AllNeighbourhoodBuildings[id]") && know.Contains("PeopleRows(d, f);") && know.Contains("WholesaleRow(d);"),
            "districts known: the set-up puts every district of the table in the game's lookup, list, array and the run-long tables");
        Check(unload.Contains("d < DistrictSplit.Count && !cityEnds") && unload.Contains("lookup.Remove(") && director.Contains("LifeGame.Unload(true);")
              && MemberText(director, "private static void Begin()").Contains("LifeGame.Unload();"),
            "districts known: a city's end keeps the districts in the game's lookup (the stand-downs still draw customers); the next load's start gives the records back");
        string draw = SplitSource(@"Core\CustomerDraw.cs");
        string known = MemberText(draw, "private static bool DistrictKnown(BuildingRegistration reg)");
        Check(MemberText(draw, "private static void Draw(BuildingRegistration reg, int figure, int hour)").Contains("!DistrictKnown(reg)")
              && known.Contains("LifeGame.GameKnows(id)") && MemberText(game, "public static bool GameKnows(string id)").Contains("lookup.ContainsKey(") && known.Contains("Unknown.Add(id)") && known.Contains("Logger?.Warn(") && known.Contains("return false;"),
            "districts known: the customers' draw refuses a business in a district the game does not know, quietly, with one line naming it");
    }
}
