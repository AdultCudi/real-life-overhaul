using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Meta;

// Versions (Scripts/Meta/VersionRules.cs and the table of Changelog.cs). 1.0.0 came straight after 0.63.11, the first time
// MAJOR moved, so everything that orders versions is held here: the compare that a save's check, the welcome and the
// Guide rest on, the order and the What's new key the table gives a version, and the table itself. The table is read as
// text, because Changelog.cs names the game's texts and cannot be compiled here: each row's order and keys are its
// version's, the rows run newest first, the newest is Latest, the unnumbered row stands five above it, the seven places
// the version is written agree, and no version is dated before an older one.
internal static partial class Program
{
    private static void Versions()
    {
        VersionCompareChecks();
        VersionOrderChecks();
        VersionTableChecks();
    }

    // Oldest first: every pair must compare as its places in this list do.
    private static readonly string[] VersionLadder =
    {
        "0.1.0", "0.1.1", "0.5.12", "0.9.14", "0.10.0", "0.63.9", "0.63.10", "0.63.11", "0.64.0", "0.99.99", "1.0.0", "1.0.1", "1.0.10", "1.1.0", "1.12.3", "2.0.0", "10.0.0"
    };

    private static void VersionCompareChecks()
    {
        bool ladder = true;
        for (int i = 0; i < VersionLadder.Length; i++)
        {
            for (int j = 0; j < VersionLadder.Length; j++)
            {
                ladder &= Math.Sign(VersionRules.Compare(VersionLadder[i], VersionLadder[j])) == Math.Sign(i - j);
            }
        }
        Check(ladder, "versions: every pair of a ladder from 0.1.0 over 0.63.11 and 1.0.0 to 10.0.0 compares as its place says, both ways");
        Check(VersionRules.Compare("0.63.11", "1.0.0") < 0 && VersionRules.Compare("1.0.0", "0.63.11") > 0 && VersionRules.Compare("0.99.99", "1.0.0") < 0,
            "versions: 1.0.0 is newer than 0.63.11 and than any 0.x version");
        Check(VersionRules.Compare("0.9.14", "0.10.0") < 0 && VersionRules.Compare("0.63.9", "0.63.10") < 0 && VersionRules.Compare("1.0.9", "1.0.10") < 0,
            "versions: the parts compare as numbers, not as text");
        Check(VersionRules.Compare("0.64.0", "1.0.0") < 0 && VersionRules.Compare("0.64.0-beta.19", "1.0.0") < 0,
            "versions: a save and a welcome a beta of 0.64.0 left behind are older than 1.0.0");
        Check(VersionRules.Compare("1.0.0", "1.0.0") == 0 && VersionRules.Compare("1.0", "1.0.0") == 0 && VersionRules.Compare("1", "1.0.0") == 0,
            "versions: a missing part counts as 0");
        Check(VersionRules.Compare(null, "0.0.0") == 0 && VersionRules.Compare(string.Empty, null) == 0 && VersionRules.Compare(null, "1.0.0") < 0 && VersionRules.Compare("1.0.0", null) > 0,
            "versions: no version at all is older than any release and never throws");
    }

