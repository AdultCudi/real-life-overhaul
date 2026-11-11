using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Suppliers
{
    /// <summary>
    /// Suppliers and contracts in the save: one line of modData under <see cref="Key"/>, game-free.
    ///
    ///     v=1^h:...^f:...;...^c:...;...^o:...;...^p:...^r:...;...^n:...;...^l:...;...
    ///
    /// Parts are divided by '^' and begin with a letter and a colon; records inside a part by ';', fields by '|'. h is the header, f the suppliers (in the catalogue's
    /// order), c the contracts (a contract's products are its last known field, divided by '~' with ',' inside), o what is owed, p the payment record, r the rival
    /// tycoons' sums, n the paper's notes, l the log of deliveries, g the last valued day of each rival shop that was charged. A contract running or with money owed on it, and every
    /// line owed, is read whatever the count; only the oldest ended ones past what the page keeps are let go. A newer version adds fields at the end of a record or a part
    /// with a new letter: this reader takes what it knows as far as it goes and puts every figure inside its bounds. A save from before the feature has no line and loads
    /// with nothing in it. Every number is written and read in the invariant culture.
    /// </summary>
    public static class SupplierStore
    {
        public const string Key = "reallife:suppliers";
        public const int Version = 1;

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static SupplierState Load(Dictionary<string, string> data, out int version)
        {
            version = 0;
            if (data == null || !data.TryGetValue(Key, out string text) || string.IsNullOrEmpty(text))
            {
                return null;
            }
            return SaveLines.Checked(data, Key, Read(text, out version), version, Version);   // (a line that cannot be read is kept as it is: Core/SaveLines)
        }

        public static string Write(SupplierState s)
        {
            var t = new StringBuilder(512 + s.Contracts.Count * 160);
            t.Append("v=").Append(Version).Append("^h:");
            Join(t, s.Seed, s.LastDay, s.NextId, s.RivalDay, s.LastHour, s.ShopsShort);
            t.Append("^f:");
            for (int i = 0; i < s.Firms.Length; i++)
            {
                FirmState f = s.Firms[i];
                t.Append(i > 0 ? ";" : string.Empty);
                Join(t, f.Health, f.Size, f.TroubleKind, f.TroubleUntil, f.ClosedDay, f.ReopenDay, f.Strong, f.SourUntil, f.Sold ? 1 : 0, f.Customers, f.Brought, f.Missed,
                    f.Book[0], f.Book[1], f.Book[2], f.Book[3], f.Book[4], f.Book[5]);
                t.Append('|').Append(Uri.EscapeDataString(f.Depot ?? string.Empty));
            }
            if (s.Contracts.Count > 0)
            {
                t.Append("^c:");
                for (int i = 0; i < s.Contracts.Count; i++)
                {
                    Contract c = s.Contracts[i];
                    t.Append(i > 0 ? ";" : string.Empty);
                    Join(t, c.Id, c.Supplier, c.Weeks, c.Share, c.Start, c.Due, c.Slip, c.Made, (int)c.Out, (int)c.Why, c.OutShare, c.OutKind, c.JudgedFor, (int)c.State, c.Ended,
                        c.Failures, c.Withheld, c.Paid, c.Credit, c.NotTaken, (int)c.Renew, c.RenewLimit, c.Cover ? 1 : 0, c.LeaveRule ? 1 : 0, c.Reminded ? 1 : 0, c.Covering ? 1 : 0);
                    t.Append('|').Append(Uri.EscapeDataString(c.Place ?? string.Empty)).Append('|').Append(Uri.EscapeDataString(c.PlaceName ?? string.Empty)).Append('|');
                    for (int k = 0; k < c.Lines.Count; k++)
                    {
                        ContractLine line = c.Lines[k];
                        t.Append(k > 0 ? "~" : string.Empty).Append(Uri.EscapeDataString(line.Item ?? string.Empty)).Append(',').Append(line.Kind.ToString(Inv)).Append(',')
                            .Append(line.Units.ToString(Inv)).Append(',').Append(line.Mills.ToString(Inv));
                    }
                    t.Append('|').Append(c.Knew ? 1 : 0);
                }
            }
            if (s.Owed.Count > 0)
            {
                t.Append("^o:");
                for (int i = 0; i < s.Owed.Count; i++)
                {
                    OwedLine o = s.Owed[i];
                    t.Append(i > 0 ? ";" : string.Empty);
                    Join(t, o.Supplier, o.Contract, o.Cents, o.Since, o.Due, o.Settlement ? 1 : 0, o.Acted ? 1 : 0);
                }
            }
            t.Append("^p:");
            Join(t, s.Record.OnTime, s.Record.Late, s.Record.Defaults);
            t.Append('|');
            for (int i = 0; i < s.Record.LateDays.Count; i++)
            {
                t.Append(i > 0 ? "," : string.Empty).Append(s.Record.LateDays[i].ToString(Inv));
            }
            t.Append("^r:");
            for (int i = 0; i < s.RivalBooks.Length; i++)
            {
                RivalSupply r = s.RivalBooks[i];
                t.Append(i > 0 ? ";" : string.Empty);
                Join(t, r.Month, r.Total, r.Failures, r.Firm, r.MonthOf);
            }
            if (s.Notes.Count > 0)
            {
                t.Append("^n:");
                for (int i = 0; i < s.Notes.Count; i++)
                {
                    SupplyNote n = s.Notes[i];
                    t.Append(i > 0 ? ";" : string.Empty);
                    Join(t, n.Day, (int)n.Kind, n.Firm, n.A, n.B);
                }
            }
            if (s.Log.Count > 0)
            {
                t.Append("^l:");
                for (int i = 0; i < s.Log.Count; i++)
                {
                    DeliveryLog e = s.Log[i];
                    t.Append(i > 0 ? ";" : string.Empty);
                    Join(t, e.Day, e.Units);
                    t.Append('|').Append(Uri.EscapeDataString(e.Place ?? string.Empty)).Append('|').Append(Uri.EscapeDataString(e.Item ?? string.Empty));
                }
            }
            if (s.Charges.Count > 0)
            {
                t.Append("^g:");
                bool first = true;
                foreach (KeyValuePair<int, RivalCharge> pair in s.Charges)
                {
                    t.Append(first ? string.Empty : ";");
                    first = false;
                    Join(t, pair.Key, pair.Value.Count, pair.Value.Bits);
                }
            }
            return t.ToString();
        }

        /// <summary>Reads a line. Null for one that is not this feature's at all.</summary>
        public static SupplierState Read(string text, out int version)
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
            var s = new SupplierState();
            for (int i = 1; i < parts.Length; i++)
            {
                string part = parts[i];
                if (part.Length < 2 || part[1] != ':')
                {
                    continue;
                }
                string body = part.Substring(2);
                switch (part[0])
                {
                    case 'h': Header(s, body); break;
                    case 'f': Firms(s, body); break;
                    case 'c': Contracts(s, body); break;
                    case 'o': Owed(s, body); break;
                    case 'p': Record(s, body); break;
                    case 'r': Rivals(s, body); break;
                    case 'n': Notes(s, body); break;
                    case 'l': Log(s, body); break;
                    case 'g': Charges(s, body); break;
                }
            }
            for (int i = 0; i < s.Contracts.Count; i++)
            {
                s.NextId = Math.Max(s.NextId, s.Contracts[i].Id + 1);   // an id is never given twice, whatever the header says
            }
            Keep(s);
            return s;
        }

        private static void Header(SupplierState s, string body)
        {
            string[] f = body.Split('|');
            s.Seed = I(f, 0);
            s.LastDay = Math.Max(0, I(f, 1));
            s.NextId = Math.Max(1, I(f, 2, 1));
            s.RivalDay = Math.Max(0, I(f, 3));
            s.LastHour = SupplierCatalogue.Clamp(I(f, 4, -1), -1, 23);
            s.ShopsShort = Math.Max(0, I(f, 5));
        }

        private static void Firms(SupplierState s, string body)
        {
            string[] records = body.Split(';');
            for (int i = 0; i < records.Length && i < s.Firms.Length; i++)
            {
                string[] f = records[i].Split('|');
                FirmState firm = s.Firms[i];
                firm.Health = SupplierCatalogue.Clamp(I(f, 0, SupplierCatalogue.Firms[i].HealthStart), 0, SupplierCatalogue.HealthMost);
                firm.Size = SupplierCatalogue.Clamp(I(f, 1, 1), 1, SupplierCatalogue.SizeMost);
                firm.TroubleKind = SupplierCatalogue.Clamp(I(f, 2), 0, 2);
                firm.TroubleUntil = Math.Max(0, I(f, 3));
                firm.ClosedDay = Math.Max(0, I(f, 4));
                firm.ReopenDay = firm.ClosedDay > 0 ? Math.Max(firm.ClosedDay, I(f, 5)) : 0;
                firm.Strong = Math.Max(0, I(f, 6));
                firm.SourUntil = Math.Max(0, I(f, 7));
                firm.Sold = I(f, 8) == 1;
                firm.Customers = Math.Max(0, I(f, 9));
                firm.Brought = Math.Max(0, I(f, 10));
                firm.Missed = Math.Max(0, I(f, 11));
                for (int k = 0; k < firm.Book.Length; k++)
                {
                    firm.Book[k] = SupplierCatalogue.Clamp(I(f, 12 + k, 1000), 500, 2000);
                }
                firm.Depot = f.Length > 18 ? Unescape(f[18]) : string.Empty;
                if (firm.Health <= 0 && firm.ClosedDay == 0)
                {
                    firm.Health = 1;   // a supplier at nothing is shut by its day, never found open at nothing
                }
            }
        }

        private static void Contracts(SupplierState s, string body)
        {
            string[] records = body.Split(';');
            for (int i = 0; i < records.Length; i++)
            {
                string[] f = records[i].Split('|');
                int id = I(f, 0), supplier = I(f, 1);
                if (f.Length < 29 || id <= 0 || SupplierCatalogue.Firm(supplier) == null || s.ContractOf(id) != null)
                {
                    continue;
                }
                int weeks = I(f, 2);
                var c = new Contract
                {
                    Id = id, Supplier = supplier, Weeks = SupplierCatalogue.TermStep(weeks) >= 0 ? weeks : 0, Share = SupplierCatalogue.Clamp(I(f, 3, 1000), 100, 3000),
                    Start = Math.Max(0, I(f, 4)), Due = Math.Max(0, I(f, 5)), Slip = SupplierCatalogue.Clamp(I(f, 6), 0, SupplierCatalogue.LateDaysMost),
                    Made = Math.Max(0, I(f, 7)), Out = (Arrival)SupplierCatalogue.Clamp(I(f, 8), 0, 3), Why = (Cause)SupplierCatalogue.Clamp(I(f, 9), 0, 7),
                    OutShare = SupplierCatalogue.Clamp(I(f, 10, 1000), 0, 1000), OutKind = SupplierCatalogue.Clamp(I(f, 11, -1), -1, SupplierCatalogue.Kinds - 1),
                    JudgedFor = Math.Max(0, I(f, 12)), State = (ContractState)SupplierCatalogue.Clamp(I(f, 13), 0, 6), Ended = Math.Max(0, I(f, 14)),
                    Failures = Math.Max(0, I(f, 15)), Withheld = Math.Max(0, I(f, 16)), Paid = Math.Max(0, L(f, 17)), Credit = Math.Max(0, L(f, 18)),
                    NotTaken = Math.Max(0, I(f, 19)), Renew = (RenewRule)SupplierCatalogue.Clamp(I(f, 20), 0, 2),
                    RenewLimit = SupplierCatalogue.Clamp(I(f, 21, SupplierCatalogue.RenewLimitDefault), 0, SupplierCatalogue.RenewLimits.Length - 1),
                    Cover = I(f, 22) == 1, LeaveRule = I(f, 23) == 1, Reminded = I(f, 24) == 1, Covering = I(f, 25) == 1, Place = Unescape(f[26]), PlaceName = Unescape(f[27]),
                    Knew = I(f, 29) == 1
                };
                string[] lines = f[28].Split('~');
                for (int k = 0; k < lines.Length && c.Lines.Count < SupplierCatalogue.LinesMost; k++)
                {
                    string[] l = lines[k].Split(',');
                    string item = l.Length > 0 ? Unescape(l[0]) : string.Empty;
                    int units = I(l, 2);
                    long mills = L(l, 3);
                    if (l.Length < 4 || item.Length == 0 || units <= 0 || mills <= 0)
                    {
                        continue;
                    }
                    c.Lines.Add(new ContractLine { Item = item, Kind = SupplierCatalogue.Clamp(I(l, 1), 0, SupplierCatalogue.Kinds - 1), Units = units, Mills = mills });
                }
                if (c.Lines.Count == 0 || c.Place.Length == 0)
                {
                    continue;   // a contract for nothing or for nowhere is none
                }
                if (c.Running && c.Made >= c.Deliveries)
                {
                    c.State = ContractState.Done;
                }
                s.Contracts.Add(c);
            }
        }

        // The ended contracts past what the page keeps are let go, the oldest first; a contract still running, or with money owed on it, is never one of them.
        private static void Keep(SupplierState s)
        {
            int ended = 0;
            for (int i = 0; i < s.Contracts.Count; i++)
            {
                ended += s.Contracts[i].Running ? 0 : 1;
            }
            for (int i = 0; i < s.Contracts.Count && ended > SupplierCatalogue.ContractsKept; i++)
            {
                if (!s.Contracts[i].Running && s.OwedOn(s.Contracts[i].Id) == 0)
                {
                    s.Contracts.RemoveAt(i--);
                    ended--;
                }
            }
        }

        private static void Charges(SupplierState s, string body)
        {
            string[] records = body.Split(';');
            for (int i = 0; i < records.Length; i++)
            {
                string[] f = records[i].Split('|');
                if (f.Length >= 3)
                {
                    s.Charges[I(f, 0)] = new RivalCharge { Count = I(f, 1), Bits = I(f, 2) };
                }
            }
        }

        private static void Owed(SupplierState s, string body)
        {
            string[] records = body.Split(';');
            for (int i = 0; i < records.Length; i++)
            {
                string[] f = records[i].Split('|');
                long cents = L(f, 2);
                if (f.Length < 5 || cents <= 0 || SupplierCatalogue.Firm(I(f, 0)) == null)
                {
                    continue;
                }
                s.Owed.Add(new OwedLine { Supplier = I(f, 0), Contract = Math.Max(0, I(f, 1)), Cents = cents, Since = Math.Max(0, I(f, 3)), Due = Math.Max(0, I(f, 4)),
                    Settlement = I(f, 5) == 1, Acted = f.Length > 6 ? I(f, 6) == 1 : I(f, 5) == 1 });   // a line from before the mark was acted on if it was a settlement
            }
        }

        private static void Record(SupplierState s, string body)
        {
            string[] f = body.Split('|');
            s.Record.OnTime = Math.Max(0, I(f, 0));
            s.Record.Late = Math.Max(0, I(f, 1));
            s.Record.Defaults = Math.Max(0, I(f, 2));
            string[] days = f.Length > 3 ? f[3].Split(',') : new string[0];
            for (int i = 0; i < days.Length && s.Record.LateDays.Count < 8; i++)
            {
                if (int.TryParse(days[i], NumberStyles.Integer, Inv, out int day) && day > 0)
                {
                    s.Record.LateDays.Add(day);
                }
            }
        }

        private static void Rivals(SupplierState s, string body)
        {
            string[] records = body.Split(';');
            for (int i = 0; i < records.Length && i < s.RivalBooks.Length; i++)
            {
                string[] f = records[i].Split('|');
                RivalSupply r = s.RivalBooks[i];
                r.Month = L(f, 0);
                r.Total = L(f, 1);
                r.Failures = Math.Max(0, I(f, 2));
                r.Firm = SupplierCatalogue.Clamp(I(f, 3), 0, SupplierCatalogue.Firms.Length);
                r.MonthOf = I(f, 4, -1);
            }
        }

        private static void Notes(SupplierState s, string body)
        {
            string[] records = body.Split(';');
            for (int i = 0; i < records.Length && s.Notes.Count < SupplierCatalogue.NotesMost; i++)
            {
                string[] f = records[i].Split('|');
                if (f.Length < 5 || I(f, 0) <= 0)
                {
                    continue;
                }
                s.Notes.Add(new SupplyNote
                {
                    Day = I(f, 0), Kind = (NoteKind)SupplierCatalogue.Clamp(I(f, 1), 0, 4), Firm = SupplierCatalogue.Clamp(I(f, 2), 0, SupplierCatalogue.Firms.Length), A = I(f, 3), B = I(f, 4)
                });
            }
        }

        private static void Log(SupplierState s, string body)
        {
            string[] records = body.Split(';');
            for (int i = 0; i < records.Length && s.Log.Count < SupplierCatalogue.LogMost; i++)
            {
                string[] f = records[i].Split('|');
                if (f.Length < 4 || I(f, 1) <= 0)
                {
                    continue;
                }
                s.Log.Add(new DeliveryLog { Day = Math.Max(0, I(f, 0)), Units = I(f, 1), Place = Unescape(f[2]), Item = Unescape(f[3]) });
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
                return string.IsNullOrEmpty(text) ? string.Empty : Uri.UnescapeDataString(text);
            }
            catch (UriFormatException)
            {
                return string.Empty;
            }
        }
    }
}
