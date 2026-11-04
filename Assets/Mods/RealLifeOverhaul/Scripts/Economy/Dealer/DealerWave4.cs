using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Economy.Dealership
{
    /// <summary>One bid on a lot: who (0 the player, 1 and more a dealer of the room by <see cref="DealerCatalogue.RivalNames"/>), the hour and the amount.</summary>
    public struct BidEntry
    {
        public int Bidder, Hour, Amount;
    }

    /// <summary>A car a rival dealer has on its lot: which dealer, the game's vehicle type, the price it asks and the day it was put out.</summary>
    public struct RivalCar
    {
        public int Rival;
        public string Type;
        public int Ask;
        public int Day;
    }

    /// <summary>
    /// The other dealers of the city as sellers: each of the <see cref="DealerCatalogue.RivalNames"/> keeps a few cars of the game's own
    /// vehicle types on its lot with prices of its own, restocked a little each morning, and a buyer looking at the player's car of the
    /// same model compares the asking prices. Seeded from the save and the day, so the same on every machine. Game-free. COST: a few
    /// dozen cars a morning, a scan of at most <see cref="Most"/> cars when a buyer looks at one.
    /// </summary>
    public static class RivalRules
    {
        public const int PerDealerLeast = 3, PerDealerMost = 6;
        public const int Most = 6 * PerDealerMost;
        public const int SoldChance = 120;                      // a day, in thousandths, that a rival's car is sold and replaced
        public const int BiasLeast = 900, BiasMost = 1180;      // a dealer's usual price against the worth, in thousandths
        public const int Spread = 60;                           // a car's price against its dealer's usual, plus or minus
        public const int TypicalKm = 40000, TypicalCondition = 880;
        public const int DearFloor = 600, CheapMost = 1150;     // what comparing prices does to a buyer's offer chance, in thousandths

        /// <summary>A dealer's usual price against a car's worth, in thousandths: its own, the same all game.</summary>
        public static int BiasOf(int seed, int rival)
        {
            var dice = new DealerDice(seed, rival, 5, 77);
            return dice.Range(BiasLeast, BiasMost);
        }

        /// <summary>The morning's restock: sold cars go, each dealer is topped up to its number, from the game's vehicle types at today's prices.</summary>
        public static void Morning(DealerState s, int day, IList<MarketModel> models, int level)
        {
            int dealers = DealerCatalogue.RivalNames.Length;
            if (models == null || models.Count == 0)
            {
                s.Rivals.Clear();
                return;
            }
            for (int i = s.Rivals.Count - 1; i >= 0; i--)
            {
                RivalCar c = s.Rivals[i];
                bool known = false;
                for (int m = 0; m < models.Count && !known; m++)
                {
                    known = models[m].Type == c.Type;
                }
                var dice = new DealerDice(s.Seed, day, c.Rival * 131 + c.Day, 83);
                if (!known || c.Rival < 0 || c.Rival >= dealers || dice.Chance(SoldChance))
                {
                    s.Rivals.RemoveAt(i);
                }
            }
            for (int r = 0; r < dealers; r++)
            {
                var want = new DealerDice(s.Seed, r, 3, 79);
                int target = want.Range(PerDealerLeast, PerDealerMost);
                int have = 0;
                for (int i = 0; i < s.Rivals.Count; i++)
                {
                    have += s.Rivals[i].Rival == r ? 1 : 0;
                }
                int bias = BiasOf(s.Seed, r);
                var dice = new DealerDice(s.Seed, day, r, 89);
                for (int n = have; n < target && s.Rivals.Count < Most; n++)
                {
                    MarketModel m = models[dice.Range(0, models.Count - 1)];
                    int worth = DealerRules.Worth(m.Price, level, TypicalKm, TypicalCondition);
                    int ask = DealerCatalogue.Named(DealerCatalogue.Share(worth, bias + dice.Range(-Spread, Spread)));
                    if (ask > 0)
                    {
                        s.Rivals.Add(new RivalCar { Rival = r, Type = m.Type, Ask = ask, Day = day });
                    }
                }
            }
        }

        /// <summary>The lowest price any rival asks for a type of car, and who; 0 when none has one.</summary>
        public static int Cheapest(DealerState s, string type, out int rival)
        {
            rival = -1;
            int best = 0;
            for (int i = 0; i < s.Rivals.Count; i++)
            {
                RivalCar c = s.Rivals[i];
                if (c.Type == type && c.Ask > 0 && (best == 0 || c.Ask < best))
                {
                    best = c.Ask;
                    rival = c.Rival;
                }
            }
            return best;
        }

        /// <summary>
        /// What a rival's price for the same model does to a buyer's offer chance, in thousandths of it: dearer than the rival and they
        /// look elsewhere (down to <see cref="DearFloor"/>), cheaper and they are drawn (up to <see cref="CheapMost"/>); alike, nothing.
        /// </summary>
        public static int Compare(int ask, int rivalAsk)
        {
            if (rivalAsk <= 0 || ask <= 0)
            {
                return 1000;
            }
            long ratio = (long)ask * 1000 / rivalAsk;
            long dear = ratio - 1000;
            return dear > 0 ? (int)Math.Max(DearFloor, 1000 - dear * 2) : (int)Math.Min(CheapMost, 1000 + (-dear) / 2);
        }
    }

    /// <summary>Viewings of the cars on a lot: how many buyers looked at each car today and yesterday, and how often a buyer looked before an offer. Game-free.</summary>
    public static class ViewRules
    {
        public const int LooksMost = 99;

        /// <summary>A buyer looks at a car today. Returns how many looks the car has had today.</summary>
        public static int Look(StockCar car)
        {
            car.ViewsToday = Math.Min(LooksMost, car.ViewsToday + 1);
            return car.ViewsToday;
        }

        /// <summary>A showroom's day is over: today's looks are yesterday's, and every car starts the new day with none.</summary>
        public static void NewDay(DealerYard y)
        {
            for (int i = 0; i < y.Cars.Count; i++)
            {
                y.Cars[i].ViewsLast = y.Cars[i].ViewsToday;
                y.Cars[i].ViewsToday = 0;
            }
        }

        /// <summary>
        /// The words' number for a buyer's offer: how many times they looked before it. At least one; a buyer who came back to a car already
        /// looked at today looked again, so a car looked at often draws offers from people who looked more than once.
        /// </summary>
        public static int LooksBeforeOffer(StockCar car, int seed, int day, int customerKey)
        {
            int seen = Math.Max(1, car.ViewsToday);
            var dice = new DealerDice(seed, day, customerKey, 97);
            return Math.Min(seen, 1 + dice.Range(0, 2));
        }
    }

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
                t.Append(i > 0 ? ";" : string.Empty).Append(c.Rival.ToString(Inv)).Append(',').Append(Uri.EscapeDataString(c.Type ?? string.Empty)).Append(',').Append(c.Ask.ToString(Inv)).Append(',').Append(c.Day.ToString(Inv));
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
                    t.Append(first ? string.Empty : ";").Append('c').Append(',').Append(car.Id.ToString(Inv)).Append(',').Append(car.ViewsToday.ToString(Inv)).Append(',').Append(car.ViewsLast.ToString(Inv));
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
                    t.Append(b > 0 ? "." : string.Empty).Append(l.BidLog[b].Bidder.ToString(Inv)).Append('_').Append(l.BidLog[b].Hour.ToString(Inv)).Append('_').Append(l.BidLog[b].Amount.ToString(Inv));
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