    private static void VersionOrderChecks()
    {
        Check(VersionRules.OrderOf("0.1.0") == 1000 && VersionRules.OrderOf("0.1.1") == 1010 && VersionRules.OrderOf("0.5.12") == 5120 && VersionRules.OrderOf("0.63.11") == 63110
              && VersionRules.OrderOf("0.99.99") == 99990 && VersionRules.OrderOf("1.0.0") == 100000 && VersionRules.OrderOf("1.0.1") == 100010 && VersionRules.OrderOf("1.1.0") == 101000
              && VersionRules.OrderOf("2.0.0") == 200000,
            "versions: a version's order is (major * 100 + minor) * 1000 + patch * 10, so 0.63.11 is 63110, 0.99.99 is 99990 and 1.0.0 is 100000");
        Check(VersionRules.KeyOf("0.2.0") == "reallife_changes_002_0" && VersionRules.KeyOf("0.5.12") == "reallife_changes_005_12" && VersionRules.KeyOf("0.63.11") == "reallife_changes_063_11"
              && VersionRules.KeyOf("1.0.0") == "reallife_changes_100_0" && VersionRules.KeyOf("1.12.3") == "reallife_changes_112_3",
            "versions: a version's What's new key is major and two digits of minor, then the patch, so 1.0.0 is reallife_changes_100_0");
        // Every version there can be up to 2.99.99, oldest first: the order rises with each, so no two share one and none of 1.x sorts under 0.x.
        int last = -1, steps = 0;
        bool rising = true;
        for (int major = 0; major <= 2; major++)
        {
            for (int minor = 0; minor <= VersionRules.PartMost; minor++)
            {
                for (int patch = 0; patch <= VersionRules.PartMost; patch++)
                {
                    int order = VersionRules.OrderOf(major.ToString(CultureInfo.InvariantCulture) + "." + minor.ToString(CultureInfo.InvariantCulture) + "." + patch.ToString(CultureInfo.InvariantCulture));
                    rising &= order > last;
                    last = order;
                    steps++;
                }
            }
        }
        Check(rising && steps == 30000 && last == 299990, "versions: the order rises with every version from 0.0.0 to 2.99.99 (" + steps + " versions, the last " + last + ")");
        Check(VersionRules.OrderOf("0.100.0") == -1 && VersionRules.OrderOf("0.63.100") == -1 && VersionRules.KeyOf("0.100.0") == null,
            "versions: a MINOR or PATCH of 100 is no version, for it would take the order of the next version up");
        Check(VersionRules.OrderOf(null) == -1 && VersionRules.OrderOf(string.Empty) == -1 && VersionRules.OrderOf("1.0") == -1 && VersionRules.OrderOf("1.0.0.0") == -1
              && VersionRules.OrderOf("a.b.c") == -1 && VersionRules.OrderOf("-1.0.0") == -1 && VersionRules.OrderOf("1.0.0-beta.1") == -1 && VersionRules.KeyOf("1.0") == null,
            "versions: anything that is not three plain numbers has no order and no key");
    }

    private static readonly Regex VersionRow = new Regex("new Entry\\((\\w+), \"([a-z0-9_]+)\", \"([a-z0-9_]+)\"\\)");
    private static readonly Regex VersionRowKey = new Regex(@"^reallife_changes_(\d{3})_(\d+)$");
    private static readonly Regex VersionAnyRowKey = new Regex(@"^reallife_changes_\d{3}(_\d+)?$");

    // The version a row's text key spells: the first release's key was written before the patch part was (0.1.0 is "001").
    private static string VersionOfRow(string textKey)
    {
        if (textKey == "reallife_changes_001")
        {
            return "0.1.0";
        }
        Match key = VersionRowKey.Match(textKey);
        if (!key.Success)
        {
            return null;
        }
        int number = int.Parse(key.Groups[1].Value, CultureInfo.InvariantCulture);
        return (number / 100).ToString(CultureInfo.InvariantCulture) + "." + (number % 100).ToString(CultureInfo.InvariantCulture) + "." + key.Groups[2].Value;
    }

