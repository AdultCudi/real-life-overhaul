using System;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The player sitting on a bus shelter's bench, held by the game's navigation blocker while the mod's clips play over the
    /// animator (<see cref="ActivityAnimator"/>); the game seats a player only in activities that run the clock. They stay an
    /// ordinary walker, so whatever ends it they can walk. Per frame only while on: the last step, then a test for any key.
    /// </summary>
    public sealed class TransitSeat
    {
        private const NavigationBlocker Blocker = NavigationBlocker.RestActivity;

        private enum Step { None, Settling, Down, Seated, Up }

        private PlayerController _player;
        private ActivityAnimator _animator;
        private BusStopDressing _at;
        private CityClip _down;
        private CityClip _wait;
        private CityClip _up;
        private Step _step;
        private int _seat = -1;
        private bool _held;
        private float _until;
        private Vector3 _feet;
        private Quaternion _facing;

        /// <summary>From the key press until the player is on their feet and free again.</summary>
        public bool Active => _step != Step.None;

        public bool Seated => _step == Step.Seated || _step == Step.Down;   // sitting, ready to stand up

        public BusStopDressing At => _at;

        /// <summary>Metres between where the feet were put and where the seat wants them.</summary>
        public float FeetOff { get; private set; }

        /// <summary>Whether the clips and the clip player are there; the prompt is not offered otherwise.</summary>
        public static bool Possible => ClipLibrary.Ready && ActivityAnimators.Working && ClipLibrary.Has("sit_bench") && ClipLibrary.Has("sit_wait") && ClipLibrary.Has("sit_bench_up");

        /// <summary>Sits the player on a seat of a dressed shelter. False, with why and nothing changed, if it cannot.</summary>
        public bool Sit(BusStopDressing dressing, int seat, out string why)
        {
            why = null;
            var game = InstanceBehavior<GameManager>.Instance;
            PlayerController player = game != null ? game.playerController : null;
            ThirdPersonCharacter body = player != null ? player.Character : null;
            if (Active || dressing == null || !dressing.SeatFree(seat) || body == null || body.animator == null)
            {
                why = "the seat is taken or the player is not there";
                return false;
            }
            if (!body.visible || RagdollSystem.Holds(body) || !Possible
                || !ClipLibrary.TryGet("sit_bench", out _down) || !ClipLibrary.TryGet("sit_wait", out _wait) || !ClipLibrary.TryGet("sit_bench_up", out _up))
            {
                why = "the sitting clips cannot be played on the player now";
                return false;
            }
            ActivityAnimator animator = ActivityAnimators.For(body.animator);
            if (animator == null || !animator.Usable)
            {
                why = "the player's animator cannot play the mod's clips";
                return false;
            }
            Vector3 feet = dressing.SeatFeetAt(seat);
            if (!NavMesh.SamplePosition(feet, out NavMeshHit hit, 0.35f, NavMesh.AllAreas))
            {
                why = "the place in front of the seat is not walkable ground";
                return false;
            }
            _player = player;
            _animator = animator;
            _at = dressing;
            _seat = seat;
            _feet = hit.position;
            FeetOff = Mathf.Sqrt(CitySpots.Flat(hit.position - feet));
            Vector3 facing = dressing.SeatFacing;
            facing.y = 0f;
            _facing = Quaternion.LookRotation(facing.sqrMagnitude > 0.01f ? facing : Vector3.forward);
            dressing.TakeSeat(seat, true);
            player.ResetNavigation();
            player.SetNavigationBlocker(Blocker);
            _held = true;
            _step = Step.Settling;
            _until = Time.time + 1.2f;   // only a limit: the last step is over well before
            return true;
        }

        /// <summary>Per frame while <see cref="Active"/>. True in the frame a key or a click asks to get up.</summary>
        public bool Frame(float delta)
        {
            if (_step == Step.Settling && _player != null)
            {
                Transform body = _player.transform;
                Vector3 next = Vector3.MoveTowards(body.position, _feet, 2.2f * delta);
                NavMeshAgent agent = _player.Character != null ? _player.Character.navmeshAgent : null;
                if (agent != null && agent.enabled && agent.isOnNavMesh)
                {
                    agent.Warp(next);
                }
                else
                {
                    body.position = next;
                }
                body.rotation = Quaternion.RotateTowards(body.rotation, _facing, 540f * delta);
                return false;
            }
            return (_step == Step.Seated || _step == Step.Down) && Input.anyKeyDown;
        }

        /// <summary>From the director's timer while <see cref="Active"/>: the steps from clip to clip.</summary>
        public void Tick(float now)
        {
            if (_step == Step.None)
            {
                return;
            }
            ThirdPersonCharacter body = _player != null ? _player.Character : null;
            if (body == null || body.animator == null || !body.visible || RagdollSystem.Holds(body) || _at == null || _at.Stop == null || !ActivityAnimators.Working)
            {
                Abort();   // the stop was taken in, they were knocked over, or the clips were switched off
                return;
            }
            switch (_step)
            {
                case Step.Settling:
                    Transform place = _player.transform;
                    if ((CitySpots.Flat(place.position - _feet) < 0.03f * 0.03f && Quaternion.Angle(place.rotation, _facing) < 2f) || now >= _until)
                    {
                        if (CitySpots.Flat(place.position - _feet) > 0.5f * 0.5f || !_animator.Play(_down.Clip, 0.2f, 1f, ClipEnd.Hold))
                        {
                            Abort();   // something kept them from the seat
                            return;
                        }
                        place.rotation = _facing;
                        _step = Step.Down;
                        _until = now + _down.Length;
                    }
                    break;

                case Step.Down:
                    if (now >= _until)
                    {
                        // sit_bench ends in sit_wait's first pose exactly, so no fade.
                        if (!_animator.Play(_wait.Clip, 0f))
                        {
                            Abort();
                            return;
                        }
                        _step = Step.Seated;
                    }
                    break;

                case Step.Seated:
                    if (!_animator.Active || _animator.Current != _wait.Clip)
                    {
                        Abort();   // something else took the animator, so nothing of ours is left on it
                    }
                    break;

                case Step.Up:
                    if (now >= _until)
                    {
                        Finish(0.15f);
                    }
                    break;
            }
        }

        /// <summary>Gets up with the clip; before they have sat down properly, at once.</summary>
        public void Stand(float now)
        {
            if (_step == Step.Seated && _animator != null && _animator.Play(_up.Clip, 0.2f))
            {
                _step = Step.Up;
                _until = now + _up.Length;
                return;
            }
            if (_step != Step.Up)
            {
                Abort();
            }
        }

        /// <summary>Undoes everything at once, whatever the step. Never throws.</summary>
        public void Abort()
        {
            Finish(0.1f);
        }

        private void Finish(float fade)
        {
            try
            {
                if (_animator != null && _step != Step.Settling && _step != Step.None && _animator.Active
                    && (_animator.Current == _down?.Clip || _animator.Current == _wait?.Clip || _animator.Current == _up?.Clip))
                {
                    _animator.Stop(fade);
                }
            }
            catch (Exception e)
            {
                TransitDirector.Logger?.Error(e);
            }
            try
            {
                if (_held && _player != null)
                {
                    _player.UnsetNavigationBlocker(Blocker);
                }
            }
            catch (Exception e)
            {
                TransitDirector.Logger?.Error(e);
            }
            if (_at != null && _seat >= 0)
            {
                _at.LeaveSeat(_seat);
            }
            _held = false;
            _step = Step.None;
            _at = null;
            _seat = -1;
            _animator = null;
            _player = null;
        }
    }
}
