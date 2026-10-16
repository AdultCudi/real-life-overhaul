using System;
using System.Collections.Generic;
using System.IO;
using RealLifeOverhaul;

// The game's build (Core/GameBuild.cs as it ships): which build is older or newer, the line Player.log gets, the member
// named in Mono's own messages of a missing member (the very lines a player's Build 3675 wrote), and the build of the
// installed game the mod is built against, which must be GameBuild.MadeFor: after a game update this fails until the
// mod has been checked against the new game and MadeFor raised (docs/contributing/after-a-game-update.md).
internal static partial class Program
{
    private static void BuildRules()
    {
        Section("the game's build: the rules (Core/GameBuild)");
        int made = GameBuild.MadeFor;
        Check(GameBuild.Of(made) == BuildKind.Same, "build: the build the mod is made for is the same build");
        Check(GameBuild.Of(3675) == BuildKind.Older && GameBuild.Of(made - 1) == BuildKind.Older, "build: Build 3675, and the one just before, are older");
        Check(GameBuild.Of(made + 1) == BuildKind.Newer, "build: the next build is newer");
        Check(GameBuild.Of(0) == BuildKind.Unknown && GameBuild.Of(-5) == BuildKind.Unknown, "build: a build that could not be read (0) is unknown, not older");

        Check(GameBuild.Line(made, "9.9.9") == null, "build: the build the mod is made for writes no warning (the diagnostics' check line says it)");
        string older = GameBuild.Line(3675, "9.9.9");
        Check(older != null && older.Contains("Build 3675") && older.Contains("Build " + made) && older.Contains("older") && older.Contains("9.9.9"),
            "build: an older game's line names both builds, says older and names the mod's version");
        Check(older != null && older.Contains("Betas set to None") && older.Contains("Verify integrity of game files"), "build: an older game's line says how to update it in Steam");
        string newer = GameBuild.Line(made + 8, "9.9.9");
        Check(newer != null && newer.Contains("newer") && !newer.Contains("Verify integrity"), "build: a newer game's line says newer, and nothing of updating");
        string unknown = GameBuild.Line(0, "9.9.9");
        Check(unknown != null && unknown.Contains("could not be read") && unknown.Contains("Build " + made), "build: a build that could not be read says so, with the build the mod is made for");
        foreach (string line in new[] { older, newer, unknown, GameBuild.Builds(3675, "9.9.9"), GameBuild.Builds(0, "9.9.9") })
        {
            Check(line != null && line.IndexOf('—') < 0 && line.IndexOf('–') < 0 && line.IndexOf(" - ", StringComparison.Ordinal) < 0 && line.IndexOf('!') < 0,
                "build: a line of the build check has no dash and no exclamation mark: " + line);
        }
        Check(GameBuild.Builds(3675, "9.9.9") == "This game is Build 3675; Real Life Overhaul 9.9.9 is made for Build " + made + ".", "build: the closing words name both builds");

        // The messages exactly as Build 3675 wrote them to Player.log (Mono's), and .NET's, and a few edges.
        var cases = new (string Message, string Member)[]
        {
            ("Method not found: void .HudConfirm.Show(string,string,System.Action,System.Action,string,string,bool,bool)",
             "HudConfirm.Show(string,string,System.Action,System.Action,string,string,bool,bool)"),
            ("Method not found: EntityController UI.InGameUI.BuildingResume.get_TargetController()", "UI.InGameUI.BuildingResume.TargetController"),
            ("Method not found: string .SubwayStation.GetNameKey()", "SubwayStation.GetNameKey()"),
            ("Field not found: int Buildings.Building.locatedInsidePrivateFenceIndex Due to: Could not find field in class", "Buildings.Building.locatedInsidePrivateFenceIndex"),
            ("Could not resolve type with token 010002a5 from typeref (expected class 'Helpers.FuneralHelper' in assembly 'BigAmbitions, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null')",
             "Helpers.FuneralHelper"),
            ("Could not load type 'Helpers.FuneralHelper' from assembly 'BigAmbitions, Version=0.0.0.0'.", "Helpers.FuneralHelper"),
            ("Method not found: 'Void HudConfirm.Show(System.String, System.String)'.", "HudConfirm.Show(System.String, System.String)"),
            ("Method not found: void .Foo.set_Bar(bool)", "Foo.Bar"),
            ("Field not found: bool .HudConfirm.isOpen", "HudConfirm.isOpen"),
        };
        foreach (var c in cases)
        {
            string got = GameBuild.MemberOf(c.Message);
            Check(got == c.Member, "member: '" + c.Message + "' names '" + got + "', wanted '" + c.Member + "'");
        }
        Check(GameBuild.MemberOf(null) == "a member it does not name" && GameBuild.MemberOf("") == "a member it does not name", "member: no message names no member, without a fault");
        string other = GameBuild.MemberOf("Something else went wrong\non two lines");
        Check(other == "Something else went wrong on two lines", "member: any other message is given as it is, on one line");
        Check(GameBuild.MemberOf(new string('x', 400)).Length == 160, "member: a long message is cut to a line");
    }

