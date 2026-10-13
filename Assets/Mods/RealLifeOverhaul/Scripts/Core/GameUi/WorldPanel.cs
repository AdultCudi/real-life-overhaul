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
    /// The game's building box (the card over a shop's door while the pointer is on the shop) is another object, and
    /// the two would cover each other where a panel's place is in front of a shop. The other half of this class
    /// (WorldPanel.Beside.cs) keeps every panel clear of it by one rule (<see cref="PanelPlacement"/>).
    ///
    /// The cost: while no panel of the mod's is up, nothing. While one is up, the few loads of WorldPanel.Beside.cs
    /// each frame and no more. Texts and buttons are objects made when what they say changes; a
    /// change of the lines alone rewrites the three texts (the game's private SetUpHeader, called through a delegate
    /// made once), and only a change of the buttons has the game make its buttons again.
    /// </summary>
    public sealed partial class WorldPanel : IOverlay
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
        /// <summary>The most buttons a panel has: one for each key the game sends to its box (interact, secondary, special, confirm).</summary>
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

        /// <summary>The place in the world the box stands over. Read by the game every frame while the box is up.</summary>
        public Vector3 Position;

        /// <summary>
        /// A thing that moves (a passer-by) the box follows instead of <see cref="Position"/>: the game reads the position every
        /// frame, so the box stays on them without the mod doing anything per frame. Null: <see cref="Position"/> is used.
        /// </summary>
        public Transform Follow;

        /// <summary>Metres above <see cref="Follow"/> the box stands.</summary>
        public float FollowLift;

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
            Button(0, keyA, a, enabledA, true);
            Button(1, keyB, b, enabledB, false);
            Button(2, null, null, false, false);
            Button(3, null, null, false, false);
        }

        /// <summary>
        /// One button by its place: 0 on the game's interact key, 1 on its secondary one, 2 on its special one, 3 on
        /// its confirm key. They stand in that order in the box. A key is a locale key; null leaves the place empty.
        /// Nothing is made unless the place differs from what it held: the buttons are put together again by the
        /// next <see cref="Show"/>. Pass a delegate made once.
        /// </summary>
        public void Button(int slot, string key, Action action, bool enabled, bool blue)
        {
            Button(slot, key, null, action, enabled, blue);
        }

        /// <summary>
        /// A button whose words are a locale key filled from <paramref name="arguments"/> (an object with the key's placeholders as
        /// members, as the game's own buttons take them). Keep the same object while the words are the same: a different one makes
        /// the game's buttons again.
        /// </summary>
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
                        ? new ButtonInfo(SlotName[i], _slotKey[i], _slotArgs[i], _slotBlue[i] ? "blue" : "gray", _slotAction[i], SlotAction[i], _slotOn[i])
                        : new ButtonInfo(SlotName[i], _slotKey[i], _slotBlue[i] ? "blue" : "gray", _slotAction[i], SlotAction[i], _slotOn[i]);
                }
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
                        OverlayUI.Show(this);   // the whole box again: the same result, with the buttons made anew
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
            if (OverlayUI.IsVisible)
            {
                return false;
            }
            if (Held())
            {
                Waiting = true;    // the game's building box is in the way and no side of it has room: tried again by the owner's timer
                return false;
            }
            _current = this;
            _linesChanged = false;
            _buttonsChanged = false;
            OverlayUI.Show(this);
            Watch(true);
            return true;
        }

        /// <summary>Takes the panel out of the game's box. Safe at any time, and when the interface has already gone.</summary>
        public void Hide()
        {
            Waiting = false;
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
                    GameContract.OverlayVisible.Setter?.Invoke(null, new object[] { false });   // (its private setter, resolved once: Core/GameContract)
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
            // (The step aside from the game's building box is in the world's own measure, so the game's own
            // WorldToScreenPoint puts the box beside it: WorldPanel.Beside.cs.)
            return ReferenceEquals(_current, this) ? Home() + _shift : Home();
        }

        // The place the panel stands over, before any step aside.
        private Vector3 Home()
        {
            return Follow != null ? Follow.position + Vector3.up * FollowLift : Position;
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
                    MethodInfo method = GameContract.OverlayHeader.Method;   // (resolved once: Core/GameContract)
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
