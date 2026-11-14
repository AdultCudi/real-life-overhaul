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
        private readonly List<int> _credited = new List<int>(8);
        private readonly List<int> _wasHeld = new List<int>(8);
        private readonly List<int> _entryStop = new List<int>(32), _entryPool = new List<int>(32), _entryWant = new List<int>(32), _entrySize = new List<int>(32);
        private readonly List<string> _entryItem = new List<string>(32), _poolItem = new List<string>(16);
        private readonly List<int> _poolLeft = new List<int>(16);

        private void TripsStart(int now)
        {
            for (int i = _trips.Trips.Count - 1; i >= 0; i--)
            {
                Trip t = _trips.Trips[i];
                int due = t.Out ? t.Back : t.Depart;
                if (due < now - StaleDays * TripRules.DayMinutes)
                {
                    Abandoned(t);
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
            TallyWrite(0, true);
            _tally.Clear();
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
            TallyWrite(now / TripRules.DayMinutes, false);
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
        // as much as fits in the vehicle in whole boxes (the boxes handed round the stops, so a full vehicle never serves only the first
        // of them), the road round them, its timeline, its fuel (paid) and its driver's hours.
        private void Leave(Trip t)
        {
            LogisticsManagerPlan plan = LogisticsGame.Plan(t.Plan);
            Warehouse from = plan != null ? LogisticsGame.From(plan) : null;
            int count = from != null ? LogisticsGame.Slots(from, _slots) : 0;
            if (from == null)
            {
                _trips.Trips.Remove(t);   // the route or its warehouse is gone or not the player's own to run: no trip
                return;
            }
            int mine = 0, first = t.Slot < count ? Share(plan, count, t.Slot, out mine) : 0;
            LogisticsGame.Stops(plan, _stops);
            int stops = t.Slot < count ? Math.Max(0, Math.Min(mine, _stops.Count - first)) : 0;
            Wants(t, from, first, stops);
            int wantedAll = 0, loadedAll = 0;
            for (int e = 0; e < _entryWant.Count; e++)
            {
                wantedAll += _entryWant[e];
            }
            bool managed = LogisticsGame.Managed(plan), ready = t.Slot < count && _slots[t.Slot].Ready;
            if (!managed || !ready)
            {
                _trips.Trips.Remove(t);   // the manager or the vehicle's driver is away: no trip, and the player is told if anything was needed
                if (wantedAll > 0)
                {
                    Behind(plan, from, null, wantedAll, !managed ? TripWhy.NoManager : TripWhy.NoDriver);
                }
                return;
            }
            TripSlot slot = _slots[t.Slot];
            int room = slot.Room, entries = _entryWant.Count;
            var pieces = new int[entries];
            int[] pool = _poolLeft.ToArray();
            int boxes = TripRules.Allot(entries, _entryStop.ToArray(), _entryPool.ToArray(), _entryWant.ToArray(), _entrySize.ToArray(), stops,
                Floor(t.Depart, TripRules.DayMinutes) + t.Slot, room, pool, pieces);
            t.Left = 0;
            t.Why = TripWhy.None;
            for (int k = 0; k < stops; k++)
            {
                LogisticsManagerPlanDestination d = _stops[first + k];
                var stop = new TripStop { Place = LogisticsGame.Key(d.deliveryTargetAddress) };
                int behind = 0;
                TripWhy why = TripWhy.None;
                for (int e = 0; e < entries; e++)
                {
                    if (_entryStop[e] != k)
                    {
                        continue;
                    }
                    if (pieces[e] > 0)
                    {
                        stop.Items.Add(_entryItem[e]);
                        stop.Pieces.Add(pieces[e]);
                        stop.Loaded += pieces[e];
                    }
                    if (pieces[e] < _entryWant[e])
                    {
                        behind += _entryWant[e] - pieces[e];
                        why |= pool[_entryPool[e]] <= 0 ? TripWhy.NoStock : TripWhy.Full;
                    }
                }
                if (stop.Items.Count > 0)
                {
                    t.Stops.Add(stop);
                    loadedAll += stop.Loaded;
                    stop.Why = why;
                }
                if (behind > 0)
                {
                    t.Left += behind;
                    t.Why |= why;
                    Behind(plan, from, LogisticsGame.Key(d.deliveryTargetAddress), behind, why);
                }
            }
            if (t.Stops.Count == 0)
            {
                _trips.Trips.Remove(t);   // nothing any stop needs is in the warehouse, or the vehicle has no room: the vehicle stays parked
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
            t.Boxes = boxes;
            t.FuelCents = TripRules.Fuel(t.Metres, slot.Kind, LogisticsGame.PumpCents());
            t.WageCents = TripRules.Wage(slot.WageCents, t.Back - t.Depart);
            LogisticsGame.Fuel(from, t.FuelCents);
            RouteTally tally = Tally(plan.id, from);
            tally.Runs++;
            tally.Taken += loadedAll;
            if (Verbose)
            {
                Logger?.Info("Trips check (a departure from " + LogisticsGame.NameOf(from) + ", slot " + (t.Slot + 1) + "): " + t.Stops.Count + " stop(s), " + t.Pieces()
                             + " piece(s) in " + t.Boxes + " of " + t.Room + " box(es), " + t.Left + " left behind, " + t.Metres + " m, back after " + (t.Back - t.Depart)
                             + " min, fuel " + t.FuelCents + " cents, wages " + t.WageCents + " cents: " + (t.Boxes <= t.Room && t.Back > t.Depart ? "PASS" : "FAIL"));
            }
        }

        // What each stop of a slot's share needs of each product, with what the warehouse holds of it not already on the road shared by the
        // stops: one entry for each stop's product, in the order of the stops, and one pool for each product.
        private void Wants(Trip t, Warehouse from, int first, int stops)
        {
            _entryStop.Clear();
            _entryPool.Clear();
            _entryWant.Clear();
            _entrySize.Clear();
            _entryItem.Clear();
            _poolItem.Clear();
            _poolLeft.Clear();
            for (int k = 0; k < stops; k++)
            {
                LogisticsGame.Needs(_stops[first + k], _items, _wanted);
                for (int g = 0; g < _items.Count; g++)
                {
                    int pool = _poolItem.IndexOf(_items[g]);
                    if (pool < 0)
                    {
                        pool = _poolItem.Count;
                        _poolItem.Add(_items[g]);
                        _poolLeft.Add(Math.Max(0, LogisticsGame.InStock(from, _items[g]) - OnRoad(t.From, _items[g])));
                    }
                    _entryStop.Add(k);
                    _entryPool.Add(pool);
                    _entryWant.Add(_wanted[g]);
                    _entrySize.Add(LogisticsGame.BoxSize(_items[g]));
                    _entryItem.Add(_items[g]);
                }
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
        // What the stop did not take stays in the warehouse and is told; what left it and the stop did not count is told as lost.
        private void Arrive(Trip t, TripStop stop)
        {
            stop.Done = true;
            LogisticsManagerPlan plan = LogisticsGame.Plan(t.Plan);
            Warehouse from = plan != null ? LogisticsGame.From(plan) : null;
            int loaded = stop.Total();
            if (stop.Loaded <= 0)
            {
                stop.Loaded = loaded;
            }
            if (from == null || !LogisticsGame.Deliver(plan, from, stop, _moved, _credited, _wasHeld))
            {
                Returned(t, plan, from, stop, loaded, TripWhy.StopGone);   // the stop or the route is gone: the goods stay in the warehouse
                return;
            }
            int delivered = 0, back = 0, lost = 0;
            TripWhy why = TripWhy.None;
            for (int g = 0; g < stop.Items.Count && g < _moved.Count; g++)
            {
                TripWhy kind = TripRules.Settle(stop.Pieces[g], _wasHeld[g], _moved[g], _credited[g], out int took, out int unmoved, out int gone);
                delivered += took;
                back += unmoved;
                lost += gone;
                why |= kind;
                if (_moved[g] > 0)
                {
                    Moved(t.From, stop.Place, stop.Items[g], _moved[g]);
                }
                stop.Pieces[g] = took;
            }
            stop.Why |= why;
            t.Why |= why;
            RouteTally tally = Tally(t.Plan, from);
            tally.Delivered += delivered;
            tally.Returned += back;
            tally.Lost += lost;
            if (back > 0 || lost > 0)
            {
                Missed(plan, from, stop.Place, back + lost, why);
            }
            if (Verbose)
            {
                Logger?.Info("Trips check (a stop of a route from " + LogisticsGame.NameOf(from) + "): " + delivered + " piece(s) delivered by the game's own call, " + back
                             + " handed back, " + lost + " lost: " + (delivered + back + lost == loaded ? "PASS" : "FAIL"));
            }
        }

        // A stop that took none of what its trip carried (it left the route): the goods never left the warehouse.
        private void Returned(Trip t, LogisticsManagerPlan plan, Warehouse from, TripStop stop, int loaded, TripWhy why)
        {
            for (int g = 0; g < stop.Pieces.Count; g++)
            {
                stop.Pieces[g] = 0;
            }
            stop.Why |= why;
            t.Why |= why;
            RouteTally tally = Tally(t.Plan, from);
            tally.Returned += loaded;
            if (loaded > 0)
            {
                Missed(plan, from, stop.Place, loaded, why);
            }
        }

        // A trip dropped before it finished (too old when the save was read): what it still carried never left the warehouse.
        private void Abandoned(Trip t)
        {
            int carried = 0;
            for (int k = 0; k < t.Stops.Count; k++)
            {
                carried += t.Out && !t.Stops[k].Done ? t.Stops[k].Total() : 0;
            }
            RouteTally tally = Tally(t.Plan, null);
            tally.Returned += carried;
            tally.Taken += carried;
            tally.Why |= TripWhy.Stale;
        }

        // Back at the warehouse: the run goes into the log.
        private void Home(Trip t)
        {
            _trips.Trips.Remove(t);
            int day = Floor(t.Depart, TripRules.DayMinutes);
            _trips.Log.Add(new TripLog
            {
                Day = day, Minute = t.Depart - day * TripRules.DayMinutes, Plan = t.Plan, Slot = t.Slot, Stops = t.Stops.Count, Pieces = t.Pieces(), Boxes = t.Boxes,
                Room = t.Room, Metres = t.Metres, Minutes = t.Back - t.Depart, FuelCents = t.FuelCents, WageCents = t.WageCents,
                Loaded = t.LoadedPieces(), Left = t.Left, Why = (int)t.Why
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

        /// <summary>What a route's trips left behind today: the pieces wanted and not sent or handed back, and why. False when nothing went wrong today.</summary>
        public bool NotSentToday(string plan, out int pieces, out TripWhy why)
        {
            RouteTally t = TripsOn ? _tally.Peek(plan, LogisticsGame.Day) : null;
            pieces = t != null ? t.Left + t.Returned + t.Lost : 0;
            why = t != null ? t.Why : TripWhy.None;
            return pieces > 0 && why != TripWhy.None;
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
