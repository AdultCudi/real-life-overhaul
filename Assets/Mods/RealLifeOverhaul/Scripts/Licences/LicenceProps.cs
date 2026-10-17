using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>The props driving licences put in the world: the bundle's model of an id, else plain shapes.</summary>
    internal static class LicenceProps
    {
        public const string SchoolSign = "driving_school_sign";
        public const string Beacon = "test_checkpoint_beacon";

        private static readonly Color White = new Color(0.93f, 0.93f, 0.90f);
        private static readonly Color Red = new Color(0.80f, 0.10f, 0.10f);
        private static readonly Color Dark = new Color(0.10f, 0.10f, 0.11f);
        private static readonly Color Teal = new Color(0.12f, 0.72f, 0.62f);
        private static readonly Color Yellow = new Color(0.98f, 0.82f, 0.10f);

        /// <summary>A new object for an id, switched on, at the origin of its parent. Null if nothing can be drawn at all.</summary>
        public static Transform Make(string id, Transform parent)
        {
            if (!Shapes.Available)
            {
                return null;
            }
            var root = new GameObject("RealLifeOverhaul " + id).transform;
            root.SetParent(parent, false);
            if (Shapes.Prop(id, root))
            {
                return root;
            }
            Mesh cube = Shapes.Cube;
            switch (id)
            {
                case SchoolSign:
                    // A sandwich board with a learner plate on each face.
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Transform face = Shapes.Piece("Board", root, cube, Shapes.Paint(White), new Vector3(0f, 0.55f, side * 0.13f), new Vector3(0.62f, 1.0f, 0.03f));
                        face.localRotation = Quaternion.Euler(side * -12f, 0f, 0f);
                        Shapes.Piece("L stem", root, cube, Shapes.Paint(Red), new Vector3(-0.10f, 0.62f, side * 0.155f), new Vector3(0.10f, 0.46f, 0.012f)).localRotation = face.localRotation;
                        Shapes.Piece("L foot", root, cube, Shapes.Paint(Red), new Vector3(0.02f, 0.44f, side * 0.185f), new Vector3(0.34f, 0.10f, 0.012f)).localRotation = face.localRotation;
                    }
                    Shapes.Piece("Hinge", root, cube, Shapes.Paint(Dark), new Vector3(0f, 1.05f, 0f), new Vector3(0.64f, 0.04f, 0.08f));
                    break;

                case Beacon:
                    Shapes.Piece("Post", root, Shapes.Post, Shapes.Paint(Teal, 1.2f), Vector3.zero, new Vector3(0.22f, 2.6f, 0.22f));
                    Shapes.Piece("Flag", root, cube, Shapes.Paint(Yellow, 0.8f), new Vector3(0f, 2.75f, 0f), new Vector3(0.5f, 0.3f, 0.06f));
                    break;

                default:
                    Object.Destroy(root.gameObject);
                    return null;
            }
            return root;
        }

        /// <summary>A parking bay painted on the road: four strips and a post at each corner, sized by <see cref="SizeBay"/>.</summary>
        public static Transform MakeBay(Transform parent)
        {
            if (!Shapes.Available)
            {
                return null;
            }
            var root = new GameObject("RealLifeOverhaul Test Bay").transform;
            root.SetParent(parent, false);
            Material line = Shapes.Paint(Yellow, 0.6f);
            for (int i = 0; i < 4; i++)
            {
                Shapes.Piece("Line", root, Shapes.Cube, line, Vector3.zero, Vector3.one);
            }
            for (int i = 0; i < 4; i++)
            {
                Shapes.Piece("Post", root, Shapes.Post, Shapes.Paint(Teal, 1.0f), Vector3.zero, new Vector3(0.14f, 0.55f, 0.14f));
            }
            return root;
        }

        public static void SizeBay(Transform bay, float halfWidth, float halfLength)
        {
            if (bay == null || bay.childCount < 8)
            {
                return;
            }
            const float thick = 0.12f, tall = 0.03f;
            Set(bay.GetChild(0), new Vector3(-halfWidth, tall, 0f), new Vector3(thick, tall, halfLength * 2f));
            Set(bay.GetChild(1), new Vector3(halfWidth, tall, 0f), new Vector3(thick, tall, halfLength * 2f));
            Set(bay.GetChild(2), new Vector3(0f, tall, halfLength), new Vector3(halfWidth * 2f, tall, thick));
            Set(bay.GetChild(3), new Vector3(0f, tall, -halfLength), new Vector3(halfWidth * 2f, tall, thick));
            for (int i = 0; i < 4; i++)
            {
                bay.GetChild(4 + i).localPosition = new Vector3((i & 1) == 0 ? -halfWidth : halfWidth, 0f, (i & 2) == 0 ? -halfLength : halfLength);
            }
        }

        private static void Set(Transform piece, Vector3 position, Vector3 size)
        {
            piece.localPosition = position;
            piece.localScale = size;
        }
    }
}
