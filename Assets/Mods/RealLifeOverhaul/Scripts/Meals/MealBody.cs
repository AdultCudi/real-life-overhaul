using System;
using System.Collections.Generic;
using BAModAPI;
using BigAmbitions.Characters;
using Extensions;
using Helpers;
using UnityEngine;

namespace RealLifeOverhaul.Meals
{
    /// <summary>
    /// The player's body at a meal, owned by the director: seated and stood up by the game's own calls (People/PlayerSeat),
    /// a plate carried in both hands over the game's walk (the mod's carry_box_hold over the arms only), the game's own
    /// seated eating (ConsumingFoodSitting, as its customers have it) or the mod's eat_sit over the upper body where the
    /// player's animator lacks it, and the plate on the table. <see cref="Release"/> undoes all of it from any state.
    /// </summary>
    public sealed class MealBody
    {
        private const string CarryClip = "carry_box_hold";   // a box in both arms over the game's walk: the plate is held the same way
        private const string EatClip = "eat_sit";            // seated at a table, fork to the mouth from a plate 0.35 m ahead
        private const string PlateProp = "plate";            // the mod's dinner plate (PropAssets), already in the bundle
        private const string GameEat = "ConsumingFoodSitting"; // the game's seated eating: its bool, and by the game's own naming its state and clip (CharacterAnimations.GetAnimationLength)

        private static readonly int GameEatHash = Animator.StringToHash(GameEat);
        private static readonly List<AnimatorClipInfo> Clips = new List<AnimatorClipInfo>(4);   // reused: read once as a meal winds down

        private readonly IModLogger _logger;
        private readonly JobProps _props;
        private readonly PlayerSeat _seat = new PlayerSeat();
        private readonly HeldTool _hold = new HeldTool();
        private PlayerController _player;
        private ActivityAnimator _animator;
        private Transform _plate;        // the mod's plate: in the hands, or on the table
        private bool _over;              // a clip of ours on part of the body
        private bool _eating;            // the game's own seated eating is on
        private bool _overWinding;       // the mod's eat_sit is fading off and the plate stays until it has
        private int _gameLayer = -1;     // the animator layer the game's seated eating was seen on as it was ended, or -1
        private int _gameState;          // its state there: the plate stays until the layer has left it
        private float _gameLength;       // s: that state's length, the most its way out can wait for the end of a round
        private bool _gameUnseen;        // the game's eating was ended but could not be found on the animator: only the cap ends the wait
        private bool _gameEats = true;   // false once the player's animator was found without it

        public MealBody(JobProps props, IModLogger logger)
        {
            _props = props;
            _logger = logger;
        }

        public bool Seated => _seat.Seated;
        public bool Carrying => _plate != null && _hold.Held;
        public bool PlateOut => _plate != null;

        /// <summary>Where the player stood before the meal seated them; false when the meal did not seat them (the game's own rest did, or nobody).</summary>
        public bool StoodAt(out Vector3 at, out Quaternion facing)
        {
            return _seat.StoodAt(out at, out facing);
        }

        /// <summary>Told who the player is; another player object (a load) forgets the old one's state without touching it.</summary>
        public void Bind(PlayerController player)
        {
            if (!ReferenceEquals(player, _player))
            {
                _player = player;
                _animator = null;
                _over = false;
                _eating = false;
                _plate = null;
                _overWinding = false;
                _gameLayer = -1;
                _gameUnseen = false;
            }
        }

        /// <summary>The plate in both hands over the game's walk; false, nothing changed, without the clip, the plate or free hands.</summary>
        public bool Carry()
        {
            if (_player == null || _plate != null || PlayerHelper.IsHoldingItem || !Ready(CarryClip, out CityClip clip, out ActivityAnimator animator))
            {
                return false;
            }
            Transform plate = _props.Take(PlateProp);
            if (plate == null)
            {
                return false;
            }
            if (!_hold.Attach(animator.Animator, plate, clip.Grip))
            {
                _props.Return(plate);
                return false;
            }
            plate.gameObject.layer = animator.Animator.gameObject.layer;
            if (!animator.PlayOver(clip.Clip, BodyMask.Arms, 0.25f))
            {
                _hold.Detach(null);
                _props.Return(plate);
                return false;
            }
            _plate = plate;
            _animator = animator;
            _over = true;
            ClipLibrary.Played(CarryClip, "the player");
            return true;
        }

