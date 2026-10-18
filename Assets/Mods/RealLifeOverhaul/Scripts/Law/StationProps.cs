using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace RealLifeOverhaul.Law
{
    /// <summary>A piece's own origin: the middle of its footprint, the middle of its face against a wall, or the model's own.</summary>
    internal enum Pivot { Floor, Wall, Own }

    /// <summary>
    /// The pieces of a police station, outside and in: the modelled prop where the bundle has it, otherwise a stand-in of
    /// plain shapes. A piece is a holder whose origin is where the code places it, the model fitted under it by its bounds,
    /// so a prop only has to face +Z. Nothing here keeps a piece: whoever asks builds it once and owns it.
    /// </summary>
    internal static class StationProps
    {
        public const string Sign = "precinct_sign";
        public const string Lamp = "blue_lamp";
        public const string Desk = "precinct_desk";
        public const string Bench = "precinct_bench";
        public const string Bars = "holding_cell_bars";
        public const string NoticeBoard = "notice_board";
        public const string FlagPole = "flag_pole";
        public const string Emblem = "police_shield_emblem";
        public const string Awning = "station_awning";
        public const string Bollard = "bollard";
        public const string OfficeDesk = "office_desk";          // not in the bundle yet: a stand-in is built
        public const string EvidenceDoor = "evidence_door";      // not in the bundle yet: a stand-in is built

        public static readonly Color PoliceBlue = new Color(0.06f, 0.17f, 0.45f);
        public static readonly Color Steel = new Color(0.16f, 0.17f, 0.19f);
        public static readonly Color White = new Color(0.92f, 0.93f, 0.95f);
        public static readonly Color Gold = new Color(0.78f, 0.62f, 0.20f);
        public static readonly Color Wood = new Color(0.36f, 0.25f, 0.16f);
        public static readonly Color Cork = new Color(0.62f, 0.47f, 0.30f);
        public static readonly Color LampBlue = new Color(0.15f, 0.35f, 1f);

        private static Mesh _star;

        /// <summary>
        /// One piece under a parent, switched off: the modelled prop fitted to the pivot asked for, or an empty holder for the
        /// caller's stand-in. <paramref name="size"/> is the model's size, or the stand-in's as given.
        /// </summary>
        public static Transform Holder(string name, Transform parent, string id, Pivot pivot, Vector3 standInSize, out Vector3 size, out bool modelled)
        {
            var holder = new GameObject(name);
            Transform place = holder.transform;
            place.SetParent(parent, false);
            size = standInSize;
            modelled = false;
            if (id != null && PropLibrary.TryGet(id, out Mesh mesh, out Material material) && mesh != null && material != null)
            {
                Bounds bounds = mesh.bounds;
                if (bounds.size.x > 0.01f && bounds.size.y > 0.01f)
                {
                    Vector3 offset = pivot == Pivot.Floor
                        ? new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z)
                        : pivot == Pivot.Wall ? new Vector3(-bounds.center.x, -bounds.center.y, -bounds.min.z) : Vector3.zero;
                    Shapes.Piece(id, place, mesh, material, offset, Vector3.one);
                    size = bounds.size;
                    modelled = true;
                }
            }
            holder.SetActive(false);
            return place;
        }

        /// <summary>Solid for everyone on foot: a box cut out of the navigation mesh while the piece is switched on.</summary>
        public static void Solid(Transform holder, Vector3 centre, Vector3 size)
        {
            var cut = holder.gameObject.AddComponent<NavMeshObstacle>();
            cut.shape = NavMeshObstacleShape.Box;
            cut.center = centre;
            cut.size = size;
            cut.carving = true;
            cut.carveOnlyStationary = true;
        }

        /// <summary>The front counter: 2.4 wide, 1.1 high, 0.8 deep; the public stand at +Z.</summary>
        public static void DeskStandIn(Transform root)
        {
            Material blue = Shapes.Paint(PoliceBlue);
            Material top = Shapes.Paint(new Color(0.80f, 0.80f, 0.78f));
            Material white = Shapes.Paint(White);
            Shapes.Piece("Body", root, Shapes.Cube, blue, new Vector3(0f, 0.52f, 0f), new Vector3(2.36f, 1.04f, 0.70f));
            Shapes.Piece("Top", root, Shapes.Cube, top, new Vector3(0f, 1.07f, 0.02f), new Vector3(2.40f, 0.06f, 0.80f));
            Shapes.Piece("Stripe", root, Shapes.Cube, white, new Vector3(0f, 0.78f, 0.352f), new Vector3(2.36f, 0.05f, 0.01f));
            Shapes.Piece("Plinth", root, Shapes.Cube, Shapes.Paint(Steel), new Vector3(0f, 0.04f, 0f), new Vector3(2.38f, 0.08f, 0.72f));
        }

        /// <summary>A waiting bench: 1.8 wide, 0.85 high, 0.5 deep; its back at -Z.</summary>
        public static void BenchStandIn(Transform root)
        {
            Material wood = Shapes.Paint(Wood);
            Material steel = Shapes.Paint(Steel);
            Shapes.Piece("Seat", root, Shapes.Cube, wood, new Vector3(0f, 0.44f, 0.03f), new Vector3(1.80f, 0.05f, 0.42f));
            Shapes.Piece("Back", root, Shapes.Cube, wood, new Vector3(0f, 0.70f, -0.21f), new Vector3(1.80f, 0.30f, 0.04f));
            for (int side = -1; side <= 1; side += 2)
            {
                Shapes.Piece("Leg", root, Shapes.Cube, steel, new Vector3(side * 0.80f, 0.21f, 0.03f), new Vector3(0.05f, 0.42f, 0.40f));
                Shapes.Piece("Upright", root, Shapes.Cube, steel, new Vector3(side * 0.80f, 0.64f, -0.21f), new Vector3(0.05f, 0.44f, 0.05f));
            }
        }

        /// <summary>A barred panel 3.0 wide and 2.4 high with a door in it; in the XY plane, 0.06 thick.</summary>
        public static void BarsStandIn(Transform root)
        {
            Material steel = Shapes.Paint(new Color(0.22f, 0.23f, 0.26f));
            Shapes.Piece("Sill", root, Shapes.Cube, steel, new Vector3(0f, 0.04f, 0f), new Vector3(3.00f, 0.08f, 0.06f));
            Shapes.Piece("Head", root, Shapes.Cube, steel, new Vector3(0f, 2.36f, 0f), new Vector3(3.00f, 0.08f, 0.06f));
            Shapes.Piece("Rail", root, Shapes.Cube, steel, new Vector3(0f, 1.20f, 0f), new Vector3(3.00f, 0.05f, 0.05f));
            for (int i = 0; i <= 24; i++)
            {
                float x = -1.47f + i * 0.1225f;
                // The door's frame: two heavier uprights a door's width apart, right of the middle.
                bool frame = i == 13 || i == 21;
                float thick = frame ? 0.06f : 0.028f;
                Shapes.Piece("Bar", root, Shapes.Drum, steel, new Vector3(x, 0.08f, 0f), new Vector3(thick, 2.24f, thick));
            }
            Shapes.Piece("Lock", root, Shapes.Cube, Shapes.Paint(Steel), new Vector3(0.20f, 1.05f, 0f), new Vector3(0.14f, 0.22f, 0.09f));
        }

        /// <summary>A cork board in a frame with papers on it: 1.2 wide, 0.9 high, 0.05 proud of the wall (which is at z 0).</summary>
        public static void NoticeBoardStandIn(Transform root)
        {
            Shapes.Piece("Frame", root, Shapes.Cube, Shapes.Paint(Steel), new Vector3(0f, 0f, 0.02f), new Vector3(1.20f, 0.90f, 0.04f));
            Shapes.Piece("Cork", root, Shapes.Cube, Shapes.Paint(Cork), new Vector3(0f, 0f, 0.043f), new Vector3(1.12f, 0.82f, 0.01f));
            Material paper = Shapes.Paint(White);
            Material blue = Shapes.Paint(new Color(0.55f, 0.68f, 0.90f));
            Material yellow = Shapes.Paint(new Color(0.93f, 0.85f, 0.45f));
            Shapes.Piece("Paper", root, Shapes.Cube, paper, new Vector3(-0.36f, 0.14f, 0.05f), new Vector3(0.26f, 0.36f, 0.004f));
            Shapes.Piece("Paper", root, Shapes.Cube, yellow, new Vector3(-0.04f, 0.18f, 0.05f), new Vector3(0.22f, 0.30f, 0.004f));
            Shapes.Piece("Paper", root, Shapes.Cube, paper, new Vector3(0.30f, 0.12f, 0.05f), new Vector3(0.30f, 0.40f, 0.004f));
            Shapes.Piece("Paper", root, Shapes.Cube, blue, new Vector3(-0.30f, -0.22f, 0.05f), new Vector3(0.34f, 0.22f, 0.004f));
            Shapes.Piece("Paper", root, Shapes.Cube, paper, new Vector3(0.14f, -0.24f, 0.05f), new Vector3(0.24f, 0.20f, 0.004f));
        }

        /// <summary>A wall plaque 0.8 across: gold rim, blue field, a white star. 0.05 proud of the wall (z 0).</summary>
        public static void EmblemStandIn(Transform root)
        {
            Shapes.Piece("Rim", root, Shapes.Cube, Shapes.Paint(Gold), new Vector3(0f, 0f, 0.02f), new Vector3(0.80f, 0.80f, 0.04f));
            Shapes.Piece("Field", root, Shapes.Cube, Shapes.Paint(PoliceBlue), new Vector3(0f, 0f, 0.043f), new Vector3(0.70f, 0.70f, 0.01f));
            Transform star = Shapes.Piece("Star", root, Star, Shapes.Paint(White), new Vector3(0f, 0.01f, 0.05f), new Vector3(0.56f, 0.56f, 1f));
            star.localRotation = Quaternion.identity;
        }

        /// <summary>An entrance canopy 2.4 wide, 0.5 high and 1.1 deep, hung from the wall at z 0 with its underside at y 0.</summary>
        public static void AwningStandIn(Transform root)
        {
            Material blue = Shapes.Paint(PoliceBlue);
            Transform roof = Shapes.Piece("Roof", root, Shapes.Cube, blue, new Vector3(0f, 0.30f, 0.54f), new Vector3(2.40f, 0.05f, 1.14f));
            roof.localRotation = Quaternion.Euler(14f, 0f, 0f);
            Shapes.Piece("Valance", root, Shapes.Cube, blue, new Vector3(0f, 0.09f, 1.08f), new Vector3(2.40f, 0.18f, 0.03f));
            Shapes.Piece("Trim", root, Shapes.Cube, Shapes.Paint(White), new Vector3(0f, 0.012f, 1.082f), new Vector3(2.40f, 0.025f, 0.032f));
            Material steel = Shapes.Paint(Steel);
            for (int side = -1; side <= 1; side += 2)
            {
                Transform stay = Shapes.Piece("Stay", root, Shapes.Cube, steel, new Vector3(side * 1.17f, 0.25f, 0.54f), new Vector3(0.03f, 0.03f, 1.12f));
                stay.localRotation = Quaternion.Euler(14f, 0f, 0f);
                Shapes.Piece("Cheek", root, Shapes.Cube, blue, new Vector3(side * 1.19f, 0.20f, 0.56f), new Vector3(0.02f, 0.30f, 1.04f));
            }
        }

        /// <summary>A flag pole 5 m tall on a round foot, with a blue flag flying towards +X from its top.</summary>
        public static void FlagPoleStandIn(Transform root)
        {
            Material steel = Shapes.Paint(new Color(0.70f, 0.72f, 0.75f));
            Shapes.Piece("Foot", root, Shapes.Drum, Shapes.Paint(Steel), Vector3.zero, new Vector3(0.28f, 0.12f, 0.28f));
            Shapes.Piece("Pole", root, Shapes.Drum, steel, Vector3.zero, new Vector3(0.07f, 5.00f, 0.07f));
            Shapes.Piece("Cap", root, Shapes.Drum, Shapes.Paint(Gold), new Vector3(0f, 5.00f, 0f), new Vector3(0.12f, 0.10f, 0.12f));
            Shapes.Piece("Flag", root, Shapes.Cube, Shapes.Paint(PoliceBlue), new Vector3(0.64f, 4.52f, 0f), new Vector3(1.20f, 0.76f, 0.012f));
            Shapes.Piece("Band", root, Shapes.Cube, Shapes.Paint(White), new Vector3(0.64f, 4.52f, 0f), new Vector3(1.20f, 0.10f, 0.016f));
        }

        /// <summary>A kerb bollard: 0.16 across, 0.9 high, dark with a white band.</summary>
        public static void BollardStandIn(Transform root)
        {
            Shapes.Piece("Post", root, Shapes.Drum, Shapes.Paint(new Color(0.10f, 0.13f, 0.22f)), Vector3.zero, new Vector3(0.16f, 0.86f, 0.16f));
            Shapes.Piece("Band", root, Shapes.Drum, Shapes.Paint(White), new Vector3(0f, 0.66f, 0f), new Vector3(0.166f, 0.08f, 0.166f));
            Shapes.Piece("Cap", root, Shapes.Drum, Shapes.Paint(Steel), new Vector3(0f, 0.86f, 0f), new Vector3(0.18f, 0.04f, 0.18f));
        }

        /// <summary>A lamp on a short bracket off a wall (z 0): the bracket, and a socket 0.22 out for the lamp itself. Returns the socket.</summary>
        public static Transform LampBracket(Transform root)
        {
            Material steel = Shapes.Paint(Steel);
            Shapes.Piece("Plate", root, Shapes.Cube, steel, new Vector3(0f, -0.02f, 0.01f), new Vector3(0.10f, 0.16f, 0.02f));
            Shapes.Piece("Arm", root, Shapes.Cube, steel, new Vector3(0f, -0.02f, 0.12f), new Vector3(0.035f, 0.035f, 0.24f));
            var socket = new GameObject("Lamp");
            socket.transform.SetParent(root, false);
            socket.transform.localPosition = new Vector3(0f, 0f, 0.22f);
            return socket.transform;
        }

        /// <summary>The blue lamp on its origin: a cap over a glass 0.17 across, 0.24 high. Returns the glass, repainted at dusk.</summary>
        public static MeshRenderer LampStandIn(Transform socket)
        {
            Shapes.Piece("Cap", socket, Shapes.Drum, Shapes.Paint(Steel), new Vector3(0f, 0.24f, 0f), new Vector3(0.20f, 0.03f, 0.20f));
            Transform glass = Shapes.Piece("Glass", socket, Shapes.Drum, LampPaint(false), Vector3.zero, new Vector3(0.17f, 0.24f, 0.17f));
            return glass.GetComponent<MeshRenderer>();
        }

        /// <summary>The one paint of a lamp's glass by day, and the one by night (shared by every lamp).</summary>
        public static Material LampPaint(bool night)
        {
            return Shapes.Paint(LampBlue, night ? 9f : 1.5f);
        }

        /// <summary>A plain office desk with a screen on it: 1.4 wide, 0.75 high, 0.7 deep; whoever works at it stands at +Z.</summary>
        public static void OfficeDeskStandIn(Transform root)
        {
            Material top = Shapes.Paint(new Color(0.55f, 0.43f, 0.30f));
            Material steel = Shapes.Paint(Steel);
            Shapes.Piece("Top", root, Shapes.Cube, top, new Vector3(0f, 0.73f, 0f), new Vector3(1.40f, 0.04f, 0.70f));
            for (int side = -1; side <= 1; side += 2)
            {
                Shapes.Piece("Side", root, Shapes.Cube, steel, new Vector3(side * 0.67f, 0.355f, 0f), new Vector3(0.04f, 0.71f, 0.64f));
            }
            Shapes.Piece("Back", root, Shapes.Cube, steel, new Vector3(0f, 0.45f, -0.30f), new Vector3(1.30f, 0.40f, 0.02f));
            Shapes.Piece("Stand", root, Shapes.Cube, steel, new Vector3(-0.30f, 0.80f, -0.16f), new Vector3(0.06f, 0.10f, 0.06f));
            Shapes.Piece("Screen", root, Shapes.Cube, Shapes.Paint(new Color(0.07f, 0.08f, 0.10f)), new Vector3(-0.30f, 1.00f, -0.16f), new Vector3(0.50f, 0.32f, 0.03f));
            Shapes.Piece("Papers", root, Shapes.Cube, Shapes.Paint(White), new Vector3(0.32f, 0.756f, 0.06f), new Vector3(0.24f, 0.012f, 0.32f));
        }

        /// <summary>A closed steel door in its frame against a wall (z 0), standing on the floor: 1.0 wide, 2.1 high.</summary>
        public static void EvidenceDoorStandIn(Transform root)
        {
            Shapes.Piece("Frame", root, Shapes.Cube, Shapes.Paint(Steel), new Vector3(0f, 1.06f, 0.015f), new Vector3(1.04f, 2.12f, 0.03f));
            Shapes.Piece("Leaf", root, Shapes.Cube, Shapes.Paint(new Color(0.30f, 0.33f, 0.38f)), new Vector3(0f, 1.03f, 0.035f), new Vector3(0.90f, 2.02f, 0.02f));
            Shapes.Piece("Handle", root, Shapes.Cube, Shapes.Paint(new Color(0.70f, 0.72f, 0.75f)), new Vector3(0.34f, 1.02f, 0.055f), new Vector3(0.04f, 0.16f, 0.03f));
            Shapes.Piece("Plate", root, Shapes.Cube, Shapes.Paint(PoliceBlue), new Vector3(0f, 1.62f, 0.047f), new Vector3(0.62f, 0.13f, 0.006f));
        }

        /// <summary>Words in the game's own font on a piece, read from its +Z. Null if no font could be borrowed.</summary>
        public static TextMeshPro Letter(Transform parent, string word, Vector3 at, float width, float height)
        {
            TMP_FontAsset font = GameUi.GameFont.Asset;
            if (font == null)
            {
                return null;
            }
            var label = new GameObject("Lettering");
            Transform place = label.transform;
            place.SetParent(parent, false);
            place.localPosition = at;
            place.localRotation = Quaternion.Euler(0f, 180f, 0f);   // text is read from behind its own forward
            var text = label.AddComponent<TextMeshPro>();
            text.font = font;
            text.text = word;
            text.color = Color.white;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
#pragma warning disable CS0618
            text.enableWordWrapping = false;
#pragma warning restore CS0618
            text.rectTransform.sizeDelta = new Vector2(width, height);
            text.enableAutoSizing = true;
            text.fontSizeMin = 0.2f;
            text.fontSizeMax = 30f;
            var renderer = label.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            return text;
        }

        /// <summary>A flat five-pointed star one unit across, in the XY plane, seen from +Z. Made once.</summary>
        public static Mesh Star
        {
            get
            {
                if (_star != null)
                {
                    return _star;
                }
                var vertices = new Vector3[11];
                var normals = new Vector3[11];
                var triangles = new int[30];
                for (int i = 0; i < 10; i++)
                {
                    float angle = Mathf.PI * 0.5f + i * Mathf.PI / 5f;
                    float radius = (i & 1) == 0 ? 0.5f : 0.2f;
                    vertices[i] = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
                    normals[i] = Vector3.forward;
                }
                vertices[10] = Vector3.zero;
                normals[10] = Vector3.forward;
                for (int i = 0; i < 10; i++)
                {
                    // Clockwise as seen from +Z (x runs to the left from there), the side it is looked at from.
                    triangles[i * 3] = 10;
                    triangles[i * 3 + 1] = i;
                    triangles[i * 3 + 2] = (i + 1) % 10;
                }
                _star = new Mesh { name = "RealLifeOverhaul Star" };
                _star.vertices = vertices;
                _star.normals = normals;
                _star.triangles = triangles;
                _star.RecalculateBounds();
                _star.UploadMeshData(false);
                return _star;
            }
        }

        public static void Dispose()
        {
            if (_star != null)
            {
                Object.Destroy(_star);
                _star = null;
            }
        }
    }
}
