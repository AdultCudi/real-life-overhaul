using System.Collections.Generic;
using System.Globalization;
using BigAmbitions.DayNightCycle;
using GleyTrafficSystem;
using Helpers;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>The bus director's part for a bus at the stop: where it stands, its doors, who gets out, and its leaving.</summary>
    public sealed partial class TransitDirector
    {
        // The bus has just stopped: where it stands, where its door is, who gets out, whether those waiting can reach it.
        private void Arrived(float now)
        {
            _dwellUntil = now + DwellLeast;
            TransitStop stop = _bus.Stop ?? _served;
            TransitLine line = TransitMap.Lines[stop.Line];
            if (!Riding && PlayerAtStop(WaitsWithin))
            {
                // Said once as it pulls in: the bus is a small one, and it is not obvious that it is theirs.
                Notifications.Show(NotificationType.Info, "reallife_transit_bus_in", new Dictionary<string, string>(1) { { "route", line.Number } }, 8f);
            }
            _doorFound = false;
            _boardable = false;
            _boarded = 0;
            _alighted = 0;
            BusVehicle car = _bus.Vehicle;
            // Where it stands: in its bay by the bay's measure (nothing of it in the lane or over the kerb, parallel to it), else by
            // the lane's.
            _bus.Measure(out float along, out float aside, out float angle, out float toKerb, out float toLane);
            bool inBay = _bus.InBay;
            bool placed = inBay ? Mathf.Abs(along) <= 2.5f && toLane >= -0.15f && toKerb >= 0f && angle <= 3f   // its rear corner a hand's breadth in the lane at the narrowest bay (NavCheck)
                                : Mathf.Abs(along) <= 2.5f && Mathf.Abs(aside) <= 1.2f && angle <= 12f;
            string held = _bus.StoodShort;   // held up short of its place, it is at the stop where it is
            Logger?.Info("Transit check (arrival): the " + line.Number + " stands at " + StopLabel(stop) + (inBay ? " in its bay" : "") + " with its middle "
                         + Mathf.Abs(along).ToString("F1", CultureInfo.InvariantCulture) + " m " + (along >= 0f ? "past" : "short of")
                         + (Mathf.Abs(stop.Stand) > 0.01f
                             ? " its place " + Mathf.Abs(stop.Stand).ToString("F1", CultureInfo.InvariantCulture) + " m " + (stop.Stand > 0f ? "on from" : "short of") + " the pole"
                             : " the pole")
                         + ", " + Mathf.Abs(aside).ToString(inBay ? "F2" : "F1") + " m " + (aside >= 0f ? "towards the kerb" : "away from the kerb")
                         + " off the lane's line" + (inBay ? " (the bay's line is " + stop.BayOffset.ToString("F2", CultureInfo.InvariantCulture) + ")" : "") + ", "
                         + angle.ToString(inBay ? "F1" : "F0")
                         + " degrees off the lane's direction, door side to the kerb with " + toKerb.ToString(inBay ? "F2" : "F1") + " m of road between"
                         + (inBay ? ", its body " + toLane.ToString("F2", CultureInfo.InvariantCulture) + " m clear of the lane's edge (a lorry on the lane's line passes "
                                    + (toLane + BusBay.LaneHalf - BusBay.WidestHalf).ToString("F2", CultureInfo.InvariantCulture)
                                    + " m from it): the lane is " + (toLane >= 0f ? "free" : "NOT free")
                             : stop.HasBay ? ", in the lane (" + (_bus.BayNote ?? "the bay was not used") + ")"
                             : ", in the lane (no bay here: " + (stop.NoBay ?? "none laid out") + ", the kerb " + stop.KerbAside.ToString("F2", CultureInfo.InvariantCulture)
                                 + " m from the lane's line)")
                         + "; " + _bus.Took.ToString("F0") + " s after setting out " + _bus.StartedBack.ToString("F0") + " m back"
                         + (held != null ? "; " + held : string.Empty) + ": "
                         + (placed ? "PASS" : held != null ? "PASS (where it was held up: it counts as standing at the stop)"
                             : inBay ? "FAIL (allowed: 2.5 m along, 3 degrees, nothing of it in the lane or over the kerb)" : "FAIL (allowed: 2.5 m along, 1.2 m aside, 12 degrees)") + ".");
            OnTime(now, line, stop);
            if (_frontCall >= 0 && _frontCall < MostCalls)
            {
                _doneAt[_frontCall] = NowMinutes();
                _lastCall = NowMinutes();
                Unpin(_frontCall);
            }

            // The door: where somebody stands to step in, on the road surface clear of every other vehicle, a little further out
            // from the body if the mesh does not come that close.
            _bus.DoorPlace(out Vector3 want, out _doorInwards);
            float further = 0f;
            for (int step = 0; step < 4 && !_doorFound; step++)
            {
                further = step * 0.3f;
                Vector3 place = want - _doorInwards * further;
                if (StreetSpots.OnRoad(place, 0.5f, out Vector3 door) && CitySpots.Flat(door - place) < 0.3f * 0.3f && JobSpots.Clear(door, car.transform))
                {
                    _door = door;
                    _doorFound = true;
                }
            }
            bool wayClear = _doorFound && WayClear(_served.Wait, _door, car);
            // Those who get off do so by the middle door, as the player does, where it is on the road and clear; else by the front one.
            _exitDoor = _door;
            _bus.MiddleDoorPlace(out Vector3 exitWant, out _);
            if (_doorFound && StreetSpots.OnRoad(exitWant, 0.5f, out Vector3 exitAt) && CitySpots.Flat(exitAt - exitWant) < 0.3f * 0.3f && JobSpots.Clear(exitAt, car.transform)
                && WayClear(_served.Wait, exitAt, car))
            {
                _exitDoor = exitAt;
            }
            int waiting = 0;
            int seated = 0;
            for (int i = 0; i < Waiters; i++)
            {
                waiting += _waiting[i].Step == RiderStep.Waiting || _waiting[i].Step == RiderStep.Arriving ? 1 : 0;
                seated += _waiting[i].Sat ? 1 : 0;
            }
            bool sat = _seat.Seated && (_bookedLine < 0 || stop.Line == _bookedLine);
            Logger?.Info("Transit check (doors): doors open; a place to step in "
                         + (_doorFound ? "found " + further.ToString("F1", CultureInfo.InvariantCulture) + " m further out than the door wants" : "NOT found on the road surface beside the door")
                         + "; the way from the kerb to it " + (!_doorFound ? "not looked at" : wayClear ? "clear" : "has vehicles standing in it") + "; " + waiting + " waiting ("
                         + seated + " seated)" + (sat ? ", the player seated and getting up" : "") + "; the bus is "
                         + (_busFull ? "full: no seat free, whoever boards stands" : "not full") + "; it stands " + (_bus.InBay ? "out of the lane" : "in the lane") + ": "
                         + (_doorFound && wayClear ? "PASS" : "FAIL (nobody on foot gets in or out this time; the player still boards from the stop)") + ".");
            if (!wayClear)
            {
                _doorFound = false;
            }
            _boardable = _doorFound;   // a full bus has no seat free: whoever gets on stands
            if (sat)
            {
                _seat.Stand(now);   // the bus is here: up off the bench
            }
            if (_doorFound && Random.value < 0.6f && !(_journey && _served == (_jOff != null ? _jOff.At : null)))
            {
                Alight(now);   // not where the player gets off: the middle door is theirs
            }
            // How long it stands at the least: in the lane it is in everybody's way, so where nobody has business with it the doors
            // open and shut and it goes; in its bay it keeps the usual stand.
            bool nobody = waiting == 0 && !sat && _alighter.Step == RiderStep.None && _player != PlayerStep.Choosing && !Riding && !PlayerAtStop(WaitsWithin);
            _dwellLeast = toLane < 0f && nobody ? DwellEmpty : DwellLeast;
            _dwellUntil = now + _dwellLeast;
            _panelKey = int.MinValue;
        }

        // The promise against the arrival, for the record, and what the arrival teaches the estimate (TransitPlan.Pace).
        private void OnTime(float now, TransitLine line, TransitStop stop)
        {
            float took = _bus.Took;
            if (took <= 0f || _promisedAt <= 0f)
            {
                return;   // put standing there for a ride's arrival: nothing promised
            }
            float moving = took - _bus.Held0 - _bus.AtLights;
            float before = _pace.Traffic;
            _pace.Learn(_bus.DriveAtStart, _bus.LightsAtStart, moving, _bus.AtLights);
            float off = now - _promisedAt;
            float slack = TransitPlan.Slack(_promised);
            string shown = _shownAt > 0f ? "; the panel first showed the player " + _shownFigure.ToString("F0") + " s, off by " + (now - _shownAt).ToString("+0;-0") + " s"
                : string.Empty;
            Logger?.Info("Transit check (on time): the " + line.Number + " at " + StopLabel(stop) + " was promised for " + _promised.ToString("F0") + " s after it set out ("
                         + (_bus.Held0 > 0.5f ? "held " + _bus.Held0.ToString("F0") + " s at its start, " : string.Empty) + _bus.DriveAtStart.ToString("F0") + " s of open road, "
                         + _bus.LightsAtStart + " lit junction(s)) and stood there after " + took.ToString("F0") + " s (" + _bus.AtLights.ToString("F0")
                         + " s of it at red lights): " + off.ToString("+0;-0") + " s against the promise" + shown + "; the estimate learns traffic x"
                         + before.ToString("F2", CultureInfo.InvariantCulture) + " to x" + _pace.Traffic.ToString("F2", CultureInfo.InvariantCulture) + ", "
                         + _pace.Light.ToString("F1", CultureInfo.InvariantCulture) + " s a junction (" + _pace.Samples + " bus(es)); the panel said DELAYED: "
                         + (_eta.State == TransitEta.Mode.Delayed || _wasDelayed ? "yes" : "no") + ": " + (Mathf.Abs(off) <= slack ? "PASS" : "FAIL (more than "
                         + slack.ToString("F0") + " s out)") + ".");
            _promisedAt = 0f;
            _wasDelayed = false;
        }

        // Nothing but the bus stands on the straight way from the kerb to its door.
        private bool WayClear(Vector3 from, Vector3 to, BusVehicle bus)
        {
            int mask = TrafficTools.VehicleMask;
            if (mask == 0)
            {
                return true;
            }
            int count = Physics.OverlapCapsuleNonAlloc(from + Vector3.up * 0.9f, to + Vector3.up * 0.9f, 0.32f, _touching, mask, QueryTriggerInteraction.Ignore);
            bool clear = count < _touching.Length;
            for (int i = 0; i < count; i++)
            {
                if (clear && _touching[i] != null && !_touching[i].transform.IsChildOf(bus.transform))
                {
                    clear = false;
                }
                _touching[i] = null;
            }
            return clear;
        }

        private void Leave(float now)
        {
            _wasStanding = false;
            _doorFound = false;
            if (_rider.Now == BusRider.Step.ToDoor)
            {
                _rider.Cancel("the bus left before they reached its door");
            }
            if (_bus.State == TransitBus.Phase.Standing)
            {
                float stood = _bus.Standing;
                _bus.Depart();
                if (_rider.Aboard && _rider.Stage == BusInside.Stage.Aboard)
                {
                    _rider.Departing();   // no getting off now: the pull-away is watched
                }
                bool sound = stood <= DwellMost + ChooseFor + 2f;
                bool waits = _bus.AwaitingGap;   // in its bay: it goes when the lane is clear, and the line is written then
                _bus.DepartureNote = null;
                _bus.DepartureSound = sound;
                if (Logger != null)
                {
                    string said = "Transit check (departure): the " + LineNumber(_bus.Stop) + " " + (waits ? "closes its doors" : "pulls away in the lane") + " after standing "
                                  + stood.ToString("F0") + " s (" + _dwellLeast.ToString("F0") + " to " + DwellMost.ToString("F0") + " allowed); " + _boarded + " got in, "
                                  + _alighted + " got out, the player " + (Riding ? "is aboard" : _player == PlayerStep.Choosing ? "was still choosing a stop" : "did not board");
                    if (waits)
                    {
                        _bus.DepartureNote = said;
                    }
                    else
                    {
                        Logger.Info(said + ": " + (sound ? "PASS" : "FAIL") + ".");
                    }
                }
            }
            _busFull = false;
            // Whoever was still on the way to the door goes back to waiting.
            for (int i = 0; i < Waiters; i++)
            {
                if ((_waiting[i].Step == RiderStep.Boarding || _waiting[i].Step == RiderStep.Rising) && _waiting[i].Person != null && _waiting[i].Person.IsValid)
                {
                    Unseat(ref _waiting[i]);
                    _waiting[i].Person.ClearPose();
                    _waiting[i].Step = _waiting[i].Person.Place(_waiting[i].Person.Position, true) && _waiting[i].Person.MoveTo(_served.Wait, Pace.Walk)
                        ? RiderStep.Arriving : RiderStep.Waiting;
                    _waiting[i].Spot = _served.Wait;
                }
            }
            if (_player == PlayerStep.Choosing)
            {
                _player = PlayerStep.None;
                Notifications.Show(NotificationType.Info, "reallife_transit_bus_missed", null, 6f);
            }
            // The bus drives off on its own and the next one is the front one, unless the player is on it, or getting on or off,
            // when it stays the director's.
            if (!_journey && !_rider.Active && !_ride.Riding && _offBus != _bus && _away.Count < AwayMost)
            {
                _away.Add(_bus);
                _bus = Spare();
                _frontCall = -1;
                _eta.NoService();
                Promote();
            }
            _panelKey = int.MinValue;
        }
    }
}
