using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Economy
{
    /// <summary>Where an order stands. Saved as a number. An order that is delivered, cancelled or refunded is counted in the day's book and kept no longer.</summary>
    public enum OrderStage
    {
        Waiting = 0,    // to be picked
        Picked = 1,     // packed, waiting for one of the player's vans
        Out = 2         // on its way
    }

    /// <summary>One product of a site's catalogue, with what it sold.</summary>
    public sealed class WebLine
    {
        public string Item = string.Empty;  // the game's name for the product
        public int Price;                   // the online price in cents; 0 follows the shop's own price
        public bool On = true;              // offered now

        public int Units, Takings, Cost, Missed;        // today: units sold, what they brought and cost (cents), units asked for that were not there
        public readonly int[] WeekUnits = new int[7];   // units sold on each of the last seven days that closed, by the day's number
        public readonly int[] WeekMissed = new int[7];
        public int BookTakings, BookCost;               // not yet entered in the game's statement of the business (cents)

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
        public int District;            // its place in the state's list of districts
        public int Placed, Due, Arrive; // hours counted from day 0 (day x 24 + the hour)
        public OrderStage Stage;
        public int Count;               // products in it
        public readonly int[] Line = new int[OnlineCatalogue.OrderLines];   // each one's place in the catalogue
        public readonly int[] Qty = new int[OnlineCatalogue.OrderLines];    // asked for
        public readonly int[] Got = new int[OnlineCatalogue.OrderLines];    // picked
        public readonly int[] Unit = new int[OnlineCatalogue.OrderLines];   // the price of one when it was ordered, cents
        public readonly int[] Paid = new int[OnlineCatalogue.OrderLines];   // what the picked ones cost the shop, cents
        public int Total;               // what the customer paid for what was picked, cents
        public int Tilt;                // its prices against the going price when ordered, thousandths
        public bool Short;              // something asked for was not there

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
        public int Cost;        // what one cost the shop, cents
    }

    /// <summary>One day of one site, in cents. What was paid in is takings less refunds, delivery and hosting.</summary>
    public sealed class DayBook
    {
        public int Day;
        public int Orders;              // placed on the day
        public int Picked, Units;       // picked on the day, and the units in them
        public int Takings, Goods, Delivery, Refunds, Hosting;
        public int OnTime, Late, Short, Cancelled, Missing, Returned, Turned;

        public int PaidIn => Takings - Refunds - Delivery - Hosting;
        public int Profit => PaidIn - Goods;
        public int Asked => OnTime + Late + Cancelled + Missing;    // orders that came to an end on the day

        public void Clear(int day)
        {
            Day = day;
            Orders = Picked = Units = Takings = Goods = Delivery = Refunds = Hosting = 0;
            OnTime = Late = Short = Cancelled = Missing = Returned = Turned = 0;
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
        public int Way = OnlineCatalogue.Courier;   // who delivers
        public int Promise = 1;                 // 0 same day, 1 next day
        public bool InHouse;                    // built by the player's own web development agency
        public int OpenDay;
        public int HostDue;                     // the day the next month's hosting is taken
        public bool Offline;                    // hosting unpaid: the site takes no orders until it is
        public int Stars = OnlineCatalogue.StarsStart;
        public readonly int[] StarLog = new int[7];     // the rating at the close of each of the last seven days, by the day's number; 0 for a day not yet closed
        public int AdFrom, AdEnd, AdPercent, AdEnded;   // an advertising campaign: its days, what it adds, and the day the last one ended
        public int Idle;                        // hours running with orders waiting and nobody to pick them
        public int BookRefunds;                 // refunds not yet taken off the takings in the game's statement, cents

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

        /// <summary>The last seven closed days and today, added up into a book of the caller's.</summary>
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

    /// <summary>One of the player's vans with its driver, as the rounds keep it. Its free time is one for all sites.</summary>
    public sealed class Van
    {
        public string Driver = string.Empty;    // the driver's id in the game
        public int FreeAt;                      // the minute (counted from day 0) it is back from its round
        public int Used;                        // minutes driven on <see cref="Day"/>
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
        public readonly List<string> Districts = new List<string>(16);  // the city's districts by the game's names; an order keeps its place in this list
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
    /// What a site's demand is worked out from, read from the game once a day (and when the player changes the
    /// catalogue): nothing of it is saved. Arrays are as long as the catalogue or the list of districts and are kept
    /// between days.
    /// </summary>
    public sealed class Site
    {
        public int Kind = 5;            // the kind of goods (MacroCatalogue's six): general goods unless the business says otherwise
        public bool Warehouse;
        public int PerDay;              // orders a day in thousandths, every factor in
        public int Lines, Districts;
        public int[] Ask = new int[OnlineCatalogue.LinesMost];      // each product's online price, cents
        public int[] Going = new int[OnlineCatalogue.LinesMost];    // ...the going price in the city, cents
        public int[] Factor = new int[OnlineCatalogue.LinesMost];   // ...what its price does to its sales, thousandths
        public int[] Stock = new int[OnlineCatalogue.LinesMost];    // ...units in the building when last counted
        public int[] Demand = new int[OnlineCatalogue.LinesMost * 16];   // the game's demand for each product in each district (product x districts + district)
        public int[] Want = new int[OnlineCatalogue.LinesMost * 16];     // ...and what that comes to at the product's price, as the weight it is drawn by
        public int[] Weight = new int[16];      // each district's share of the orders
        public int[] Metres = new int[16];      // road from the building to each district's middle
        public bool[] Measured = new bool[16];  // ...measured along the roads, not yet a straight line's guess
        public int Need, Price, Stars, Ads, Campaign, Cycle, Season, Crowd;   // the parts of PerDay as they were worked out, for the page

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
