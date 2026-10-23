using UnityEngine;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// A panel kept in the picture (<see cref="KeepInView"/>, <see cref="PanelEdgeRules"/>): a place off the screen or
    /// behind the camera is drawn at the screen's edge toward it. Asked by the game every frame while the panel is in its
    /// box: a few dot products, nothing allocated or searched.
    /// </summary>
    public sealed partial class WorldPanel
    {
        /// <summary>Drawn at the screen's edge toward a place that is out of the picture, rather than off the screen. Set once by its owner.</summary>
        public bool KeepInView;

        /// <summary>True when the place is out of the picture now (or too near its edge for the box) and the panel stands at the edge. For a self-check line, once an event.</summary>
        public bool AtEdge => KeepInView && InView(Unmoved(), out _);

        // The place itself: the thing followed, or the place given.
        private Vector3 Unmoved()
        {
            return Follow != null ? Follow.position + Vector3.up * FollowLift : Position;
        }

        // The place, or the point at the picture's edge toward it; the game's own camera, as OverlayUI.Update projects with.
        private static bool InView(Vector3 at, out Vector3 shown)
        {
            shown = at;
            Camera camera = GameManager.GetMainCamera();
            if (camera == null || camera.orthographic)
            {
                return false;
            }
            Transform view = camera.transform;
            Vector3 gap = at - view.position;
            float z = Vector3.Dot(gap, view.forward);
            float tanHalf = Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            if (!PanelEdgeRules.Edge(Vector3.Dot(gap, view.right), Vector3.Dot(gap, view.up), z, tanHalf, camera.aspect, out float across, out float up))
            {
                return false;
            }
            shown = camera.ViewportToWorldPoint(new Vector3(across, up, Mathf.Max(z, PanelEdgeRules.LeastDepth)));
            return true;
        }
    }
}
