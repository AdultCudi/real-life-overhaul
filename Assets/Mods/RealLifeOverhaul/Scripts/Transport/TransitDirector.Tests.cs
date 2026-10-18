using System.Collections.Generic;
using BAModAPI;
using BigAmbitions.DayNightCycle;
using GleyTrafficSystem;
using Helpers;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>The bus director's test buttons on the options page.</summary>
    public sealed partial class TransitDirector
    {
        private bool TestReady(string test, out Vector3 at)
        {
            at = default;
            TestRefusal why = TestRefusals.Street();
            if (why == TestRefusal.None && (!Settings.Buses || _failed))
            {
                why = TestRefusal.FeatureOff;
            }
            if (why == TestRefusal.None && Riding)
            {
                why = TestRefusal.SceneActive;
            }
            if (why != TestRefusal.None)
            {
                TestRefusals.Tell(Logger, test, why);
                return false;
            }
            TransitMap.Complete();   // a press before the layout has finished: finished now
            if (TransitMap.Stops.Count == 0)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.NoBusLine);
                return false;
            }
            at = PlayerHelper.PlayerController.transform.position;
            return true;
        }

        /// <summary>Options button: dresses the nearest stop now, in view or not, and says where it is.</summary>
        public void TestDress()
        {
            if (!TestReady("Bus stop", out Vector3 at))
            {
                return;
            }
            TransitStop stop = TransitMap.Nearest(at, 600f);
            if (stop == null)
            {
                TestRefusals.Tell(Logger, "Bus stop", TestRefusal.NoBusStop, 600);
                return;
            }
            DressNow(stop, at);
            int metres = Mathf.RoundToInt(Mathf.Sqrt(CitySpots.Flat(stop.Sign - at)));
            BusStopDressing dressed = DressingOf(stop);
            Logger?.Info("Bus stop test: the stop at " + StopName(stop) + " (" + TransitMap.Numbers(stop, ", ") + ", " + (stop.Shelter ? "with room for a shelter" : "pole only")
                         + (stop.Parking ? ", a parking lane between lane and kerb" : "") + ") is dressed, " + metres + " m away"
                         + (dressed != null ? "; " + dressed.BayFound() : "") + "; its check line follows in a moment.");
            Notifications.Show(NotificationType.Info, "reallife_transit_test_dressed",
                new Dictionary<string, string>(2) { { "name", StopName(stop) }, { "metres", metres.ToString() } }, 8f);
        }

        // Dresses a stop here and now, in view or not, taking the dressing furthest from the player if all are out.
        private void DressNow(TransitStop stop, Vector3 at)
        {
            if (Dressed(stop))
            {
                return;
            }
            int slot = 0;
            float furthest = -1f;
            for (int i = 0; i < Dressings; i++)
            {
                float sqr = _dressings[i] == null || _dressings[i].Stop == null ? float.MaxValue : CitySpots.Flat(_dressings[i].Stop.Sign - at);
                if (sqr > furthest)
                {
                    furthest = sqr;
                    slot = i;
                }
            }
            if (_dressings[slot] == null)
            {
                _dressings[slot] = new BusStopDressing(_root, slot + 1);
            }
            if (_seat.At == _dressings[slot])
            {
                _seat.Abort();
            }
            if (_dressings[slot].Stop == _served && _served != null)
            {
                EndService();
            }
            PutDressing(_dressings[slot], stop.At);
            _streetClear = false;
        }

        /// <summary>Options button: brings a bus to the nearest stop now, or says exactly why it cannot.</summary>
        public void TestBus()
        {
            if (!TestReady("Bus", out Vector3 at))
            {
                return;
            }
            // Only a stop near enough to be served: further off its bus would be let go again before it came.
            TransitStop stop = _served ?? NearestBoardable(at, ServeKeep - 5f);
            if (stop == null)
            {
                TestRefusals.Tell(Logger, "Bus", TestRefusal.NoBusStop, Mathf.RoundToInt(ServeKeep - 5f));
                return;
            }
            if (_bus.State != TransitBus.Phase.Idle)
            {
                TestRefusals.Tell(Logger, "Bus", TestRefusal.SceneActive, 0, "a bus is already " + _bus.State + ", " + _bus.Remaining.ToString("F0") + " m from the stop");
                return;
            }
            if (_served != stop)
            {
                if (_served != null)
                {
                    EndService();
                }
                DressNow(stop, at);
                if (!Dressed(stop))
                {
                    TestRefusals.Tell(Logger, "Bus", TestRefusal.NothingFits, 0, "the stop could not be dressed");
                    return;
                }
                BeginService(stop, Time.time);
                _nextWaiter = Time.time + 1f;
            }
            // The first of its lines, in view if it must be (the button is pressed to see it); out of view is tried first.
            _frontCall = 0;
            _pinned[0] = true;
            _callAt[0] = NowMinutes() + BusLead * _minutesPerSecond;
            _eta.NoService();
            TransitStop call = _served.Calls.Count > 0 ? _served.Calls[0] : stop;
            if (!_bus.Send(call, false, out string refusal) && !_bus.Send(call, true, out refusal))
            {
                TestRefusals.Tell(Logger, "Bus", TestRefusal.NothingFits, 0, refusal);
                return;
            }
            _eta.Assign(_bus.Estimate(_pace));
            Assigned(Time.time, true);
            Notifications.Show(NotificationType.Info, "reallife_transit_test_bus", new Dictionary<string, string>(1) { { "name", StopName(stop) } }, 8f);
        }

        /// <summary>
        /// Options button: rides from the nearest stop to the stop two further along its line (or its last; from a line's last
        /// stop, to the nearest stop of another line), fare and all.
        /// </summary>
        public void TestRide()
        {
            if (!TestReady("Bus ride", out Vector3 at))
            {
                return;
            }
            TransitStop from = NearestBoardable(at, 600f);
            if (from == null)
            {
                TestRefusals.Tell(Logger, "Bus ride", TestRefusal.NoBusStop, 600);
                return;
            }
            if (!TransitRide.CanStart())
            {
                TestRefusals.Tell(Logger, "Bus ride", TestRefusal.SceneActive, 0, "a menu, a dialog or another journey is in the way");
                return;
            }
            // Two stops along the line where it has them; from a line's last stop, the nearest stop of another line.
            int direct = TransitTrips.From(from, _trips, NowMinutes(), Calls);
            if (_trips.Count == 0)
            {
                TestRefusals.Tell(Logger, "Bus ride", TestRefusal.NothingFits, 0, "the network has no other stop to ride to");
                return;
            }
            TransitTrip trip = _trips[direct > 0 ? Mathf.Min(1, direct - 1) : 0];
            if (!TransitState.PayBus(out bool pass))
            {
                return;
            }
            StartRide(trip.Board ?? from, trip, pass, "is put on the bus by the test button");
        }

        private static TransitStop NearestBoardable(Vector3 at, float within)
        {
            TransitStop nearest = null;
            float best = within * within;
            List<TransitStop> stops = TransitMap.Places;
            for (int i = 0; i < stops.Count; i++)
            {
                float sqr = CitySpots.Flat(stops[i].Sign - at);
                if (sqr < best)
                {
                    best = sqr;
                    nearest = stops[i];
                }
            }
            return nearest;
        }

        /// <summary>Options button: the subway journey from the nearest station to the next one in the game's list.</summary>
        public void TestSubway()
        {
            _subway.Test();
        }
    }
}
