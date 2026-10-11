using System;
using BAModAPI;
using UnityEngine;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// A <see cref="WorldPanel"/> that stands over a door or a desk while the player is near it, with the few things
    /// every such place needs: the panel is put over the place (or over the player's head while the place is behind
    /// the camera), asked for from the owner's timer, taken down again, and the key that pressed one of its buttons
    /// is known to have been used this frame so that the mod's own interact key does nothing else with it
    /// (<see cref="TakesKey"/>, read by <c>NearbyInteraction</c> through the feature's static
    /// <c>TakesInteractKey</c>). One line goes to the log when the game accepts or refuses the panel and when it goes.
    ///
    /// The cost: nothing per frame. The owner calls <see cref="Show"/> from its once-a-second timer and builds the
    /// texts only when what they say has changed.
    /// </summary>
    public sealed class DoorPanel
    {
        private readonly IModLogger _logger;
        private readonly string _check;
        private int _state;          // 0 not up, 1 up, 2 refused by the game's own panel
        private int _keyFrame = -1;

        /// <param name="logger">The feature's logger.</param>
        /// <param name="check">The log line's start, for example "Travel check (hotel panel)".</param>
        public DoorPanel(IModLogger logger, string check)
        {
            _logger = logger;
            _check = check;
        }

        /// <summary>The game's panel, for the owner to give lines and buttons to.</summary>
        public readonly WorldPanel Panel = new WorldPanel();

        /// <summary>True while the panel is up, and in the frame its button ran.</summary>
        public bool TakesKey => Panel.Showing || _keyFrame == Time.frameCount;

        /// <summary>True from the first <see cref="Show"/> the game accepted until <see cref="Hide"/>.</summary>
        public bool Up => _state == 1;

        /// <summary>A button's action made so that it marks the frame its key was used in. Make it once and keep it.</summary>
        public Action Press(Action action)
        {
            return () =>
            {
                _keyFrame = Time.frameCount;
                action();
            };
        }

        /// <summary>
        /// Puts the panel over a place (a metre or two above it), or over the player when the place is behind the
        /// camera. <paramref name="what"/> is for the log line, built only when the state changes (pass a delegate).
        /// </summary>
        public bool Show(Vector3 place, Vector3 player, float up, Func<string> what)
        {
            Vector3 over = place + Vector3.up * up;
            Camera camera = GameManager.GetMainCamera();
            if (camera != null && Vector3.Dot(over - camera.transform.position, camera.transform.forward) < 0.5f)
            {
                over = player + Vector3.up * 2.1f;
            }
            Panel.Position = over;
            int state = Panel.Show() ? 1 : 2;
            if (state != _state)
            {
                _state = state;
                if (Meta.Dev.Verbose)
                {
                    _logger?.Info(_check + ": " + (what != null ? what() : string.Empty) + ": "
                                  + (state == 1 ? "the game's own panel is up: PASS" : "FAIL (the game's panel is showing another place; it is tried again while the player stands here)") + ".");
                }
            }
            return state == 1;
        }

        /// <summary>Takes the panel down if it is up. Safe at any time.</summary>
        public void Hide(string why)
        {
            if (_state != 0)
            {
                bool was = _state == 1;
                _state = 0;
                Panel.Hide();
                if (was && Meta.Dev.Verbose)
                {
                    _logger?.Info(_check + ": closed (" + why + ").");
                }
            }
        }
    }
}
