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
    /// <summary>The bus director's part for the buses: the front one and the one behind it on their way, watched, and handed off.</summary>
    public sealed partial class TransitDirector
    {
        // A bus done with is let go: ticked among the buses driving away until nobody can see it, and another takes its place.
        private void HandOff(ref TransitBus bus)
        {
            if (bus.State == TransitBus.Phase.Idle)
            {
                return;
            }
            bus.Release(remove: false);
            if (bus.State == TransitBus.Phase.Idle)
            {
                return;
            }
            _away.Add(bus);
            bus = Spare();
        }

        // An idle bus: one that has gone, or a new one.
        private TransitBus Spare()
        {
            for (int i = _spare.Count - 1; i >= 0; i--)
            {
                TransitBus bus = _spare[i];
                _spare.RemoveAt(i);
                if (bus.State == TransitBus.Phase.Idle)
                {
                    return bus;
                }
            }
            _made++;
            return new TransitBus(_root.gameObject);
        }

        // The buses driving away: each goes once nobody can see it, and is idle again.
        private void RunAway(float now)
        {
            for (int i = _away.Count - 1; i >= 0; i--)
            {
                TransitBus bus = _away[i];
                bus.Tick(now);
                if (bus.TakeDeparted(out float gap, out bool pulledOut))
                {
                    Departed(bus, gap, pulledOut);
                }
                bus.TakeLost();
                if (bus.State == TransitBus.Phase.Idle)
                {
                    _away.RemoveAt(i);
                    _spare.Add(bus);
                }
            }
        }

        private void Departed(TransitBus bus, float gap, bool pulledOut)
        {
            // It waited in its bay for the lane to clear: the departure line was held until now.
            Logger?.Info((bus.DepartureNote ?? "Transit check (departure): the bus leaves its bay") + "; in its bay it waited " + gap.ToString("F0")
                         + " s for the lane beside and behind it to be clear"
                         + (pulledOut ? " and pulled out" : " and no gap came: it drives on from where it stands, out of the lane") + ": "
                         + (pulledOut && bus.DepartureSound ? "PASS" : "FAIL") + ".");
            bus.DepartureNote = null;
        }

        private void RunBus(float now)
        {
            _bus.Tick(now);
            _next.Tick(now);
            RunAway(now);
            if (_bus.TakeDeparted(out float gap, out bool pulledOut))
            {
                Departed(_bus, gap, pulledOut);
            }
            if (_served == null)
            {
                _bus.TakeLost();
                _next.TakeLost();
                return;
            }
            if (_bus.TakeLost())
            {
                // The game or a scene took the vehicle: another is sent at once.
                _eta.Lost();
                _wasStanding = false;
                _doorFound = false;
                _refusals = 0;
                _nextTry = now + RetryEvery;
                _panelKey = int.MinValue;
            }
            if (_next.TakeLost())
            {
                Unpin(_nextCall);
                _nextCall = -1;
            }
            if (!_journey)
            {
                Promote();
                Schedule(NowMinutes());
            }
            switch (_bus.State)
            {
                case TransitBus.Phase.Idle:
                    if (_journey)
                    {
                        break;
                    }
                    int soonest = Soonest();
                    if (_eta.State == TransitEta.Mode.Here || _frontCall >= _calls)
                    {
                        _frontCall = -1;   // the bus that stood here has gone and was kept: the next call is the timetable's
                    }
                    if (soonest != _frontCall && !(_frontCall >= 0 && _pinned[_frontCall]))
                    {
                        _frontCall = soonest;
                        if (_frontCall >= 0 && _frontCall == _nextCall && _next.State == TransitBus.Phase.Idle)
                        {
                            _nextCall = -1;   // the bus behind was given up: this call is the front one's now
                        }
                        _shownAt = -1f;
                        _wasDelayed = false;
                        if (_frontCall >= 0 && _callAt[_frontCall] < double.MaxValue)
                        {
                            _eta.Timetable(SecondsTo(_callAt[_frontCall]));
                        }
                        else
                        {
                            _eta.NoService();
                        }
                        _panelKey = int.MinValue;
                    }
                    if (_frontCall < 0 || _callAt[_frontCall] >= double.MaxValue)
                    {
                        break;
                    }
                    float seconds = SecondsTo(_callAt[_frontCall]);
                    if (!_pinned[_frontCall] && _eta.State == TransitEta.Mode.Timetable && Mathf.Abs(seconds - _eta.Seconds) > 2f)
                    {
                        _eta.Timetable(seconds);   // the timetable moved under it: the rain, the clock's rate
                    }
                    if (seconds <= Lead)
                    {
                        _pinned[_frontCall] = true;   // due: the time shown is kept until a bus has stood here for it
                        if (now >= _nextTry && _player != PlayerStep.Choosing && !_ride.Riding)
                        {
                            if (Put(_bus, _eta, _frontCall, ref _refusals, out string refusal))
                            {
                                Assigned(now, false);
                            }
                            else
                            {
                                _refusals++;
                                _nextTry = now + RetryEvery;
                                if (_refusals == 1 || _refusals % 15 == 0)
                                {
                                    Logger?.Info("Transit: no bus could be put on the road out of sight for " + StopLabel(_served.Calls[_frontCall]) + " yet (try " + _refusals
                                                 + ", again every " + RetryEvery.ToString("F0") + " s; " + seconds.ToString("F0") + " s to its call; the panel says DELAYED " + 8
                                                 + " s after it is due): " + refusal + ".");
                                }
                            }
                        }
                    }
                    if (_bus.State == TransitBus.Phase.Idle)
                    {
                        _eta.Wait(TickInterval, _refusals > 0);   // not in the tick a bus has just been put on the road
                    }
                    break;

                case TransitBus.Phase.Approaching:
                    _eta.Track(TickInterval, _bus.Estimate(_pace), _bus.Waiting != TransitBus.Hold.None);
                    if (!_journey)
                    {
                        Watch(now);   // with the player aboard it is never moved up its line: Journey gives it up instead
                    }
                    break;

                case TransitBus.Phase.Standing:
                    if (!_wasStanding)
                    {
                        _wasStanding = true;
                        _eta.Arrived();
                        Arrived(now);
                    }
                    bool busy = _alighter.Step == RiderStep.Exiting;
                    for (int i = 0; i < Waiters; i++)
                    {
                        busy |= _waiting[i].Step == RiderStep.Rising || _waiting[i].Step == RiderStep.Boarding || _waiting[i].Step == RiderStep.Entering;
                    }
                    bool held = (_player == PlayerStep.Choosing && now < _chooseUntil) || _ride.Riding || _rider.HoldsBus || _rider.Holds(_bus);
                    if (_rider.WantsDeparture && !busy)
                    {
                        Leave(now);   // they sit or hold on: the doors close and it goes
                        break;
                    }
                    // The driver waits for somebody at or walking to the stop, unless the player waits for another line.
                    bool wanted = PlayerAtStop(WaitsWithin) && !_rider.Aboard && (_bookedLine < 0 || _bus.Stop == null || _bus.Stop.Line == _bookedLine);
                    if ((now >= _dwellUntil && !busy && !held && !wanted) || _bus.Standing > DwellMost + (held ? ChooseFor : 0f))
                    {
                        Leave(now);
                    }
                    break;

                case TransitBus.Phase.Leaving:
                    if (_wasStanding)
                    {
                        Leave(now);   // it is already leaving: the bookkeeping only
                    }
                    break;
            }
            RunNext(now);
            _wasDelayed |= _eta.State == TransitEta.Mode.Delayed;
        }

        // The bus after the front one, of whichever line calls next, on its way while the front one stands: a bus every
        // SpacingSeconds where the lines run often. Not while the player rides or gets on.
        private void RunNext(float now)
        {
            if (_journey || _served == null)
            {
                return;
            }
            if (_next.State == TransitBus.Phase.Standing && _bus.State != TransitBus.Phase.Standing && !_rider.Active && !_ride.Riding)
            {
                // The bus behind has pulled in with the front one gone: it is the front one now.
                HandOff(ref _bus);
                Promote();
                return;
            }
            if (_next.State == TransitBus.Phase.Idle)
            {
                if (_nextCall >= 0 && _nextCall == _frontCall)
                {
                    _nextCall = -1;
                }
                if (_rider.Active || _ride.Riding || _bus.State == TransitBus.Phase.Idle || _player == PlayerStep.Choosing)
                {
                    return;
                }
                int soonest = Soonest();
                if (soonest != _nextCall && !(_nextCall >= 0 && _pinned[_nextCall]))
                {
                    _nextCall = soonest;
                    if (_nextCall >= 0 && _callAt[_nextCall] < double.MaxValue)
                    {
                        _nextEta.Timetable(SecondsTo(_callAt[_nextCall]));
                    }
                    else
                    {
                        _nextEta.NoService();
                    }
                }
                if (_nextCall < 0 || _callAt[_nextCall] >= double.MaxValue)
                {
                    return;
                }
                float seconds = SecondsTo(_callAt[_nextCall]);
                if (seconds <= Lead)
                {
                    _pinned[_nextCall] = true;
                    if (now >= _nextTry2 && Put(_next, _nextEta, _nextCall, ref _nextRefusals, out _))
                    {
                        Logger?.Info("Transit check (bus assigned): the next bus, the " + LineNumber(_next.Stop) + " for " + StopLabel(_next.Stop) + ", is on its way behind the "
                                     + LineNumber(_bus.Stop) + ": it starts " + _next.StartedBack.ToString("F0") + " m of line before the stop, "
                                     + Mathf.Sqrt(CitySpots.Flat(_next.Vehicle.transform.position - PlayerHelper.GetPosition())).ToString("F0") + " m from the player, seen: "
                                     + (_next.InView ? "yes" : "no") + ", held at its start " + _next.Held0.ToString("F0") + " s; it reckons " + _nextEta.Seconds.ToString("F0")
                                     + " s, the timetable " + seconds.ToString("F0") + " s: " + (!_next.InView ? "PASS" : "FAIL (it appears where it can be seen)") + ".");
                    }
                    else if (_next.State == TransitBus.Phase.Idle && now >= _nextTry2)
                    {
                        _nextRefusals++;
                        _nextTry2 = now + RetryEvery;
                    }
                }
                if (_next.State == TransitBus.Phase.Idle)
                {
                    _nextEta.Wait(TickInterval, _nextRefusals > 0);
                }
                return;
            }
            if (_next.State == TransitBus.Phase.Approaching)
            {
                _nextEta.Track(TickInterval, _next.Estimate(_pace), _next.Waiting != TransitBus.Hold.None);
                if (_next.NoProgressFor > GiveUpAfter || _next.Out > GiveUpOut)
                {
                    Logger?.Info("Transit check (re-plan): the next bus, the " + LineNumber(_next.Stop) + ", has got nowhere for " + _next.NoProgressFor.ToString("F0")
                                 + " s: it is let go, and the next is sent: FAIL.");
                    HandOff(ref _next);
                    _nextEta.Lost();
                    _nextTry2 = now + 15f;
                }
            }
        }

        // The front bus has gone: the one behind takes its place, with what it was showing.
        private void Promote()
        {
            if (_bus.State != TransitBus.Phase.Idle || _next.State == TransitBus.Phase.Idle)
            {
                return;
            }
            TransitBus idle = _bus;
            _bus = _next;
            _next = idle;
            _eta.CopyFrom(_nextEta);
            _frontCall = _nextCall;
            _nextCall = -1;
            _nextEta.NoService();
            _promised = _eta.Seconds;
            _promisedAt = Time.time + _eta.Seconds;
            _shownAt = -1f;
            _wasStanding = false;
            _doorFound = false;
            _refusals = _nextRefusals;
            _nextRefusals = 0;
            _moveUpAt = Time.time + MoveUpAfter;
            _routeLogAt = Time.time + 60f;
            _busFull = IsRush() && Random.value < 0.25f;
            _panelKey = int.MinValue;
        }

        private void Unpin(int call)
        {
            if (call >= 0 && call < MostCalls)
            {
                _pinned[call] = false;
            }
        }

        // A bus put on the road for a call as far back as the seconds to it take to drive, out of sight, held at its start for
        // what is over. False, with why.
        private bool Put(TransitBus bus, TransitEta eta, int call, ref int refusals, out string refusal)
        {
            TransitStop stop = _served.Calls[call];
            float seconds = Mathf.Max(0f, SecondsTo(_callAt[call]));
            float prefer = TransitPlan.StartBack(seconds, BusCruise, _pace, LightsPerMetre, StartLeast, StartMost);
            // Not further back than the time allows at first; then as far as a start out of sight can be found.
            float most = refusals < 2 ? prefer + 20f : refusals < 6 ? StartMost : StartFar;
            if (!bus.Send(stop, false, out refusal, most, prefer))
            {
                return false;
            }
            float estimate = bus.Estimate(_pace);
            float hold = seconds - estimate;
            if (hold > 0.5f)
            {
                bus.HoldFor(hold);
                estimate = seconds;
            }
            eta.Assign(estimate);
            refusals = 0;
            return true;
        }

        private bool PlayerAtStop(float within)
        {
            return _served != null && !PlayerHelper.IsUsingVehicle && !BuildingManager.IsInsideBuilding
                   && CitySpots.Flat(_served.Sign - PlayerHelper.GetPosition()) < within * within;
        }

        private static bool IsRush()
        {
            return TransitPlan.IsRush(TimeHelper.CurrentHour, TimeHelper.IsWeekend);
        }

        private void Assigned(float now, bool test)
        {
            _busFull = !test && IsRush() && Random.value < 0.25f;
            _promised = _eta.Seconds;
            _promisedAt = now + _eta.Seconds;
            _routeLogAt = now + 60f;
            _moveUpAt = now + MoveUpAfter;
            _refusals = 0;
            _wasStanding = false;
            _panelKey = int.MinValue;
            Vector3 at = _bus.Vehicle.transform.position;
            float away = Mathf.Sqrt(CitySpots.Flat(at - PlayerHelper.GetPosition()));
            bool seen = _bus.InView;
            bool pass = away >= TransitSight.NearKeep && (test || !seen);   // the test button may start it in view, far off
            TransitStop stop = _bus.Stop;
            TransitLine line = TransitMap.Lines[stop.Line];
            Logger?.Info("Transit check (bus assigned): the " + line.Number + " to " + line.To + " for " + StopLabel(stop) + (test ? " (test button)" : "") + ": " + _bus.Chassis
                         + "; it starts " + _bus.StartedBack.ToString("F0") + " m of line before the stop, " + away.ToString("F0")
                         + " m from the player, seen (on the screen, no building between): " + (seen ? "yes" : "no") + "; " + _bus.Remaining.ToString("F0") + " m to drive, "
                         + _bus.DriveAtStart.ToString("F0") + " s of open road as it will drive it, " + _bus.LightsAtStart + " lit junction(s); held at its start "
                         + _bus.Held0.ToString("F0") + " s; the estimate (traffic x" + _pace.Traffic.ToString("F2", CultureInfo.InvariantCulture) + ", "
                         + _pace.Light.ToString("F1", CultureInfo.InvariantCulture) + " s a junction) is "
                         + _eta.Seconds.ToString("F0") + " s, the panel " + (_eta.State == TransitEta.Mode.Delayed ? "DELAYED" : "on time") + "; "
                         + (_bus.InBay ? "it will stand in the stop's bay, " + stop.BayOffset.ToString("F2", CultureInfo.InvariantCulture) + " m off the lane's line"
                             : stop.HasBay ? "it will stop in the lane (" + (_bus.BayNote ?? "the bay cannot be used") + ")"
                             : "the stop has no bay (" + (stop.NoBay ?? "none laid out") + "): it will stop in the lane")
                         + (_bus.SentAway > 0 ? ", " + _bus.SentAway + " of the game's parked cars taken in from the bay out of view" : "")
                         + ": " + (pass ? "PASS" : "FAIL (it appears where it can be seen, or too near)") + ".");
        }

        // The bus on its way: a line a minute, and what is done when it gets nowhere.
        private void Watch(float now)
        {
            float still = _bus.NoProgressFor;
            if (now >= _routeLogAt)
            {
                _routeLogAt = now + 60f;
                Logger?.Info("Transit check (en route): " + _bus.Out.ToString("F0") + " s out, " + _bus.Remaining.ToString("F0") + " m still to drive of "
                             + _bus.StartedBack.ToString("F0") + "; " + WaitingWords() + "; no progress for " + still.ToString("F0") + " s; the panel: " + PanelWords() + ": "
                             + (still < 25f ? "PASS" : "FAIL (it is getting nowhere)") + ".");
            }
            if (still > MoveUpAfter && now >= _moveUpAt && !_bus.InView && !_bus.Near && _bus.Remaining > 110f)
            {
                // Held up out of sight and away from the player: put further up its line, past what holds it.
                _moveUpAt = now + 30f;
                float from = _bus.Remaining;
                string why = WaitingWords();
                TransitStop stop = _bus.Stop;
                _bus.Release(remove: true);
                if (_bus.Send(stop, false, out string refusal, from - 45f))
                {
                    _routeLogAt = now + 60f;
                    Logger?.Info("Transit check (re-plan): the bus had got nowhere for " + still.ToString("F0") + " s, " + from.ToString("F0") + " m from the stop (" + why
                                 + "), out of sight and away from the player: it is moved up its line to " + _bus.Remaining.ToString("F0") + " m from the stop ("
                                 + _bus.Chassis + "): PASS.");
                }
                else
                {
                    _eta.Lost();
                    _refusals = 0;
                    _nextTry = now + RetryEvery;
                    Logger?.Info("Transit check (re-plan): the bus had got nowhere for " + still.ToString("F0") + " s, " + from.ToString("F0") + " m from the stop (" + why
                                 + "), out of sight, and could not be moved up its line (" + refusal + "): taken off the road, another is sent: FAIL.");
                }
                _panelKey = int.MinValue;
                return;
            }
            if (still > GiveUpAfter || _bus.Out > GiveUpOut)
            {
                Logger?.Info("Transit check (re-plan): the bus has got nowhere for " + still.ToString("F0") + " s and has been out " + _bus.Out.ToString("F0") + " s, "
                             + _bus.Remaining.ToString("F0") + " m from the stop (" + WaitingWords()
                             + "): given up (it drives on and goes once nobody can see it), the next bus is sent: FAIL.");
                Notifications.Show(NotificationType.Info, "reallife_transit_bus_skipped", new Dictionary<string, string>(1) { { "route", LineNumber(_bus.Stop) } }, 8f);
                HandOff(ref _bus);
                _eta.Lost();
                _refusals = 0;
                _nextTry = now + 15f;
                _panelKey = int.MinValue;
            }
        }

        private string WaitingWords()
        {
            switch (_bus.Waiting)
            {
                case TransitBus.Hold.Light: return "standing at a red light for " + _bus.WaitingFor.ToString("F0") + " s";
                case TransitBus.Hold.Traffic: return "standing for " + _bus.WaitingFor.ToString("F0") + " s behind " + _bus.WaitsFor();
                case TransitBus.Hold.Closure: return "standing before a closed road for " + _bus.WaitingFor.ToString("F0") + " s";
                default: return _bus.HoldLeft > 0f ? "held at its start for " + _bus.HoldLeft.ToString("F0") + " s more, to keep the timetable" : "driving";
            }
        }

        private string PanelWords()
        {
            return _eta.State == TransitEta.Mode.Delayed ? "DELAYED" + (_eta.Known ? ", " + _eta.Seconds.ToString("F0") + " s" : string.Empty)
                : _eta.State == TransitEta.Mode.Here ? "at the stop" : _eta.State == TransitEta.Mode.None ? "no service" : _eta.Seconds.ToString("F0") + " s (" + _eta.State + ")";
        }

        private static string LineNumber(TransitStop stop)
        {
            return stop != null && stop.Line >= 0 && stop.Line < TransitMap.Lines.Count ? TransitMap.Lines[stop.Line].Number : "bus";
        }
    }
}
