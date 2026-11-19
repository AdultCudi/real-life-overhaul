using System;
using System.Collections.Generic;
using RealLifeOverhaul.GameUi;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The private driver button on the game's own top bar, for the old interface (the bar of the RLO interface has its own,
    /// HudDriver): a copy of the top bar's help button, the control itself with its picture swapped for the game's car marker, put
    /// in the row of the bar's buttons. It shows while the player has a driver contract and the RLO interface's HUD is not
    /// standing over the bar, takes the same press, the Driver key and the state colours as the kit's, and says the state and the
    /// key in the game's own tooltip. Built once when the city is ready and given back with it; read on the interface director's
    /// slow tick (a few field reads) and never a frame, but for one key test while the button shows. A fault removes it for the
    /// session.
    /// </summary>
    internal static class DriverButton
    {
        private const float Gap = 8f;   // units of the game's interface: from the button to its neighbour

        private static readonly Color Rest = Color.white;
        private static readonly Color Coming = new Color(0.96f, 0.66f, 0.18f);
        private static readonly Color Waiting = new Color(0.32f, 0.78f, 0.46f);
        private static readonly Color Riding = new Color(0.40f, 0.60f, 0.98f);

        private static GameObject _piece;
        private static Image _icon;
        private static BasicTooltip _tip;
        private static DriverKeys _keys;
        private static DriverState _state = DriverState.None;
        private static int _cars = -1;
        private static string _car;
        private static bool _failed;
        private static bool _open;

        /// <summary>True while the button stands on the bar.</summary>
        public static bool Shown => _piece != null && _piece.activeSelf;

        /// <summary>
        /// The interface director's slow tick: built the first time the bar is there, shown while a contract stands and the kit's HUD
        /// is not over the bar, its colour and words moved only when the state did.
        /// </summary>
        public static void Tick()
        {
            if (_failed)
            {
                return;
            }
            try
            {
                if (HudLive.Live)
                {
                    Hide();   // the kit's bar has the button
                    return;
                }
                if (_piece == null && !Build())
                {
                    _failed = true;
                    return;
                }
                bool shows = HudDriver.Look(out DriverState state, out bool open);
                _open = open;
                if (state != _state || HudDriver.Cars != _cars || HudDriver.CarName() != _car)
                {
                    _state = state;
                    _cars = HudDriver.Cars;
                    _car = HudDriver.CarName();
                    Paint(state);
                }
                if (_piece.activeSelf != shows)
                {
                    _piece.SetActive(shows);
                }
                if (_keys != null && _keys.enabled != shows)
                {
                    _keys.enabled = shows;
                }
            }
            catch (Exception e)
            {
                UiDirector.Logger?.Error(e);
                Remove();
                _failed = true;
            }
        }

        /// <summary>The city is unloading or the interface took the bar: the button goes with the bar's copy of it.</summary>
        public static void Remove()
        {
            if (_piece != null)
            {
                UnityEngine.Object.Destroy(_piece);
            }
            _piece = null;
            _icon = null;
            _tip = null;
            _keys = null;
            _state = DriverState.None;
            _cars = -1;
            _car = null;
            _failed = false;
            _open = false;
        }

        private static void Hide()
        {
            if (_piece != null && _piece.activeSelf)
            {
                _piece.SetActive(false);
            }
            if (_keys != null)
            {
                _keys.enabled = false;
            }
        }

        // The colour of the state on the picture, and the tooltip's words with the key as bound now and the car's name.
        private static void Paint(DriverState state)
        {
            _icon.color = state == DriverState.Coming ? Coming : state == DriverState.Waiting ? Waiting : state == DriverState.Driving ? Riding : Rest;
            if (_tip != null)
            {
                _tip.titleKey = null;
                _tip.descriptionKey = HudDriverRules.NoteKey(state, _cars);
                _tip.localizationArguments = new Dictionary<string, string>(2) { { "key", Keys.Label(KeyAction.Driver) }, { "car", _car ?? string.Empty } };
            }
        }

        private static bool Build()
        {
            UIs uis = InstanceBehavior<UIs>.IsInitialized ? InstanceBehavior<UIs>.Instance : null;
            Transform container = uis != null && uis.topBar != null && uis.topBar.container != null ? uis.topBar.container.transform : null;
            Transform row = UiClone.Need(container, "Buttons", "the private driver button");
            Transform help = UiClone.Need(row, "HelpButton", "the private driver button");
            Sprite car = InstanceBehavior<GlobalReferences>.Instance != null ? InstanceBehavior<GlobalReferences>.Instance.vehiclePOIIcon : null;
            if (help == null || help.GetComponent<Button>() == null || car == null)
            {
                UiClone.Fallback("the game's top bar has no help button or the game no car marker as expected, so the private driver has no button there (the phone's still has)");
                return false;
            }
            Transform holder = UiClone.Dormant(row, "RealLifeOverhaul Driver Holder");
            try
            {
                GameObject copy = UiClone.Asleep(help.gameObject, holder, "RealLifeOverhaul Driver");
                UiClone.StripGameScripts(copy, m => m is BasicTooltip);
                Image[] pictures = copy.GetComponentsInChildren<Image>(true);
                Image icon = pictures.Length > 1 ? pictures[1] : pictures.Length > 0 ? pictures[0] : null;   // the picture inside the button, or the button's own
                if (icon == null)
                {
                    UiClone.Fallback("the game's help button has no picture to swap, so the private driver has no button there");
                    UnityEngine.Object.DestroyImmediate(holder.gameObject);
                    return false;
                }
                foreach (TMPro.TMP_Text label in copy.GetComponentsInChildren<TMPro.TMP_Text>(true))
                {
                    UnityEngine.Object.DestroyImmediate(label.gameObject);   // the control only: no words of the help button's
                }
                icon.sprite = car;
                icon.preserveAspect = true;
                icon.color = Rest;
                Button button = UiClone.Rewire(copy.transform, Pressed);
                var rect = (RectTransform)copy.transform;
                rect.SetParent(row, false);
                rect.SetSiblingIndex(help.GetSiblingIndex() + 1);
                var helpRect = (RectTransform)help;
                if (row.GetComponent<LayoutGroup>() == null)
                {
                    rect.anchoredPosition = helpRect.anchoredPosition - new Vector2(helpRect.rect.width + Gap, 0f);   // no layout moves it: beside the help button, to its left
                }
                _tip = copy.GetComponent<BasicTooltip>();
                _icon = icon;
                _piece = copy;
                _piece.SetActive(false);
                _keys = copy.AddComponent<DriverKeys>();
                _keys.enabled = false;
                UnityEngine.Object.DestroyImmediate(holder.gameObject);
                return button != null;
            }
            catch (Exception e)
            {
                UiDirector.Logger?.Error(e);
                if (holder != null)
                {
                    UnityEngine.Object.DestroyImmediate(holder.gameObject);
                }
                UiClone.Fallback("the private driver button could not be put on the game's top bar");
                return false;
            }
        }

        // A click: the same press as the kit's button and its key.
        private static void Pressed()
        {
            if (!HudDriver.Press())
            {
                UiGame.Instance?.Play(UiCue.Error);
            }
        }
    }

    /// <summary>The Driver key on the old interface: one key test a frame while the button shows, guarded; it is on the button's own object, so it goes with it.</summary>
    internal sealed class DriverKeys : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("ui", "DriverKeys", typeof(DriverKeys));
        private static readonly int Hot = HotSpots.Slot("Ui: DriverKeys.Update");

        private void Update()
        {
            using var hotSpot = HotSpots.Time(Hot);
            if (FrameGuard.Off)
            {
                enabled = false;
                return;
            }
            try
            {
                HudDriver.Frame();
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }
    }
}
