using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The mod's minimap card where it stands, in px of a kit layout (from its top left), for every piece of the kit's that
    /// docks near the bottom left to keep clear of it: the item panel, the HUD's own pieces, the notice cards and the activity
    /// strip. One reading of the card's four corners, whole px, made where a host looks at the game a few times a second
    /// (never a frame); nothing is allocated. The card is the game's own panel, so its place is read from the screen as the
    /// game's canvases put it, whatever the resolution or the player's UI zoom.
    /// </summary>
    public static class UiMinimapBox
    {
        private static readonly Vector3[] Corners = new Vector3[4];
        private static RectTransform _of;   // the card last read, and the canvas it stands on
        private static Canvas _canvas;

        /// <summary>
        /// True while the city map's dock stands where the HUD's left pieces do. The minimap is hidden then, so its box is the dock's and
        /// every piece that keeps clear of the card keeps clear of the dock by the same rule.
        /// </summary>
        public static bool Dock => MapKits.DockShowing;

        /// <summary>The card's box in the layout's px (the city map's dock while that is up), or all zero while neither shows or the layout is not measured.</summary>
        public static void Read(float layoutW, float layoutH, out float x, out float y, out float w, out float h)
        {
            x = y = w = h = 0f;
            if (layoutW > 0f && layoutH > 0f && BuildFace.Showing)
            {
                UiBuildModel build = BuildFace.Current;
                UiBuildPanels.LeftBox(build, default(UiFrame), out x, out y, out w, out h);   // Build mode's panels stand where the minimap's card does: the objectives keep clear
                return;
            }
            if (layoutW > 0f && layoutH > 0f && Dock)
            {
                UiMapFilters.DockBox(layoutH, out x, out y, out w, out h);
                return;
            }
            RectTransform card = RealLifeOverhaul.Nav.NavHud.Card;
            if (card == null || layoutW <= 0f || layoutH <= 0f || Screen.width <= 0 || Screen.height <= 0)
            {
                return;
            }
            if (!ReferenceEquals(card, _of))
            {
                _of = card;
                _canvas = card.GetComponentInParent<Canvas>(true);   // the game's HUD canvas it stands on, as WorldBuilding reads the door box's
            }
            Camera camera = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;
            card.GetWorldCorners(Corners);   // bottom left, top left, top right, bottom right
            Vector2 low = RectTransformUtility.WorldToScreenPoint(camera, Corners[0]), high = RectTransformUtility.WorldToScreenPoint(camera, Corners[2]);
            UiHudRules.ToLayout(low.x, high.y, Screen.width, Screen.height, layoutW, layoutH, out float left, out float top);
            UiHudRules.ToLayout(high.x, low.y, Screen.width, Screen.height, layoutW, layoutH, out float right, out float bottom);
            if (right <= left || bottom <= top)
            {
                return;
            }
            x = Mathf.Floor(left);
            y = Mathf.Floor(top);
            w = Mathf.Ceil(right - left);
            h = Mathf.Ceil(bottom - top);
        }

        /// <summary>The box as one number, for a host's change signature.</summary>
        public static int Stamp(float x, float y, float w, float h)
        {
            unchecked
            {
                return (((int)x * 31 + (int)y) * 31 + (int)w) * 31 + (int)h;
            }
        }
    }
}
