using System;
using System.Globalization;
using Helpers;
using UI.MiniMenu;
using UI.Notification;
using UI.Smartphone;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>The bus director's part for pointing at a stop or the bus, the clicks, and the panel's buttons.</summary>
    public sealed partial class TransitDirector
    {
        // Each dressing gets its two pointers when it is first seen, and the bus's box follows whether a bus stands.
        private void Pointers()
        {
            bool installed = TransitCta.Install();
            for (int i = 0; installed && i < Dressings; i++)
            {
                BusStopDressing dressing = _dressings[i];
                if (dressing == null || _pointed[i])
                {
                    continue;
                }
                _pointed[i] = true;
                TransitPointer.Make(dressing.Sign, TransitPointer.Kind.Stop, dressing, new Vector3(0.25f, 1.45f, 0f), new Vector3(0.7f, 2.9f, 0.35f), true);
                TransitPointer.Make(dressing.Shelter, TransitPointer.Kind.Bench, dressing, new Vector3(0f, 1.25f, 0f),
                    new Vector3(BusStopDressing.ShelterDepth, 2.5f, BusStopDressing.ShelterLength), true);
            }
            BusPointer(_served != null && _bus.State == TransitBus.Phase.Standing && _bus.Vehicle != null);
        }

        // The box round the standing bus: put where the bus has stopped, switched off when it goes. The bus itself is not touched.
        private void BusPointer(bool on)
        {
            if (on == _busPointed)
            {
                return;
            }
            _busPointed = on;
            if (!on)
            {
                if (_busPointer != null)
                {
                    _busPointer.gameObject.SetActive(false);
                }
                return;
            }
            _bus.Vehicle.Footprint(out _busCentre, out _busForward, out _busHalfLength, out _busHalfWidth);
            _busForward.y = 0f;
            _busForward = _busForward.sqrMagnitude > 0.01f ? _busForward.normalized : Vector3.forward;
            if (!TransitCta.Installed)
            {
                return;   // the place is still measured: the panel is offered beside the bus all the same
            }
            if (_busPointer == null)
            {
                _busPointer = TransitPointer.Make(_root, TransitPointer.Kind.Bus, null, Vector3.zero, Vector3.one, false);
                if (_busPointer == null)
                {
                    return;
                }
                _busBox = _busPointer.GetComponent<BoxCollider>();
            }
            _busPointer.transform.SetPositionAndRotation(new Vector3(_busCentre.x, _bus.Vehicle.transform.position.y + 1.4f, _busCentre.z),
                Quaternion.LookRotation(_busForward, Vector3.up));
            _busBox.size = new Vector3(_busHalfWidth * 2f + 0.2f, 2.8f, _busHalfLength * 2f + 0.2f);
            _busPointer.gameObject.SetActive(true);
        }

        /// <summary>
        /// What the game's pointer is offered on one of the buses' pointers: a locale key, or null for nothing. Asked when the
        /// pointer comes onto the object and when it is clicked, never per frame.
        /// </summary>
        public static string Offer(TransitPointer pointer)
        {
            TransitDirector director = Instance;
            return director != null && pointer != null ? director.PointerOffer(pointer) : null;
        }

        private string PointerOffer(TransitPointer pointer)
        {
            if (_failed || !Settings.Buses || _ride.Riding || _rider.Active || _seat.Active || (_list != null && _list.IsOpen) || (_picker != null && _picker.IsOpen)
                || PlayerHelper.IsUsingVehicle || BuildingManager.IsInsideBuilding)
            {
                return null;
            }
            if (pointer.What == TransitPointer.Kind.Bus)
            {
                return _served != null && _bus.State == TransitBus.Phase.Standing ? TransitCta.Board : null;
            }
            BusStopDressing dressing = pointer.Dressing;
            if (dressing == null || dressing.Stop == null)
            {
                return null;
            }
            if (pointer.What == TransitPointer.Kind.Bench && _bus.State != TransitBus.Phase.Standing && TransitSeat.Possible
                && dressing.FreeSeat(PlayerHelper.GetPosition(), 1000f) >= 0)
            {
                return TransitCta.Sit;
            }
            return TransitCta.Choose;
        }

        /// <summary>The game ran the click on one of the buses' pointers.</summary>
        internal void PointerClicked(TransitPointer pointer)
        {
            try
            {
                string offer = PointerOffer(pointer);
                var game = InstanceBehavior<GameManager>.Instance;
                PlayerController player = game != null ? game.playerController : null;
                if (offer == null || player == null)
                {
                    return;
                }
                if (_onChoose == null)
                {
                    Wire();
                }
                Vector3 at = player.transform.position;
                if (offer == TransitCta.Sit)
                {
                    BusStopDressing shelter = pointer.Dressing;
                    int seat = shelter.FreeSeat(at, 1000f);
                    if (seat < 0)
                    {
                        return;
                    }
                    Vector3 feet = shelter.SeatFeetAt(seat);
                    float away = Mathf.Sqrt(CitySpots.Flat(feet - at));
                    Logger?.Info("Transit check (pointer): the player clicks the bench at " + StopLabel(shelter.Stop) + ", " + away.ToString("F1", CultureInfo.InvariantCulture)
                                 + " m from seat " + (seat + 1) + ": " + (away <= SitRange ? "they sit down" : "the game walks them to it first") + ": PASS.");
                    if (away <= SitRange)
                    {
                        SitDown(shelter, seat);
                    }
                    else
                    {
                        _goStop = null;
                        _goShelter = shelter;
                        _goSeat = seat;
                        player.SetGoal(feet, Guard.Listener(_sitOnArrival));
                    }
                    return;
                }
                bool bus = pointer.What == TransitPointer.Kind.Bus;
                TransitStop stop = bus ? _served : pointer.Dressing.Stop;
                if (stop == null)
                {
                    return;
                }
                bool here = AtStop(stop, at) || (stop == _served && _bus.State == TransitBus.Phase.Standing && AtBus(at));
                Logger?.Info("Transit check (pointer): the player clicks "
                             + (bus ? "the bus standing at " : pointer.What == TransitPointer.Kind.Bench ? "the shelter of " : "the pole of ") + StopLabel(stop) + ", "
                             + Mathf.Sqrt(CitySpots.Flat(stop.Sign - at)).ToString("F1", CultureInfo.InvariantCulture) + " m from the pole: "
                             + (here ? "the list of stops is opened" : "the game walks them to the stop first") + ": PASS.");
                if (here)
                {
                    ChooseAt(stop);
                }
                else
                {
                    _goShelter = null;
                    _goStop = stop;
                    player.SetGoal(stop.Wait, Guard.Listener(_chooseOnArrival));
                }
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        // The walk a click started has ended at the stop.
        private void ChooseOnArrival()
        {
            try
            {
                TransitStop stop = _goStop;
                _goStop = null;
                if (stop == null || _failed || !Settings.Buses || _ride.Riding || PlayerHelper.IsUsingVehicle)
                {
                    return;
                }
                Vector3 at = PlayerHelper.GetPosition();
                if (!AtStop(stop, at))
                {
                    Logger?.Info("Transit check (pointer): the walk to " + StopLabel(stop) + " ended " + Mathf.Sqrt(CitySpots.Flat(stop.Wait - at)).ToString("F1", CultureInfo.InvariantCulture)
                                 + " m from its waiting place: FAIL (nothing is opened; the panel is offered within " + Reach.ToString("F0") + " m).");
                    return;
                }
                ChooseAt(stop);
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        private void SitOnArrival()
        {
            try
            {
                BusStopDressing shelter = _goShelter;
                int seat = _goSeat;
                _goShelter = null;
                _goSeat = -1;
                if (shelter == null || shelter.Stop == null || _failed || _ride.Riding || _seat.Active || !shelter.SeatFree(seat)
                    || CitySpots.Flat(shelter.SeatFeetAt(seat) - PlayerHelper.GetPosition()) > SitRange * SitRange)
                {
                    return;
                }
                SitDown(shelter, seat);
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        // The list at a dressed stop the player stands at, which becomes the stop served if it was not.
        private void ChooseAt(TransitStop stop)
        {
            stop = stop.At;
            if (_served != stop)
            {
                if (!Dressed(stop))
                {
                    return;
                }
                if (_served != null)
                {
                    EndService();
                }
                BeginService(stop, Time.time);
            }
            OpenList();
        }

        private void ChooseClicked()
        {
            try
            {
                _keyFrame = Time.frameCount;   // the key that pressed the button does nothing else this frame
                if (_served != null && !_ride.Riding && !_seat.Active)
                {
                    OpenList();
                }
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        private void SitClicked()
        {
            try
            {
                _keyFrame = Time.frameCount;
                if (_sitAt != null && !_ride.Riding)
                {
                    SitDown(_sitAt, _sitSeat);
                }
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        private void StandClicked()
        {
            try
            {
                _keyFrame = Time.frameCount;
                if (_seat.Seated)
                {
                    _seat.Stand(Time.time);
                    _panelKey = int.MinValue;
                }
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        private void SitDown(BusStopDressing shelter, int seat)
        {
            bool sat = _seat.Sit(shelter, seat, out string why);
            Logger?.Info("Transit check (seat): the player sits down on seat " + (seat + 1) + " of " + BusStopDressing.Seats + " at " + StopLabel(shelter.Stop) + ", seat "
                         + BusStopDressing.BenchSeat.ToString("F2", CultureInfo.InvariantCulture) + " m up as the clips are made for"
                         + (sat ? "; feet " + _seat.FeetOff.ToString("F2", CultureInfo.InvariantCulture) + " m from where the clip wants them: "
                                  + (_seat.FeetOff < 0.12f ? "PASS" : "FAIL (the mesh does not reach the seat's place)")
                             : ": FAIL (" + why + ")") + ".");
            _sitAt = null;
            _sitSeat = -1;
            _panelKey = int.MinValue;
        }
    }
}
