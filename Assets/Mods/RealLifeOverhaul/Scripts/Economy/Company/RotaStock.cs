using System;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The goods of one shop as the staff rota planner counts them, filled from the game by RotaGame.ReadStock;
    /// nothing here knows the game. A plan's week is valued against them (<see cref="RotaStockRun"/>), so that no plan
    /// counts sales of goods the shop will not have.
    ///
    /// THE GAME'S OWN RULES, AS THEY ARE USED HERE
    ///   selling     a customer buys a product only from a shelf that holds it (RetailBusinessSimulator, its list of
    ///               shelves with stock); a sale takes one unit, and a shelf that runs empty is filled again at once
    ///               from the storage and from any vehicle parked at the business (ItemHelper.SubtractFromStock,
    ///               ReStockingHelper.RefillSingleItemWithLimit). So what sells out is everything on site, not one shelf
    ///   paper bags  where the business type needs them, each customer who buys goods takes a bag at the till, and a
    ///               till with none is passed over: with no bag left anywhere nobody buys goods
    ///               (RetailBusinessSimulator.GetIdealPointOfSale and ProcessPaperBag)
    ///   deliveries  a delivery contract that repeats arrives at 8 on a Monday (DeliveryHelper, BusinessHelper.
    ///               HandleWholesaleDeliveries), onto the shelves that hold the product and then into the business's
    ///               storage, a box at a time; what finds no room goes back and is refunded (DeliverCargoToBuilding).
    ///               An Urgent Order (BizManContractSettings.MakeUrgentOrder) brings the contract's next delivery to
    ///               8 the next morning, at the game's surcharge, and the contract goes back to Mondays after it: a
    ///               delivery once, not a day of the week, so a week of a plan is counted without it. It counts towards
    ///               the wholesaler's weekly limit of the product (Item.maxWholesaleOrderAmount), and after it the game
    ///               lowers the contract's amount to what is left of that week's limit (DeliverWholesaleContract): one
    ///               wholesaler never sends more than the limit a week, however it is ordered
    ///   rounds      a logistics manager's round from a warehouse runs at 2 every morning and tops each product up to
    ///               its target, counted over everything on site (LogisticsManagerPlanDestination.ProcessStockTarget)
    ///
    /// A product no delivery and no round brings is restocked by hand, if at all: its sales are not capped, and the
    /// page says how long the stock there lasts. Money is in hundredths; amounts of goods in thousandths of a unit
    /// where a customer's share of one is meant.
    /// </summary>
    public sealed class RotaStock
    {
        public const int ProductsMost = 32;
        public const int DeliveryHour = 8;      // a Monday, the planner's day 0 (DeliveryHelper.DeliveryHour)
        public const int RoundHour = 2;         // every day (GameManager runs LogisticsManagerHelper.DoAllWarehouseDeliveries at 2)

        public int Products;
        public readonly int[] PerCustomer = new int[ProductsMost];  // thousandths of a unit a customer served takes (a bag: a customer who buys goods)
        public readonly int[] UnitMargin = new int[ProductsMost];   // hundredths a unit sold leaves after its cost
        public readonly int[] OnSite = new int[ProductsMost];       // units there now: shelves, storage, a vehicle parked there
        public readonly int[] Shelf = new int[ProductsMost];        // units the shelves that sell it hold at most
        public readonly int[] Box = new int[ProductsMost];          // units a box of it holds in storage
        public readonly int[] Weekly = new int[ProductsMost];       // units the delivery contracts bring at 8 on a Monday
        public readonly int[] Target = new int[ProductsMost];       // a logistics manager tops it up to so many units at 2 every morning; 0: no round
        public readonly int[] Most = new int[ProductsMost];         // the most of it the wholesalers deliver in a week; 0: no figure
        public readonly int[] Warehouse = new int[ProductsMost];    // units the warehouse of its round holds now
        public readonly bool[] Bag = new bool[ProductsMost];        // a paper bag (see above)
        public readonly bool[] Backorder = new bool[ProductsMost];  // the wholesaler has none this week: the next Monday delivery brings none (said, not counted: a week's event)
        public readonly bool[] Shortage = new bool[ProductsMost];   // the wholesaler is short this week and sends less (said, not counted)
        public int Boxes;       // box places in the business's storage that these goods may use
        public int Goods;       // hundredths a customer leaves for goods that are not counted here one by one (they still need a bag)
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
            Array.Clear(Bag, 0, ProductsMost);
            Array.Clear(Backorder, 0, ProductsMost);
            Array.Clear(Shortage, 0, ProductsMost);
        }

        /// <summary>
        /// Splits what a customer leaves (<paramref name="margin"/>, hundredths) into the products counted one by one,
        /// <see cref="Goods"/> for those that are not, and <see cref="Other"/>: <paramref name="goods"/> is the part of
        /// the margin that is goods of any kind. Nothing is counted twice and nothing is lost: Other + Goods + every
        /// product's share is the margin.
        /// </summary>
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

        /// <summary>What a customer leaves in all, by the split (the margin it was made from, give or take rounding).</summary>
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

        /// <summary>Units a week the customers would buy, rounded.</summary>
        public int WantUnits(int p)
        {
            return (int)Math.Min(int.MaxValue, (Want[p] + 500) / 1000);
        }

        /// <summary>Units the busiest day's customers would buy, rounded.</summary>
        public int DayUnits(int p)
        {
            return (int)Math.Min(int.MaxValue, (DayMost[p] + 500) / 1000);
        }

        /// <summary>Days the units given last at this week's sales; a week or more: 7.</summary>
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

        /// <summary>Which of the above holds the product back in a week that ran it out; <see cref="None"/> if it did not run out.</summary>
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
            if (stock.Weekly[p] > 0 && stock.Most[p] > 0 && stock.Weekly[p] >= stock.Most[p])
            {
                return Second;
            }
            return Order;
        }
    }

    /// <summary>
    /// A week of a plan against the goods, hour by hour, as the game runs them: the round at 2, the delivery at 8 on a
    /// Monday, then the hour's customers. One run is two weeks long, as the cleanliness of RotaLadder.Evaluate: the first
    /// starts with what is there now and the second is the one counted, so that what a plan comes to is what it comes to
    /// week after week with the deliveries there are. Nothing is allocated.
    /// </summary>
    public sealed class RotaStockRun
    {
        private readonly long[] _left = new long[RotaStock.ProductsMost];  // thousandths of a unit on site
        private RotaStock _stock;
        private bool _bags;

        /// <summary>True if some goods ran out in the last hour sold.</summary>
        public bool Short { get; private set; }

        /// <summary>Starts a run with what is on site now.</summary>
        public void Start(RotaStock stock)
        {
            _stock = stock;
            _bags = false;
            for (int p = 0; p < RotaStock.ProductsMost; p++)
            {
                _left[p] = p < stock.Products ? (long)Math.Max(0, stock.OnSite[p]) * 1000 : 0;
                _bags |= p < stock.Products && stock.Bag[p] && stock.Capped(p);
            }
        }

        /// <summary>Units of a product on site in the run now.</summary>
        public int Left(int p)
        {
            return p >= 0 && p < RotaStock.ProductsMost ? (int)Math.Min(int.MaxValue, _left[p] / 1000) : 0;
        }

        /// <summary>
        /// The deliveries due at the start of an hour of the week: the round at <see cref="RotaStock.RoundHour"/> every
        /// day and the delivery contracts at <see cref="RotaStock.DeliveryHour"/> on a Monday. <paramref name="week"/>
        /// (may be null) is told what came and what went back.
        /// </summary>
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
                        Bring(p, Math.Max(0, s.Target[p] - Left(p)), week);
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
                }
            }
        }

        // So many units of a product arrive: onto its shelves, then into storage, a box at a time; the rest goes back.
        private void Bring(int p, int units, RotaStockWeek week)
        {
            if (units <= 0)
            {
                return;
            }
            int room = Room(p), taken = Math.Min(units, room);
            _left[p] += (long)taken * 1000;
            if (week != null)
            {
                week.Delivered[p] += taken;
                week.Refused[p] += units - taken;
            }
        }

        /// <summary>The units of a product that would fit on site now: room on its shelves, in its boxes, and in the storage's free box places.</summary>
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

        // The boxes a product takes in storage: what its shelves cannot hold, in whole boxes.
        private int Boxes(int q)
        {
            RotaStock s = _stock;
            int stored = Left(q) - Math.Min(Left(q), Math.Max(0, s.Shelf[q]));
            int box = Math.Max(1, s.Box[q]);
            return stored <= 0 ? 0 : (stored + box - 1) / box;
        }

        /// <summary>
        /// The customers served in an hour buy what is there: hundredths they leave in all, at the cleanliness factor
        /// given (thousandths, as RotaLadder.Factor). <paramref name="week"/> (may be null) counts it.
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
                long want = buyers * s.PerCustomer[p] / 1000 * factor / 1000;
                if (_bags)
                {
                    long all = (long)served * s.PerCustomer[p] * factor / 1000;
                    goodsLost += (all - want) * s.UnitMargin[p] / 1000;
                }
                if (!s.Capped(p))
                {
                    value += want * s.UnitMargin[p] / 1000;
                    Count(week, p, cell, want, want, 0);
                    _left[p] = Math.Max(0, _left[p] - want);   // (only for how long it lasts: nothing caps it)
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
