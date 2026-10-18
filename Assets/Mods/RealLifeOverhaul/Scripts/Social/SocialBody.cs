using System;
using BAModAPI;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The player's own body while they are with somebody: held, turned, talking, a cup or a box in hand. The director
    /// undoes all of it (<see cref="Release"/>) on every way a scene ends. A whole-body clip needs the player held.
    /// </summary>
    public sealed class SocialBody
    {
        private const NavigationBlocker Blocker = NavigationBlocker.EntertainActivity;

        private readonly IModLogger _logger;
        private readonly CityProps _props;
        private readonly HeldTool _hold = new HeldTool();
        private PlayerController _player;
        private ActivityAnimator _animator;
        private Transform _tool;
        private string _clip;
        private string _over;
        private bool _held;

        public SocialBody(CityProps props, IModLogger logger)
        {
            _props = props;
            _logger = logger;
        }

        /// <summary>The name of the masked clip that is on, or null.</summary>
        public string OverName => _over != null && _animator != null && _animator.Over != null ? _over : null;

        /// <summary>A different player object, after a load, drops what was kept of the last.</summary>
        public void Bind(PlayerController player)
        {
            if (!ReferenceEquals(player, _player))
            {
                _player = player;
                _animator = null;
                _held = false;
                _clip = null;
                _over = null;
                _tool = null;
            }
        }

        /// <summary>Keeps the player where they stand, by the game's own navigation blocker.</summary>
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

        /// <summary>The game's own turn on the spot.</summary>
        public void Turn(MonoBehaviour host, Vector3 towards)
        {
            if (_player == null || _player.Character == null || host == null || !host.isActiveAndEnabled)
            {
                return;
            }
            Vector3 flat = towards - _player.transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude > 0.04f)
            {
                host.StartCoroutine(_player.Character.RotateTowards(towards, 0.25f));
            }
        }

        /// <summary>A clip on the whole held player; a loop or a held pose lasts until <see cref="Rest"/>. False if it cannot be had.</summary>
        public bool Clip(string name, float fade = 0.3f, ClipEnd end = ClipEnd.Return)
        {
            if (!_held || !Ready(name, out CityClip clip, out ActivityAnimator animator, out Animator body))
            {
                return false;
            }
            if (clip.HasTool && !Tool(body, clip))
            {
                return false;
            }
            if (!animator.Play(clip.Clip, fade, 1f, end))
            {
                PutAway();
                return false;
            }
            _animator = animator;
            _clip = name;
            return true;
        }

        /// <summary>A clip over part of the body, the rest the game's; <paramref name="toolId"/> must have the tool's build and pivot.</summary>
        public bool Over(string name, BodyMask mask, float fade = 0.25f, string toolId = null)
        {
            if (!Ready(name, out CityClip clip, out ActivityAnimator animator, out Animator body))
            {
                return false;
            }
            BodyMask part = mask == BodyMask.Full ? clip.Mask : mask;
            if (part == BodyMask.Full)
            {
                return false;   // the legs are the game's
            }
            if (clip.HasTool && !Tool(body, clip, toolId))
            {
                return false;
            }
            if (!animator.PlayOver(clip.Clip, part, fade))
            {
                PutAway();
                return false;
            }
            _animator = animator;
            _over = name;
            return true;
        }

        /// <summary>The masked clip off, and its tool put away unless a whole-body clip is on.</summary>
        public void StopOver(float fade = 0.25f)
        {
            if (_over != null && _animator != null)
            {
                _animator.StopOver(fade);
            }
            _over = null;
            if (_clip == null)
            {
                PutAway();
            }
        }

        /// <summary>Back to the game's own animation, hands empty; the hold stays.</summary>
        public void Rest(float fade = 0.3f)
        {
            if (_clip != null && _animator != null)
            {
                _animator.Stop(fade);
            }
            _clip = null;
            StopOver(fade);
            PutAway();
        }

        /// <summary>Everything undone, whatever the state. Never throws.</summary>
        public void Release()
        {
            try
            {
                if (_animator != null && (_clip != null || _over != null))
                {
                    _animator.Dispose();
                }
                _clip = null;
                _over = null;
                PutAway();
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
            Free();
        }

        private bool Ready(string name, out CityClip clip, out ActivityAnimator animator, out Animator body)
        {
            clip = null;
            animator = null;
            body = null;
            if (_player == null || !ActivityAnimators.Working || !ClipLibrary.Ready || !ClipLibrary.Has(name) || !ClipLibrary.TryGet(name, out clip))
            {
                return false;
            }
            ThirdPersonCharacter character = _player.Character;
            if (character == null || character.animator == null || !character.visible || RagdollSystem.Holds(character))
            {
                return false;
            }
            body = character.animator;
            animator = ActivityAnimators.For(body);
            return animator != null && animator.Usable;
        }

        private bool Tool(Animator animator, CityClip clip, string toolId = null)
        {
            string id = toolId ?? clip.Prop;
            if (_tool != null && _tool.name == id && _hold.Held)
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
            tool.gameObject.layer = animator.gameObject.layer;
            _tool = tool;
            return true;
        }

        private void PutAway()
        {
            _hold.Detach(null);
            if (_tool != null)
            {
                _tool.gameObject.layer = 0;
                _props.Return(_tool);
            }
            _tool = null;
        }
    }
}
