using System;
using BAModAPI;
using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// A job's use of the game's panel over a place (<see cref="WorldPanel"/>): shows it, takes it down, keeps it over the
    /// right place and says in the log, once per change, whether the game took it. A job makes one of these for the shift
    /// and takes it down in its End.
    ///
    /// For a job that does one thing at a time on the interact key (take the parcels, mop the spill, offer a flyer) the
    /// panel is set with <see cref="Offer"/>: the first button runs the job's action by the locale key that names it,
    /// the second is the job's "let time pass". For anything else the job sets the buttons on <see cref="Panel"/>
    /// itself, as the drinks cart does. The buttons carry their key (the game writes it) and also take a click, so the
    /// mod reads no key.
    ///
    /// The cost: texts and buttons are made only when what they say changes (the panel compares them); the game puts the
    /// box over the place every frame by itself.
    /// </summary>
    public sealed class JobPanel
    {
        private readonly WorldPanel _panel = new WorldPanel();

        /// <summary>The game's white, fetched once: the colour of a line that needs no attention.</summary>
        public readonly Color32 White = WorldPanel.White();
        private readonly string _name;
        private readonly Action<string> _run;
        private readonly Action _second;
        private readonly Action _press;
        private readonly Action _pressSecond;
        private string _action;
        private int _up;   // 0 not up, 1 up, 2 refused by another panel

        /// <param name="name">What the log calls this panel ("flyer panel").</param>
        /// <param name="run">Runs the action the first button names (its locale key), when the game says it was pressed.</param>
        /// <param name="second">Runs the second button; null when the job has none.</param>
        public JobPanel(string name, Action<string> run = null, Action second = null)
        {
            _name = name;
            _run = run;
            _second = second;
            _press = Press;
            _pressSecond = PressSecond;
        }

        /// <summary>The game's panel itself, for a job that sets its own buttons.</summary>
        public WorldPanel Panel => _panel;

        /// <summary>True while the game is showing this panel: the interact key is then the panel's first button.</summary>
        public bool Up => _up == 1;

        /// <summary>
        /// The words and the one or two buttons. A button is a locale key (null leaves it out); the first is the action
        /// the job names, the second the job's own button.
        /// </summary>
        public void Offer(string first, string left, string right, Color32 rightColour, string action, bool actionOn, string secondKey, bool secondOn)
        {
            _action = action;
            _panel.Lines(first, left, right, rightColour);
            _panel.Button(0, action, action != null ? _press : null, actionOn, true);
            _panel.Button(1, secondKey, secondKey != null ? _pressSecond : null, secondOn, true);
            _panel.Button(2, null, null, false, false);
            _panel.Button(3, null, null, false, false);
        }

        private void Press()
        {
            _run?.Invoke(_action);
        }

        private void PressSecond()
        {
            _second?.Invoke();
        }

        /// <summary>Over a place in the world; over the player's head while that place is behind the camera.</summary>
        public bool Show(Vector3 over, PlayerController player)
        {
            _panel.Follow = null;
            _panel.Position = Fit(over, player);
            return Note(_panel.Show());
        }

        /// <summary>Over a person or thing that moves; the game follows it every frame. Over the player's head while it is behind the camera.</summary>
        public bool Show(Transform over, float lift, PlayerController player)
        {
            Vector3 at = over.position + Vector3.up * lift;
            Vector3 fitted = Fit(at, player);
            if (fitted == at)
            {
                _panel.Follow = over;
                _panel.FollowLift = lift;
            }
            else
            {
                _panel.Follow = null;
                _panel.Position = fitted;
            }
            return Note(_panel.Show());
        }

        private static Vector3 Fit(Vector3 over, PlayerController player)
        {
            Camera camera = GameManager.GetMainCamera();
            if (camera != null && player != null && Vector3.Dot(over - camera.transform.position, camera.transform.forward) < 0.5f)
            {
                return player.transform.position + Vector3.up * 2.1f;
            }
            return over;
        }

        private bool Note(bool shown)
        {
            int up = shown ? 1 : 2;
            if (up != _up)
            {
                _up = up;
                if (Meta.Dev.Verbose)
                {
                    JobDirector.Logger?.Info("Jobs check (" + _name + "): " + (up == 1 ? "the game's own panel is up: PASS"
                        : "FAIL (the game's panel is showing another place; it is tried again five times a second while the job wants it)") + ".");
                }
            }
            return shown;
        }

        /// <summary>Takes the panel down. Safe at any time, and when it is not up.</summary>
        public void Hide()
        {
            if (_up != 0)
            {
                _up = 0;
                _panel.Follow = null;
                _panel.Hide();
            }
        }
    }
}
