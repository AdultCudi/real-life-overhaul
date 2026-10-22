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
    /// The car's speed as one more reading of the game's vehicle panel (VehicleInfoPanel's row of parking, cargo, fuel and
    /// condition): a copy of its condition reading, put after it, in the same lettering. Made the first time it is wanted,
    /// as <see cref="CarDash"/> adds its reading, then only switched on and off and written when the whole number changes;
    /// each reading's text is made once and kept, so nothing is allocated. No panel as expected, no reading, and one log line.
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
        private const int Kept = 400;                   // readings 0 to 399 keep their text: the fastest car is well under that
        private static readonly string[][] Texts = new string[2][];   // each unit's texts by reading, for the two units in use
        private static readonly string[] Units = new string[2];       // and which unit each holds

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
                    _value.text = Text(value, unit);
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

        // "57 km/h": made the first time that reading is shown, then kept.
        private static string Text(int value, string unit)
        {
            if (value < 0 || value >= Kept || unit == null)
            {
                return value + " " + unit;
            }
            int slot = ReferenceEquals(Units[0], unit) || Units[0] == unit ? 0 : ReferenceEquals(Units[1], unit) || Units[1] == unit ? 1 : -1;
            if (slot < 0)
            {
                slot = Units[0] == null ? 0 : 1;
                Units[slot] = unit;
                Texts[slot] = new string[Kept];
            }
            string[] texts = Texts[slot];
            return texts[value] ?? (texts[value] = value + " " + unit);
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
