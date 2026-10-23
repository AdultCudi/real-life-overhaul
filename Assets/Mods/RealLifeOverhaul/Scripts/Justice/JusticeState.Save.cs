using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul
{
    // One line of text in the save's modData, which the game carries without reading: a save made with it loads without
    // the mod. Whatever does not read as expected is left at nothing: a damaged line costs the history, never the load.
    public static partial class JusticeState
    {
        private const string Version = "1";

        private static readonly char[] Entry = { ';' };

        public static void Load(Dictionary<string, string> data)
        {
            Clear();
            Loaded = true;
            if (data != null && data.TryGetValue(Key, out string text) && !string.IsNullOrEmpty(text))
            {
                try
                {
                    Parse(text);
                }
                catch (Exception)
                {
                    Clear();
                    SaveLines.Unreadable(data, Key, "it could not be read");   // the line is kept as it was read
                }
            }
        }

        public static void Save(Dictionary<string, string> data)
        {
            if (Loaded && data != null)
            {
                data[Key] = Write();
            }
        }

        public static string Write()
        {
            Trim();
            var text = new StringBuilder(256);
            text.Append(Version);
            text.Append("|H:");
            Join(text, NextNumber, Day, ProbationUntil, SuspendedUntil, CommunityHours, CommunityDue, LastConvictionDay, LastSuitDay,
                JailHoursPending, HearNow, ServeWarrant ? 1 : 0, Compensated);
            text.Append("|L:");
            Join(text, (int)Lawyer, LawyerNumber, LawyerStars);
            text.Append(',').Append(Escape(LawyerName)).Append(',').Append(Escape(LawyerStreet));
            text.Append("|P:");
            Join(text, PrisonTotal, PrisonOriginal, PrisonServed, PrisonBehaviour, PrisonEarned, PrisonStudied, PrisonExercised, PrisonRemitted,
                PrisonParoleHeard ? 1 : 0, PrisonCase);
            text.Append(',').Append(Escape(PrisonCar)).Append(',').Append(Escape(PrisonSeen));
            text.Append("|C:");
            for (int i = 0; i < Cases.Count; i++)
            {
                JusticeCase c = Cases[i];
                if (i > 0)
                {
                    text.Append(';');
                }
                int flags = (c.Cameras ? 1 : 0) | (c.InTheAct ? 2 : 0) | (c.Fled ? 4 : 0) | (c.Complied ? 8 : 0) | (c.Bond ? 16 : 0) | (c.Remand ? 32 : 0)
                            | (c.DealReady ? 64 : 0) | (c.MotionTried ? 128 : 0) | (c.MetInPerson ? 256 : 0) | (c.Reviewed ? 512 : 0) | (c.Appealed ? 1024 : 0);
                Join(text, c.Number, c.OpenedDay, c.CourtDay, (int)c.Stage, (int)c.Outcome, c.Witnesses, flags, c.PaidAtScene, c.HeldHours, c.BailPaid,
                    c.Seed, c.Reminded, c.WarrantDay, c.ClosedDay, c.FineImposed, c.PrisonImposed, c.JailImposed, c.CommunityImposed, c.ProbationImposed,
                    c.SuspensionImposed, Mille(c.LawyerQuality), c.Postponed);
                text.Append(',');
                for (int k = 0; k < c.Charges.Count; k++)
                {
                    JusticeCharge charge = c.Charges[k];
                    if (k > 0)
                    {
                        text.Append('/');
                    }
                    text.Append(Escape(charge.Id)).Append('~').Append(Mille(charge.Evidence).ToString(CultureInfo.InvariantCulture)).Append('~')
                        .Append(((charge.Dropped ? 1 : 0) | (charge.Convicted ? 2 : 0) | (charge.Reduced ? 4 : 0)).ToString(CultureInfo.InvariantCulture));
                }
            }
            text.Append("|D:");
            for (int i = 0; i < Debts.Count; i++)
            {
                if (i > 0)
                {
                    text.Append(';');
                }
                Join(text, Debts[i].Kind, Debts[i].Left, Debts[i].PerDay, Debts[i].Missed, Debts[i].CaseNumber);
            }
            text.Append("|R:");
            for (int i = 0; i < Record.Count; i++)
            {
                if (i > 0)
                {
                    text.Append(';');
                }
                Join(text, Record[i].Day, Record[i].CaseNumber, Record[i].Sealed ? 1 : 0);
                text.Append(',').Append(Escape(Record[i].ChargeId));
            }
            text.Append("|S:");
            for (int i = 0; i < Suits.Count; i++)
            {
                if (i > 0)
                {
                    text.Append(';');
                }
                Join(text, Suits[i].Number, Suits[i].Claim, Suits[i].DueDay, Suits[i].Injury ? 1 : 0, Suits[i].Contested ? 1 : 0, Suits[i].Seed);
            }
            WriteClaims(text);
            return text.ToString();
        }

        public static void Parse(string text)
        {
            string[] sections = text.Split('|');
            if (sections.Length < 2 || sections[0] != Version)
            {
                return;
            }
            for (int s = 1; s < sections.Length; s++)
            {
                string section = sections[s];
                if (section.Length < 2 || section[1] != ':')
                {
                    continue;
                }
                string body = section.Substring(2);
                switch (section[0])
                {
                    case 'H':
                    {
                        var f = new Fields(body);
                        NextNumber = Math.Max(1, f.Int());
                        Day = f.Int();
                        ProbationUntil = f.Int();
                        SuspendedUntil = f.Int();
                        CommunityHours = Math.Min(JusticeLaw.CommunityCapHours, Math.Max(0, f.Int()));
                        CommunityDue = f.Int();
                        LastConvictionDay = f.Int();
                        LastSuitDay = f.Int();
                        JailHoursPending = Math.Min(JusticeLaw.CustodyCapHours, Math.Max(0, f.Int()));
                        HearNow = f.Int();
                        ServeWarrant = f.Int() != 0;
                        Compensated = f.Int();
                        break;
                    }
                    case 'L':
                    {
                        var f = new Fields(body);
                        int kind = f.Int();
                        Lawyer = kind >= 0 && kind <= 3 ? (LawyerKind)kind : LawyerKind.None;
                        LawyerNumber = f.Int();
                        LawyerStars = f.Int();
                        LawyerName = f.Text();
                        LawyerStreet = f.Text();
                        break;
                    }
                    case 'P':
                    {
                        var f = new Fields(body);
                        PrisonTotal = Math.Min(JusticeLaw.PrisonCapMax, Math.Max(0, f.Int()));
                        PrisonOriginal = f.Int();
                        PrisonServed = Math.Max(0, f.Int());
                        PrisonBehaviour = f.Signed();
                        PrisonEarned = f.Int();
                        PrisonStudied = f.Int();
                        PrisonExercised = f.Int();
                        PrisonRemitted = f.Int();
                        PrisonParoleHeard = f.Int() != 0;
                        PrisonCase = f.Int();
                        PrisonCar = f.Text();
                        PrisonSeen = f.Text();
                        break;
                    }
                    case 'C':
                        foreach (string one in body.Split(Entry, StringSplitOptions.RemoveEmptyEntries))
                        {
                            JusticeCase c = ParseCase(one);
                            if (c != null)
                            {
                                Cases.Add(c);
                            }
                        }
                        break;
                    case 'D':
                        foreach (string one in body.Split(Entry, StringSplitOptions.RemoveEmptyEntries))
                        {
                            var f = new Fields(one);
                            var debt = new JusticeDebt { Kind = f.Int(), Left = f.Int(), PerDay = Math.Max(10, f.Int()), Missed = f.Int(), CaseNumber = f.Int() };
                            if (debt.Left > 0 && debt.Kind >= 0 && debt.Kind <= 2)
                            {
                                Debts.Add(debt);
                            }
                        }
                        break;
                    case 'R':
                        foreach (string one in body.Split(Entry, StringSplitOptions.RemoveEmptyEntries))
                        {
                            var f = new Fields(one);
                            var entry = new JusticeRecord { Day = f.Int(), CaseNumber = f.Int(), Sealed = f.Int() != 0, ChargeId = f.Text() };
                            if (JusticeLaw.Find(entry.ChargeId) != null)   // a charge this build does not have is dropped
                            {
                                Record.Add(entry);
                            }
                        }
                        break;
                    case 'S':
                        foreach (string one in body.Split(Entry, StringSplitOptions.RemoveEmptyEntries))
                        {
                            var f = new Fields(one);
                            var suit = new JusticeSuit { Number = f.Int(), Claim = f.Int(), DueDay = f.Int(), Injury = f.Int() != 0, Contested = f.Int() != 0, Seed = f.Int() };
                            if (suit.Claim > 0)
                            {
                                Suits.Add(suit);
                            }
                        }
                        break;
                    case 'O':
                        ParseClaims(body);
                        break;
                }
            }
            if (HearNow != 0 && (Find(HearNow) == null || !Find(HearNow).Open))
            {
                HearNow = 0;
            }
            if (PrisonTotal > 0 && PrisonServed > PrisonTotal)
            {
                PrisonServed = PrisonTotal;
            }
        }

        private static JusticeCase ParseCase(string text)
        {
            var f = new Fields(text);
            var c = new JusticeCase { Number = f.Int(), OpenedDay = f.Int(), CourtDay = f.Int() };
            int stage = f.Int();
            int outcome = f.Int();
            c.Stage = stage >= 0 && stage <= 3 ? (CaseStage)stage : CaseStage.Closed;
            c.Outcome = outcome >= 0 && outcome <= 6 ? (CaseOutcome)outcome : CaseOutcome.None;
            c.Witnesses = f.Int();
            int flags = f.Int();
            c.Cameras = (flags & 1) != 0;
            c.InTheAct = (flags & 2) != 0;
            c.Fled = (flags & 4) != 0;
            c.Complied = (flags & 8) != 0;
            c.Bond = (flags & 16) != 0;
            c.Remand = (flags & 32) != 0;
            c.DealReady = (flags & 64) != 0;
            c.MotionTried = (flags & 128) != 0;
            c.MetInPerson = (flags & 256) != 0;
            c.Reviewed = (flags & 512) != 0;
            c.Appealed = (flags & 1024) != 0;
            c.PaidAtScene = f.Int();
            c.HeldHours = f.Int();
            c.BailPaid = f.Int();
            c.Seed = f.Int();
            c.Reminded = f.Int();
            c.WarrantDay = f.Int();
            c.ClosedDay = f.Int();
            c.FineImposed = f.Int();
            c.PrisonImposed = f.Int();
            c.JailImposed = f.Int();
            c.CommunityImposed = f.Int();
            c.ProbationImposed = f.Int();
            c.SuspensionImposed = f.Int();
            c.LawyerQuality = f.Int() / 1000f;
            c.Postponed = f.Int();
            string charges = f.Raw();
            if (charges != null)
            {
                foreach (string one in charges.Split('/'))
                {
                    string[] parts = one.Split('~');
                    if (parts.Length < 3)
                    {
                        continue;
                    }
                    string id = Unescape(parts[0]);
                    if (JusticeLaw.Find(id) == null
                        || !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int evidence)
                        || !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int bits))
                    {
                        continue;
                    }
                    c.Charges.Add(new JusticeCharge
                    {
                        Id = id, Evidence = JusticeLaw.Clamp(evidence / 1000f, 0f, 1f), Dropped = (bits & 1) != 0, Convicted = (bits & 2) != 0, Reduced = (bits & 4) != 0
                    });
                }
            }
            if (c.Number <= 0)
            {
                return null;
            }
            if (c.Open && c.Standing == 0)
            {
                c.Stage = CaseStage.Closed;   // no charge this build knows: nothing to answer
                c.Outcome = CaseOutcome.Dismissed;
            }
            return c;
        }

        // Comma-separated fields in order. Past the end every number is 0 and every text empty, so an older, shorter line reads.
        private struct Fields
        {
            private readonly string[] _parts;
            private int _next;

            public Fields(string text)
            {
                _parts = text.Split(',');
                _next = 0;
            }

            public int Signed()
            {
                return _next < _parts.Length && int.TryParse(_parts[_next++], NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : 0;
            }

            public int Int()
            {
                return Math.Max(0, Signed());
            }

            public string Text()
            {
                return _next < _parts.Length ? Unescape(_parts[_next++]) : string.Empty;
            }

            public string Raw()
            {
                return _next < _parts.Length ? _parts[_next++] : null;
            }
        }

        private static int Mille(float value)
        {
            return (int)Math.Round(JusticeLaw.Clamp(value, 0f, 1f) * 1000f);
        }

        private static void Join(StringBuilder text, params int[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                if (i > 0)
                {
                    text.Append(',');
                }
                text.Append(values[i].ToString(CultureInfo.InvariantCulture));
            }
        }

        // Names come from the game's data and other mods: anything that could break the line is written as %XXXX.
        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }
            StringBuilder escaped = null;
            for (int i = 0; i < value.Length; i++)
            {
                char ch = value[i];
                bool plain = ch < 128 && (char.IsLetterOrDigit(ch) || ch == ' ' || ch == '_' || ch == '-' || ch == '.' || ch == ':' || ch == '&' || ch == '\'');
                if (!plain && escaped == null)
                {
                    escaped = new StringBuilder(value.Length + 8).Append(value, 0, i);
                }
                if (escaped != null)
                {
                    if (plain)
                    {
                        escaped.Append(ch);
                    }
                    else
                    {
                        escaped.Append('%').Append(((int)ch).ToString("X4", CultureInfo.InvariantCulture));
                    }
                }
            }
            return escaped != null ? escaped.ToString() : value;
        }

        private static string Unescape(string value)
        {
            if (string.IsNullOrEmpty(value) || value.IndexOf('%') < 0)
            {
                return value ?? string.Empty;
            }
            var plain = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] == '%' && i + 4 < value.Length
                    && int.TryParse(value.Substring(i + 1, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int code))
                {
                    plain.Append((char)code);
                    i += 4;
                }
                else
                {
                    plain.Append(value[i]);
                }
            }
            return plain.ToString();
        }
    }
}
