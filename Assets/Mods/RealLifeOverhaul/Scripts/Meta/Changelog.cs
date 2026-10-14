using System;
using System.Globalization;
using System.Text;
using Localizor;

namespace RealLifeOverhaul.Meta
{
    /// <summary>
    /// What changed in each version, for the guide's "What's new" page and the welcome panel's "since last time"
    /// line. One row per released version, newest first. 0.1.0 is the first public release; the numbers the mod
    /// carried while it was being developed (0.13 to 0.46) were never released and are not listed.
    ///
    /// Numbers are MAJOR.MINOR.PATCH: a new feature raises MINOR (0.6.0), a fix alone raises PATCH (0.5.1, up to 0.5.99),
    /// and MAJOR is for a change that old saves cannot follow.
    ///
    /// A NEW VERSION is four edits, and the build fails if the first three disagree (the CheckVersion target of
    /// the csproj): &lt;Version&gt; in RealLifeOverhaul.Build/RealLifeOverhaul.csproj (the assembly's version, which
    /// is what the running mod reports), <see cref="Latest"/> below, Version in ModManifest.asset, and one row at
    /// the top of <see cref="All"/> with its two texts at the end of en.json (reallife_changes_ and three digits,
    /// major then two of minor, and the same with _title: 0.2.0 is "002", order 2000; a third number is added after
    /// an underscore: 0.1.1 is "001_1", order 1010; 0.5.12 is "005_12", order 5120). Work merged before it has a
    /// number goes in a row of order <see cref="Unnumbered"/>, which takes the new number when the version is set.
    /// </summary>
    public static class Changelog
    {
        /// <summary>The version this table was last brought up to; used when the assembly carries no version of its own.</summary>
        public const string Latest = "0.12.2";

        /// <summary>Order of a row for work merged after the last numbered version: above the newest numbered row, below the next one.</summary>
        public const int Unnumbered = 12025;   // OrderOf(Latest) + 5: it moves up with Latest

        public sealed class Entry
        {
            public readonly int Order;          // OrderOf its version: 0.1.0 is 1000, 0.1.1 is 1010, 0.5.12 is 5120, 0.12.0 is 12000, 1.0.0 is 100000
            public readonly string TitleKey;
            public readonly string TextKey;

            public Entry(int order, string titleKey, string textKey)
            {
                Order = order;
                TitleKey = titleKey;
                TextKey = textKey;
            }
        }

