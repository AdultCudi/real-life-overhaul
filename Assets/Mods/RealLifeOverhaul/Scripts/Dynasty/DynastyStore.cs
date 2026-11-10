using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Dynasty
{
    /// <summary>
    /// The dynasty in the save: one line of modData under <see cref="Key"/>, game-free.
    ///
    ///     v=1^h:...^w:...^b:...;...^g:...;...^x:...^t:...;...^k:...;...^e:...^l:...^r:...;...^c:...^y:...;...^n:...;...^o:...;...
    ///
    /// Parts are divided by '^' and begin with a letter and a colon; records inside a part by ';', fields by '|'; a statement's bequests by ',' and ':' inside its last
    /// field. h is the header, w the will, b its bequests, g the dated gifts, x a handover under way (its step, then its statement), t the statements kept, k the family's
    /// ledger, e the retired founder, l the debt for tax, r the rivals' lines, c the city's estates, y the lineage, n the paper's pieces, o the grown children's days. A newer
    /// version adds fields at the end of a record or a part with a new letter: this reader takes what it knows and puts every figure inside its bounds. A line from before the
    /// feature is none at all. Every number is written and read in the invariant culture, and every name is escaped, so no name can break a record.
    /// </summary>
    public static class DynastyStore
    {
        public const string Key = "reallife:dynasty";
        public const int Version = 1;
        private const int StatementFields = 30;

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static DynastyState Load(Dictionary<string, string> data, out int version)
        {
            version = 0;
            if (data == null || !data.TryGetValue(Key, out string text) || string.IsNullOrEmpty(text))
            {
                return null;
            }
            return SaveLines.Checked(data, Key, Read(text, out version), version, Version);   // (a line that cannot be read is kept as it is: Core/SaveLines)
        }

        public static string Write(DynastyState s)
        {
            var t = new StringBuilder(512);
            t.Append("v=").Append(Version).Append("^h:");
            Join(t, s.Seed, s.LastDay, s.Generation, s.RetireTold, s.HeldTold, s.PaperDay);
            Will w = s.Will;
            t.Append("^w:");
            Join(t, w.Written ? 1 : 0, w.Day, (int)w.HeirKind, w.HeirId, w.RetireAge, w.Keep, w.Fees);
            t.Append('|').Append(Esc(w.HeirName)).Append('|').Append(Esc(w.Firm));
            if (w.Bequests.Count > 0)
            {
                t.Append("^b:");
                for (int i = 0; i < w.Bequests.Count; i++)
                {
                    Bequest b = w.Bequests[i];
                    t.Append(i > 0 ? ";" : string.Empty);
                    Join(t, (int)b.Kind, b.Id, b.Share);
                    t.Append('|').Append(Esc(b.Name));
                }
            }
            if (s.Gifts.Count > 0)
            {
                t.Append("^g:");
                for (int i = 0; i < s.Gifts.Count; i++)
                {
                    t.Append(i > 0 ? ";" : string.Empty);
                    Join(t, s.Gifts[i].Day, s.Gifts[i].Amount);
                }
            }
            if (s.Hand != null)
            {
                t.Append("^x:");
                Join(t, s.Hand.Step, s.Hand.LoanIn ? 1 : 0, s.Hand.TaxOut ? 1 : 0, s.Hand.KeepOut ? 1 : 0, s.Hand.Passed ? 1 : 0);
                t.Append('|');
                WriteStatement(t, s.Hand.S);
            }
            if (s.Statements.Count > 0)
            {
                t.Append("^t:");
                for (int i = 0; i < s.Statements.Count; i++)
                {
                    t.Append(i > 0 ? ";" : string.Empty);
                    WriteStatement(t, s.Statements[i]);
                }
            }
            if (s.Kin.Count > 0)
            {
                t.Append("^k:");
                for (int i = 0; i < s.Kin.Count; i++)
                {
                    t.Append(i > 0 ? ";" : string.Empty);
                    Join(t, (int)s.Kin[i].Kind, s.Kin[i].Held, s.Kin[i].Day);
                    t.Append('|').Append(Esc(s.Kin[i].Name));
                }
            }
            if (s.Elder != null)
            {
                Elder e = s.Elder;
                t.Append("^e:");
                Join(t, e.Born, e.Since, e.Fund, e.Draw, e.Taxable, e.Tax, e.Month);
                t.Append('|').Append(Esc(e.Name));
            }
            EstateLoan l = s.Loan;
            if (l.Principal > 0 || l.Owed > 0)
            {
                t.Append("^l:");
                Join(t, l.Principal, l.Owed, l.PerDay, l.Since, l.LastDay, l.Late, l.PaidCents);
            }
            t.Append("^r:");
            for (int i = 0; i < s.RivalLines.Length; i++)
            {
                RivalLine r = s.RivalLines[i];
                t.Append(i > 0 ? ";" : string.Empty);
                Join(t, r.Born, r.RetireAge, r.DeathAge, r.Generation, r.HandDay, (int)r.HandMode, r.HandWorth, r.HandTax, r.ElderBorn, r.ElderDeath);
            }
            CityEstates c = s.City;
            t.Append("^c:");
            Join(t, c.Year, c.Count, c.Taxed, c.Value, c.Tax, c.Unpaid, c.LastCount, c.LastTaxed, c.LastValue, c.LastTax, c.LastUnpaid);
            if (s.Line.Count > 0)
            {
                t.Append("^y:");
                for (int i = 0; i < s.Line.Count; i++)
                {
                    LineEntry y = s.Line[i];
                    t.Append(i > 0 ? ";" : string.Empty);
                    Join(t, y.Generation, y.Day, (int)y.Mode, y.Net, y.Tax);
                    t.Append('|').Append(Esc(y.Name));
                }
            }
            if (s.Notes.Count > 0)
            {
                t.Append("^n:");
                for (int i = 0; i < s.Notes.Count; i++)
                {
                    DynastyNote n = s.Notes[i];
                    t.Append(i > 0 ? ";" : string.Empty);
                    Join(t, n.Day, n.Who, (int)n.Mode, n.Generation, n.Tax);
                    t.Append('|').Append(Esc(n.From)).Append('|').Append(Esc(n.To));
                }
            }
            if (s.Grown.Count > 0)
            {
                t.Append("^o:");
                for (int i = 0; i < s.Grown.Count; i++)
                {
                    t.Append(i > 0 ? ";" : string.Empty).Append(s.Grown[i].Day.ToString(Inv)).Append('|').Append(Esc(s.Grown[i].Name));
                }
            }
            return t.ToString();
        }

        private static void WriteStatement(StringBuilder t, Statement s)
        {
            Join(t, s.Day, (int)s.Mode, s.Generation, (int)s.HeirKind, s.HeirId, s.HeirAge, s.Upbringing, s.ByLaw ? 1 : 0, s.SpouseHeir ? 1 : 0, s.Level, s.Cash, s.Reserved,
                s.Other, s.Net, s.Keep, s.Gifts, s.Charity, s.Spouse, s.Prior, s.Taxable, s.Tax, s.TaxCash, s.Loan, s.Paid, s.Residue, s.Scale, s.UnderWater ? 1 : 0);
            t.Append('|').Append(Esc(s.From)).Append('|').Append(Esc(s.To)).Append('|');
            for (int i = 0; i < s.Lines.Count; i++)
            {
                BequestLine b = s.Lines[i];
                t.Append(i > 0 ? "," : string.Empty).Append(((int)b.Kind).ToString(Inv)).Append(':').Append(b.Id.ToString(Inv)).Append(':').Append(b.Share.ToString(Inv)).Append(':')
                    .Append(b.Asked.ToString(Inv)).Append(':').Append(b.Amount.ToString(Inv)).Append(':').Append(((int)b.Landing).ToString(Inv)).Append(':').Append(b.Paid ? '1' : '0')
                    .Append(':').Append(Esc(b.Name));
            }
        }

        /// <summary>Reads a line. Null for one that is not this feature's at all.</summary>
        public static DynastyState Read(string text, out int version)
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
            var s = new DynastyState();
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
                    case 'w': WillOf(s, body); break;
                    case 'b': Bequests(s, body); break;
                    case 'g': Gifts(s, body); break;
                    case 'x': Hand(s, body); break;
                    case 't': Statements(s, body); break;
                    case 'k': KinOf(s, body); break;
                    case 'e': ElderOf(s, body); break;
                    case 'l': Loan(s, body); break;
                    case 'r': Rivals(s, body); break;
                    case 'c': City(s, body); break;
                    case 'y': Lineage(s, body); break;
                    case 'n': Notes(s, body); break;
                    case 'o': Grown(s, body); break;
                }
            }
            DynastyRules.Tidy(s.Will);   // after the bequests are read: a will that cannot stand loses the bequests that break it, never its heir
            return s;
        }

        private static void Header(DynastyState s, string body)
        {
            string[] f = body.Split('|');
            s.Seed = I(f, 0);
            s.LastDay = Math.Max(0, I(f, 1));
            s.Generation = Math.Max(1, I(f, 2, 1));
            s.RetireTold = Math.Max(0, I(f, 3));
            s.HeldTold = Math.Max(0, I(f, 4));
            s.PaperDay = Math.Max(0, I(f, 5));
        }

        private static void WillOf(DynastyState s, string body)
        {
            string[] f = body.Split('|');
            Will w = s.Will;
            w.Written = I(f, 0) != 0;
            w.Day = Math.Max(0, I(f, 1));
            w.HeirKind = (KinKind)DynastyCatalogue.Clamp(I(f, 2), 0, (int)KinKind.Person);
            w.HeirId = I(f, 3);
            w.RetireAge = I(f, 4, w.RetireAge);
            w.Keep = I(f, 5);
            w.Fees = Math.Max(0L, L(f, 6));
            w.HeirName = Unesc(f, 7);
            w.Firm = Unesc(f, 8);
        }

        private static void Bequests(DynastyState s, string body)
        {
            string[] records = body.Split(';');
            for (int i = 0; i < records.Length && s.Will.Bequests.Count < DynastyCatalogue.BequestsMost; i++)
            {
                string[] f = records[i].Split('|');
                int kind = I(f, 0);
                if (f.Length < 3 || kind < (int)KinKind.Partner || kind > (int)KinKind.Charity)
                {
                    continue;
                }
                s.Will.Bequests.Add(new Bequest { Kind = (KinKind)kind, Id = I(f, 1), Share = I(f, 2), Name = Unesc(f, 3) });
            }
        }

        private static void Gifts(DynastyState s, string body)
        {
            string[] records = body.Split(';');
            for (int i = 0; i < records.Length && s.Gifts.Count < DynastyCatalogue.GiftsKept; i++)
            {
                string[] f = records[i].Split('|');
                if (f.Length >= 2 && L(f, 1) > 0)
                {
                    s.Gifts.Add(new LifeGift { Day = I(f, 0), Amount = L(f, 1) });
                }
            }
        }

        private static void Hand(DynastyState s, string body)
        {
            string[] f = body.Split('|');
            if (f.Length < 5 + StatementFields - 3)
            {
                return;
            }
            var h = new Handover { Step = Math.Max(0, I(f, 0)), LoanIn = I(f, 1) != 0, TaxOut = I(f, 2) != 0, KeepOut = I(f, 3) != 0, Passed = I(f, 4) != 0 };
            ReadStatement(h.S, f, 5);
            s.Hand = h;
        }

        private static void Statements(DynastyState s, string body)
        {
            string[] records = body.Split(';');
            for (int i = 0; i < records.Length; i++)
            {
                string[] f = records[i].Split('|');
                if (f.Length < StatementFields - 3)
                {
                    continue;
                }
                var st = new Statement();
                ReadStatement(st, f, 0);
                s.Keep(st);
            }
        }

        private static void ReadStatement(Statement s, string[] f, int at)
        {
            s.Day = I(f, at);
            s.Mode = (HandMode)DynastyCatalogue.Clamp(I(f, at + 1), 0, (int)HandMode.Elder);
            s.Generation = Math.Max(1, I(f, at + 2, 1));
            s.HeirKind = (KinKind)DynastyCatalogue.Clamp(I(f, at + 3), 0, (int)KinKind.Person);
            s.HeirId = I(f, at + 4);
            s.HeirAge = DynastyCatalogue.Clamp(I(f, at + 5), 0, 120);
            s.Upbringing = DynastyCatalogue.Clamp(I(f, at + 6), 0, 1000);
            s.ByLaw = I(f, at + 7) != 0;
            s.SpouseHeir = I(f, at + 8) != 0;
            s.Level = DynastyCatalogue.Clamp(I(f, at + 9, PriceRules.Start), PriceRules.Least, PriceRules.Most);
            s.Cash = Math.Max(0L, L(f, at + 10));
            s.Reserved = Math.Max(0L, L(f, at + 11));
            s.Other = L(f, at + 12);
            s.Net = L(f, at + 13);
            s.Keep = Math.Max(0L, L(f, at + 14));
            s.Gifts = Math.Max(0L, L(f, at + 15));
            s.Charity = Math.Max(0L, L(f, at + 16));
            s.Spouse = Math.Max(0L, L(f, at + 17));
            s.Prior = Math.Max(0L, L(f, at + 18));
            s.Taxable = Math.Max(0L, L(f, at + 19));
            s.Tax = Math.Max(0L, L(f, at + 20));
            s.TaxCash = Math.Max(0L, L(f, at + 21));
            s.Loan = Math.Max(0L, L(f, at + 22));
            s.Paid = Math.Max(0L, L(f, at + 23));
            s.Residue = L(f, at + 24);
            s.Scale = DynastyCatalogue.Clamp(I(f, at + 25, 1000), 0, 1000);
            s.UnderWater = I(f, at + 26) != 0;
            s.From = Unesc(f, at + 27);
            s.To = Unesc(f, at + 28);
            string lines = at + 29 < f.Length ? f[at + 29] : string.Empty;
            if (lines.Length == 0)
            {
                return;
            }
            string[] records = lines.Split(',');
            for (int i = 0; i < records.Length && s.Lines.Count < DynastyCatalogue.BequestsMost; i++)
            {
                string[] b = records[i].Split(':');
                if (b.Length < 7)
                {
                    continue;
                }
                s.Lines.Add(new BequestLine
                {
                    Kind = (KinKind)DynastyCatalogue.Clamp(I(b, 0), 0, (int)KinKind.Charity), Id = I(b, 1), Share = I(b, 2), Asked = Math.Max(0L, L(b, 3)), Amount = Math.Max(0L, L(b, 4)),
                    Landing = (Landing)DynastyCatalogue.Clamp(I(b, 5), 0, (int)Landing.Pot), Paid = I(b, 6) != 0, Name = Unesc(b, 7)
                });
            }
        }

        private static void KinOf(DynastyState s, string body)
        {
            string[] records = body.Split(';');
            for (int i = 0; i < records.Length && s.Kin.Count < DynastyCatalogue.KinKept; i++)
            {
                string[] f = records[i].Split('|');
                if (f.Length >= 4)
                {
                    s.Kin.Add(new Kin { Kind = (KinKind)DynastyCatalogue.Clamp(I(f, 0), 0, (int)KinKind.Person), Held = Math.Max(0L, L(f, 1)), Day = I(f, 2), Name = Unesc(f, 3) });
                }
            }
        }

        private static void ElderOf(DynastyState s, string body)
        {
            string[] f = body.Split('|');
            if (f.Length < 7)
            {
                return;
            }
            s.Elder = new Elder
            {
                Born = I(f, 0), Since = I(f, 1), Fund = Math.Max(0L, L(f, 2)), Draw = Math.Max(0L, L(f, 3)), Taxable = Math.Max(0L, L(f, 4)), Tax = Math.Max(0L, L(f, 5)),
                Month = I(f, 6, -1), Name = Unesc(f, 7)
            };
        }

        private static void Loan(DynastyState s, string body)
        {
            string[] f = body.Split('|');
            EstateLoan l = s.Loan;
            l.Principal = Math.Max(0L, L(f, 0));
            l.Owed = Math.Max(0L, L(f, 1));
            l.PerDay = Math.Max(l.Owed > 0 ? 1L : 0L, L(f, 2));
            l.Since = Math.Max(0, I(f, 3));
            l.LastDay = Math.Max(0, I(f, 4));
            l.Late = Math.Max(0, I(f, 5));
            l.PaidCents = Math.Max(0L, L(f, 6));
        }

        private static void Rivals(DynastyState s, string body)
        {
            string[] records = body.Split(';');
            for (int i = 0; i < records.Length && i < s.RivalLines.Length; i++)
            {
                string[] f = records[i].Split('|');
                if (f.Length < 4)
                {
                    continue;
                }
                RivalLine r = s.RivalLines[i];
                r.Born = I(f, 0, int.MinValue);
                r.RetireAge = Math.Max(0, I(f, 1));
                r.DeathAge = Math.Max(0, I(f, 2));
                r.Generation = Math.Max(1, I(f, 3, 1));
                r.HandDay = I(f, 4, -1);
                r.HandMode = (HandMode)DynastyCatalogue.Clamp(I(f, 5), 0, (int)HandMode.Retire);
                r.HandWorth = Math.Max(0L, L(f, 6));
                r.HandTax = Math.Max(0L, L(f, 7));
                r.ElderBorn = I(f, 8, int.MinValue);
                r.ElderDeath = Math.Max(0, I(f, 9));
            }
        }

        private static void City(DynastyState s, string body)
        {
            string[] f = body.Split('|');
            CityEstates c = s.City;
            c.Year = I(f, 0, -1);
            c.Count = Math.Max(0, I(f, 1));
            c.Taxed = Math.Max(0, I(f, 2));
            c.Value = Math.Max(0L, L(f, 3));
            c.Tax = Math.Max(0L, L(f, 4));
            c.Unpaid = Math.Max(0L, L(f, 5));
            c.LastCount = Math.Max(0, I(f, 6));
            c.LastTaxed = Math.Max(0, I(f, 7));
            c.LastValue = Math.Max(0L, L(f, 8));
            c.LastTax = Math.Max(0L, L(f, 9));
            c.LastUnpaid = Math.Max(0L, L(f, 10));
        }

        private static void Lineage(DynastyState s, string body)
        {
            string[] records = body.Split(';');
            for (int i = 0; i < records.Length && s.Line.Count < DynastyCatalogue.LineKept; i++)
            {
                string[] f = records[i].Split('|');
                if (f.Length >= 6)
                {
                    s.Line.Add(new LineEntry
                    {
                        Generation = Math.Max(1, I(f, 0, 1)), Day = I(f, 1), Mode = (HandMode)DynastyCatalogue.Clamp(I(f, 2), 0, (int)HandMode.Elder), Net = L(f, 3),
                        Tax = Math.Max(0L, L(f, 4)), Name = Unesc(f, 5)
                    });
                }
            }
        }

        private static void Notes(DynastyState s, string body)
        {
            string[] records = body.Split(';');
            for (int i = 0; i < records.Length && s.Notes.Count < DynastyCatalogue.NotesKept; i++)
            {
                string[] f = records[i].Split('|');
                if (f.Length >= 5)
                {
                    s.Notes.Add(new DynastyNote
                    {
                        Day = I(f, 0), Who = DynastyCatalogue.Clamp(I(f, 1), -1, DynastyState.Rivals - 1), Mode = (HandMode)DynastyCatalogue.Clamp(I(f, 2), 0, (int)HandMode.Elder),
                        Generation = Math.Max(1, I(f, 3, 1)), Tax = Math.Max(0L, L(f, 4)), From = Unesc(f, 5), To = Unesc(f, 6)
                    });
                }
            }
        }

        private static void Grown(DynastyState s, string body)
        {
            string[] records = body.Split(';');
            for (int i = 0; i < records.Length && s.Grown.Count < 12; i++)
            {
                string[] f = records[i].Split('|');
                if (f.Length >= 2 && Unesc(f, 1).Length > 0)
                {
                    s.Grown.Add(new GrownChild { Day = I(f, 0), Name = Unesc(f, 1) });
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

        private static string Esc(string text) => Uri.EscapeDataString(text ?? string.Empty);

        private static string Unesc(string[] f, int at)
        {
            if (at >= f.Length || string.IsNullOrEmpty(f[at]))
            {
                return string.Empty;
            }
            try
            {
                return Uri.UnescapeDataString(f[at]);
            }
            catch (UriFormatException)
            {
                return string.Empty;
            }
        }
    }
}
