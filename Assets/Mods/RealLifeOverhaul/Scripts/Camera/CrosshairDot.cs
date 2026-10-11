using BAModAPI;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The small dot at the centre of the screen while the camera looks with the mouse. One <c>Image</c> that is a child of the game's own
    /// HUD canvas (<c>UIs.playerHUD</c>): the mod has no canvas of its own for it, it goes when the game's HUD goes, and it is made
    /// the first time it is wanted and then only switched on and off. If the game's HUD cannot be found there is no dot.
    /// </summary>
    public static class CrosshairDot
    {
        private static GameObject _dot;
        private static bool _on;

        /// <summary>True once the dot has been made (so a caller that only wants it off can leave it alone otherwise).</summary>
        public static bool Exists => _dot != null;

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
                    return;   // no HUD to stand on yet; tried again on the next change
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
