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
    /// The game's own panel over a place in the world (OverlayUI: a first line, a second left and right, buttons with their
    /// key): this class is an IOverlay, so nothing is copied or drawn. Which panel has the one box is WorldPanel.Pick's,
    /// keeping it clear of the building box WorldPanel.Beside's. Texts and buttons are rebuilt only when what they say
    /// changes. Costs nothing while no panel is up.
    /// </summary>
    public sealed partial class WorldPanel : IOverlay
    {
        private static readonly FeatureGuard FrameGuard = Guard.For(null, "WorldPanel", typeof(WorldPanel));   // the game's calls of it

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
        /// <summary>The most buttons a panel has: one per key the game sends to its box (interact, secondary, special, confirm).</summary>
        public const int Slots = 4;

        private static readonly PlayerAction[] SlotAction = { PlayerAction.Interact, PlayerAction.SecondaryInteract, PlayerAction.SpecialInteract, PlayerAction.Confirm };
        private static readonly string[] SlotName = { "RealLifeFirst", "RealLifeSecond", "RealLifeThird", "RealLifeFourth" };

        private ButtonInfo[] _buttons;
        private readonly string[] _slotKey = new string[Slots];
        private readonly object[] _slotArgs = new object[Slots];
        private readonly Action[] _slotAction = new Action[Slots];
        private readonly bool[] _slotOn = new bool[Slots];
        private readonly bool[] _slotBlue = new bool[Slots];
        private bool _slotsChanged;      // a slot differs from the buttons last put together
        private bool _linesChanged;
        private bool _buttonsChanged;

        /// <summary>The place the box stands over; the game reads it every frame while the box is up.</summary>
        public Vector3 Position;

        /// <summary>A moving thing (a passer-by) the box follows instead of <see cref="Position"/>; null for none. The game reads it every frame.</summary>
        public Transform Follow;

        /// <summary>Metres above <see cref="Follow"/> the box stands.</summary>
        public float FollowLift;

        /// <summary>This panel is the one in the game's box. Two loads: fit for a test every frame.</summary>
        public bool Showing => _current == this && OverlayUI.IsVisible;

        /// <summary>The three lines in final words (null leaves a line out). Nothing is made unless a line differs.</summary>
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

        /// <summary>Up to two buttons, on the interact and secondary keys (locale keys; null leaves one out). Pass delegates made once.</summary>
        public void Buttons(string keyA, Action a, bool enabledA, string keyB, Action b, bool enabledB)
        {
            Button(0, keyA, a, enabledA, true);
            Button(1, keyB, b, enabledB, false);
            Button(2, null, null, false, false);
            Button(3, null, null, false, false);
        }

        /// <summary>One button by place (0 interact, 1 secondary, 2 special, 3 confirm); null leaves it empty. Rebuilt by the next <see cref="Show"/> if changed.</summary>
        public void Button(int slot, string key, Action action, bool enabled, bool blue)
        {
            Button(slot, key, null, action, enabled, blue);
        }

        /// <summary>A button whose key is filled from <paramref name="arguments"/>; keep the same object while the words stay, another remakes the buttons.</summary>
        public void Button(int slot, string key, object arguments, Action action, bool enabled, bool blue)
        {
            if (ReferenceEquals(key, _slotKey[slot]) && (key == null || (enabled == _slotOn[slot] && blue == _slotBlue[slot] && ReferenceEquals(action, _slotAction[slot])
                                                                           && ReferenceEquals(arguments, _slotArgs[slot]))))
            {
                return;
            }
            _slotArgs[slot] = arguments;
            _slotKey[slot] = key;
            _slotAction[slot] = action;
            _slotOn[slot] = enabled;
            _slotBlue[slot] = blue;
            _slotsChanged = true;
        }

        // The game's button list from the four places: made only when a place has changed.
        private void Assemble()
        {
            _slotsChanged = false;
            int count = 0;
            for (int i = 0; i < Slots; i++)
            {
                count += _slotKey[i] != null ? 1 : 0;
            }
            _buttons = count > 0 ? new ButtonInfo[count] : null;
            for (int i = 0, at = 0; i < Slots; i++)
            {
                if (_slotKey[i] != null)
                {
                    _buttons[at++] = _slotArgs[i] != null
                        ? new ButtonInfo(SlotName[i], _slotKey[i], _slotArgs[i], _slotBlue[i] ? "blue" : "gray", Guard.Wrap(_slotAction[i]), SlotAction[i], _slotOn[i])
                        : new ButtonInfo(SlotName[i], _slotKey[i], _slotBlue[i] ? "blue" : "gray", Guard.Wrap(_slotAction[i]), SlotAction[i], _slotOn[i]);
                }
            }
            _buttonsChanged = true;
        }

        /// <summary>Puts the panel in the game's box or brings it up to date; false without the interface, or when another panel keeps the box.</summary>
        public bool Show()
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            OverlayUI box = uis != null ? uis.overlayUI : null;
            if (box == null)
            {
                return false;
            }
            if (_slotsChanged)
            {
                Assemble();
            }
            if (_current == this && OverlayUI.IsVisible && box.GetCurrentOverlayType == typeof(WorldPanel))
            {
                if (_buttonsChanged)
                {
                    _buttonsChanged = false;
                    _measureAgain = true;   // the box is another size now
                    box.RefreshButtons();
                }
                if (_linesChanged)
                {
                    _linesChanged = false;
                    _measureAgain = true;
                    Action<OverlayUI> header = HeaderCall();
                    if (header != null)
                    {
                        header(box);
                    }
                    else
                    {
                        OverlayUI.Show(this);   // the whole box again: the same, with the buttons made anew
                    }
                }
                return true;
            }
            if (_current == this)
            {
                _current = null;   // the game put one of its own in the box since
                Watch(false);
            }
            Waiting = false;
            if (OverlayUI.IsVisible && !Contest(box))
            {
                return false;   // another panel keeps the box by the rule: the owner's timer asks again
            }
            if (Held())
            {
                Waiting = true;    // the building box is in the way with no room beside it: the owner's timer asks again
                return false;
            }
            _current = this;
            _linesChanged = false;
            _buttonsChanged = false;
            OverlayUI.Show(this);
            Watch(true);
            return true;
        }

        /// <summary>Takes the panel out of the game's box. Safe at any time, also after the interface has gone.</summary>
        public void Hide()
        {
            Waiting = false;
            Untell();
            if (_current != this)
            {
                return;
            }
            _current = null;
            Watch(false);
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
                    GameContract.OverlayVisible.Setter?.Invoke(null, new object[] { false });   // its private setter, resolved once
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        Vector3 IOverlay.GetTargetPosition()
        {
            if (FrameGuard.Off)
            {
                return Vector3.zero;
            }
            try
            {
                // The step aside from the building box is in the world's measure, so the game's WorldToScreenPoint puts the box beside it.
                return ReferenceEquals(_current, this) ? Home() + _shift : Home();
            }
            catch (Exception fault)
            {
                return Guard.Failed(FrameGuard, fault, Vector3.zero);
            }
        }

        // The place before any step aside; for a panel kept in view, the picture's edge toward a place out of it.
        private Vector3 Home()
        {
            Vector3 at = Unmoved();
            return KeepInView && InView(at, out Vector3 shown) ? shown : at;
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

        private static bool Same(string a, string b)
        {
            return ReferenceEquals(a, b) || string.Equals(a, b, StringComparison.Ordinal);
        }

        /// <summary>The game's own colours for the right-hand words; fixed ones without its references.</summary>
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
                    MethodInfo method = GameContract.OverlayHeader.Method;
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
