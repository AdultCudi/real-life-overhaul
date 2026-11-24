using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Giving
{
    /// <summary>
    /// Giving in the save: one line of modData under <see cref="Key"/>, game-free.
    ///
    ///     v=1^h:...^b:...;...^s:...;...^l:...;...^p:...;...^r:...;...^n:...;...^m:...;...^c:...^u:...
    ///
    /// Parts are divided by '^' and begin with a letter and a colon; records inside a part by ';', fields by '|'. h is the header, b the causes' books, s the standing
    /// gifts, l the loans, p what was given to each person, r the rivals' giving, n the gifts the paper has still to print, m the names the player's big gifts carry, c the
    /// excess of the deduction carried forward and u the gifts to each district's upkeep. A newer version adds fields at the end of a
    /// record or a part with a new letter: this reader takes what it knows and puts every figure inside its bounds. A line from before the feature is none at all. Every
    /// number is written and read in the invariant culture, so a player whose language writes a decimal comma saves and loads the same line.
    /// </summary>
    public static class GivingStore
    {
        public const string Key = "reallife:giving";
        public const int Version = 1;

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static GivingState Load(Dictionary<string, string> data, out int version)
        {
            version = 0;
            if (data == null || !data.TryGetValue(Key, out string text) || string.IsNullOrEmpty(text))
            {
                return null;
            }
            return SaveLines.Checked(data, Key, Read(text, out version), version, Version);   // (a line that cannot be read is kept as it is: Core/SaveLines)
        }

        public static string Write(GivingState s)
        {
            var t = new StringBuilder(256 + s.Loans.Count * 48);
            t.Append("v=").Append(Version).Append("^h:");
            Join(t, s.Seed, s.LastDay, s.Month, s.PeriodStart, s.TaxGifts, s.TaxClaimed, s.StandingMonth, s.MonthGiven, s.StandingDone, s.PressDone, s.LastAsk,
                s.NeighbourMonth, s.NeighbourMoney, s.NeighbourYear, s.NeighbourHelped, s.PaperDay, s.MonthStep, s.MonthNumber, s.FoodGifts);
            t.Append("^b:");
            for (int i = 0; i < s.Books.Length; i++)
            {
                CharityBook b = s.Books[i];
                t.Append(i > 0 ? ";" : string.Empty);
                Join(t, b.Pot, b.PlayerMonth, b.OthersMonth, b.PlayerYear, b.OthersYear, b.PaidMonth, b.PaidYear, b.PeopleYear, b.LastMoney, b.LastPeople, b.LastMonth,
                    b.PlayerTotal, b.StandingTold, b.MonthPeople);
            }
            if (s.Standing.Count > 0)
            {
                t.Append("^s:");
                for (int i = 0; i < s.Standing.Count; i++)
                {
                    StandingGift g = s.Standing[i];
                    t.Append(i > 0 ? ";" : string.Empty);
                    Join(t, g.Charity, g.Amount, g.Since, g.Skipped ? 1 : 0, g.PaidTotal, g.Month, g.Tag);
                }
            }
            if (s.Loans.Count > 0)
            {
                t.Append("^l:");
                for (int i = 0; i < s.Loans.Count; i++)
                {
                    LoanOut l = s.Loans[i];
                    t.Append(i > 0 ? ";" : string.Empty);
                    Join(t, l.Person, l.Principal, l.Remaining, l.Made, l.Part, l.Misses, l.Next, l.Paid, (int)l.State, l.Closed);
                    t.Append('|').Append(Uri.EscapeDataString(l.Name ?? string.Empty)).Append('|').Append(l.Parts.ToString(Inv)).Append('|').Append(l.Extra.ToString(Inv));
                }
            }
            if (s.People.Count > 0)
            {
                t.Append("^p:");
                for (int i = 0; i < s.People.Count; i++)
                {
                    PersonGiving p = s.People[i];
                    t.Append(i > 0 ? ";" : string.Empty);
                    Join(t, p.Person, p.Start, p.Given, p.AffectionDone, p.TrustDone, p.AskDay, p.GivenTotal, p.Helps, p.HelpDay, p.LastHelp, p.RefusedDay, p.CoveredUntil, p.Nos, p.MorePending);
                }
            }
            t.Append("^r:");
            for (int i = 0; i < s.RivalBooks.Length; i++)
            {
                RivalGiving r = s.RivalBooks[i];
                t.Append(i > 0 ? ";" : string.Empty);
                Join(t, r.Total, r.Year, r.LastDay, r.Last, r.LastCharity, r.Month);
            }
            if (s.Notes.Count > 0)
            {
                t.Append("^n:");
                for (int i = 0; i < s.Notes.Count; i++)
                {
                    GivingNote n = s.Notes[i];
                    t.Append(i > 0 ? ";" : string.Empty);
                    Join(t, n.Day, n.Charity, n.Amount, n.Giver, n.Naming, n.Tag);
                }
            }
            if (s.Namings.Count > 0)
            {
                t.Append("^m:");
                for (int i = 0; i < s.Namings.Count; i++)
                {
                    GivingNaming m = s.Namings[i];
                    t.Append(i > 0 ? ";" : string.Empty);
                    Join(t, m.Day, m.Charity, m.Amount, m.Tag);
                    t.Append('|').Append(Uri.EscapeDataString(m.Name ?? string.Empty));
                }
            }
            bool carried = s.CarryTaken > 0;
            for (int i = 0; i < s.Carry.Length; i++)
            {
                carried |= s.Carry[i] > 0;
            }
            if (carried)
            {
                t.Append("^c:");
                Join(t, s.CarryTaken);
                for (int i = 0; i < s.Carry.Length; i++)
                {
                    t.Append('|').Append(s.Carry[i].ToString(Inv));
                }
            }
            bool upkept = false;
            for (int i = 0; i < s.UpkeepTotal.Length; i++)
            {
                upkept |= s.UpkeepTotal[i] > 0;
            }
            if (upkept)
            {
                t.Append("^u:");
                Join(t, s.UpkeepTotal);
            }
            return t.ToString();
        }

        /// <summary>Reads a line. Null for one that is not this feature's at all.</summary>
        public static GivingState Read(string text, out int version)
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
            var s = new GivingState();
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
                    case 'b': Books(s, body); break;
                    case 's': Standing(s, body); break;
                    case 'l': Loans(s, body); break;
                    case 'p': People(s, body); break;
                    case 'r': Rivals(s, body); break;
                    case 'n': Notes(s, body); break;
                    case 'm': Namings(s, body); break;
                    case 'c': Carry(s, body); break;
                    case 'u': Upkeep(s, body); break;
                }
            }
            s.TaxClaimed = Math.Min(s.TaxClaimed, s.TaxGifts + s.CarryTaken);   // what is claimed never passes the gifts it was claimed for
            return s;
        }

        private static void Header(GivingState s, string body)
        {
            string[] f = body.Split('|');
            s.Seed = I(f, 0);
            s.LastDay = Math.Max(0, I(f, 1));
            s.Month = I(f, 2, -1);
            s.PeriodStart = Math.Max(-1, I(f, 3, -1));
            s.TaxGifts = Math.Max(0, L(f, 4));
            s.TaxClaimed = Math.Max(0, L(f, 5));   // held to the gifts and the carry once the whole line is read
            s.StandingMonth = I(f, 6, -1);
            s.MonthGiven = Math.Max(0, L(f, 7));
            s.StandingDone = Math.Max(0, I(f, 8));
            s.PressDone = Math.Max(0, I(f, 9));
            s.LastAsk = Math.Max(0, I(f, 10));
            s.NeighbourMonth = I(f, 11);
            s.NeighbourMoney = Math.Max(0, L(f, 12));
            s.NeighbourYear = Math.Max(0, L(f, 13));
            s.NeighbourHelped = Math.Max(0, I(f, 14));
            s.PaperDay = Math.Max(0, I(f, 15));
            int step = I(f, 16, -1);
            s.MonthStep = step >= 0 && step < GivingMonth.Steps ? step : -1;   // a line from before the month went by steps has no pass under way
            s.MonthNumber = Math.Max(0, I(f, 17));
            s.FoodGifts = Math.Max(0, L(f, 18));   // a line from before food gifts has none
        }

        private static void Books(GivingState s, string body)
        {
            string[] records = body.Split(';');
            for (int i = 0; i < records.Length && i < s.Books.Length; i++)
            {
                string[] f = records[i].Split('|');
                CharityBook b = s.Books[i];
                b.Pot = Math.Max(0, L(f, 0));
                b.PlayerMonth = Math.Max(0, L(f, 1));
                b.OthersMonth = Math.Max(0, L(f, 2));
                b.PlayerYear = Math.Max(0, L(f, 3));
                b.OthersYear = Math.Max(0, L(f, 4));
                b.PaidMonth = Math.Max(0, L(f, 5));
                b.PaidYear = Math.Max(0, L(f, 6));
                b.PeopleYear = Math.Max(0, I(f, 7));
                b.LastMoney = Math.Max(0, L(f, 8));
                b.LastPeople = Math.Max(0, I(f, 9));
                b.LastMonth = I(f, 10, -1);
                b.PlayerTotal = Math.Max(0, L(f, 11));
                b.StandingTold = Math.Max(0, I(f, 12));
                b.MonthPeople = Math.Max(0, I(f, 13));
            }
        }

        private static void Standing(GivingState s, string body)
        {
            string[] records = body.Split(';');
            for (int i = 0; i < records.Length && s.Standing.Count < GivingCatalogue.StandingMost; i++)
            {
                string[] f = records[i].Split('|');
                int charity = I(f, 0, -1);
                if (charity < 0 || charity >= (int)Charity.Count || I(f, 1) <= 0 || s.StandingOf(charity) != null)
                {
                    continue;
                }
                int tag = I(f, 6, -1);
                s.Standing.Add(new StandingGift
                {
                    Charity = charity, Amount = I(f, 1), Since = I(f, 2), Skipped = I(f, 3) == 1, PaidTotal = Math.Max(0, L(f, 4)), Month = I(f, 5, -1),
                    Tag = tag >= 0 && tag < GivingState.Districts ? tag : -1
                });
            }
        }

        private static void Loans(GivingState s, string body)
        {
            string[] records = body.Split(';');
            for (int i = 0; i < records.Length; i++)
            {
                string[] f = records[i].Split('|');
                if (f.Length < 11 || I(f, 1) <= 0)
                {
                    continue;
                }
                int state = GivingCatalogue.Clamp(I(f, 8), 0, 2);
                // (what is owed may pass what was lent by the share a business loan adds on top)
                s.Loans.Add(new LoanOut
                {
                    Person = I(f, 0), Principal = I(f, 1), Remaining = GivingCatalogue.Clamp(I(f, 2), 0, I(f, 1) + Math.Max(0, I(f, 12))), Made = I(f, 3), Part = Math.Max(1, I(f, 4)),
                    Misses = Math.Max(0, I(f, 5)), Next = I(f, 6), Paid = Math.Max(0, I(f, 7)), State = (LoanState)state, Closed = I(f, 9), Name = Unescape(f[10]),
                    Parts = I(f, 11) <= 0 ? GivingCatalogue.LoanParts : GivingCatalogue.Clamp(I(f, 11), 1, GivingCatalogue.BusinessParts), Extra = Math.Max(0, I(f, 12))
                });
            }
        }

        private static void People(GivingState s, string body)
        {
            string[] records = body.Split(';');
            for (int i = 0; i < records.Length; i++)
            {
                string[] f = records[i].Split('|');
                if (f.Length < 7)
                {
                    continue;
                }
                s.People.Add(new PersonGiving
                {
                    Person = I(f, 0), Start = I(f, 1), Given = Math.Max(0, I(f, 2)), AffectionDone = Math.Max(0, I(f, 3)), TrustDone = Math.Max(0, I(f, 4)),
                    AskDay = Math.Max(0, I(f, 5)), GivenTotal = Math.Max(0, I(f, 6)), Helps = GivingCatalogue.Clamp(I(f, 7), 0, 1000), HelpDay = Math.Max(0, I(f, 8)),
                    LastHelp = Math.Max(0, I(f, 9)), RefusedDay = Math.Max(0, I(f, 10)), CoveredUntil = Math.Max(0, I(f, 11)), Nos = GivingCatalogue.Clamp(I(f, 12), 0, 1000),
                    MorePending = I(f, 13) == 1 ? 1 : 0
                });
            }
        }

        private static void Rivals(GivingState s, string body)
        {
            string[] records = body.Split(';');
            for (int i = 0; i < records.Length && i < s.RivalBooks.Length; i++)
            {
                string[] f = records[i].Split('|');
                RivalGiving r = s.RivalBooks[i];
                r.Total = Math.Max(0, L(f, 0));
                r.Year = Math.Max(0, L(f, 1));
                r.LastDay = Math.Max(0, I(f, 2));
                r.Last = Math.Max(0, L(f, 3));
                r.LastCharity = GivingCatalogue.Clamp(I(f, 4), 0, (int)Charity.Count - 1);
                r.Month = I(f, 5, -1);
            }
        }

        private static void Notes(GivingState s, string body)
        {
            string[] records = body.Split(';');
            for (int i = 0; i < records.Length; i++)
            {
                string[] f = records[i].Split('|');
                if (f.Length < 4 || L(f, 2) <= 0)
                {
                    continue;
                }
                int charity = GivingCatalogue.Clamp(I(f, 1), 0, (int)Charity.Count - 1), giver = GivingCatalogue.Clamp(I(f, 3), -1, GivingState.Rivals - 1);
                int tag = I(f, 5, -1);
                s.Notes.Add(new GivingNote
                {
                    Day = I(f, 0), Charity = charity, Amount = L(f, 2), Giver = giver, Naming = I(f, 4) == 1 ? 1 : 0, Tag = tag >= 0 && tag < GivingState.Districts ? tag : -1
                });
            }
        }

        private static void Namings(GivingState s, string body)
        {
            string[] records = body.Split(';');
            for (int i = 0; i < records.Length && s.Namings.Count < GivingCatalogue.NamingsKept; i++)
            {
                string[] f = records[i].Split('|');
                if (f.Length < 5 || L(f, 2) <= 0)
                {
                    continue;
                }
                int tag = I(f, 3, -1);
                s.Namings.Add(new GivingNaming
                {
                    Day = I(f, 0), Charity = GivingCatalogue.Clamp(I(f, 1), 0, (int)Charity.Count - 1), Amount = L(f, 2), Name = Unescape(f[4]),
                    Tag = tag >= 0 && tag < GivingState.Districts ? tag : -1
                });
            }
        }

        private static void Carry(GivingState s, string body)
        {
            string[] f = body.Split('|');
            s.CarryTaken = Math.Max(0, L(f, 0));
            for (int i = 0; i < s.Carry.Length; i++)
            {
                s.Carry[i] = Math.Max(0, L(f, i + 1));
            }
        }

        private static void Upkeep(GivingState s, string body)
        {
            string[] f = body.Split('|');
            for (int i = 0; i < s.UpkeepTotal.Length; i++)
            {
                s.UpkeepTotal[i] = Math.Max(0, L(f, i));
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
