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
    /// A NEW VERSION is four edits, and the build fails if the first three disagree (the CheckVersion target of
    /// the csproj): &lt;Version&gt; in RealLifeOverhaul.Build/RealLifeOverhaul.csproj (the assembly's version, which
    /// is what the running mod reports), <see cref="Latest"/> below, Version in ModManifest.asset, and one row at
    /// the top of <see cref="All"/> with its two texts at the end of en.json (reallife_changes_ and three digits,
    /// major then two of minor, and the same with _title: 0.2.0 is "002", order 2000; a third number is added after
    /// an underscore: 0.1.1 is "001_1", order 1010). Work merged before it has a
    /// number goes in a row of order <see cref="Unnumbered"/>, which takes the new number when the version is set.
    /// </summary>
    public static class Changelog
    {
        /// <summary>The version this table was last brought up to; used when the assembly carries no version of its own.</summary>
        public const string Latest = "0.3.5";

        /// <summary>Order of a row for work merged after the last numbered version: above the newest numbered row, below the next one.</summary>
        public const int Unnumbered = 3055;   // OrderOf(Latest) + 5: it moves up with Latest

        public sealed class Entry
        {
            public readonly int Order;          // OrderOf its version: 0.1.0 is 1000, 0.1.1 is 1010, 0.12.0 is 12000, 1.0.0 is 100000
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

        /// <summary>"0.1.0" is 1000, "0.1.1" is 1010, "0.12.3" is 12030, "1.0.0" is 100000 (a third number above 9 counts as 9); anything that is not a version is 0.</summary>
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
            return (major * 100 + minor) * 100 + Math.Min(9, Math.Max(0, patch)) * 10;
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
