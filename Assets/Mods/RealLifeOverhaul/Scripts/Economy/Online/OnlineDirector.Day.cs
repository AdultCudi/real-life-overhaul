using System;
using System.Collections.Generic;
using System.Globalization;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>The online shops' director, part: the game's hour (orders worked through) and day (books, statements, hosting, demand).</summary>
    public sealed partial class OnlineDirector
    {
        private const int CatchUp = 3;      // hours missed still worked through one by one; after more, nobody is blamed for the gap

        // The game's hour event (every hour, and first thing in its NewDay before its daily run), after the credit side's.
        private void OnHour()
        {
            if (!_running || _broken || Save == null)
            {
                return;
            }
            try
            {
                int now = Now, day = Save.Day;
                if (now <= _state.LastHour)
                {
                    return;
                }
                bool opens = false;
                for (int i = 0; i < _places.Count; i++)
                {
                    if (_places[i].Shop.Today.Day < day)
                    {
                        // Yesterday's book is paid in, dated yesterday, before the game's daily run. The hour's own
                        // orders wait for the day event, which enters yesterday's sales in the statement first.
                        CloseBook(_places[i], day);
                        opens = true;
                    }
                }
                if (opens && Save.Hour == 0)
                {
                    _dayOpen = true;
                    return;
                }
                StepTo(now);
            }
            catch (Exception e)
            {
                Fault("the hour", e);
            }
        }

        // Every hour not yet worked through, up to now.
        private void StepTo(int now)
        {
            _dayOpen = false;
            if (_places.Count == 0)
            {
                _state.LastHour = now;
                return;
            }
            if (now - _state.LastHour > CatchUp)
            {
                for (int i = 0; i < _places.Count; i++)
                {
                    OnlineRules.Resume(_state, _places[i].Shop);
                }
                _state.LastHour = now - 1;
            }
            float started = Time.realtimeSinceStartup;
            int came = 0, picked = 0, cancelled = 0, sent = 0, arrived = 0, late = 0, returned = 0, hours = 0;
            for (int hour = _state.LastHour + 1; hour <= now; hour++, hours++)
            {
                OnlineGame.ReadVans(_state, hour / 24);
                for (int i = 0; i < _places.Count; i++)
                {
                    WebPlace place = _places[i];
                    NetHour(place, hour);
                    place.Pickers = OnlineGame.Pickers(place, _state, hour);
                    _stockAt = place;
                    int idle = place.Shop.Idle;
                    HourCount count = OnlineRules.Hour(_state, place.Shop, place.Site, hour, place.Pickers, OnlineGame.OpenAt(place, hour), _take, _put);
                    OnlineGame.Picked(place.Reg);
                    came += count.Came;
                    picked += count.Picked;
                    cancelled += count.Cancelled + count.Missing;
                    sent += count.Sent;
                    arrived += count.Arrived;
                    late += count.Late;
                    returned += count.Returned;
                    if (idle < OnlineCatalogue.IdleAlert && place.Shop.Idle >= OnlineCatalogue.IdleAlert && Settings.EconomyOnlineWaitNotes)
                    {
                        Tell(NotificationType.Warning, "reallife_online_note_idle", new Dictionary<string, string>
                        {
                            { "name", place.Shop.Name }, { "count", place.Shop.Count(OrderStage.Waiting).ToString() }
                        }, TabOrders, null);
                    }
                }
                _state.LastHour = hour;
            }
            _stockAt = null;
            int mark = AlertMark();
            if (mark != _alertMark || came + picked + cancelled + sent + arrived + returned > 0)
            {
                Changed();
            }
            if (Verbose && came + picked + cancelled + sent + arrived + returned > 0)
            {
                Logger?.Info("Online check (hour " + (now % 24) + " of day " + (now / 24) + ", " + hours + " hour(s) worked through): " + _places.Count + " site(s), " + came + " order(s) came, "
                             + picked + " picked from real stock, " + cancelled + " cancelled or given up, " + sent + " sent, " + arrived + " at the door of which " + late + " late, "
                             + returned + " returned; " + ((Time.realtimeSinceStartup - started) * 1000f).ToString("0.00", CultureInfo.InvariantCulture) + " ms: PASS");
            }
        }

        private int TakeStock(string item, int wanted, out int cost) => OnlineGame.Take(_stockAt.Reg, item, wanted, out cost);

        private int PutStock(string item, int units, int unitCost) => OnlineGame.PutBack(_stockAt.Reg, item, units, unitCost);

        /// <summary>Closes a site's book and pays its takings, refunds and delivery in, dated the book's day. True if any money moved.</summary>
        private bool CloseBook(WebPlace place, int day)
        {
            WebShop shop = place.Shop;
            OnlineRules.CloseDay(shop, day, _closed);
            DayBook b = _closed;
            if (b.Takings == 0 && b.Refunds == 0 && b.Delivery == 0)
            {
                return false;
            }
            int? dated = b.Day > 0 && b.Day < Save.Day ? (int?)Math.Max(b.Day, Save.Day - 1) : null;   // yesterday at the earliest, as the game dates its own daily entries
            float before = Save.Money;
            OnlineGame.Give(b.Takings, OnlineGame.TakingsKey, place, dated);
            OnlineGame.Take(b.Refunds, OnlineGame.RefundsKey, place, dated, false, true);
            OnlineGame.Take(b.Delivery, shop.Way == OnlineCatalogue.Courier ? OnlineGame.CourierKey : OnlineGame.VansKey, place, dated, true, true);
            if (Verbose)
            {
                float moved = Save.Money - before, wanted = (b.Takings - b.Refunds - b.Delivery) / 100f;
                // The game keeps cash in a float: a few millionths of it is as near as two readings can be.
                bool near = Mathf.Abs(moved - wanted) <= 0.02f + (Mathf.Abs(before) + Mathf.Abs(wanted)) * 0.000004f;
                Logger?.Info("Online check (the book of day " + b.Day + ", " + shop.Name + "): " + b.Orders + " order(s) came, " + b.Picked + " picked, takings " + Cents(b.Takings)
                             + ", cost of goods " + Cents(b.Goods) + ", refunds " + Cents(b.Refunds) + ", delivery " + Cents(b.Delivery) + ", hosting " + Cents(b.Hosting)
                             + " (taken when due); the game's cash moved by " + moved.ToString("0.00", CultureInfo.InvariantCulture)
                             + " against " + wanted.ToString("0.00", CultureInfo.InvariantCulture) + " wanted: " + (near ? "PASS" : "FAIL, the cash did not move by the book's sum"));
            }
            return true;
        }

        private static string Cents(int cents)
        {
            return (cents / 100f).ToString("0.00", CultureInfo.InvariantCulture);
        }

        // The game's day event (after its daily run), after the credit side has settled its day.
        private void OnDay()
        {
            if (!_running || _broken || Save == null)
            {
                return;
            }
            try
            {
                float started = Time.realtimeSinceStartup;
                int day = Save.Day;
                _index = Prices.Level;
                int gone = ReadPlaces(), booked = 0, waiting = 0, sales = 0, goods = 0, pruned = 0;
                for (int i = 0; i < _places.Count; i++)
                {
                    WebPlace place = _places[i];
                    if (place.Shop.Today.Day < day)
                    {
                        CloseBook(place, day);   // a day event that no hour event came before
                    }
                    int s, g;
                    if (OnlineGame.Book(place, day - 1, out s, out g))
                    {
                        booked += s != 0 || g != 0 ? 1 : 0;
                        sales += s;
                        goods += g;
                    }
                    else
                    {
                        waiting++;
                    }
                    Host(place, day);
                    pruned += OnlineRules.Prune(place.Shop);
                }
                float entered = Time.realtimeSinceStartup;
                PlanAll(day);
                if (_dayOpen || Now > _state.LastHour)
                {
                    StepTo(Now);   // the orders of the hour that opened the day
                }
                Changed();
                if (Verbose)
                {
                    float done = Time.realtimeSinceStartup;
                    Logger?.Info("Online check (day " + day + "): " + _places.Count + " site(s)" + (gone > 0 ? ", " + gone + " closed with its business" : string.Empty) + "; " + booked
                                 + " site(s) had yesterday's sales of " + Cents(sales) + " and goods of " + Cents(goods) + " entered in the game's statement for day " + (day - 1)
                                 + (waiting > 0 ? ", " + waiting + " wait for a statement the game has not made" : string.Empty)
                                 + (pruned > 0 ? ", " + pruned + " product(s) no longer offered taken out" : string.Empty)
                                 + "; prices " + (_index / 10f).ToString("0.0", CultureInfo.InvariantCulture) + ", the economy " + CityEconomy.State + "; the books "
                                 + ((entered - started) * 1000f).ToString("0.0", CultureInfo.InvariantCulture) + " ms, demand and the first hour "
                                 + ((done - entered) * 1000f).ToString("0.0", CultureInfo.InvariantCulture) + " ms: PASS");
                }
            }
            catch (Exception e)
            {
                Fault("the day", e);
            }
        }

        // A month's hosting when it falls due, if the cash covers it; if not, the site is down and asked again each day.
        private void Host(WebPlace place, int day)
        {
            WebShop shop = place.Shop;
            if (!OnlineRules.HostingDue(shop, day))
            {
                return;
            }
            int cost = OnlineRules.HostingCost(shop.Quality, _index);
            if (OnlineGame.Take(cost * 100, OnlineGame.HostingKey, place, null, true, false))
            {
                bool was = shop.Offline;
                OnlineRules.HostingPaid(shop, day, cost * 100, MonthDays);
                if (was)
                {
                    Tell(NotificationType.Success, "reallife_online_note_back", new Dictionary<string, string> { { "name", shop.Name } }, TabShops, "shop:" + shop.Place);
                }
                if (Verbose)
                {
                    Logger?.Info("Online check (hosting, " + shop.Name + "): " + cost + " taken for a month at today's prices (" + _index + "), deductible; due again on day "
                                 + shop.HostDue + ": PASS");
                }
                return;
            }
            if (!shop.Offline)
            {
                OnlineRules.HostingMissed(shop);
                Tell(NotificationType.Warning, "reallife_online_note_unpaid", new Dictionary<string, string> { { "name", shop.Name }, { "amount", OnlineText.Money(cost * 100) } },
                    TabShops, "shop:" + shop.Place);
                if (Verbose)
                {
                    Logger?.Info("Online check (hosting, " + shop.Name + "): " + cost + " could not be taken: the cash does not cover it. The site takes no orders until it is paid: PASS");
                }
            }
        }

        private void PlanAll(int day)
        {
            int cycle = (int)CityEconomy.State, season = OnlineBridges.Season != null ? OnlineBridges.Season(day) : 1;
            for (int i = 0; i < _places.Count; i++)
            {
                Plan(_places[i], day, cycle, season, false);
            }
        }

        // Reads the site's prices and the game's demand and works out its orders a day; logged when the player changed something.
        private void Plan(WebPlace place, int day, int cycle, int season, bool say)
        {
            OnlineGame.ReadSite(place, _state);
            OnlineGame.GuessRoads(place, _centres, _hasCentre);
            place.Site.Mbps = Speed(place.Reg, -1);
            int campaign = OnlineBridges.Campaign != null && place.Reg != null ? OnlineBridges.Campaign(place.Reg) : 0;
            OnlineRules.Plan(place.Shop, place.Site, day, campaign, cycle, season, _state.Shops.Count);
            if (Verbose && say)
            {
                Site s = place.Site;
                Logger?.Info("Online check (demand, " + place.Shop.Name + "): " + (s.PerDay / 1000f).ToString("0.0", CultureInfo.InvariantCulture) + " order(s) a day of at most "
                             + OnlineCatalogue.OrdersMost[OnlineCatalogue.Quality(place.Shop.Quality)] + ": quality " + place.Shop.Quality + (place.Shop.InHouse ? " built in-house" : string.Empty)
                             + ", " + s.Lines + " product(s), the game's demand " + s.Need + " of 100, prices " + s.Price + ", promise " + place.Shop.Promise + ", stars " + s.Stars
                             + ", advertising " + s.Ads + "%, a campaign at the business " + s.Campaign + "%, the economy " + s.Cycle + ", season " + s.Season + ", " + s.Crowd
                             + " site(s) sharing the city (factors in thousandths); the connection " + s.Mbps + " Mbps, a bound of " + OnlineRules.Most(place.Shop.Quality, s.Mbps)
                             + " a day" + (place.Shop.Offline ? "; offline, hosting unpaid" : string.Empty) + ": PASS");
            }
        }

        /// <summary>One site's day worked out again now: the player has just changed something of it.</summary>
        internal void Replan(WebPlace place)
        {
            Plan(place, Save.Day, (int)CityEconomy.State, OnlineBridges.Season != null ? OnlineBridges.Season(Save.Day) : 1, true);
        }
    }
}
