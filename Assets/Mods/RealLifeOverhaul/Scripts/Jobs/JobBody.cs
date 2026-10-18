using System;
using BAModAPI;
using BigAmbitions.Characters;
using Extensions;
using Helpers;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The player's own body at work: held still for a beat, mopping, strumming, tool in hand. One for the session, owned by
    /// the director, which undoes everything on every way out (<see cref="Release"/>), so a faulting job cannot leave the
    /// player rooted or holding a mop. The body is moved by one of the mod's clips over the game's controller, with the
    /// clip's tool at its grip, or else by one of the game's own loops with its hand object. A clip replaces every layer,
    /// legs included, so the player is held first (<see cref="Hold"/>, the game's navigation blocker) and nothing here plays
    /// while they walk; only <see cref="Over"/> leaves the legs to the game.
    /// </summary>
    public sealed class JobBody
    {
        private const NavigationBlocker Blocker = NavigationBlocker.PaidActivity;

        private readonly IModLogger _logger;
        private readonly JobProps _props;
        private readonly HeldTool _hold = new HeldTool();
        private PlayerController _player;
        private ActivityAnimator _animator;
        private Transform _tool;            // one of the mod's props, in the hand
        private readonly System.Collections.Generic.List<GameObject> _gameProps = new System.Collections.Generic.List<GameObject>(4);   // the game's own carried objects, on the body
        private PermanentAnimationType _pose;
        private PermanentAnimationType? _under;
        private bool _posed;
        private bool _held;
        private string _clip;
        private string _over;               // a clip on the masked layer (an arm, the arms): it goes on while they walk
        private float _beatEnds = -1f;
        private string _next;               // the clip that follows the one-off being played (Chain)
        private float _nextAt = -1f;
        private float _nextFade;

        public JobBody(JobProps props, IModLogger logger)
        {
            _props = props;
            _logger = logger;
        }

        /// <summary>True while the player is kept on the spot.</summary>
        public bool Holding => _held;

        /// <summary>True while a clip or one of the game's animations started here is on the player.</summary>
        public bool Working => ClipAlive || _posed;

        /// <summary>Told who the player is; a different player object (a load) drops whatever was remembered.</summary>
        public void Bind(PlayerController player)
        {
            if (!ReferenceEquals(player, _player))
            {
                _player = player;
                _animator = null;
                _held = false;
                _posed = false;
                _clip = null;
                _over = null;
                _tool = null;
                _gameProps.Clear();
            }
        }

        /// <summary>Keeps the player where they stand (the game's blocker), for good or for some seconds, after which <see cref="Tick"/> lets them go.</summary>
        public void Hold(float seconds = -1f)
        {
            if (_player == null)
            {
                return;
            }
            if (!_held)
            {
                _held = true;
                _player.ResetNavigation();
                _player.SetNavigationBlocker(Blocker);
            }
            _beatEnds = seconds > 0f ? Time.time + seconds : -1f;
        }

        /// <summary>Lets the player walk again. Safe when they were not held.</summary>
        public void Free()
        {
            _beatEnds = -1f;
            if (_held)
            {
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
        }

        /// <summary>Called by the director on its tick: ends a timed hold, and the clip that went with it.</summary>
        public void Tick(float now)
        {
            if (_beatEnds > 0f && now >= _beatEnds)
            {
                Rest();
                Free();
            }
            if (_next != null && now >= _nextAt)
            {
                string next = _next;
                float fade = _nextFade;
                _next = null;
                if (_clip != null)
                {
                    Clip(next, fade);   // the first is over and held on its last pose: on with the one made to follow it
                }
            }
        }

        /// <summary>
        /// One of the mod's clips on the player, with its tool if it has one; the player must be held. A loop runs until
        /// <see cref="Rest"/>; a one-off returns by itself, or with <see cref="ClipEnd.Hold"/> keeps its last pose and tool. All or
        /// nothing: false, with nothing changed, if the clip or its tool cannot be had.
        /// </summary>
        public bool Clip(string name, float fade = 0.25f, ClipEnd end = ClipEnd.Return)
        {
            _next = null;   // whatever was to follow the clip before this one no longer does
            if (_player == null || !_held || !ActivityAnimators.Working || !ClipLibrary.Ready || !ClipLibrary.TryGet(name, out CityClip clip))
            {
                return false;
            }
            ThirdPersonCharacter body = _player.Character;
            if (body == null || body.animator == null || !body.visible || RagdollSystem.Holds(body))
            {
                return false;
            }
            ActivityAnimator animator = ActivityAnimators.For(body.animator);
            if (animator == null || !animator.Usable)
            {
                return false;
            }
            if (clip.HasTool && !Tool(body.animator, clip))
            {
                return false;
            }
            ClearPose();
            if (!animator.Play(clip.Clip, fade, 1f, end))
            {
                PutAway();
                return false;
            }
            _animator = animator;
            _clip = name;
            return true;
        }

        /// <summary>
        /// Two clips made to follow one another (down on a knee, then the kneeling loop): the first played once and held on its
        /// last pose, the second from <see cref="Tick"/> the moment it is over, the tool kept if both hold it. All or nothing.
        /// <see cref="Rest"/>, <see cref="Release"/> or another <see cref="Clip"/> ends the chain.
        /// </summary>
        public bool Chain(string first, string then, float fade = 0.25f, float thenFade = 0.15f)
        {
            if (!ClipLibrary.Has(first) || !ClipLibrary.Has(then) || !ClipLibrary.TryGet(first, out CityClip one) || one.Loop
                || !Clip(first, fade, ClipEnd.Hold))
            {
                return false;
            }
            _next = then;
            _nextAt = Time.time + one.Length;
            _nextFade = thenFade;
            return true;
        }

        /// <summary>
        /// One of the mod's loops over part of the body (an arm, both arms) while the rest stays the game's: how something is
        /// carried while walking. Until <see cref="StopOver"/>; asking again for the one on changes nothing. No tool comes with it
        /// and the player need not be held. False if it cannot be had.
        /// </summary>
        public bool Over(string name, BodyMask mask, float fade = 0.25f)
        {
            if (_player == null || !ActivityAnimators.Working || !ClipLibrary.Has(name) || !ClipLibrary.TryGet(name, out CityClip clip) || !clip.Loop)
            {
                return false;
            }
            ThirdPersonCharacter body = _player.Character;
            if (body == null || body.animator == null || !body.visible || RagdollSystem.Holds(body))
            {
                return false;
            }
            BodyMask part = mask == BodyMask.Full ? clip.Mask : mask;   // Full: whatever the clip says it was made for
            ActivityAnimator animator = ActivityAnimators.For(body.animator);
            if (part == BodyMask.Full || animator == null || !animator.Usable || !animator.PlayOver(clip.Clip, part, fade))
            {
                return false;   // never the whole body from here: the legs are the game's while they walk
            }
            _animator = animator;
            _over = name;
            return true;
        }

        /// <summary>The masked clip off again: the arm goes back to whatever is underneath.</summary>
        public void StopOver(float fade = 0.25f)
        {
            if (_over != null && _animator != null)
            {
                _animator.StopOver(fade);
            }
            _over = null;
        }

        /// <summary>True while a loop started with <see cref="Clip"/> is still playing; the clip service may be switched off under us.</summary>
        public bool ClipAlive => _clip != null && _animator != null && _animator.Active;

        /// <summary>True while this clip, started with <see cref="Clip"/>, is still the one on the player.</summary>
        public bool Playing(string name)
        {
            return _clip == name && _animator != null && _animator.Active && ClipLibrary.TryGet(name, out CityClip clip) && _animator.Current == clip.Clip;
        }

        /// <summary>Whether the player's animator has one of the game's looping animations.</summary>
        public bool Knows(PermanentAnimationType pose)
        {
            return _player != null && _player.Character != null && _player.Character.animator != null
                   && _player.Character.animator.HasParameter(pose.ToString());
        }

        /// <summary>
        /// One of the game's own loops on the player, until <see cref="Rest"/>; false if their animator has none such. A loop that
        /// sits on another (mopping on the mop-in-hand idle) names that one as <paramref name="under"/>.
        /// </summary>
        public bool Pose(PermanentAnimationType pose, PermanentAnimationType? under = null)
        {
            if (!Knows(pose) || (under.HasValue && !Knows(under.Value)))
            {
                return false;
            }
            StopClip(0.2f);
            ClearPose();
            if (under.HasValue)
            {
                _player.Character.animator.SetBool(under.Value);
            }
            _player.Character.animator.SetBool(pose);
            _pose = pose;
            _under = under;
            _posed = true;
            return true;
        }

        /// <summary>
        /// One of the game's carried objects on the player, as the game places it: on its bone (a hand, or the upper chest for a
        /// strap) at its own or the data's offset. Looks only: colliders and scripts are switched off. Removed by <see cref="Rest"/>.
        /// </summary>
        public bool GameProp(string prefab, Transform hand, Vector3? position = null, Quaternion? rotation = null)
        {
            if (_player == null || _player.Character == null || string.IsNullOrEmpty(prefab))
            {
                return false;
            }
            GameObject made = null;
            try
            {
                ThirdPersonCharacter body = _player.Character;
                made = PrefabHelper.CreatePrefab(prefab);
                if (made == null || hand == null)
                {
                    if (made != null)
                    {
                        UnityEngine.Object.Destroy(made);
                    }
                    return false;
                }
                made.name = "RealLifeOverhaul Job " + prefab;
                Transform prop = made.transform;
                Vector3 at = position ?? prop.position;
                Quaternion turned = rotation ?? prop.rotation;
                foreach (Collider collider in made.GetComponentsInChildren<Collider>(true))
                {
                    collider.enabled = false;
                }
                foreach (MonoBehaviour script in made.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    script.enabled = false;
                }
                prop.SetParent(hand);
                prop.localPosition = at;
                prop.localRotation = turned;
                int layer = body.gameObject.layer;
                foreach (Transform part in made.GetComponentsInChildren<Transform>(true))
                {
                    part.gameObject.layer = layer;
                }
                _gameProps.Add(made);
                return true;
            }
            catch (Exception e)
            {
                _logger?.Error(e);
                if (made != null)
                {
                    UnityEngine.Object.Destroy(made);
                }
                return false;
            }
        }

        /// <summary>Back to the game's own idle: the clip faded out, the pose cleared, the hands emptied. The hold stays.</summary>
        public void Rest(float fade = 0.25f)
        {
            StopClip(fade);
            ClearPose();
            PutAway();
            RemoveGameProp();
        }

        /// <summary>Everything undone at once: for the end of a job, whatever state it was in. Never throws.</summary>
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
                _next = null;
                ClearPose();
                PutAway();
                RemoveGameProp();
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
            Free();
        }

        private void StopClip(float fade)
        {
            _next = null;
            if (_clip != null && _animator != null)
            {
                _animator.Stop(fade);
            }
            _clip = null;
        }

        private void ClearPose()
        {
            if (!_posed)
            {
                return;
            }
            _posed = false;
            if (_player != null && _player.Character != null && _player.Character.animator != null)
            {
                _player.Character.animator.SetBool(_pose, false);
                if (_under.HasValue)
                {
                    _player.Character.animator.SetBool(_under.Value, false);
                }
            }
        }

        private bool Tool(Animator animator, CityClip clip)
        {
            if (_tool != null && _tool.name == clip.Prop && _hold.Held)
            {
                _hold.Regrip(clip.Grip);   // the same tool, held for another clip
                return true;
            }
            PutAway();
            Transform tool = _props.Take(clip.Prop);
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

        private void RemoveGameProp()
        {
            for (int i = 0; i < _gameProps.Count; i++)
            {
                if (_gameProps[i] != null)
                {
                    UnityEngine.Object.Destroy(_gameProps[i]);
                }
            }
            _gameProps.Clear();
        }
    }
}