        public static readonly Entry[] All =
        {
            new Entry(Unnumbered, "reallife_changes_next_title", "reallife_changes_next"),
            new Entry(12020, "reallife_changes_012_2_title", "reallife_changes_012_2"),
            new Entry(12010, "reallife_changes_012_1_title", "reallife_changes_012_1"),
            new Entry(12000, "reallife_changes_012_0_title", "reallife_changes_012_0"),
            new Entry(11040, "reallife_changes_011_4_title", "reallife_changes_011_4"),
            new Entry(11030, "reallife_changes_011_3_title", "reallife_changes_011_3"),
            new Entry(11020, "reallife_changes_011_2_title", "reallife_changes_011_2"),
            new Entry(11010, "reallife_changes_011_1_title", "reallife_changes_011_1"),
            new Entry(11000, "reallife_changes_011_0_title", "reallife_changes_011_0"),
            new Entry(10040, "reallife_changes_010_4_title", "reallife_changes_010_4"),
            new Entry(10030, "reallife_changes_010_3_title", "reallife_changes_010_3"),
            new Entry(10020, "reallife_changes_010_2_title", "reallife_changes_010_2"),
            new Entry(10010, "reallife_changes_010_1_title", "reallife_changes_010_1"),
            new Entry(10000, "reallife_changes_010_0_title", "reallife_changes_010_0"),
            new Entry(9140, "reallife_changes_009_14_title", "reallife_changes_009_14"),
            new Entry(9130, "reallife_changes_009_13_title", "reallife_changes_009_13"),
            new Entry(9120, "reallife_changes_009_12_title", "reallife_changes_009_12"),
            new Entry(9110, "reallife_changes_009_11_title", "reallife_changes_009_11"),
            new Entry(9100, "reallife_changes_009_10_title", "reallife_changes_009_10"),
            new Entry(9090, "reallife_changes_009_9_title", "reallife_changes_009_9"),
            new Entry(9080, "reallife_changes_009_8_title", "reallife_changes_009_8"),
            new Entry(9070, "reallife_changes_009_7_title", "reallife_changes_009_7"),
            new Entry(9060, "reallife_changes_009_6_title", "reallife_changes_009_6"),
            new Entry(9050, "reallife_changes_009_5_title", "reallife_changes_009_5"),
            new Entry(9040, "reallife_changes_009_4_title", "reallife_changes_009_4"),
            new Entry(9030, "reallife_changes_009_3_title", "reallife_changes_009_3"),
            new Entry(9020, "reallife_changes_009_2_title", "reallife_changes_009_2"),
            new Entry(9010, "reallife_changes_009_1_title", "reallife_changes_009_1"),
            new Entry(9000, "reallife_changes_009_0_title", "reallife_changes_009_0"),
            new Entry(8020, "reallife_changes_008_2_title", "reallife_changes_008_2"),
            new Entry(8010, "reallife_changes_008_1_title", "reallife_changes_008_1"),
            new Entry(8000, "reallife_changes_008_0_title", "reallife_changes_008_0"),
            new Entry(7080, "reallife_changes_007_8_title", "reallife_changes_007_8"),
            new Entry(7070, "reallife_changes_007_7_title", "reallife_changes_007_7"),
            new Entry(7060, "reallife_changes_007_6_title", "reallife_changes_007_6"),
            new Entry(7050, "reallife_changes_007_5_title", "reallife_changes_007_5"),
            new Entry(7040, "reallife_changes_007_4_title", "reallife_changes_007_4"),
            new Entry(7030, "reallife_changes_007_3_title", "reallife_changes_007_3"),
            new Entry(7020, "reallife_changes_007_2_title", "reallife_changes_007_2"),
            new Entry(7010, "reallife_changes_007_1_title", "reallife_changes_007_1"),
            new Entry(7000, "reallife_changes_007_0_title", "reallife_changes_007_0"),
            new Entry(6020, "reallife_changes_006_2_title", "reallife_changes_006_2"),
            new Entry(6010, "reallife_changes_006_1_title", "reallife_changes_006_1"),
            new Entry(6000, "reallife_changes_006_0_title", "reallife_changes_006_0"),
            new Entry(5000, "reallife_changes_005_0_title", "reallife_changes_005_0"),
            new Entry(4090, "reallife_changes_004_9_title", "reallife_changes_004_9"),
            new Entry(4080, "reallife_changes_004_8_title", "reallife_changes_004_8"),
            new Entry(4070, "reallife_changes_004_7_title", "reallife_changes_004_7"),
            new Entry(4060, "reallife_changes_004_6_title", "reallife_changes_004_6"),
            new Entry(4050, "reallife_changes_004_5_title", "reallife_changes_004_5"),
            new Entry(4040, "reallife_changes_004_4_title", "reallife_changes_004_4"),
            new Entry(4030, "reallife_changes_004_3_title", "reallife_changes_004_3"),
            new Entry(4020, "reallife_changes_004_2_title", "reallife_changes_004_2"),
            new Entry(4010, "reallife_changes_004_1_title", "reallife_changes_004_1"),
            new Entry(4000, "reallife_changes_004_0_title", "reallife_changes_004_0"),
            new Entry(3090, "reallife_changes_003_9_title", "reallife_changes_003_9"),
            new Entry(3080, "reallife_changes_003_8_title", "reallife_changes_003_8"),
            new Entry(3070, "reallife_changes_003_7_title", "reallife_changes_003_7"),
            new Entry(3060, "reallife_changes_003_6_title", "reallife_changes_003_6"),
            new Entry(3050, "reallife_changes_003_5_title", "reallife_changes_003_5"),
            new Entry(3040, "reallife_changes_003_4_title", "reallife_changes_003_4"),
            new Entry(3030, "reallife_changes_003_3_title", "reallife_changes_003_3"),
            new Entry(3020, "reallife_changes_003_2_title", "reallife_changes_003_2"),
            new Entry(3010, "reallife_changes_003_1_title", "reallife_changes_003_1"),
            new Entry(3000, "reallife_changes_003_0_title", "reallife_changes_003_0"),
            new Entry(2040, "reallife_changes_002_4_title", "reallife_changes_002_4"),
            new Entry(2030, "reallife_changes_002_3_title", "reallife_changes_002_3"),
            new Entry(2020, "reallife_changes_002_2_title", "reallife_changes_002_2"),
            new Entry(2010, "reallife_changes_002_1_title", "reallife_changes_002_1"),
            new Entry(2000, "reallife_changes_002_0_title", "reallife_changes_002_0"),
            new Entry(1060, "reallife_changes_001_6_title", "reallife_changes_001_6"),
            new Entry(1050, "reallife_changes_001_5_title", "reallife_changes_001_5"),
            new Entry(1040, "reallife_changes_001_4_title", "reallife_changes_001_4"),
            new Entry(1030, "reallife_changes_001_3_title", "reallife_changes_001_3"),
            new Entry(1020, "reallife_changes_001_2_title", "reallife_changes_001_2"),
            new Entry(1010, "reallife_changes_001_1_title", "reallife_changes_001_1"),
            new Entry(1000, "reallife_changes_001_title", "reallife_changes_001")
        };

