using System;
using System.Reflection;
using BAModAPI;
using UI;
using UI.Elements;
using UI.Overlays;
using UnityEngine;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// The game's own panel over a place in the world: the dark box with an arrow that stands over a petrol pump or a
    /// lift (Canvases/PlayerHUD/OverlayUI, class UI.Overlays.OverlayUI), with a first line, a second line left and
    /// right, and buttons that carry their key in brackets. The game shows whatever implements its IOverlay, so this
    /// class is one: nothing is copied and nothing is drawn by the mod. The game itself puts the box over the place
    /// every frame, writes the key of each button after its words (the player's own binding), and runs a button on
    /// its key (GameManager routes Interact, SecondaryInteract and SpecialInteract to OverlayUI.OnPlayerActionPressed)
    /// or on a click.
    ///
    /// The game has one such box. If it is showing one of its own (a filling station, a lift) or another of the
    /// mod's, <see cref="Show"/> does nothing and says so: the one that was there first keeps the place.
    ///
    /// The cost: nothing per frame of the mod's. Texts and buttons are objects made when what they say changes; a
    /// change of the lines alone rewrites the three texts (the game's private SetUpHeader, called through a delegate
    /// made once), and only a change of the buttons has the game make its buttons again.
    /// </summary>
    public sealed class WorldPanel : IOverlay
    {
        public static IModLogger Logger;

        private static WorldPanel _current;            // the one last handed to the game
        private static Action<OverlayUI> _setUpHeader; // OverlayUI.SetUpHeader: the lines alone, the buttons left as they are
        private static bool _looked;

        private LabelInfo _first;
        private LabelInfo _left;
        private LabelInfo _right;
        private string _firstWords;
        private string _leftWords;
        private string _rightWords;
        private Color32 _rightColour;
        private ButtonInfo[] _buttons;
        private string _keyA;
        private string _keyB;
        private bool _onA;
        private bool _onB;
        private bool _linesChanged;
        private bool _buttonsChanged;

        /// <summary>The place in the world the box stands over. Read by the game every frame while the box is up.</summary>
        public Vector3 Position;

        /// <summary>True while this panel is the one in the game's box. Two loads: fit for a per-frame test.</summary>
        public bool Showing => _current == this && OverlayUI.IsVisible;

        /// <summary>The three lines, as final words (null leaves a line out). Nothing is made unless a line differs from what is there.</summary>
        public void Lines(string first, string left, string right, Color32 rightColour)
        {
            if (!Same(first, _firstWords))
            {
                _firstWords = first;
                _first = first != null ? new LabelInfo(first, White(), false) : null;
                _linesChanged = true;
            }
            if (!Same(left, _leftWords))
            {
                _leftWords = left;
                _left = left != null ? new LabelInfo(left, White(), false) : null;
                _linesChanged = true;
            }
            if (!Same(right, _rightWords) || rightColour.r != _rightColour.r || rightColour.g != _rightColour.g || rightColour.b != _rightColour.b || rightColour.a != _rightColour.a)
            {
                _rightWords = right;
                _rightColour = rightColour;
                _right = right != null ? new LabelInfo(right, rightColour, false) : null;
                _linesChanged = true;
            }
        }

        /// <summary>
        /// Up to two buttons: the first on the game's interact key, the second on its secondary one. A key is a locale
        /// key (the game's component writes and translates it); null leaves the button out. The actions are kept as
        /// given: pass delegates made once.
        /// </summary>
        public void Buttons(string keyA, Action a, bool enabledA, string keyB, Action b, bool enabledB)
        {
            if (ReferenceEquals(keyA, _keyA) && ReferenceEquals(keyB, _keyB) && enabledA == _onA && enabledB == _onB)
            {
                return;
            }
            _keyA = keyA;
            _keyB = keyB;
            _onA = enabledA;
            _onB = enabledB;
            int count = (keyA != null ? 1 : 0) + (keyB != null ? 1 : 0);
            _buttons = count > 0 ? new ButtonInfo[count] : null;
            int at = 0;
            if (keyA != null)
            {
                _buttons[at++] = new ButtonInfo("RealLifeFirst", keyA, "blue", a, PlayerAction.Interact, enabledA);
            }
            if (keyB != null)
            {
                _buttons[at] = new ButtonInfo("RealLifeSecond", keyB, "gray", b, PlayerAction.SecondaryInteract, enabledB);
            }
            _buttonsChanged = true;
        }

        /// <summary>
        /// Puts the panel in the game's box, or brings the box up to date with what was set since. False when the
        /// game's interface is not there or the box is taken by another panel.
        /// </summary>
        public bool Show()
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            OverlayUI box = uis != null ? uis.overlayUI : null;
            if (box == null)
            {
                return false;
            }
            if (_current == this && OverlayUI.IsVisible && box.GetCurrentOverlayType == typeof(WorldPanel))
            {
                if (_buttonsChanged)
                {
                    _buttonsChanged = false;
                    box.RefreshButtons();
                }
                if (_linesChanged)
                {
                    _linesChanged = false;
                    Action<OverlayUI> header = HeaderCall();
                    if (header != null)
                    {
                        header(box);
                    }
                    else
                    {
                        OverlayUI.Show(this);   // the whole box again: the same result, with the buttons made anew
                    }
                }
                return true;
            }
            if (_current == this)
            {
                _current = null;   // the game put one of its own in the box since
            }
            if (OverlayUI.IsVisible)
            {
                return false;
            }
            _current = this;
            _linesChanged = false;
            _buttonsChanged = false;
            OverlayUI.Show(this);
            return true;
        }

        /// <summary>Takes the panel out of the game's box. Safe at any time, and when the interface has already gone.</summary>
        public void Hide()
        {
            if (_current != this)
            {
                return;
            }
            _current = null;
            try
            {
                UIs uis = InstanceBehavior<UIs>.Instance;
                OverlayUI box = uis != null ? uis.overlayUI : null;
                if (box != null)
                {
                    if (box.GetCurrentOverlayType == typeof(WorldPanel))
                    {
                        OverlayUI.Hide(this);
                    }
                }
                else if (OverlayUI.IsVisible)
                {
                    // The interface went first (the city is unloading): the game's flag must not be left saying a box is up.
                    MethodInfo set = typeof(OverlayUI).GetProperty("IsVisible", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)?.GetSetMethod(true);
                    set?.Invoke(null, new object[] { false });
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // ---------------------------------------------------------------- what the game asks

        Vector3 IOverlay.GetTargetPosition()
        {
            return Position;
        }

        LabelInfo IOverlay.GetFirstLineLabel()
        {
            return _first;
        }

        LabelInfo IOverlay.GetSecondLineLeftLabel()
        {
            return _left;
        }

        LabelInfo IOverlay.GetSecondLineRightLabel()
        {
            return _right;
        }

        ButtonInfo[] IOverlay.GetButtons()
        {
            return _buttons;
        }

        // ---------------------------------------------------------------- helpers

        private static bool Same(string a, string b)
        {
            return ReferenceEquals(a, b) || string.Equals(a, b, StringComparison.Ordinal);
        }

        /// <summary>The game's own colours for the right-hand word; white if its references are not there.</summary>
        public static Color32 White()
        {
            GlobalReferences references = InstanceBehavior<GlobalReferences>.Instance;
            return references != null ? references.colors.white : new Color32(255, 255, 255, 255);
        }

        public static Color32 Green()
        {
            GlobalReferences references = InstanceBehavior<GlobalReferences>.Instance;
            return references != null ? references.colors.green : new Color32(0, 165, 0, 255);
        }

        public static Color32 Red()
        {
            GlobalReferences references = InstanceBehavior<GlobalReferences>.Instance;
            return references != null ? references.colors.red : new Color32(230, 60, 50, 255);
        }

        public static Color32 Yellow()
        {
            GlobalReferences references = InstanceBehavior<GlobalReferences>.Instance;
            return references != null ? references.colors.yellow : new Color32(255, 200, 60, 255);
        }

        private static Action<OverlayUI> HeaderCall()
        {
            if (!_looked)
            {
                _looked = true;
                try
                {
                    MethodInfo method = typeof(OverlayUI).GetMethod("SetUpHeader", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null, Type.EmptyTypes, null);
                    _setUpHeader = method != null ? (Action<OverlayUI>)Delegate.CreateDelegate(typeof(Action<OverlayUI>), method, false) : null;
                }
                catch (Exception e)
                {
                    _setUpHeader = null;
                    Logger?.Error(e);
                }
                if (_setUpHeader == null)
                {
                    UiClone.Fallback("the game's panel cannot rewrite its lines alone, so a change of a line makes its buttons again as well");
                }
            }
            return _setUpHeader;
        }
    }
}
