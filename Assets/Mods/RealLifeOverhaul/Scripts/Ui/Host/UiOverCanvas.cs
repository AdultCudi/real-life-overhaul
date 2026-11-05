using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// A canvas of the kit's own nested in the canvas a game piece stands on (the HUD's, for the HUD host): the game's scaler
    /// above it (3840 x 2160 and the player's UI zoom) scales the kit as it scales the game's own piece, and its order is
    /// just above the piece's (UiHostRules.Above), so the game's question boxes, its notices and other mods' canvases stay
    /// over it. The canvases are walked as the engine nests them, switched off ones too, and nothing of the game's is
    /// touched. One way of binding over the game, in the host's library with the rest (README, "The five steps"). Cost: one
    /// walk of the canvases above the piece, once.
    /// </summary>
    public static class UiOverCanvas
    {
        /// <summary>The new canvas's rect, stretched over the piece's root canvas and switched off; null when no canvas stands above the piece.</summary>
        public static RectTransform Make(Component over, string name)
        {
            if (over == null)
            {
                return null;
            }
            Canvas[] above = over.GetComponentsInParent<Canvas>(true);
            if (above.Length == 0)
            {
                return null;
            }
            var overrides = new bool[above.Length];
            for (int i = 0; i < above.Length; i++)
            {
                overrides[i] = above[i].overrideSorting;
            }
            Canvas root = above[UiBindRules.Root(above.Length)];
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(root.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var canvas = go.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = UiHostRules.Above(above[UiBindRules.Sorter(overrides, above.Length)].sortingOrder);
            go.AddComponent<GraphicRaycaster>();
            go.SetActive(false);
            UiForeignWindows.Forget(root);   // the canvas the kit now stands on, if it was taken for another mod's window before
            return rect;
        }
    }
}
