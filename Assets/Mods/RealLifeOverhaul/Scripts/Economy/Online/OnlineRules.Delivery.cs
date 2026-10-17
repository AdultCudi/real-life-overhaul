using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Economy
{
    /// <summary>The online shops' rules, part: picking, delivery, returns and the rating.</summary>
    public static partial class OnlineRules
    {
        private static readonly Stack<WebReturn> ReturnPool = new Stack<WebReturn>(16);

        public static WebReturn NewReturn()
        {
            return ReturnPool.Count > 0 ? ReturnPool.Pop() : new WebReturn();
        }

        /// <summary>The staff on shift pick, oldest first; an order still unpicked long after its promise is given up.</summary>
        public static void Pick(WebShop shop, Site site, int hour, int pickers, StockTake take, ref HourCount count)
        {
            int room = Math.Max(0, pickers) * OnlineCatalogue.PickPerHour;
            bool left = false;
            DayBook book = shop.Today;
            for (int i = 0; i < shop.Orders.Count; i++)
            {
                WebOrder o = shop.Orders[i];
                if (o.Stage != OrderStage.Waiting)
                {
                    continue;
                }
                if (hour > o.Due + OnlineCatalogue.WaitMost)
                {
                    book.Cancelled++;
                    count.Cancelled++;
                    Rate(shop, OnlineCatalogue.ScoreLost, o.Tilt);
                    Drop(shop, i--);
                    continue;
                }
                if (room <= 0)
                {
                    left = true;
                    continue;
                }
                room--;
                if (!PickOrder(shop, site, hour, i, take, ref count))
                {
                    i--;
                }
            }
            shop.Idle = left && pickers <= 0 ? shop.Idle + 1 : 0;
        }

        /// <summary>Picks one order from the real stock as far as it goes and sends it on. False if nothing of it was there: it is cancelled.</summary>
        public static bool PickOrder(WebShop shop, Site site, int hour, int index, StockTake take, ref HourCount count)
        {
            WebOrder o = shop.Orders[index];
            DayBook book = shop.Today;
            int total = 0, units = 0, asked = 0, goods = 0;
            for (int j = 0; j < o.Count; j++)
            {
                WebLine line = o.Line[j] >= 0 && o.Line[j] < shop.Lines.Count ? shop.Lines[o.Line[j]] : null;
                int cost = 0;
                int got = line != null && take != null ? OnlineCatalogue.Clamp(take(line.Item, o.Qty[j], out cost), 0, o.Qty[j]) : 0;
                cost = got > 0 ? Math.Max(0, cost) : 0;
                o.Got[j] = got;
                o.Paid[j] = cost;
                asked += o.Qty[j];
                units += got;
                total += got * o.Unit[j];
                goods += cost;
                if (line != null)
                {
                    line.Units += got;
                    line.Takings += got * o.Unit[j];
                    line.Cost += cost;
                    line.Missed += o.Qty[j] - got;
                    line.BookTakings += got * o.Unit[j];
                    line.BookCost += cost;
                }
            }
            if (units == 0)
            {
                book.Cancelled++;
                count.Cancelled++;
                Rate(shop, OnlineCatalogue.ScoreLost, o.Tilt);
                Drop(shop, index);
                return false;
            }
            o.Short = units < asked;
            o.Total = total;
            book.Short += o.Short ? 1 : 0;
            book.Picked++;
            book.Units += units;
            book.Takings += total;
            book.Goods += goods;
            count.Picked++;
            if (shop.Way == OnlineCatalogue.Courier)
            {
                int metres = Metres(site, o.District);
                o.Stage = OrderStage.Out;
                o.Arrive = hour + CourierTime(metres);
                book.Delivery += CourierFee(metres);
                count.Sent++;
            }
            else
            {
                o.Stage = OrderStage.Picked;
            }
            return true;
        }

        private static void Drop(WebShop shop, int index)
        {
            Recycle(shop.Orders[index]);
            shop.Orders.RemoveAt(index);
        }

        private static int Metres(Site site, int district)
        {
            return district >= 0 && district < site.Districts ? Math.Max(0, site.Metres[district]) : 0;
        }

        /// <summary>Cents a courier charges for a parcel, at the city's prices.</summary>
        public static int CourierFee(int metres)
        {
            return CompanyCatalogue.AtPrices(OnlineCatalogue.CourierBase + (int)((long)OnlineCatalogue.CourierPerKm * Math.Max(0, metres) / 1000), Prices.Level);
        }

        /// <summary>Hours from collection to the door.</summary>
        public static int CourierTime(int metres)
        {
            return OnlineCatalogue.CourierHours + Math.Max(0, metres) / OnlineCatalogue.CourierMetresPerHour;
        }

        /// <summary>Minutes a van takes one way.</summary>
        public static int Travel(int metres)
        {
            return Math.Max(1, (int)((long)Math.Max(0, metres) * 60 / OnlineCatalogue.VanMetresPerHour));
        }

        /// <summary>Cents of fuel and wear for a round there and back, at the city's prices.</summary>
        public static int VanCost(int metres)
        {
            return CompanyCatalogue.AtPrices((int)(2L * Math.Max(0, metres) * OnlineCatalogue.VanCostPerKm / 1000), Prices.Level);
        }

        /// <summary>Each van that is back takes the packed order promised soonest and others for its district, as many as it holds and its driver's day allows.</summary>
        public static void Rounds(OnlineState s, WebShop shop, Site site, int hour, ref HourCount count)
        {
            int h = hour % 24, day = hour / 24, now = hour * 60;
            if (shop.Way != OnlineCatalogue.OwnVans || h < OnlineCatalogue.DriveFrom || h >= OnlineCatalogue.DriveUntil)
            {
                return;
            }
            for (int v = 0; v < s.Vans.Count; v++)
            {
                Van van = s.Vans[v];
                if (van.Day != day)
                {
                    van.Day = day;
                    van.Used = 0;
                }
                if (!van.Ready || van.Capacity <= 0 || van.FreeAt > now)
                {
                    continue;
                }
                int first = -1;
                for (int i = 0; i < shop.Orders.Count; i++)
                {
                    if (shop.Orders[i].Stage == OrderStage.Picked && (first < 0 || shop.Orders[i].Due < shop.Orders[first].Due))
                    {
                        first = i;
                    }
                }
                if (first < 0)
                {
                    return;   // nothing is packed
                }
                int district = shop.Orders[first].District, metres = Metres(site, district), travel = Travel(metres);
                int room = Math.Min(van.Capacity, (OnlineCatalogue.DriverMinutes - van.Used - OnlineCatalogue.LoadMinutes - 2 * travel) / OnlineCatalogue.StopMinutes);
                if (room <= 0)
                {
                    continue;   // the driver's day no longer stretches to that district
                }
                int taken = 0;
                Send(shop.Orders[first], now, travel, ++taken);
                for (int i = 0; i < shop.Orders.Count && taken < room; i++)
                {
                    WebOrder o = shop.Orders[i];
                    if (o.Stage == OrderStage.Picked && o.District == district)
                    {
                        Send(o, now, travel, ++taken);
                    }
                }
                int minutes = OnlineCatalogue.LoadMinutes + 2 * travel + OnlineCatalogue.StopMinutes * taken;
                van.FreeAt = now + minutes;
                van.Used += minutes;
                shop.Today.Delivery += VanCost(metres);
                count.Sent += taken;
            }
        }

        private static void Send(WebOrder o, int now, int travel, int door)
        {
            o.Stage = OrderStage.Out;
            o.Arrive = (now + OnlineCatalogue.LoadMinutes + travel + OnlineCatalogue.StopMinutes * door + 59) / 60;
        }

        /// <summary>Orders reach their doors, a late one with part of its value refunded; a packed order no van took is refunded in full.</summary>
        public static void Deliver(OnlineState s, WebShop shop, Site site, int hour, ref HourCount count)
        {
            DayBook book = shop.Today;
            for (int i = 0; i < shop.Orders.Count; i++)
            {
                WebOrder o = shop.Orders[i];
                if (o.Stage == OrderStage.Out && o.Arrive <= hour)
                {
                    bool late = o.Arrive > o.Due;
                    if (late)
                    {
                        int refund = OnlineCatalogue.Share(o.Total, OnlineCatalogue.LateRefund);
                        book.Refunds += refund;
                        shop.BookRefunds += refund;
                        book.Late++;
                        count.Late++;
                    }
                    else
                    {
                        book.OnTime++;
                    }
                    count.Arrived++;
                    Rate(shop, late ? OnlineCatalogue.ScoreLate : o.Short ? OnlineCatalogue.ScoreShort : OnlineCatalogue.ScoreGood, o.Tilt);
                    ComesBack(s, shop, site, o, hour / 24, late);
                    Drop(shop, i--);
                }
                else if (o.Stage == OrderStage.Picked && hour > o.Due + OnlineCatalogue.MissingHours)
                {
                    book.Refunds += o.Total;
                    shop.BookRefunds += o.Total;
                    book.Missing++;
                    count.Missing++;
                    Rate(shop, OnlineCatalogue.ScoreLost, o.Tilt);
                    Drop(shop, i--);
                }
            }
        }

        // A draw from the order's own number against the share for the kind of goods; its first product comes back.
        private static void ComesBack(OnlineState s, WebShop shop, Site site, WebOrder o, int day, bool late)
        {
            int kind = OnlineCatalogue.Clamp(site.Kind, 0, OnlineCatalogue.ReturnShare.Length - 1);
            if (shop.Returns.Count >= OnlineCatalogue.ReturnsMost || Roll(s.Seed, o.Id, o.Placed, 11, 1000) >= OnlineCatalogue.ReturnShare[kind])
            {
                return;
            }
            for (int j = 0; j < o.Count; j++)
            {
                if (o.Got[j] <= 0 || o.Line[j] < 0 || o.Line[j] >= shop.Lines.Count)
                {
                    continue;
                }
                WebReturn r = NewReturn();
                r.Day = day + OnlineCatalogue.ReturnDays;
                r.Item = shop.Lines[o.Line[j]].Item;
                r.Qty = o.Got[j];
                r.Refund = OnlineCatalogue.Share((long)o.Got[j] * o.Unit[j], late ? 1000 - OnlineCatalogue.LateRefund : 1000);   // a late order had its share back already
                r.Cost = o.Paid[j] / o.Got[j];
                shop.Returns.Add(r);
                return;
            }
        }

        /// <summary>Returns that are back are refunded; goods fit to sell again go back into stock if there is room, the rest is written off.</summary>
        public static void Returns(OnlineState s, WebShop shop, Site site, int hour, StockBack put, ref HourCount count)
        {
            if (shop.Returns.Count == 0 || hour % 24 < OnlineCatalogue.ReturnHour)
            {
                return;
            }
            int day = hour / 24, kind = OnlineCatalogue.Clamp(site.Kind, 0, OnlineCatalogue.ResaleShare.Length - 1), key = Key(shop.Place);
            DayBook book = shop.Today;
            for (int i = 0; i < shop.Returns.Count; i++)
            {
                WebReturn r = shop.Returns[i];
                if (r.Day > day)
                {
                    continue;
                }
                book.Refunds += r.Refund;
                shop.BookRefunds += r.Refund;
                book.Returned++;
                count.Returned++;
                bool fit = Roll(s.Seed, key, r.Day * 64 + r.Qty, r.Refund, 1000) < OnlineCatalogue.ResaleShare[kind];
                int back = fit && put != null ? OnlineCatalogue.Clamp(put(r.Item, r.Qty, r.Cost), 0, r.Qty) : 0;
                if (back > 0)
                {
                    int worth = back * r.Cost;
                    book.Goods -= worth;
                    WebLine line = shop.Line(r.Item);
                    if (line != null)
                    {
                        line.Cost -= worth;
                        line.BookCost -= worth;
                    }
                }
                shop.Returns.RemoveAt(i--);
                if (ReturnPool.Count < 64)
                {
                    ReturnPool.Push(r);
                }
            }
        }

        /// <summary>Moves the rating a step towards one order's mark, which is lower for a dear order and higher for a cheap one.</summary>
        public static void Rate(WebShop shop, int score, int tilt)
        {
            if (tilt > OnlineCatalogue.DearAbove)
            {
                score -= OnlineCatalogue.DearCost;
            }
            else if (tilt > 0 && tilt < OnlineCatalogue.CheapBelow)
            {
                score += OnlineCatalogue.CheapGain;
            }
            score = OnlineCatalogue.Clamp(score, OnlineCatalogue.StarsLeast, OnlineCatalogue.StarsMost);
            shop.Stars = OnlineCatalogue.Clamp(shop.Stars + (score - shop.Stars) / OnlineCatalogue.RatingWeight, OnlineCatalogue.StarsLeast, OnlineCatalogue.StarsMost);
        }
    }
}