        private static string _version;

        /// <summary>The running mod's version: the assembly's when it was built with one (the dotnet build), otherwise <see cref="Latest"/>.</summary>
        public static string Version
        {
            get
            {
                if (_version == null)
                {
                    _version = Latest;
                    try
                    {
                        Version built = typeof(Changelog).Assembly.GetName().Version;
                        if (built != null && (built.Major > 0 || built.Minor > 0))
                        {
                            _version = built.Major + "." + built.Minor + "." + Math.Max(0, built.Build);
                        }
                    }
                    catch (Exception)
                    {
                        // The table's own number stands.
                    }
                }
                return _version;
            }
        }

        /// <summary>"0.1.0" is 1000, "0.1.1" is 1010, "0.5.12" is 5120, "0.12.3" is 12030, "1.0.0" is 100000 (a third number above 99 counts as 99); anything that is not a version is 0.</summary>
        public static int OrderOf(string version)
        {
            if (string.IsNullOrEmpty(version))
            {
                return 0;
            }
            string[] parts = version.Split('.');
            if (parts.Length < 2 || !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int major)
                || !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int minor))
            {
                return 0;
            }
            int patch = 0;
            if (parts.Length > 2)
            {
                int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out patch);
            }
            return (major * 100 + minor) * 1000 + Math.Min(99, Math.Max(0, patch)) * 10;
        }

        /// <summary>Compares two versions number by number; a missing part counts as 0.</summary>
        public static int Compare(string a, string b)
        {
            string[] left = (a ?? string.Empty).Split('.');
            string[] right = (b ?? string.Empty).Split('.');
            for (int i = 0; i < Math.Max(left.Length, right.Length); i++)
            {
                int.TryParse(i < left.Length ? left[i] : "0", NumberStyles.Integer, CultureInfo.InvariantCulture, out int x);
                int.TryParse(i < right.Length ? right[i] : "0", NumberStyles.Integer, CultureInfo.InvariantCulture, out int y);
                if (x != y)
                {
                    return x < y ? -1 : 1;
                }
            }
            return 0;
        }

        /// <summary>The whole table as the guide prints it.</summary>
        public static string Page()
        {
            var text = new StringBuilder();
            foreach (Entry entry in All)
            {
                text.Append("# ").Append(entry.TitleKey.GetLocalization()).Append('\n').Append(entry.TextKey.GetLocalization()).Append("\n\n");
            }
            return text.ToString();
        }

        /// <summary>What came after the version the player last saw, newest first; empty if nothing did.</summary>
        public static string Since(string seenVersion)
        {
            int seen = OrderOf(seenVersion);
            int now = OrderOf(Version);
            var text = new StringBuilder();
            foreach (Entry entry in All)
            {
                if (entry.Order > seen && entry.Order <= Math.Max(now, seen))
                {
                    text.Append("<b>").Append(entry.TitleKey.GetLocalization()).Append("</b>: ").Append(entry.TextKey.GetLocalization()).Append('\n');
                }
            }
            return text.ToString();
        }
    }
}
