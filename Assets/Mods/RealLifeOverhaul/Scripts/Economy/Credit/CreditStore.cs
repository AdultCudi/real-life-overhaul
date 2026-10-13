using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The credit file in the save: one line of the save's modData, under <see cref="Key"/>. Game-free.
    ///
    ///     v=2~h:...~d:...~e:...~f:...~l:...~a:...~k:...~r:...
    ///
    /// Parts are divided by '~' and begin with a letter and a colon; records inside a part by ';', fields by '|'.
    ///   h  the header: the numbers of <see cref="CreditFile"/>, in the order of <see cref="Save"/>
    ///   d  the instalments of each of the last sixty days: day|on time|missed|part|test on time|test missed
    ///   e  the last 120 events: kind|day|amount|count|effect|test|source
    ///   f  the folded totals, one record per kind: count|amount
    ///   l  the loans as last seen: street|number|total|first day|remaining|payment|interest, and from version 2 what the
    ///      mod keeps about each: its standing with the lender (twelve numbers, <see cref="LadderState"/>), what is held
    ///      on it in memory with the loan's own interest and payment, its purpose and its security
    ///   a  the mod's own accounts (version 2): the numbers of <see cref="CreditAccount"/>, its standing, and three texts
    ///   k  the lender of last resort (version 2): the numbers of <see cref="SharkDebt"/>
    ///   r  the ways out of debt and an approval for finance (version 2): the numbers of <see cref="Relief"/>, the lending
    ///      bank's street, and the court's numbers of the claims still open, divided by commas
    ///
    /// VERSION 1 had the first five parts and seven fields to a loan. Its file loads as it is: there are no accounts,
    /// nothing is owed to the lender of last resort, and every loan starts in order with its lender
    /// (<see cref="Upgrade"/>).
    ///
    /// A NEWER VERSION adds fields at the end of a record or a part with a new letter; this reader takes the fields it
    /// knows and skips parts it does not, so a save written by a later version still loads (and what it does not know
    /// is not written back). A change that cannot be read that way raises <see cref="Version"/> and gets a step in
    /// <see cref="Upgrade"/>. Nothing here is the game's: the loans themselves are the game's own records, and a save
    /// loads in the plain game with this line ignored.
    /// </summary>
    public static class CreditStore
    {
        public const string Key = "reallife:economy";
        public const int Version = 2;

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>The file of this save; null (and <paramref name="fresh"/> true) if the save has none or it cannot be read.</summary>
        public static CreditFile Load(Dictionary<string, string> data, out bool fresh, out int version)
        {
            fresh = true;
            version = 0;
            string text;
            if (data == null || !data.TryGetValue(Key, out text) || string.IsNullOrEmpty(text))
            {
                return null;
            }
            try
            {
                CreditFile file = SaveLines.Checked(data, Key, Read(text, out version), version, Version);   // (a line that cannot be read is kept as it is: Core/SaveLines)
                if (file == null)
                {
                    return null;
                }
                Upgrade(file, version);
                fresh = false;
                return file;
            }
            catch (Exception)
            {
                SaveLines.Unreadable(data, Key, "it could not be read");
                return null;   // a damaged line: the file is opened afresh from what the save shows (and the line is kept as it is)
            }
        }

        public static void Save(Dictionary<string, string> data, CreditFile file)
        {
            if (data != null && file != null)
            {
                data[Key] = Write(file);
            }
        }

        /// <summary>
        /// What an older version's file needs before this version uses it. From version 1: nothing is made up. The
        /// parts it did not have are empty, the next account's number is the first, and a loan that was being missed
        /// starts its standing from nothing (the misses themselves are on the file's days, where the rating reads them).
        /// </summary>
        private static void Upgrade(CreditFile file, int from)
        {
            if (from < 2)
            {
                file.NextAccountId = Math.Max(1, file.NextAccountId);
                file.Relief.ClearApproval();
            }
        }

        public static string Write(CreditFile f)
        {
            var t = new StringBuilder(512 + f.Events.Count * 28 + f.Days.Count * 16);
            t.Append("v=").Append(Version).Append("~h:");
            Join(t, f.StartDay, f.FirstCreditDay, f.OldOnTime, f.OldMissed, f.OldPart, f.TestOffset, f.LastScore, f.LastGrade, f.SettledDay,
                f.UnseenMissed, f.UnseenGrade ? 1 : 0, f.LastMissedDay, f.LastMissedCount, f.LastMissedAmount, f.LastGradeDay, f.LastGradeWas,
                f.LegalSeen.Convictions, f.LegalSeen.Judgments, f.LegalSeen.LateDays, f.LegalSeen.Outstanding, f.LegalSeen.RecentOffences, f.NextAccountId);
            t.Append("~d:");
            for (int i = 0; i < f.Days.Count; i++)
            {
                PayDay d = f.Days[i];
                t.Append(i > 0 ? ";" : string.Empty);
                Join(t, d.Day, d.OnTime, d.Missed, d.Part, d.TestOnTime, d.TestMissed);
            }
            t.Append("~e:");
            for (int i = 0; i < f.Events.Count; i++)
            {
                CreditEvent e = f.Events[i];
                t.Append(i > 0 ? ";" : string.Empty);
                Join(t, (int)e.Kind, e.Day, e.Amount, e.Count, e.Effect, e.Test ? 1 : 0);
                t.Append('|').Append(Clean(e.Source));
            }
            t.Append("~f:");
            for (int k = 0; k < f.FoldedCount.Length; k++)
            {
                t.Append(k > 0 ? ";" : string.Empty).Append(f.FoldedCount[k].ToString(Inv)).Append('|').Append(f.FoldedAmount[k].ToString(Inv));
            }
            t.Append("~l:");
            for (int i = 0; i < f.Loans.Count; i++)
            {
                LoanMark m = f.Loans[i];
                t.Append(i > 0 ? ";" : string.Empty).Append(Clean(m.Street)).Append('|').Append(m.Number.ToString(Inv)).Append('|')
                    .Append(m.Total.ToString("R", Inv)).Append('|').Append(m.FirstDay.ToString(Inv)).Append('|')
                    .Append(m.Remaining.ToString("R", Inv)).Append('|').Append(m.Payment.ToString(Inv)).Append('|').Append(m.Interest.ToString(Inv)).Append('|');
                Standing(t, m.Ladder);
                t.Append('|');
                Join(t, m.Held, m.OwnInterest, m.OwnPayment, m.Purpose);
                t.Append('|').Append(Clean(m.Security));
            }
            t.Append("~a:");
            for (int i = 0, written = 0; i < f.Accounts.Count && written < CreditFile.AccountsMost; i++)
            {
                CreditAccount a = f.Accounts[i];
                if (a.Closed)
                {
                    continue;   // closed: forgotten at the day's end, and never kept in a save
                }
                t.Append(written++ > 0 ? ";" : string.Empty);
                Join(t, a.Id, (int)a.Kind, a.Owed, a.Agreed, a.RateBp);
                t.Append('|').Append(a.Carry.ToString(Inv)).Append('|');
                Join(t, a.OpenedDay, a.Term, a.Instalment, a.NextDay, a.DueDay, a.StatementLeft, a.MinimumLeft, a.Charged ? 1 : 0, a.Secured ? 1 : 0, a.SecurityValue,
                    a.Number, a.Frozen ? 1 : 0, a.Closed ? 1 : 0, a.Test ? 1 : 0);
                t.Append('|');
                Standing(t, a.Ladder);
                t.Append('|').Append(Clean(a.Security)).Append('|').Append(Clean(a.SecurityName)).Append('|').Append(Clean(a.Street));
            }
            SharkDebt shark = f.Shark;
            t.Append("~k:");
            Join(t, shark.Known ? 1 : 0, shark.Borrowed, shark.Owed, shark.TakenDay, shark.DueDay, shark.GrownDay, shark.Extended ? 1 : 0, shark.ActedDay, (int)shark.Waiting, shark.Test ? 1 : 0);
            Relief r = f.Relief;
            t.Append("~r:");
            Join(t, r.Arranged ? 1 : 0, r.ArrangedDay, r.ArrangedPerDay, r.ArrangedTotal, r.ArrangedPaid, r.ArrangedMisses, r.ArrangedEndDay, r.BankruptDay, r.OverdueSince,
                r.AdviceTold ? 1 : 0, r.AdviceKnown ? 1 : 0, r.CollectorKnown ? 1 : 0, r.ApprovedKind, r.ApprovedAmount, r.ApprovedDeposit, r.ApprovedTerm, r.ApprovedRateBp,
                r.ApprovedDay, r.ApprovedNumber);
            t.Append('|').Append(Clean(r.ApprovedStreet)).Append('|');
            for (int i = 0; i < r.Claims.Count; i++)
            {
                t.Append(i > 0 ? "," : string.Empty).Append(r.Claims[i].ToString(Inv));
            }
            return t.ToString();
        }

        public static CreditFile Read(string text, out int version)
        {
            version = 0;
            string[] parts = text.Split('~');
            if (parts.Length == 0 || !parts[0].StartsWith("v=", StringComparison.Ordinal) || !int.TryParse(parts[0].Substring(2), NumberStyles.Integer, Inv, out version) || version < 1)
            {
                return null;
            }
            var f = new CreditFile();
            for (int p = 1; p < parts.Length; p++)
            {
                string part = parts[p];
                if (part.Length < 2 || part[1] != ':')
                {
                    continue;
                }
                string body = part.Substring(2);
                switch (part[0])
                {
                    case 'h': Header(f, body.Split('|')); break;
                    case 'd':
                        foreach (string record in Records(body))
                        {
                            string[] c = record.Split('|');
                            if (c.Length >= 4 && I(c, 0) > 0)
                            {
                                f.Days.Add(new PayDay { Day = I(c, 0), OnTime = Count(c, 1), Missed = Count(c, 2), Part = Count(c, 3), TestOnTime = Count(c, 4), TestMissed = Count(c, 5) });
                            }
                        }
                        f.Days.Sort((a, b) => a.Day.CompareTo(b.Day));
                        break;
                    case 'e':
                        foreach (string record in Records(body))
                        {
                            string[] c = record.Split('|');
                            int kind = I(c, 0, -1);
                            if (c.Length >= 3 && kind >= 0 && kind < (int)CreditKind.Count && f.Events.Count < CreditCatalogue.LedgerMost)
                            {
                                f.Events.Add(new CreditEvent
                                {
                                    Kind = (CreditKind)kind, Day = I(c, 1), Amount = I(c, 2), Count = Math.Max(1, I(c, 3, 1)), Effect = I(c, 4),
                                    Test = I(c, 5) != 0, Source = c.Length > 6 ? c[6] : string.Empty
                                });
                            }
                        }
                        break;
                    case 'f':
                        {
                            string[] records = body.Split(';');
                            for (int k = 0; k < records.Length && k < f.FoldedCount.Length; k++)
                            {
                                string[] c = records[k].Split('|');
                                f.FoldedCount[k] = Count(c, 0);
                                long amount;
                                f.FoldedAmount[k] = c.Length > 1 && long.TryParse(c[1], NumberStyles.Integer, Inv, out amount) ? amount : 0L;
                            }
                        }
                        break;
                    case 'l':
                        foreach (string record in Records(body))
                        {
                            string[] c = record.Split('|');
                            if (c.Length >= 7)
                            {
                                var mark = new LoanMark
                                {
                                    Street = c[0], Number = I(c, 1), Total = F(c, 2), FirstDay = I(c, 3), Remaining = F(c, 4), Payment = I(c, 5), Interest = I(c, 6),
                                    Held = Count(c, 19) & (LoanHold.Penalty | LoanHold.Frozen), OwnInterest = Count(c, 20), OwnPayment = Count(c, 21),
                                    Purpose = Math.Min(Count(c, 22), LoanPurpose.Consolidation), Security = c.Length > 23 ? c[23] : string.Empty
                                };
                                Standing(mark.Ladder, c, 7);
                                f.Loans.Add(mark);
                            }
                        }
                        break;
                    case 'a':
                        foreach (string record in Records(body))
                        {
                            string[] c = record.Split('|');
                            int kind = I(c, 1, -1);
                            if (c.Length < 20 || kind < 0 || kind >= (int)ProductKind.Count || I(c, 0) <= 0 || f.Accounts.Count >= CreditFile.AccountsMost)
                            {
                                continue;
                            }
                            var a = new CreditAccount
                            {
                                Id = I(c, 0), Kind = (ProductKind)kind, Owed = Count(c, 2), Agreed = Count(c, 3), RateBp = Count(c, 4), Carry = L(c, 5),
                                OpenedDay = I(c, 6), Term = Count(c, 7), Instalment = Count(c, 8), NextDay = I(c, 9), DueDay = I(c, 10), StatementLeft = Count(c, 11),
                                MinimumLeft = Count(c, 12), Charged = I(c, 13) != 0, Secured = I(c, 14) != 0, SecurityValue = Count(c, 15), Number = I(c, 16),
                                Frozen = I(c, 17) != 0, Closed = I(c, 18) != 0, Test = I(c, 19) != 0,
                                Security = c.Length > 32 ? c[32] : string.Empty, SecurityName = c.Length > 33 ? c[33] : string.Empty, Street = c.Length > 34 ? c[34] : string.Empty
                            };
                            Standing(a.Ladder, c, 20);
                            f.Accounts.Add(a);
                        }
                        break;
                    case 'k':
                        {
                            string[] c = body.Split('|');
                            SharkDebt k = f.Shark;
                            k.Known = I(c, 0) != 0;
                            k.Borrowed = Count(c, 1);
                            k.Owed = Count(c, 2);
                            k.TakenDay = I(c, 3);
                            k.DueDay = I(c, 4);
                            k.GrownDay = I(c, 5);
                            k.Extended = I(c, 6) != 0;
                            k.ActedDay = I(c, 7);
                            int waiting = I(c, 8);
                            k.Waiting = waiting >= 0 && waiting <= (int)SharkAct.Damage ? (SharkAct)waiting : SharkAct.None;
                            k.Test = I(c, 9) != 0;
                            if (k.Borrowed <= 0 || k.Owed <= 0)
                            {
                                bool known = k.Known;
                                k.Clear();
                                k.Known = known;
                            }
                        }
                        break;
                    case 'r':
                        {
                            string[] c = body.Split('|');
                            Relief r = f.Relief;
                            r.Arranged = I(c, 0) != 0;
                            r.ArrangedDay = I(c, 1);
                            r.ArrangedPerDay = Count(c, 2);
                            r.ArrangedTotal = Count(c, 3);
                            r.ArrangedPaid = Count(c, 4);
                            r.ArrangedMisses = Count(c, 5);
                            r.ArrangedEndDay = Count(c, 6);
                            r.BankruptDay = Count(c, 7);
                            r.OverdueSince = Count(c, 8);
                            r.AdviceTold = I(c, 9) != 0;
                            r.AdviceKnown = I(c, 10) != 0;
                            r.CollectorKnown = I(c, 11) != 0;
                            int kind = I(c, 12, -1);
                            r.ApprovedKind = kind == (int)ProductKind.CarFinance || kind == (int)ProductKind.Mortgage ? kind : -1;
                            r.ApprovedAmount = Count(c, 13);
                            r.ApprovedDeposit = Count(c, 14);
                            r.ApprovedTerm = Count(c, 15);
                            r.ApprovedRateBp = Count(c, 16);
                            r.ApprovedDay = I(c, 17);
                            r.ApprovedNumber = I(c, 18);
                            r.ApprovedStreet = c.Length > 19 ? c[19] : string.Empty;
                            foreach (string claim in c.Length > 20 && c[20].Length > 0 ? c[20].Split(',') : Array.Empty<string>())
                            {
                                int number;
                                if (int.TryParse(claim, NumberStyles.Integer, Inv, out number) && number > 0 && r.Claims.Count < Relief.ClaimsMost && !r.Claims.Contains(number))
                                {
                                    r.Claims.Add(number);
                                }
                            }
                            if (!r.HasApproval)
                            {
                                r.ClearApproval();
                            }
                        }
                        break;
                }
            }
            return f;
        }

        private static void Header(CreditFile f, string[] c)
        {
            f.StartDay = I(c, 0);
            f.FirstCreditDay = Math.Max(0, I(c, 1));
            f.OldOnTime = Count(c, 2);
            f.OldMissed = Count(c, 3);
            f.OldPart = Count(c, 4);
            f.TestOffset = I(c, 5);
            f.LastScore = I(c, 6);
            f.LastGrade = I(c, 7, -1);
            f.SettledDay = I(c, 8);
            f.UnseenMissed = Count(c, 9);
            f.UnseenGrade = I(c, 10) != 0;
            f.LastMissedDay = I(c, 11);
            f.LastMissedCount = Count(c, 12);
            f.LastMissedAmount = Count(c, 13);
            f.LastGradeDay = I(c, 14);
            f.LastGradeWas = I(c, 15, -1);
            f.LegalSeen = new LegalStanding { Convictions = Count(c, 16), Judgments = Count(c, 17), LateDays = Count(c, 18), Outstanding = Count(c, 19), RecentOffences = Count(c, 20) };
            if (f.LastScore != 0)
            {
                f.LastScore = CreditCatalogue.Clamp(f.LastScore);
            }
            if (f.LastGrade < -1 || f.LastGrade > (int)CreditGrade.Excellent)
            {
                f.LastGrade = -1;
            }
            f.NextAccountId = Math.Max(1, I(c, 21, 1));
        }

        // A debt's standing with its lender: twelve numbers, in the order of LadderState.
        private static void Standing(StringBuilder t, LadderState s)
        {
            Join(t, s.Misses, s.Overdue, s.PaidRun, s.Step, s.Penalty ? 1 : 0, s.PlanLeft, s.PlanPerDay, s.PlanUsed ? 1 : 0, s.CallDue ? 1 : 0, s.RepoDay, s.RepoPending ? 1 : 0, s.ClaimNumber);
        }

        private static void Standing(LadderState s, string[] c, int at)
        {
            s.Misses = Count(c, at);
            s.Overdue = Count(c, at + 1);
            s.PaidRun = Count(c, at + 2);
            s.Step = Math.Min(Count(c, at + 3), (int)LadderStep.Claim);
            s.Penalty = I(c, at + 4) != 0;
            s.PlanLeft = Count(c, at + 5);
            s.PlanPerDay = Count(c, at + 6);
            s.PlanUsed = I(c, at + 7) != 0;
            s.CallDue = I(c, at + 8) != 0;
            s.RepoDay = Count(c, at + 9);
            s.RepoPending = I(c, at + 10) != 0;
            s.ClaimNumber = Count(c, at + 11);
        }

        private static long L(string[] c, int at)
        {
            long value;
            return at < c.Length && long.TryParse(c[at], NumberStyles.Integer, Inv, out value) ? Math.Max(0L, value) : 0L;
        }

        private static string[] Records(string body)
        {
            return body.Length == 0 ? Array.Empty<string>() : body.Split(';');
        }

        private static void Join(StringBuilder t, params int[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                t.Append(i > 0 ? "|" : string.Empty).Append(values[i].ToString(Inv));
            }
        }

        private static int I(string[] c, int at, int fallback = 0)
        {
            int value;
            return at < c.Length && int.TryParse(c[at], NumberStyles.Integer, Inv, out value) ? value : fallback;
        }

        private static int Count(string[] c, int at)
        {
            return Math.Max(0, I(c, at));
        }

        private static float F(string[] c, int at)
        {
            float value;
            return at < c.Length && float.TryParse(c[at], NumberStyles.Float, Inv, out value) && !float.IsNaN(value) && !float.IsInfinity(value) ? value : 0f;
        }

        /// <summary>A text with the three dividers taken out, so that it can never break the line apart.</summary>
        public static string Clean(string text)
        {
            return string.IsNullOrEmpty(text) ? string.Empty : text.Replace('|', ' ').Replace(';', ' ').Replace('~', ' ');
        }
    }
}
