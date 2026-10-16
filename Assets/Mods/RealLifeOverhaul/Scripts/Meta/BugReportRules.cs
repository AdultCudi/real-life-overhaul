using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace RealLifeOverhaul.Meta
{
    /// <summary>What kind of line of the game's log the mod kept for a bug report.</summary>
    public enum BugLineKind
    {
        Warning,
        Error,
        Exception
    }

    /// <summary>One warning or error of the mod's, as the engine handed it over: its text and stack are the engine's own strings, kept, not copied.</summary>
    public struct BugLine
    {
        public BugLineKind Kind;
        public long Ticks;      // when, in UTC ticks
        public string Text;
        public string Stack;
    }

    /// <summary>
    /// The last <see cref="Most"/> warnings and errors the mod wrote to the game's log this session, for a bug report. The
    /// engine hands each line of its log to the mod from whichever thread wrote it (BugReportGame's listener); a line of the
    /// mod's is kept in a fixed ring of <see cref="Most"/> slots under a lock: no allocation, a few field writes. Read only
    /// when a report is made.
    /// </summary>
    public sealed class BugLog
    {
        public const int Most = 50;   // lines a report gives: the last ones say what went wrong just before the player pressed

        private readonly BugLine[] _lines = new BugLine[Most];
        private readonly object _lock = new object();
        private int _next;
        private int _count;
        private long _total;

        /// <summary>Every line of the mod's kept since the game started, the ones that fell out of the ring too.</summary>
        public long Total
        {
            get
            {
                lock (_lock)
                {
                    return _total;
                }
            }
        }

        public void Add(BugLineKind kind, long ticks, string text, string stack)
        {
            lock (_lock)
            {
                _lines[_next] = new BugLine { Kind = kind, Ticks = ticks, Text = text, Stack = stack };
                _next = (_next + 1) % Most;
                if (_count < Most)
                {
                    _count++;
                }
                _total++;
            }
        }

        /// <summary>The lines kept, oldest first, into the list given (emptied first); the number of lines since the game started.</summary>
        public long Copy(List<BugLine> into)
        {
            into.Clear();
            lock (_lock)
            {
                int first = (_next - _count + Most) % Most;
                for (int i = 0; i < _count; i++)
                {
                    into.Add(_lines[(first + i) % Most]);
                }
                return _total;
            }
        }
    }

    /// <summary>Everything report.txt says, gathered on the main thread when the player presses "Report a bug" (BugReportGame.Facts).</summary>
    public sealed class BugFacts
    {
        public string Words;                // what the player typed, or null
        public string Written;              // the local date and time of the press, "2026-10-15 14:03:22"
        public string ModVersion;
        public int GameBuild;               // 0: it could not be read
        public int MadeFor;
        public string Platform;
        public string Language;
        public string GoingPublic;
        public string Preset;               // "Standard", or "Custom (nearest Standard)"
        public readonly List<string> Changed = new List<string>();   // "id = value (preset value)", one per option that differs
        public bool InCity;
        public bool PhoneOpen;              // the phone's full menu was open: the screenshot shows it
        public int Day;
        public int Hour;
        public int Minute;
        public double Money;
        public readonly List<BugLine> Lines = new List<BugLine>(BugLog.Most);
        public long LinesTotal;
        public string Contract;
        public readonly List<string> Files = new List<string>(6);   // what the report's folder holds, as said at its top
        public string Profile;              // the player's Windows user folder, written as %USERPROFILE%
        public readonly List<BugStaff> Staff = new List<BugStaff>(32);    // every employee of the player's, by business: what wages and hours are, read on the press
        public string StaffNote;            // why the staff could not be read, or null
        public readonly List<BugMoney> Books = new List<BugMoney>(64);    // the game's transactions of yesterday and today, one entry each
        public string BooksNote;            // why the books could not be read, or null
    }

    /// <summary>One employee of the player's as report.txt's "Staff and wages" gives them: no name, only what their pay is made of.</summary>
    public struct BugStaff
    {
        public string Business;     // the business's name, "" for nobody's
        public string Kind;         // its business type, the game's key
        public string Skill;        // the main skill, the game's key
        public int Level;           // its value, 0 to 100
        public int WageCents;       // hundredths an hour, as the game pays it (EmployeeInstance.hourlyWage)
        public int FormulaCents;    // the game's own wage for the skill at its level, hundredths an hour (0: not known)
        public int HoursToday;      // counted so far today, paid tonight (EmployeeInstance.workedHoursToday)
        public int HoursWeek;       // counted since Monday
        public int RotaWeek;        // on the rota a week (EmployeeInstance.assignedWeeklyHours)
    }

    /// <summary>One of the game's transactions as report.txt's "Money in and out" adds it up: its day, kind, place and amount.</summary>
    public struct BugMoney
    {
        public int Day;
        public string Kind;         // the transaction's type, the game's or the mod's key
        public string Where;        // the business it was booked at, "" for the player's own
        public long Cents;          // in hundredths: positive in, negative out
        public long BalanceCents;   // the cash just after it, in hundredths
    }

    /// <summary>
    /// The game-free part of "Report a bug" (BugReport.cs): which lines of the game's log are the mod's, what report.txt says
    /// and in what order, the player's user folder taken out of everything written, the tail of a log too big to copy whole,
    /// the summary put on the clipboard, and the folder's name. No Unity or game types: EconomyCheck's BugReportChecks run it
    /// on made-up facts. Nothing here runs per frame; <see cref="Keep"/> runs once per line the engine logs and is a few
    /// comparisons with no allocation.
    /// </summary>
    public static class BugReportRules
    {
        public const string ModPrefix = "[Mod:Real Life Overhaul]";   // what the game's logger puts before every line of the mod's
        public const string OwnPrefix = "[RealLifeOverhaul]";         // what the mod puts before a line it writes to the engine itself
        public const string Namespace = "RealLifeOverhaul.";          // in the stack of an exception thrown in the mod's code
        public const string Mask = "%USERPROFILE%";
        public const string OtherUser = "<user>";       // a user folder's name the exact pass did not know (another user, an 8.3 name, Proton's home)
        public const string SteamIdMask = "<steamid>";  // a 17-digit Steam id, the player's or another's in a Going Public session
        public const string NameMask = "<name>";        // a Steam name on the lines that are known to carry one
        public const int ErrorChars = 120;         // of an error's message in the failure notification: the toast takes 240 in all
        public const int KeepReports = 5;          // reports kept in the folder, the new one with them: older ones are deleted
        public const int MostWords = 500;          // characters the player may type: a few sentences; the rest belongs in the post
        public const int SummaryWords = 900;       // characters of them on the clipboard: Discord takes 2,000 in a message
        public const int LineChars = 400;          // of one log line in report.txt: an exception's message, not a page of it
        public const long LogCap = 6L * 1024 * 1024;       // bytes of Player.log copied: its last 6 MB, some 600 KB zipped
        public const long PrevLogCap = 3L * 1024 * 1024;   // bytes of Player-prev.log copied: the session before matters less

        // Unity's LogType, by number (Error 0, Assert 1, Warning 2, Log 3, Exception 4): named here so that this file needs no engine.
        public const int TypeError = 0, TypeAssert = 1, TypeWarning = 2, TypeLog = 3, TypeException = 4;

        /// <summary>
        /// True for a line of the game's log that is a warning or an error of the mod's: one the mod wrote (either prefix), or
        /// an error or exception whose stack passes through the mod's code. Plain lines are never kept. No allocation.
        /// </summary>
        public static bool Keep(int type, string text, string stack, out BugLineKind kind)
        {
            kind = type == TypeWarning ? BugLineKind.Warning : type == TypeException ? BugLineKind.Exception : BugLineKind.Error;
            if (type == TypeLog || type < TypeError || type > TypeException)
            {
                return false;
            }
            if (text != null && (text.StartsWith(ModPrefix, StringComparison.Ordinal) || text.StartsWith(OwnPrefix, StringComparison.Ordinal)))
            {
                return true;
            }
            return type != TypeWarning && stack != null && stack.IndexOf(Namespace, StringComparison.Ordinal) >= 0;
        }

        /// <summary>One kept line as report.txt gives it: the time, the kind, its first line cut to <see cref="LineChars"/>, and for an error the first place in the mod's code.</summary>
        public static string Line(BugLine line)
        {
            var text = new StringBuilder(160);
            text.Append(new DateTime(line.Ticks, DateTimeKind.Utc).ToString("HH:mm:ss", CultureInfo.InvariantCulture)).Append(" UTC ")
                .Append(line.Kind == BugLineKind.Warning ? "warning" : line.Kind == BugLineKind.Exception ? "exception" : "error").Append(": ")
                .Append(Cut(FirstLine(line.Text), LineChars));
            if (line.Kind != BugLineKind.Warning)
            {
                string at = ModFrame(line.Stack);
                if (at != null)
                {
                    text.Append(" | at ").Append(Cut(at, LineChars));
                }
            }
            return text.ToString();
        }

        // The first line of the stack that is the mod's own code.
        private static string ModFrame(string stack)
        {
            if (string.IsNullOrEmpty(stack))
            {
                return null;
            }
            int at = stack.IndexOf(Namespace, StringComparison.Ordinal);
            if (at < 0)
            {
                return null;
            }
            int start = stack.LastIndexOf('\n', at) + 1;
            int end = stack.IndexOf('\n', at);
            return stack.Substring(start, (end < 0 ? stack.Length : end) - start).Trim();
        }

        private static string FirstLine(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }
            int end = text.IndexOf('\n');
            return (end < 0 ? text : text.Substring(0, end)).TrimEnd('\r');
        }

        private static string Cut(string text, int most)
        {
            return text.Length <= most ? text : text.Substring(0, most) + "...";
        }

        /// <summary>
        /// report.txt, in this order: what is in the report and what was not read; then what went wrong in the player's words,
        /// the mod's version, the game's build, the platform, the language, Going Public, the preset and every option changed
        /// from it, the day and time, the money (rounded), the mod's last warnings and errors, and the game contract; then, in
        /// a city, the staff and their wages (<see cref="StaffText"/>) and yesterday's and today's money in and out
        /// (<see cref="BooksText"/>), so a report on wages or a sudden loss shows where the money went. The player's user
        /// folder is taken out of all of it.
        /// </summary>
        public static string Text(BugFacts f)
        {
            var text = new StringBuilder(4096);
            text.Append("Real Life Overhaul bug report, written ").Append(f.Written).Append('\n');
            text.Append("In this report: ").Append(f.Files.Count > 0 ? string.Join(", ", f.Files) : "report.txt").Append(".\n");
            text.Append("Nothing outside the game's own folders was read. Your Windows user folder is written as ").Append(Mask)
                .Append(" here and in the copied logs, Steam ids as ").Append(SteamIdMask).Append(". The mod sent nothing: you post this yourself.\n");
            text.Append("Names, such as your Steam name, may still be in the logs: look through the zip before you post it where everyone can read it.\n\n");

            string words = f.Words != null ? f.Words.Trim() : string.Empty;
            text.Append("What went wrong: ").Append(words.Length > 0 ? words.Replace("\r", string.Empty).Replace("\n", "\n  ") : "(not said)").Append('\n');
            text.Append("Mod version: ").Append(f.ModVersion).Append('\n');
            text.Append("Game build: ").Append(f.GameBuild > 0 ? f.GameBuild.ToString(CultureInfo.InvariantCulture) : "could not be read")
                .Append(" (this version of the mod is made for Build ").Append(f.MadeFor.ToString(CultureInfo.InvariantCulture)).Append(")\n");
            text.Append("Platform: ").Append(f.Platform).Append('\n');
            text.Append("Language: ").Append(f.Language).Append('\n');
            text.Append("Going Public: ").Append(f.GoingPublic).Append('\n');
            text.Append("Preset: ").Append(f.Preset).Append(", ").Append(f.Changed.Count.ToString(CultureInfo.InvariantCulture)).Append(" option(s) changed from it\n");
            foreach (string changed in f.Changed)
            {
                text.Append("  ").Append(changed).Append('\n');
            }
            if (f.InCity)
            {
                text.Append("Day and time: day ").Append(f.Day.ToString(CultureInfo.InvariantCulture)).Append(", ")
                    .Append(f.Hour.ToString("00", CultureInfo.InvariantCulture)).Append(':').Append(f.Minute.ToString("00", CultureInfo.InvariantCulture)).Append('\n');
                text.Append("Money: about ").Append(RoundMoney(f.Money).ToString("N0", CultureInfo.InvariantCulture)).Append('\n');
                text.Append("Phone: ").Append(f.PhoneOpen ? "open (the screenshot shows it)" : "closed").Append('\n');
            }
            else
            {
                text.Append("Day and time: no save loaded\n");
                text.Append("Money: no save loaded\n");
            }
            text.Append("The mod's warnings and errors this session: ").Append(f.LinesTotal.ToString(CultureInfo.InvariantCulture));
            text.Append(f.Lines.Count == 0 ? ", none\n" : f.LinesTotal > f.Lines.Count ? ", the last " + f.Lines.Count.ToString(CultureInfo.InvariantCulture) + ":\n" : ":\n");
            foreach (BugLine line in f.Lines)
            {
                text.Append("  ").Append(Line(line)).Append('\n');
            }
            text.Append("Game contract: ").Append(f.Contract).Append('\n');
            if (f.InCity)
            {
                StaffText(f.Staff, f.StaffNote, text);
                BooksText(f.Books, f.BooksNote, f.Day, text);
            }
            return MaskUser(text.ToString(), f.Profile);
        }

        public const int MostStaff = 200;       // employees listed one by one: a large empire's payroll, not a page per person
        public const int MostKinds = 15;        // kinds of money a day listed, the largest first: the rest are added up in one line
        public const int StaffWeekDays = 7;     // days: a week on the rota, to say what a day of it costs

        /// <summary>
        /// "Staff and wages": each business with its staff, how many hours they have worked today and stand on the rota a week,
        /// and what a day of that rota costs at their wages; then each employee, by their main skill, with their wage an hour
        /// against the game's own for the skill at their level (and how many times it, where it is higher), their hours today
        /// and this week, and their hours on the rota. The staff come grouped by business (BugReportGame sorts them).
        /// </summary>
        public static void StaffText(List<BugStaff> staff, string note, StringBuilder text)
        {
            text.Append("\nStaff and wages: ");
            if (note != null)
            {
                text.Append(note).Append('\n');
                return;
            }
            text.Append(staff.Count == 0 ? "no employees\n" : staff.Count.ToString(CultureInfo.InvariantCulture) + " employee(s)\n");
            int listed = 0;
            for (int i = 0; i < staff.Count;)
            {
                int end = i;
                int today = 0, rota = 0;
                long rotaCents = 0;
                while (end < staff.Count && staff[end].Business == staff[i].Business)
                {
                    today += staff[end].HoursToday;
                    rota += staff[end].RotaWeek;
                    rotaCents += (long)staff[end].WageCents * staff[end].RotaWeek;
                    end++;
                }
                text.Append("  ").Append(string.IsNullOrEmpty(staff[i].Business) ? "(no business)" : staff[i].Business);
                if (!string.IsNullOrEmpty(staff[i].Kind))
                {
                    text.Append(" (").Append(Short(staff[i].Kind)).Append(')');
                }
                text.Append(": ").Append((end - i).ToString(CultureInfo.InvariantCulture)).Append(" staff, ").Append(today.ToString(CultureInfo.InvariantCulture))
                    .Append(" h worked today, ").Append(rota.ToString(CultureInfo.InvariantCulture)).Append(" h a week on the rota, about ")
                    .Append(Money(rotaCents / StaffWeekDays, false)).Append(" a day of it at their wages\n");
                for (int s = i; s < end; s++)
                {
                    if (listed >= MostStaff)
                    {
                        continue;
                    }
                    listed++;
                    BugStaff e = staff[s];
                    text.Append("    ").Append(Short(e.Skill)).Append(' ').Append(e.Level.ToString(CultureInfo.InvariantCulture)).Append(": ")
                        .Append(Money(e.WageCents, true)).Append(" an hour (the game's own for the skill ")
                        .Append(e.FormulaCents > 0 ? Money(e.FormulaCents, true) : "not known");
                    if (e.FormulaCents > 0 && e.WageCents > e.FormulaCents + e.FormulaCents / 20)
                    {
                        text.Append(", ").Append(((double)e.WageCents / e.FormulaCents).ToString("0.0", CultureInfo.InvariantCulture)).Append(" times it");
                    }
                    text.Append("), ").Append(e.HoursToday.ToString(CultureInfo.InvariantCulture)).Append(" h today, ").Append(e.HoursWeek.ToString(CultureInfo.InvariantCulture))
                        .Append(" h this week, ").Append(e.RotaWeek.ToString(CultureInfo.InvariantCulture)).Append(" h a week on the rota\n");
                }
                i = end;
            }
            if (listed < staff.Count)
            {
                text.Append("    and ").Append((staff.Count - listed).ToString(CultureInfo.InvariantCulture)).Append(" more\n");
            }
        }

        /// <summary>
        /// "Money in and out": for yesterday (whose night paid the wages, dated on it) and today, the cash before the day's
        /// first transaction and after its last, what came in and went out, then the kinds of money by where they were booked,
        /// the largest first (at most <see cref="MostKinds"/>, the rest in one line). The moves come in the game's order.
        /// </summary>
        public static void BooksText(List<BugMoney> moves, string note, int today, StringBuilder text)
        {
            text.Append("\nMoney in and out (the game's books): ");
            if (note != null)
            {
                text.Append(note).Append('\n');
                return;
            }
            text.Append("yesterday and today\n");
            var sums = new Dictionary<string, long>(32, StringComparer.Ordinal);
            var counts = new Dictionary<string, int>(32, StringComparer.Ordinal);
            var order = new List<string>(32);
            for (int day = today - 1; day <= today; day++)
            {
                sums.Clear();
                counts.Clear();
                order.Clear();
                long gotIn = 0, wentOut = 0, first = 0, last = 0;
                int seen = 0;
                for (int i = 0; i < moves.Count; i++)
                {
                    BugMoney m = moves[i];
                    if (m.Day != day)
                    {
                        continue;
                    }
                    if (seen++ == 0)
                    {
                        first = m.BalanceCents - m.Cents;
                    }
                    last = m.BalanceCents;
                    gotIn += Math.Max(0, m.Cents);
                    wentOut += Math.Min(0, m.Cents);
                    string key = Short(m.Kind) + (string.IsNullOrEmpty(m.Where) ? string.Empty : " at " + m.Where);
                    if (sums.TryGetValue(key, out long sum))
                    {
                        sums[key] = sum + m.Cents;
                        counts[key]++;
                    }
                    else
                    {
                        sums[key] = m.Cents;
                        counts[key] = 1;
                        order.Add(key);
                    }
                }
                text.Append("  day ").Append(day.ToString(CultureInfo.InvariantCulture)).Append(day == today ? " (today, so far)" : string.Empty).Append(": ");
                if (seen == 0)
                {
                    text.Append("nothing booked\n");
                    continue;
                }
                text.Append(seen.ToString(CultureInfo.InvariantCulture)).Append(" transaction(s), cash ").Append(Money(first, false)).Append(" to ").Append(Money(last, false))
                    .Append(", in ").Append(Money(gotIn, false)).Append(", out ").Append(Money(-wentOut, false)).Append('\n');
                order.Sort((a, b) =>
                {
                    int by = Math.Abs(sums[b]).CompareTo(Math.Abs(sums[a]));
                    return by != 0 ? by : string.CompareOrdinal(a, b);
                });
                long rest = 0;
                int restKinds = 0;
                for (int k = 0; k < order.Count; k++)
                {
                    if (k >= MostKinds)
                    {
                        rest += sums[order[k]];
                        restKinds++;
                        continue;
                    }
                    text.Append("    ").Append(order[k]).Append(": ").Append(Money(sums[order[k]], false)).Append(" (")
                        .Append(counts[order[k]].ToString(CultureInfo.InvariantCulture)).Append(")\n");
                }
                if (restKinds > 0)
                {
                    text.Append("    ").Append(restKinds.ToString(CultureInfo.InvariantCulture)).Append(" other kind(s): ").Append(Money(rest, false)).Append('\n');
                }
            }
        }

        /// <summary>A game key without its mod's prefix and its kind ("ba:transaction_wage" is "wage", "ba:skill_hrmanager" is "hrmanager").</summary>
        public static string Short(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return "?";
            }
            int colon = key.IndexOf(':');
            string name = colon >= 0 ? key.Substring(colon + 1) : key;
            int under = name.IndexOf('_');
            bool kind = name.StartsWith("transaction_", StringComparison.Ordinal) || name.StartsWith("skill_", StringComparison.Ordinal) || name.StartsWith("businesstype_", StringComparison.Ordinal);
            return kind && under < name.Length - 1 ? name.Substring(under + 1) : name;
        }

        /// <summary>Hundredths as money: with the cents (a wage an hour) or whole, in the invariant culture, a minus sign for money out.</summary>
        public static string Money(long cents, bool withCents)
        {
            return withCents ? (cents / 100m).ToString("N2", CultureInfo.InvariantCulture) : Math.Round(cents / 100m, MidpointRounding.AwayFromZero).ToString("N0", CultureInfo.InvariantCulture);
        }

        /// <summary>Money as report.txt gives it: to the nearest 100 under 10,000, to the nearest 1,000 above. Enough to tell rich from poor, nothing more.</summary>
        public static long RoundMoney(double money)
        {
            if (double.IsNaN(money) || double.IsInfinity(money))
            {
                return 0;
            }
            double step = Math.Abs(money) < 10000d ? 100d : 1000d;
            return (long)(Math.Round(money / step, MidpointRounding.AwayFromZero) * step);
        }

        /// <summary>
        /// The text with the player's user folder written as %USERPROFILE%, however it is written there: with back or forward
        /// slashes, doubled backslashes (as in JSON), in any case. Only the whole folder: "C:\Users\ann" does not take the
        /// start of "C:\Users\anna". A folder shorter than "C:\x" is left alone, and so is a text without it.
        /// </summary>
        public static string MaskUser(string text, string profile)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }
            string back = string.IsNullOrEmpty(profile) ? string.Empty : profile.Replace('/', '\\').TrimEnd('\\');
            if (back.Length >= 4)
            {
                text = MaskOne(text, back.Replace("\\", "\\\\"));
                text = MaskOne(text, back);
                text = MaskOne(text, back.Replace('\\', '/'));
                text = MaskOne(text, back.Replace("\\", "\\/"));
            }
            return MaskForms(text);
        }

        // ---------------------------------------------------------------- the forms the exact pass does not know

        private const string Sep = @"(?:\\\\|\\/|\\|/|%5[cC]|%2[fF])";   // a path's separator: a doubled backslash, JSON's backslash and slash, a backslash, a slash, or one of them percent-encoded
        private const string Name = @"((?:(?!%5[cC]|%2[fF])[^\\/""'\s:*?<>|])(?:(?!%5[cC]|%2[fF])[^\\/""'\s:*?<>|])*?)(?=" + Sep + @"|[""'\s:*?<>|,;)\]}]|\.(?:\s|$)|$)";
        private static readonly Regex UsersFolder = new Regex(@"((?:[a-zA-Z](?::|%3[aA]))" + Sep + "+(?i:users)" + Sep + "+)" + Name, RegexOptions.CultureInvariant);
        private static readonly Regex HomeFolder = new Regex(@"((?:/|\\\\|\\|%2[fF]|%5[cC])(?i:home)" + Sep + "+)" + Name, RegexOptions.CultureInvariant);
        private static readonly Regex SteamId = new Regex(@"(?<!\d)7656119\d{10}(?!\d)", RegexOptions.CultureInvariant);
        private static readonly Regex SteamQuoted = new Regex(@"(\bname\s*[=:]\s*')([^'\r\n]*)(')", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        private static readonly Regex PlayerName = new Regex(@"(player name:[ \t]*)([^\r\n]+)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        private static readonly MatchEvaluator UserFolder = m => Kept(m.Groups[2].Value) ? m.Value : m.Groups[1].Value + OtherUser;

        // The folders under Users that are nobody's.
        private static bool Kept(string name)
        {
            return name == OtherUser || string.Equals(name, "Public", StringComparison.OrdinalIgnoreCase) || string.Equals(name, "Default", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(name, "All Users", StringComparison.OrdinalIgnoreCase) || string.Equals(name, "Default User", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// After the exact pass, by rule: the folder after a drive's Users folder (any case, any separator, JSON's \/ and
        /// percent-encoded ones: another user, an 8.3 name such as ANNSMI~1), the folder after /home/ (Proton's paths), every
        /// 17-digit Steam id, and the Steam name on the lines known to carry one (Going Public's "[Steam] ... name='...'" and
        /// "Using configured player name: ..."). Each is looked for only when a cheap search finds its sign in the text.
        /// </summary>
        public static string MaskForms(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }
            if (text.IndexOf("users", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                text = UsersFolder.Replace(text, UserFolder);
            }
            if (text.IndexOf("home", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                text = HomeFolder.Replace(text, UserFolder);
            }
            if (text.IndexOf("7656119", StringComparison.Ordinal) >= 0)
            {
                text = SteamId.Replace(text, SteamIdMask);
            }
            if (text.IndexOf("[Steam]", StringComparison.Ordinal) >= 0)
            {
                text = SteamQuoted.Replace(text, "$1" + NameMask + "$3");
            }
            if (text.IndexOf("player name:", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                text = PlayerName.Replace(text, "$1" + NameMask);
            }
            return text;
        }

        private static string MaskOne(string text, string folder)
        {
            int at = text.IndexOf(folder, StringComparison.OrdinalIgnoreCase);
            if (at < 0)
            {
                return text;
            }
            var result = new StringBuilder(text.Length);
            int from = 0;
            while (at >= 0)
            {
                int end = at + folder.Length;
                if (end < text.Length && NameChar(text[end]) && !(text[end] == '.' && (end + 1 == text.Length || char.IsWhiteSpace(text[end + 1]))))
                {
                    at = text.IndexOf(folder, at + 1, StringComparison.OrdinalIgnoreCase);   // another folder that starts the same
                    continue;
                }
                result.Append(text, from, at - from).Append(Mask);
                from = end;
                at = text.IndexOf(folder, from, StringComparison.OrdinalIgnoreCase);
            }
            result.Append(text, from, text.Length - from);
            return result.ToString();
        }

        // A character that may go on a user folder's name: then what was found is the start of another folder.
        private static bool NameChar(char c)
        {
            return char.IsLetterOrDigit(c) || c == '.' || c == '_' || c == '-';
        }

        /// <summary>Where the copy of a log starts: its last <paramref name="cap"/> bytes, or all of it.</summary>
        public static long TailStart(long length, long cap)
        {
            return length > cap && cap > 0 ? length - cap : 0;
        }

        /// <summary>
        /// Copies a log to the writer, its user folder taken out of each line: all of it, or when longer than
        /// <paramref name="cap"/> bytes its last ones, from the first whole line, under a line that says how much was left
        /// out. Read a line at a time, so a large log never sits in memory whole. Returns the lines written.
        /// </summary>
        public static int CopyTail(Stream from, TextWriter to, long cap, string profile)
        {
            long length = from.Length;
            long start = TailStart(length, cap);
            bool partLine = false;
            if (start > 0)
            {
                from.Seek(start - 1, SeekOrigin.Begin);
                partLine = from.ReadByte() != '\n';   // the cut fell inside a line, not on the start of one
                to.Write("[Real Life Overhaul: the first " + (start / 1024).ToString("N0", CultureInfo.InvariantCulture) + " KB of this log were left out; its last "
                         + ((length - start) / 1024).ToString("N0", CultureInfo.InvariantCulture) + " KB follow]\n");
            }
            int lines = 0;
            using (var reader = new StreamReader(from, new UTF8Encoding(false), false, 64 * 1024, true))
            {
                if (partLine)
                {
                    reader.ReadLine();   // the line the cut fell in
                }
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    to.Write(MaskUser(line, profile));
                    to.Write('\n');
                    lines++;
                }
            }
            return lines;
        }

        /// <summary>The report's folder and zip name: the local date and time of the press, sortable, safe in any file system.</summary>
        public static string FolderName(DateTime local)
        {
            return local.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture);
        }

        /// <summary>The name for a new report: the one given, or with _2, _3 ... when a report of that name (its folder or its zip) is there.</summary>
        public static string UniqueName(string name, Func<string, bool> taken)
        {
            if (!taken(name))
            {
                return name;
            }
            for (int n = 2; ; n++)
            {
                string next = name + "_" + n.ToString(CultureInfo.InvariantCulture);
                if (!taken(next))
                {
                    return next;
                }
            }
        }

        private static readonly Regex ReportName = new Regex(@"^(\d{4}-\d{2}-\d{2}_\d{2}-\d{2}-\d{2})(?:_(\d+))?(?:\.zip)?$", RegexOptions.CultureInvariant);

        /// <summary>
        /// The reports to delete before a new one is written, so that with it there are <paramref name="keep"/>: of the names
        /// in the reports' folder (folders and zips), the reports' own names only, newest first by date, time and number; the
        /// rest are given back without ".zip", each once. Nothing else in the folder is ever named.
        /// </summary>
        public static List<string> Older(IEnumerable<string> names, int keep)
        {
            var stems = new List<KeyValuePair<string, int>>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string name in names)
            {
                Match m = ReportName.Match(name ?? string.Empty);
                if (!m.Success)
                {
                    continue;
                }
                string stem = name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ? name.Substring(0, name.Length - 4) : name;
                if (seen.Add(stem))
                {
                    stems.Add(new KeyValuePair<string, int>(stem, m.Groups[2].Success ? int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) : 1));
                }
            }
            stems.Sort((a, b) =>
            {
                int by = string.CompareOrdinal(ReportName.Match(b.Key).Groups[1].Value, ReportName.Match(a.Key).Groups[1].Value);
                return by != 0 ? by : b.Value.CompareTo(a.Value);
            });
            var older = new List<string>();
            for (int i = Math.Max(0, keep - 1); i < stems.Count; i++)
            {
                older.Add(stems[i].Key);
            }
            return older;
        }

        /// <summary>A file of the report that failed, as report.txt's list gives it: its name and the reason, the user folder taken out.</summary>
        public static string NotRead(string name, string reason, string profile)
        {
            return name + ": could not be read (" + Cut(FirstLine(MaskUser(reason ?? "unknown", profile)), LineChars) + ")";
        }

        /// <summary>An error's message as the failure notification shows it: the user folder taken out, cut to <see cref="ErrorChars"/>.</summary>
        public static string ErrorText(string message, string profile)
        {
            return Cut(MaskUser(message ?? string.Empty, profile), ErrorChars);
        }

        /// <summary>A file address for a folder, not escaped: Windows reads an escaped one in its own code page, which mangles "João" or a Chinese name.</summary>
        public static string FileUrl(string path)
        {
            return "file:///" + path.Replace('\\', '/').TrimStart('/');
        }

        /// <summary>What the file explorer is given to open a folder with the report picked.</summary>
        public static string ExplorerSelect(string path)
        {
            return "/select,\"" + path + "\"";
        }

        /// <summary>
        /// What is copied for the player to paste in Discord with the file: the versions, their words (cut to
        /// <see cref="SummaryWords"/>) and where the file is, the user folder taken out.
        /// </summary>
        public static string Summary(string modVersion, int gameBuild, string words, string file, string profile)
        {
            words = words != null ? words.Trim() : string.Empty;
            var text = new StringBuilder(256 + words.Length);
            text.Append("Real Life Overhaul ").Append(modVersion).Append(", game Build ").Append(gameBuild > 0 ? gameBuild.ToString(CultureInfo.InvariantCulture) : "?").Append('\n');
            text.Append("What went wrong: ").Append(words.Length > 0 ? Cut(words, SummaryWords) : "(not said)").Append('\n');
            text.Append("Report: ").Append(file);
            return MaskUser(text.ToString(), profile);
        }
    }
}
