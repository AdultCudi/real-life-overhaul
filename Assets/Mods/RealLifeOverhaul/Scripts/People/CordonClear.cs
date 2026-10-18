using System.Collections.Generic;
using BAModAPI;
using Extensions;
using Helpers;
using Streets.Pedestrians;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Clears a cordon as its tape goes up (called by Traffic/SceneCordon): the game's pedestrians, the living city's people
    /// and the officers only there to be seen who stand inside are put at the nearest place outside (Traffic/CordonRules),
    /// on ground their own agent walks. The scene's own people are never moved. Cost: one pass, on that frame only.
    /// </summary>
    internal static class CordonClear
    {
        private const float Reach = 1f;    // metres from a place outside the tape to the walkable ground found for it
        private const int MostPut = 48;    // people put outside at one raising: more than the street holds near one scene

        private static readonly float[] Alongs = new float[CordonRules.Exits];
        private static readonly float[] Acrosses = new float[CordonRules.Exits];
        private static readonly bool[] AlongSide = new bool[CordonRules.Exits];
        private static readonly float[] PutAlong = new float[MostPut];
        private static readonly float[] PutAcross = new float[MostPut];

        private static CordonBox _box;
        private static Vector3 _scene, _forward, _right;

        private static int _put, _walkers, _city, _presence, _left, _returned;
        private static float _furthest;

        /// <summary>Puts everybody but the scene's own people outside a cordon whose tape has just gone up, and logs it.</summary>
        public static void Out(SceneCordon cordon, IModLogger logger)
        {
            if (cordon == null || !cordon.Frame(out _scene, out _forward, out _right, out float back, out float front, out float left, out float right))
            {
                return;
            }
            _box = new CordonBox(back, front, left, right);
            _put = _walkers = _city = _presence = _left = _returned = 0;
            _furthest = 0f;
            try
            {
                Walkers();
                CityLife();
                Presence();
            }
            catch (System.Exception e)
            {
                logger?.Error(e);
            }
            int inside = _walkers + _city + _presence;
            if (inside == 0)
            {
                logger?.Info("Cordon check (cleared): nobody but the scene's own people stood inside the tape as it went up: PASS.");
                return;
            }
            logger?.Info($"Cordon check (cleared): {inside} people stood inside the tape or against it as it went up ({_walkers} of the game's pedestrians, "
                         + $"{_city} of the living city's, {_presence} officer(s) only there to be seen); {_put} put at the nearest place outside it "
                         + System.FormattableString.Invariant($"(the furthest {_furthest:F1} m from where they stood), each at least {CordonRules.Spacing:F2} m from the next")
                         + (_returned > 0 ? $", {_returned} out of view given back to the game's pool" : "")
                         + (_left > 0 ? $"; {_left} with no walkable ground outside within reach, left where they stand: FAIL." : ": PASS."));
        }

        // One the mod has borrowed is switched off (PedestrianTakeover).
        private static void Walkers()
        {
            var city = InstanceBehavior<CityManager>.Instance;
            List<Pedestrian> walkers = city != null && city.pedestrianSpawner != null ? city.pedestrianSpawner.activePedestrians : null;
            if (walkers == null)
            {
                return;
            }
            for (int i = walkers.Count - 1; i >= 0; i--)
            {
                Pedestrian walker = walkers[i];
                if (walker == null || !walker.isActiveAndEnabled || walker.tpc == null || walker.tpc.isSittingOn != null || RagdollSystem.Holds(walker.tpc)
                    || !Within(walker.tpc))
                {
                    continue;
                }
                _walkers++;
                if (Put(walker.tpc))
                {
                    continue;
                }
                if (!StreetSpots.IsInView(walker.transform.position))
                {
                    _returned++;
                    walker.Release();   // the game's own way of moving one on: it comes out again somewhere else
                    continue;
                }
                _left++;
            }
        }

        // Whoever the casualty system has is not the living city's any more.
        private static void CityLife()
        {
            CityLifeDirector director = CityLifeDirector.Instance;
            CityPeople people = director != null ? director.People : null;
            if (people == null)
            {
                return;
            }
            for (int i = 0; i < people.Count; i++)
            {
                CityPerson person = people.At(i);
                if (person == null || !person.Valid || !person.InUse || person.Taken || person.Seated || !Within(person.Character))
                {
                    continue;
                }
                _city++;
                if (!Put(person.Character))
                {
                    _left++;
                }
            }
        }

        // Everyone else in the pool is a scene's, and the scene's to place.
        private static void Presence()
        {
            ActorPool pool = ActorPool.Instance;
            if (pool == null)
            {
                return;
            }
            for (int i = pool.ActorCount - 1; i >= 0; i--)
            {
                StreetActor actor = pool.ActorAt(i);
                if (actor == null || !actor.InUse || !actor.Yields || !actor.IsValid || actor.Limp || !Within(actor.Character))
                {
                    continue;
                }
                _presence++;
                if (!Put(actor.Character))
                {
                    _left++;
                }
            }
        }

        private static bool Within(ThirdPersonCharacter body)
        {
            Vector3 offset = body.transform.position - _scene;
            return CordonRules.Inside(_box, Vector3.Dot(offset, _forward), Vector3.Dot(offset, _right));
        }

        private static bool Put(ThirdPersonCharacter body)
        {
            Vector3 at = body.transform.position;
            Vector3 offset = at - _scene;
            NavMeshAgent agent = body.navmeshAgent;
            var filter = new NavMeshQueryFilter
            {
                agentTypeID = agent != null ? agent.agentTypeID : NavMeshHelper.NpcAgentTypeId,
                areaMask = agent != null ? agent.areaMask : NavMesh.AllAreas
            };
            CordonRules.Out(_box, Vector3.Dot(offset, _forward), Vector3.Dot(offset, _right), Alongs, Acrosses, AlongSide);
            for (int i = 0; i < CordonRules.Exits && _put < MostPut; i++)
            {
                float along = Alongs[i];
                float across = Acrosses[i];
                if (!CordonRules.Spread(_box, AlongSide[i], ref along, ref across, PutAlong, PutAcross, _put))
                {
                    continue;
                }
                Vector3 wanted = _scene + _forward * along + _right * across;
                wanted.y = at.y;
                if (!StreetSpots.OnWalk(wanted, Reach, filter, out Vector3 spot))
                {
                    continue;
                }
                // The ground found may lie nearer the tape than asked (or over it), or beside somebody put there already: not then.
                Vector3 found = spot - _scene;
                float foundAlong = Vector3.Dot(found, _forward);
                float foundAcross = Vector3.Dot(found, _right);
                if (CordonRules.Inside(_box, foundAlong, foundAcross) || !CordonRules.Apart(foundAlong, foundAcross, PutAlong, PutAcross, _put)
                    || !Shift(body, spot))
                {
                    continue;
                }
                PutAlong[_put] = foundAlong;
                PutAcross[_put] = foundAcross;
                _put++;
                Vector3 moved = spot - at;
                moved.y = 0f;
                _furthest = Mathf.Max(_furthest, moved.magnitude);
                return true;
            }
            return false;
        }

        // Somebody walking is sent on to where they were going, held still if they were held (lights, a horn).
        private static bool Shift(ThirdPersonCharacter body, Vector3 spot)
        {
            NavMeshAgent agent = body.navmeshAgent;
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                bool going = agent.hasPath || agent.pathPending;
                Vector3 bound = agent.destination;
                bool held = agent.isStopped;
                if (!agent.Warp(spot))
                {
                    return false;
                }
                if (going)
                {
                    agent.SetDestination(bound);
                }
                agent.isStopped = held;
                return true;
            }
            body.transform.position = spot;
            return true;
        }
    }
}
