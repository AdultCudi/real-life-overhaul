using System;
using System.Collections.Generic;
using Buildings.Office.Headquarters;
using Entities;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    public sealed partial class LogisticsDirector
    {
        private const int EventsASecond = 8;       // trip events (a departure, a stop, a return) handled in one second at most
        private const int RoadsASecond = 3;        // slices of a road's search in one second
        private const int StaleDays = 2;           // a trip due this long ago is dropped at a load (the option was off, or the save played without it)
        private const int CatchUpRuns = 3;         // departures of one route begun in one second at most, when time ran ahead

        private readonly List<int> _hours = new List<int>(4);
        private readonly List<TripSlot> _slots = new List<TripSlot>(8);
        private readonly List<LogisticsManagerPlanDestination> _stops = new List<LogisticsManagerPlanDestination>(16);
        private readonly List<string> _items = new List<string>(8);
        private readonly List<int> _wanted = new List<int>(8);
        private readonly List<int> _moved = new List<int>(8);
        private readonly Dictionary<string, int> _roads = new Dictionary<string, int>(StringComparer.Ordinal);   // "from>to": metres of road
        private readonly List<string> _roadQueue = new List<string>(16);
        private readonly List<Vector3> _roadEnds = new List<Vector3>(32);
        private int[] _shares = new int[8], _slotStops = new int[8];

        private void TripsStart(int now)
        {
            for (int i = _trips.Trips.Count - 1; i >= 0; i--)
            {
                Trip t = _trips.Trips[i];
                int due = t.Out ? t.Back : t.Depart;
                if (due < now - StaleDays * TripRules.DayMinutes)
                {
                    _trips.Trips.RemoveAt(i);   // its goods never left the warehouse: nothing is lost
                }
            }
            foreach (RouteTimes r in _trips.Routes.Values)
            {
                if (r.Last < now - TripRules.DayMinutes)
                {
                    r.Last = now;   // departures missed while the trips were not running are not made up
                }
            }
            TripRules.Trim(_trips.Log, LogisticsGame.Day);
        }

        private void TripsStop()
        {
            _roads.Clear();
            _roadQueue.Clear();
            _roadEnds.Clear();
        }

        private void TripsSecond(int now)
        {
            Schedule(now);
            int events = 0;
            while (events < EventsASecond && Step(now))
            {
                events++;
            }
            MeasureRoads();
        }

        // ------------------------------------------------------------------------------------------------- departures

        // Each route the trips run: its departures due since the last begun, each a trip for every slot of its warehouse that drives.
        private void Schedule(int now)
        {
            List<LogisticsManagerPlan> plans = LogisticsGame.Plans;
            for (int i = 0; plans != null && i < plans.Count; i++)
            {
                LogisticsManagerPlan plan = plans[i];
                Warehouse from = plan != null && !string.IsNullOrEmpty(plan.id) ? LogisticsGame.From(plan) : null;
                if (from == null)
                {
                    continue;
                }
                RouteTimes r = Times(plan);
                if (r.Last == int.MinValue)
                {
                    r.Last = now;   // a route first seen: its next departure is its first
                    continue;
                }
                for (int k = 0; k < CatchUpRuns; k++)
                {
                    int next = TripRules.Next(r.Last, r.First, r.Runs, r.Gap, _hours);
                    if (next > now)
                    {
                        break;
                    }
                    r.Last = next;
                    Depart(plan, from, next);
                }
            }
        }

        /// <summary>A route's departures, the defaults while the player set none (twice a day when it carries goods that spoil).</summary>
        public RouteTimes Times(LogisticsManagerPlan plan)
        {
            RouteTimes r = _trips.Route(plan.id);
            if (!r.Set)
            {
                TripRules.Default(ShortLived(plan), out r.First, out r.Runs, out r.Gap);
            }
            return r;
        }

        // A route that carries goods that keep a week or less: its manager sends them more often in smaller loads.
        private bool ShortLived(LogisticsManagerPlan plan)
        {
            if (!_spoilOn || plan.destinations == null)
            {
                return false;
            }
            for (int i = 0; i < plan.destinations.Count; i++)
            {
                LogisticsManagerPlanDestination d = plan.destinations[i];
                for (int k = 0; d != null && d.stockTargets != null && k < d.stockTargets.Count; k++)
                {
                    string item = d.stockTargets[k] != null ? d.stockTargets[k].itemName : null;
                    int life = item != null ? SpoilRules.Life(item, LogisticsGame.Weighed(item), false) : 0;
                    if (life > 0 && life <= SpoilRules.ShortLife)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        // A departure of the route: one trip waiting for each slot that drives, leaving now or as soon as its vehicle is back.
        private int Depart(LogisticsManagerPlan plan, Warehouse from, int at)
        {
            string fromKey = LogisticsGame.Key(from.Address);
            int count = LogisticsGame.Slots(from, _slots), made = 0;
            for (int s = 0; s < count; s++)
            {
                if (_slots[s].Stops <= 0)
                {
                    continue;
                }
                Trip busy = _trips.Busy(fromKey, s);
                if (busy != null && !busy.Out)
                {
                    continue;   // a trip waits already for this vehicle: this departure leaves it as it is
                }
                _trips.Trips.Add(new Trip { Plan = plan.id, From = fromKey, Slot = s, Depart = busy != null ? Math.Max(at, busy.Back) : at });
                made++;
                _changes++;
            }
            return made;
        }

        /// <summary>Send now: every free vehicle of the route leaves at once with what its stops need. Returns the trips begun.</summary>
        public int SendNow(LogisticsManagerPlan plan)
        {
            Warehouse from = TripsOn ? LogisticsGame.From(plan) : null;
            if (from == null || !LogisticsGame.Managed(plan))
            {
                return 0;
            }
            int made = Depart(plan, from, LogisticsGame.Now);
            if (made > 0)
            {
                Logger?.Info("Delivery trips: " + made + " trip(s) sent now on a route from " + LogisticsGame.NameOf(from) + ".");
            }
            return made;
        }

        // ------------------------------------------------------------------------------------------------- the trips' events

        // The earliest event due: a trip leaving, arriving at a stop or back at its warehouse. False when none is due.
        private bool Step(int now)
        {
            Trip next = null;
            int when = int.MaxValue;
            for (int i = 0; i < _trips.Trips.Count; i++)
            {
                int due = Due(_trips.Trips[i]);
                if (due <= now && due < when)
                {
                    when = due;
                    next = _trips.Trips[i];
                }
            }
            if (next == null)
            {
                return false;
            }
            _changes++;
            if (!next.Out)
            {
                Leave(next);
                return true;
            }
            for (int k = 0; k < next.Stops.Count; k++)
            {
                if (!next.Stops[k].Done)
                {
                    if (LogisticsGame.GameBusy)
                    {
                        return false;   // the game's own deliveries are running: the stop waits a second
                    }
                    Arrive(next, next.Stops[k]);
                    return true;
                }
            }
            Home(next);
            return true;
        }

        private static int Due(Trip t)
        {
            if (!t.Out)
            {
                return t.Depart;
            }
            for (int k = 0; k < t.Stops.Count; k++)
            {
                if (!t.Stops[k].Done)
                {
                    return t.Stops[k].Arrive;
                }
            }
            return t.Back;
        }

        // A trip leaves: its slot's share of the route's stops, what each needs of the warehouse's goods not already on the road,
        // as much as fits in the vehicle in whole boxes, the road round them, its timeline, its fuel (paid) and its driver's hours.
        private void Leave(Trip t)
        {
            LogisticsManagerPlan plan = LogisticsGame.Plan(t.Plan);
            Warehouse from = plan != null ? LogisticsGame.From(plan) : null;
            int count = from != null ? LogisticsGame.Slots(from, _slots) : 0;
            if (from == null || t.Slot >= count || !_slots[t.Slot].Ready || !LogisticsGame.Managed(plan))
            {
                _trips.Trips.Remove(t);   // the route, its warehouse, its vehicle or its people are gone or away: no trip
                return;
            }
            TripSlot slot = _slots[t.Slot];
            int first = Share(plan, count, t.Slot, out int mine);
            LogisticsGame.Stops(plan, _stops);
            int room = slot.Room, left = room;
            for (int k = first; k < first + mine && k < _stops.Count; k++)
            {
                LogisticsManagerPlanDestination d = _stops[k];
                LogisticsGame.Needs(d, _items, _wanted);
                var stop = new TripStop { Place = LogisticsGame.Key(d.deliveryTargetAddress) };
                for (int g = 0; g < _items.Count && left > 0; g++)
                {
                    int free = LogisticsGame.InStock(from, _items[g]) - OnRoad(t.From, _items[g]) - Loaded(t, _items[g]);
                    int pieces = TripRules.Fit(Math.Min(_wanted[g], free), LogisticsGame.BoxSize(_items[g]), ref left);
                    if (pieces > 0)
                    {
                        stop.Items.Add(_items[g]);
                        stop.Pieces.Add(pieces);
                    }
                }
                if (stop.Items.Count > 0)
                {
                    t.Stops.Add(stop);
                }
            }
            if (t.Stops.Count == 0)
            {
                _trips.Trips.Remove(t);   // nothing any stop needs is in the warehouse: the vehicle stays parked
                return;
            }
            var legs = new int[t.Stops.Count + 1];
            var arrive = new int[t.Stops.Count];
            string at = t.From;
            t.Metres = 0;
            for (int k = 0; k <= t.Stops.Count; k++)
            {
                string to = k < t.Stops.Count ? t.Stops[k].Place : t.From;
                int metres = Metres(at, to);
                t.Metres += metres;
                legs[k] = TripRules.Drive(metres, slot.Kind);
                at = to;
            }
            t.Back = TripRules.Timeline(t.Depart, legs, t.Stops.Count, arrive);
            for (int k = 0; k < t.Stops.Count; k++)
            {
                t.Stops[k].Arrive = arrive[k];
            }
            t.Out = true;
            t.Vehicle = slot.Vehicle;
            t.Driver = slot.Driver;
            t.Kind = slot.Kind;
            t.Room = room;
            t.Boxes = room - left;
            t.FuelCents = TripRules.Fuel(t.Metres, slot.Kind, LogisticsGame.PumpCents());
            t.WageCents = TripRules.Wage(slot.WageCents, t.Back - t.Depart);
            LogisticsGame.Fuel(from, t.FuelCents);
            if (Verbose)
            {
                Logger?.Info("Trips check (a departure from " + LogisticsGame.NameOf(from) + ", slot " + (t.Slot + 1) + "): " + t.Stops.Count + " stop(s), " + t.Pieces()
                             + " piece(s) in " + t.Boxes + " of " + t.Room + " box(es), " + t.Metres + " m, back after " + (t.Back - t.Depart) + " min, fuel "
                             + t.FuelCents + " cents, wages " + t.WageCents + " cents: " + (t.Boxes <= t.Room && t.Back > t.Depart ? "PASS" : "FAIL"));
            }
        }

        // The first of the route's stops a slot serves and how many: the stops shared over the slots that drive, in their order.
        private int Share(LogisticsManagerPlan plan, int count, int slot, out int mine)
        {
            if (_shares.Length < count)
            {
                _shares = new int[count];
                _slotStops = new int[count];
            }
            for (int s = 0; s < count; s++)
            {
                _slotStops[s] = _slots[s].Stops;
            }
            TripRules.Shares(_slotStops, count, LogisticsGame.MostStops(plan), _shares);
            int first = 0;
            for (int s = 0; s < slot; s++)
            {
                first += _shares[s];
            }
            mine = _shares[slot];
            return first;
        }

        // Pieces of a product on the road from a warehouse, not yet delivered.
        private int OnRoad(string from, string item)
        {
            int n = 0;
            for (int i = 0; i < _trips.Trips.Count; i++)
            {
                Trip t = _trips.Trips[i];
                if (t.Out && t.From == from)
                {
                    n += Loaded(t, item);
                }
            }
            return n;
        }

        private static int Loaded(Trip t, string item)
        {
            int n = 0;
            for (int k = 0; k < t.Stops.Count; k++)
            {
                TripStop st = t.Stops[k];
                int g = st.Done ? -1 : st.Items.IndexOf(item);
                n += g >= 0 ? st.Pieces[g] : 0;
            }
            return n;
        }

        // A stop reached: its goods delivered by the game's own call; what moved is what left the warehouse (and its age goes with it).
        private void Arrive(Trip t, TripStop stop)
        {
            stop.Done = true;
            LogisticsManagerPlan plan = LogisticsGame.Plan(t.Plan);
            Warehouse from = plan != null ? LogisticsGame.From(plan) : null;
            if (from == null || !LogisticsGame.Deliver(plan, from, stop, _moved))
            {
                return;   // the stop or the route is gone: the goods stay in the warehouse
            }
            int moved = 0;
            for (int g = 0; g < stop.Items.Count && g < _moved.Count; g++)
            {
                moved += _moved[g];
                if (_moved[g] > 0)
                {
                    Moved(t.From, stop.Place, stop.Items[g], _moved[g]);
                }
                stop.Pieces[g] = _moved[g];
            }
            if (Verbose)
            {
                Logger?.Info("Trips check (a stop of a route from " + LogisticsGame.NameOf(from) + "): " + moved + " piece(s) delivered by the game's own call: PASS");
            }
        }

        // Back at the warehouse: the run goes into the log.
        private void Home(Trip t)
        {
            _trips.Trips.Remove(t);
            int day = Floor(t.Depart, TripRules.DayMinutes);
            _trips.Log.Add(new TripLog
            {
                Day = day, Minute = t.Depart - day * TripRules.DayMinutes, Plan = t.Plan, Slot = t.Slot, Stops = t.Stops.Count, Pieces = t.Pieces(), Boxes = t.Boxes,
                Room = t.Room, Metres = t.Metres, Minutes = t.Back - t.Depart, FuelCents = t.FuelCents, WageCents = t.WageCents
            });
            TripRules.Trim(_trips.Log, LogisticsGame.Day);
        }

        private static int Floor(int a, int b) => a >= 0 ? a / b : -((-a + b - 1) / b);

        // ------------------------------------------------------------------------------------------------- roads

        // Metres between two places: the road measured on the GPS's graph once known, the straight line's guess until then.
        private int Metres(string from, string to)
        {
            string pair = string.CompareOrdinal(from, to) <= 0 ? from + ">" + to : to + ">" + from;
            if (_roads.TryGetValue(pair, out int known) && known > 0)
            {
                return known;
            }
            BuildingRegistration a = LogisticsGame.Reg(from), b = LogisticsGame.Reg(to);
            if (a == null || b == null || !LogisticsGame.At(from, a.Address, out Vector3 pa) || !LogisticsGame.At(to, b.Address, out Vector3 pb))
            {
                return 1000;   // a place the city does not show: a kilometre
            }
            int guess = TripRules.Road(LogisticsGame.Straight(pa, pb));
            if (!_roads.ContainsKey(pair) && LogisticsBridges.Road != null)
            {
                _roads[pair] = 0;
                _roadQueue.Add(pair);
                _roadEnds.Add(pa);
                _roadEnds.Add(pb);
            }
            return Math.Max(1, guess);
        }

        // A few slices of the first road waiting to be measured.
        private void MeasureRoads()
        {
            for (int n = 0; n < RoadsASecond && _roadQueue.Count > 0 && LogisticsBridges.Road != null; n++)
            {
                int metres = LogisticsBridges.Road(_roadEnds[0], _roadEnds[1]);
                if (metres < 0)
                {
                    continue;
                }
                _roads[_roadQueue[0]] = metres > 0 ? metres : TripRules.Road(LogisticsGame.Straight(_roadEnds[0], _roadEnds[1]));
                _roadQueue.RemoveAt(0);
                _roadEnds.RemoveRange(0, 2);
            }
        }

        // ------------------------------------------------------------------------------------------------- what the pages read

        /// <summary>Sets a route's departures from its page; kept in the save.</summary>
        public void SetTimes(LogisticsManagerPlan plan, int first, int runs, int gap)
        {
            if (!TripsOn || plan == null || string.IsNullOrEmpty(plan.id))
            {
                return;
            }
            RouteTimes r = Times(plan);
            r.First = Mathf.Clamp(first, 0, 23);
            r.Runs = Mathf.Clamp(runs, 1, TripRules.RunChoices[TripRules.RunChoices.Length - 1]);
            r.Gap = Mathf.Clamp(gap, 1, 23);
            r.Set = true;
            _changes++;
            Logger?.Info("Delivery trips: a route's departures set to " + r.Runs + " a day from " + r.First + ":00, every " + r.Gap + " hour(s).");
        }

        /// <summary>The trip of a warehouse's slot on the road or waiting to leave; null when its vehicle is parked.</summary>
        public Trip OnRoad(BuildingRegistration warehouse, int slot) => TripsOn && warehouse != null ? _trips.Busy(LogisticsGame.Key(warehouse.Address), slot) : null;

        /// <summary>The trips of a route on the road or waiting, into the list given.</summary>
        public void RouteTrips(string plan, List<Trip> into)
        {
            into.Clear();
            for (int i = 0; TripsOn && i < _trips.Trips.Count; i++)
            {
                if (_trips.Trips[i].Plan == plan)
                {
                    into.Add(_trips.Trips[i]);
                }
            }
        }

        /// <summary>A route's runs over the last seven days, newest first, into the list given.</summary>
        public void RouteRuns(string plan, List<TripLog> into)
        {
            into.Clear();
            for (int i = TripsOn ? _trips.Log.Count - 1 : -1; i >= 0; i--)
            {
                if (_trips.Log[i].Plan == plan)
                {
                    into.Add(_trips.Log[i]);
                }
            }
        }

        /// <summary>A route's runs, pieces, fuel and wages a day over the last seven days.</summary>
        public void RouteWeek(string plan, out int runs, out int pieces, out int fuelCents, out int wageCents)
        {
            runs = pieces = fuelCents = wageCents = 0;
            if (TripsOn)
            {
                TripRules.Week(_trips.Log, plan, LogisticsGame.Day, out runs, out pieces, out fuelCents, out wageCents);
            }
        }

        /// <summary>The next departure of a route, an absolute game minute; -1 when trips are off.</summary>
        public int NextRun(LogisticsManagerPlan plan)
        {
            if (!TripsOn || plan == null || string.IsNullOrEmpty(plan.id))
            {
                return -1;
            }
            RouteTimes r = Times(plan);
            return TripRules.Next(Math.Max(r.Last == int.MinValue ? LogisticsGame.Now : r.Last, LogisticsGame.Now), r.First, r.Runs, r.Gap, _hours);
        }
    }
}