        /// <summary>The player sat down on the seat's place (a bag may stay in the hand, as the game's own rest allows).</summary>
        public bool Sit(Transform place)
        {
            return _player != null && _seat.Sit(_player, place, true);
        }

        /// <summary>The plate on the table at this spot: the one carried, or one put there. False without a plate.</summary>
        public bool PlateOn(Vector3 at, Quaternion turn)
        {
            if (_plate == null)
            {
                _plate = _props.Take(PlateProp);
                if (_plate == null)
                {
                    return false;
                }
            }
            StopOver();
            _hold.Detach(null);
            _plate.gameObject.layer = 0;
            _plate.SetPositionAndRotation(at, turn);
            return true;
        }

        /// <summary>The carried plate put away without a table (no table at the seat): the hands are empty for the meal.</summary>
        public void PlateAway()
        {
            StopOver();
            _hold.Detach(null);
            if (_plate != null)
            {
                _plate.gameObject.layer = 0;
                _props.Return(_plate);
            }
            _plate = null;
        }

        /// <summary>Eating at the seat: the game's own seated eating where the player's animator has it, else the mod's clip over the upper body.</summary>
        public bool Eat()
        {
            ThirdPersonCharacter body = _player != null ? _player.Character : null;
            if (body == null || body.animator == null)
            {
                return false;
            }
            if (_gameEats && body.animator.HasParameter(PermanentAnimationType.ConsumingFoodSitting.ToString()))
            {
                body.animator.SetBool(PermanentAnimationType.ConsumingFoodSitting);
                _eating = true;
                return true;
            }
            _gameEats = false;
            if (!Ready(EatClip, out CityClip clip, out ActivityAnimator animator) || !animator.PlayOver(clip.Clip, BodyMask.Upper, 0.3f))
            {
                return false;
            }
            _animator = animator;
            _over = true;
            ClipLibrary.Played(EatClip, "the player");
            return true;
        }

        /// <summary>
        /// The meal is eaten: the eating animation is ended and the plate stays on the table. True when there is an animation to see out
        /// (see <see cref="WoundDown"/>); false when nothing was eating, and the plate may go at once. The game's own eating is found on
        /// the animator first, while it still plays: once its bool is off, the layer it is on leaves it in its own time, which may be
        /// the end of a round of bites rather than at once.
        /// </summary>
        public bool WindDown()
        {
            _overWinding = _over && _animator != null;
            _gameLayer = -1;
            _gameUnseen = false;
            _gameLength = 0f;
            if (_eating)
            {
                _gameLayer = GameEatingLayer(out _gameState, out _gameLength);
                _gameUnseen = _gameLayer < 0;
            }
            StopEating();
            StopOver();
            return _overWinding || _gameLayer >= 0 || _gameUnseen;
        }

        /// <summary>
        /// After <see cref="WindDown"/>: the mod's clip has faded off the body and the game's eating has left its layer, transition
        /// and all. A few reads a frame for a second or two.
        /// </summary>
        public bool WoundDown
        {
            get
            {
                if (_overWinding && _animator != null && _animator.OverWeight > 0.02f)
                {
                    return false;
                }
                if (_gameUnseen)
                {
                    return false;
                }
                Animator animator = _gameLayer >= 0 ? GameAnimator() : null;
                if (animator == null || _gameLayer >= animator.layerCount)
                {
                    return true;
                }
                return !animator.IsInTransition(_gameLayer) && animator.GetCurrentAnimatorStateInfo(_gameLayer).fullPathHash != _gameState;
            }
        }

