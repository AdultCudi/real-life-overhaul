using System;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Markets
{
    /// <summary>Part of the save line: a company's record, a player's listed company's own fields with it.</summary>
    public static partial class MarketStore
    {
        // Today's step candles are kept for the companies the player follows: watched, held or with an order in.
        private static bool Followed(MarketState state, int id)
        {
            if (state.Watching(id) || state.Held(id) != 0)
            {
                return true;
            }
            for (int i = 0; i < state.Orders.Count; i++)
            {
                if (state.Orders[i].StockId == id && state.Orders[i].IsOpen)
                {
                    return true;
                }
            }
            return false;
        }

        private static void Stock(StringBuilder t, MarketState state, Stock k, bool city)
        {
            t.Append(k.Id.ToString(Inv));
            Join(t, (int)k.Status, k.ListedDay, k.GoneDay, (int)k.Reason, k.GoneCents);
            t.Append('|').Append(MarketCatalogue.IsRival(k.Id) ? Clean(k.Name) : string.Empty).Append('|').Append(Clean(k.RivalId));
            D(t, k.LogFair);
            D(t, k.Mis);
            D(t, k.Sent);
            D(t, k.Outlook);
            L(t, k.Shares);
            D(t, city ? 0.0 : k.Impact);
            Join(t, k.PrevClose, k.Open, k.High, k.Low, k.Last);
            L(t, k.DayVolume);
            D(t, k.Rev);
            D(t, k.Margin);
            D(t, k.Growth);
            D(t, k.MarginShift);
            D(t, k.MarginTarget);
            D(t, k.GrowthTarget);
            D(t, k.Debt);
            D(t, k.DebtRate);
            D(t, k.Equity);
            D(t, k.Distress);
            D(t, k.Competition);
            Join(t, k.Quarter);
            D(t, k.QRev);
            D(t, k.QNi);
            Join(t, k.PendingQuarter);
            D(t, k.PendingRev);
            D(t, k.PendingNi);
            Join(t, k.ResultsDay, k.DpsCents, k.DeclaredCents, k.RecordDay, k.PayDay, k.ExDone ? 1 : 0, k.LastExDay);
            D(t, k.Adv);
            Join(t, k.SplitDay, k.SplitRatio, k.BidDay, k.BidEndDay, k.BidCents, k.BidderId, k.ResultsBoostDay, k.LowDays);
            t.Append('|');
            for (int i = 0; i < k.Results.Count; i++)
            {
                QuarterResult r = k.Results[i];
                t.Append(i > 0 ? ":" : string.Empty).Append(r.Quarter.ToString(Inv)).Append(',').Append(r.Day.ToString(Inv)).Append(',').Append(r.RevenueK.ToString(Inv)).Append(',')
                    .Append(r.ProfitK.ToString(Inv)).Append(',').Append(r.Eps.ToString(Inv)).Append(',').Append(r.Consensus.ToString(Inv)).Append(',').Append(r.DpsCents.ToString(Inv));
            }
            t.Append('|');
            MarketCodec.Write(t, k.Daily);
            t.Append('|');
            MarketCodec.Write(t, k.Monthly);
            bool followed = city || Followed(state, k.Id);
            t.Append('|');
            MarketCodec.Write(t, k.Bars, followed ? k.BarCount : 0);
            t.Append('|');
            MarketCodec.Write(t, k.LastBars, followed ? k.LastBarCount : 0);
            Join(t, k.LastBarsDay);
            if (k.Own != null)
            {
                Own(t, k);
            }
        }

        /// <summary>One company's record alone: a player's company set aside while its founder is a guest elsewhere.</summary>
        public static string StockLine(MarketState state, Stock k)
        {
            var t = new StringBuilder(4096);
            Stock(t, state, k, true);
            return t.ToString();
        }

        /// <summary>A company read back from its record alone; null if it cannot be read.</summary>
        public static Stock ReadStockLine(string record, int year)
        {
            try
            {
                return string.IsNullOrEmpty(record) ? null : ReadStock(record.Split('|'), year);
            }
            catch (Exception)
            {
                return null;
            }
        }

        // A player's company: its founder, name, ticker, sector and usual debt, then its own figures, then its founder's deals.
        private static void Own(StringBuilder t, Stock k)
        {
            OwnListing o = k.Own;
            t.Append('|').Append(o.Owner.ToString("X16", Inv)).Append('|').Append(Clean(k.Name)).Append('|').Append(Clean(k.Ticker));
            Join(t, (int)k.Sector, o.DebtBp);
            t.Append('|').Append(o.Test ? 1 : 0).Append(',').Append(o.Founder.ToString(Inv)).Append(',').Append(R(o.Rps)).Append(',').Append(o.PayBp.ToString(Inv)).Append(',')
                .Append(o.ProposedCents.ToString(Inv)).Append(',').Append(o.Board ? 1 : 0).Append(',').Append(R(o.RevQ)).Append(',').Append(R(o.NiQ)).Append(',').Append(R(o.RevY)).Append(',')
                .Append(R(o.NiY)).Append(',').Append(R(o.LeverY)).Append(',').Append(R(o.Expect)).Append(',').Append(R(o.PendingExpect)).Append(',').Append(o.ListedCents.ToString(Inv)).Append(',')
                .Append(o.LockDay.ToString(Inv)).Append(',').Append(o.AgmDay.ToString(Inv)).Append(',').Append(o.YearCents.ToString(Inv)).Append(',').Append(o.YearIndex.ToString(Inv)).Append(',')
                .Append(o.YearScandals.ToString(Inv)).Append(',').Append(o.YearDividendCents.ToString(Inv)).Append(',').Append(o.LastStanding.ToString(Inv)).Append(',')
                .Append(o.ActBp.ToString(Inv)).Append(',').Append(((int)o.ActDemand).ToString(Inv)).Append(',').Append(o.ActDay.ToString(Inv)).Append(',').Append(o.Votes.ToString(Inv)).Append(',')
                .Append(o.VotesAgainstBp.ToString(Inv)).Append(',').Append(o.IssuedYear.ToString(Inv)).Append(',').Append(o.IssuedBp.ToString(Inv)).Append(',').Append(o.SaleDay.ToString(Inv))
                .Append(',').Append(R(o.Carry)).Append(',').Append(o.DealYear.ToString(Inv)).Append(',').Append(o.BoughtBp.ToString(Inv)).Append(',').Append(o.SoldBp.ToString(Inv)).Append(',')
                .Append(o.BackBp.ToString(Inv));
            t.Append('|');
            for (int i = 0; i < o.Deals.Count; i++)
            {
                OwnDeal d = o.Deals[i];
                t.Append(i > 0 ? ":" : string.Empty).Append(d.Day.ToString(Inv)).Append(',').Append(d.Shares.ToString(Inv)).Append(',').Append(d.Cents.ToString(Inv));
            }
            t.Append('|').Append(Clean(k.Def.Trade));
        }

        private static string R(double value)
        {
            return double.IsNaN(value) || double.IsInfinity(value) ? "0" : value.ToString("R", Inv);
        }

        // A player's company read back: its catalogue row made again from its own fields, and its figures.
        private static OwnListing ReadOwn(string[] c, out CompanyDef def, int id)
        {
            def = null;
            if (c.Length < 66 || !ulong.TryParse(S(c, 59), NumberStyles.HexNumber, Inv, out ulong owner) || owner == 0UL)
            {
                return null;
            }
            string ticker = S(c, 61).Length > 0 ? S(c, 61) : "OWN";
            int debtBp = Clamp(I(c, 63), 0, 30000);
            def = MarketCatalogue.PlayerDef(id, S(c, 60), ticker, (MarketSector)Clamp(I(c, 62), 0, (int)MarketSector.Count - 1), debtBp, S(c, 66).Length > 0 ? S(c, 66) : null);
            string[] f = S(c, 64).Split(',');
            var o = new OwnListing
            {
                Owner = owner, Test = I(f, 0) != 0, Founder = Math.Max(0, L(f, 1)), Rps = Clamp(F(f, 2, 0.0), -1e7, 1e7), PayBp = Clamp(I(f, 3), 0, 10000),
                ProposedCents = Clamp(I(f, 4), 0, 10000000), Board = I(f, 5, 1) != 0, RevQ = Math.Max(0.0, F(f, 6, 0.0)), NiQ = F(f, 7, 0.0), RevY = Math.Max(0.0, F(f, 8, 0.0)),
                NiY = F(f, 9, 0.0), LeverY = Clamp(F(f, 10, 0.0), -1.0, 1.0), Expect = F(f, 11, 0.0), PendingExpect = F(f, 12, 0.0), ListedCents = Math.Max(0, I(f, 13)), LockDay = I(f, 14),
                AgmDay = Math.Max(0, I(f, 15)), YearCents = Math.Max(0, I(f, 16)), YearIndex = Math.Max(0, I(f, 17)), YearScandals = Math.Max(0, I(f, 18)), YearDividendCents = Math.Max(0, I(f, 19)),
                LastStanding = Clamp(I(f, 20, -1), -1, 100), ActBp = Clamp(I(f, 21), 0, 10000), ActDemand = (ActivistDemand)Clamp(I(f, 22), 0, (int)ActivistDemand.Count - 1),
                ActDay = Math.Max(0, I(f, 23)), Votes = Math.Max(0, I(f, 24)), VotesAgainstBp = Clamp(I(f, 25), 0, 10000), IssuedYear = I(f, 26, -1), IssuedBp = Math.Max(0, I(f, 27)),
                SaleDay = Math.Max(0, I(f, 28)), Carry = Clamp(F(f, 29, 0.0), -1.0, 1.0), DebtBp = debtBp, DealYear = I(f, 30, -1), BoughtBp = Math.Max(0, I(f, 31)), SoldBp = Math.Max(0, I(f, 32)),
                BackBp = Math.Max(0, I(f, 33))
            };
            foreach (string r in S(c, 65).Length > 0 ? c[65].Split(':') : Array.Empty<string>())
            {
                string[] d = r.Split(',');
                if (d.Length >= 3 && o.Deals.Count < 16)
                {
                    o.Deals.Add(new OwnDeal { Day = Int(d[0]), Shares = Long(d[1]), Cents = Math.Max(1, Int(d[2])) });
                }
            }
            if (o.ActDemand == ActivistDemand.None)
            {
                o.ActBp = 0;
            }
            return o;
        }

        private static Stock ReadStock(string[] c, int year)
        {
            int id = I(c, 0);
            CompanyDef def = MarketCatalogue.Find(id);
            OwnListing own = null;
            if (def == null && ListingRules.IsPlayer(id))
            {
                own = ReadOwn(c, out def, id);   // a player's company: its row is in its record
            }
            if (def == null || c.Length < 54)
            {
                return null;
            }
            var k = new Stock
            {
                Id = id, Def = def, Name = MarketCatalogue.IsRival(id) ? S(c, 6) : def.Name, Ticker = def.Ticker, Sector = def.Sector, Size = def.Size,
                Status = I(c, 1) == (int)StockStatus.Gone ? StockStatus.Gone : StockStatus.Listed, ListedDay = I(c, 2), GoneDay = I(c, 3), Reason = (GoneReason)Clamp(I(c, 4), 0, 3),
                GoneCents = Math.Max(0, I(c, 5)), RivalId = S(c, 7).Length > 0 ? S(c, 7) : null
            };
            if (MarketCatalogue.IsRival(id) && string.IsNullOrEmpty(k.Name))
            {
                return null;
            }
            k.LogFair = Clamp(F(c, 8, 0.0), -10.0, 15.0);
            k.Mis = Clamp(F(c, 9, 0.0), -3.0, 3.0);
            k.Sent = Clamp(F(c, 10, 0.0), -3.0, 3.0);
            k.Outlook = Clamp(F(c, 11, 0.0), -8.0, 8.0);
            k.Shares = Math.Max(1000, L(c, 12));
            k.Impact = Math.Max(0.0, F(c, 13, 0.0));
            k.PrevClose = Math.Max(1, I(c, 14, 1));
            k.Open = Math.Max(1, I(c, 15, 1));
            k.High = Math.Max(1, I(c, 16, 1));
            k.Low = Math.Max(1, I(c, 17, 1));
            k.Last = Math.Max(1, I(c, 18, 1));
            k.DayVolume = Math.Max(0, L(c, 19));
            k.Rev = Math.Max(1.0, F(c, 20, def.RevenueM * 1e6));
            k.Margin = Clamp(F(c, 21, def.MarginBp / 10000.0), -0.5, 0.8);
            k.Growth = Clamp(F(c, 22, def.GrowthBp / 10000.0), -0.25, 0.4);
            k.MarginShift = own != null ? Clamp(F(c, 23, 0.0), -2.0, 2.0) : Clamp(F(c, 23, 0.0), -0.3, 0.9);   // a player's company's usual margin is its own books'
            k.MarginTarget = Clamp(F(c, 24, k.Margin), -1.0, 1.0);
            k.GrowthTarget = Clamp(F(c, 25, MarketCatalogue.LongRealGrowth), -0.25, 0.4);
            k.Debt = Math.Max(0.0, F(c, 26, 0.0));
            k.DebtRate = Clamp(F(c, 27, 0.05), 0.0, 0.5);
            k.Equity = F(c, 28, 0.0);
            k.Distress = Clamp(F(c, 29, 0.0), 0.0, 1.0);
            k.Competition = Clamp(F(c, 30, 0.0), 0.0, MarketInputs.CompetitionMost);
            k.Quarter = I(c, 31, -1);
            k.QRev = F(c, 32, 0.0);
            k.QNi = F(c, 33, 0.0);
            k.PendingQuarter = I(c, 34, -1);
            k.PendingRev = F(c, 35, 0.0);
            k.PendingNi = F(c, 36, 0.0);
            k.ResultsDay = Math.Max(0, I(c, 37));
            k.DpsCents = Math.Max(0, I(c, 38));
            k.DeclaredCents = Math.Max(0, I(c, 39));
            k.RecordDay = Math.Max(0, I(c, 40));
            k.PayDay = Math.Max(0, I(c, 41));
            k.ExDone = I(c, 42, 1) != 0;
            k.LastExDay = I(c, 43);
            k.Adv = Math.Max(1.0, F(c, 44, 1000.0));
            k.SplitDay = Math.Max(0, I(c, 45));
            k.SplitRatio = Clamp(I(c, 46), 0, 10);
            k.BidDay = Math.Max(0, I(c, 47));
            k.BidEndDay = Math.Max(0, I(c, 48));
            k.BidCents = Math.Max(0, I(c, 49));
            k.BidderId = Math.Max(0, I(c, 50));
            k.ResultsBoostDay = I(c, 51);
            k.LowDays = Math.Max(0, I(c, 52));
            if (k.SplitDay > 0 && k.SplitRatio < 2)
            {
                k.SplitDay = 0;
            }
            if (k.BidCents > 0 && k.BidEndDay <= 0)
            {
                k.BidCents = 0;
            }
            foreach (string r in S(c, 53).Length > 0 ? c[53].Split(':') : Array.Empty<string>())
            {
                string[] f = r.Split(',');
                if (f.Length >= 7 && k.Results.Count < MarketCatalogue.QuartersKept)
                {
                    k.Results.Add(new QuarterResult
                    {
                        Quarter = Int(f[0]), Day = Int(f[1]), RevenueK = Long(f[2]), ProfitK = Long(f[3]), Eps = Int(f[4]), Consensus = Int(f[5]), DpsCents = Math.Max(0, Int(f[6]))
                    });
                }
            }
            if (c.Length > 54)
            {
                MarketCodec.Read(c[54], k.Daily, MarketCatalogue.DailyKept);
            }
            if (c.Length > 55)
            {
                MarketCodec.Read(c[55], k.Monthly, MarketCatalogue.MonthlyKept);
            }
            k.BarCount = c.Length > 56 ? MarketCodec.Read(c[56], k.Bars) : 0;
            k.LastBarCount = c.Length > 57 ? MarketCodec.Read(c[57], k.LastBars) : 0;
            k.LastBarsDay = I(c, 58);
            if (double.IsNaN(k.LogFair) || double.IsNaN(k.Mis) || double.IsInfinity(k.LogFair + k.Mis))
            {
                k.LogFair = Math.Log(k.Last / 100.0);
                k.Mis = k.Sent = 0.0;
            }
            k.Own = own;
            if (own != null)
            {
                own.Founder = Math.Min(own.Founder, k.Shares);
            }
            return k;
        }
    }
}
