using System.Collections.Generic;
using BAModAPI;
using GleyTrafficSystem;
using Streets.Pedestrians;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>Traffic sees people: one look at the street, the cars and the people gathered and each car's strip laid against the grid.</summary>
    public sealed partial class SolidStreets : MonoBehaviour
    {
        private const float Level = 3f;              // metres up or down within which a person and a car are on the same road

        // The traffic cars within 90 m that the traffic system itself is driving. Four engine calls for each car out.
        private int GatherCars(Vector3 player, TrafficManager traffic, List<VehicleComponent> pooled)
        {
            int count = 0;
            _game.BeginLook();
            for (int i = 0; i < pooled.Count && count < SolidRules.MostCars; i++)
            {
                VehicleComponent car = pooled[i];
                if (car == null || !car.isActiveAndEnabled || car.rb == null)
                {
                    continue;
                }
                int index = car.GetIndex();
                // (A car the mod drives itself, a response unit or the reckless one, has a driver with eyes of its own.)
                if (index < 0 || index >= _heldSince.Length || traffic.IsVehicleIgnored(index))
                {
                    continue;
                }
                Transform body = _game.At(index, car);
                if (body == null)
                {
                    continue;   // not measured yet, and enough have been at this look
                }
                Vector3 at = body.position;
                float dx = at.x - player.x;
                float dz = at.z - player.z;
                if (dx * dx + dz * dz > SolidRules.Near * SolidRules.Near)
                {
                    continue;
                }
                Vector3 forward = body.forward;
                float flat = Mathf.Sqrt(forward.x * forward.x + forward.z * forward.z);
                if (flat < 0.5f)
                {
                    continue;
                }
                Vector3 velocity = car.rb.velocity;
                _car[count] = car;
                _index[count] = index;
                _cx[count] = at.x;
                _cy[count] = at.y;
                _cz[count] = at.z;
                _fx[count] = forward.x / flat;
                _fz[count] = forward.z / flat;
                _speed[count] = Mathf.Max(0f, (velocity.x * forward.x + velocity.z * forward.z) / flat);
                count++;
            }
            return count;
        }

        private void GatherPeople(Vector3 player, List<Pedestrian> walkers)
        {
            _grid.Begin(player.x, player.z);
            const float near = SolidRules.Near * SolidRules.Near;
            for (int i = walkers != null ? walkers.Count - 1 : -1; i >= 0; i--)
            {
                Pedestrian walker = walkers[i];
                // Switched off: gone, or in somebody's hands (a casualty). The exception is somebody the mod has sent into the road.
                if (walker == null || walker.tpc == null || (!walker.enabled && !Jaywalker.Holds(walker)))
                {
                    continue;
                }
                Vector3 at = walker.transform.position;
                float dx = at.x - player.x;
                float dz = at.z - player.z;
                if (dx * dx + dz * dz > near)
                {
                    continue;
                }
                int person = _grid.Add(at.x, at.z);
                if (person < 0)
                {
                    continue;
                }
                Note(person, Walker, i, at.y);
                var agent = walker.tpc.navmeshAgent;
                if (agent != null && agent.isActiveAndEnabled)
                {
                    Vector3 going = agent.velocity;
                    _vx[person] = going.x;
                    _vz[person] = going.z;
                }
            }
            List<BaseHuman> standing = StreetPeople.Standing(Logger);
            for (int i = standing.Count - 1; i >= 0; i--)
            {
                BaseHuman figure = standing[i];
                if (figure == null || !figure.gameObject.activeInHierarchy)
                {
                    continue;
                }
                Vector3 at = figure.transform.position;
                float dx = at.x - player.x;
                float dz = at.z - player.z;
                if (dx * dx + dz * dz <= near)
                {
                    int person = _grid.Add(at.x, at.z);
                    if (person >= 0)
                    {
                        Note(person, Figure, i, at.y);
                    }
                }
            }
            ActorPool actors = ActorPool.Instance;
            for (int i = actors != null ? actors.ActorCount - 1 : -1; i >= 0; i--)
            {
                StreetActor actor = actors.ActorAt(i);
                if (!actor.InUse || !actor.IsValid || actor.Limp)
                {
                    continue;   // (somebody lying in the road is in the list of bodies below)
                }
                Vector3 at = actor.Position;
                float dx = at.x - player.x;
                float dz = at.z - player.z;
                if (dx * dx + dz * dz <= near)
                {
                    int person = _grid.Add(at.x, at.z);
                    if (person >= 0)
                    {
                        Note(person, Actor, i, at.y);
                    }
                }
            }
            RagdollSystem ragdolls = RagdollSystem.Instance;
            for (int i = 0; ragdolls != null && ragdolls.enabled && i < RagdollSystem.MaxBodies; i++)
            {
                if (ragdolls.TryGetBody(i, out Vector3 at))
                {
                    float dx = at.x - player.x;
                    float dz = at.z - player.z;
                    if (dx * dx + dz * dz <= near)
                    {
                        int person = _grid.Add(at.x, at.z);
                        if (person >= 0)
                        {
                            Note(person, Body, i, at.y);
                            _place[person] = 2;   // wherever a body lies, a car must not drive over it
                        }
                    }
                }
            }
            _grid.Build();
        }

        private void Note(int person, byte kind, int source, float height)
        {
            _kind[person] = kind;
            _source[person] = source;
            _height[person] = height;
            _vx[person] = 0f;
            _vz[person] = 0f;
            _place[person] = 0;
            _reachedIn[person] = float.MaxValue;
            _nearestCar[person] = -1;
        }

        // True if somebody is in the road and not on a pavement: a grid lookup, once a tick for each person found.
        private bool InRoad(int person)
        {
            if (_place[person] == 0)
            {
                var at = new Vector3(_grid.X(person), _height[person], _grid.Z(person));
                _place[person] = HornReactions.PlaceOf(at, out _) == StreetRules.Place.Pavement ? (byte)1 : (byte)2;
            }
            return _place[person] == 2;
        }

        private void Look(int cars, List<Pedestrian> walkers, float now)
        {
            for (int c = 0; c < cars; c++)
            {
                _ahead[c] = -1;
                _aheadBy[c] = float.MaxValue;
                int index = _index[c];
                float speed = _speed[c];
                float halfWidth = _game.HalfWidth(index);
                float halfStrip = SolidRules.HalfStrip(halfWidth);
                float axle = _game.Axle(index);
                float nose = _game.Nose(index);
                int found = _grid.Strip(_cx[c], _cz[c], _fx[c], _fz[c], axle - 0.5f, nose + SolidRules.PersonRadius + SolidRules.SightFor(speed, _game.Gap(index)),
                    halfStrip + SolidRules.StepBand, _found, _foundAhead, _foundSide);
                if (found == 0)
                {
                    continue;
                }
                for (int k = 0; k < found; k++)
                {
                    int person = _found[k];
                    float level = _height[person] - _cy[c];
                    if (level > Level || level < -Level)
                    {
                        continue;   // on a bridge over the road, or under it
                    }
                    float distance = _foundAhead[k] - nose - SolidRules.PersonRadius;
                    float side = _foundSide[k];
                    if (side > halfStrip || side < -halfStrip)
                    {
                        Stepper(c, person, distance, side, halfStrip, walkers, now);
                        continue;
                    }
                    // (Somebody lying in the road takes up more of it than their middle: the whole strip counts for them.)
                    bool moving = _kind[person] == Body || _vx[person] * _vx[person] + _vz[person] * _vz[person] > SolidRules.MovingFrom * SolidRules.MovingFrom;
                    if (distance < -1.5f || !SolidRules.InWay(side, halfWidth, moving) || !InRoad(person))
                    {
                        continue;
                    }
                    if (distance < _aheadBy[c])
                    {
                        _aheadBy[c] = distance;
                        _ahead[c] = person;
                    }
                    float reachedIn = (distance < 0f ? 0f : distance) / (speed < 1f ? 1f : speed);
                    if (reachedIn < _reachedIn[person])
                    {
                        _reachedIn[person] = reachedIn;
                        _nearestCar[person] = c;
                    }
                }
            }
        }

        // Somebody walking into a strip: if the car could no longer stop, they wait at the kerb, unless an accident is owed.
        private void Stepper(int c, int person, float distance, float side, float halfStrip, List<Pedestrian> walkers, float now)
        {
            float speed = _speed[c];
            if (_kind[person] != Walker || distance < 0f || !SolidRules.MustWait(speed, distance))
            {
                return;
            }
            float sideSpeed = _vx[person] * _fz[c] - _vz[person] * _fx[c];
            if (!SolidRules.StepsIn(side, sideSpeed, halfStrip))
            {
                return;
            }
            Pedestrian walker = walkers[_source[person]];
            if (Jaywalker.Holds(walker) || StopsAtItsLine(c) || Owed())
            {
                return;
            }
            // (Where they would be in a second: on the carriageway, or only further along the pavement the strip sweeps?)
            var then = new Vector3(_grid.X(person) + _vx[person] * SolidRules.StepWithin, _height[person], _grid.Z(person) + _vz[person] * SolidRules.StepWithin);
            if (HornReactions.PlaceOf(then, out _) == StreetRules.Place.Pavement)
            {
                return;
            }
            HornReactions reactions = HornReactions.Instance;
            var car = new Vector3(_cx[c], _cy[c], _cz[c]);
            if (reactions != null && reactions.Wait(walker, car, false, SolidRules.WaitFor(speed, distance)) && Meta.Dev.Verbose)
            {
                Quiet();
                Logger?.Info($"Traffic check (person ahead): '{TrafficTools.Name(_car[c])}' at {speed * 3.6f:F0} km/h, somebody about to step out {distance:F0} m ahead, "
                             + $"needs {SolidRules.SafeToStep(speed):F0} m: they wait at the kerb: PASS");
                Loud();
            }
        }

        // True if the car will stop at a stop line ahead (held by the traffic system, or its light red): nobody waits for it.
        private bool StopsAtItsLine(int c)
        {
            return _car[c].GetCurrentAction() == SpecialDriveActionTypes.StopInPoint
                   || HornReactions.LightOf(new Vector3(_cx[c], _cy[c], _cz[c]), new Vector3(_fx[c], 0f, _fz[c])) == StreetRules.Light.Red;
        }

        // True if a pedestrian is owed; the reckless drivers are asked once a tick at most, only when needed.
        private bool Owed()
        {
            if (!_owedAsked)
            {
                _owedAsked = true;
                RecklessDrivers drivers = RecklessDrivers.Instance;
                _owed = (object)_striker == null && drivers != null && drivers.PersonOwed;
            }
            return _owed;
        }
    }
}