        /// <summary>
        /// The longest the eating can take to end after <see cref="WindDown"/>, s, before the cap: the game's eating state's own length
        /// (its way out may wait for the end of a round), or 0 where only the mod's clip, or nothing, was eating.
        /// </summary>
        public float EatingLength => _gameLayer >= 0 ? _gameLength : 0f;

        /// <summary>The clock the eating runs on: the game's animator on its own update mode, the mod's clip on game time (ActivityAnimator).</summary>
        public float Clock
        {
            get
            {
                Animator animator = _gameLayer >= 0 || _gameUnseen ? GameAnimator() : null;
                return animator != null && animator.updateMode == AnimatorUpdateMode.UnscaledTime ? Time.unscaledTime : Time.time;
            }
        }

        /// <summary>What the plate waited for as the meal wound down, for its line.</summary>
        public string WoundBy => _gameLayer >= 0
            ? "the game's seated eating to leave layer " + _gameLayer + " (" + _gameLength.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " s a round)"
            : _gameUnseen ? "the game's seated eating, not found on the animator" : _overWinding ? "the mod's eat_sit to fade" : "nothing";

        // The layer where the game's seated eating plays, its state and that state's length; -1 where it is on no layer. Its state by the
        // game's naming, else its clip by name. Once per meal: the clip names are read here only.
        private int GameEatingLayer(out int state, out float length)
        {
            state = 0;
            length = 0f;
            Animator animator = GameAnimator();
            if (animator == null || !animator.isActiveAndEnabled)
            {
                return -1;
            }
            for (int layer = 0; layer < animator.layerCount; layer++)
            {
                AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(layer);
                bool found = info.shortNameHash == GameEatHash;
                if (!found)
                {
                    animator.GetCurrentAnimatorClipInfo(layer, Clips);
                    for (int i = 0; i < Clips.Count && !found; i++)
                    {
                        AnimationClip clip = Clips[i].clip;
                        found = clip != null && Clips[i].weight > 0.5f && clip.name.IndexOf(GameEat, StringComparison.OrdinalIgnoreCase) >= 0;
                    }
                    Clips.Clear();
                }
                if (found)
                {
                    state = info.fullPathHash;
                    length = info.length;
                    return layer;
                }
            }
            return -1;
        }

        private Animator GameAnimator()
        {
            return _player != null && _player.Character != null ? _player.Character.animator : null;
        }

        /// <summary>What eats: the game's own animation, the mod's clip, or none.</summary>
        public string EatingBy => _eating ? "the game's own seated eating" : _over ? "the mod's eat_sit clip" : "nothing (no clip could be played)";

        /// <summary>Everything undone: the eating stopped, the plate back on the shelf, the player on their feet where they stood. Never throws.</summary>
        public void Release()
        {
            try
            {
                _overWinding = false;
                _gameLayer = -1;
                _gameUnseen = false;
                StopEating();
                StopOver();
                _hold.Detach(null);
                if (_plate != null)
                {
                    _plate.gameObject.layer = 0;
                    _props.Return(_plate);
                }
                _plate = null;
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
            try
            {
                _seat.Stand();
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
        }

        private void StopEating()
        {
            if (!_eating)
            {
                return;
            }
            _eating = false;
            if (_player != null && _player.Character != null && _player.Character.animator != null)
            {
                _player.Character.animator.SetBool(PermanentAnimationType.ConsumingFoodSitting, false);
            }
        }

        private void StopOver()
        {
            if (_over && _animator != null)
            {
                _animator.StopOver(0.25f);
            }
            _over = false;
        }

        private bool Ready(string name, out CityClip clip, out ActivityAnimator animator)
        {
            clip = null;
            animator = null;
            if (_player == null || !ActivityAnimators.Working || !ClipLibrary.Ready || !ClipLibrary.Has(name) || !ClipLibrary.TryGet(name, out clip))
            {
                return false;
            }
            ThirdPersonCharacter body = _player.Character;
            if (body == null || body.animator == null || !body.visible || RagdollSystem.Holds(body))
            {
                return false;
            }
            animator = ActivityAnimators.For(body.animator);
            return animator != null && animator.Usable;
        }
    }
}
