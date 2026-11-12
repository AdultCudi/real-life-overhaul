using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// The save line of the stock market's extras: <c>reallife:stock_extras</c>, version 1. Parts apart by '|', each a letter
    /// and a colon, records by ';', fields by ','; every number in the invariant culture. A save without the line starts
    /// with nothing; a part or a field this version does not know is passed over, so a later version's line is read as far
    /// as it goes; a line that cannot be read is kept as it was (Core/SaveLines). Game-free; written at a save only.
    /// </summary>
    public static class ExtrasStore
    {
        public const string Key = "reallife:stock_extras";
        public const int Version = 1;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static void Load(Dictionary<string, string> data, MarketState state)
        {
            if (data == null || state == null || !data.TryGetValue(Key, out string text) || string.IsNullOrEmpty(text))
            {
                return;
            }
            ExtrasState read = SaveLines.Checked(data, Key, Read(text, out int version), version, Version);
            if (read != null)
            {
                state.Extras.CopyFrom(read);
            }
        }

        public static void Save(Dictionary<string, string> data, MarketState state)
        {
            if (data == null || state == null || SaveLines.IsKept(data, Key))
            {
                return;   // a line this version could not read stays as it was
            }
            if (state.Extras.Empty)
            {
                data.Remove(Key);
                return;
            }
            data[Key] = Write(state.Extras);
        }

        public static string Write(ExtrasState x)
        {
            var t = new StringBuilder(256);
            t.Append('v').Append(Version.ToString(Inv));
            t.Append("|f:");
            Join(t, x.Units, x.Cost, x.Realised, x.Paid, x.Fees);
            t.Append(',').Append(x.Track.ToString("R", Inv)).Append(',');
            Join(t, x.TrackDay, x.OddCents);
            t.Append("|d:");
            for (int i = 0; i < x.Dues.Count; i++)
            {
                Sep(t, i);
                Join(t, x.Dues[i].PayDay, x.Dues[i].Cents);
            }
            t.Append("|s:");
            for (int i = 0; i < x.Splits.Count; i++)
            {
                Sep(t, i);
                Join(t, x.Splits[i].Stock, x.Splits[i].Day, x.Splits[i].New, x.Splits[i].Old);
            }
            t.Append("|w:");
            for (int i = 0; i < x.SplitWaits.Count; i++)
            {
                Sep(t, i);
                Join(t, x.SplitWaits[i].Stock, x.SplitWaits[i].Day);
            }
            t.Append("|p:");
            for (int i = 0; i < x.Pushes.Count; i++)
            {
                Sep(t, i);
                t.Append(x.Pushes[i].Stock.ToString(Inv)).Append(',').Append(x.Pushes[i].Ln.ToString("R", Inv));
            }
            t.Append("|t:");
            for (int i = 0; i < x.Told.Count; i++)
            {
                Sep(t, i);
                Join(t, x.Told[i].Stock, x.Told[i].Holder, x.Told[i].Bp);
            }
            t.Append("|r:");
            for (int i = 0; i < x.Rivals.Length; i++)
            {
                RivalBook r = x.Rivals[i];
                Sep(t, i);
                Join(t, r.Units, r.UnitsCost, r.LookDay, r.ArmStock, r.ArmKind, r.ArmDay);
                for (int k = 0; k < r.Stakes.Count; k++)
                {
                    t.Append(',');
                    Join(t, r.Stakes[k].Stock, r.Stakes[k].Shares, r.Stakes[k].Cost, r.Stakes[k].Day);
                }
            }
            BankFloat b = x.Bank;
            t.Append("|b:");
            Join(t, (int)b.Stage, b.AdvisedDay, b.PriceDay, b.FloatBp, b.FairCents, b.LowCents, b.HighCents, b.OldShares, b.NewShares, b.PriceCents, b.Raised, b.Fees, b.StockId, b.ListDay,
                b.PaidOthers, b.Founder, b.Shares, b.HeldCents);
            t.Append("|k:");
            for (int i = 0; i < b.Days.Count; i++)
            {
                Sep(t, i);
                Join(t, b.Days[i].Day, b.Days[i].Income, b.Days[i].Profit, b.Days[i].Capital);
            }
            t.Append("|q:");
            for (int i = 0; i < b.Books.Count; i++)
            {
                Sep(t, i);
                Join(t, b.Books[i].Quarter, b.Books[i].Revenue, b.Books[i].Profit, b.Books[i].Days);
            }
            return t.ToString();
        }

        /// <summary>The line read; null if it is not one of these lines at all.</summary>
        public static ExtrasState Read(string text, out int version)
        {
            version = 0;
            if (string.IsNullOrEmpty(text) || text[0] != 'v')
            {
                return null;
            }
            string[] parts = text.Split('|');
            if (!int.TryParse(parts[0].Substring(1), NumberStyles.Integer, Inv, out version) || version < 1)
            {
                return null;
            }
            var x = new ExtrasState();
            for (int p = 1; p < parts.Length; p++)
            {
                string part = parts[p];
                if (part.Length < 2 || part[1] != ':')
                {
                    continue;
                }
                string body = part.Substring(2);
                string[] records = body.Length == 0 ? new string[0] : body.Split(';');
                switch (part[0])
                {
                    case 'f':
                    {
                        string[] c = body.Split(',');
                        x.Units = Math.Max(0L, L(c, 0));
                        x.Cost = Math.Max(0L, L(c, 1));
                        x.Realised = L(c, 2);
                        x.Paid = Math.Max(0L, L(c, 3));
                        x.Fees = Math.Max(0L, L(c, 4));
                        double track = D(c, 5, 1.0);
                        x.Track = track > 0.0 && track <= 4.0 ? track : 1.0;
                        x.TrackDay = (int)L(c, 6);
                        x.OddCents = Math.Max(-99L, Math.Min(99L, L(c, 7)));
                        break;
                    }
                    case 'd':
                        for (int i = 0; i < records.Length && x.Dues.Count < ExtrasCatalogue.FundDuesMost; i++)
                        {
                            string[] c = records[i].Split(',');
                            if (L(c, 1) > 0)
                            {
                                x.Dues.Add(new FundDue { PayDay = (int)L(c, 0), Cents = L(c, 1) });
                            }
                        }
                        break;
                    case 's':
                        for (int i = 0; i < records.Length && x.Splits.Count < ExtrasCatalogue.SplitsKept; i++)
                        {
                            string[] c = records[i].Split(',');
                            int fresh = (int)L(c, 2), old = (int)L(c, 3);
                            if (L(c, 0) > 0 && fresh >= 1 && old >= 1 && fresh != old && fresh <= ExtrasCatalogue.RatioMost && old <= ExtrasCatalogue.RatioMost)
                            {
                                x.Splits.Add(new SplitNote { Stock = (int)L(c, 0), Day = (int)L(c, 1), New = fresh, Old = old });
                            }
                        }
                        break;
                    case 'w':
                        for (int i = 0; i < records.Length && x.SplitWaits.Count < ExtrasCatalogue.SplitWaitsKept; i++)
                        {
                            string[] c = records[i].Split(',');
                            if (L(c, 0) > 0 && L(c, 1) > 0)
                            {
                                x.SplitWaits.Add(new SplitWait { Stock = (int)L(c, 0), Day = (int)L(c, 1) });
                            }
                        }
                        break;
                    case 'p':
                        for (int i = 0; i < records.Length && x.Pushes.Count < 256; i++)
                        {
                            string[] c = records[i].Split(',');
                            double ln = D(c, 1, 0.0);
                            if (L(c, 0) > 0 && ln != 0.0 && Math.Abs(ln) < 5.0)
                            {
                                x.Pushes.Add(new PushNote { Stock = (int)L(c, 0), Ln = ln });
                            }
                        }
                        break;
                    case 't':
                        for (int i = 0; i < records.Length && x.Told.Count < ExtrasCatalogue.DisclosuresKept; i++)
                        {
                            string[] c = records[i].Split(',');
                            if (L(c, 0) > 0 && L(c, 2) > 0)
                            {
                                x.Told.Add(new StakeTold { Stock = (int)L(c, 0), Holder = (int)Math.Max(0L, L(c, 1)), Bp = (int)Math.Min(10000L, L(c, 2)) });
                            }
                        }
                        break;
                    case 'r':
                        for (int i = 0; i < records.Length && i < x.Rivals.Length; i++)
                        {
                            string[] c = records[i].Split(',');
                            RivalBook r = x.Rivals[i];
                            r.Units = Math.Max(0L, L(c, 0));
                            r.UnitsCost = Math.Max(0L, L(c, 1));
                            r.LookDay = c.Length > 2 ? (int)L(c, 2) : -1;
                            r.ArmStock = (int)Math.Max(0L, L(c, 3));
                            r.ArmKind = ExtrasCatalogue.Clamp((int)L(c, 4), 0, ExtrasCatalogue.ArmWords.Length - 1);
                            r.ArmDay = (int)L(c, 5);
                            for (int k = 6; k + 3 < c.Length && r.Stakes.Count < ExtrasCatalogue.RivalStakesMost; k += 4)
                            {
                                if (L(c, k) > 0 && L(c, k + 1) > 0)
                                {
                                    r.Stakes.Add(new RivalStake { Stock = (int)L(c, k), Shares = L(c, k + 1), Cost = Math.Max(0L, L(c, k + 2)), Day = (int)L(c, k + 3) });
                                }
                            }
                        }
                        break;
                    case 'b':
                    {
                        string[] c = body.Split(',');
                        BankFloat b = x.Bank;
                        b.Stage = (BankStage)ExtrasCatalogue.Clamp((int)L(c, 0), 0, (int)BankStage.Ended);
                        b.AdvisedDay = (int)L(c, 1);
                        b.PriceDay = (int)L(c, 2);
                        b.FloatBp = ExtrasCatalogue.Clamp((int)L(c, 3), 0, 9000);
                        b.FairCents = (int)Math.Max(0L, L(c, 4));
                        b.LowCents = (int)Math.Max(0L, L(c, 5));
                        b.HighCents = (int)Math.Max(0L, L(c, 6));
                        b.OldShares = Math.Max(0L, L(c, 7));
                        b.NewShares = Math.Max(0L, L(c, 8));
                        b.PriceCents = (int)Math.Max(0L, L(c, 9));
                        b.Raised = Math.Max(0L, L(c, 10));
                        b.Fees = Math.Max(0L, L(c, 11));
                        b.StockId = (int)Math.Max(0L, L(c, 12));
                        b.ListDay = (int)L(c, 13);
                        b.PaidOthers = Math.Max(0L, L(c, 14));
                        b.Founder = Math.Max(0L, L(c, 15));   // a line from before them: the stake is read again from the stock, or from the offer
                        b.Shares = Math.Max(0L, L(c, 16));
                        b.HeldCents = Math.Max(0L, L(c, 17));
                        break;
                    }
                    case 'k':
                        for (int i = 0; i < records.Length && x.Bank.Days.Count < ExtrasCatalogue.BankDaysKept; i++)
                        {
                            string[] c = records[i].Split(',');
                            x.Bank.Days.Add(new BankDayNote { Day = (int)L(c, 0), Income = L(c, 1), Profit = L(c, 2), Capital = c.Length > 3 ? Math.Max(-1L, L(c, 3)) : -1L });
                        }
                        break;
                    case 'q':
                        for (int i = 0; i < records.Length && x.Bank.Books.Count < ExtrasCatalogue.BankQuartersKept; i++)
                        {
                            string[] c = records[i].Split(',');
                            x.Bank.Books.Add(new BookQuarter { Quarter = (int)L(c, 0), Revenue = L(c, 1), Profit = L(c, 2), Days = (int)Math.Max(0L, L(c, 3)) });
                        }
                        break;
                }
            }
            return x;
        }

        private static void Sep(StringBuilder t, int i)
        {
            if (i > 0)
            {
                t.Append(';');
            }
        }

        private static void Join(StringBuilder t, params long[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                if (i > 0)
                {
                    t.Append(',');
                }
                t.Append(values[i].ToString(Inv));
            }
        }

        private static long L(string[] c, int at)
        {
            return at < c.Length && long.TryParse(c[at], NumberStyles.Integer, Inv, out long v) ? v : 0L;
        }

        private static double D(string[] c, int at, double otherwise)
        {
            return at < c.Length && double.TryParse(c[at], NumberStyles.Float, Inv, out double v) && !double.IsNaN(v) && !double.IsInfinity(v) ? v : otherwise;
        }
    }
}
