using System.Collections.Generic;
using UnityEngine;

namespace RealLifeOverhaul.Leisure
{
    /// <summary>
    /// The props leisure puts out, pooled: made the first time they are asked for, switched off
    /// and reused after. Each comes from <see cref="PropLibrary"/> by id when the bundle has it
    /// and is otherwise a clean stand-in of plain shapes (<see cref="Shapes"/>) with the size and
    /// pivot the wish-list in docs/features/leisure.md gives, so the modelled prop drops in by id with no
    /// change here. No colliders, nothing per instance.
    /// </summary>
    public sealed class LeisureProps
    {
        private static readonly Color Dark = new Color(0.10f, 0.10f, 0.11f);
        private static readonly Color Steel = new Color(0.62f, 0.64f, 0.67f);
        private static readonly Color Cork = new Color(0.86f, 0.24f, 0.18f);

        private readonly Dictionary<string, Stack<Transform>> _shelf = new Dictionary<string, Stack<Transform>>(8);
        private readonly Transform _root;

        public LeisureProps(Transform parent)
        {
            _root = new GameObject("RealLifeOverhaul Leisure Props").transform;
            _root.SetParent(parent, false);
        }

        /// <summary>True if Take would hand out something for this id right now.</summary>
        public static bool Has(string id)
        {
            return PropLibrary.TryGet(id, out _, out _) || (Shapes.Available && IsShape(id));
        }

        /// <summary>A prop, switched on, under the leisure root at scale one. Null if it cannot be had.</summary>
        public Transform Take(string id)
        {
            bool modelled = PropLibrary.TryGet(id, out Mesh mesh, out Material material) && mesh != null && material != null;
            string name = modelled ? id : id + " (stand-in)";
            if (_shelf.TryGetValue(name, out Stack<Transform> spares))
            {
                while (spares.Count > 0)
                {
                    Transform spare = spares.Pop();
                    if (spare != null)
                    {
                        spare.gameObject.SetActive(true);
                        return spare;
                    }
                }
            }
            var made = new GameObject(name);
            Transform prop = made.transform;
            prop.SetParent(_root, false);
            if (modelled)
            {
                made.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = made.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            }
            else if (!Shapes.Available || !Build(id, prop))
            {
                Object.Destroy(made);
                return null;
            }
            return prop;
        }

        /// <summary>Back on the shelf, switched off. Safe with null, with a destroyed prop and with one already returned.</summary>
        public void Return(Transform prop)
        {
            if (prop == null || (prop.parent == _root && !prop.gameObject.activeSelf))
            {
                return;
            }
            prop.SetParent(_root, false);
            prop.localScale = Vector3.one;
            SetLayer(prop, 0);
            prop.gameObject.SetActive(false);
            if (!_shelf.TryGetValue(prop.name, out Stack<Transform> spares))
            {
                spares = new Stack<Transform>(2);
                _shelf[prop.name] = spares;
            }
            spares.Push(prop);
        }

        /// <summary>A prop and its pieces on one layer (the body's while it is in a hand).</summary>
        public static void SetLayer(Transform prop, int layer)
        {
            prop.gameObject.layer = layer;
            for (int i = 0; i < prop.childCount; i++)
            {
                prop.GetChild(i).gameObject.layer = layer;
            }
        }

        public void Dispose()
        {
            _shelf.Clear();
            if (_root != null)
            {
                Object.Destroy(_root.gameObject);
            }
        }

        private static bool IsShape(string id)
        {
            switch (id)
            {
                case "fishing_rod":
                case "fishing_float":
                case "fishing_line":
                case "drink_glass":
                case "clothes_rack":
                    return true;
                default:
                    return false;
            }
        }

