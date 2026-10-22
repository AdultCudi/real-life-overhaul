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
    /// What a car of the player's needs (SERVICE DUE, TYRES WORN, BRAKES WORN, BREAKDOWN), as a fifth reading in the game's
    /// own vehicle panel: a copy of its "Condition" reading made once a city, a child of the panel so it comes and goes with
    /// it. The game already shows the fuel there. Car ownership calls <see cref="Show"/> from its timer; the text is written
    /// only when the word changes.
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

        /// <summary>
        /// Shows <paramref name="word"/> (capitals; the same string object while nothing changes) in the game's red when
        /// <paramref name="urgent"/>, else its orange.
        /// </summary>
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

        /// <summary>Takes the reading out of the panel; <paramref name="forget"/> also forgets this session's refusal (the city unloads).</summary>
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
            using var piece = SessionWatch.Piece("game UI: the car's reading being copied from the game's vehicle panel");
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
                Sprite sprite = GameSprites.Get("Icon-ExclamationMark") ?? GameSprites.Get("Icon-Info-32");
                if (sprite != null)
                {
                    _picture.sprite = sprite;
                }
            }
            copy.SetActive(false);
            copy.transform.SetParent(info, false);
            copy.transform.SetSiblingIndex(condition.GetSiblingIndex() + 1);
            UnityEngine.Object.DestroyImmediate(holder.gameObject);
            _reading = copy;
            return true;
        }

        /// <summary>The game's pictures this asks for, named before the one look behind the loading screen (GameSprites.Prewarm).</summary>
        internal static void WantPictures()
        {
            GameSprites.Want("Icon-ExclamationMark", "Icon-Info-32");
        }
    }
}
