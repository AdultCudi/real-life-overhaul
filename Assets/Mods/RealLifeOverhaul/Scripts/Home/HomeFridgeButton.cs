using System;
using BAModAPI;
using Player.HUD.ItemInfoOverlays;
using RealLifeOverhaul.GameUi;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul
{
    /// <summary>
    /// "Cook a meal" as a button in the game's own fridge panel: a copy of its ChangeClothesButton, made once per city. This
    /// component sits on the panel, which leaves children it does not know alone, and shows the button at each opening for
    /// a fridge of the player's home. A second copy, "Family", opens the family's board, a way in beside the Family contact
    /// (Family/FamilyPhone.BoardShown, through Wiring/FamilyLinks). Nothing runs per frame.
    /// </summary>
    public sealed class HomeFridgeButton : MonoBehaviour
    {
        public const string TextKey = "reallife_home_fridge_cook";
        public const string FamilyKey = "reallife_home_fridge_family";

        /// <summary>Set by Wiring/FamilyLinks: whether the Family button shows, and what it opens.</summary>
        public static Func<bool> FamilyShown;
        public static Action FamilyClicked;

        public static IModLogger Logger;

        private static HomeFridgeButton _instance;
        private static bool _failed;

        private DecorativeItemHolderOverlay _panel;
        private GameObject _button;
        private GameObject _family;

        /// <summary>Puts the button in the game's fridge panel, once per city. False if the panel is not built as expected (said once in Player.log).</summary>
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
                DecorativeItemHolderOverlay panel = overlays != null ? overlays.GetComponentInChildren<DecorativeItemHolderOverlay>(true) : null;
                if (panel == null)
                {
                    return false;   // the interface is not there yet: asked again on the next look at the home
                }
                Transform list = panel.transform;
                Transform original = list.Find("ChangeClothesButton") ?? list.Find("EmptyFridgeButton");
                if (original == null || original.GetComponent<Button>() == null)
                {
                    _failed = true;
                    UiClone.Fallback("the game's fridge panel has no button to copy, so cooking is offered on the cookers only");
                    return false;
                }
                using var piece = SessionWatch.Piece("game UI: the fridge panel's cooking button being copied from the game's own");
                Transform items = list.Find("ConsumeButtons");
                GameObject copy = Copy(original, list, "RealLifeOverhaul Cook", TextKey, Clicked, items != null ? items.GetSiblingIndex() + 1 : 0);
                GameObject family = Copy(original, list, "RealLifeOverhaul Family", FamilyKey, FamilyPressed, copy.transform.GetSiblingIndex() + 1);

                _instance = panel.gameObject.AddComponent<HomeFridgeButton>();
                _instance._panel = panel;
                _instance._button = copy;
                _instance._family = family;
                _instance.Decide();
                if (Meta.Dev.Verbose)
                {
                    Logger?.Info("Home check (offer): 'Cook a meal' is a button in the game's fridge panel, copied from its " + original.name + ": PASS.");
                }
                return true;
            }
            catch (Exception e)
            {
                _failed = true;
                Logger?.Error(e);
                UiClone.Fallback("the cooking button could not be put in the game's fridge panel, so cooking is offered on the cookers only");
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
                    if (_instance._family != null)
                    {
                        Destroy(_instance._family);
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
                var fridge = _panel.linkedController as FridgeController;
                bool home = fridge != null && HomeDirector.Instance != null && HomeTidy.AtHome() && HomePlace.Fridges.Contains(fridge);
                bool show = home && Settings.HomeLife && Settings.HomeRoutines;
                if (_button.activeSelf != show)
                {
                    _button.SetActive(show);
                }
                bool family = home && FamilyShown != null && FamilyShown();
                if (_family != null && _family.activeSelf != family)
                {
                    _family.SetActive(family);
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

        // A copy of the game's button, asleep until it is wired, put in the panel's list at the place given.
        private static GameObject Copy(Transform original, Transform list, string name, string key, Action click, int at)
        {
            Transform holder = UiClone.Dormant(list, name + " Holder");
            GameObject copy = UiClone.Asleep(original.gameObject, holder, name);
            UiClone.Rewire(copy.transform, click);
            UiClone.Key(copy.GetComponentInChildren<TMPro.TMP_Text>(true), key);
            copy.SetActive(false);
            copy.transform.SetParent(list, false);
            copy.transform.SetSiblingIndex(at);
            DestroyImmediate(holder.gameObject);
            return copy;
        }

        private static void FamilyPressed()
        {
            OverlayManager overlays = InstanceBehavior<OverlayManager>.Instance;
            if (overlays != null)
            {
                overlays.HideDetailedOverlay();
            }
            FamilyClicked?.Invoke();
        }

        private static void Clicked()
        {
            OverlayManager overlays = InstanceBehavior<OverlayManager>.Instance;
            if (overlays != null)
            {
                overlays.HideDetailedOverlay();   // as the game's own buttons of this panel do
            }
            HomeDirector.Instance?.OfferClicked(HomeWish.Cook, _instance != null && _instance._panel != null ? _instance._panel.linkedController as ItemController : null);
        }
    }
}
