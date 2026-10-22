using System;
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

        private readonly IModLogger _logger;
        private readonly JobProps _props;
        private readonly PlayerSeat _seat = new PlayerSeat();
        private readonly HeldTool _hold = new HeldTool();
        private PlayerController _player;
        private ActivityAnimator _animator;
        private Transform _plate;        // the mod's plate: in the hands, or on the table
        private bool _over;              // a clip of ours on part of the body
        private bool _eating;            // the game's own seated eating is on
        private bool _gameEats = true;   // false once the player's animator was found without it

        public MealBody(JobProps props, IModLogger logger)
        {
            _props = props;
            _logger = logger;
        }

        public bool Seated => _seat.Seated;
        public bool Carrying => _plate != null && _hold.Held;
        public bool PlateOut => _plate != null;

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

        /// <summary>What eats: the game's own animation, the mod's clip, or none.</summary>
        public string EatingBy => _eating ? "the game's own seated eating" : _over ? "the mod's eat_sit clip" : "nothing (no clip could be played)";

        /// <summary>Everything undone: the eating stopped, the plate back on the shelf, the player on their feet where they stood. Never throws.</summary>
        public void Release()
        {
            try
            {
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
