using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Economy
{
    /// <summary>Where an order stands, saved as a number. A finished order is counted in the day's book and dropped.</summary>
    public enum OrderStage
    {
        Waiting = 0,    // to be picked
        Picked = 1,     // packed, waiting for a van
        Out = 2         // on its way
    }

    /// <summary>One product of a site's catalogue, with what it sold.</summary>
    public sealed class WebLine
    {
        public string Item = string.Empty;  // the game's name for the product
        public int Price;                   // cents; 0 follows the shop's own price
        public bool On = true;              // offered now

        public int Units, Takings, Cost, Missed;        // today; money in cents, Missed = units asked for and not there
        public readonly int[] WeekUnits = new int[7];   // by the day's number mod 7
        public readonly int[] WeekMissed = new int[7];
        public int BookTakings, BookCost;               // cents not yet in the game's statement of the business

        public int SoldWeek => Sum(WeekUnits) + Units;
        public int MissedWeek => Sum(WeekMissed) + Missed;

        private static int Sum(int[] ring)
        {
            int sum = 0;
            for (int i = 0; i < ring.Length; i++)
            {
                sum += ring[i];
            }
            return sum;
        }
    }

    /// <summary>One order: up to three products for a district, with a promise.</summary>
    public sealed class WebOrder
    {
        public int Id;
        public int District;            // index into OnlineState.Districts
        public int Placed, Due, Arrive; // hours counted from day 0
        public OrderStage Stage;
        public int Count;               // products in it
        public readonly int[] Line = new int[OnlineCatalogue.OrderLines];   // index into the catalogue
        public readonly int[] Qty = new int[OnlineCatalogue.OrderLines];    // asked for
        public readonly int[] Got = new int[OnlineCatalogue.OrderLines];    // picked
        public readonly int[] Unit = new int[OnlineCatalogue.OrderLines];   // cents, the price when ordered
        public readonly int[] Paid = new int[OnlineCatalogue.OrderLines];   // cents the picked units cost the shop
        public int Total;               // cents paid for what was picked
        public int Tilt;                // its prices against the going price when ordered, thousandths
        public bool Short;              // something asked for was not there

        /// <summary>Cents for everything asked for.</summary>
        public int Asked
        {
            get
            {
                int value = 0;
                for (int i = 0; i < Count; i++)
                {
                    value += Qty[i] * Unit[i];
                }
                return value;
            }
        }
    }

    /// <summary>Goods on their way back from a customer.</summary>
    public sealed class WebReturn
    {
        public int Day;         // the day they are back
        public string Item = string.Empty;
        public int Qty;
        public int Refund;      // cents
        public int Cost;        // cents one cost the shop
    }

    /// <summary>One day of one site, in cents.</summary>
    public sealed class DayBook
    {
        public int Day;
        public int Orders;              // placed on the day
        public int Picked, Units;
        public int Takings, Goods, Delivery, Refunds, Hosting;
        public int OnTime, Late, Short, Cancelled, Missing, Returned, Turned;
        public int PaidTakings, PaidRefunds, PaidDelivery;   // of today's, paid in already by a stop in the middle of the day

        public int PaidIn => Takings - Refunds - Delivery - Hosting;
        public int Profit => PaidIn - Goods;

        public void Clear(int day)
        {
            Day = day;
            Orders = Picked = Units = Takings = Goods = Delivery = Refunds = Hosting = 0;
            OnTime = Late = Short = Cancelled = Missing = Returned = Turned = 0;
            PaidTakings = PaidRefunds = PaidDelivery = 0;
        }

        public void Add(DayBook other)
        {
            Orders += other.Orders;
            Picked += other.Picked;
            Units += other.Units;
            Takings += other.Takings;
            Goods += other.Goods;
            Delivery += other.Delivery;
            Refunds += other.Refunds;
            Hosting += other.Hosting;
            OnTime += other.OnTime;
            Late += other.Late;
            Short += other.Short;
            Cancelled += other.Cancelled;
            Missing += other.Missing;
            Returned += other.Returned;
            Turned += other.Turned;
        }

        public void CopyFrom(DayBook other)
        {
            Clear(other.Day);
            Add(other);
        }
    }

    /// <summary>One online shop: a business or a warehouse of the player's with a site.</summary>
    public sealed class WebShop
    {
        public string Place = string.Empty;     // the business, written as the company writes a place
        public string Name = string.Empty;
        public int Quality;                     // 0 Basic, 1 Standard, 2 Professional
        public int Way = OnlineCatalogue.Courier;
        public int Promise = 1;                 // 0 same day, 1 next day
        public bool InHouse;                    // built by the player's own web development agency
        public int OpenDay;
        public int HostDue;                     // the day the next month's hosting is taken
        public bool Offline;                    // hosting unpaid: no orders until it is
        public int Stars = OnlineCatalogue.StarsStart;
        public readonly int[] StarLog = new int[7];     // the rating at each day's close, by the day's number mod 7; 0 for none
        public int AdFrom, AdEnd, AdPercent, AdEnded;   // an advertising campaign: its days, per cent, and the day the last one ended
        public int Idle;                        // hours with orders waiting and nobody to pick them
        public int BookRefunds;                 // cents not yet taken off the takings in the game's statement

        public readonly List<WebLine> Lines = new List<WebLine>(OnlineCatalogue.LinesMost);
        public readonly List<WebOrder> Orders = new List<WebOrder>(32);
        public readonly List<WebReturn> Returns = new List<WebReturn>(8);
        public readonly DayBook Today = new DayBook();
        public readonly DayBook[] Week = { new DayBook(), new DayBook(), new DayBook(), new DayBook(), new DayBook(), new DayBook(), new DayBook() };

        public bool Seen;                       // not saved: its business is still the player's

        public bool Advertising(int day) => AdEnd > 0 && day >= AdFrom && day < AdEnd;

        public WebLine Line(string item)
        {
            for (int i = 0; i < Lines.Count; i++)
            {
                if (string.Equals(Lines[i].Item, item, StringComparison.Ordinal))
                {
                    return Lines[i];
                }
            }
            return null;
        }

        public int Count(OrderStage stage)
        {
            int count = 0;
            for (int i = 0; i < Orders.Count; i++)
            {
                count += Orders[i].Stage == stage ? 1 : 0;
            }
            return count;
        }

        /// <summary>Open orders whose promise has passed at an hour.</summary>
        public int Overdue(int hour)
        {
            int count = 0;
            for (int i = 0; i < Orders.Count; i++)
            {
                count += Orders[i].Due < hour ? 1 : 0;
            }
            return count;
        }

        /// <summary>The six closed days before today and today, added up into the caller's book.</summary>
        public void WeekInto(DayBook into, int today)
        {
            into.Clear(today);
            for (int i = 0; i < Week.Length; i++)
            {
                if (Week[i].Day > today - 7 && Week[i].Day < today && Week[i].Day > 0)
                {
                    into.Add(Week[i]);
                }
            }
            into.Add(Today);
            into.Day = today;
        }

        /// <summary>The rating a week ago, or at the oldest close on record; 0 with none.</summary>
        public int StarsBefore(int today)
        {
            for (int back = 7; back >= 1; back--)
            {
                int day = today - back;
                if (day > 0 && StarLog[day % 7] > 0)
                {
                    return StarLog[day % 7];
                }
            }
            return 0;
        }
    }

    /// <summary>One of the player's vans with its driver. Its free time is shared by all sites.</summary>
    public sealed class Van
    {
        public string Driver = string.Empty;    // the driver's id in the game
        public int FreeAt;                      // minutes counted from day 0
        public int Used;                        // minutes driven on Day
        public int Day;
        public int Capacity;                    // not saved: parcels, read from the game each hour
        public bool Ready;                      // not saved: vehicle and driver are there this hour
    }

    /// <summary>Everything about the online shops that a save keeps.</summary>
    public sealed class OnlineState
    {
        public int Seed;
        public int FirstDay;
        public int LastHour = -1;       // the last hour worked through, counted from day 0; -1 before the first
        public int NextOrder = 1;
        public readonly List<string> Districts = new List<string>(16);  // by the game's names; orders keep the index
        public readonly List<WebShop> Shops = new List<WebShop>(4);
        public readonly List<Van> Vans = new List<Van>(4);

        public WebShop Shop(string place)
        {
            for (int i = 0; i < Shops.Count; i++)
            {
                if (string.Equals(Shops[i].Place, place, StringComparison.Ordinal))
                {
                    return Shops[i];
                }
            }
            return null;
        }

        public Van VanOf(string driver)
        {
            for (int i = 0; i < Vans.Count; i++)
            {
                if (string.Equals(Vans[i].Driver, driver, StringComparison.Ordinal))
                {
                    return Vans[i];
                }
            }
            return null;
        }

        public int District(string name)
        {
            for (int i = 0; i < Districts.Count; i++)
            {
                if (string.Equals(Districts[i], name, StringComparison.Ordinal))
                {
                    return i;
                }
            }
            return -1;
        }
    }

    /// <summary>
    /// What a site's demand is worked out from, read from the game once a day and when the catalogue changes; not
    /// saved. The arrays are kept between days and grow with the districts.
    /// </summary>
    public sealed class Site
    {
        public int Kind = 5;            // MacroCatalogue's kind of goods: general unless the business says otherwise
        public int PerDay;              // orders a day, thousandths
        public int Lines, Districts;
        public int[] Ask = new int[OnlineCatalogue.LinesMost];      // cents, the online price
        public int[] Going = new int[OnlineCatalogue.LinesMost];    // cents, the going price in the city
        public int[] Factor = new int[OnlineCatalogue.LinesMost];   // thousandths, what the price does to sales
        public int[] Stock = new int[OnlineCatalogue.LinesMost];    // units in the building when last counted
        public int[] Demand = new int[OnlineCatalogue.LinesMost * 16];   // the game's demand, [line x districts + district]
        public int[] Want = new int[OnlineCatalogue.LinesMost * 16];     // Demand at the line's price: the weight it is drawn by
        public int[] Weight = new int[16];      // each district's share of the orders
        public int[] Metres = new int[16];      // road to each district's middle
        public bool[] Measured = new bool[16];  // measured along the roads, not a straight line's guess
        public int Need, Price, Stars, Ads, Campaign, Cycle, Season, Crowd;   // the parts of PerDay, for the page and the log

        public void Size(int lines, int districts)
        {
            Lines = lines;
            Districts = districts;
            if (Demand.Length < OnlineCatalogue.LinesMost * districts)
            {
                Demand = new int[OnlineCatalogue.LinesMost * districts];
                Want = new int[OnlineCatalogue.LinesMost * districts];
            }
            if (Weight.Length < districts)
            {
                Weight = new int[districts];
                int[] metres = new int[districts];
                bool[] measured = new bool[districts];
                Array.Copy(Metres, metres, Metres.Length);
                Array.Copy(Measured, measured, Measured.Length);
                Metres = metres;
                Measured = measured;
            }
        }
    }
}
