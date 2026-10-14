using System;
using System.Collections.Generic;
using BigAmbitions.InputSystem;
using Player.HUD.ControlHints;
using UnityEngine.InputSystem;

namespace RealLifeOverhaul.Economy.Dealership
{
    /// <summary>
    /// The keys of the Car Showroom's camera and of choosing a bay, in the game's own panel of control hints (the one it
    /// shows for driving and for placing an item: Player.HUD.ControlHints), each key drawn as the player has bound it.
    /// Two groups of the game's kind (IControlsHintProvider), put in the game's list of them once a city, behind the
    /// loading screen (the list is one of the game's assets, found by a search of what is in memory); the panel shows a
    /// group while it is active. Where the game's own words fit they are its own (rotate left, rotate right, press to
    /// cancel), so they follow the game's language.
    ///
    /// If the list cannot be found, or the player has the game's hints switched off, the camera says its keys once in a
    /// notice instead (<see cref="Shown"/>). The cost: nothing per frame; a change of group is two field writes and the
    /// game's own event.
    /// </summary>
    internal sealed class ShowroomHints : IControlsHintProvider
    {
        public const string LookHeader = "reallife_showroom_hints_look";
        public const string PlaceHeader = "reallife_showroom_hints_place";
        private const int Rank = 20;   // above the game's own groups: the showroom's keys come first while they apply

        private static ControlsHintRegistry _registry;
        private static ShowroomHints _look, _place;

        private readonly List<ControlsHint> _hints;
        private bool _active;

        private ShowroomHints(string header, List<ControlsHint> hints)
        {
            HeaderKey = header;
            _hints = hints;
        }

        public string HeaderKey { get; }

        public int Priority => Rank;

        public bool IsActive => _active;

        public IReadOnlyList<ControlsHint> Hints => _hints;

        public event Action Changed;

        /// <summary>True while the game's panel will show the keys: the groups are in its list and the player has its hints on.</summary>
        public static bool Shown => _registry != null && _look != null && PlayerPrefSettings.ControlHints;

        /// <summary>
        /// Once a city, behind the loading screen: the game's list of hint groups found and the showroom's two put in it.
        /// True when done, whether or not it could be (the camera then says its keys in a notice).
        /// </summary>
        public static bool Prewarm()
        {
            ControlsHintRegistry[] found = SceneFinds.InMemory<ControlsHintRegistry>("showroom: the game's list of control hints");
            _registry = found != null && found.Length > 0 ? found[0] : null;
            if (_registry == null)
            {
                ShowroomView.Logger?.Info("Car Showroom: the game's panel of control hints was not found; the showroom's camera says its keys in a notice.");
                return true;
            }
            if (_look == null)
            {
                _look = new ShowroomHints(LookHeader, new List<ControlsHint>
                {
                    Hint("reallife_showroom_hint_drag", PlayerAction.RightClick),
                    Hint("ba:controls_hint_rotate_left", PlayerAction.RotateLeft),
                    Hint("ba:controls_hint_rotate_right", PlayerAction.RotateRight),
                    Hint("reallife_showroom_hint_zoom"),
                    Hint("reallife_showroom_hint_bay", PlayerAction.Interact),
                    Hint("ba:controls_hint_placement_cancel", PlayerAction.Cancel)
                });
                _place = new ShowroomHints(PlaceHeader, new List<ControlsHint>
                {
                    Hint("reallife_showroom_hint_put"),
                    Hint("reallife_showroom_hint_turn_car", PlayerAction.RotateItem),
                    Hint("ba:controls_hint_rotate_left", PlayerAction.RotateLeft),
                    Hint("ba:controls_hint_rotate_right", PlayerAction.RotateRight),
                    Hint("reallife_showroom_hint_zoom"),
                    Hint("ba:controls_hint_placement_cancel", PlayerAction.Cancel)
                });
            }
            Register(_look);
            Register(_place);
            return true;
        }

        private static ControlsHint Hint(string key, params PlayerAction[] actions)
        {
            var bindings = new List<ControlsHintBinding>(actions.Length);
            for (int i = 0; i < actions.Length; i++)
            {
                if (InputActionHelper.PlayerInputActionMap.TryGetValue(actions[i], out InputAction action) && action != null)
                {
                    bindings.Add(new ControlsHintBinding(InputActionReference.Create(action)));
                }
            }
            return new ControlsHint(key, bindings.ToArray());
        }

        private static void Register(ShowroomHints group)
        {
            IReadOnlyList<IControlsHintProvider> there = _registry.Providers;
            for (int i = 0; i < there.Count; i++)
            {
                if (there[i] == group || string.Equals(there[i].HeaderKey, group.HeaderKey, StringComparison.Ordinal))
                {
                    return;
                }
            }
            _registry.Register(group);
        }

        /// <summary>Which group the panel shows: the camera's, the bay's, or none.</summary>
        public static void Show(bool look, bool place)
        {
            Set(_look, look);
            Set(_place, place);
        }

        private static void Set(ShowroomHints group, bool on)
        {
            if (group == null || group._active == on)
            {
                return;
            }
            group._active = on;
            group.Changed?.Invoke();
        }

        /// <summary>The city is unloading: both groups off and out of the game's list (it is an asset, and outlives the city).</summary>
        public static void Forget()
        {
            Show(false, false);
            if (_registry != null)
            {
                if (_look != null)
                {
                    _registry.Deregister(_look);
                }
                if (_place != null)
                {
                    _registry.Deregister(_place);
                }
            }
            _registry = null;
        }
    }
}
