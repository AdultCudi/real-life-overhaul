using System;
using System.Collections.Generic;
using Helpers;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The bus ride through the city in real time, on the game's own activity panel (<see cref="BusRidePanel"/>).
    ///
    /// - WHEN. The player is seated or holds a strap and the doors close (BusRider.Leave): if their stop is further along the
    ///   same line and the line's lane points between are all in the road graph and joined (TransitBus.GapWhy), the panel is
    ///   shown and the ride is driven. Otherwise (a change of bus, a gap in the path, the panel busy, a Going Public session)
    ///   the ride is as before: the bus is seen pulling away and the time passes under the fade, and the log says why.
    /// - THE BUS DRIVES ITS LINE. The bus the player is on is the director's own (<see cref="_bus"/>): once out on its lane after
    ///   a stop it is sent on to the next stop of its line (TransitBus.CarryTo) and that stop is the one served, so people walk
    ///   up and wait there, somebody may get out, and those waiting get on, exactly as at the stop the player boarded at. It
    ///   calls at every stop and stands for the usual time, and drives on to the next, until it stands at the player's.
    /// - THERE, the player gets up and off by the middle door (BusRider.GetOffHere), and the bus closes its doors and leaves.
    /// - GETTING OFF EARLY. A move, or the panel's button, asks for the next stop (RideRules.GetOffAt): the bus calls there anyway,
    ///   and the player gets off there. Nothing is given back. The button then offers to stay on.
    /// - SKIP (the panel's time-skip button), or a bus that gets nowhere for long (RideRules.Stuck), is taken off the street, or
    ///   can no longer be sent on: the rest of the ride passes under the fade, as before, to a bus standing at the stop.
    /// - A SAVE on the way has the player at the last stop the bus called at, or the stop they boarded at (with the fare) before
    ///   it has called at any (BusInside.SaveFareBack). The game's own autosave waits while the panel is up, as for its own actions.
    ///
    /// The budget: nothing at all when no ride is on. While one is: on the director's 0.25 s timer a few comparisons and the
    /// bar's figure; per frame the fast-forward key (BusRidePanel.Keys), with BusRider's own frame.
    /// </summary>
    public sealed partial class TransitDirector
    {
        private const double NoConnection = 180.0;      // game minutes: a second bus further off than this is no connection (the night)
        private const double MinChangeMinutes = 120.0;  // game minutes the fare paid covers the second bus for, at the least
        private const float CarryPatience = 25f;   // seconds of the game's pace (Time.time: none while paused, six a second in fast forward) a bus that has left a stop may take to be out on its lane and sent on

        private bool _journey;
        private BusRidePanel _jPanel;
        private TransitStop _jFrom;        // boarded at
        private TransitStop _jFinal;       // the ticket's stop
        private TransitStop _jOff;         // the stop they will get off at: the ticket's, or the next one asked for
        private TransitStop _jTarget;      // the stop the bus is driving to or stands at
        private TransitStop _jPassed;      // the last stop it left (at first the one boarded at)
        private bool _jStanding;
        private bool _jOffAsked;
        private bool _jGettingOff;
        private float _jBegan;             // Time.unscaledTime the ride began
        private float _jGameBegan;         // Time.time the ride began: the limits are in the game's pace (RideRules.Stuck)
        private float _jLeftAt;            // ...the bus last left a stop
        private float _jStillSince;        // ...it last got a metre further
        private float _jBest;              // metres along the line it has come
        private float _jShown;             // the bar, 0 to 1
        private float _jMinutes;           // game minutes the ride takes by the timetable (TransitTrips): for a skip of the rest
        private float _jClockFrom;         // the game's clock in minutes when the ride began
        private float _jFastSeconds;       // real seconds of it fast-forwarded
        private float _jLastFrame;
        private int _jCalls;               // stops called at on the way

        /// <summary>A ride through the city in real time is under way.</summary>
        public static bool Journeying => Instance != null && Instance._journey;

        // ---------------------------------------------------------------- BusRider calls these

        /// <summary>
        /// The doors have shut with the player seated or holding a strap: the ride through the city on the game's panel, if
        /// it can be had. False, with the reason in the log, for the ride as before (the time passes under the fade).
        /// </summary>
        internal bool JourneyBegins(TransitBus bus, TransitStop from, TransitStop to)
        {
            if (_journey || _failed || bus == null || bus != _bus || from == null || to == null)
            {
                return false;
            }
            string why = null;
            TransitLine line = from.Line >= 0 && from.Line < TransitMap.Lines.Count ? TransitMap.Lines[from.Line] : null;
            if (!TransitTrips.IsDirect(from, to) || line == null)
            {
                why = "the stop is reached with a change of bus, which is not driven";
            }
            else if (GoingPublic.ClockShared)
            {
                why = "in a Going Public session a ride takes no time";
            }
            else
            {
                why = TransitBus.GapWhy(line, from.PathIndex, to.PathIndex);
                if (why != null)
                {
                    why = "the line cannot be driven to the stop in real time: " + why;
                }
            }
            if (why == null)
            {
                _jFrom = from;
                _jFinal = to;
                _jOff = to;
                _jTarget = from;
                _jPassed = from;
                _jPanel = BusRidePanel.Show(this);
                if (_jPanel == null)
                {
                    why = "the game's activity panel is busy";
                }
            }
            if (why != null)
            {
                Logger?.Info("Transit check (journey): from " + StopLabel(from) + " to " + StopLabel(to) + " the ride is not driven in real time (" + why + "): the time passes under the fade as before: PASS.");
                return false;
            }
            _journey = true;
            TransitBus.FastRide = true;
            // The bus that was coming on behind is not the player's: it drives on along its line and goes once nobody can see it.
            HandOff(ref _next);
            Unpin(_nextCall);
            _nextCall = -1;
            _nextEta.NoService();
            _jFrom = from;
            _jFinal = to;
            _jOff = to;
            _jTarget = from;
            _jPassed = from;
            _jStanding = false;
            _jOffAsked = false;
            _jGettingOff = false;
            _jBegan = Time.unscaledTime;
            _jGameBegan = Time.time;
            _jLeftAt = _jGameBegan;
            _jStillSince = _jGameBegan;
            _jLastFrame = _jBegan;
            _jBest = from.Metres;
            _jShown = 0f;
            _jMinutes = TransitTrips.Between(from, to).Minutes;
            _jClockFrom = TimeHelper.NowInMinutes();
            _jFastSeconds = 0f;
            _jCalls = 0;
            _rideTo = to;
            TransitState.BusRides++;
            bool first = _hasPlan && _planLeg == 1 && to == _plan.ChangeOff;
            bool second = _hasPlan && _planLeg == 2;
            Logger?.Info("Transit check (journey): " + (first ? "leg 1 of 2: " : second ? "leg 2 of 2: " : string.Empty) + "the player rides the " + line.Number + " from " + StopLabel(from) + " to " + StopLabel(to)
                         + (first ? " to change there for the " + LineNumber(_plan.ChangeOn) + " to " + StopLabel(_plan.To) : string.Empty)
                         + " in real time on the game's activity panel: " + (to.Order - from.Order) + " stop(s), " + (to.Metres - from.Metres).ToString("F0") + " m of line, " + _jMinutes.ToString("F0")
                         + " game minutes by the timetable; the panel offers fast-forward and the time skip: PASS.");
            return true;
        }

        /// <summary>The panel's title: the journey's end, and on the first bus of a journey with a change, the change.</summary>
        internal (string key, object arguments) JourneyHeadline()
        {
            if (_journey && _hasPlan && _planLeg == 1 && !_jOffAsked && _jFinal == _plan.ChangeOff)
            {
                return ("reallife_bus_ride_title_change", new { name = StopName(_plan.To), change = StopName(_plan.ChangeOff), route = LineNumber(_plan.ChangeOn) });
            }
            return ("reallife_bus_ride_title", new { name = JourneyStopName(true) });
        }

        // ---------------------------------------------------------------- the change of bus

        // Off the first bus at the change: the next bus is the second line's, at this place or a short walk away. One fare
        // covers the journey (TransitState.TransferUntil, kept in the save). Where the second line does not run now, the
        // rest of the journey passes under the fade, as a change always did.
        private void BeginChange()
        {
            TransitStop on = _plan.ChangeOn;
            TransitLine second = TransitMap.Lines[on.Line];
            double now = NowMinutes();
            double next = TransitPlan.NextCall(now, second.Main, second.OffsetAt(on.Order), Raining(), CivicPolicy.ServiceScale);
            if (next >= double.MaxValue || next - now > NoConnection)
            {
                Notifications.Show(NotificationType.Info, "reallife_transit_change_none", new Dictionary<string, string>(1) { { "route", second.Number } }, 8f);
                Logger?.Info("Transit check (journey): leg 1 of 2 done at " + StopLabel(_plan.ChangeOff) + "; the " + second.Number + " does not run now (the night service is the main lines'), so the rest of the journey to "
                             + StopLabel(_plan.To) + " passes under the fade: PASS.");
                FadeRest("the connecting bus does not run now");
                return;
            }
            _planLeg = 2;
            _changing = true;
            TransitState.TransferUntil = now + Math.Max(MinChangeMinutes, next - now + 60.0);
            bool walk = on.At != _plan.ChangeOff.At;
            float metres = Mathf.Sqrt(CitySpots.Flat(on.Wait - _plan.ChangeOff.Wait));
            int minutes = Mathf.Max(1, Mathf.CeilToInt((float)(next - now)));
            if (!walk)
            {
                Book(on);
                minutes = BookedMinutes(minutes);
            }
            Notifications.Show(NotificationType.Info, walk ? "reallife_transit_change_walk" : "reallife_transit_change_here",
                new Dictionary<string, string>(3) { { "route", second.Number }, { "name", StopName(on) }, { "minutes", minutes.ToString() } }, 9f);
            Logger?.Info("Transit check (journey): leg 1 of 2 done at " + StopLabel(_plan.ChangeOff) + "; the change: " + (walk ? "a walk of " + metres.ToString("F0") + " m to " + StopLabel(on.At) : "at the same stop")
                         + " for the " + second.Number + " to " + StopLabel(_plan.To) + ", the next in " + minutes + " game minutes by its timetable; no second fare: PASS.");
        }

        // At the change, once the first bus has gone: the place of the second bus is the one served, and the player walks there.
        private void ChangeStep()
        {
            if (!_changing || !_hasPlan || _offBus != null || _rider.Active)
            {
                return;
            }
            TransitStop on = _plan.ChangeOn;
            if (_served == on.At && _booked != null)
            {
                return;
            }
            if (_served != on.At)
            {
                if (_served != null)
                {
                    EndService();
                }
                if (!Dressed(on.At))
                {
                    DressNow(on.At, PlayerHelper.GetPosition());
                }
                BeginService(on.At, Time.time);
                var game = InstanceBehavior<GameManager>.Instance;
                PlayerController player = game != null ? game.playerController : null;
                if (player != null && !AtStop(on.At, player.transform.position))
                {
                    player.SetGoal(on.Wait, null);   // the game's own walk to the next stop
                }
            }
            Book(on);
        }

        // The second bus of a journey, booked at its stop: boarded by itself when it stands, as a stop picked while waiting is.
        private void Book(TransitStop on)
        {
            _booked = _plan.To;
            _bookedLine = on.Line;
            _bookedTrip = new TransitTrip { To = _plan.To, Board = on, Direct = true, Minutes = TransitTrips.Between(on, _plan.To).Minutes, Metres = _plan.To.Metres - on.Metres };
            _panelKey = int.MinValue;
        }

        // Game minutes to the booked line's next call at the place served, as the panel has it.
        private int BookedMinutes(int otherwise)
        {
            if (_served == null)
            {
                return otherwise;
            }
            Schedule(NowMinutes());
            for (int k = 0; k < _calls; k++)
            {
                if (_served.Calls[k].Line == _bookedLine && _callAt[k] < double.MaxValue)
                {
                    return Mathf.Max(1, Mathf.CeilToInt((float)(_callAt[k] - NowMinutes())));
                }
            }
            return otherwise;
        }

        // The stop panel's skip at the change: the rest of the journey under the fade.
        private void SkipChangeClicked()
        {
            try
            {
                _keyFrame = Time.frameCount;
                if (_changing && _hasPlan && TransitRide.CanStart() && !_rider.Active && !_ride.Riding)
                {
                    FadeRest("the player skipped the rest of the journey at the change");
                }
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        // The rest of a journey with a change, from the change, under the fade: the wait for the second bus and its ride.
        private void FadeRest(string why)
        {
            TransitStop on = _plan.ChangeOn;
            TransitStop to = _plan.To;
            TransitLine second = TransitMap.Lines[on.Line];
            double now = NowMinutes();
            double next = TransitPlan.NextCall(now, second.Main, second.OffsetAt(on.Order), Raining(), CivicPolicy.ServiceScale);
            float wait = next < double.MaxValue ? Mathf.Clamp((float)(next - now), 0f, (float)NoConnection) : TransitTrips.ChangeMinutes;
            float minutes = wait + TransitTrips.Between(on, to).Minutes;
            Logger?.Info("Transit check (journey): " + why + ": the rest of the journey to " + StopLabel(to) + ", " + minutes.ToString("F0") + " game minutes (" + wait.ToString("F0")
                         + " of them waiting for the " + second.Number + "), passes under the fade: PASS.");
            DropPlan();
            TransitState.TransferUntil = 0.0;
            _booked = null;
            _bookedLine = -1;
            _rideTo = to;
            _player = PlayerStep.Riding;
            _seat.Abort();
            HideHud();
            TransitState.BusRides++;
            _ride.Begin(Mathf.Max(1f, minutes), to.Wait, "reallife_transit_ride_fade", RideFaded, RideArrived, arriveAboard: _arriveAboard ?? (_arriveAboard = ArriveAboard));
        }

        /// <summary>A move of the player's own while riding, or the panel's button: off at the next stop. No refund.</summary>
        internal void JourneyGetOff(string why)
        {
            if (!_journey || _jGettingOff || _jOffAsked)
            {
                return;
            }
            TransitLine line = TransitMap.Lines[_jFrom.Line];
            bool standing = _bus.State == TransitBus.Phase.Standing && _jTarget != _jPassed;
            int order = RideRules.GetOffAt(_jPassed.Order, _jTarget.Order, standing, _bus.DoorsOpen, _jFinal.Order);
            if (order < 0 || order >= line.Stops.Count)
            {
                return;
            }
            _jOff = line.Stops[order];
            _jOffAsked = true;
            if (_hasPlan && _jOff != _plan.ChangeOff)
            {
                DropPlan();   // (off before the change: the rest of the journey is let go)
            }
            _jPanel?.Refresh();
            Notifications.Show(NotificationType.Info, "reallife_bus_ride_getting_off", new Dictionary<string, string>(1) { { "name", StopName(_jOff) } }, 6f);
            Logger?.Info("Transit check (journey): " + why + ": they will get off at " + StopLabel(_jOff) + (_jOff == _jFinal ? " (their own stop)" : ", " + (_jFinal.Order - _jOff.Order) + " stop(s) short of theirs, nothing given back")
                         + ": PASS.");
        }

        /// <summary>The panel's "stay on": the stop asked for is let go, and they ride on to their own.</summary>
        internal void JourneyStayOn()
        {
            if (!_journey || _jGettingOff || !_jOffAsked)
            {
                return;
            }
            _jOffAsked = false;
            _jOff = _jFinal;
            _jPanel?.Refresh();
            Logger?.Info("Transit check (journey): the player stays on to " + StopLabel(_jFinal) + ": PASS.");
        }

        /// <summary>The rest of the ride under the fade, from where the bus is (TransitRide, as the ride always was).</summary>
        internal void JourneySkip(string why)
        {
            if (!_journey)
            {
                return;
            }
            TransitStop to = _jOff;
            float minutes = RideRules.MinutesLeft(TransitTrips.Between(_jFrom, to).Minutes, _jShown);
            if (_hasPlan && _planLeg == 1 && !_jOffAsked && to == _plan.ChangeOff)
            {
                // A journey with a change: the skip is of the whole of it, to its last stop (the change and the second bus passed under the fade).
                minutes += TransitTrips.RestMinutes(_plan, null);
                to = _plan.To;
                DropPlan();
            }
            Logger?.Info("Transit check (journey): " + why + ": the rest of the ride, " + minutes.ToString("F0") + " game minutes to " + StopLabel(to) + ", passes under the fade after "
                         + (Time.unscaledTime - _jBegan).ToString("F0") + " s of it in real time and " + _jCalls + " stop(s) called at: PASS.");
            EndJourney(null);
            _rider.SkipRest();
            _rideTo = to;
            _ride.Begin(minutes, to.Wait, "reallife_transit_ride_fade", RideFaded, RideArrived, tap: false, atBlack: _atBlack ?? (_atBlack = _rider.AtBlack),
                arriveAboard: _arriveAboard ?? (_arriveAboard = ArriveAboard));
        }

        /// <summary>The game closed the panel itself: the ride goes on without it, and the player gets off at their stop as before.</summary>
        internal void JourneyPanelGone()
        {
            _jPanel = null;
            Logger?.Info("Transit check (journey): the game closed its activity panel during the ride: the bus drives on to " + (_jOff != null ? StopLabel(_jOff) : "the stop") + " all the same: PASS.");
        }

        /// <summary>The rider is done, whichever way (off, ended early, the city going): the ride is over.</summary>
        internal void RiderCleared()
        {
            if (_journey)
            {
                EndJourney(_jGettingOff ? null : "the ride ended early");
            }
        }

        // ---------------------------------------------------------------- what the panel shows

        internal float JourneyProgress => _journey ? _jShown : 0f;
        internal bool JourneyOffAsked => _journey && _jOffAsked;

        internal string JourneyStopName(bool final)
        {
            TransitStop stop = final ? _jOff ?? _jFinal : _jTarget;
            return stop != null ? StopName(stop) : string.Empty;
        }

        internal float JourneyMinutesLeft()
        {
            float left = _journey ? RideRules.MinutesLeft(_jMinutes, _jShown) : 1f;
            return _journey && _hasPlan && _planLeg == 1 && !_jOffAsked ? left + TransitTrips.RestMinutes(_plan, null) : left;
        }

        internal (string key, object arguments) JourneyBarLabel()
        {
            if (!_journey || _jOff == null)
            {
                return ("reallife_bus_ride_bar_last", new { name = _jOff != null ? StopName(_jOff) : string.Empty });
            }
            bool standing = _bus.State == TransitBus.Phase.Standing && _jTarget != _jPassed;
            int next = _jTarget != _jPassed ? _jTarget.Order : _jPassed.Order + 1;
            int left = RideRules.StopsLeft(next, _jOff.Order);
            if (_jGettingOff || (standing && _jTarget == _jOff))
            {
                return ("reallife_bus_ride_bar_here", new { name = StopName(_jOff) });
            }
            TransitLine line = TransitMap.Lines[_jFrom.Line];
            string nextName = next >= 0 && next < line.Stops.Count ? StopName(line.Stops[next]) : StopName(_jOff);
            if (left <= 1)
            {
                return ("reallife_bus_ride_bar_last", new { name = nextName });
            }
            return ("reallife_bus_ride_bar", new { count = left, name = nextName });
        }

        // ---------------------------------------------------------------- the timer and the frame

        // Per frame, only while a ride is on: the fast-forward key.
        private void JourneyFrame()
        {
            float now = Time.unscaledTime;
            if (BusRidePanel.FastForwarding)
            {
                _jFastSeconds += now - _jLastFrame;
            }
            _jLastFrame = now;
            _jPanel?.Keys();
        }

        // On the director's timer, before the bus is ticked, only while a ride is on.
        private void RunJourney(float now)
        {
            float real = Time.unscaledTime;
            TransitBus bus = _bus;
            if (bus.State == TransitBus.Phase.Idle || bus.Vehicle == null)
            {
                if (_rider.RidingThrough)
                {
                    JourneySkip("the bus was taken off the street");
                }
                return;
            }
            if (_jGettingOff)
            {
                return;   // (BusRider has them; the bus stands for them: TransitDirector.RunBus)
            }
            TransitLine line = TransitMap.Lines[_jFrom.Line];
            switch (bus.State)
            {
                case TransitBus.Phase.Leaving:
                    if (_jStanding)
                    {
                        // It has left a stop on the way.
                        _jStanding = false;
                        _jPassed = _jTarget;
                        _jLeftAt = now;
                        _jCalls++;
                        _rider.PassedStop(_jPassed);
                    }
                    if (_jTarget == _jPassed && _jPassed.Order + 1 < line.Stops.Count)
                    {
                        TransitStop next = line.Stops[_jPassed.Order + 1];
                        string refusal = "it waits in its bay for the lane to be clear";
                        if (!bus.AwaitingGap && bus.CarryTo(next, out refusal))
                        {
                            _jTarget = next;
                            MoveService(next, now);
                            Logger?.Info("Transit check (journey): the bus drives on from " + StopLabel(_jPassed) + " to " + StopLabel(next) + ", " + bus.Remaining.ToString("F0") + " m, "
                                         + (bus.InBay ? "to stand in its bay" : "to stop in the lane") + ": PASS.");
                        }
                        else if (!bus.AwaitingGap && now - _jLeftAt > CarryPatience)
                        {
                            JourneySkip("the bus could not be sent on to " + StopLabel(next) + " (" + refusal + ")");
                            return;
                        }
                    }
                    break;

                case TransitBus.Phase.Standing:
                    if (_jTarget != _jPassed)
                    {
                        _jStanding = true;
                        if (_jTarget == _jOff && bus.DoorsOpen && _rider.GetOffHere(bus, _jTarget))
                        {
                            _jGettingOff = true;
                            _jShown = 1f;
                            float took = real - _jBegan;
                            Logger?.Info("Transit check (journey): the bus stands at " + StopLabel(_jOff) + " with the player in it, " + took.ToString("F0") + " s after the doors shut ("
                                         + _jFastSeconds.ToString("F0") + " s of it fast-forwarded), " + (TimeHelper.NowInMinutes() - _jClockFrom).ToString("F0") + " game minutes, "
                                         + (_jOff.Metres - _jFrom.Metres).ToString("F0") + " m of line and " + _jCalls + " stop(s) on the way; they get off by the middle door"
                                         + (_jOff == _jFinal ? "" : " short of their own stop, as they asked") + ": PASS.");
                            Notifications.Show(NotificationType.Info, "reallife_transit_ride_arrived", new Dictionary<string, string>(1) { { "name", StopName(_jOff) } }, 6f);
                            ModState.AddModifier(ModState.MoodUp);
                            _jPanel?.Close();
                            _jPanel = null;
                            return;
                        }
                    }
                    break;
            }
            // How far along: the bar, and whether it is getting anywhere.
            bool approaching = bus.State == TransitBus.Phase.Approaching;
            bool atStop = bus.State == TransitBus.Phase.Standing || bus.AwaitingGap;
            float at = RideRules.AtMetres(_jTarget.Metres, bus.Remaining, approaching, bus.State == TransitBus.Phase.Standing && _jTarget != _jPassed, _jPassed.Metres);
            if (at > _jBest + RideRules.PastStop)
            {
                _jBest = at;
                _jStillSince = now;
            }
            _jShown = RideRules.Advance(_jShown, RideRules.Progress(_jFrom.Metres, _jOff.Metres, _jBest));
            if (RideRules.Stuck(now - _jStillSince, atStop, now - _jGameBegan))
            {
                JourneySkip("the bus has got nowhere for " + (now - _jStillSince).ToString("F0") + " s (" + (atStop ? "at a stop" : WaitingWords()) + ")");
            }
        }

        // The stop the bus drives to is the one served now: people walk up and wait there, and get on when it stands.
        private void MoveService(TransitStop stop, float now)
        {
            ReleaseRiders();
            _served = stop.At;
            _calls = Mathf.Min(_served.Calls.Count, MostCalls);
            _frontCall = _served.Calls.IndexOf(stop);
            for (int i = 0; i < MostCalls; i++)
            {
                _pinned[i] = i == _frontCall;
                _doneAt[i] = double.MinValue;
                _callAt[i] = double.MaxValue;
            }
            _nextCall = -1;
            _wasStanding = false;
            _doorFound = false;
            _refusals = 0;
            _nextWaiter = now + 1f;
            _panelKey = int.MinValue;
        }

        private void EndJourney(string why)
        {
            if (!_journey)
            {
                return;
            }
            _journey = false;
            TransitBus.FastRide = false;
            if (why != null)
            {
                Logger?.Info("Transit check (journey): " + why + " after " + (Time.unscaledTime - _jBegan).ToString("F0") + " s, " + _jCalls + " stop(s) called at; the game's panel is closed: PASS.");
            }
            try
            {
                _jPanel?.Close();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            _jPanel = null;
            _jGettingOff = false;
            _jOffAsked = false;
        }
    }
}