    private static void VersionTableChecks()
    {
        string changelog = ModSource(@"Meta\Changelog.cs");
        Dictionary<string, string> en = LoadWords(System.IO.Path.Combine(DesignLocales, "en.json"));
        Match latestFound = Regex.Match(changelog, "public const string Latest = \"([^\"]+)\";");
        Match unnumberedFound = Regex.Match(changelog, @"public const int Unnumbered = (\d+);");
        MatchCollection rows = VersionRow.Matches(changelog);
        Check(latestFound.Success && unnumberedFound.Success && rows.Count > 190 && en != null, "versions: Changelog.cs's Latest, Unnumbered and table and en.json are read (" + rows.Count + " rows)");
        if (!latestFound.Success || !unnumberedFound.Success || rows.Count < 2 || en == null)
        {
            return;
        }
        string latest = latestFound.Groups[1].Value;
        int unnumbered = int.Parse(unnumberedFound.Groups[1].Value, CultureInfo.InvariantCulture);
        Check(rows[0].Groups[1].Value == "Unnumbered" && rows[0].Groups[2].Value == "reallife_changes_next_title" && rows[0].Groups[3].Value == "reallife_changes_next",
            "versions: the table's first row is the unnumbered one, with the next row's two texts");

        var faults = new List<string>();
        var inTable = new HashSet<string>(StringComparer.Ordinal);
        int before = int.MaxValue, beforeDay = int.MaxValue;
        for (int i = 1; i < rows.Count; i++)
        {
            string orderText = rows[i].Groups[1].Value, titleKey = rows[i].Groups[2].Value, textKey = rows[i].Groups[3].Value;
            string version = VersionOfRow(textKey);
            inTable.Add(textKey);
            if (version == null || !int.TryParse(orderText, NumberStyles.None, CultureInfo.InvariantCulture, out int order))
            {
                faults.Add(textKey + " is no version's row");
                continue;
            }
            if (order != VersionRules.OrderOf(version))
            {
                faults.Add(version + " has order " + order + ", not " + VersionRules.OrderOf(version));
            }
            if (textKey != "reallife_changes_001" && textKey != VersionRules.KeyOf(version))
            {
                faults.Add(version + " has the key " + textKey + ", not " + VersionRules.KeyOf(version));
            }
            if (titleKey != textKey + "_title")
            {
                faults.Add(version + "'s title key is " + titleKey);
            }
            if (!en.TryGetValue(titleKey, out string title) || title != version)
            {
                faults.Add(version + "'s title in en.json reads " + (title ?? "nothing"));
            }
            if (!en.ContainsKey(textKey))
            {
                faults.Add(version + " has no text in en.json");
            }
            if (order >= before)
            {
                faults.Add(version + " (" + order + ") stands under an older row (" + before + ")");
            }
            before = order;
            // A day as one number, so two can be compared: a row lower in the table is older and was never released later.
            if (ReleaseDates.Try(order, out int year, out int month, out int day))
            {
                int stamp = year * 10000 + month * 100 + day;
                if (stamp > beforeDay)
                {
                    faults.Add(version + " is dated after the version above it");
                }
                beforeDay = stamp;
            }
            else
            {
                faults.Add(version + " has no day in ReleaseDates.cs");
            }
        }
        Check(faults.Count == 0, "versions: every row of the table has its version's order, key, title and day, newest first"
              + (faults.Count > 0 ? " (" + faults.Count + ": " + string.Join("; ", faults.GetRange(0, Math.Min(4, faults.Count))) + ")" : string.Empty));

        var stray = new List<string>();
        foreach (KeyValuePair<string, string> text in en)
        {
            if (VersionAnyRowKey.IsMatch(text.Key) && !inTable.Contains(text.Key))
            {
                stray.Add(text.Key);
            }
        }
        Check(stray.Count == 0, "versions: every numbered What's new row of en.json is a row of the table" + (stray.Count > 0 ? " (not: " + string.Join(", ", stray.GetRange(0, Math.Min(6, stray.Count))) + ")" : string.Empty));

        string newest = VersionOfRow(rows[1].Groups[3].Value);
        Check(newest == latest && VersionRules.OrderOf(latest) > 0, "versions: Latest (" + latest + ") is the version of the table's newest numbered row (" + newest + ")");
        Check(unnumbered == VersionRules.OrderOf(latest) + 5, "versions: the unnumbered row's order (" + unnumbered + ") is Latest's (" + VersionRules.OrderOf(latest) + ") and five");

        // The seven places the version is written (docs/standards/git-and-review.md, RLO-GIT-009) and the change note: the version script writes them
        // all, the build holds the first three, and here every one is held to Latest.
        string root = RepoRoot();
        var places = new[]
        {
            new KeyValuePair<string, string>(@"RealLifeOverhaul.Build\RealLifeOverhaul.csproj", "<Version>" + latest + "</Version>"),
            new KeyValuePair<string, string>(@"Assets\Mods\RealLifeOverhaul\ModManifest.asset", "  Version: " + latest + "\n"),
            new KeyValuePair<string, string>("README.md", latest + " is the current one."),
            new KeyValuePair<string, string>("INSTALL.md", "RealLifeOverhaul-" + latest + "-setup.exe"),
            new KeyValuePair<string, string>(@"RealLifeOverhaul.Build\Installer\RealLifeOverhaul.iss", "ISCC.exe /DAppVersion=" + latest + " "),
            new KeyValuePair<string, string>(@"RealLifeOverhaul.Build\Workshop\description.txt", "the mod version (" + latest + ")")
        };
        var apart = new List<string>();
        foreach (KeyValuePair<string, string> place in places)
        {
            string path = System.IO.Path.Combine(root, place.Key);
            if (!System.IO.File.Exists(path) || !System.IO.File.ReadAllText(path).Replace("\r\n", "\n").Contains(place.Value))
            {
                apart.Add(place.Key);
            }
        }
        string note = System.IO.Path.Combine(root, @"RealLifeOverhaul.Build\Workshop\changenote.txt");
        if (!System.IO.File.Exists(note) || System.IO.File.ReadAllText(note).TrimStart('﻿').Replace("\r\n", "\n").Split('\n')[0].Trim() != latest)
        {
            apart.Add(@"RealLifeOverhaul.Build\Workshop\changenote.txt");
        }
        Check(apart.Count == 0, "versions: the csproj, the manifest, the README, INSTALL.md, the installer script, the Workshop description and the change note all say " + latest
              + (apart.Count > 0 ? " (not: " + string.Join(", ", apart) + ")" : string.Empty));
    }
}
