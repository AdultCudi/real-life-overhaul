using System.Collections.Generic;
using UI.Notification;

namespace RealLifeOverhaul.Economy.Dealership
{
    /// <summary>
    /// The showroom's business beyond buying and selling: the advertising events the player books, its reputation with
    /// buyers and the missions that build it, the workshop that brings a car's condition back, and the cars won at the
    /// trade auction on their way. The rules are DealerRules.Business; this keeps the money, the cars and the notices.
    /// Nothing runs per frame: the events end on the day, the workshop and the deliveries are seen to on the hour, and
    /// the rest on the player's word or a sale.
    /// </summary>
    public sealed partial class DealerDirector
    {
        private readonly List<DealerDelivery> _due = new List<DealerDelivery>(4);
        private readonly List<int> _done = new List<int>(4);

        internal static string GrowthRow(string kind, int n) => kind + n;
        internal static string RepRow(DealerYard y) => "rep:" + y.Place;
        internal static string MissionRow(DealerYard y, int mission) => "mission:" + mission + ":" + y.Place;
        internal static string EventRow(int kind) => "event:" + kind;
        internal static string DeliveryRow(DealerDelivery q) => "way:" + q.Id;

        // ---------------------------------------------------------------- reputation and missions

        /// <summary>A change to a showroom's reputation; a step reached is said, with its perk.</summary>
        internal void Repute(DealerYard y, int change)
        {
            if (y == null || change == 0)
            {
                return;
            }
            int before = DealerRules.Repute(y, change);
            Stepped(y, before);
        }

        private void Stepped(DealerYard y, int before)
        {
            int step = DealerRules.Step(y.Rep);
            if (step > before)
            {
                Logger?.Info("Dealership: '" + y.Name + "' reaches step " + step + " of its reputation (" + y.Rep + ").");
                Tell(NotificationType.Info, "reallife_dealer_note_step", new Dictionary<string, string>(3)
                    { { "name", y.Name }, { "step", DealerText.Step(step) }, { "perk", DealerText.Perk(step) } }, TabGrowth, RepRow(y));
            }
        }

        /// <summary>The missions a showroom has reached: each is done once, gives its reputation and is said.</summary>
        internal void Advance(DealerYard y)
        {
            if (y == null)
            {
                return;
            }
            int before = DealerRules.Step(y.Rep);
            int given = DealerRules.Missions(y, _done);
            for (int i = 0; i < _done.Count; i++)
            {
                int m = _done[i];
                Logger?.Info("Dealership: '" + y.Name + "' has done mission " + m + "; reputation +" + DealerCatalogue.MissionReward[m] + ", now " + y.Rep + ".");
                Tell(NotificationType.Info, "reallife_dealer_note_mission", new Dictionary<string, string>(3)
                    { { "name", y.Name }, { "mission", DealerText.Mission(m) }, { "points", DealerCatalogue.MissionReward[m].ToString() } }, TabGrowth, MissionRow(y, m));
            }
            if (given > 0)
            {
                Stepped(y, before);
            }
        }

        // ---------------------------------------------------------------- advertising events

        /// <summary>What an event of this kind costs now.</summary>
        internal static int EventCost(int kind)
        {
            return kind >= 0 && kind < DealerCatalogue.EventKinds ? Prices.At(DealerCatalogue.EventCost[kind]) : 0;
        }

        /// <summary>The player books an event at a showroom: paid now at the showroom, it runs from today. A featured event names its kind of car.</summary>
        public bool BookEvent(string place, int kind, int feature)
        {
            DealerYard y = _state != null ? _state.Yard(place) : null;
            BuildingRegistration reg = y != null ? RegOf(y) : null;
            if (y == null || reg == null)
            {
                return Refuse(_running ? DealerRefusal.Gone : DealerRefusal.Off);
            }
            EventRefusal no = DealerRules.CanBook(y, kind, Save.Day);
            if (no != EventRefusal.None)
            {
                return Refuse(no == EventRefusal.Running ? DealerRefusal.EventOn : no == EventRefusal.Rested ? DealerRefusal.Rested : DealerRefusal.Busy);
            }
            int cost = EventCost(kind);
            if (!DealerGame.Take(cost, DealerLines.EventKey, DealerGame.Named(reg), reg, false))
            {
                return Refuse(DealerRefusal.Cash);
            }
            DealerRules.Book(y, kind, feature, Save.Day);
            Book(y, 0, 0, 0, 0);
            y.Today.Spent += cost;
            Logger?.Info("Dealership: event " + kind + (kind == DealerCatalogue.FeaturedKind ? " (kind of car " + feature + ")" : string.Empty) + " booked at '" + y.Name + "' for " + cost
                         + ", days " + y.EventFrom + " to " + y.EventUntil + ".");
            Tell(NotificationType.Info, "reallife_dealer_note_event", new Dictionary<string, string>(3)
                { { "event", DealerText.Event(kind, y.Feature) }, { "name", y.Name }, { "day", FinanceText.Day(y.EventUntil) } }, TabGrowth, EventRow(kind));
            Advance(y);
            Changed();
            return true;
        }

