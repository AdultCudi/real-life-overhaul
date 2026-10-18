using System.Collections;
using System.Globalization;
using BAModAPI;
using Helpers;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul
{
    /// <summary>
    /// A place of work the player takes by walking on to it (behind a cart, at a desk, on a pitch): stepping into the zone
    /// draws them the last step, turns them to the work, holds them there (<see cref="JobBody.Hold"/>) and puts on the
    /// working pose. <see cref="StepAway"/> or the director's <see cref="Clear"/> lets them go; having stepped away they
    /// must leave the zone before it takes them again. Cost: a distance and a few comparisons on the job's tick; only the
    /// last step, half a second, is per frame.
    /// </summary>
    public sealed class JobStand
    {
        private const float GlideSpeed = 2.2f;      // metres a second over the last step
        private const float GlideMost = 0.8f;       // seconds: the limit, not the usual time
        private const float Missed = 0.5f;          // metres from the place when the glide ends beyond which it is not taken
        private const float RearmBeyond = 0.6f;     // metres past the zone's edge at which it will take the player again

        private readonly JobDirector _host;
        private readonly JobBody _body;
        private readonly IModLogger _logger;
        private Vector3 _post;
        private Quaternion _facing;
        private float _reach;
        private string _idle;
        private bool _placed;
        private bool _armed;
        private bool _standing;
        private bool _settling;
        private int _token;
        private float _busyUntil;

        public JobStand(JobDirector host, JobBody body, IModLogger logger)
        {
            _host = host;
            _body = body;
            _logger = logger;
        }

        /// <summary>True from the moment the zone takes the player until they step away.</summary>
        public bool Standing => _standing;

        /// <summary>True once they are on the exact place, turned and in the pose: the work can be offered.</summary>
        public bool Ready => _standing && !_settling;

        /// <summary>Says where the place is; nothing happens to the player until they walk into the zone.</summary>
        /// <param name="facing">The way somebody at work there faces.</param>
        /// <param name="reach">The zone's radius in metres.</param>
        /// <param name="idleClip">The looping clip of the mod's that is the working pose, or null for the game's own standing.</param>
        public void Place(Vector3 post, Vector3 facing, float reach, string idleClip)
        {
            Clear();
            facing.y = 0f;
            _post = post;
            _facing = Quaternion.LookRotation(facing.sqrMagnitude > 0.0001f ? facing.normalized : Vector3.forward);
            _reach = reach;
            _idle = idleClip;
            _placed = true;
            _armed = true;
        }

        /// <summary>On the job's tick: takes a player who walked into the zone and keeps the working pose on; returns <see cref="Standing"/>.</summary>
        public bool Tick(PlayerController player, bool onFoot, float now)
        {
            if (!_placed || player == null)
            {
                return false;
            }
            ThirdPersonCharacter body = player.Character;
            if (_standing)
            {
                if (body == null || !body.visible || RagdollSystem.Holds(body) || !_body.Holding)
                {
                    Leave("something else took the player's body");
                    _armed = false;
                    return false;
                }
                if (!_settling && now >= _busyUntil && _idle != null && ClipLibrary.Has(_idle) && !_body.Playing(_idle))
                {
                    _body.Clip(_idle, 0.3f);   // back to the working pose after a one-off motion, or asked again quietly
                }
                return true;
            }
            float away = JobSpots.Flat(player.transform.position, _post);
            if (!_armed)
            {
                _armed = away > _reach + RearmBeyond;
                return false;
            }
            if (!onFoot || away > _reach || body == null || !body.visible || RagdollSystem.Holds(body) || _host == null || !_host.isActiveAndEnabled)
            {
                return false;
            }
            Enter(player, away, now);
            return _standing;
        }

        private void Enter(PlayerController player, float away, float now)
        {
            _body.Bind(player);
            _body.Hold();
            if (!_body.Holding)
            {
                return;
            }
            _standing = true;
            _settling = true;
            _busyUntil = 0f;
            _token++;
            _host.StartCoroutine(Guard.Routine(Settle(player, _token, away)));
        }

        // The last step: per frame, only while it lasts.
        private IEnumerator Settle(PlayerController player, int token, float from)
        {
            float until = Time.time + GlideMost;
            while (token == _token && _standing && player != null && Time.time < until)
            {
                Transform place = player.transform;
                Vector3 next = Vector3.MoveTowards(place.position, _post, GlideSpeed * Time.deltaTime);
                NavMeshAgent agent = player.Character != null ? player.Character.navmeshAgent : null;
                if (agent != null && agent.enabled && agent.isOnNavMesh)
                {
                    agent.Warp(next);
                }
                else
                {
                    place.position = next;
                }
                place.rotation = Quaternion.RotateTowards(place.rotation, _facing, 540f * Time.deltaTime);
                if (CitySpots.Flat(place.position - _post) < 0.03f * 0.03f && Quaternion.Angle(place.rotation, _facing) < 2f)
                {
                    break;
                }
                yield return null;
            }
            if (token != _token || !_standing || player == null)
            {
                yield break;
            }
            float off = JobSpots.Flat(player.transform.position, _post);
            if (off > Missed)
            {
                Leave("the place could not be reached (" + off.ToString("F2", CultureInfo.InvariantCulture) + " m short)");
                _armed = false;
                yield break;
            }
            player.transform.rotation = _facing;
            _settling = false;
            bool posed = _idle != null && ClipLibrary.Has(_idle) && _body.Clip(_idle, 0.3f);
            if (Meta.Dev.Verbose)
            {
                _logger?.Info("Jobs: stand entered: walked into the zone " + from.ToString("F2", CultureInfo.InvariantCulture) + " m from the place (zone "
                              + _reach.ToString("F2", CultureInfo.InvariantCulture) + " m), now "
                              + off.ToString("F2", CultureInfo.InvariantCulture) + " m from it, facing the customers, movement locked; working pose: "
                              + (posed ? "'" + _idle + "'" : _idle != null ? "the game's own standing ('" + _idle + "' is not to be had)" : "the game's own standing") + ".");
            }
        }

        /// <summary>A one-off motion is about to play (a drink served): the working pose and its tool are put down and come back after.</summary>
        public void Busy(float seconds, float now)
        {
            if (_standing)
            {
                _busyUntil = now + seconds;
                _body.Rest(0.15f);
            }
        }

        /// <summary>The player chooses to leave the place: free to walk at once, and not taken again until they have left the zone.</summary>
        public void StepAway()
        {
            if (_standing)
            {
                Leave("the player stepped away");
                _armed = false;
            }
        }

        /// <summary>The job is over: whatever state the stand is in, the player is free. Never throws.</summary>
        public void Clear()
        {
            if (_standing)
            {
                Leave("the shift ended");
            }
            _placed = false;
            _armed = false;
            _token++;
        }

        private void Leave(string why)
        {
            _standing = false;
            _settling = false;
            _token++;
            try
            {
                _body.Rest(0.2f);
            }
            catch (System.Exception e)
            {
                _logger?.Error(e);   // the player's objects may be going with the city: the hold is still let go below
            }
            _body.Free();
            if (Meta.Dev.Verbose)
            {
                _logger?.Info("Jobs: stand left (" + why + "); the player can walk.");
            }
        }
    }
}
