using System;
using BAModAPI;
using BigAmbitions.Characters;
using Dancing;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul.Leisure
{
    /// <summary>
    /// The player's own body at play: kept on the spot (the game's own navigation blocker),
    /// playing one of the mod's clips with its tool at the clip's grip (HeldTool), one of the
    /// mod's clips over an arm while the rest stays the game's, or one of the game's own
    /// dances. One for the session, owned by the director, which calls <see cref="Release"/>
    /// on every way out of every activity, so nothing here can leave the player rooted, mid
    /// clip or with something in a hand.
    /// </summary>
    public sealed class LeisureBody
    {
        private const NavigationBlocker Blocker = NavigationBlocker.EntertainActivity;

        private readonly IModLogger _logger;
        private readonly LeisureProps _props;
        private readonly HeldTool _hold = new HeldTool();
        private PlayerController _player;
        private ActivityAnimator _animator;
        private Transform _tool;
        private bool _held;
        private bool _clip;
        private bool _over;
        private bool _dancing;
        private bool _turning;
        private Quaternion _turnTo;
        private bool _settling;
        private Vector3 _settleTo;

        public LeisureBody(LeisureProps props, IModLogger logger)
        {
            _props = props;
            _logger = logger;
        }

        public bool Holding => _held;
        public Transform Tool => _tool;
        public bool NeedsFrame => _turning || _settling;

        /// <summary>The clip started with <see cref="Play"/> is still on (a one-off ends by itself; the clip service may be switched off under us).</summary>
        public bool ClipAlive => _clip && _animator != null && _animator.Active && _animator.Current != null;

        public void Bind(PlayerController player)
        {
            if (!ReferenceEquals(player, _player))
            {
                _player = player;
                _animator = null;
                _held = false;
                _clip = false;
                _over = false;
                _dancing = false;
                _turning = false;
                _settling = false;
                _tool = null;
            }
        }

        public ThirdPersonCharacter Character => _player != null ? _player.Character : null;

        /// <summary>Whether the body can be played with at all right now: there, drawn, on its feet and not something else's.</summary>
        public bool Usable
        {
            get
            {
                ThirdPersonCharacter body = Character;
                return body != null && body.animator != null && body.visible && !RagdollSystem.Holds(body);
            }
        }

        public void Hold()
        {
            if (_player == null || _held)
            {
                return;
            }
            _held = true;
            _player.ResetNavigation();
            _player.SetNavigationBlocker(Blocker);
        }

        public void Free()
        {
            if (!_held)
            {
                return;
            }
            _held = false;
            try
            {
                if (_player != null)
                {
                    _player.UnsetNavigationBlocker(Blocker);
                }
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
        }

        /// <summary>Turns the held player to look at a point, over a third of a second (stepped by <see cref="Frame"/>).</summary>
        public void Face(Vector3 point)
        {
            if (_player == null)
            {
                return;
            }
            Vector3 to = point - _player.transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > 0.0004f)
            {
                _turnTo = Quaternion.LookRotation(to);
                _turning = true;
            }
        }

        /// <summary>
        /// Brings the held player the last step on to an exact spot (a stool) over a third of a second. The spot must be
        /// on the navigation mesh within 0.6 m of where they stand; false, and nothing moved, if it is not.
        /// </summary>
        public bool Settle(Vector3 spot)
        {
            if (_player == null || !_held)
            {
                return false;
            }
            Vector3 from = _player.transform.position;
            if (CitySpots.Flat(spot - from) > 0.6f || !NavMesh.SamplePosition(spot, out NavMeshHit hit, 0.25f, NavMesh.AllAreas))
            {
                return false;
            }
            _settleTo = new Vector3(spot.x, hit.position.y, spot.z);
            _settling = true;
            return true;
        }

        /// <summary>Per frame, only while <see cref="NeedsFrame"/>: the turn and the last step. Allocates nothing.</summary>
        public void Frame(float delta)
        {
            if (_player == null)
            {
                _turning = false;
                _settling = false;
                return;
            }
            Transform body = _player.transform;
            if (_turning)
            {
                body.rotation = Quaternion.RotateTowards(body.rotation, _turnTo, 540f * delta);
                if (Quaternion.Angle(body.rotation, _turnTo) < 0.5f)
                {
                    body.rotation = _turnTo;
                    _turning = false;
                }
            }
            if (_settling)
            {
                Vector3 next = Vector3.MoveTowards(body.position, _settleTo, 1.6f * delta);
                NavMeshAgent agent = _player.Character != null ? _player.Character.navmeshAgent : null;
                if (agent != null && agent.enabled && agent.isOnNavMesh)
                {
                    agent.Warp(next);
                }
                else
                {
                    body.position = next;
                }
                if ((next - _settleTo).sqrMagnitude < 0.0001f)
                {
                    _settling = false;
                }
            }
        }

        /// <summary>
        /// One of the mod's clips on the whole body, with a tool in the hand at the clip's grip: the clip's own prop, or
        /// <paramref name="toolId"/> in its place (a glass where the clip was made with a cup of the same size). The
        /// player must be held. All or nothing.
        /// </summary>
        public bool Play(string clipName, string toolId = null, float fade = 0.3f, ClipEnd end = ClipEnd.Return)
        {
            if (!_held || !Ready(clipName, out CityClip clip, out ActivityAnimator animator))
            {
                return false;
            }
            if (!Grip(animator.Animator, clip, toolId))
            {
                return false;
            }
            StopDance();
            if (!animator.Play(clip.Clip, fade, 1f, end))
            {
                PutAway();
                return false;
            }
            _animator = animator;
            _clip = true;
            return true;
        }

        /// <summary>
        /// One of the mod's clips on the whole body with the hands EMPTY, whatever tool it was made with: for a clip that
        /// picks its tool up part of the way through (<see cref="PickUp"/> at its grip mark, <see cref="SetDown"/> at its
        /// release mark). The player must be held.
        /// </summary>
        public bool PlayBare(string clipName, float fade = 0.3f, ClipEnd end = ClipEnd.Return)
        {
            if (!_held || !Ready(clipName, out CityClip clip, out ActivityAnimator animator))
            {
                return false;
            }
            PutAway();
            StopDance();
            if (!animator.Play(clip.Clip, fade, 1f, end))
            {
                return false;
            }
            _animator = animator;
            _clip = true;
            return true;
        }

        /// <summary>
        /// Takes something that stands in the world (one of <see cref="LeisureProps"/>' props) into the hand, at the grip
        /// of the clip being played. From then on it is the body's, and is put away with everything else unless
        /// <see cref="SetDown"/> hands it back first. False, and nothing changed, if it cannot be done.
        /// </summary>
        public bool PickUp(string clipName, Transform tool)
        {
            if (tool == null || !_clip || !Ready(clipName, out CityClip clip, out ActivityAnimator animator) || !clip.HasTool)
            {
                return false;
            }
            PutAway();
            if (!_hold.Attach(animator.Animator, tool, clip.Grip))
            {
                return false;
            }
            LeisureProps.SetLayer(tool, animator.Animator.gameObject.layer);
            _tool = tool;
            return true;
        }

        /// <summary>Out of the hand and into the world where it is now: the caller's again (to stand somewhere, and to give back). Null if the hand was empty.</summary>
        public Transform SetDown()
        {
            Transform tool = _tool;
            _hold.Detach(null);
            _tool = null;
            if (tool != null)
            {
                LeisureProps.SetLayer(tool, 0);
            }
            return tool;
        }

        /// <summary>One of the mod's loops over part of the body (never the legs), with a tool as for <see cref="Play"/>. The player need not be held.</summary>
        public bool Over(string clipName, BodyMask mask, string toolId = null, float fade = 0.3f)
        {
            if (!Ready(clipName, out CityClip clip, out ActivityAnimator animator))
            {
                return false;
            }
            BodyMask part = mask == BodyMask.Full ? clip.Mask : mask;
            if (part == BodyMask.Full || !Grip(animator.Animator, clip, toolId))
            {
                return false;
            }
            if (!animator.PlayOver(clip.Clip, part, fade))
            {
                PutAway();
                return false;
            }
            _animator = animator;
            _over = true;
            return true;
        }

        /// <summary>Back to the game's own animation; the hands are emptied. The hold stays.</summary>
        public void Rest(float fade = 0.3f)
        {
            if (_animator != null)
            {
                if (_clip)
                {
                    _animator.Stop(fade);
                }
                if (_over)
                {
                    _animator.StopOver(fade);
                }
            }
            _clip = false;
            _over = false;
            PutAway();
            StopDance();
        }

        /// <summary>One of the game's own dances on the held player. False if they cannot dance now.</summary>
        public bool Dance(DanceType dance)
        {
            if (!_held || !Usable)
            {
                return false;
            }
            try
            {
                Character.SetDance(dance);
                _dancing = true;
                return true;
            }
            catch (Exception e)
            {
                _logger?.Error(e);
                return false;
            }
        }

        public void StopDance()
        {
            if (!_dancing)
            {
                return;
            }
            _dancing = false;
            try
            {
                if (Character != null && Character.animator != null)
                {
                    Character.StopDancing();
                }
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
        }

        /// <summary>Everything undone at once, whatever state it was in. Never throws.</summary>
        public void Release()
        {
            try
            {
                _turning = false;
                _settling = false;
                if (_animator != null && (_clip || _over))
                {
                    _animator.Dispose();
                }
                _clip = false;
                _over = false;
                PutAway();
                StopDance();
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
            Free();
        }

        private bool Ready(string clipName, out CityClip clip, out ActivityAnimator animator)
        {
            clip = null;
            animator = null;
            if (_player == null || !ActivityAnimators.Working || !ClipLibrary.Ready || !ClipLibrary.Has(clipName)
                || !ClipLibrary.TryGet(clipName, out clip) || !Usable)
            {
                return false;
            }
            animator = ActivityAnimators.For(Character.animator);
            return animator != null && animator.Usable;
        }

        // The tool for a clip, in the hand at the clip's grip. A clip without a tool empties the hands.
        private bool Grip(Animator animator, CityClip clip, string toolId)
        {
            if (!clip.HasTool)
            {
                PutAway();
                return true;
            }
            string id = toolId ?? clip.Prop;
            if (_tool != null && _hold.Held && _tool.name.StartsWith(id, StringComparison.Ordinal))
            {
                _hold.Regrip(clip.Grip);
                return true;
            }
            PutAway();
            Transform tool = _props.Take(id);
            if (tool == null)
            {
                return false;
            }
            if (!_hold.Attach(animator, tool, clip.Grip))
            {
                _props.Return(tool);
                return false;
            }
            LeisureProps.SetLayer(tool, animator.gameObject.layer);
            _tool = tool;
            return true;
        }

        private void PutAway()
        {
            _hold.Detach(null);
            if (_tool != null)
            {
                _props.Return(_tool);
            }
            _tool = null;
        }
    }
}
