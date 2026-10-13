using System;
using BAModAPI;
using RealLifeOverhaul.GameUi;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The speed of the car, as one more reading of the game's own vehicle panel (the row of parking, cargo, fuel and condition that
    /// <c>VehicleInfoPanel</c> keeps): a copy of its condition reading, put after it, with the speed and its unit in the same
    /// lettering. The game's panel shows no speed of its own. Made the first time it is wanted (the way <see cref="CarDash"/> adds
    /// its reading), then only switched on and off and written when the whole number changes. If the game's panel is not built as
    /// expected there is no reading, with one line in the log.
    /// </summary>
    public static class SpeedReading
    {
        public static IModLogger Logger;

        private static GameObject _reading;
        private static TMP_Text _value;
        private static int _shownValue = int.MinValue;
        private static string _shownUnit;
        private static bool _on;
        private static bool _failed;

        public static void Show(int value, string unit)
        {
            if (_failed)
            {
                return;
            }
            try
            {
                if (_reading == null)
                {
                    _on = false;
                    _shownValue = int.MinValue;
                    if (!Build())
                    {
                        _failed = true;
                        return;
                    }
                }
                if (value != _shownValue || !ReferenceEquals(unit, _shownUnit))
                {
                    _shownValue = value;
                    _shownUnit = unit;
                    _value.text = value + " " + unit;
                }
                if (!_on)
                {
                    _on = true;
                    _reading.SetActive(true);
                    if (Meta.Dev.Verbose)
                    {
                        Logger?.Info("Driving check (speed): the speed is in the game's vehicle panel, after its condition reading: PASS.");
                    }
                }
            }
            catch (Exception e)
            {
                _failed = true;
                Logger?.Error(e);
                Logger?.Warn("Driving: the speed reading in the game's vehicle panel is off for this session after the error above.");
                Remove();
            }
        }

        public static void Hide()
        {
            if (_on)
            {
                _on = false;
                if (_reading != null)
                {
                    _reading.SetActive(false);
                }
            }
        }

        private static void Remove()
        {
            try
            {
                if (_reading != null)
                {
                    UnityEngine.Object.Destroy(_reading);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            _reading = null;
            _value = null;
            _on = false;
        }

        private static bool Build()
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            Transform info = uis != null && uis.playerHUD != null && uis.playerHUD.itemPanelUI != null && uis.playerHUD.itemPanelUI.vehicleInfo != null
                ? uis.playerHUD.itemPanelUI.vehicleInfo.transform
                : null;
            Transform condition = UiClone.Need(info, "Condition", "the car's condition reading");
            if (condition == null)
            {
                UiClone.Fallback("the game's vehicle panel has no condition reading to copy, so the speed is not shown there");
                return false;
            }
            using var piece = SessionWatch.Piece("game UI: the speed reading being copied from the game's vehicle panel");
            Transform holder = UiClone.Dormant(info, "RealLifeOverhaul Speed Holder");
            GameObject copy = UiClone.Asleep(condition.gameObject, holder, "RealLifeOverhaul Speed");
            UiClone.StripGameScripts(copy, null);
            Transform value = copy.transform.Find("Value");
            Transform picture = copy.transform.Find("Icon");
            _value = value != null ? value.GetComponent<TMP_Text>() : copy.GetComponentInChildren<TMP_Text>(true);
            if (_value == null)
            {
                UnityEngine.Object.DestroyImmediate(holder.gameObject);
                UiClone.Fallback("the game's condition reading has no text, so the speed is not shown in the vehicle panel");
                return false;
            }
            _value.raycastTarget = false;
            _value.textWrappingMode = TextWrappingModes.NoWrap;
            GlobalReferences references = InstanceBehavior<GlobalReferences>.Instance;
            _value.color = references != null ? references.colors.black : new Color32(0, 0, 0, 255);
            if (picture != null)
            {
                // The condition's picture would say something else: the game's own information picture, or none.
                var image = picture.GetComponent<Image>();
                GameSprites.Want("Icon-Info-32");
                Sprite sprite = GameSprites.Get("Icon-Info-32");
                if (image != null && sprite != null)
                {
                    image.sprite = sprite;
                    image.color = references != null ? references.colors.black : new Color32(0, 0, 0, 255);
                    image.raycastTarget = false;
                }
                else
                {
                    picture.gameObject.SetActive(false);
                }
            }
            copy.SetActive(false);
            copy.transform.SetParent(info, false);
            copy.transform.SetSiblingIndex(condition.GetSiblingIndex() + 1);
            UnityEngine.Object.DestroyImmediate(holder.gameObject);
            _reading = copy;
            return true;
        }
    }
}
