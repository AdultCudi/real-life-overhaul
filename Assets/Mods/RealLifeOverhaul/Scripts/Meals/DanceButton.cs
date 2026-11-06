using System;
using BAModAPI;
using Player.HUD.ItemInfoOverlays;
using RealLifeOverhaul.GameUi;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Meals
{
    /// <summary>
    /// "Dance" in the game's own panel over a loudspeaker (its radio panel, RadioDetailedOverlay): a copy of the panel's
    /// own radio button, made once per city and put after it. This component sits on the panel and shows the button at
    /// each opening while music plays in a place of the player's own; it reads "Stop dancing" while the player dances to it.
    /// Nothing runs per frame: one look each time the game switches the panel on.
    /// </summary>
    public sealed class DanceButton : MonoBehaviour
    {
        public static IModLogger Logger;

        private static DanceButton _instance;
        private static bool _failed;

        private RadioOverlay _panel;
        private GameObject _button;
        private Component _label;
        private bool _saysStop;

        /// <summary>Puts the button in the game's radio panel, once per city. False if the panel is not built as expected (said once in Player.log).</summary>
        public static bool Install()
        {
            if (_instance != null)
            {
                return true;
            }
            if (_failed)
            {
                return false;
            }
            try
            {
                OverlayManager overlays = InstanceBehavior<OverlayManager>.Instance;
                RadioOverlay panel = overlays != null ? overlays.GetComponentInChildren<RadioOverlay>(true) : null;
                if (panel == null)
                {
                    return false;   // the interface is not there yet: asked again a second later
                }
                Transform list = panel.transform;
                Transform original = list.Find("ToggleRadioButton");
                if (original == null || original.GetComponent<Button>() == null)
                {
                    _failed = true;
                    UiClone.Fallback("the game's radio panel has no button to copy, so dancing to the music is not offered");
                    return false;
                }
                using var piece = SessionWatch.Piece("game UI: the radio panel's dance button being copied from the game's own");
                Transform holder = UiClone.Dormant(list, "RealLifeOverhaul Dance Holder");
                GameObject copy = UiClone.Asleep(original.gameObject, holder, "RealLifeOverhaul Dance");
                UiClone.Rewire(copy.transform, Clicked);
                Component label = copy.GetComponentInChildren<TMPro.TMP_Text>(true);
                UiClone.Key(label, MealsText.ButtonDance);
                copy.SetActive(false);
                copy.transform.SetParent(list, false);
                copy.transform.SetSiblingIndex(original.GetSiblingIndex() + 1);
                DestroyImmediate(holder.gameObject);

                _instance = panel.gameObject.AddComponent<DanceButton>();
                _instance._panel = panel;
                _instance._button = copy;
                _instance._label = label;
                _instance.Decide();
                if (Meta.Dev.Verbose)
                {
                    Logger?.Info("Meals check (dance): 'Dance' is a button in the game's radio panel, copied from its " + original.name + ": PASS.");
                }
                return true;
            }
            catch (Exception e)
            {
                _failed = true;
                Logger?.Error(e);
                UiClone.Fallback("the dance button could not be put in the game's radio panel, so dancing to the music is not offered");
                return false;
            }
        }

        public static void Remove()
        {
            try
            {
                if (_instance != null)
                {
                    if (_instance._button != null)
                    {
                        Destroy(_instance._button);
                    }
                    Destroy(_instance);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            _instance = null;
            _failed = false;
        }

        // The game has just filled the panel and switched it on.
        private void OnEnable()
        {
            Decide();
        }

        private void Decide()
        {
            try
            {
                if (_button == null || _panel == null)
                {
                    return;
                }
                MealsDirector director = MealsDirector.Instance;
                bool dancing = director != null && director.DancingNow;
                bool show = Settings.MealsDance && director != null && _panel.linkedController is LoudspeakerController && MealsGame.OwnPlace()
                            && (dancing || MealsGame.MusicPlaying());
                if (show && dancing != _saysStop)
                {
                    _saysStop = dancing;
                    UiClone.Key(_label, dancing ? MealsText.ButtonStop : MealsText.ButtonDance);
                }
                if (_button.activeSelf != show)
                {
                    _button.SetActive(show);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                enabled = false;
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private static void Clicked()
        {
            MealsGame.HidePanel();
            MealsDirector.Instance?.DanceClicked();
        }
    }
}
