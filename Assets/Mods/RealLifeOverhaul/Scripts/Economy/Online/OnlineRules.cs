using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Economy
{
    /// <summary>Takes units of a product out of the real stock of the building. Answers how many it got and, in <paramref name="cost"/>, what they cost the shop in cents.</summary>
    public delegate int StockTake(string item, int wanted, out int cost);

    /// <summary>Puts returned units back into the building's stock at their cost. Answers how many found room.</summary>
    public delegate int StockBack(string item, int units, int unitCost);

    /// <summary>What one hour did at one site, for the log.</summary>
    public struct HourCount
    {
        public int Came, Picked, Cancelled, Sent, Arrived, Late, Missing, Returned;
    }

    /// <summary>
    /// The online shops by rule: how many orders come in an hour and what is in them, picking, the rounds of the
    /// player's vans and the courier, lateness, the rating, returns and the day's book. Game-free: stock is reached
    /// through two delegates, and every draw is a function of the save's seed, the site and the hour, so the same save
    /// gives the same orders whenever and in whatever order the hours are worked through.
    ///
    /// Cost: one hour of one site is a handful of passes over its open orders (80 at most) and its catalogue (12 at
    /// most), with no allocation (orders are taken from and given back to a pool). Nothing here is ever called per
    /// frame: the director calls <see cref="Hour"/> on the game's hour event.
    /// </summary>
    public static class OnlineRules
    {
        private static readonly Stack<WebOrder> Pool = new Stack<WebOrder>(64);
        private static readonly Stack<WebReturn> ReturnPool = new Stack<WebReturn>(16);

        // ---------------------------------------------------------------- draws

        /// <summary>A number from the seed and three more, the same every time.</summary>
        public static uint Mix(int seed, int a, int b, int c)
        {
            unchecked
            {
                uint h = (uint)seed * 2654435761u + 0x9e3779b9u;
                h ^= (uint)a + 0x9e3779b9u + (h << 6) + (h >> 2);
                h ^= (uint)b + 0x85ebca6bu + (h << 6) + (h >> 2);
                h ^= (uint)c + 0xc2b2ae35u + (h << 6) + (h >> 2);
                h ^= h >> 16;
                h *= 0x85ebca6bu;
                h ^= h >> 13;
                h *= 0xc2b2ae35u;
                h ^= h >> 16;
                return h;
            }
        }

        public static int Roll(int seed, int a, int b, int c, int below)
        {
            return below <= 1 ? 0 : (int)(Mix(seed, a, b, c) % (uint)below);
        }

        /// <summary>A number for a site that is always the same for it.</summary>
        public static int Key(string place)
        {
            unchecked
            {
                uint h = 2166136261u;
                for (int i = 0; place != null && i < place.Length; i++)
                {
                    h = (h ^ place[i]) * 16777619u;
                }
                return (int)(h & 0x3fffffff);
            }
        }

        // ---------------------------------------------------------------- demand

        /// <summary>What the width of the catalogue does: one product brings four tenths of the orders eight do.</summary>
        public static int Breadth(int lines)
        {
            if (lines <= 0)
            {
                return 0;
            }
            return lines >= OnlineCatalogue.BreadthLines ? 1000 : OnlineCatalogue.BreadthLeast + (1000 - OnlineCatalogue.BreadthLeast) * (lines - 1) / (OnlineCatalogue.BreadthLines - 1);
        }

        /// <summary>The game's own demand for the catalogue's products over all districts (0 to 100), as a factor.</summary>
        public static int NeedFactor(int need)
        {
            return OnlineCatalogue.Clamp(need * 1000 / OnlineCatalogue.NeedMiddle, OnlineCatalogue.NeedLeast, OnlineCatalogue.NeedMost);
        }

        /// <summary>
        /// What a price does to sales, by its ratio to the going price in thousandths: the inverse square (ten per cent
        /// dearer sells a sixth less, ten per cent cheaper a quarter more), no more than double however cheap, and
        /// nothing at all from sixty per cent over the going price.
        /// </summary>
        public static int PriceFactor(int ratio)
        {
            if (ratio <= 0 || ratio >= OnlineCatalogue.PriceCeiling)
            {
                return 0;
            }
            long r = Math.Max(ratio, OnlineCatalogue.PriceFloor);
            return (int)Math.Min(1000L * 1000 * 1000 / (r * r), OnlineCatalogue.PriceFactorMost);
        }

        public static int StarsFactor(int stars)
        {
            return OnlineCatalogue.StarsFloorFactor + OnlineCatalogue.Clamp(stars, OnlineCatalogue.StarsLeast, OnlineCatalogue.StarsMost) / 5;
        }

        /// <summary>Advertising online (per cent) and half of a campaign that runs at the business itself.</summary>
        public static int MarketingFactor(int ads, int campaign)
        {
            return 1000 + 10 * OnlineCatalogue.Clamp(ads, 0, OnlineCatalogue.AdMost) + 10 * OnlineCatalogue.Clamp(campaign, 0, CompanyCatalogue.CampaignMost) * OnlineCatalogue.CampaignShare / 1000;
        }

        /// <summary>What sharing the city with the player's other sites leaves of each one's orders.</summary>
        public static int Crowd(int shops)
        {
            return 1000 * 1000 / (1000 + OnlineCatalogue.CrowdStep * (Math.Max(1, shops) - 1));
        }

        /// <summary>
        /// The orders a site takes in a day, in thousandths of an order: its quality's figure by every factor, and
        /// never more than the bound. Each factor only ever moves it its own way: a better site, a wider catalogue,
        /// more demand, a lower price, a sooner promise, a better rating and more marketing never give fewer orders.
        /// </summary>
        public static int PerDay(int quality, bool inHouse, int lines, int need, int price, int promise, int stars, int ads, int campaign, int cycle, int season, int shops)
        {
            int q = OnlineCatalogue.Quality(quality);
            long v = OnlineCatalogue.OrdersADay[q] * 1000L;
            v = v * (inHouse ? OnlineCatalogue.AgencyOrders : 1000) / 1000;
            v = v * Breadth(lines) / 1000;
            v = v * NeedFactor(need) / 1000;
            v = v * OnlineCatalogue.Clamp(price, 0, OnlineCatalogue.PriceFactorMost) / 1000;
            v = v * OnlineCatalogue.PromiseFactor[OnlineCatalogue.Clamp(promise, 0, 1)] / 1000;
            v = v * StarsFactor(stars) / 1000;
            v = v * MarketingFactor(ads, campaign) / 1000;
            v = v * OnlineCatalogue.CycleFactor[OnlineCatalogue.Clamp(cycle, 0, 3)] / 1000;
            v = v * OnlineCatalogue.SeasonFactor[OnlineCatalogue.Clamp(season, 0, 3)] / 1000;
            v = v * Crowd(shops) / 1000;
            return (int)Math.Min(v, OnlineCatalogue.OrdersMost[q] * 1000L);
        }

        /// <summary>
        /// Works a site's day out from what was read into <paramref name="site"/> (prices, the going prices, the
        /// game's demand by district): each product's price factor, each district's share of the orders, and the
        /// orders a day. A site whose hosting is unpaid, or with nothing on offer that anybody would buy, takes none.
        /// </summary>
        public static void Plan(WebShop shop, Site site, int day, int campaign, int cycle, int season, int shops)
        {
            int lines = Math.Min(shop.Lines.Count, site.Lines), districts = site.Districts, on = 0;
            long need = 0, price = 0;
            for (int d = 0; d < districts; d++)
            {
                site.Weight[d] = 0;
            }
            for (int l = 0; l < lines; l++)
            {
                int going = site.Going[l];
                site.Factor[l] = shop.Lines[l].On && going > 0 && site.Ask[l] > 0 ? PriceFactor((int)Math.Min(int.MaxValue, (long)site.Ask[l] * 1000 / going)) : 0;
                if (!shop.Lines[l].On)
                {
                    continue;
                }
                on++;
                price += site.Factor[l];
                for (int d = 0; d < districts; d++)
                {
                    int wanted = site.Demand[l * districts + d];
                    need += wanted;
                    site.Weight[d] += wanted * site.Factor[l] / 1000;
                }
            }
            for (int l = 0; l < lines; l++)
            {
                // What each district wants of each product at its price, worked out once here so that drawing an order only adds up; nothing of one nobody would buy.
                for (int d = 0; d < districts; d++)
                {
                    site.Want[l * districts + d] = site.Factor[l] <= 0 ? 0 : site.Demand[l * districts + d] * site.Factor[l] / 1000 + 1;
                }
            }
            site.Need = on > 0 && districts > 0 ? (int)(need / (on * districts)) : 0;
            site.Price = on > 0 ? (int)(price / on) : 0;
            site.Stars = shop.Stars;
            site.Ads = shop.Advertising(day) ? shop.AdPercent : 0;
            site.Campaign = campaign;
            site.Cycle = cycle;
            site.Season = season;
            site.Crowd = shops;
            site.PerDay = shop.Offline || on == 0 ? 0 : PerDay(shop.Quality, shop.InHouse, on, site.Need, site.Price, shop.Promise, shop.Stars, site.Ads, campaign, cycle, season, shops);
        }

        // ---------------------------------------------------------------- orders

        public static WebOrder NewOrder()
        {
            WebOrder o = Pool.Count > 0 ? Pool.Pop() : new WebOrder();
            o.Id = o.District = o.Placed = o.Due = o.Arrive = o.Count = o.Total = o.Tilt = 0;
            o.Stage = OrderStage.Waiting;
            o.Short = false;
            for (int i = 0; i < OnlineCatalogue.OrderLines; i++)
            {
                o.Line[i] = -1;
                o.Qty[i] = o.Got[i] = o.Unit[i] = o.Paid[i] = 0;
            }
            return o;
        }

        public static void Recycle(WebOrder order)
        {
            if (order != null && Pool.Count < 256)
            {
                Pool.Push(order);
            }
        }

        public static WebReturn NewReturn()
        {
            return ReturnPool.Count > 0 ? ReturnPool.Pop() : new WebReturn();
        }

        /// <summary>The hour an order placed at an hour is promised for.</summary>
        public static int Due(int promise, int hour)
        {
            int day = hour / 24, h = hour % 24;
            if (promise != 0)
            {
                return (day + 1) * 24 + OnlineCatalogue.NextDayHour;
            }
            return h < OnlineCatalogue.SameDayCutoff ? day * 24 + Math.Min(h + OnlineCatalogue.SameDayHours, OnlineCatalogue.SameDayLatest) : (day + 1) * 24 + OnlineCatalogue.NextMorning;
        }

        /// <summary>How many orders an hour brings: the day's figure by the hour's share, the fraction settled by a draw.</summary>
        public static int Expected(int seed, int key, int perDay, int hour)
        {
            int expected = (int)((long)perDay * OnlineCatalogue.HourShare[hour % 24] / 1000);   // thousandths of an order
            return expected / 1000 + (Roll(seed, key, hour, 1, 1000) < expected % 1000 ? 1 : 0);
        }

        /// <summary>
        /// The orders of an hour come in. No more are taken in a day than the site's bound, and none while the site
        /// already holds as many open orders as it can (those are turned away and counted).
        /// </summary>
        public static int Arrive(OnlineState s, WebShop shop, Site site, int hour)
        {
            if (site.PerDay <= 0 || shop.Offline)
            {
                return 0;
            }
            int key = Key(shop.Place), most = OnlineCatalogue.OrdersMost[OnlineCatalogue.Quality(shop.Quality)];
            int n = Expected(s.Seed, key, site.PerDay, hour), came = 0;
            for (int k = 0; k < n && shop.Today.Orders < most; k++)
            {
                if (shop.Orders.Count >= OnlineCatalogue.OpenMost)
                {
                    shop.Today.Turned++;
                    continue;
                }
                WebOrder o = Draw(s, shop, site, hour, key, k);
                if (o == null)
                {
                    break;
                }
                shop.Orders.Add(o);
                shop.Today.Orders++;
                came++;
            }
            return came;
        }

        /// <summary>
        /// One order: a district by the districts' shares, one to three products by what that district wants of each
        /// at its price, and a few of each (fewer of a dear one). Null with nothing on offer that sells.
        /// </summary>
        public static WebOrder Draw(OnlineState s, WebShop shop, Site site, int hour, int key, int k)
        {
            int lines = Math.Min(shop.Lines.Count, site.Lines), districts = site.Districts, sellable = 0;
            for (int l = 0; l < lines; l++)
            {
                sellable += site.Factor[l] > 0 ? 1 : 0;
            }
            if (sellable == 0 || districts <= 0)
            {
                return null;
            }
            int salt = hour * 16 + k;
            long all = 0;
            for (int d = 0; d < districts; d++)
            {
                all += site.Weight[d] + 1;
            }
            long at = Mix(s.Seed, key, salt, 2) % all;
            int district = 0;
            for (int d = 0; d < districts; d++)
            {
                at -= site.Weight[d] + 1;
                if (at < 0)
                {
                    district = d;
                    break;
                }
            }
            int roll = Roll(s.Seed, key, salt, 3, 100);
            int count = Math.Min(sellable, roll < OnlineCatalogue.OneLine ? 1 : roll < OnlineCatalogue.TwoLines ? 2 : OnlineCatalogue.OrderLines);
            WebOrder o = NewOrder();
            o.Id = s.NextOrder++;
            if (s.NextOrder > 99999999)
            {
                s.NextOrder = 1;
            }
            o.District = district;
            o.Placed = hour;
            o.Due = Due(shop.Promise, hour);
            long tilt = 0;
            for (int c = 0; c < count; c++)
            {
                long total = 0;
                for (int l = 0; l < lines; l++)
                {
                    total += Taken(o, c, l) ? 0 : Wanted(site, l, district);
                }
                long pick = Mix(s.Seed, key, salt, 4 + c) % Math.Max(1, total);
                int line = -1;
                for (int l = 0; l < lines && line < 0; l++)
                {
                    if (Taken(o, c, l))
                    {
                        continue;
                    }
                    pick -= Wanted(site, l, district);
                    line = pick < 0 ? l : -1;
                }
                if (line < 0)
                {
                    break;
                }
                int ask = site.Ask[line];
                o.Line[c] = line;
                o.Unit[c] = ask;
                o.Qty[c] = ask < OnlineCatalogue.CheapUnder ? 1 + Roll(s.Seed, key, salt, 8 + c, 3) : ask < OnlineCatalogue.DearFrom ? 1 + Roll(s.Seed, key, salt, 8 + c, 2) : 1;
                tilt += site.Going[line] > 0 ? (long)ask * 1000 / site.Going[line] : 1000;
                o.Count = c + 1;
            }
            if (o.Count == 0)
            {
                Recycle(o);
                return null;
            }
            o.Tilt = (int)(tilt / o.Count);
            return o;
        }

        private static bool Taken(WebOrder o, int upTo, int line)
        {
            for (int i = 0; i < upTo; i++)
            {
                if (o.Line[i] == line)
                {
                    return true;
                }
            }
            return false;
        }

        // What a district wants of a product at its price (OnlineRules.Plan).
        private static int Wanted(Site site, int line, int district)
        {
            return site.Want[line * site.Districts + district];
        }

        // ---------------------------------------------------------------- picking

        /// <summary>
        /// The staff on shift pick, the oldest order first, six orders each in the hour. What is picked leaves the real
        /// stock and is charged; what is not there is left out (a short order), and an order with nothing of it there
        /// is cancelled. With a courier a picked order is collected at once; with the player's own vans it waits for a
        /// round. An order nobody picked by a day after its promise is given up by the customer.
        /// </summary>
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

        /// <summary>
        /// One order is picked: each product is taken out of the real stock, as much of it as is there; the order is
        /// charged for what was picked and handed to the courier or set by for a van. False if nothing of it was
        /// there: it is cancelled, marked against the rating and dropped.
        /// </summary>
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

        // ---------------------------------------------------------------- delivery

        /// <summary>What a courier charges for a parcel, cents: a base and so much a kilometre of road.</summary>
        public static int CourierFee(int metres)
        {
            // (The catalogue's cents are the start's: at the city's price level, Core/Prices.)
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

        /// <summary>What a round there and back costs in fuel and wear, cents.</summary>
        public static int VanCost(int metres)
        {
            return CompanyCatalogue.AtPrices((int)(2L * Math.Max(0, metres) * OnlineCatalogue.VanCostPerKm / 1000), Prices.Level);
        }

        /// <summary>
        /// The player's vans take rounds: each van that is back, in the hours rounds leave, takes the packed order
        /// promised soonest and with it the others for the same district, as many as it holds and as its driver's day
        /// still stretches to. The van is away for loading, the way there and back and five minutes a door.
        /// </summary>
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
                    continue;   // what is left of this driver's day does not stretch to that district
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

        /// <summary>
        /// Orders reach their doors. One that comes after its promise has a quarter of its value paid back; one that
        /// was packed and never taken by a van is refunded in full a day after its promise. Each moves the rating, and
        /// a share of the delivered ones, by the kind of goods, will come back.
        /// </summary>
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

        // Whether a delivered order will be sent back: a draw from the order's own number against the share for the kind of goods. The first product of it comes back.
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
                r.Refund = OnlineCatalogue.Share((long)o.Got[j] * o.Unit[j], late ? 1000 - OnlineCatalogue.LateRefund : 1000);   // (a late order has had a quarter back already)
                r.Cost = o.Paid[j] / o.Got[j];
                shop.Returns.Add(r);
                return;
            }
        }

        /// <summary>
        /// Returns that are back are dealt with, from ten in the morning: the customer has the money back, and the
        /// goods go back into stock if they are fit to sell again (a draw by the kind of goods) and there is room;
        /// otherwise they are written off.
        /// </summary>
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

        // ---------------------------------------------------------------- the rating

        /// <summary>
        /// One order's mark moves the rating a twentieth of the way to it: five for on time and whole, three for
        /// short, two for late, one for cancelled or never delivered; half a star less for a dear order, a quarter
        /// more for a cheap one. The rating stays between one and five stars and moves 0.2 at the very most.
        /// </summary>
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

        // ---------------------------------------------------------------- the hour and the day

        /// <summary>
        /// One hour at one site, in the order things happen: returns are dealt with, orders reach their doors, new
        /// orders come in, the staff pick, the vans leave.
        /// </summary>
        public static HourCount Hour(OnlineState s, WebShop shop, Site site, int hour, int pickers, StockTake take, StockBack put)
        {
            var count = new HourCount();
            Returns(s, shop, site, hour, put, ref count);
            Deliver(s, shop, site, hour, ref count);
            count.Came = Arrive(s, shop, site, hour);
            Pick(shop, site, hour, pickers, take, ref count);
            Rounds(s, shop, site, hour, ref count);
            return count;
        }

        /// <summary>
        /// The day's book is closed and the book of <paramref name="day"/> opened: the closed one is copied to
        /// <paramref name="closed"/> for whoever pays it in, and kept as one of the week's seven with the rating at
        /// its close and each product's units. An advertising campaign whose days are over is ended.
        /// </summary>
        public static void CloseDay(WebShop shop, int day, DayBook closed)
        {
            DayBook t = shop.Today;
            closed.CopyFrom(t);
            if (t.Day > 0)
            {
                int slot = t.Day % 7;
                shop.Week[slot].CopyFrom(t);
                shop.StarLog[slot] = shop.Stars;
                for (int l = 0; l < shop.Lines.Count; l++)
                {
                    WebLine line = shop.Lines[l];
                    line.WeekUnits[slot] = line.Units;
                    line.WeekMissed[slot] = line.Missed;
                }
            }
            for (int skipped = Math.Max(t.Day + 1, day - 7); skipped < day; skipped++)
            {
                // (days on which nothing ran: what the ring still holds for them is from a week or more before)
                int slot = skipped % 7;
                shop.Week[slot].Clear(0);
                shop.StarLog[slot] = 0;
                for (int l = 0; l < shop.Lines.Count; l++)
                {
                    shop.Lines[l].WeekUnits[slot] = shop.Lines[l].WeekMissed[slot] = 0;
                }
            }
            for (int l = 0; l < shop.Lines.Count; l++)
            {
                WebLine line = shop.Lines[l];
                line.Units = line.Takings = line.Cost = line.Missed = 0;
            }
            if (shop.AdEnd > 0 && day >= shop.AdEnd)
            {
                shop.AdEnded = shop.AdEnd;
                shop.AdFrom = shop.AdEnd = shop.AdPercent = 0;
            }
            t.Clear(day);
        }

        /// <summary>
        /// The site was not running for some hours (the option was off, or the save was played without the mod):
        /// nobody is blamed for them. Orders not yet picked are dropped unrated; what was packed or on its way counts
        /// as delivered on time, since it was charged.
        /// </summary>
        public static void Resume(OnlineState s, WebShop shop)
        {
            for (int i = shop.Orders.Count - 1; i >= 0; i--)
            {
                shop.Today.OnTime += shop.Orders[i].Stage != OrderStage.Waiting ? 1 : 0;
                Drop(shop, i);
            }
            shop.Idle = 0;
            for (int v = 0; v < s.Vans.Count; v++)
            {
                s.Vans[v].FreeAt = 0;
            }
        }

        // ---------------------------------------------------------------- hosting, advertising, the catalogue

        public static bool HostingDue(WebShop shop, int day)
        {
            return day >= shop.HostDue;
        }

        /// <summary>A month's hosting was taken: the site is up for the month.</summary>
        public static void HostingPaid(WebShop shop, int day, int cents, int monthDays)
        {
            shop.Today.Hosting += Math.Max(0, cents);
            shop.HostDue = day + Math.Max(1, monthDays);
            shop.Offline = false;
        }

        /// <summary>The cash did not cover it: the site is down, takes no orders, and the fee is asked for again each day.</summary>
        public static void HostingMissed(WebShop shop)
        {
            shop.Offline = true;
        }

        public static int SetUpCost(int quality, bool inHouse, int index)
        {
            int cost = CompanyCatalogue.AtPrices(OnlineCatalogue.SetUp[OnlineCatalogue.Quality(quality)], index);
            return inHouse ? cost - OnlineCatalogue.Share(cost, OnlineCatalogue.AgencyDiscount) : cost;
        }

        public static int HostingCost(int quality, int index)
        {
            return CompanyCatalogue.AtPrices(OnlineCatalogue.Hosting[OnlineCatalogue.Quality(quality)], index);
        }

        /// <summary>
        /// Advertising online is one of the company's campaigns in all but where it works: a price that follows the
        /// city's prices, a number of days from tomorrow, more orders by the economy (CompanyCatalogue.CampaignCycle)
        /// and less for a repeat too soon (CampaignRules.Repeat).
        /// </summary>
        public static void AdQuote(WebShop shop, int day, int index, int cycle, out int cost, out int percent)
        {
            cost = CompanyCatalogue.AtPrices(OnlineCatalogue.AdCost, index);
            long effect = (long)OnlineCatalogue.AdPercent * CompanyCatalogue.CampaignCycle[OnlineCatalogue.Clamp(cycle, 0, CompanyCatalogue.CampaignCycle.Length - 1)] / 1000
                          * CampaignRules.Repeat(shop.AdEnded, day + 1);
            percent = OnlineCatalogue.Clamp((int)((effect + 500) / 1000), 1, OnlineCatalogue.AdMost);
        }

        public static bool CanAdvertise(WebShop shop)
        {
            return shop.AdEnd <= 0;
        }

        public static void Advertise(WebShop shop, int day, int percent)
        {
            shop.AdFrom = day + 1;
            shop.AdEnd = shop.AdFrom + OnlineCatalogue.AdDays;
            shop.AdPercent = OnlineCatalogue.Clamp(percent, 0, OnlineCatalogue.AdMost);
        }

        /// <summary>A new site, with its first month's hosting in the set-up.</summary>
        public static WebShop Open(OnlineState s, string place, string name, int quality, int way, int promise, bool inHouse, int day, int monthDays)
        {
            if (s.Shops.Count >= OnlineCatalogue.ShopsMost || string.IsNullOrEmpty(place) || s.Shop(place) != null)
            {
                return null;
            }
            var shop = new WebShop
            {
                Place = place, Name = name ?? string.Empty, Quality = OnlineCatalogue.Quality(quality), Way = OnlineCatalogue.Clamp(way, 0, OnlineCatalogue.Ways - 1),
                Promise = OnlineCatalogue.Clamp(promise, 0, 1), InHouse = inHouse, OpenDay = day, HostDue = day + Math.Max(1, monthDays)
            };
            shop.Today.Clear(day);
            s.Shops.Add(shop);
            return shop;
        }

        /// <summary>Puts a product in the catalogue (or offers it again). Null when the catalogue is full.</summary>
        public static WebLine Offer(WebShop shop, string item)
        {
            WebLine line = shop.Line(item);
            if (line == null)
            {
                if (shop.Lines.Count >= OnlineCatalogue.LinesMost || string.IsNullOrEmpty(item))
                {
                    return null;
                }
                line = new WebLine { Item = item };
                shop.Lines.Add(line);
            }
            line.On = true;
            return line;
        }

        /// <summary>
        /// Products no longer offered are taken out of the catalogue once nothing hangs on them: no order still to be
        /// picked, nothing sold today, nothing waiting to be entered in the books. Orders keep their products' places.
        /// </summary>
        public static int Prune(WebShop shop)
        {
            int removed = 0;
            for (int l = shop.Lines.Count - 1; l >= 0; l--)
            {
                WebLine line = shop.Lines[l];
                if (line.On || line.Units != 0 || line.Takings != 0 || line.Cost != 0 || line.BookTakings != 0 || line.BookCost != 0 || Waits(shop, l))
                {
                    continue;
                }
                shop.Lines.RemoveAt(l);
                removed++;
                for (int i = 0; i < shop.Orders.Count; i++)
                {
                    WebOrder o = shop.Orders[i];
                    for (int j = 0; j < o.Count; j++)
                    {
                        o.Line[j] = o.Line[j] == l ? -1 : o.Line[j] > l ? o.Line[j] - 1 : o.Line[j];
                    }
                }
            }
            return removed;
        }

        private static bool Waits(WebShop shop, int line)
        {
            for (int i = 0; i < shop.Orders.Count; i++)
            {
                WebOrder o = shop.Orders[i];
                for (int j = 0; o.Stage == OrderStage.Waiting && j < o.Count; j++)
                {
                    if (o.Line[j] == line)
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