        // The events whose last day is over: each is said with what its days came to, and its kind rests.
        private void EndEvents(int day)
        {
            for (int i = 0; i < _state.Yards.Count; i++)
            {
                DealerYard y = _state.Yards[i];
                if (!DealerRules.EventOver(y, day))
                {
                    continue;
                }
                DealerReport.Sum(_state, y.Place, y.EventFrom, y.EventUntil, day, _sum, _scratch);
                Tell(NotificationType.Info, "reallife_dealer_note_event_over", new Dictionary<string, string>(4)
                    { { "event", DealerText.Event(y.Event, y.Feature) }, { "name", y.Name }, { "visitors", _sum.Visitors.ToString() }, { "sold", _sum.Sold.ToString() } }, TabReports, null);
                Logger?.Info("Dealership: the event at '" + y.Name + "' is over: " + _sum.Visitors + " visitor(s), " + _sum.Offers + " offer(s), " + _sum.Sold + " sold over days " + y.EventFrom + " to " + y.EventUntil + ".");
                DealerRules.EndEvent(y);
            }
        }

        private readonly DealerBook _sum = new DealerBook(), _scratch = new DealerBook();

        // ---------------------------------------------------------------- extras

        public void OfferExtras(string place, bool on)
        {
            DealerYard y = _state != null ? _state.Yard(place) : null;
            if (y != null)
            {
                y.Extras = on;
                Changed();
            }
        }

        // ---------------------------------------------------------------- the workshop

        /// <summary>What the workshop would charge for a car now.</summary>
        internal int WorkshopCost(StockCar c)
        {
            return DealerRules.WorkshopCost(DealerRules.Worth(DealerGame.ListPrice(c.Type), Prices.Level, 0, 1000), c.Condition, c.Faults, Prices.At(DealerCatalogue.WorkshopBase));
        }

        /// <summary>
        /// A car into the showroom's workshop: paid now, off show until the work is done (any offer on it is withdrawn), and
        /// back with every fault put right and most of its missing condition. Refused while it is out on a test drive, in the
        /// workshop already, with nothing to do, or with every bay taken.
        /// </summary>
        public bool Workshop(int car)
        {
            DealerYard yard = null;
            StockCar c = _state != null ? _state.Car(car, out yard) : null;
            if (c == null)
            {
                return Refuse(_running ? DealerRefusal.Gone : DealerRefusal.Off);
            }
            int now = Now;
            if (c.ShopUntil > now)
            {
                return Refuse(DealerRefusal.InWorkshop);
            }
            if (c.OutUntil > now)
            {
                return Refuse(DealerRefusal.Busy);
            }
            if (!DealerRules.WorkshopWorth(c))
            {
                return Refuse(DealerRefusal.Nothing);
            }
            if (yard.InWorkshop(now) >= DealerCatalogue.WorkshopBays)
            {
                return Refuse(DealerRefusal.Bays);
            }
            BuildingRegistration reg = RegOf(yard);
            int cost = WorkshopCost(c);
            if (reg == null || !DealerGame.Take(cost, DealerLines.WorkshopKey, DealerGame.Named(reg), reg, false))
            {
                return Refuse(reg == null ? DealerRefusal.NoLot : DealerRefusal.Cash);
            }
            c.ShopUntil = now + DealerRules.WorkshopHours(c.Condition + DealerRules.FaultCondition(c.Faults));
            c.Cost += cost;
            RemoveDeals(yard, c.Id);
            Book(yard, 0, cost, 0, 0);
            yard.Today.Spent += cost;
            Logger?.Info("Dealership: a " + c.Type + " of '" + yard.Name + "' into the workshop for " + cost + " until hour " + c.ShopUntil + " (condition " + c.Condition + ", faults " + c.Faults + ").");
            Changed();
            return true;
        }

        // The cars whose work is done this hour come back on show: condition up, every fault gone, the asking price after its worth.
        private void WorkshopHour(DealerYard y, int now)
        {
            for (int i = 0; i < y.Cars.Count; i++)
            {
                StockCar c = y.Cars[i];
                if (c.ShopUntil < 0 || c.ShopUntil > now)
                {
                    continue;
                }
                Finish(y, c);
            }
        }

        private void Finish(DealerYard y, StockCar c)
        {
            int level = Prices.Level, before = c.Condition;
            bool followed = c.Asking == DealerRules.Asking(Worth(c, level));
            c.Condition = DealerRules.Worked(c.Condition, c.Faults);
            c.Faults = 0;
            c.Known = 0;
            c.ShopUntil = -1;
            if (followed)
            {
                c.Asking = DealerRules.Asking(Worth(c, level));
            }
            DealerGame.Wear(c);
            DealerRules.Count(y, DealerCatalogue.TallyWorkshop, 1);
            Logger?.Info("Dealership: the workshop of '" + y.Name + "' has finished a " + c.Type + ": condition " + before + " to " + c.Condition + ", asked at " + c.Asking + ".");
            Tell(NotificationType.Info, "reallife_dealer_note_workshop", new Dictionary<string, string>(3)
                { { "car", DealerGame.CarName(c.Type) }, { "name", y.Name }, { "condition", DealerText.Condition(c.Condition) } }, TabStock, CarRow(c));
            Advance(y);
        }

