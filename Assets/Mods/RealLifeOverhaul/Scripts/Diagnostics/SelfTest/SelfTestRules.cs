using System;
using System.IO;
using System.Text;

namespace RealLifeOverhaul.SelfTest
{
    /// <summary>What a step came to.</summary>
    public enum SelfTestOutcome : byte
    {
        Pass = 0,
        Fail,
        Skipped
    }

    /// <summary>
    /// The self-test's small game-free rules: where its trigger file is and whether it counts, the names it writes files
    /// under, and whose a log line is. The self-test is a developer diagnostic (like CharacterLab and PerfProbe): wholly
    /// inert unless the trigger file exists, one File.Exists when the game starts and never per frame. Checked offline
    /// (EconomyCheck --selftest). COST: a few string operations.
    /// </summary>
    public static class SelfTestRules
    {
        public const string Folder = "RealLifeOverhaul";    // the mod's folder in the game's data folder
        public const string FileName = "selftest.json";
        public const int StaleMinutes = 180;                // a trigger file older than this is a leftover: deleted unread, so a player's session never meets one

        /// <summary>The trigger file's place in the game's data folder (Application.persistentDataPath).</summary>
        public static string TriggerPath(string dataFolder)
        {
            return string.IsNullOrEmpty(dataFolder) ? null : Path.Combine(dataFolder, Folder, FileName);
        }

        public enum Verdict : byte
        {
            Absent = 0,   // no file: the mod does nothing at all
            Stale,        // a file left behind long ago: taken away unread
            Run           // a fresh file: its plan is read
        }

        /// <summary>
        /// Whether a plan is to run: one existence test, and the file's age only when it exists. Nothing else is read or
        /// made without a file. <paramref name="path"/> is the file's place when the answer is not Absent.
        /// </summary>
        public static Verdict Look(string dataFolder, Func<string, bool> exists, Func<string, DateTime> written, DateTime now, out string path)
        {
            path = TriggerPath(dataFolder);
            if (path == null || !exists(path))
            {
                path = null;
                return Verdict.Absent;
            }
            return now - written(path) > TimeSpan.FromMinutes(StaleMinutes) ? Verdict.Stale : Verdict.Run;
        }

        /// <summary>A name made safe for a file: letters, digits, dash and underscore; at most 40 characters; the fallback when nothing is left.</summary>
        public static string SafeName(string text, string fallback)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return fallback;
            }
            var sb = new StringBuilder(text.Length);
            for (int i = 0; i < text.Length && sb.Length < 40; i++)
            {
                char c = text[i];
                sb.Append(c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z' || c >= '0' && c <= '9' || c == '-' || c == '_' ? c : '_');
            }
            return sb.ToString();
        }

        /// <summary>The output folder: a plan's absolute folder as it is, a relative one under the self-test's own folder of the data folder.</summary>
        public static string OutputFolder(string dataFolder, string output)
        {
            if (string.IsNullOrWhiteSpace(output))
            {
                return null;
            }
            return Path.IsPathRooted(output) ? output : Path.Combine(dataFolder, Folder, "selftest", output);
        }

        public const string OwnerRlo = "RLO";
        public const string OwnerOther = "the game or another mod";

        /// <summary>Whose a log line is, by its words and its stack: the mod's own namespace and name mark it as RLO's.</summary>
        public static string Owner(string message, string stack)
        {
            return Has(message) || Has(stack) ? OwnerRlo : OwnerOther;
        }

        private static bool Has(string text)
        {
            return text != null && (text.IndexOf("RealLifeOverhaul", StringComparison.Ordinal) >= 0 || text.IndexOf("Real Life Overhaul", StringComparison.Ordinal) >= 0);
        }

        /// <summary>The words of a span as the game's time machine reads them ("2d5h30m"): true when it is made of whole numbers and d, h, m.</summary>
        public static bool SpanOk(string span)
        {
            if (string.IsNullOrEmpty(span) || span.Length > 24)
            {
                return false;
            }
            bool digits = false;
            for (int i = 0; i < span.Length; i++)
            {
                char c = span[i];
                if (c >= '0' && c <= '9')
                {
                    digits = true;
                }
                else if ((c == 'd' || c == 'h' || c == 'm' || c == 'w') && digits)
                {
                    digits = false;
                }
                else
                {
                    return false;
                }
            }
            return !digits;
        }

        /// <summary>The last line of a finished run, as Player.log gets it.</summary>
        public static string DoneLine(int passed, int failed)
        {
            return "SELFTEST DONE passed " + passed + " failed " + failed;
        }
    }
}
