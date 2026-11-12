using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Economy.Dealership
{
    /// <summary>
    /// What wave 4 added to the car showroom in lines of the save of their own, so an old save loads as it was: <see cref="RivalsKey"/> the
    /// other dealers' cars, <see cref="ViewsKey"/> each car's looks and what each offer's buyer looked, <see cref="BidsKey"/> the last bids
    /// of each lot. Game-free; each reads into a state the main line made, and a line that cannot be read changes nothing.
    /// </summary>
    public static class DealerExtraStore
    {
        public const string RivalsKey = "reallife:dealer_rivals";
        public const string ViewsKey = "reallife:dealer_views";
        public const string BidsKey = "reallife:dealer_bids";
        public const int Version = 1;

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static string WriteRivals(DealerState s)
        {
            var t = new StringBuilder(32 + s.Rivals.Count * 40);
            t.Append("v=").Append(Version).Append('|');
            for (int i = 0; i < s.Rivals.Count && i < RivalRules.Most; i++)
            {
                RivalCar c = s.Rivals[i];
                t.Append(i > 0 ? ";" : string.Empty).Append(c.Rival.ToString(Inv)).Append(',').Append(Uri.EscapeDataString(c.Type ?? string.Empty))
                    .Append(',').Append(c.Ask.ToString(Inv)).Append(',').Append(c.Day.ToString(Inv));
            }
            return t.ToString();
        }

        public static bool ReadRivals(DealerState s, string text)
        {
            string[] list = Body(text);
            if (list == null)
            {
                return false;
            }
            try
            {
                s.Rivals.Clear();
                for (int i = 0; i < list.Length && s.Rivals.Count < RivalRules.Most; i++)
                {
                    string[] f = list[i].Split(',');
                    int rival = f.Length >= 4 ? Int(f[0]) : -1;
                    if (rival < 0 || rival >= DealerCatalogue.RivalNames.Length || f[1].Length == 0 || Int(f[2]) <= 0)
                    {
                        continue;
                    }
                    s.Rivals.Add(new RivalCar { Rival = rival, Type = Uri.UnescapeDataString(f[1]), Ask = Int(f[2]), Day = Int(f[3]) });
                }
                return true;
            }
            catch (Exception)
            {
                s.Rivals.Clear();
                return false;
            }
        }

        public static string WriteViews(DealerState s)
        {
            var t = new StringBuilder(32 + s.Yards.Count * 64);
            t.Append("v=").Append(Version).Append('|');
            bool first = true;
            for (int y = 0; y < s.Yards.Count; y++)
            {
                DealerYard yard = s.Yards[y];
                for (int c = 0; c < yard.Cars.Count; c++)
                {
                    StockCar car = yard.Cars[c];
                    if (car.ViewsToday == 0 && car.ViewsLast == 0)
                    {
                        continue;
                    }
                    t.Append(first ? string.Empty
                        : ";").Append('c').Append(',').Append(car.Id.ToString(Inv)).Append(',').Append(car.ViewsToday.ToString(Inv)).Append(',').Append(car.ViewsLast.ToString(Inv));
                    first = false;
                }
                for (int d = 0; d < yard.Deals.Count; d++)
                {
                    if (yard.Deals[d].Looks > 0)
                    {
                        t.Append(first ? string.Empty : ";").Append('o').Append(',').Append(yard.Deals[d].Id.ToString(Inv)).Append(',').Append(yard.Deals[d].Looks.ToString(Inv));
                        first = false;
                    }
                }
            }
            return t.ToString();
        }

        public static bool ReadViews(DealerState s, string text)
        {
            string[] list = Body(text);
            if (list == null)
            {
                return false;
            }
            try
            {
                for (int i = 0; i < list.Length; i++)
                {
                    string[] f = list[i].Split(',');
                    if (f.Length < 3)
                    {
                        continue;
                    }
                    int id = Int(f[1]);
                    for (int y = 0; y < s.Yards.Count; y++)
                    {
                        if (f[0] == "c")
                        {
                            StockCar car = s.Yards[y].Car(id);
                            if (car != null && f.Length >= 4)
                            {
                                car.ViewsToday = DealerCatalogue.Clamp(Int(f[2]), 0, ViewRules.LooksMost);
                                car.ViewsLast = DealerCatalogue.Clamp(Int(f[3]), 0, ViewRules.LooksMost);
                            }
                        }
                        else if (f[0] == "o")
                        {
                            DealerOffer d = s.Yards[y].Deal(id);
                            if (d != null)
                            {
                                d.Looks = DealerCatalogue.Clamp(Int(f[2]), 0, ViewRules.LooksMost);
                            }
                        }
                    }
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static string WriteBids(DealerState s)
        {
            var t = new StringBuilder(32 + s.Market.Count * 64);
            t.Append("v=").Append(Version).Append('|');
            bool first = true;
            for (int i = 0; i < s.Market.Count; i++)
            {
                DealerListing l = s.Market[i];
                if (l.Kind != ListingKind.Auction || l.BidLogCount == 0)
                {
                    continue;
                }
                t.Append(first ? string.Empty : ";").Append(l.Id.ToString(Inv)).Append(',');
                first = false;
                for (int b = 0; b < l.BidLogCount && b < DealerCatalogue.BidsKept; b++)
                {
                    t.Append(b > 0 ? "." : string.Empty).Append(l.BidLog[b].Bidder.ToString(Inv)).Append('_').Append(l.BidLog[b].Hour.ToString(Inv))
                        .Append('_').Append(l.BidLog[b].Amount.ToString(Inv));
                }
            }
            return t.ToString();
        }

        public static bool ReadBids(DealerState s, string text)
        {
            string[] list = Body(text);
            if (list == null)
            {
                return false;
            }
            try
            {
                for (int i = 0; i < list.Length; i++)
                {
                    string[] f = list[i].Split(',');
                    DealerListing l = f.Length >= 2 ? s.Listing(Int(f[0])) : null;
                    if (l == null || l.Kind != ListingKind.Auction)
                    {
                        continue;
                    }
                    string[] bids = f[1].Length > 0 ? f[1].Split('.') : new string[0];
                    l.BidLogCount = 0;
                    for (int b = 0; b < bids.Length && l.BidLogCount < DealerCatalogue.BidsKept; b++)
                    {
                        string[] q = bids[b].Split('_');
                        if (q.Length >= 3 && Int(q[2]) > 0)
                        {
                            l.BidLog[l.BidLogCount++] = new BidEntry { Bidder = Math.Max(0, Int(q[0])), Hour = Int(q[1]), Amount = Int(q[2]) };
                        }
                    }
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static void Read(DealerState s, Dictionary<string, string> data)
        {
            string text;
            if (data == null)
            {
                return;
            }
            if (data.TryGetValue(RivalsKey, out text))
            {
                ReadRivals(s, text);
            }
            if (data.TryGetValue(ViewsKey, out text))
            {
                ReadViews(s, text);
            }
            if (data.TryGetValue(BidsKey, out text))
            {
                ReadBids(s, text);
            }
        }

        public static void Write(DealerState s, Dictionary<string, string> data)
        {
            data[RivalsKey] = WriteRivals(s);
            data[ViewsKey] = WriteViews(s);
            data[BidsKey] = WriteBids(s);
        }

        // The records of a line after its "v=n|" head; null when it is not a line of this format.
        private static string[] Body(string text)
        {
            if (string.IsNullOrEmpty(text) || !text.StartsWith("v=", StringComparison.Ordinal))
            {
                return null;
            }
            int bar = text.IndexOf('|');
            if (bar < 0)
            {
                return null;
            }
            string body = text.Substring(bar + 1);
            return body.Length == 0 ? new string[0] : body.Split(';');
        }

        private static int Int(string text)
        {
            int n;
            return int.TryParse(text, NumberStyles.Integer, Inv, out n) ? n : 0;
        }
    }
}
