using System;
using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// A job's use of the game's panel over a place (<see cref="WorldPanel"/>): <see cref="Offer"/> puts one action on the
    /// interact key, or a job sets its own buttons on <see cref="Panel"/>; the game runs them on its keys or a click.
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
        /// <param name="run">Runs the action the first button names (its locale key) when it is pressed.</param>
        /// <param name="second">Runs the second button; null when the job has none.</param>
        public JobPanel(string name, Action<string> run = null, Action second = null)
        {
            _name = name;
            _panel.Name = name;
            _panel.KeepInView = true;   // the game's camera sees little of a shop and indoors hides its guider: off the screen nothing would say where
            _run = run;
            _second = second;
            _press = Press;
            _pressSecond = PressSecond;
        }

        /// <summary>The game's panel itself, for a job that sets its own buttons.</summary>
        public WorldPanel Panel => _panel;

        /// <summary>True while the game is showing this panel: the interact key is then the panel's first button.</summary>
        public bool Up => _up == 1;

        /// <summary>The words and the one or two buttons, each a locale key (null leaves it out): the job's action, then its own second button.</summary>
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

        /// <summary>Over a place in the world; at the screen's edge toward it while it is out of the picture (<see cref="WorldPanel.KeepInView"/>).</summary>
        public bool Show(Vector3 over)
        {
            _panel.Follow = null;
            _panel.For(over);   // measured from the place itself when another panel wants the game's box
            _panel.Position = over;
            return Note(_panel.Show());
        }

        /// <summary>Over a person or thing that moves, followed by the game every frame; at the screen's edge toward it while it is out of the picture.</summary>
        public bool Show(Transform over, float lift)
        {
            _panel.For(over.position);
            _panel.Follow = over;
            _panel.FollowLift = lift;
            return Note(_panel.Show());
        }

        /// <summary>True while the panel's place is out of the picture and the panel stands at the screen's edge toward it. For a self-check line.</summary>
        public bool AtEdge => _panel.AtEdge;

        private bool Note(bool shown)
        {
            int up = shown ? 1 : 2;
            if (up != _up)
            {
                _up = up;
                if (Meta.Dev.Verbose)
                {
                    JobDirector.Logger?.Info("Jobs check (" + _name + "): " + (up == 1 ? "the game's own panel is up: PASS"
                        : "FAIL (another panel has the game's box and keeps it by the rule; it is tried again five times a second while the job wants it)") + ".");
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
