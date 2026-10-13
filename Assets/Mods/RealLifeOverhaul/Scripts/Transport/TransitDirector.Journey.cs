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
            Logger?.Info("Transit check (journey): the player rides the " + line.Number + " from " + StopLabel(from) + " to " + StopLabel(to) + " in real time on the game's activity panel: "
                         + (to.Order - from.Order) + " stop(s), " + (to.Metres - from.Metres).ToString("F0") + " m of line, " + _jMinutes.ToString("F0")
                         + " game minutes by the timetable; the panel offers fast-forward and the time skip: PASS.");
            return true;
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
            return _journey ? RideRules.MinutesLeft(_jMinutes, _jShown) : 1f;
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
            _served = stop;
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