    // The game's build as its main menu reads it: GameVersion.GetCurrent() is the resource Versioning/Current, a
    // ScriptableObject in resources.assets whose serialised fields after its name ("Current") are experimental (a bool,
    // padded to four bytes), buildNumber (an int) and latestBuildNumberForVersion (a count, then build and name pairs).
    internal static int ReadInstalledBuild(string dataFolder, out string how)
    {
        string path = Path.Combine(dataFolder, "resources.assets");
        how = path;
        if (!File.Exists(path))
        {
            how = path + " is not there";
            return 0;
        }
        byte[] data = File.ReadAllBytes(path);
        byte[] name = { 7, 0, 0, 0, (byte)'C', (byte)'u', (byte)'r', (byte)'r', (byte)'e', (byte)'n', (byte)'t' };
        var found = new List<int>();
        for (int at = IndexOf(data, name, 0); at >= 0; at = IndexOf(data, name, at + 1))
        {
            int field = (at + name.Length + 3) & ~3;
            if (field + 12 > data.Length || data[field] > 1 || data[field + 1] != 0 || data[field + 2] != 0 || data[field + 3] != 0)
            {
                continue;
            }
            int build = BitConverter.ToInt32(data, field + 4);
            int groups = BitConverter.ToInt32(data, field + 8);
            if (build < 1000 || build > 100000 || groups < 1 || groups > 200)
            {
                continue;
            }
            // The groups: an int and a name each, in order, the last running to 9999 or beyond.
            int read = field + 12, last = 0;
            bool fits = true;
            for (int g = 0; g < groups && fits; g++)
            {
                if (read + 8 > data.Length)
                {
                    fits = false;
                    break;
                }
                int upTo = BitConverter.ToInt32(data, read);
                int length = BitConverter.ToInt32(data, read + 4);
                fits = upTo >= last && length >= 0 && length < 64 && read + 8 + length <= data.Length;
                last = upTo;
                read = (read + 8 + length + 3) & ~3;
            }
            if (fits && last >= build)
            {
                found.Add(build);
            }
        }
        if (found.Count != 1)
        {
            how = path + ": " + (found.Count == 0 ? "the game's version object was not found" : found.Count + " version objects were found");
            return 0;
        }
        return found[0];
    }

    private static int IndexOf(byte[] data, byte[] what, int from)
    {
        for (int i = from; i <= data.Length - what.Length; i++)
        {
            if (data[i] != what[0])
            {
                continue;
            }
            int j = 1;
            while (j < what.Length && data[i + j] == what[j])
            {
                j++;
            }
            if (j == what.Length)
            {
                return i;
            }
        }
        return -1;
    }

    // The game's build on disk against the build the mod is made for: the gate's guard against a game update that has
    // not been checked yet.
    private static int InstalledBuild(string managed)
    {
        Section("the game's build on disk, against GameBuild.MadeFor");
        string dataFolder = Path.GetDirectoryName(managed.TrimEnd('\\', '/'));
        int build = dataFolder != null ? ReadInstalledBuild(dataFolder, out string how) : 0;
        if (build <= 0)
        {
            Check(false, "game build: the build of the game the mod is built against could not be read (" + (dataFolder ?? managed) + "); give the game's Managed folder as the first argument");
            return 0;
        }
        Check(build == GameBuild.MadeFor, "game build: the game at " + managed + " is Build " + build + ", and GameBuild.MadeFor says " + GameBuild.MadeFor
            + ". After a game update: read what this check prints of the new game, fix it, then set GameBuild.MadeFor (Core/GameBuild.cs) to " + build);
        Console.WriteLine("   the game at " + managed + " is Build " + build + "; the mod is made for Build " + GameBuild.MadeFor + ".");
        return build;
    }
}
