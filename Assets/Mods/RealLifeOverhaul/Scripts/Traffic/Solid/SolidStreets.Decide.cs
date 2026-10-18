using System.Collections.Generic;
using BAModAPI;
using GleyTrafficSystem;
using Streets.Pedestrians;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>Traffic sees people: what each car does about whoever is ahead of it, and the stand-ins shared out.</summary>
    public sealed partial class SolidStreets : MonoBehaviour
    {
        private const float GlanceFor = 0.6f;        // seconds a car stays watched after it was last within a second of somebody
        private const float StrikerFor = 3f;         // seconds a car that is about to hit somebody stays the one, unless it is named again

        private void Decide(int cars, List<Pedestrian> walkers, float now)
        {
            _brakes.Begin();
            CarStrikes strikes = CarStrikes.Instance;
            HornReactions reactions = HornReactions.Instance;
            bool verbose = Meta.Dev.Verbose;
            for (int c = 0; c < cars; c++)
            {
                int index = _index[c];
                int person = _ahead[c];
                if (person < 0)
                {
                    _heldSince[index] = 0f;
                    _movedOn[index] = false;
                    _judged[index] = (byte)SolidRules.Driver.Clear;
                    continue;
                }
                VehicleComponent car = _car[c];
                float speed = _speed[c];
                float distance = _aheadBy[c];
                float gap = _game.Gap(index);
                float nose = _game.Nose(index);
                Pedestrian walker = _kind[person] == Walker ? walkers[_source[person]] : null;
                bool hears = _kind[person] == Walker || _kind[person] == Figure;   // nobody sounds a horn at a body in the road, or at a crew at work
                var judged = SolidRules.Judge(speed, distance, gap);
                var before = (SolidRules.Driver)_judged[index];
                _judged[index] = (byte)judged;
                var carAt = new Vector3(_cx[c], _cy[c], _cz[c]);
                var facing = new Vector3(_fx[c], 0f, _fz[c]);
                bool first = _nearestCar[person] == c;   // of the cars that have this person ahead, the one that reaches them first

                // Is this the one car that may reach somebody? Arranged (somebody sent out too late), or owed and too late to stop.
                bool strike = false;
                if ((object)_striker == car)
                {
                    Human(person, walkers, out _, out int who);
                    strike = who == _strikerId;
                }
                if (!strike && (object)_striker == null && judged == SolidRules.Driver.Late && first && speed >= SolidRules.StrikeFrom && !StopsAtItsLine(c))
                {
                    bool arranged = (object)_oweCar == car && walker != null && walker == _oweWalker;
                    if (arranged || (walker != null && walker.enabled && Owed()))
                    {
                        strike = true;
                        _owed = false;   // one at a time: nobody else is owed at this look
                        Quiet();
                        Logger?.Info($"Traffic check (struck): '{TrafficTools.Name(car)}' at {speed * 3.6f:F0} km/h could not stop: needed "
                                     + $"{SolidRules.StoppingDistance(speed, SolidRules.HardMost) + gap:F0} m, had {distance:F0}"
                                     + $" ({(arranged ? "somebody stepped out too late" : "a pedestrian is owed")}): it brakes with all its tyres give and reaches them at what is left.");
                        Loud();
                    }
                }
                if (strike)
                {
                    BaseHuman human = Human(person, walkers, out _, out int who);
                    _striker = car;
                    _strikerId = who;
                    _strikerUntil = now + StrikerFor;
                    _reachedIn[person] = float.MaxValue;   // no stand-in for them while this car comes
                    _game.Unsee(car, walker);
                    _game.Unfade(human, now);
                }

                // Somebody a step into its way whom it can no longer stop for comfortably steps back, when nothing is owed.
                if (!strike && walker != null && walker.enabled && reactions != null && before == SolidRules.Driver.Clear && SolidRules.MustWait(speed, distance)
                    && !StopsAtItsLine(c))
                {
                    float sideSpeed = _vx[person] * _fz[c] - _vz[person] * _fx[c];
                    SolidRules.Local(_grid.X(person), _grid.Z(person), _cx[c], _cz[c], _fx[c], _fz[c], out _, out float side);
                    if (StreetRules.StepsBack(side, sideSpeed, _game.HalfWidth(index)))
                    {
                        reactions.Wait(walker, carAt, true, SolidRules.WaitFor(speed, distance));
                    }
                }

                // The brake: for whoever has begun, and whoever must.
                if (judged != SolidRules.Driver.Clear || _brakes.Was(car))
                {
                    _brakes.Add(car, _grid.X(person), _grid.Z(person), nose, gap, strike);
                }
                if (verbose && before == SolidRules.Driver.Clear && judged != SolidRules.Driver.Clear && !strike)
                {
                    float needs = SolidRules.StoppingDistance(speed, judged == SolidRules.Driver.Ease ? SolidRules.HardFrom : SolidRules.HardMost) + gap;
                    Quiet();
                    Logger?.Info($"Traffic check (person ahead): '{TrafficTools.Name(car)}' at {speed * 3.6f:F0} km/h, the person {distance:F0} m ahead, needs {needs:F0} m to stop: "
                                 + (judged == SolidRules.Driver.Late ? "braking with whatever it takes (nothing is owed: it must stop): PASS"
                                     : judged == SolidRules.Driver.Hard ? "braking hard: PASS" : "braking: PASS"));
                    Loud();
                }

                // The horn at somebody who stepped out close. One car for one person: the one that reaches them first.
                if (first && hears && SolidRules.HornDue(judged, before, now - _lastHorn[index]))
                {
                    string why = null;
                    if (verbose)
                    {
                        Quiet();
                        why = $"somebody stepped out {distance:F0} m ahead at {speed * 3.6f:F0} km/h: a hard brake";
                        Loud();
                    }
                    Horn(c, index, car, walker, carAt, facing, reactions, now, why);
                }

                // Standing behind somebody. Not at a red light or a stop line the traffic system holds it at: there the people have the road.
                if (SolidRules.Held(speed, distance, gap) && car.GetCurrentAction() != SpecialDriveActionTypes.StopInPoint
                    && HornReactions.LightOf(carAt, facing) != StreetRules.Light.Red)
                {
                    if (_heldSince[index] <= 0f)
                    {
                        _heldSince[index] = now;
                    }
                    float heldFor = now - _heldSince[index];
                    if (first && hears && SolidRules.DawdleHorn(heldFor, now - _lastHorn[index]) && Dawdles(person))
                    {
                        string why = null;
                        if (verbose)
                        {
                            Quiet();
                            why = $"it has stood behind somebody for {heldFor:F0} s";
                            Loud();
                        }
                        Horn(c, index, car, walker, carAt, facing, reactions, now, why);
                    }
                    if (SolidRules.MoveOn(heldFor) && !_movedOn[index] && walker != null && walker.enabled)
                    {
                        _movedOn[index] = true;
                        bool moved = reactions != null && reactions.MoveOn(walker, carAt);
                        Quiet();
                        Logger?.Info($"Traffic check (held too long): '{TrafficTools.Name(car)}' has stood behind somebody for {heldFor:F0} s (the most is {SolidRules.HoldMost:F0}): "
                                     + (moved ? "moved the person on: PASS" : "they could not be moved on; put elsewhere by the game once nobody can see them"));
                        Loud();
                    }
                    else if (walker != null && walker.enabled && walker.isActiveAndEnabled
                             && SolidRules.Remove(heldFor, StreetSpots.IsInView(new Vector3(_grid.X(person), _height[person], _grid.Z(person)))))
                    {
                        // Out of view and still there: back to the game's pool after this look, which reads the pool's list.
                        _heldSince[index] = 0f;
                        _movedOn[index] = false;
                        _reachedIn[person] = float.MaxValue;
                        _putAway = walker;
                        Quiet();
                        Logger?.Info($"Traffic check (held too long): '{TrafficTools.Name(car)}' was still held after {heldFor:F0} s by somebody nobody can see: the game puts them elsewhere: PASS");
                        Loud();
                    }
                }
                else
                {
                    _heldSince[index] = 0f;
                    _movedOn[index] = false;
                }

                if (strikes != null && Settings.Ragdolls && SolidRules.Watch(speed, distance))
                {
                    strikes.Glance(car, now + GlanceFor);
                }
            }
            _brakes.End();
        }

        // Somebody to hoot at: standing, hardly moving, or walking where they have no right (a walker with the light is waited for).
        private bool Dawdles(int person)
        {
            if (_vx[person] * _vx[person] + _vz[person] * _vz[person] < SolidRules.MovingFrom * SolidRules.MovingFrom)
            {
                return true;
            }
            var at = new Vector3(_grid.X(person), _height[person], _grid.Z(person));
            return !(HornReactions.PlaceOf(at, out bool theirs) == StreetRules.Place.Crossing && theirs);
        }

        private void Horn(int c, int index, VehicleComponent car, Pedestrian walker, Vector3 carAt, Vector3 facing, HornReactions reactions, float now, string why)
        {
            _lastHorn[index] = now;
            bool sounded = _game.Hoot(index, car);
            // Somebody the mod stood in the lane walks on at the horn; anybody else answers it.
            if ((walker == null || !Jaywalker.Hooted(walker)) && reactions != null)
            {
                reactions.Sound(carAt, facing, _game.HalfWidth(index));
            }
            if (why != null)
            {
                Quiet();
                Logger?.Info($"Traffic check (horn): '{TrafficTools.Name(car)}': {why}: " + (sounded ? "its horn sounds, and the people answer it: PASS" : "it has no horn of the game's to sound"));
                Loud();
            }
        }

        // The stand-ins: everybody in some car's way who is not sensed as they are, the most pressing first.
        private void Share(List<Pedestrian> walkers, float now)
        {
            _pool.Begin();
            bool usable = _game.StandInsUsable;
            int people = _grid.Count;
            for (int person = 0; person < people; person++)
            {
                if (_reachedIn[person] == float.MaxValue)
                {
                    continue;
                }
                BaseHuman human = Human(person, walkers, out Pedestrian walker, out int id);
                _game.Unfade(human, now);
                if (usable && id != 0 && !_game.SensedAsIs(walker))
                {
                    _pool.Want(id, _reachedIn[person], person);
                }
            }
            _pool.Share(_freed, _given, _tags);
            for (int slot = 0; slot < _pool.Size; slot++)
            {
                if (_freed[slot])
                {
                    _game.Park(slot);   // (taken from one person: the cars are told before it stands on another)
                }
                if (_pool.Holder(slot) != 0)
                {
                    int person = _tags[slot];
                    if (!_game.Place(slot, new Vector3(_grid.X(person), _height[person], _grid.Z(person)), transform))
                    {
                        _pool.Clear(_freed);
                        break;
                    }
                }
            }
            if (Meta.Dev.Verbose && _pool.InUse != _proxiesSaid && now >= _nextProxyLine)
            {
                _proxiesSaid = _pool.InUse;
                _nextProxyLine = now + 1f;
                Quiet();
                Logger?.Info($"Traffic check (proxy): {_pool.InUse} of {_pool.Size} in use: {(_pool.InUse <= _pool.Size ? "PASS" : "more than there are")}");
                Loud();
            }
        }

        // Who somebody in the grid is: the character, the walker if one, and an id kept from look to look (never 0).
        private BaseHuman Human(int person, List<Pedestrian> walkers, out Pedestrian walker, out int id)
        {
            walker = null;
            id = 0;
            int source = _source[person];
            switch (_kind[person])
            {
                case Walker:
                    walker = source < walkers.Count ? walkers[source] : null;
                    if (walker == null || walker.tpc == null)
                    {
                        walker = null;
                        return null;
                    }
                    id = walker.tpc.GetInstanceID();
                    return walker.tpc;
                case Figure:
                    List<BaseHuman> standing = StreetPeople.Standing(Logger);
                    BaseHuman figure = source < standing.Count ? standing[source] : null;
                    id = figure != null ? figure.GetInstanceID() : 0;
                    return figure;
                case Actor:
                    ActorPool actors = ActorPool.Instance;
                    StreetActor actor = actors != null && source < actors.ActorCount ? actors.ActorAt(source) : null;
                    if (actor == null || !actor.IsValid)
                    {
                        return null;
                    }
                    id = actor.Character.GetInstanceID();
                    return actor.Character;
                default:
                    id = int.MinValue + 1 + source;
                    return null;
            }
        }
    }
}
