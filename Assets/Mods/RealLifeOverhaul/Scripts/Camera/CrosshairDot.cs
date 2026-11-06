using UI;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The dot at the centre of the screen during mouse look: one Image under the game's HUD canvas (UIs.playerHUD), made
    /// the first time it is wanted and then only switched on and off. No HUD, no dot.
    /// </summary>
    public static class CrosshairDot
    {
        private static GameObject _dot;
        private static bool _on;

        /// <summary>True once the dot has been made.</summary>
        public static bool Exists => _dot != null;

        /// <summary>Shows or hides the dot; nothing while there is no HUD to stand on.</summary>
        public static void Set(bool on)
        {
            if (on == _on && (_dot != null || !on))
            {
                return;
            }
            if (_dot == null)
            {
                if (!on)
                {
                    _on = false;
                    return;
                }
                _dot = Make();
                if (_dot == null)
                {
                    return;   // tried again on the next change
                }
            }
            _on = on;
            _dot.SetActive(on);
        }

        private static GameObject Make()
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            if (uis == null || uis.playerHUD == null)
            {
                return null;
            }
            var dot = new GameObject("RealLifeOverhaul Crosshair", typeof(RectTransform), typeof(Image));
            dot.transform.SetParent(uis.playerHUD.transform, false);
            var rect = (RectTransform)dot.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(4f, 4f);
            var image = dot.GetComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.75f);
            image.raycastTarget = false;
            dot.SetActive(false);
            return dot;
        }
    }
}
