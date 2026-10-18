using System.Globalization;
using UI;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>The bus rider's part for the ride: the pull-away, the ride through the city or under the fade, and the far end.</summary>
    public sealed partial class BusRider
    {
        private float _shutSince = -1f;
        private float _movingSince = -1f;

        /// <summary>The director has shut the doors: no getting off now; the pull-away is watched.</summary>
        public void Departing()
        {
            _departing = true;
            _stage = BusInside.Stage.Leaving;
            _shutSince = -1f;
            _movingSince = -1f;
            if (_step == Step.Seated || _step == Step.Holding)
            {
                _step = Step.Leaving;
            }
            // Still on their way to it, they sit down or take the strap as the bus moves off.
        }

        private void Leave(float now, BusVehicle bus)
        {
            if (bus == null)
            {
                return;
            }
            if (_shutSince < 0f)
            {
                if (bus.DoorsShut)
                {
                    _shutSince = now;
                    _shutAt = bus.transform.position;
                    // The ride in real time where the line can be driven to their stop, else the bus is seen pulling away.
                    if (_director.JourneyBegins(_bus, _from, _to))
                    {
                        _step = Step.Riding;
                        _stage = BusInside.Stage.Riding;
                        _since = now;
                    }
                }
                return;
            }
            float metres = Mathf.Sqrt(CitySpots.Flat(bus.transform.position - _shutAt));
            if (_movingSince < 0f && metres > 0.3f)
            {
                _movingSince = now;
            }
            if (BusInside.FadeNow(now - _shutSince, _movingSince < 0f ? 0f : now - _movingSince, metres))
            {
                Logger?.Info("Transit check (ride): the doors shut and the bus pulls away with the player in it: " + metres.ToString("F0") + " m in "
                             + (now - _shutSince).ToString("F1", CultureInfo.InvariantCulture)
                             + " s are seen before the time of the ride passes: " + (metres >= 5f ? "PASS" : "FAIL (it was held where it stood)") + ".");
                _step = Step.Fading;
                _since = now;
                _director.RiderRides(_from, _to);
            }
        }

        /// <summary>Riding through the city: the bus has called at a stop and left it, so a save now has them there.</summary>
        public void PassedStop(TransitStop stop)
        {
            if (_stage == BusInside.Stage.Riding && stop != null)
            {
                _passed = stop;
                _passedCount++;
            }
        }

        /// <summary>Riding through the city: the bus stands at their stop with its doors open, and they get off. False if not riding it.</summary>
        public bool GetOffHere(TransitBus bus, TransitStop stop)
        {
            if (!RidingThrough || bus == null || bus != _bus || stop == null || bus.Vehicle == null)
            {
                return false;
            }
            _to = stop;
            _stage = BusInside.Stage.Arriving;
            _step = Step.Arrived;
            _since = Time.time;
            _until = float.MaxValue;
            _fare = 0f;
            return true;
        }

        /// <summary>Riding through the city: the rest of the ride passes under the fade from here.</summary>
        public void SkipRest()
        {
            if (_step == Step.Riding)
            {
                _step = Step.Fading;
                _since = Time.time;
            }
        }

        /// <summary>TransitRide: the screen is black; they are taken off the moving bus to the stop they left, out of the world.</summary>
        public void AtBlack()
        {
            if (_step != Step.Fading && _step != Step.Leaving && _step != Step.Riding)
            {
                return;
            }
            Release(SafeSpot());
            _step = Step.Away;
            _stage = BusInside.Stage.Away;
        }

        /// <summary>
        /// TransitRide at the far end, the screen still black: a bus standing at the stop with them in their seat or on the strap.
        /// False, with nothing changed, if no bus can stand there now.
        /// </summary>
        public bool ArriveAboard(TransitBus bus, TransitStop to)
        {
            var game = InstanceBehavior<GameManager>.Instance;
            PlayerController player = game != null ? game.playerController : null;
            ThirdPersonCharacter body = player != null ? player.Character : null;
            if (bus == null || to == null || body == null || body.animator == null || (_step != Step.Away && _step != Step.None))
            {
                Clear();
                return false;
            }
            if (_step == Step.None)
            {
                _place = BusInside.Place.Seat;   // the test button's ride: nobody boarded, so nobody chose
                _from = null;
            }
            if (!bus.PutStanding(to, out string refusal))
            {
                Logger?.Info("Transit check (arrival aboard): no bus can stand at " + Name(to) + " now (" + refusal + "): the player is put down on the pavement under the fade: FAIL.");
                Clear();
                return false;
            }
            BusVehicle vehicle = bus.Vehicle;
            bus.MiddleDoorPlace(out Vector3 door, out _);
            ActivityAnimator clips = ActivityAnimators.For(body.animator);
            if (!Possible(vehicle) || clips == null || !clips.Usable || !Ground(door, vehicle, out _))
            {
                Logger?.Info("Transit check (arrival aboard): the bus stands at " + Name(to) + " but "
                             + (!Possible(vehicle) ? "cannot be seen into or the clips are not there" : "nothing is free beside its middle door to step down to")
                             + ": the player is put down on the pavement under the fade: FAIL.");
                bus.Release(remove: true);
                Clear();
                return false;
            }
            _player = player;
            _body = body;
            _clips = clips;
            _pace = GamePace();
            _bus = bus;
            _to = to;
            if (_from == null)
            {
                _from = to;   // the test button's ride: nobody boarded
            }
            _fare = 0f;
            _gotOff = false;
            _departing = false;
            _stage = BusInside.Stage.Arriving;
            Hold();
            if (!_blocked)
            {
                _player.SetNavigationBlocker(Blocker);
                _blocked = true;
            }
            bool standing = _place == BusInside.Place.Strap;
            _at = BusInside.At(_place);
            _facing = 0f;
            _up = 0f;
            Put(vehicle);
            if (!Play(standing ? "strap_hang" : "sit_wait", 0f, ClipEnd.Return))
            {
                Release(SafeSpot());
                bus.Release(remove: true);
                Clear();
                return false;
            }
            _clips.Evaluate(0f);
            _step = Step.Arrived;
            _since = Time.time;
            _until = float.MaxValue;
            return true;
        }

        // The screen clears: the doors open, and they get up and off.
        private void ArrivedFrame(float now, BusVehicle bus)
        {
            if (UiFader.isFading)
            {
                return;
            }
            if (_until == float.MaxValue)
            {
                bus.Doors(true);
                _until = now + 0.9f;   // they stand as the doors finish opening
                return;
            }
            if (now < _until)
            {
                return;
            }
            if (_place == BusInside.Place.Strap)
            {
                _clips.Stop(0.3f);
                _step = Step.Holding;
                WayOut(now);
                return;
            }
            _step = Step.Seated;
            Up(now);
        }
    }
}
