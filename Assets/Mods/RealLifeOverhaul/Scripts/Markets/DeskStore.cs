using System;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// The desk's own part of the market's line, at the end, so an older reader passes it over. A body that is not this
    /// version's is ignored and not written back: the check that a later part is passed over still holds. Nothing of it
    /// goes in the city's line a host sends. Game-free.
    /// </summary>
    public static class DeskStore
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static bool Any(MarketState state)
        {
            if (state == null)
            {
                return false;
            }
            if (state.Charts.Layers != 0 || state.Charts.FirstPane != ChartPane.None || state.Charts.Kind != 0 || state.Charts.Frame != 0
                || state.Charts.Drawings.Count > 0 || state.Charts.Alerts.Count > 0 || state.Rules.Count > 0)
            {
                return true;
            }
            return state.Listing != null && state.Listing.BidCount > 0;
        }

        public static void Write(StringBuilder t, MarketState state)
        {
            ChartBook c = state.Charts;
            t.Append("1|c|").Append(c.Layers.ToString(Inv)).Append('|').Append((int)c.FirstPane).Append('|').Append((int)c.SecondPane)
                .Append('|').Append(c.Kind).Append('|').Append(c.Frame);
            for (int i = 0; i < c.Drawings.Count; i++)
            {
                ChartDrawing d = c.Drawings[i];
                t.Append(";d|").Append(d.StockId.ToString(Inv)).Append('|').Append((int)d.Kind).Append('|').Append(d.Frame)
                    .Append('|').Append(d.FromIndex).Append('|').Append(d.ToIndex).Append('|').Append(d.FromCents).Append('|').Append(d.ToCents).Append('|').Append(d.Magnet);
            }
            for (int i = 0; i < c.Alerts.Count; i++)
            {
                PriceAlert a = c.Alerts[i];
                t.Append(";a|").Append(a.StockId.ToString(Inv)).Append('|').Append((int)a.Kind).Append('|').Append(a.Cents)
                    .Append('|').Append(a.Drawing).Append('|').Append(a.Hit ? 1 : 0).Append('|').Append(a.HitDay);
            }
            for (int i = 0; i < state.Rules.Count; i++)
            {
                RuleBot r = state.Rules[i];
                t.Append(";r|").Append(r.Id).Append('|').Append((int)r.Kind).Append('|').Append(r.Owner).Append('|').Append(r.Who)
                    .Append('|').Append(r.StockId).Append('|').Append(r.Level).Append('|').Append(r.GainBp).Append('|').Append((int)r.Type)
                    .Append('|').Append(r.Budget).Append('|').Append(r.PerTrade).Append('|').Append(r.StopBp).Append('|').Append(r.OpenOnly ? 1 : 0)
                    .Append('|').Append(r.Reinvest ? 1 : 0).Append('|').Append(r.Tell ? 1 : 0).Append('|').Append(r.MaxOpen).Append('|').Append((int)r.Status)
                    .Append('|').Append(r.Cash).Append('|').Append(0).Append('|').Append(0).Append('|').Append(r.OpenTrades)   // the two before the open trades were a cash flow's day and week, never a gain: kept as places, read as nothing
                    .Append('|').Append(r.DayGain).Append('|').Append(r.WeekGain).Append('|').Append(r.FigDay).Append('|').Append(r.Shares).Append('|').Append(r.Cost);
            }
            ListingFile file = state.Listing;
            if (file != null && file.BidCount > 0)
            {
                t.Append(";b|").Append(file.BidCount).Append('|').Append(file.CoverBp).Append('|').Append(file.SetCents).Append('|').Append(file.PriceCents);
                for (int i = 0; i < file.BidCount && i < BookBids.Most; i++)
                {
                    BookBid b = file.Bids[i];
                    t.Append('|').Append((int)b.Who).Append('|').Append(b.WhoId).Append('|').Append(b.Cents).Append('|').Append(b.Size).Append('|').Append(b.Filled).Append('|').Append(b.Cut);
                }
            }
        }

        /// <summary>True when the body was this version's. Anything else is left unread, and the caller does not write it back.</summary>
        public static bool Read(MarketState state, string body)
        {
            if (state == null || string.IsNullOrEmpty(body) || body.Length < 2 || body[0] != '1' || body[1] != '|')
            {
                return false;
            }
            string[] recs = body.Split(';');
            for (int i = 0; i < recs.Length; i++)
            {
                string[] c = recs[i].Split('|');
                if (c.Length < 2)
                {
                    continue;
                }
                if (c[0] == "1" && c.Length > 2 && c[1] == "c")
                {
                    state.Charts.Layers = I(c, 2);
                    state.Charts.FirstPane = Pane(I(c, 3));
                    state.Charts.SecondPane = Pane(I(c, 4));
                    state.Charts.Kind = Clamp(I(c, 5), 0, 2);
                    state.Charts.Frame = Clamp(I(c, 6), 0, 4);
                }
                else if (c[0] == "d" && state.Charts.Drawings.Count < ChartBook.DrawingsMost && c.Length >= 9)
                {
                    state.Charts.Drawings.Add(new ChartDrawing
                    {
                        StockId = I(c, 1), Kind = (ChartMark)Clamp(I(c, 2), 0, 6), Frame = I(c, 3), FromIndex = I(c, 4), ToIndex = I(c, 5),
                        FromCents = I(c, 6), ToCents = I(c, 7), Magnet = (byte)Clamp(I(c, 8), 0, 1)
                    });
                }
                else if (c[0] == "a" && state.Charts.Alerts.Count < ChartBook.AlertsMost && c.Length >= 7)
                {
                    state.Charts.Alerts.Add(new PriceAlert
                    {
                        StockId = I(c, 1), Kind = (AlertKind)Clamp(I(c, 2), 0, 2), Cents = I(c, 3), Drawing = I(c, 4), Hit = I(c, 5) != 0, HitDay = I(c, 6)
                    });
                }
                else if (c[0] == "r" && state.Rules.Count < RuleBots.Most && c.Length >= 21)
                {
                    state.Rules.Add(new RuleBot
                    {
                        Id = I(c, 1), Kind = (RuleKind)Clamp(I(c, 2), 0, 7), Owner = I(c, 3) == 0 ? 0 : 1, Who = I(c, 4), StockId = I(c, 5),
                        Level = I(c, 6), GainBp = I(c, 7), Type = (OrderType)Clamp(I(c, 8), 0, 2), Budget = L(c, 9), PerTrade = L(c, 10), StopBp = I(c, 11),
                        OpenOnly = I(c, 12) != 0, Reinvest = I(c, 13) != 0, Tell = I(c, 14) != 0, MaxOpen = Math.Max(1, I(c, 15)),
                        Status = (RuleStatus)Clamp(I(c, 16), 0, 2), Cash = L(c, 17), OpenTrades = Math.Max(0, I(c, 20))
                    });
                    if (c.Length >= 26)
                    {
                        // The gain figures and what the rule holds, at the end of its line: an older line has none, and starts them at nothing.
                        RuleBot made = state.Rules[state.Rules.Count - 1];
                        made.DayGain = L(c, 21);
                        made.WeekGain = L(c, 22);
                        made.FigDay = I(c, 23);
                        made.Shares = Math.Max(0, L(c, 24));
                        made.Cost = Math.Max(0, L(c, 25));
                    }
                }
                else if (c[0] == "b" && state.Listing != null && c.Length >= 4)
                {
                    ListingFile file = state.Listing;
                    file.BidCount = Clamp(I(c, 1), 0, BookBids.Most);
                    file.CoverBp = I(c, 2);
                    file.SetCents = I(c, 3);
                    int at = 5;   // c[4] is the price the listing already keeps
                    for (int b = 0; b < file.BidCount && at + 5 < c.Length; b++)
                    {
                        file.Bids[b] = new BookBid
                        {
                            Who = (BidWho)Clamp(I(c, at), 0, 3), WhoId = I(c, at + 1), Cents = I(c, at + 2), Size = L(c, at + 3), Filled = L(c, at + 4), Cut = (byte)Clamp(I(c, at + 5), 0, 2)
                        };
                        at += 6;
                    }
                }
            }
            return true;
        }

        private static ChartPane Pane(int v) => (ChartPane)Clamp(v, 0, 5);

        private static int Clamp(int v, int lo, int hi) => v < lo ? lo : v > hi ? hi : v;

        private static int I(string[] c, int i)
        {
            return i < c.Length && int.TryParse(c[i], NumberStyles.Integer, Inv, out int v) ? v : 0;
        }

        private static long L(string[] c, int i)
        {
            return i < c.Length && long.TryParse(c[i], NumberStyles.Integer, Inv, out long v) ? v : 0L;
        }
    }
}
