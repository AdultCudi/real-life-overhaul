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
    /// What a car of the player's needs, as one more reading in the game's own vehicle panel.
    ///
    /// While something is driven the game shows its panel at the bottom of the screen (UI.ItemPanel.ItemPanelUI); in
    /// its headline the object VehicleInfo (UI.ItemPanel.VehicleInfoPanel, a horizontal layout) holds the steering
    /// assist switch and four readings, each a picture of 32 x 32 and a value in the headline's dark type: the
    /// parking zone, the cargo, the fuel as a percentage (orange from 50, red from 30) and the condition. The fuel is
    /// therefore the game's to show, and the mod draws no gauge of its own.
    ///
    /// The mod adds what the game does not know: a fifth reading, a copy of "Condition" made once per city and put
    /// after it, which says SERVICE DUE, TYRES WORN, BRAKES WORN or BREAKDOWN in the game's orange or red and is
    /// switched off while nothing needs attention. It is a child of the game's panel, so it comes and goes with it.
    /// Car ownership calls <see cref="Show"/> from its timer; the text is written only when the word changes.
    /// </summary>
    public static class CarDash
    {
        public static IModLogger Logger;

        private static GameObject _reading;
        private static TMP_Text _value;
        private static Image _picture;
        private static string _shown;
        private static bool _urgentShown;
        private static bool _on;
        private static bool _failed;

        /// <param name="word">Final words in capitals ("SERVICE DUE"). Pass the same string object while nothing changes.</param>
        /// <param name="urgent">The game's red (a breakdown); otherwise its orange.</param>
        public static void Show(string word, bool urgent)
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
                    _shown = null;
                    if (!Build())
                    {
                        _failed = true;
                        return;
                    }
                }
                if (!ReferenceEquals(word, _shown) || urgent != _urgentShown)
                {
                    _shown = word;
                    _urgentShown = urgent;
                    GlobalReferences references = InstanceBehavior<GlobalReferences>.Instance;
                    Color32 colour = references != null ? (urgent ? references.colors.red : references.colors.orange) : (urgent ? new Color32(230, 60, 50, 255) : new Color32(240, 150, 40, 255));
                    _value.text = word;
                    _value.color = colour;
                    if (_picture != null)
                    {
                        _picture.color = colour;
                    }
                    if (Meta.Dev.Verbose)
                    {
                        Logger?.Info("Car check (dash): '" + word + "' is in the game's vehicle panel, after its condition reading, in the game's " + (urgent ? "red" : "orange") + ": PASS.");
                    }
                }
                if (!_on)
                {
                    _on = true;
                    _reading.SetActive(true);
                }
            }
            catch (Exception e)
            {
                _failed = true;
                Logger?.Error(e);
                Logger?.Warn("Car ownership: the reading in the game's vehicle panel is off for this session after the error above; the Garage contact still says what a car needs.");
                Remove(false);
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
                if (Meta.Dev.Verbose)
                {
                    Logger?.Info("Car check (dash): the reading is off (nothing needs attention, or the car was left).");
                }
            }
        }

        /// <summary>The reading out of the game's panel; with <paramref name="forget"/>, a refusal of this session is forgotten too (the city unloads).</summary>
        public static void Remove(bool forget = true)
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
            _picture = null;
            _shown = null;
            _on = false;
            if (forget)
            {
                _failed = false;
            }
        }

        private static bool Build()
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            Transform info = uis != null && uis.playerHUD != null && uis.playerHUD.itemPanelUI != null && uis.playerHUD.itemPanelUI.vehicleInfo != null
                ? uis.playerHUD.itemPanelUI.vehicleInfo.transform
                : null;
            Transform condition = UiClone.Need(info, "Condition", "the car's reading");
            if (condition == null)
            {
                UiClone.Fallback("the game's vehicle panel has no condition reading to copy, so what a car needs is not shown there (the Garage contact says it)");
                return false;
            }
            SessionWatch.Mark("game UI: the car's reading being copied from the game's vehicle panel");
            Transform holder = UiClone.Dormant(info, "RealLifeOverhaul Service Holder");
            GameObject copy = UiClone.Asleep(condition.gameObject, holder, "RealLifeOverhaul Service");
            UiClone.StripGameScripts(copy, null);
            Transform value = copy.transform.Find("Value");
            Transform picture = copy.transform.Find("Icon");
            _value = value != null ? value.GetComponent<TMP_Text>() : copy.GetComponentInChildren<TMP_Text>(true);
            _picture = picture != null ? picture.GetComponent<Image>() : null;
            if (_value == null)
            {
                UnityEngine.Object.DestroyImmediate(holder.gameObject);
                UiClone.Fallback("the game's condition reading has no text, so what a car needs is not shown in the vehicle panel");
                return false;
            }
            _value.raycastTarget = false;
            _value.textWrappingMode = TextWrappingModes.NoWrap;
            if (_picture != null)
            {
                _picture.raycastTarget = false;
                GameSprites.Want("Icon-ExclamationMark", "Icon-Info-32");   // (one look through the game's pictures for both)
                Sprite sprite = GameSprites.Get("Icon-ExclamationMark") ?? GameSprites.Get("Icon-Info-32");
                if (sprite != null)
                {
                    _picture.sprite = sprite;   // (failing both, the game's own picture for condition stays: the reading is about the car's state either way)
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
