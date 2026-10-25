using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Economy
{
    /// <summary>Takes units out of the building's real stock: how many it got, and in <paramref name="cost"/> what they cost the shop in cents.</summary>
    public delegate int StockTake(string item, int wanted, out int cost);

    /// <summary>Puts returned units back into the building's stock at their cost: how many found room.</summary>
    public delegate int StockBack(string item, int units, int unitCost);

    /// <summary>What one hour did at one site, for the log.</summary>
    public struct HourCount
    {
        public int Came, Picked, Cancelled, Sent, Arrived, Late, Missing, Returned;
    }

    /// <summary>
    /// The online shops by rule, game-free: demand, orders, the hour and the day (picking and delivery are in
    /// OnlineRules.Delivery). Every draw is a function of the seed, the site and the hour, so a save gives the same
    /// orders however its hours are worked through. An hour of a site is a few passes with no allocation.
    /// </summary>
    public static partial class OnlineRules
    {
        private static readonly Stack<WebOrder> Pool = new Stack<WebOrder>(64);

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

        /// <summary>What the width of the catalogue does: one product brings four tenths of the orders eight do.</summary>
        public static int Breadth(int lines)
        {
            if (lines <= 0)
            {
                return 0;
            }
            return lines >= OnlineCatalogue.BreadthLines ? 1000 : OnlineCatalogue.BreadthLeast + (1000 - OnlineCatalogue.BreadthLeast) * (lines - 1) / (OnlineCatalogue.BreadthLines - 1);
        }

        /// <summary>The game's own demand for the catalogue (0 to 100) as a factor.</summary>
        public static int NeedFactor(int need)
        {
            return OnlineCatalogue.Clamp(need * 1000 / OnlineCatalogue.NeedMiddle, OnlineCatalogue.NeedLeast, OnlineCatalogue.NeedMost);
        }

        /// <summary>What a price does to sales, by its ratio to the going price in thousandths: the inverse square, capped, and nothing from the ceiling up.</summary>
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

        /// <summary>Advertising online (per cent) and half of a campaign at the business itself.</summary>
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
        /// The most orders a site takes in a day: its quality's bound at the building's connection (NetSpeedRules at the
        /// shops' lean), none with no connection, the quality's own bound with nothing known or in the grace.
        /// </summary>
        public static int Most(int quality, int mbps)
        {
            long most = OnlineCatalogue.OrdersMost[OnlineCatalogue.Quality(quality)];
            return (int)(most * NetSpeedRules.Factor(mbps, OnlineCatalogue.NetLean) / 1000);
        }

        /// <summary>Orders a day in thousandths: the quality's figure by every factor, never over the bound.</summary>
        public static int PerDay(int quality, bool inHouse, int lines, int need, int price, int promise, int stars, int ads, int campaign, int cycle, int season, int shops)
        {
            return PerDay(quality, inHouse, lines, need, price, promise, stars, ads, campaign, cycle, season, shops, NetSpeedRules.Unknown);
        }

        /// <summary>The same with the building's connection setting the bound (<see cref="Most"/>).</summary>
        public static int PerDay(int quality, bool inHouse, int lines, int need, int price, int promise, int stars, int ads, int campaign, int cycle, int season, int shops, int mbps)
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
            return (int)Math.Min(v, Most(quality, mbps) * 1000L);
        }

        /// <summary>Works out a site's price factors, district shares and orders a day from what was read into <paramref name="site"/>.</summary>
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
                // Worked out once here so that drawing an order only adds up.
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
            site.PerDay = shop.Offline || on == 0 ? 0 : PerDay(shop.Quality, shop.InHouse, on, site.Need, site.Price, shop.Promise, shop.Stars, site.Ads, campaign, cycle, season, shops, site.Mbps);
        }

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
        /// The hour's orders come in, up to the day's bound at the building's connection; none in an hour the connection is
        /// down; while the site holds all it can, they are turned away.
        /// </summary>
        public static int Arrive(OnlineState s, WebShop shop, Site site, int hour)
        {
            if (site.PerDay <= 0 || shop.Offline || site.Down)
            {
                return 0;
            }
            int key = Key(shop.Place), most = Most(shop.Quality, site.Mbps);
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

        /// <summary>One order: a district by its share, one to three products by what it wants at their price, fewer of a dear one. Null with nothing that sells.</summary>
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

        private static int Wanted(Site site, int line, int district)
        {
            return site.Want[line * site.Districts + district];
        }

        /// <summary>One hour at one site, in the order things happen: returns, deliveries, new orders, picking, rounds.</summary>
        public static HourCount Hour(OnlineState s, WebShop shop, Site site, int hour, int pickers, StockTake take, StockBack put)
        {
            return Hour(s, shop, site, hour, pickers, true, take, put);
        }

        /// <summary>The same for an hour the place is open in or not: a closed hour's waiting orders count for nothing (OnlineRules.IdleAfter).</summary>
        public static HourCount Hour(OnlineState s, WebShop shop, Site site, int hour, int pickers, bool open, StockTake take, StockBack put)
        {
            var count = new HourCount();
            Returns(s, shop, site, hour, put, ref count);
            Deliver(s, shop, site, hour, ref count, put);
            count.Came = Arrive(s, shop, site, hour);
            Pick(shop, site, hour, pickers, open, take, ref count);
            Rounds(s, shop, site, hour, ref count);
            return count;
        }

        /// <summary>
        /// Hands the day's book to <paramref name="closed"/>, less what a stop earlier that day paid in, keeps it in the
        /// week's ring and opens the book of <paramref name="day"/>. On the book's own day (a stop in the middle of it) only
        /// the money not yet paid in is handed over, and the day stays open with all its figures.
        /// </summary>
        public static void CloseDay(WebShop shop, int day, DayBook closed)
        {
            DayBook t = shop.Today;
            if (t.Day == day && day > 0)
            {
                closed.Clear(day);
                closed.Takings = t.Takings - t.PaidTakings;
                closed.Refunds = t.Refunds - t.PaidRefunds;
                closed.Delivery = t.Delivery - t.PaidDelivery;
                t.PaidTakings = t.Takings;
                t.PaidRefunds = t.Refunds;
                t.PaidDelivery = t.Delivery;
                return;
            }
            closed.CopyFrom(t);
            closed.Takings -= t.PaidTakings;
            closed.Refunds -= t.PaidRefunds;
            closed.Delivery -= t.PaidDelivery;
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
                // Days nothing ran on: their slots still hold a week or more before.
                int slot = skipped % 7;
                shop.Week[slot].Clear(0);
                shop.StarLog[slot] = 0;
                for (int l = 0; l < shop.Lines.Count; l++)
                {
                    shop.Lines[l].WeekUnits[slot] = shop.Lines[l].WeekMissed[slot] = 0;
                }
            }
            int opens = day % 7;   // the day that opens takes the slot of the day a week before it, which leaves the week
            for (int l = 0; l < shop.Lines.Count; l++)
            {
                WebLine line = shop.Lines[l];
                line.Units = line.Takings = line.Cost = line.Missed = 0;
                line.WeekUnits[opens] = line.WeekMissed[opens] = 0;
            }
            if (shop.AdEnd > 0 && day >= shop.AdEnd)
            {
                shop.AdEnded = shop.AdEnd;
                shop.AdFrom = shop.AdEnd = shop.AdPercent = 0;
            }
            t.Clear(day);
        }

        /// <summary>After hours the site did not run nobody is blamed: unpicked orders go unrated, packed or sent ones count as on time.</summary>
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

        /// <summary>The shop's own price of a product in cents at a price level: the game's price is the start's (Core/Prices).</summary>
        public static int ShopCents(float price, int level)
        {
            return (int)Math.Round(price * (level / 1000f) * 100f);
        }

        /// <summary>
        /// A site's sums waiting for the game's statement: the takings (each line's, never under nothing) and the cost of
        /// goods, which goods back on the shelves can take under nothing. False with nothing to enter.
        /// </summary>
        public static bool Unbooked(WebShop shop, out long takings, out long cost)
        {
            takings = cost = 0;
            bool costs = false;
            for (int l = 0; l < shop.Lines.Count; l++)
            {
                takings += Math.Max(0, shop.Lines[l].BookTakings);
                cost += shop.Lines[l].BookCost;
                costs |= shop.Lines[l].BookCost != 0;
            }
            return takings != 0 || costs;
        }

        public static bool HostingDue(WebShop shop, int day)
        {
            return day >= shop.HostDue;
        }

        public static void HostingPaid(WebShop shop, int day, int cents, int monthDays)
        {
            shop.Today.Hosting += Math.Max(0, cents);
            shop.HostDue = day + Math.Max(1, monthDays);
            shop.Offline = false;
        }

        /// <summary>The cash did not cover hosting: the site takes no orders, and the fee stays due each day.</summary>
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

        /// <summary>Advertising is priced and weighed as the company's campaigns are: by the economy, and less for a repeat too soon.</summary>
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

        /// <summary>A campaign from tomorrow.</summary>
        public static void Advertise(WebShop shop, int day, int percent)
        {
            shop.AdFrom = day + 1;
            shop.AdEnd = shop.AdFrom + OnlineCatalogue.AdDays;
            shop.AdPercent = OnlineCatalogue.Clamp(percent, 0, OnlineCatalogue.AdMost);
        }

        /// <summary>A new site, its first month's hosting in the set-up. Null when it cannot be opened.</summary>
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

        /// <summary>Puts a product in the catalogue, or offers it again. Null when the catalogue is full.</summary>
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

        /// <summary>Takes products no longer offered out once no open order, sale or unbooked sum hangs on them; orders' indexes follow.</summary>
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
