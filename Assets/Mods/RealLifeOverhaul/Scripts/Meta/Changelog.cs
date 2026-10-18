using System;
using System.Globalization;
using System.Text;
using Localizor;

namespace RealLifeOverhaul.Meta
{
    /// <summary>
    /// What changed in each released version, newest first, for the Guide's "What's new" page. A version is MAJOR.MINOR.PATCH:
    /// a feature raises MINOR, a fix alone PATCH, and MAJOR is for a change that old saves cannot follow.
    ///
    /// A new version is four edits, and the build fails if the first three disagree (the csproj's CheckVersion target):
    /// &lt;Version&gt; in RealLifeOverhaul.csproj, <see cref="Latest"/>, Version in ModManifest.asset, and a row at the top of
    /// <see cref="All"/> with its two texts in en.json (reallife_changes_ with major and two digits of minor, then the patch:
    /// 0.2.0 is "002_0", 0.5.12 is "005_12"; the same with _title). Work merged before the number is set goes in the row of
    /// order <see cref="Unnumbered"/>.
    /// </summary>
    public static class Changelog
    {
        /// <summary>The version this table was last brought up to; used when the assembly carries no version of its own.</summary>
        public const string Latest = "0.39.1";

        /// <summary>Order of a row for work merged after the last numbered version: above the newest numbered row, below the next one.</summary>
        public const int Unnumbered = 39015;   // Latest's order + 5: it moves up with Latest

        public sealed class Entry
        {
            public readonly int Order;          // (major * 100 + minor) * 1000 + patch * 10: 0.1.1 is 1010, 0.5.12 is 5120, 1.0.0 is 100000
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
            new Entry(39010, "reallife_changes_039_1_title", "reallife_changes_039_1"),
            new Entry(39000, "reallife_changes_039_0_title", "reallife_changes_039_0"),
            new Entry(38010, "reallife_changes_038_1_title", "reallife_changes_038_1"),
            new Entry(38000, "reallife_changes_038_0_title", "reallife_changes_038_0"),
            new Entry(37010, "reallife_changes_037_1_title", "reallife_changes_037_1"),
            new Entry(37000, "reallife_changes_037_0_title", "reallife_changes_037_0"),
            new Entry(36010, "reallife_changes_036_1_title", "reallife_changes_036_1"),
            new Entry(36000, "reallife_changes_036_0_title", "reallife_changes_036_0"),
            new Entry(35000, "reallife_changes_035_0_title", "reallife_changes_035_0"),
            new Entry(34000, "reallife_changes_034_0_title", "reallife_changes_034_0"),
            new Entry(33010, "reallife_changes_033_1_title", "reallife_changes_033_1"),
            new Entry(33000, "reallife_changes_033_0_title", "reallife_changes_033_0"),
            new Entry(32050, "reallife_changes_032_5_title", "reallife_changes_032_5"),
            new Entry(32040, "reallife_changes_032_4_title", "reallife_changes_032_4"),
            new Entry(32030, "reallife_changes_032_3_title", "reallife_changes_032_3"),
            new Entry(32020, "reallife_changes_032_2_title", "reallife_changes_032_2"),
            new Entry(32010, "reallife_changes_032_1_title", "reallife_changes_032_1"),
            new Entry(32000, "reallife_changes_032_0_title", "reallife_changes_032_0"),
            new Entry(31000, "reallife_changes_031_0_title", "reallife_changes_031_0"),
            new Entry(30020, "reallife_changes_030_2_title", "reallife_changes_030_2"),
            new Entry(30010, "reallife_changes_030_1_title", "reallife_changes_030_1"),
            new Entry(30000, "reallife_changes_030_0_title", "reallife_changes_030_0"),
            new Entry(29030, "reallife_changes_029_3_title", "reallife_changes_029_3"),
            new Entry(29020, "reallife_changes_029_2_title", "reallife_changes_029_2"),
            new Entry(29010, "reallife_changes_029_1_title", "reallife_changes_029_1"),
            new Entry(29000, "reallife_changes_029_0_title", "reallife_changes_029_0"),
            new Entry(28040, "reallife_changes_028_4_title", "reallife_changes_028_4"),
            new Entry(28030, "reallife_changes_028_3_title", "reallife_changes_028_3"),
            new Entry(28020, "reallife_changes_028_2_title", "reallife_changes_028_2"),
            new Entry(28010, "reallife_changes_028_1_title", "reallife_changes_028_1"),
            new Entry(28000, "reallife_changes_028_0_title", "reallife_changes_028_0"),
            new Entry(27070, "reallife_changes_027_7_title", "reallife_changes_027_7"),
            new Entry(27060, "reallife_changes_027_6_title", "reallife_changes_027_6"),
            new Entry(27050, "reallife_changes_027_5_title", "reallife_changes_027_5"),
            new Entry(27040, "reallife_changes_027_4_title", "reallife_changes_027_4"),
            new Entry(27030, "reallife_changes_027_3_title", "reallife_changes_027_3"),
            new Entry(27020, "reallife_changes_027_2_title", "reallife_changes_027_2"),
            new Entry(27010, "reallife_changes_027_1_title", "reallife_changes_027_1"),
            new Entry(27000, "reallife_changes_027_0_title", "reallife_changes_027_0"),
            new Entry(26000, "reallife_changes_026_0_title", "reallife_changes_026_0"),
            new Entry(25020, "reallife_changes_025_2_title", "reallife_changes_025_2"),
            new Entry(25010, "reallife_changes_025_1_title", "reallife_changes_025_1"),
            new Entry(25000, "reallife_changes_025_0_title", "reallife_changes_025_0"),
            new Entry(24000, "reallife_changes_024_0_title", "reallife_changes_024_0"),
            new Entry(23000, "reallife_changes_023_0_title", "reallife_changes_023_0"),
            new Entry(22000, "reallife_changes_022_0_title", "reallife_changes_022_0"),
            new Entry(21010, "reallife_changes_021_1_title", "reallife_changes_021_1"),
            new Entry(21000, "reallife_changes_021_0_title", "reallife_changes_021_0"),
            new Entry(20000, "reallife_changes_020_0_title", "reallife_changes_020_0"),
            new Entry(19020, "reallife_changes_019_2_title", "reallife_changes_019_2"),
            new Entry(19010, "reallife_changes_019_1_title", "reallife_changes_019_1"),
            new Entry(19000, "reallife_changes_019_0_title", "reallife_changes_019_0"),
            new Entry(18000, "reallife_changes_018_0_title", "reallife_changes_018_0"),
            new Entry(17020, "reallife_changes_017_2_title", "reallife_changes_017_2"),
            new Entry(17010, "reallife_changes_017_1_title", "reallife_changes_017_1"),
            new Entry(17000, "reallife_changes_017_0_title", "reallife_changes_017_0"),
            new Entry(16000, "reallife_changes_016_0_title", "reallife_changes_016_0"),
            new Entry(15030, "reallife_changes_015_3_title", "reallife_changes_015_3"),
            new Entry(15020, "reallife_changes_015_2_title", "reallife_changes_015_2"),
            new Entry(15010, "reallife_changes_015_1_title", "reallife_changes_015_1"),
            new Entry(15000, "reallife_changes_015_0_title", "reallife_changes_015_0"),
            new Entry(14000, "reallife_changes_014_0_title", "reallife_changes_014_0"),
            new Entry(13000, "reallife_changes_013_0_title", "reallife_changes_013_0"),
            new Entry(12040, "reallife_changes_012_4_title", "reallife_changes_012_4"),
            new Entry(12030, "reallife_changes_012_3_title", "reallife_changes_012_3"),
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
    }
}
