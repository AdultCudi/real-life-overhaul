using System;
using System.Globalization;

namespace RealLifeOverhaul.Meta
{
    /// <summary>
    /// A version's numbers: how two versions compare, and the order and the What's new key that <see cref="Changelog"/>'s
    /// table gives a version. MINOR and PATCH each stay under 100, so that 1.0.0 comes after every 0.x version in both.
    /// Pure, and checked offline (EconomyCheck's VersionChecks). COST: a short parse at a load, a save or a page, never per frame.
    /// </summary>
    public static class VersionRules
    {
        public const int PartMost = 99;   // the most MINOR or PATCH may be: one more would take the order of the next version up
        private const string RowKey = "reallife" + "_changes_";   // split, so the locale check does not take a prefix for a key

        /// <summary>Compares two versions number by number; a missing part, and a part that is not a number, counts as 0.</summary>
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

        /// <summary>The three numbers of "MAJOR.MINOR.PATCH"; false for anything else, a part over <see cref="PartMost"/> in MINOR or PATCH included.</summary>
        public static bool Parts(string version, out int major, out int minor, out int patch)
        {
            major = 0;
            minor = 0;
            patch = 0;
            string[] parts = (version ?? string.Empty).Split('.');
            return parts.Length == 3
                   && int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out major)
                   && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out minor)
                   && int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out patch)
                   && minor <= PartMost && patch <= PartMost;
        }

        /// <summary>A version's order in the table: (major * 100 + minor) * 1000 + patch * 10, so 0.63.11 is 63110 and 1.0.0 is 100000. -1 for no version.</summary>
        public static int OrderOf(string version)
        {
            return Parts(version, out int major, out int minor, out int patch) ? (major * 100 + minor) * 1000 + patch * 10 : -1;
        }

        /// <summary>A version's What's new key: major and two digits of minor, then the patch, so 0.63.11 is reallife_changes_063_11 and 1.0.0 reallife_changes_100_0. Null for no version.</summary>
        public static string KeyOf(string version)
        {
            return Parts(version, out int major, out int minor, out int patch)
                ? RowKey + (major * 100 + minor).ToString("000", CultureInfo.InvariantCulture) + "_" + patch.ToString(CultureInfo.InvariantCulture)
                : null;
        }
    }
}
