using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Health
{
    /// <summary>
    /// Health in the save: one line of modData under <see cref="Key"/>, game-free.
    ///
    ///     v=1^h:...^c:...^p:...^k:...^s:...^d:...^l:...^a:...^t:...;...^r:...;...^o:...^e:...^n:...;...
    ///
    /// Parts are divided by '^' and begin with a letter and a colon; records inside a part by ';', fields by '|'. h is the header, c the player's case, p the policy, k the
    /// days of its claims, s a hospital stay under way, d the medical debt, l the sick leave, a the appointment, t the staff off, r the rivals, o the people's pass and the
    /// month's sums, e who is ill by the day their case ends, n the paper's notes. A newer version adds fields at the end of a record or a part with a new letter: this
    /// reader takes what it knows and puts every figure inside its bounds. A line from before the feature is none at all. Every number is written and read in the invariant
    /// culture, so a language with a decimal comma saves and loads the same line.
    /// </summary>
    public static class HealthStore
    {
        public const string Key = "reallife:health";
        public const int Version = 1;

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static HealthState Load(Dictionary<string, string> data, out int version)
        {
            version = 0;
            if (data == null || !data.TryGetValue(Key, out string text) || string.IsNullOrEmpty(text))
            {
                return null;
            }
            return SaveLines.Checked(data, Key, Read(text, out version), version, Version);   // (a line that cannot be read is kept as it is: Core/SaveLines)
        }

        public static string Write(HealthState s)
        {
            var t = new StringBuilder(384 + s.Staff.Count * 24);
            t.Append("v=").Append(Version).Append("^h:");
            Join(t, s.Seed, s.LastDay, s.LastHour, s.GameStays, s.Year, s.BillsYear, s.ClaimsYear, s.LastBill, s.LastClaim, s.LastBillDay, s.Injuries, s.StaffDay);
            HealthCase c = s.Case;
            t.Append("^c:");
            Join(t, (int)c.Kind, c.Began, c.Until, c.Treated ? 1 : 0, c.Serious ? 1 : 0, c.Work ? 1 : 0, c.Neglect, c.LastDay);
            HealthPolicy p = s.Policy;
            t.Append("^p:");
            Join(t, (int)p.Plan, p.Since, p.NextDue, p.Missed, p.Year, p.PaidYear);
            if (p.ClaimDays.Count > 0)
            {
                t.Append("^k:");
                for (int i = 0; i < p.ClaimDays.Count; i++)
                {
                    t.Append(i > 0 ? "|" : string.Empty).Append(p.ClaimDays[i].ToString(Inv));
                }
            }
            HealthStay st = s.Stay;
            if (st.Active)
            {
                t.Append("^s:");
                Join(t, 1, st.From, st.Until, st.Days, (int)st.Kind, st.GameNight ? 1 : 0);
            }
            HealthDebt d = s.Debt;
            t.Append("^d:");
            Join(t, d.Owed, d.PerDay, d.Late, d.Claim, d.LastDay, d.Paid);
            LeaveBook l = s.Leave;
            t.Append("^l:");
            Join(t, l.Year, l.PaidUsed, l.SpellLast, l.SpellDays, l.LastCovered ? 1 : 0, l.ClaimDay, l.Claims, l.Noticed, l.NoteUntil, l.PaidDay, l.PaidMoney, l.NoteFrom);
            HealthAppointment a = s.Appointment;
            if (a.Booked)
            {
                t.Append("^a:");
                Join(t, a.Day, a.Hour, a.Kept ? 1 : 0, a.Number);
                t.Append('|').Append(Uri.EscapeDataString(a.Street ?? string.Empty));
            }
            if (s.Staff.Count > 0)
            {
                t.Append("^t:");
                for (int i = 0; i < s.Staff.Count; i++)
                {
                    StaffCase sc = s.Staff[i];
                    t.Append(i > 0 ? ";" : string.Empty);
                    Join(t, sc.From, sc.Until);
                    t.Append('|').Append(Uri.EscapeDataString(sc.Id ?? string.Empty));
                }
            }
            t.Append("^r:");
            for (int i = 0; i < s.RivalCases.Length; i++)
            {
                RivalHealth r = s.RivalCases[i];
                t.Append(i > 0 ? ";" : string.Empty);
                Join(t, (int)r.Kind, r.Day, r.Until, r.Serious ? 1 : 0, r.Bill, r.LastDay);
            }
            PopHealth o = s.Pop;
            t.Append("^o:");
            Join(t, o.Day, o.Cursor, o.IllNow, o.Month, o.Cases, o.Serious, o.LostDays, o.LostWages, o.Bills, o.Debt, o.LastCases, o.LastSerious, o.LastLostDays, o.LastLostWages,
                o.LastBills, o.LastDebt);
            t.Append("^e:");
            for (int i = 0; i < o.Ends.Length; i++)
            {
                t.Append(i > 0 ? "|" : string.Empty).Append(o.Ends[i].ToString(Inv));
            }
            if (s.Notes.Count > 0)
            {
                t.Append("^n:");
                for (int i = 0; i < s.Notes.Count; i++)
                {
                    t.Append(i > 0 ? ";" : string.Empty);
                    Join(t, s.Notes[i].Day, s.Notes[i].Rival, (int)s.Notes[i].Kind);
                }
            }
            return t.ToString();
        }

        /// <summary>Reads a line. Null for one that is not this feature's at all.</summary>
        public static HealthState Read(string text, out int version)
        {
            version = 0;
            if (string.IsNullOrEmpty(text) || !text.StartsWith("v=", StringComparison.Ordinal))
            {
                return null;
            }
            string[] parts = text.Split('^');
            if (!int.TryParse(parts[0].Substring(2), NumberStyles.Integer, Inv, out version))
            {
                return null;
            }
            var s = new HealthState();
            for (int i = 1; i < parts.Length; i++)
            {
                string part = parts[i];
                if (part.Length < 2 || part[1] != ':')
                {
                    continue;
                }
                string[] f = part.Substring(2).Split(part[0] == 't' || part[0] == 'r' || part[0] == 'n' ? ';' : '|');
                switch (part[0])
                {
                    case 'h': Header(s, f); break;
                    case 'c': Case(s.Case, f); break;
                    case 'p': Policy(s.Policy, f); break;
                    case 'k': Claims(s.Policy, f); break;
                    case 's': Stay(s.Stay, f); break;
                    case 'd': Debt(s.Debt, f); break;
                    case 'l': Leave(s.Leave, f); break;
                    case 'a': Appointment(s.Appointment, f); break;
                    case 't': Staff(s, f); break;
                    case 'r': Rivals(s, f); break;
                    case 'o': People(s.Pop, f); break;
                    case 'e': Ends(s.Pop, f); break;
                    case 'n': Notes(s, f); break;
                }
            }
            return s;
        }

        private static void Header(HealthState s, string[] f)
        {
            s.Seed = I(f, 0);
            s.LastDay = Math.Max(0, I(f, 1));
            s.LastHour = HealthCatalogue.Clamp(I(f, 2, -1), -1, 23);
            s.GameStays = Math.Max(-1, I(f, 3, -1));
            s.Year = Math.Max(-1, I(f, 4, -1));
            s.BillsYear = Math.Max(0, L(f, 5));
            s.ClaimsYear = HealthCatalogue.Clamp(L(f, 6), 0, s.BillsYear);   // a plan never paid more than the bills
            s.LastBill = Math.Max(0, L(f, 7));
            s.LastClaim = HealthCatalogue.Clamp(L(f, 8), 0, s.LastBill);
            s.LastBillDay = Math.Max(0, I(f, 9));
            s.Injuries = Math.Max(0, I(f, 10));
            s.StaffDay = Math.Max(0, I(f, 11));
        }

        private static void Case(HealthCase c, string[] f)
        {
            int kind = I(f, 0);
            c.Kind = kind > 0 && kind < (int)Condition.Count ? (Condition)kind : Condition.None;
            c.Began = Math.Max(0, I(f, 1));
            c.Until = Math.Max(0, I(f, 2));
            c.Treated = I(f, 3) == 1;
            c.Serious = I(f, 4) == 1;
            c.Work = I(f, 5) == 1;
            c.Neglect = HealthCatalogue.Clamp(I(f, 6), 0, 1000);
            c.LastDay = Math.Max(0, I(f, 7));
            if (!c.Open)
            {
                c.Clear();
            }
        }

        private static void Policy(HealthPolicy p, string[] f)
        {
            int plan = I(f, 0);
            p.Plan = plan > 0 && plan < (int)Plan.Count ? (Plan)plan : Plan.None;
            p.Since = Math.Max(0, I(f, 1));
            p.NextDue = Math.Max(0, I(f, 2));
            p.Missed = HealthCatalogue.Clamp(I(f, 3), 0, HealthCatalogue.LapseAfter);
            p.Year = Math.Max(-1, I(f, 4, -1));
            p.PaidYear = Math.Max(0, L(f, 5));
        }

        private static void Claims(HealthPolicy p, string[] f)
        {
            for (int i = 0; i < f.Length && p.ClaimDays.Count < HealthCatalogue.ClaimsKept; i++)
            {
                int day = I(f, i, -1);
                if (day >= 0)
                {
                    p.ClaimDays.Add(day);
                }
            }
        }

        private static void Stay(HealthStay st, string[] f)
        {
            int kind = I(f, 4);
            st.Active = I(f, 0) == 1;
            st.From = Math.Max(0, I(f, 1));
            st.Until = Math.Max(st.From, I(f, 2));
            st.Days = HealthCatalogue.Clamp(I(f, 3), 0, HealthCatalogue.StayMost);
            st.Kind = kind > 0 && kind < (int)Condition.Count ? (Condition)kind : Condition.None;
            st.GameNight = I(f, 5) == 1;
            // A stay is never longer than the longest: a line that says otherwise is held to it, so nobody is kept in hospital by a bad figure.
            st.Until = Math.Min(st.Until, st.From + (HealthCatalogue.StayMost + 1) * 1440);
        }

        private static void Debt(HealthDebt d, string[] f)
        {
            d.Owed = Math.Max(0, L(f, 0));
            d.PerDay = d.Owed > 0 ? Math.Max(1, I(f, 1)) : 0;
            d.Late = Math.Max(0, I(f, 2));
            d.Claim = Math.Max(0, I(f, 3));
            d.LastDay = Math.Max(0, I(f, 4));
            d.Paid = Math.Max(0, L(f, 5));
        }

        private static void Leave(LeaveBook l, string[] f)
        {
            l.Year = Math.Max(-1, I(f, 0, -1));
            l.PaidUsed = Math.Max(0, I(f, 1));
            l.SpellLast = Math.Max(0, I(f, 2));
            l.SpellDays = Math.Max(0, I(f, 3));
            l.LastCovered = I(f, 4) == 1;
            l.ClaimDay = Math.Max(0, I(f, 5));
            l.Claims = Math.Max(0, I(f, 6));
            l.Noticed = HealthCatalogue.Clamp(I(f, 7), 0, l.Claims);
            l.NoteUntil = Math.Max(0, I(f, 8));
            l.PaidDay = Math.Max(0, I(f, 9));
            l.PaidMoney = Math.Max(0, L(f, 10));
            l.NoteFrom = HealthCatalogue.Clamp(I(f, 11), 0, l.NoteUntil);
        }

        private static void Appointment(HealthAppointment a, string[] f)
        {
            a.Day = Math.Max(0, I(f, 0));
            a.Hour = HealthCatalogue.Clamp(I(f, 1), 0, 23);
            a.Kept = I(f, 2) == 1;
            a.Number = Math.Max(0, I(f, 3));
            a.Street = f.Length > 4 ? Unescape(f[4]) : string.Empty;
        }

        private static void Staff(HealthState s, string[] records)
        {
            for (int i = 0; i < records.Length && s.Staff.Count < HealthCatalogue.StaffCasesMost; i++)
            {
                string[] f = records[i].Split('|');
                string id = f.Length > 2 ? Unescape(f[2]) : string.Empty;
                if (id.Length > 0)
                {
                    int from = Math.Max(0, I(f, 0));
                    s.Staff.Add(new StaffCase { Id = id, From = from, Until = HealthCatalogue.Clamp(I(f, 1), from, from + HealthCatalogue.StaffCourseMost) });
                }
            }
        }

        private static void Rivals(HealthState s, string[] records)
        {
            for (int i = 0; i < records.Length && i < s.RivalCases.Length; i++)
            {
                string[] f = records[i].Split('|');
                RivalHealth r = s.RivalCases[i];
                int kind = I(f, 0);
                r.Kind = kind > 0 && kind < (int)Condition.Count ? (Condition)kind : Condition.None;
                r.Day = Math.Max(0, I(f, 1));
                r.Until = Math.Max(0, I(f, 2));
                r.Serious = I(f, 3) == 1;
                r.Bill = Math.Max(0, L(f, 4));
                r.LastDay = Math.Max(0, I(f, 5));
            }
        }

        private static void People(PopHealth o, string[] f)
        {
            o.Day = Math.Max(0, I(f, 0));
            o.Cursor = Math.Max(-1, I(f, 1, -1));
            o.IllNow = Math.Max(0, I(f, 2));
            o.Month = Math.Max(-1, I(f, 3, -1));
            o.Cases = Math.Max(0, I(f, 4));
            o.Serious = Math.Max(0, I(f, 5));
            o.LostDays = Math.Max(0, I(f, 6));
            o.LostWages = Math.Max(0, L(f, 7));
            o.Bills = Math.Max(0, L(f, 8));
            o.Debt = Math.Max(0, L(f, 9));
            o.LastCases = Math.Max(0, I(f, 10));
            o.LastSerious = Math.Max(0, I(f, 11));
            o.LastLostDays = Math.Max(0, I(f, 12));
            o.LastLostWages = Math.Max(0, L(f, 13));
            o.LastBills = Math.Max(0, L(f, 14));
            o.LastDebt = Math.Max(0, L(f, 15));
        }

        private static void Ends(PopHealth o, string[] f)
        {
            for (int i = 0; i < f.Length && i < o.Ends.Length; i++)
            {
                o.Ends[i] = Math.Max(0, I(f, i));
            }
        }

        private static void Notes(HealthState s, string[] records)
        {
            for (int i = 0; i < records.Length && s.Notes.Count < 8; i++)
            {
                string[] f = records[i].Split('|');
                int kind = I(f, 2), rival = I(f, 1, -1);
                if (kind > 0 && kind < (int)Condition.Count && rival >= 0 && rival < HealthState.Rivals)
                {
                    s.Notes.Add(new HealthNote { Day = Math.Max(0, I(f, 0)), Rival = rival, Kind = (Condition)kind });
                }
            }
        }

        private static void Join(StringBuilder t, params long[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                t.Append(i > 0 ? "|" : string.Empty).Append(values[i].ToString(Inv));
            }
        }

        private static int I(string[] f, int at, int fallback = 0) => at < f.Length && int.TryParse(f[at], NumberStyles.Integer, Inv, out int v) ? v : fallback;

        private static long L(string[] f, int at) => at < f.Length && long.TryParse(f[at], NumberStyles.Integer, Inv, out long v) ? v : 0L;

        private static string Unescape(string text)
        {
            try
            {
                return Uri.UnescapeDataString(text ?? string.Empty);
            }
            catch (UriFormatException)
            {
                return string.Empty;
            }
        }
    }
}