        // Sizes and pivots as in the wish-list in docs/features/leisure.md.
        private static bool Build(string id, Transform prop)
        {
            switch (id)
            {
                case "fishing_rod":
                    // Pivot: the butt of the handle. The rod runs along +Z, 2.1 m long.
                    Rod(prop, "Handle", new Color(0.16f, 0.13f, 0.10f), 0f, 0.34f, 0.030f);
                    Rod(prop, "Blank", Dark, 0.34f, 1.10f, 0.016f);
                    Rod(prop, "Tip", Dark, 1.44f, 0.66f, 0.008f);
                    Shapes.Piece("Reel", prop, Shapes.Drum, Shapes.Paint(Steel), new Vector3(0f, -0.075f, 0.40f), new Vector3(0.07f, 0.045f, 0.07f));
                    return true;
                case "fishing_float":
                    // Pivot: the waterline. 0.10 m tall, red over white.
                    Shapes.Piece("Top", prop, Shapes.Drum, Shapes.Paint(Cork, 0.3f), new Vector3(0f, 0f, 0f), new Vector3(0.045f, 0.07f, 0.045f));
                    Shapes.Piece("Keel", prop, Shapes.Drum, Shapes.Paint(Color.white), new Vector3(0f, -0.04f, 0f), new Vector3(0.035f, 0.04f, 0.035f));
                    return true;
                case "fishing_line":
                    // Pivot: one end. A unit length along +Z, scaled to the span.
                    Shapes.Piece("Line", prop, Shapes.Cube, Shapes.Paint(new Color(0.85f, 0.87f, 0.90f)), new Vector3(0f, 0f, 0.5f), new Vector3(0.004f, 0.004f, 1f));
                    return true;
                case "drink_glass":
                    // Pivot: ground centre. A tumbler the size of the coffee cup it is held like (0.075 x 0.12).
                    Shapes.Piece("Glass", prop, Shapes.Drum, Shapes.Paint(new Color(0.80f, 0.86f, 0.90f)), Vector3.zero, new Vector3(0.075f, 0.12f, 0.075f));
                    Shapes.Piece("Drink", prop, Shapes.Drum, Shapes.Paint(new Color(0.78f, 0.50f, 0.14f)), new Vector3(0f, 0.004f, 0f), new Vector3(0.077f, 0.085f, 0.077f));
                    return true;
                case "clothes_rack":
                    // Pivot: ground centre. A rail 1.4 m wide along X and 1.55 m up on two feet, with garments hanging from it; browsed from +Z.
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Shapes.Piece("Upright", prop, Shapes.Cube, Shapes.Paint(Steel), new Vector3(side * 0.70f, 0.78f, 0f), new Vector3(0.035f, 1.56f, 0.035f));
                        Shapes.Piece("Foot", prop, Shapes.Cube, Shapes.Paint(Steel), new Vector3(side * 0.70f, 0.02f, 0f), new Vector3(0.05f, 0.04f, 0.56f));
                    }
                    Shapes.Piece("Rail", prop, Shapes.Cube, Shapes.Paint(Steel), new Vector3(0f, 1.55f, 0f), new Vector3(1.44f, 0.035f, 0.035f));
                    Color[] cloth =
                    {
                        new Color(0.16f, 0.22f, 0.36f), new Color(0.70f, 0.70f, 0.68f), new Color(0.42f, 0.47f, 0.36f),
                        new Color(0.12f, 0.12f, 0.13f), new Color(0.56f, 0.20f, 0.22f), new Color(0.80f, 0.68f, 0.46f), new Color(0.90f, 0.90f, 0.88f)
                    };
                    for (int i = 0; i < cloth.Length; i++)
                    {
                        float x = -0.54f + i * 0.18f;
                        float drop = i % 3 == 0 ? 0.92f : 0.66f;
                        Shapes.Piece("Garment", prop, Shapes.Cube, Shapes.Paint(cloth[i]), new Vector3(x, 1.50f - drop * 0.5f, 0f), new Vector3(0.05f, drop, 0.42f));
                    }
                    Shapes.Piece("Sign", prop, Shapes.Cube, Shapes.Paint(new Color(0.36f, 0.78f, 0.52f), 0.6f), new Vector3(0f, 1.68f, 0f), new Vector3(0.60f, 0.16f, 0.02f));
                    return true;
                default:
                    return false;
            }
        }

        // A round length of the rod along +Z, starting 'from' metres up the rod.
        private static void Rod(Transform prop, string name, Color colour, float from, float length, float thick)
        {
            Transform piece = Shapes.Piece(name, prop, Shapes.Drum, Shapes.Paint(colour), new Vector3(0f, 0f, from), new Vector3(thick, length, thick));
            piece.localRotation = Quaternion.Euler(90f, 0f, 0f);   // the drum stands on its origin along Y: laid along Z
        }
    }
}
