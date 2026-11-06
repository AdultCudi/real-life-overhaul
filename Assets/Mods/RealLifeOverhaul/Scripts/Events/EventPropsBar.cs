using UnityEngine;

namespace RealLifeOverhaul.Events
{
    /// <summary>The light bar's shapes, shared by the livery (it is part of the truck's dressing, not a pooled prop).</summary>
    internal static class EventPropsBar
    {
        public static void Build(Transform root)
        {
            if (Shapes.Prop(EventProps.LightBar, root) || !Shapes.Available)
            {
                return;
            }
            Shapes.Piece("Bar", root, Shapes.Cube, Shapes.Paint(new Color(0.04f, 0.04f, 0.045f)), new Vector3(0f, 0.03f, 0f), new Vector3(1.30f, 0.06f, 0.28f));
            Shapes.Piece("Red", root, Shapes.Cube, Shapes.Paint(new Color(0.95f, 0.06f, 0.05f), 5f), new Vector3(-0.36f, 0.11f, 0f), new Vector3(0.52f, 0.11f, 0.24f));
            Shapes.Piece("Clear", root, Shapes.Cube, Shapes.Paint(new Color(1f, 0.96f, 0.84f), 3f), new Vector3(0f, 0.11f, 0f), new Vector3(0.18f, 0.11f, 0.24f));
            Shapes.Piece("Red 2", root, Shapes.Cube, Shapes.Paint(new Color(0.95f, 0.06f, 0.05f), 5f), new Vector3(0.36f, 0.11f, 0f), new Vector3(0.52f, 0.11f, 0.24f));
        }
    }
}