        // ---------------------------------------------------------------- deliveries

        /// <summary>A lot won and paid for, booked to arrive at a showroom with room for it a few days on.</summary>
        private DealerDelivery Ship(DealerListing l, DealerYard y, int cost, int now)
        {
            var q = new DealerDelivery
            {
                Id = _state.Next(), Place = y.Place, Type = l.Type, Colour = l.Colour, Km = l.Km, Condition = l.Condition, Faults = l.Faults, Known = l.Inspected ? l.Faults : 0,
                Cost = cost, Due = DealerRules.DeliveryDue(_state.Seed, l.Id, now)
            };
            _state.Deliveries.Add(q);
            return q;
        }

        // The cars due by this hour arrive: on the lot they were held for, or another with room. One with nowhere to go
        // waits and is said once; one with no showroom of the player's left at all is sold to the trade.
        private void Arrivals(int now)
        {
            DealerRules.Due(_state, now, _due);
            for (int i = 0; i < _due.Count; i++)
            {
                DealerDelivery q = _due[i];
                DealerYard y = _state.Yard(q.Place);
                if (y == null || RegOf(y) == null || y.FreeSpot() < 0)
                {
                    y = Room(null, out DealerRefusal _);
                }
                StockCar car = y != null ? Arrive(y, q.Type, q.Colour, q.Km, q.Condition, q.Faults, q.Known, q.Cost, CarSource.Auction, true) : null;
                if (car != null)
                {
                    _state.Deliveries.Remove(q);
                    DealerRules.Count(y, DealerCatalogue.TallyDelivered, 1);
                    Logger?.Info("Dealership: the " + q.Type + " won at auction has arrived at '" + y.Name + "'.");
                    Tell(NotificationType.Info, "reallife_dealer_note_arrived", new Dictionary<string, string>(2) { { "car", DealerGame.CarName(q.Type) }, { "name", y.Name } }, TabStock, CarRow(car));
                    Advance(y);
                    continue;
                }
                if (!AnyShowroom())
                {
                    SellOnTheWay(q);
                    continue;
                }
                if (!q.Told)
                {
                    q.Told = true;
                    DealerYard to = _state.Yard(q.Place);
                    Tell(NotificationType.Warning, "reallife_dealer_note_no_place", new Dictionary<string, string>(2)
                        { { "car", DealerGame.CarName(q.Type) }, { "name", to != null ? to.Name : string.Empty } }, TabStock, DeliveryRow(q));
                }
            }
        }

        private bool AnyShowroom()
        {
            for (int i = 0; i < _state.Yards.Count; i++)
            {
                if (_state.Yards[i].Laid && RegOf(_state.Yards[i]) != null)
                {
                    return true;
                }
            }
            return false;
        }

        // A car on its way with no showroom to go to: another dealer takes it at the trade's price, paid to the player.
        private void SellOnTheWay(DealerDelivery q)
        {
            int worth = DealerRules.Worth(DealerGame.ListPrice(q.Type), Prices.Level, q.Km, q.Condition);
            int price = DealerRules.TradePrice(worth);
            DealerYard y = _state.Yard(q.Place);
            BuildingRegistration reg = y != null ? RegOf(y) : null;
            DealerGame.Give(price, DealerLines.SoldType, DealerGame.Sold(q.Type, reg), reg);
            _state.Deliveries.Remove(q);
            Logger?.Info("Dealership: the " + q.Type + " on its way had no showroom to go to; sold to the trade for " + price + ".");
            Tell(NotificationType.Info, "reallife_dealer_note_way_sold", new Dictionary<string, string>(2) { { "car", DealerGame.CarName(q.Type) }, { "price", DealerText.Money(price) } }, TabStock, null);
        }

        // ---------------------------------------------------------------- for the save tools

        // Before the save is prepared for removing the mod: the workshop's cars are finished, and the cars on their way
        // are put on a lot with room or, failing that, sold to the trade, so that nothing paid for is left in the mod's line.
        private void Settle()
        {
            if (!_running || _state == null || Save == null)
            {
                return;
            }
            ReadYards();
            int finished = 0, delivered = _state.Deliveries.Count;
            for (int y = 0; y < _state.Yards.Count; y++)
            {
                List<StockCar> cars = _state.Yards[y].Cars;
                for (int c = 0; c < cars.Count; c++)
                {
                    if (cars[c].ShopUntil >= 0)
                    {
                        Finish(_state.Yards[y], cars[c]);
                        finished++;
                    }
                }
            }
            for (int i = 0; i < _state.Deliveries.Count; i++)
            {
                _state.Deliveries[i].Due = Now;
                _state.Deliveries[i].Told = true;
            }
            Arrivals(Now);
            while (_state.Deliveries.Count > 0)
            {
                SellOnTheWay(_state.Deliveries[0]);
            }
            Logger?.Info("Dealership: settled for the save tools: " + finished + " car(s) finished in the workshop, " + delivered + " car(s) on their way delivered or sold to the trade.");
        }
    }
}
