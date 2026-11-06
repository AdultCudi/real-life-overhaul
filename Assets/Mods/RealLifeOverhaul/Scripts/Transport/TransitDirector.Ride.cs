using System;
using System.Collections.Generic;
using System.Globalization;
using Helpers;
using UI.MiniMenu;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>The bus director's part for the ride: boarding, the ride under the fade, and what BusRider tells it on the way.</summary>
    public sealed partial class TransitDirector
    {
        // The ride begins, seen where it can be (BusRider), else under the fade at the stop. False if it cannot, or the fare is short.
        private bool Board(TransitTrip trip, string how)
        {
            TransitStop to = trip.To;
            TransitStop from = _bus.Stop ?? _served;
            if (_served == null || to == null || to.At == _served || !TransitRide.CanStart() || _bus.State != TransitBus.Phase.Standing || _rider.Active)
            {
                return false;
            }
            // The bus's own line: to the change where the journey has one, else the stop picked.
            bool planned = trip.Planned && trip.Board.Line == from.Line;
            TransitStop leg = planned && trip.Changes ? trip.ChangeOff : planned ? trip.To : to;
            if (planned && trip.Changes)
            {
                _plan = trip;
                _hasPlan = true;
                _planLeg = 1;
            }
            else if (!_changing)
            {
                DropPlan();
            }
            string why = !_doorFound ? "there is no place on the road at its front door to step in from" : null;
            if (why == null && !BusRider.Possible(_bus.Vehicle))
            {
                why = _bus.Vehicle != null && _bus.Vehicle.HasInside ? "the clips for it cannot be played" : "the props are older than the bus's inside";
            }
            if (why == null)
            {
                _seat.Abort();
                if (_rider.Board(_bus, from, leg, _door, _busFull, out why))
                {
                    _rideTo = leg;
                    HideHud();
                    _changing = false;
                    return true;
                }
                if (_rider.RefusedFare)
                {
                    return false;   // the game has said why: the fare is more than they have
                }
            }
            Logger?.Info("Transit check (boarding): the player " + how + " but is not walked onto the bus (" + why + "): the ride is the fade at the stop: FAIL.");
            if (!TransitState.PayBus(out bool pass))
            {
                Logger?.Info("Transit check (ride): the player " + how + " but cannot pay the fare of " + TransitState.BusFare.ToString("F2", CultureInfo.InvariantCulture)
                             + ": nobody boards: FAIL.");
                return false;
            }
            _changing = false;
            StartRide(from, trip, pass, how + " while the bus stands (" + _bus.Standing.ToString("F0") + " s)");
            return true;
        }

        // The ride under the fade at the stop: the whole journey, its change included.
        private void StartRide(TransitStop from, TransitTrip picked, bool pass, string how)
        {
            TransitStop to = picked.To;
            TransitTrip trip = TransitTrips.Between(from, to);
            float minutes = picked.Planned ? TransitTrips.RestMinutes(picked, from) : trip.Minutes;
            DropPlan();
            TransitLine line = TransitMap.Lines[from.Line];
            _rideTo = to;
            _player = PlayerStep.Riding;
            _seat.Abort();
            HideHud();
            TapCard();
            TransitState.BusRides++;
            bool sound = to != from && minutes >= 1f;
            string paid = pass ? "on a day pass" : "fare " + TransitState.BusFare.ToString("F2", CultureInfo.InvariantCulture);
            if (trip.Direct)
            {
                _text.Length = 0;
                for (int i = from.Order + 1; i <= to.Order && i < line.Stops.Count; i++)
                {
                    _text.Append(i > from.Order + 1 ? ", " : "").Append(line.Stops[i].Name ?? "stop " + (i + 1));
                }
                Logger?.Info("Transit check (ride): the player " + how + " and rides the " + line.Number + " from " + (from.Name ?? "stop " + (from.Order + 1)) + ", calling at " + _text
                             + ": " + (to.Order - from.Order) + " stop(s), " + trip.Metres.ToString("F0") + " m, " + minutes.ToString("F0") + " game minutes, "
                             + paid + ": " + (sound ? "PASS" : "FAIL (not a ride to another stop)") + ".");
            }
            else
            {
                Logger?.Info("Transit check (ride): the player " + how + " and rides from " + StopLabel(from) + " to " + StopLabel(to) + " with a change of bus: about "
                             + trip.Metres.ToString("F0") + " m of road (the straight line times " + TransitTrips.RoadFactor.ToString("F2", CultureInfo.InvariantCulture) + "), "
                             + minutes.ToString("F0") + " game minutes with " + TransitTrips.ChangeMinutes.ToString("F0") + " for the change, " + paid + ": "
                             + (sound ? "PASS" : "FAIL (not a ride to another stop)") + ".");
            }
            _ride.Begin(minutes, to.Wait, "reallife_transit_ride_fade", RideFaded, RideArrived, arriveAboard: _arriveAboard ?? (_arriveAboard = ArriveAboard));
        }

        private void DropPlan()
        {
            _hasPlan = false;
            _planLeg = 0;
            _changing = false;
            _plan = default(TransitTrip);
        }

        private Func<bool> _arriveAboard;
        private Action _atBlack;

        // At the far end under the fade: in the bus that stands at the stop (BusRider), if one can.
        private bool ArriveAboard()
        {
            return _rideTo != null && Settings.Buses && !_failed && _rider.ArriveAboard(_seenOff, _rideTo);
        }

        /// <summary>True while somebody steps down by the middle door: a rider boarding there waits.</summary>
        internal bool MiddleDoorBusy => _alighter.Step == RiderStep.Exiting;

        internal bool RideRunning => _ride.Riding;   // the ride's time is passing under its fade

        /// <summary>The player has reached the front door and paid: they step in.</summary>
        internal void RiderBoards(TransitStop from, TransitStop to, bool pass)
        {
            Logger?.Info("Transit check (boarding): the player is at the front door of the bus at " + StopLabel(from) + " for " + StopLabel(to) + " and "
                         + (pass ? "shows the day pass" : "pays the fare of " + TransitState.BusFare.ToString("F2", CultureInfo.InvariantCulture)) + ": PASS.");
            _player = PlayerStep.Riding;
        }

        /// <summary>The bus has been seen pulling away with the player in it: the ride passes under a fade.</summary>
        internal void RiderRides(TransitStop from, TransitStop to)
        {
            TransitTrip trip = TransitTrips.Between(from, to);
            if (_hasPlan && _planLeg == 1 && to == _plan.ChangeOff)
            {
                // The first bus of a journey with a change is not driven: the whole journey passes under the fade.
                Logger?.Info("Transit check (journey): the first bus to the change at " + StopLabel(_plan.ChangeOff) + " is not driven, so the whole journey to "
                             + StopLabel(_plan.To) + " passes under the fade, its change included: PASS.");
                to = _plan.To;
                trip.Minutes = TransitTrips.RestMinutes(_plan, from);
                trip.Direct = false;
                DropPlan();
            }
            _rideTo = to;
            TransitState.BusRides++;
            Logger?.Info("Transit check (ride): the player rides from " + StopLabel(from) + " to " + StopLabel(to) + (trip.Direct ? " on the same bus" : " with a change of bus")
                         + ": " + trip.Metres.ToString("F0") + " m, " + trip.Minutes.ToString("F0") + " game minutes under the fade: "
                         + (to != from && trip.Minutes >= 1f ? "PASS" : "FAIL (not a ride to another stop)") + ".");
            _ride.Begin(trip.Minutes, to.Wait, "reallife_transit_ride_fade", RideFaded, RideArrived, tap: false, atBlack: _atBlack ?? (_atBlack = _rider.AtBlack),
                arriveAboard: _arriveAboard ?? (_arriveAboard = ArriveAboard));
        }

        /// <summary>The player is off a bus: at the far end it goes once they are clear; at the start they are at the stop again.</summary>
        internal void RiderOff(TransitBus bus, bool arriving)
        {
            if (arriving)
            {
                _offBus = bus;
                _offAt = Time.time;
                if (_hasPlan && _planLeg == 1 && bus != null && bus.Stop == _plan.ChangeOff)
                {
                    BeginChange();
                }
                else if (_hasPlan)
                {
                    DropPlan();
                }
                return;
            }
            _player = PlayerStep.None;
            _panelKey = int.MinValue;
        }

        /// <summary>Gives a fare back: the ride did not happen (got off again, ended early, a mid-ride save loaded).</summary>
        internal void RiderFareBack(float fare, string why)
        {
            if (fare <= 0f)
            {
                return;
            }
            bool given = RealLifeOverhaul.Economy.BizBooks.Book(fare, new TransactionInfo("reallife_transaction_bus_fare_back"), null, null, force: true, showNotification: true);
            Notifications.Show(NotificationType.Info, "reallife_transit_fare_back", new Dictionary<string, string>(1) { { "fare", TransitText.Money(fare) } }, 6f);
            Logger?.Info("Transit check (fare back): " + why + ": the fare of " + fare.ToString("F2", CultureInfo.InvariantCulture) + " is given back: "
                + (given ? "PASS" : "FAIL (the game would not pay it)") + ".");
        }

        // The bus the player left at the far end goes once they are clear of its door, or after a few seconds.
        private void SeeOff(float now)
        {
            TransitBus bus = _offBus;
            if (bus == null || bus.State != TransitBus.Phase.Standing || bus.Vehicle == null)
            {
                _offBus = null;
                return;
            }
            bus.MiddleDoorPlace(out Vector3 door, out _);
            float clear = Mathf.Sqrt(CitySpots.Flat(PlayerHelper.GetPosition() - door));
            if (clear < 1.6f && now - _offAt < 3f)
            {
                return;
            }
            _offBus = null;
            bus.Depart();
            Logger?.Info("Transit check (departure): the bus the player got off closes its doors and leaves " + StopLabel(bus.Stop) + ", the player "
                         + clear.ToString("F1", CultureInfo.InvariantCulture) + " m from its door: PASS.");
        }

        internal static void TapSound()
        {
            try
            {
                Beep();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        private static void Beep()
        {
            var game = InstanceBehavior<GameManager>.Instance;
            SfxManager sounds = InstanceBehavior<SfxManager>.Instance;
            if (sounds != null && game != null && game.playerController != null)
            {
                sounds.PlayAudio(BigAmbitions.SoundSystem.SoundType.PurchaseSuccess, game.playerController.transform.position);
            }
        }

        private void RideFaded()
        {
            // Nobody can see: the stop left is cleared, and the stop they get off at made ready.
            ClearStreet(true);
            _streetClear = false;
        }

        private void RideArrived()
        {
            TransitStop to = _rideTo;
            _rideTo = null;
            DropPlan();
            if (to == null)
            {
                return;
            }
            DressNear(to.Sign);
            ModState.AddModifier(ModState.MoodUp);
            Notifications.Show(NotificationType.Info, "reallife_transit_ride_arrived", new Dictionary<string, string>(1) { { "name", StopName(to) } }, 6f);
            if (_rider.ArrivalBus != null)
            {
                Logger?.Info("Transit check (arrival aboard): the bus stands at " + StopLabel(to) + (_rider.ArrivalBus.InBay ? " in its bay" : " in the lane") + " with the player "
                             + (_rider.Now == BusRider.Step.Arrived ? "in it" : "") + " as the screen clears; they get off by the middle door: PASS.");
                return;
            }
            // The bus that brought them pulls away, put on the lane while the screen is still dark.
            _seeOff = to;
            _seeOffBy = Time.time + 1.2f;
            Vector3 at = PlayerHelper.GetPosition();
            float off = Mathf.Sqrt(CitySpots.Flat(at - to.Wait));
            bool onPavement = CitySpots.Pavement(at, 0.3f, out _);
            Logger?.Info("Transit check (put-down): the player stands " + off.ToString("F1", CultureInfo.InvariantCulture) + " m from the waiting place of " + StopLabel(to)
                         + ", on the pavement: " + (onPavement ? "yes" : "no") + ", the stop dressed: " + (Dressed(to) ? "yes" : "no") + ": "
                         + (off < 3f && onPavement ? "PASS" : "FAIL") + ".");
        }

        // The card on the reader: the upper body only, over whatever the legs are doing.
        private static void TapCard()
        {
            try
            {
                var game = InstanceBehavior<GameManager>.Instance;
                Animator animator = game != null && game.playerController != null && game.playerController.Character != null
                    ? game.playerController.Character.animator : null;
                if (animator != null && ClipLibrary.TryGet("pay_tap", out CityClip tap))
                {
                    ActivityAnimator player = ActivityAnimators.For(animator);
                    if (player != null && player.Usable)
                    {
                        player.PlayOver(tap.Clip, BodyMask.Upper, 0.15f);
                    }
                }
                Beep();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }
    }
}
