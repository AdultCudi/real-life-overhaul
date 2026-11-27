using System;
using System.Collections.Generic;

// The weekday names of 27 Nov 2026: the game numbers its days of the week from Monday as 1 (DayOfWeekOrdered), so a list of names that starts at
// Monday is read one before the game's number. Read as it was, the pause menu said Friday on a Thursday, a work card named the next day and a
// place's opening hours stood under the wrong day's name, with Sunday twice. Run alone with --days33.
internal static partial class Program
{
    private static void Days33()
    {
        string scripts = System.IO.Path.GetFullPath(System.IO.Path.Combine(LocalesFolder(), @"..\Scripts"));
        var wrong = new List<string>();
        int named = 0;
        foreach (string path in System.IO.Directory.GetFiles(scripts, "*.cs", System.IO.SearchOption.AllDirectories))
        {
            string[] lines = System.IO.File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                // a member list naming the game's method is no use of its number
                if (!line.Contains("GetDayOfWeekIndex(") || line.Contains("StaticMethod("))
                {
                    continue;
                }
                named++;
                if (!line.Contains("- (int)DayOfWeekOrdered.Monday"))
                {
                    wrong.Add(System.IO.Path.GetFileName(path) + ":" + (i + 1));
                }
            }
        }
        Check(named >= 3, "days33: the pause menu, the work card and a place's hours read the game's weekday number (" + named + " uses found)");
        Check(wrong.Count == 0, "days33: every use of the game's weekday number takes Monday's 1 off before it picks a name from a list starting at Monday"
                                + (wrong.Count > 0 ? " (not at " + string.Join(", ", wrong) + ")" : string.Empty));
    }
}
