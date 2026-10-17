using System;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// One shop's goods as the planner counts them, filled by RotaGame.ReadStock, so that no plan counts sales of goods
    /// the shop will not have. Only what a repeating contract or a round brings caps sales: an Urgent Order is a delivery
    /// once, and the rest is restocked by hand. Money in hundredths; a customer's share of a unit in thousandths.
    /// </summary>
    public sealed class RotaStock
    {
        public const int ProductsMost = 32;
        public const int DeliveryHour = 8;      // hour of a Monday, the planner's day 0 (DeliveryHelper.DeliveryHour)
        public const int RoundHour = 2;         // hour of every day (LogisticsManagerHelper.DoAllWarehouseDeliveries)

        public int Products;
        public readonly int[] PerCustomer = new int[ProductsMost];  // thousandths of a unit a customer served takes (a bag: a customer who buys goods)
        public readonly int[] UnitMargin = new int[ProductsMost];   // hundredths a unit sold leaves after its cost
        public readonly int[] OnSite = new int[ProductsMost];       // units there now: shelves, storage, a vehicle parked there
        public readonly int[] Shelf = new int[ProductsMost];        // units the shelves that sell it hold at most
        public readonly int[] Box = new int[ProductsMost];          // units a box of it holds in storage
        public readonly int[] Weekly = new int[ProductsMost];       // units the delivery contracts bring at 8 on a Monday
        public readonly int[] Target = new int[ProductsMost];       // units a logistics round tops it up to at 2 every morning; 0: no round
        public readonly int[] Most = new int[ProductsMost];         // units the contracts' wholesalers deliver in a week at most, each line its own; 0: no figure
        public readonly int[] Warehouse = new int[ProductsMost];    // units the warehouses of its rounds hold now, each counted once
        public readonly int[] Restock = new int[ProductsMost];      // units those warehouses' own contracts bring them at 8 on a Monday
        public readonly bool[] Bag = new bool[ProductsMost];        // a paper bag
        public readonly bool[] Backorder = new bool[ProductsMost];  // the wholesaler has none this week (said, not counted: a week's event)
        public readonly bool[] Shortage = new bool[ProductsMost];   // the wholesaler is short this week and sends less (said, not counted)
        public int Boxes;       // box places in the business's storage these goods may use
        public int Goods;       // hundredths a customer leaves for goods not counted here one by one (they still need a bag)
        public int Other;       // hundredths a customer leaves for what needs neither goods nor a bag: entrance fees, services

        /// <summary>True for a product whose sales a plan caps: a delivery or a round brings it.</summary>
        public bool Capped(int p)
        {
            return p >= 0 && p < Products && (Weekly[p] > 0 || Target[p] > 0);
        }

        /// <summary>True if any product's sales are capped.</summary>
        public bool Any
        {
            get
            {
                for (int p = 0; p < Products; p++)
                {
                    if (Capped(p))
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        public void Clear()
        {
            Products = Boxes = Goods = Other = 0;
            Array.Clear(PerCustomer, 0, ProductsMost);
            Array.Clear(UnitMargin, 0, ProductsMost);
            Array.Clear(OnSite, 0, ProductsMost);
            Array.Clear(Shelf, 0, ProductsMost);
            Array.Clear(Box, 0, ProductsMost);
            Array.Clear(Weekly, 0, ProductsMost);
            Array.Clear(Target, 0, ProductsMost);
            Array.Clear(Most, 0, ProductsMost);
            Array.Clear(Warehouse, 0, ProductsMost);
            Array.Clear(Restock, 0, ProductsMost);
            Array.Clear(Bag, 0, ProductsMost);
            Array.Clear(Backorder, 0, ProductsMost);
            Array.Clear(Shortage, 0, ProductsMost);
        }

        /// <summary>
        /// A repeating contract's line for a product: what it brings on a Monday and its wholesaler's weekly limit (0: none).
        /// Each line has a limit of its own, so the most is their sum.
        /// </summary>
        public void Contract(int p, int amount, int limit)
        {
            Weekly[p] += Math.Max(0, amount);
            Most[p] += Math.Max(0, limit);
        }

        /// <summary>
        /// What sales leave after their goods: the takings at today's prices (the city's takings line, <paramref name="factor"/>
        /// thousandths) less what the goods cost, at today's prices already. A customer's margin and each product's are made
        /// the same way, so the one splits into the others.
        /// </summary>
        public static double Left(double takings, double goods, int factor)
        {
            return takings * factor / 1000.0 - goods;
        }

        /// <summary>Splits a customer's <paramref name="margin"/>, <paramref name="goods"/> of it goods, into the products, Goods and Other.</summary>
        public void Split(int margin, int goods)
        {
            long counted = 0;
            for (int p = 0; p < Products; p++)
            {
                counted += (long)PerCustomer[p] * UnitMargin[p] / 1000;
            }
            goods = Math.Max(goods, (int)Math.Min(int.MaxValue, counted));
            Goods = (int)Math.Max(0L, goods - counted);
            Other = (int)Math.Max(int.MinValue / 2, Math.Min(int.MaxValue / 2, (long)margin - goods));
        }

        /// <summary>What a customer leaves in all by the split: the margin it was made from, give or take rounding.</summary>
        public long Margin
        {
            get
            {
                long all = (long)Goods + Other;
                for (int p = 0; p < Products; p++)
                {
                    all += (long)PerCustomer[p] * UnitMargin[p] / 1000;
                }
                return all;
            }
        }
    }

    /// <summary>What the goods came to in one week of a plan, product by product.</summary>
    public sealed class RotaStockWeek
    {
        public readonly long[] Want = new long[RotaStock.ProductsMost];     // thousandths of a unit the week's customers would buy
        public readonly long[] Sold = new long[RotaStock.ProductsMost];     // and of those, what was there for them
        public readonly long[] Lost = new long[RotaStock.ProductsMost];     // hundredths the rest would have left (a bag: the goods not bought for want of one)
        public readonly int[] Out = new int[RotaStock.ProductsMost];        // the first hour of the week it ran out in; -1: it did not
        public readonly int[] Delivered = new int[RotaStock.ProductsMost];  // units the deliveries and rounds brought
        public readonly int[] Refused = new int[RotaStock.ProductsMost];    // units of a delivery that found no room and went back
        public readonly long[] DayMost = new long[RotaStock.ProductsMost];  // thousandths of a unit the busiest day's customers would buy
        private readonly long[] _dayWant = new long[RotaStock.ProductsMost];
        private int _day = -1;
        public int Products;
        public long LostAll;    // hundredths in all
        public int Short;       // products that ran out

        public void Clear(int products)
        {
            Products = products;
            LostAll = 0;
            Short = 0;
            Array.Clear(Want, 0, RotaStock.ProductsMost);
            Array.Clear(Sold, 0, RotaStock.ProductsMost);
            Array.Clear(Lost, 0, RotaStock.ProductsMost);
            Array.Clear(Delivered, 0, RotaStock.ProductsMost);
            Array.Clear(Refused, 0, RotaStock.ProductsMost);
            Array.Clear(DayMost, 0, RotaStock.ProductsMost);
            Array.Clear(_dayWant, 0, RotaStock.ProductsMost);
            _day = -1;
            for (int p = 0; p < RotaStock.ProductsMost; p++)
            {
                Out[p] = -1;
            }
        }

        // An hour of the week begins: a new day starts its count of what the customers would buy.
        internal void Hour(int cell)
        {
            int day = cell / RotaCatalogue.Hours;
            if (day != _day)
            {
                _day = day;
                Array.Clear(_dayWant, 0, RotaStock.ProductsMost);
            }
        }

        internal void Wanted(int p, long want)
        {
            _dayWant[p] += want;
            DayMost[p] = Math.Max(DayMost[p], _dayWant[p]);
        }

        public void CopyFrom(RotaStockWeek from)
        {
            Products = from.Products;
            LostAll = from.LostAll;
            Short = from.Short;
            Array.Copy(from.Want, Want, RotaStock.ProductsMost);
            Array.Copy(from.Sold, Sold, RotaStock.ProductsMost);
            Array.Copy(from.Lost, Lost, RotaStock.ProductsMost);
            Array.Copy(from.Out, Out, RotaStock.ProductsMost);
            Array.Copy(from.Delivered, Delivered, RotaStock.ProductsMost);
            Array.Copy(from.Refused, Refused, RotaStock.ProductsMost);
            Array.Copy(from.DayMost, DayMost, RotaStock.ProductsMost);
        }

        public int WantUnits(int p)
        {
            return (int)Math.Min(int.MaxValue, (Want[p] + 500) / 1000);
        }

        public int DayUnits(int p)
        {
            return (int)Math.Min(int.MaxValue, (DayMost[p] + 500) / 1000);
        }

        /// <summary>Days the units given last at this week's sales; 7 for a week or more.</summary>
        public int DaysOf(int p, int units)
        {
            long want = Want[p];
            if (want <= 0)
            {
                return 7;
            }
            long days = (long)units * 1000 * 7 / want;
            return (int)Math.Max(0, Math.Min(7, days));
        }
    }

    /// <summary>What would let a product that runs out sell to every customer, in the order the planner looks.</summary>
    public static class RotaLift
    {
        public const int None = 0;
        public const int Room = 1;          // a delivery or a round finds no room for all of it: more storage
        public const int Target = 2;        // the round's target is below what a busy day sells
        public const int Order = 3;         // the Monday delivery is smaller than a week's sales: a larger amount, or an Urgent Order in the week
        public const int Second = 4;        // the Monday delivery is the most one wholesaler sends a week: a second wholesaler, or a round from a warehouse

        /// <summary>Which of the above holds back a product the week ran out of; <see cref="None"/> if it did not run out.</summary>
        public static int Of(RotaStock stock, RotaStockWeek week, int p)
        {
            if (p < 0 || p >= stock.Products || week.Out[p] < 0)
            {
                return None;
            }
            if (week.Refused[p] > 0)
            {
                return Room;
            }
            if (stock.Target[p] > 0)
            {
                return Target;
            }
            if (stock.Weekly[p] > 0 && stock.Most[p] > 0 && stock.Weekly[p] >= stock.Most[p])   // every line at its wholesaler's limit
            {
                return Second;
            }
            return Order;
        }
    }

    /// <summary>
    /// A week of a plan against the goods, hour by hour as the game runs it: a logistics round tops each product up to
    /// its target at 2, with no more than its warehouse holds (LogisticsManagerPlanDestination.ProcessStockTarget); the
    /// contracts arrive at 8 on a Monday, onto the shelves, then into storage a box at a time, the rest refunded
    /// (DeliverCargoToBuilding), and the warehouse's own at the same hour; a sale takes a unit and an empty shelf refills
    /// at once from all on site (ItemHelper.SubtractFromStock); without a bag, where the type needs one, nobody buys goods
    /// (RetailBusinessSimulator.GetIdealPointOfSale). A run is two weeks, the first from what is there now, the second
    /// counted. Nothing is allocated.
    /// </summary>
    public sealed class RotaStockRun
    {
        private readonly long[] _left = new long[RotaStock.ProductsMost];  // thousandths of a unit on site
        private readonly int[] _store = new int[RotaStock.ProductsMost];   // units in the warehouse of its round
        private RotaStock _stock;
        private bool _bags;

        /// <summary>True if some goods ran out in the last hour sold.</summary>
        public bool Short { get; private set; }

        public void Start(RotaStock stock)
        {
            _stock = stock;
            _bags = false;
            for (int p = 0; p < RotaStock.ProductsMost; p++)
            {
                _left[p] = p < stock.Products ? (long)Math.Max(0, stock.OnSite[p]) * 1000 : 0;
                _store[p] = p < stock.Products ? Math.Max(0, stock.Warehouse[p]) : 0;
                _bags |= p < stock.Products && stock.Bag[p] && stock.Capped(p);
            }
        }

        /// <summary>Units of a product on site in the run now.</summary>
        public int Left(int p)
        {
            return p >= 0 && p < RotaStock.ProductsMost ? (int)Math.Min(int.MaxValue, _left[p] / 1000) : 0;
        }

        /// <summary>The deliveries due as an hour of the week begins; <paramref name="week"/> (may be null) is told what came and went back.</summary>
        public void Arrive(int cell, RotaStockWeek week)
        {
            RotaStock s = _stock;
            int hour = cell % RotaCatalogue.Hours;
            if (hour == RotaStock.RoundHour)
            {
                for (int p = 0; p < s.Products; p++)
                {
                    if (s.Target[p] > 0)
                    {
                        _store[p] -= Bring(p, Math.Min(_store[p], Math.Max(0, s.Target[p] - Left(p))), week);   // what finds no room goes back to the warehouse
                    }
                }
            }
            if (cell == RotaStock.DeliveryHour)
            {
                for (int p = 0; p < s.Products; p++)
                {
                    if (s.Weekly[p] > 0)
                    {
                        Bring(p, s.Weekly[p], week);
                    }
                    _store[p] += s.Target[p] > 0 ? Math.Max(0, s.Restock[p]) : 0;
                }
            }
        }

        // So many units arrive: onto the shelves, then into storage a box at a time; the rest goes back. Returns those taken in.
        private int Bring(int p, int units, RotaStockWeek week)
        {
            if (units <= 0)
            {
                return 0;
            }
            int room = Room(p), taken = Math.Min(units, room);
            _left[p] += (long)taken * 1000;
            if (week != null)
            {
                week.Delivered[p] += taken;
                week.Refused[p] += units - taken;
            }
            return taken;
        }

        /// <summary>The units of a product that would fit on site now: room on its shelves, in its boxes and in free box places.</summary>
        public int Room(int p)
        {
            RotaStock s = _stock;
            int used = 0;
            for (int q = 0; q < s.Products; q++)
            {
                used += Boxes(q);
            }
            int units = Left(p), box = Math.Max(1, s.Box[p]);
            int onShelf = Math.Min(units, Math.Max(0, s.Shelf[p])), stored = units - onShelf;
            long room = (long)(Math.Max(0, s.Shelf[p]) - onShelf) + ((long)Boxes(p) * box - stored) + (long)Math.Max(0, s.Boxes - used) * box;
            return (int)Math.Max(0, Math.Min(int.MaxValue, room));
        }

        // The whole boxes a product takes in storage: what its shelves cannot hold.
        private int Boxes(int q)
        {
            RotaStock s = _stock;
            int stored = Left(q) - Math.Min(Left(q), Math.Max(0, s.Shelf[q]));
            int box = Math.Max(1, s.Box[q]);
            return stored <= 0 ? 0 : (stored + box - 1) / box;
        }

        /// <summary>
        /// The customers served in an hour buy what is there: hundredths they leave in all, at the cleanliness factor given
        /// (thousandths, as RotaLadder.Factor). <paramref name="week"/> (may be null) counts it.
        /// </summary>
        public long Sell(int cell, int served, int factor, RotaStockWeek week)
        {
            RotaStock s = _stock;
            Short = false;
            if (served <= 0)
            {
                return 0;
            }
            long buyers = (long)served * 1000;  // thousandths of a customer who buy goods
            long value = 0;
            if (week != null)
            {
                week.Hour(cell);
            }
            if (_bags)
            {
                for (int p = 0; p < s.Products; p++)
                {
                    if (!s.Bag[p] || !s.Capped(p))
                    {
                        continue;
                    }
                    long want = (long)served * s.PerCustomer[p], take = Math.Min(want, _left[p]);
                    _left[p] -= take;
                    value += take * s.UnitMargin[p] / 1000;
                    if (want > take && want > 0)
                    {
                        buyers = buyers * take / want;
                        Short = true;
                    }
                    Count(week, p, cell, want, take, 0);
                }
            }
            value += (long)served * s.Other * factor / 1000 + buyers * s.Goods / 1000 * factor / 1000;
            long goodsLost = (long)served * 1000 - buyers > 0 ? ((long)served * 1000 - buyers) * s.Goods / 1000 * factor / 1000 : 0;
            for (int p = 0; p < s.Products; p++)
            {
                if (s.Bag[p] && s.Capped(p))
                {
                    continue;
                }
                // Cleanliness changes how much a customer buys, not that a customer who buys takes one bag (ProcessPaperBag).
                int scale = s.Bag[p] ? 1000 : factor;
                long want = buyers * s.PerCustomer[p] / 1000 * scale / 1000;
                if (_bags)
                {
                    long all = (long)served * s.PerCustomer[p] * scale / 1000;
                    goodsLost += (all - want) * s.UnitMargin[p] / 1000;
                }
                if (!s.Capped(p))
                {
                    value += want * s.UnitMargin[p] / 1000;
                    Count(week, p, cell, want, want, 0);
                    _left[p] = Math.Max(0, _left[p] - want);   // only for how long it lasts: nothing caps it
                    continue;
                }
                long take = Math.Min(want, _left[p]);
                _left[p] -= take;
                value += take * s.UnitMargin[p] / 1000;
                Short |= want > take;
                Count(week, p, cell, want, take, (want - take) * s.UnitMargin[p] / 1000);
            }
            if (week != null && goodsLost > 0)
            {
                // The goods not bought for want of a bag are the bag's to answer for.
                for (int p = 0; p < s.Products; p++)
                {
                    if (s.Bag[p] && s.Capped(p))
                    {
                        week.Lost[p] += goodsLost;
                        week.LostAll += goodsLost;
                        break;
                    }
                }
            }
            return value;
        }

        private static void Count(RotaStockWeek week, int p, int cell, long want, long take, long lost)
        {
            if (week == null)
            {
                return;
            }
            week.Want[p] += want;
            week.Wanted(p, want);
            week.Sold[p] += take;
            week.Lost[p] += lost;
            week.LostAll += lost;
            if (want > take && week.Out[p] < 0)
            {
                week.Out[p] = cell;
                week.Short++;
            }
        }
    }
}
