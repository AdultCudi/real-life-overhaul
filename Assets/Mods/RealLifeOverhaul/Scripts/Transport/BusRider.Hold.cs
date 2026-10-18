using System;
using GleyTrafficSystem;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul
{
    /// <summary>The bus rider's part for carrying the player on the bus and letting go of them, and every way a ride ends.</summary>
    public sealed partial class BusRider
    {
        private Transform _camera;          // the bus the camera is kept inside of
        private bool _bodyPaused;

        // On the bus: carried by it from now on, but not parented to it.
        private void Hold()
        {
            if (_held)
            {
                return;
            }
            NavMeshAgent agent = _body.navmeshAgent;
            _player.ResetNavigation();
            if (agent != null)
            {
                agent.enabled = false;
            }
            _body.isKinematic = true;
            if (_body.characterRigidbody != null)
            {
                _body.characterRigidbody.isKinematic = true;
            }
            if (_body.capsuleCollider != null)
            {
                _body.capsuleCollider.enabled = false;
                if (TrafficManager.HasInstance && TrafficManager.IsInitialized)
                {
                    Manager.TriggerColliderRemovedEvent(_body.capsuleCollider);
                }
            }
            _body.enabled = false;      // its Update would stand the walk still every frame: the walk is ours to give it (Move)
            _bodyPaused = true;
            _held = true;
            BusVehicle bus = _bus != null ? _bus.Vehicle : null;
            if (bus != null)
            {
                // The third-person camera keeps to the inside of the bus.
                _camera = bus.Shell;
                ThirdPersonCamera.Confine(_camera, new Vector3(0f, BusInside.Floor + 1f, 0f), new Vector3(BusInside.Wall - 0.1f, 1f, 5.15f));
            }
        }

        // Back on the ground on the navigation mesh, everything of theirs as it was.
        private void Unhold(Vector3 ground)
        {
            if (!_held)
            {
                return;
            }
            _held = false;
            if (_camera != null)
            {
                ThirdPersonCamera.Unconfine(_camera);
                _camera = null;
            }
            try
            {
                _body.Move(Vector3.zero);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            if (_bodyPaused)
            {
                _body.enabled = true;
                _bodyPaused = false;
            }
            _player.transform.position = ground;
            NavMeshAgent agent = _body.navmeshAgent;
            if (agent != null)
            {
                agent.enabled = true;
                if (agent.isOnNavMesh)
                {
                    agent.Warp(ground);
                }
            }
            if (_body.capsuleCollider != null)
            {
                _body.capsuleCollider.enabled = true;
            }
            if (_body.characterRigidbody != null)
            {
                _body.characterRigidbody.isKinematic = false;
            }
            _body.isKinematic = false;
            _body.Reset();
            if (_bus != null && _bus.Vehicle != null)
            {
                _bus.Vehicle.ShowRoof(true);
            }
        }

        private void Put(BusVehicle bus)
        {
            Vector3 at = bus.OnFloor(_at, _up);
            _player.transform.SetPositionAndRotation(at, bus.Facing(_facing));
            _lastPut = at;
        }

        // A point on the navigation mesh by the bus, clear of every vehicle but the bus.
        private static bool Ground(Vector3 want, BusVehicle bus, out Vector3 ground)
        {
            ground = want;
            if (!NavMesh.SamplePosition(want, out NavMeshHit hit, 0.6f, NavMesh.AllAreas) || CitySpots.Flat(hit.position - want) > 0.45f * 0.45f)
            {
                return false;
            }
            ground = hit.position;
            return JobSpots.Clear(ground, bus != null ? bus.transform : null);
        }

        /// <summary>Where the player is if a save is written now or the ride ends early: the stop boarded at, or got off at once the ride's time has passed.</summary>
        public Vector3 SafeSpot()
        {
            TransitStop stop = _stage == BusInside.Stage.Arriving ? _to : _passed ?? _from;
            Vector3 want = stop != null ? stop.Wait : (_player != null ? _player.transform.position : Vector3.zero);
            return NavMesh.SamplePosition(want, out NavMeshHit hit, 3f, NavMesh.AllAreas) ? hit.position : want;
        }

        /// <summary>For a save: the player's place in it, and a fare to give back when loaded. False when not aboard.</summary>
        public bool ForSave(out Vector3 place, out float fareBack)
        {
            place = Vector3.zero;
            fareBack = 0f;
            if (!Aboard)
            {
                return false;
            }
            place = SafeSpot();
            fareBack = BusInside.SaveFareBack(_stage, _passedCount) ? _fare : 0f;
            return true;
        }

        /// <summary>The walk to the door stopped short: nothing was paid.</summary>
        public void Cancel(string why)
        {
            if (_step == Step.ToDoor)
            {
                Finish(why, put: false);
            }
        }

        /// <summary>Undoes everything at once: the player on the ground (with <paramref name="put"/> beside the bus), the fare back if due. Never throws.</summary>
        public void Abort(string why, bool put = true)
        {
            if (_step == Step.None)
            {
                return;
            }
            if (_step == Step.Away)
            {
                return;   // the ride's own routine has the player while its time passes; the director clears this when it ends
            }
            Logger?.Info("Transit check (ride ended early): " + why + " (" + _step + "): the player is given back on the ground"
                         + (_fare > 0f && BusInside.FareBackOnCancel(_stage) ? ", the fare with them" : "") + ".");
            GiveBack(why);
            Finish(why, put);
        }

        private void GiveBack(string why)
        {
            if (_fare > 0f && BusInside.FareBackOnCancel(_stage))
            {
                float fare = _fare;
                _fare = 0f;
                _director.RiderFareBack(fare, why);
            }
        }

        // Ends it: the player let go of beside the door or at the stop, the blocker off, the clips handed back.
        private void Finish(string why, bool put)
        {
            try
            {
                // A walk to the door the game is still making goes on; at its end nothing boards.
                if (_held)
                {
                    // Riding through the city the bus may be anywhere in the traffic: to the last stop it called at.
                    Release(_stage == BusInside.Stage.Riding ? SafeSpot() : put ? Beside() : _player != null ? Nearest(_player.transform.position) : Vector3.zero);
                }
                if (_clips != null && _clips.Active)
                {
                    _clips.Stop(0.2f);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            Clear();
        }

        // Off the bus onto the ground, the clips handed back.
        private void Release(Vector3 ground)
        {
            if (_clips != null && _clips.Active)
            {
                _clips.Dispose();
            }
            Unhold(ground);
            if (_blocked && _player != null)
            {
                _player.UnsetNavigationBlocker(Blocker);
                _blocked = false;
            }
        }

        private Vector3 Beside()
        {
            BusVehicle bus = _bus != null ? _bus.Vehicle : null;
            if (bus != null && bus.Out)
            {
                BusInside.Spot to = _at.Z > 3.7f ? BusInside.FrontExitTo : BusInside.ExitTo;
                if (Ground(bus.transform.TransformPoint(new Vector3(to.X, 0f, to.Z)), bus, out Vector3 ground))
                {
                    return ground;
                }
            }
            return SafeSpot();
        }

        private static Vector3 Nearest(Vector3 at)
        {
            return NavMesh.SamplePosition(at, out NavMeshHit hit, 3f, NavMesh.AllAreas) ? hit.position : at;
        }

        private void Clear()
        {
            try
            {
                if (_held)
                {
                    Unhold(SafeSpot());
                }
                if (_blocked && _player != null)
                {
                    _player.UnsetNavigationBlocker(Blocker);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            _blocked = false;
            _held = false;
            _step = Step.None;
            _stage = BusInside.Stage.None;
            _bus = null;
            _departing = false;
            _gotOff = false;
            _fare = 0f;
            _middleIn = false;
            _offChosen = false;
            _passed = null;
            _passedCount = 0;
            _path.Clear();
            _director.RiderCleared();
        }

        /// <summary>The ride's time has passed with nothing at the far end for them: the ride is over.</summary>
        public void Done()
        {
            if (_step == Step.Away)
            {
                Clear();
            }
        }
    }
}
