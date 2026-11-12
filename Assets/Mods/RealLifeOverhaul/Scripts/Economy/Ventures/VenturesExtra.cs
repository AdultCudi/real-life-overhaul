using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Economy.Ventures
{
    /// <summary>What an investor asks to be kept besides the profit target. Saved as a number.</summary>
    public enum PromiseKind { Firms = 0, Morale = 1, Report = 2, Count = 3 }

    /// <summary>How a deal may be bought back after its term. Saved as a number.</summary>
    public enum BuyBackMode { None = 0, Optional = 1, Callable = 2 }

    /// <summary>
    /// A promise made to an investor that is checked once a month: to run so many firms by a day, to keep the team's morale at a floor until a day, or to send a report each
    /// quarter. A report promise stays open and is judged at the end of each quarter; the others end on their day, kept or broken.
    /// </summary>
    public sealed class Promise
    {
        public int Id, DealId;
        public PromiseKind Kind;
        public int Target;       // firms to run, or the morale floor, or 0 for a report
        public int Due;          // the day it is judged (the end of the quarter for a report)
        public int State;        // 0 open, 1 kept, 2 broken (a report stays open)
        public int Kept, Missed; // a report promise's quarters kept and missed; the others count their checks in Missed
        public int Checked;      // the month index last looked at
        public int ReportDay;    // the day the last report was sent
    }

    /// <summary>The term a deal carries to buy the investor back after so many days, at so many tenths of the money put in.</summary>
    public sealed class DealTerm
    {
        public int DealId;
        public int Day;          // from this day
        public int Times10;      // 15 is one and a half times the money, less what was paid out
        public BuyBackMode Mode;
    }

    /// <summary>A thing other investors heard: one who lost the founder's money, or a promise broken, and the day it got round.</summary>
    public sealed class Heard
    {
        public int InvestorId, Day, Kind;   // Kind: 0 left unpaid, 1 a promise broken
    }

    /// <summary>
    /// What Ventures keeps besides the investors, offers and deals (<see cref="VenturesFile"/>): the promises checked each month, the buy-back terms, the ring of monthly
    /// valuations and what the other investors heard. Its own save key, so an old save loads as it was and carries none of these.
    /// </summary>
    public sealed class VenturesExtra
    {
        public const int RingMonths = 12, HeardMost = 12, PromisesMost = 24, TermsMost = 12;

        public int NextId = 1;
        public readonly List<Promise> Promises = new List<Promise>(8);
        public readonly List<DealTerm> Terms = new List<DealTerm>(4);
        public readonly List<Heard> Heards = new List<Heard>(4);
        public readonly int[] Valuation = new int[RingMonths];   // the player's firm, implied by the deals, oldest first; 0: no deal that month
        public int ValuationCount;
        public int MonthDone = -1;                               // the month index the monthly look last ran for

        public DealTerm TermOf(int dealId)
        {
            for (int i = 0; i < Terms.Count; i++)
            {
                if (Terms[i].DealId == dealId)
                {
                    return Terms[i];
                }
            }
            return null;
        }

        public void Remember(int value)
        {
            if (ValuationCount < RingMonths)
            {
                Valuation[ValuationCount++] = value;
                return;
            }
            for (int i = 1; i < RingMonths; i++)
            {
                Valuation[i - 1] = Valuation[i];
            }
            Valuation[RingMonths - 1] = value;
        }

        public void Hear(int investorId, int day, int kind)
        {
            if (Heards.Count >= HeardMost)
            {
                Heards.RemoveAt(0);
            }
            Heards.Add(new Heard { InvestorId = investorId, Day = day, Kind = kind });
        }

        /// <summary>Whatever points at a deal that is gone is dropped.</summary>
        public void Prune(VenturesFile file)
        {
            Promises.RemoveAll(p => file.Deal(p.DealId) == null);
            Terms.RemoveAll(t => file.Deal(t.DealId) == null);
        }
    }

    /// <summary>The extras in the save, game-free: one line of modData, parts by '~' (t, p, v, h), records by ';', fields by '|'. A later version's fields are skipped.</summary>
    public static class VenturesExtraStore
    {
        public const string Key = "reallife:ventures_extra";
        public const int Version = 1;

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>The extras of this save; an empty set if it has none (an older save) or the line cannot be read.</summary>
        public static VenturesExtra Load(Dictionary<string, string> data)
        {
            string text;
            if (data == null || !data.TryGetValue(Key, out text) || string.IsNullOrEmpty(text))
            {
                return new VenturesExtra();
            }
            try
            {
                return Read(text) ?? new VenturesExtra();
            }
            catch (Exception)
            {
                return new VenturesExtra();
            }
        }

        public static void Save(Dictionary<string, string> data, VenturesExtra x)
        {
            if (data != null && x != null)
            {
                data[Key] = Write(x);
            }
        }

        public static string Write(VenturesExtra x)
        {
            var t = new StringBuilder(256);
            t.Append("v=").Append(Version).Append("~t:");
            for (int i = 0; i < x.Terms.Count && i < VenturesExtra.TermsMost; i++)
            {
                DealTerm d = x.Terms[i];
                t.Append(i > 0 ? ";" : string.Empty);
                Join(t, d.DealId, d.Day, d.Times10, (int)d.Mode);
            }
            t.Append("~p:");
            for (int i = 0; i < x.Promises.Count && i < VenturesExtra.PromisesMost; i++)
            {
                Promise p = x.Promises[i];
                t.Append(i > 0 ? ";" : string.Empty);
                Join(t, p.Id, p.DealId, (int)p.Kind, p.Target, p.Due, p.State, p.Kept, p.Missed, p.Checked, p.ReportDay);
            }
            t.Append("~v:").Append(x.ValuationCount).Append('|').Append(x.MonthDone).Append('|').Append(x.NextId).Append('|');
            for (int i = 0; i < x.ValuationCount; i++)
            {
                t.Append(i > 0 ? "," : string.Empty).Append(x.Valuation[i].ToString(Inv));
            }
            t.Append("~h:");
            for (int i = 0; i < x.Heards.Count && i < VenturesExtra.HeardMost; i++)
            {
                Heard h = x.Heards[i];
                t.Append(i > 0 ? ";" : string.Empty);
                Join(t, h.InvestorId, h.Day, h.Kind);
            }
            return t.ToString();
        }

        public static VenturesExtra Read(string text)
        {
            string[] parts = text.Split('~');
            int version;
            if (parts.Length == 0 || !parts[0].StartsWith("v=", StringComparison.Ordinal) || !int.TryParse(parts[0].Substring(2), NumberStyles.Integer, Inv, out version) || version < 1)
            {
                return null;
            }
            var x = new VenturesExtra();
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
                    case 't':
                        foreach (string record in Records(body))
                        {
                            string[] c = record.Split('|');
                            int mode = I(c, 3);
                            if (c.Length >= 4 && I(c, 0) > 0 && mode > 0 && mode <= (int)BuyBackMode.Callable && x.Terms.Count < VenturesExtra.TermsMost)
                            {
                                x.Terms.Add(new DealTerm { DealId = I(c, 0), Day = I(c, 1), Times10 = Math.Max(10, I(c, 2)), Mode = (BuyBackMode)mode });
                            }
                        }
                        break;
                    case 'p':
                        foreach (string record in Records(body))
                        {
                            string[] c = record.Split('|');
                            int kind = I(c, 2, -1);
                            if (c.Length >= 9 && I(c, 0) > 0 && kind >= 0 && kind < (int)PromiseKind.Count && x.Promises.Count < VenturesExtra.PromisesMost)
                            {
                                x.Promises.Add(new Promise
                                {
                                    Id = I(c, 0), DealId = I(c, 1), Kind = (PromiseKind)kind, Target = I(c, 3), Due = I(c, 4), State = Math.Max(0, Math.Min(2, I(c, 5))),
                                    Kept = Math.Max(0, I(c, 6)), Missed = Math.Max(0, I(c, 7)), Checked = I(c, 8, -1), ReportDay = I(c, 9)
                                });
                            }
                        }
                        break;
                    case 'v':
                    {
                        string[] c = body.Split('|');
                        x.MonthDone = I(c, 1, -1);
                        x.NextId = Math.Max(1, I(c, 2, 1));
                        if (c.Length > 3 && c[3].Length > 0)
                        {
                            foreach (string v in c[3].Split(','))
                            {
                                int value;
                                if (int.TryParse(v, NumberStyles.Integer, Inv, out value) && x.ValuationCount < VenturesExtra.RingMonths)
                                {
                                    x.Valuation[x.ValuationCount++] = Math.Max(0, value);
                                }
                            }
                        }
                        break;
                    }
                    case 'h':
                        foreach (string record in Records(body))
                        {
                            string[] c = record.Split('|');
                            if (c.Length >= 3 && x.Heards.Count < VenturesExtra.HeardMost)
                            {
                                x.Heards.Add(new Heard { InvestorId = I(c, 0), Day = I(c, 1), Kind = Math.Max(0, Math.Min(1, I(c, 2))) });
                            }
                        }
                        break;
                }
            }
            return x;
        }

        private static string[] Records(string body) => body.Length == 0 ? Array.Empty<string>() : body.Split(';');

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
    }
}
