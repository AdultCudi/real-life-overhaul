using System;
using BAModAPI;
using UnityEngine;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// A <see cref="WorldPanel"/> over a door or a desk while the player is near, shown from the owner's timer, with the frame
    /// a button's key ran known (<see cref="TakesKey"/>) so the mod's own interact key does nothing else with it, and a log
    /// line when the game takes or refuses it. Costs nothing a frame.
    /// </summary>
    public sealed class DoorPanel
    {
        private readonly IModLogger _logger;
        private readonly string _check;
        private int _state;          // 0 not up, 1 up, 2 refused by the game's own panel, 3 waiting for the building box to leave it room
        private int _keyFrame = -1;

        /// <param name="logger">The feature's logger.</param>
        /// <param name="check">The log line's start, for example "Travel check (hotel panel)".</param>
        public DoorPanel(IModLogger logger, string check)
        {
            _logger = logger;
            _check = check;
            Panel.Name = NameOf(check);
        }

        // "Social check (prompt panel)" is the "Social prompt panel" in the line about who has the box.
        private static string NameOf(string check)
        {
            int at = check != null ? check.IndexOf(" check (", StringComparison.Ordinal) : -1;
            return at > 0 && check.EndsWith(")", StringComparison.Ordinal) ? check.Substring(0, at) + " " + check.Substring(at + 8, check.Length - at - 9) : check ?? "place";
        }

        /// <summary>The game's panel, for the owner to give lines and buttons to.</summary>
        public readonly WorldPanel Panel = new WorldPanel();

        /// <summary>True while the panel is up, and in the frame its button ran.</summary>
        public bool TakesKey => Panel.Showing || _keyFrame == Time.frameCount;

        /// <summary>True from the first <see cref="Show"/> the game accepted until <see cref="Hide"/>.</summary>
        public bool Up => _state == 1;

        /// <summary>A button's action that marks the frame its key was used in. Make it once and keep it.</summary>
        public Action Press(Action action)
        {
            return () =>
            {
                _keyFrame = Time.frameCount;
                action();
            };
        }

        /// <summary>The panel <paramref name="up"/> metres over a place, or over the player when it is behind the camera; <paramref name="what"/> builds the log line.</summary>
        public bool Show(Vector3 place, Vector3 player, float up, Func<string> what)
        {
            Panel.For(place);
            Vector3 over = place + Vector3.up * up;
            Camera camera = GameManager.GetMainCamera();
            if (camera != null && Vector3.Dot(over - camera.transform.position, camera.transform.forward) < 0.5f)
            {
                over = player + Vector3.up * 2.1f;
            }
            Panel.Position = over;
            int state = Panel.Show() ? 1 : Panel.Waiting ? 3 : 2;
            if (state != _state)
            {
                _state = state;
                if (Meta.Dev.Verbose)
                {
                    _logger?.Info(_check + ": " + (what != null ? what() : string.Empty) + ": "
                                  + (state == 1 ? "the game's own panel is up: PASS"
                                      : state == 3 ? "the game's building box leaves it no room on any side, so it waits and is tried again while the player stands here: PASS"
                                      : "FAIL (another panel has the game's box and keeps it by the rule, see the 'Panel check (who has the box)' line;"
                                         + " it is tried again while the player stands here)") + ".");
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
