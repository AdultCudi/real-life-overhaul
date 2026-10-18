using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Everything civic life stands in the street that is not a person, pooled by id: the prop bundle's model if it has one,
    /// else the plain stand-in of the same id (<see cref="CivicStandIns"/>), so a model added to the bundle appears with no
    /// code change. One object, one mesh, no collider, no script per prop. Nothing here runs per frame.
    /// </summary>
    public sealed class CivicProps
    {
        private sealed class Kind
        {
            public Mesh Mesh;
            public Material[] Materials;
            public bool Modelled;
            public bool OwnMesh;   // built here: destroyed with the pool
        }

        private readonly Dictionary<string, Kind> _kinds = new Dictionary<string, Kind>(32);
        private readonly Dictionary<string, Stack<Transform>> _shelf = new Dictionary<string, Stack<Transform>>(32);
        private readonly Transform _root;

        public CivicProps(Transform parent)
        {
            _root = new GameObject("RealLifeOverhaul Civic Props").transform;
            _root.SetParent(parent, false);
        }

        /// <summary>How many are out now.</summary>
        public int Out { get; private set; }

        /// <summary>A prop object, switched on. Null only if the id has neither a model nor a stand-in, or shapes cannot be drawn at all.</summary>
        public Transform Take(string id)
        {
            if (_shelf.TryGetValue(id, out Stack<Transform> spare))
            {
                while (spare.Count > 0)
                {
                    Transform again = spare.Pop();
                    if (again != null)
                    {
                        again.gameObject.SetActive(true);
                        Out++;
                        return again;
                    }
                }
            }
            Kind kind = Resolve(id);
            if (kind == null)
            {
                return null;
            }
            var made = new GameObject(id);
            made.transform.SetParent(_root, false);
            made.AddComponent<MeshFilter>().sharedMesh = kind.Mesh;
            var renderer = made.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = kind.Materials;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            Out++;
            return made.transform;
        }

        /// <summary>Stands a prop at a point, turned so that its +Z looks along <paramref name="forward"/>.</summary>
        public Transform Stand(string id, Vector3 position, Vector3 forward)
        {
            Transform prop = Take(id);
            if (prop != null)
            {
                forward.y = 0f;
                prop.SetPositionAndRotation(position, forward.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(forward) : Quaternion.identity);
                prop.localScale = Vector3.one;
            }
            return prop;
        }

        /// <summary>Back on the shelf, switched off. Safe with null, with a destroyed prop, and twice.</summary>
        public void Return(Transform prop)
        {
            if (prop == null || (prop.parent == _root && !prop.gameObject.activeSelf))
            {
                return;
            }
            prop.SetParent(_root, false);
            prop.localScale = Vector3.one;
            prop.gameObject.SetActive(false);
            if (!_shelf.TryGetValue(prop.name, out Stack<Transform> spare))
            {
                spare = new Stack<Transform>(8);
                _shelf[prop.name] = spare;
            }
            spare.Push(prop);
            Out = Mathf.Max(0, Out - 1);
        }

        public void ReturnAll(List<Transform> props)
        {
            for (int i = 0; i < props.Count; i++)
            {
                Return(props[i]);
            }
            props.Clear();
        }

        /// <summary>The meshes built here go; the objects go with the root they hang under.</summary>
        public void Dispose()
        {
            foreach (Kind kind in _kinds.Values)
            {
                if (kind != null && kind.OwnMesh && kind.Mesh != null)
                {
                    UnityEngine.Object.Destroy(kind.Mesh);
                }
            }
            _kinds.Clear();
            _shelf.Clear();
            if (_root != null)
            {
                UnityEngine.Object.Destroy(_root.gameObject);
            }
        }

        // The model if the bundle has one, else the stand-in. Asked again each time while the bundle is still loading.
        private Kind Resolve(string id)
        {
            if (_kinds.TryGetValue(id, out Kind known) && (known.Modelled || PropLibrary.Ready == false || !PropLibraryHas(id)))
            {
                return known;
            }
            if (PropLibraryHas(id) && PropLibrary.TryGet(id, out Mesh mesh, out Material material) && mesh != null && material != null)
            {
                // Objects already made from the stand-in keep it until they are next built: a model never swaps in view.
                var modelled = new Kind { Mesh = mesh, Materials = new[] { material }, Modelled = true };
                if (known != null)
                {
                    DropShelf(id);
                }
                _kinds[id] = modelled;
                return modelled;
            }
            if (known != null)
            {
                return known;
            }
            Kind standIn = CivicStandIns.Build(id);
            if (standIn != null)
            {
                _kinds[id] = standIn;
            }
            return standIn;
        }

        // Asking PropLibrary.TryGet for an id it has not logs a warning once per id; the wish-list ids are asked through SizeOf first, which is silent.
        private static bool PropLibraryHas(string id)
        {
            return PropLibrary.Ready && PropLibrary.SizeOf(id) != Vector3.zero;
        }

        private void DropShelf(string id)
        {
            if (_shelf.TryGetValue(id, out Stack<Transform> spare))
            {
                while (spare.Count > 0)
                {
                    Transform old = spare.Pop();
                    if (old != null)
                    {
                        UnityEngine.Object.Destroy(old.gameObject);
                    }
                }
            }
        }

        /// <summary>The plain stand-ins by id: boxes in metres, pivot on the ground under the middle and facing +Z unless said; the specification for the bundle's models.</summary>
        private static class CivicStandIns
        {
            // Colours (sRGB). A sub-mesh per colour used.
            private static readonly Color Steel = new Color(0.55f, 0.57f, 0.60f);
            private static readonly Color Dark = new Color(0.10f, 0.10f, 0.11f);
            private static readonly Color Board = new Color(0.62f, 0.47f, 0.28f);
            private static readonly Color Net = new Color(0.16f, 0.40f, 0.22f);
            private static readonly Color HoardingBlue = new Color(0.10f, 0.22f, 0.42f);
            private static readonly Color White = new Color(0.90f, 0.90f, 0.88f);
            private static readonly Color Yellow = new Color(0.95f, 0.78f, 0.08f);
            private static readonly Color Orange = new Color(0.93f, 0.36f, 0.05f);
            private static readonly Color Asphalt = new Color(0.06f, 0.06f, 0.065f);
            private static readonly Color Earth = new Color(0.27f, 0.20f, 0.13f);
            private static readonly Color Concrete = new Color(0.60f, 0.60f, 0.58f);
            private static readonly Color Red = new Color(0.72f, 0.12f, 0.10f);
            private static readonly Color Broken = new Color(0.20f, 0.20f, 0.21f);   // a road's broken edge, a little lighter than the road
            private static readonly Color Hole = new Color(0.025f, 0.025f, 0.03f);   // the wet bottom of a hole
            private static readonly Color Paper = new Color(0.78f, 0.77f, 0.72f);

            private static readonly Builder B = new Builder();

            public static Kind Build(string id)
            {
                if (!Shapes.Available)
                {
                    return null;
                }
                B.Begin();
                switch (id)
                {
                    case "campaign_poster_0": Poster(CivicColours.Party(0)); break;
                    case "campaign_poster_1": Poster(CivicColours.Party(1)); break;
                    case "campaign_poster_2": Poster(CivicColours.Party(2)); break;
                    case "campaign_poster_3": Poster(CivicColours.Party(3)); break;
                    case "campaign_board_0": CampaignBoard(CivicColours.Party(0)); break;
                    case "campaign_board_1": CampaignBoard(CivicColours.Party(1)); break;
                    case "campaign_board_2": CampaignBoard(CivicColours.Party(2)); break;
                    case "campaign_board_3": CampaignBoard(CivicColours.Party(3)); break;
                    case "polling_sign":
                        CampaignBoard(White);
                        B.Box(0f, 0.62f, 0.16f, 0.36f, 0.10f, 0.012f, HoardingBlue, -14f);
                        B.Box(0f, 0.62f, -0.16f, 0.36f, 0.10f, 0.012f, HoardingBlue, 14f);
                        break;
                    case "soapbox":
                        // 0.60 x 0.30 x 0.45: a crate to stand on; the top is 0.30 m up.
                        B.Box(0f, 0.15f, 0f, 0.60f, 0.30f, 0.45f, Board);
                        B.Box(0f, 0.15f, 0.228f, 0.56f, 0.06f, 0.006f, Dark);
                        B.Box(0f, 0.15f, -0.228f, 0.56f, 0.06f, 0.006f, Dark);
                        break;

                    case "cone":
                        B.Box(0f, 0.015f, 0f, 0.36f, 0.03f, 0.36f, Dark);
                        B.Box(0f, 0.20f, 0f, 0.22f, 0.34f, 0.22f, Orange);
                        B.Box(0f, 0.47f, 0f, 0.15f, 0.20f, 0.15f, White);
                        B.Box(0f, 0.66f, 0f, 0.08f, 0.18f, 0.08f, Orange);
                        break;
                    case "barrier":
                        B.Box(0f, 0.90f, 0f, 2.40f, 0.26f, 0.04f, White);
                        B.Box(0f, 0.90f, 0f, 2.40f, 0.09f, 0.05f, Orange);
                        B.Box(-0.95f, 0.45f, 0f, 0.05f, 0.90f, 0.70f, White);
                        B.Box(0.95f, 0.45f, 0f, 0.05f, 0.90f, 0.70f, White);
                        break;
                    case "sandwich_board":
                        CampaignBoard(White);
                        break;
                    case "toolbox":
                        B.Box(0f, 0.10f, 0f, 0.47f, 0.20f, 0.23f, Red);
                        B.Box(0f, 0.22f, 0f, 0.30f, 0.03f, 0.03f, Dark);
                        break;
                    case "road_sign_works":
                        // 0.75 x 1.55 x 0.60: a 0.75 m square warning board on an A-frame, its face at +Z, bottom edge 0.75 m up.
                        B.Box(0f, 1.15f, 0.02f, 0.75f, 0.75f, 0.02f, Orange);
                        B.Box(0f, 1.15f, 0.032f, 0.40f, 0.12f, 0.006f, Dark);
                        B.Box(0f, 1.32f, 0.032f, 0.12f, 0.12f, 0.006f, Dark);
                        B.Box(-0.30f, 0.39f, 0f, 0.04f, 0.78f, 0.04f, Steel);
                        B.Box(0.30f, 0.39f, 0f, 0.04f, 0.78f, 0.04f, Steel);
                        B.Box(0f, 0.03f, 0f, 0.70f, 0.06f, 0.60f, Dark);
                        break;
                    case "trench_cover":
                        // 1.00 x 0.02 x 2.00, lying along Z: broken-up surface and the open trench in it.
                        B.Box(0f, 0.008f, 0f, 1.00f, 0.016f, 2.00f, Earth);
                        B.Box(0f, 0.012f, 0f, 0.55f, 0.02f, 1.80f, Dark);
                        break;
                    case "spoil_heap":
                        // 0.9 x 0.35 x 0.9: what came out of the trench.
                        B.Box(0f, 0.08f, 0f, 0.90f, 0.16f, 0.90f, Earth);
                        B.Box(0.04f, 0.21f, -0.03f, 0.58f, 0.12f, 0.60f, Earth);
                        B.Box(0f, 0.31f, 0f, 0.28f, 0.08f, 0.30f, Earth);
                        break;
                    case "road_marking":
                        // 0.10 x 0.004 x 2.00, along Z: sprayed setting-out line.
                        B.Box(0f, 0.004f, 0f, 0.10f, 0.006f, 2.00f, Yellow);
                        break;
                    case "asphalt_patch":
                        // 1.20 x 0.012 x 2.00, along Z: the new surface.
                        B.Box(0f, 0.007f, 0f, 1.20f, 0.014f, 2.00f, Asphalt);
                        break;
                    case "works_tent":
                        // 1.5 x 1.9 x 1.5: a frame tent over an open cover: four legs, a pitched top, three walls (open at +Z).
                        B.Box(-0.72f, 0.80f, -0.72f, 0.04f, 1.60f, 0.04f, Steel);
                        B.Box(0.72f, 0.80f, -0.72f, 0.04f, 1.60f, 0.04f, Steel);
                        B.Box(-0.72f, 0.80f, 0.72f, 0.04f, 1.60f, 0.04f, Steel);
                        B.Box(0.72f, 0.80f, 0.72f, 0.04f, 1.60f, 0.04f, Steel);
                        B.Box(0f, 1.00f, -0.74f, 1.48f, 1.20f, 0.012f, Yellow);
                        B.Box(-0.74f, 1.00f, 0f, 0.012f, 1.20f, 1.48f, Yellow);
                        B.Box(0.74f, 1.00f, 0f, 0.012f, 1.20f, 1.48f, Yellow);
                        B.Box(0f, 1.66f, 0f, 1.56f, 0.12f, 1.56f, Yellow);
                        B.Box(0f, 1.80f, 0f, 1.00f, 0.16f, 1.00f, Yellow);
                        break;
                    case "manhole_open":
                        // 0.9 x 0.06 x 0.9: the frame, the dark shaft, the lid laid beside it at +X.
                        B.Box(0f, 0.01f, 0f, 0.90f, 0.02f, 0.90f, Concrete);
                        B.Box(0f, 0.014f, 0f, 0.62f, 0.022f, 0.62f, Dark);
                        break;
                    case "manhole_lid":
                        B.Box(0f, 0.025f, 0f, 0.66f, 0.05f, 0.66f, Steel);
                        break;

                    case "scaffold_bay":
                        // The first lift: 2.40 wide (X) x 2.40 high x 1.80 deep (Z). Pivot: the ground at the middle of its WALL side;
                        // it stands out from the wall towards +Z. Standards at the four corners (the outer row 1.74 from the pivot),
                        // ledgers under a boarded deck 0.05 thick whose top is 2.40 up. Open underneath: people walk through along X.
                        ScaffoldFrame(2.40f);
                        B.Box(0f, 2.375f, 0.90f, 2.40f, 0.05f, 1.70f, Board);
                        break;
                    case "scaffold_bay_upper":
                        // A lift above the first: 2.00 high, the same plan, with a toe board and a guard rail (1.00 up) on the outer face.
                        ScaffoldFrame(2.00f);
                        B.Box(0f, 1.975f, 0.90f, 2.40f, 0.05f, 1.70f, Board);
                        B.Box(0f, 0.10f, 1.76f, 2.40f, 0.15f, 0.03f, Board);
                        B.Box(0f, 1.00f, 1.76f, 2.40f, 0.048f, 0.048f, Steel);
                        break;
                    case "scaffold_net":
                        // 2.40 x 2.00, 0.01 thick: debris netting on the outer face of an upper bay. Pivot as the bay's (so z = 1.80).
                        B.Box(0f, 1.00f, 1.80f, 2.38f, 1.96f, 0.01f, Net);
                        break;
                    case "scaffold_fan":
                        // The protection fan over a covered walkway: a board sloping out 0.9 m from the first deck's outer edge. Pivot as the bay's.
                        B.Box(0f, 2.62f, 2.20f, 2.40f, 0.04f, 0.90f, Board, 0f, -28f);
                        break;
                    case "skip_bin":
                        // 3.40 long (X) x 1.25 high x 1.70 wide (Z): a builder's skip, yellow, sloping ends suggested by a narrower base.
                        B.Box(0f, 0.30f, 0f, 2.60f, 0.60f, 1.50f, Yellow);
                        B.Box(0f, 0.90f, 0f, 3.40f, 0.60f, 1.70f, Yellow);
                        B.Box(0f, 1.16f, 0f, 3.10f, 0.14f, 1.40f, Earth);
                        B.Box(0f, 0.75f, 0.86f, 1.20f, 0.20f, 0.01f, Dark);
                        break;
                    case "site_hoarding":
                        // 2.40 wide (X) x 2.40 high x 0.10: one painted timber hoarding panel. Pivot: ground centre; its public face is +Z.
                        B.Box(0f, 1.20f, 0f, 2.40f, 2.40f, 0.04f, HoardingBlue);
                        B.Box(0f, 2.30f, 0.022f, 2.40f, 0.14f, 0.008f, White);
                        B.Box(-1.17f, 1.20f, -0.05f, 0.06f, 2.40f, 0.08f, Board);
                        B.Box(1.17f, 1.20f, -0.05f, 0.06f, 2.40f, 0.08f, Board);
                        break;
                    case "site_hoarding_notice":
                        // A hoarding panel carrying the "opening soon" board: 1.2 x 0.8 white, centred 1.5 m up.
                        B.Box(0f, 1.20f, 0f, 2.40f, 2.40f, 0.04f, HoardingBlue);
                        B.Box(0f, 2.30f, 0.022f, 2.40f, 0.14f, 0.008f, White);
                        B.Box(0f, 1.50f, 0.024f, 1.20f, 0.80f, 0.008f, White);
                        B.Box(0f, 1.66f, 0.03f, 0.90f, 0.10f, 0.004f, Dark);
                        B.Box(0f, 1.44f, 0.03f, 0.70f, 0.06f, 0.004f, Dark);
                        B.Box(0f, 1.30f, 0.03f, 0.50f, 0.06f, 0.004f, Dark);
                        break;
                    case "site_notice_board":
                        // 0.90 x 1.80 x 0.45: a notice on two posts, read from +Z; the board is 0.9 x 0.65, its centre 1.45 m up.
                        B.Box(-0.38f, 0.90f, 0f, 0.05f, 1.80f, 0.05f, Steel);
                        B.Box(0.38f, 0.90f, 0f, 0.05f, 1.80f, 0.05f, Steel);
                        B.Box(0f, 1.45f, 0.03f, 0.90f, 0.65f, 0.02f, White);
                        B.Box(0f, 1.70f, 0.042f, 0.86f, 0.12f, 0.004f, HoardingBlue);
                        B.Box(0f, 1.50f, 0.042f, 0.66f, 0.05f, 0.004f, Dark);
                        B.Box(0f, 1.40f, 0.042f, 0.66f, 0.05f, 0.004f, Dark);
                        B.Box(0f, 1.30f, 0.042f, 0.46f, 0.05f, 0.004f, Dark);
                        B.Box(-0.38f, 0.03f, 0f, 0.10f, 0.06f, 0.45f, Dark);
                        B.Box(0.38f, 0.03f, 0f, 0.10f, 0.06f, 0.45f, Dark);
                        break;
                    case "step_ladder":
                        // The bundle's own size (0.47 x 0.72 x 0.75; climbed from -Z, the first tread 0.24 up and 0.24 back): used only if the bundle is missing.
                        B.Box(0f, 0.70f, 0f, 0.40f, 0.04f, 0.30f, Steel);
                        B.Box(0f, 0.46f, -0.12f, 0.40f, 0.04f, 0.09f, Steel);
                        B.Box(0f, 0.22f, -0.24f, 0.40f, 0.04f, 0.09f, Steel);
                        B.Box(-0.21f, 0.36f, 0.24f, 0.03f, 0.72f, 0.03f, Steel);
                        B.Box(0.21f, 0.36f, 0.24f, 0.03f, 0.72f, 0.03f, Steel);
                        B.Box(-0.21f, 0.36f, -0.16f, 0.03f, 0.72f, 0.03f, Steel, 24f);
                        B.Box(0.21f, 0.36f, -0.16f, 0.03f, 0.72f, 0.03f, Steel, 24f);
                        break;
                    case "bucket":
                        B.Box(0f, 0.14f, 0f, 0.28f, 0.28f, 0.28f, White);
                        break;
                    // for Faults/FaultWorld
                    case "pothole":
                        // 1.00 x 0.02 x 0.85: a hole in the road, its broken edge lighter than the road, a dark wet bottom, loose chips round it.
                        // Pivot: road level at its middle; it lies along Z.
                        B.Box(0f, 0.004f, 0f, 0.95f, 0.008f, 0.55f, Broken);
                        B.Box(0.05f, 0.004f, 0.02f, 0.60f, 0.008f, 0.85f, Broken);
                        B.Box(0f, 0.007f, 0f, 0.74f, 0.008f, 0.40f, Asphalt);
                        B.Box(0.04f, 0.007f, 0.02f, 0.44f, 0.008f, 0.66f, Asphalt);
                        B.Box(0.02f, 0.010f, 0.01f, 0.52f, 0.006f, 0.30f, Hole);
                        B.Box(-0.52f, 0.015f, 0.12f, 0.08f, 0.03f, 0.06f, Broken);
                        B.Box(0.41f, 0.012f, -0.31f, 0.06f, 0.024f, 0.07f, Broken);
                        B.Box(0.12f, 0.012f, 0.47f, 0.07f, 0.024f, 0.05f, Broken);
                        break;
                    case "litter_pile":
                        // 0.43 x 0.10 x 0.41, as the bundle's model: newspaper, a paper cup, a crushed can, a carton, a crisp packet. Pivot: ground centre.
                        B.Box(0f, 0.004f, 0f, 0.36f, 0.008f, 0.26f, Paper);
                        B.Box(0.12f, 0.04f, 0.10f, 0.08f, 0.08f, 0.10f, White, 90f);
                        B.Box(-0.13f, 0.035f, -0.09f, 0.065f, 0.065f, 0.12f, Red, 90f);
                        B.Box(-0.05f, 0.03f, 0.13f, 0.16f, 0.05f, 0.10f, Board);
                        B.Box(0.10f, 0.012f, -0.14f, 0.12f, 0.02f, 0.09f, Yellow);
                        break;
                    default:
                        return null;
                }
                return B.End(id);
            }

            // 0.60 x 0.85 x 0.012: a poster. Pivot: the centre of its BACK (it is stuck to a wall); its face is +Z.
            private static void Poster(Color party)
            {
                B.Box(0f, 0f, 0.004f, 0.60f, 0.85f, 0.008f, White);
                B.Box(0f, 0.27f, 0.009f, 0.56f, 0.27f, 0.004f, party);
                B.Box(0f, -0.06f, 0.009f, 0.30f, 0.30f, 0.004f, Dark);
                B.Box(0f, -0.31f, 0.009f, 0.56f, 0.12f, 0.004f, party);
            }

            // The sandwich board's size (0.60 x 0.93 x 0.46) in a party's colour: two leaning faces, +Z and -Z.
            private static void CampaignBoard(Color party)
            {
                B.Box(0f, 0.46f, 0.12f, 0.60f, 0.93f, 0.02f, party, -14f);
                B.Box(0f, 0.46f, -0.12f, 0.60f, 0.93f, 0.02f, party, 14f);
                B.Box(0f, 0.55f, 0.145f, 0.50f, 0.36f, 0.008f, White, -14f);
                B.Box(0f, 0.55f, -0.145f, 0.50f, 0.36f, 0.008f, White, 14f);
            }

            private static void ScaffoldFrame(float height)
            {
                const float tube = 0.048f;
                B.Box(-1.18f, height * 0.5f, 0.06f, tube, height, tube, Steel);
                B.Box(1.18f, height * 0.5f, 0.06f, tube, height, tube, Steel);
                B.Box(-1.18f, height * 0.5f, 1.74f, tube, height, tube, Steel);
                B.Box(1.18f, height * 0.5f, 1.74f, tube, height, tube, Steel);
                B.Box(0f, height - 0.10f, 0.06f, 2.40f, tube, tube, Steel);
                B.Box(0f, height - 0.10f, 1.74f, 2.40f, tube, tube, Steel);
                B.Box(-1.18f, height - 0.10f, 0.90f, tube, tube, 1.70f, Steel);
                B.Box(1.18f, height - 0.10f, 0.90f, tube, tube, 1.70f, Steel);
                B.Box(-1.18f, 0.02f, 0.06f, 0.15f, 0.04f, 0.15f, Dark);
                B.Box(1.18f, 0.02f, 0.06f, 0.15f, 0.04f, 0.15f, Dark);
                B.Box(-1.18f, 0.02f, 1.74f, 0.15f, 0.04f, 0.15f, Dark);
                B.Box(1.18f, 0.02f, 1.74f, 0.15f, 0.04f, 0.15f, Dark);
            }
        }

        /// <summary>Boxes merged into one mesh, a sub-mesh per colour. Reused: one build at a time, on first use of an id only.</summary>
        private sealed class Builder
        {
            private readonly List<Vector3> _vertices = new List<Vector3>(512);
            private readonly List<Vector3> _normals = new List<Vector3>(512);
            private readonly List<Color> _colours = new List<Color>(6);
            private readonly List<List<int>> _triangles = new List<List<int>>(6);

            public void Begin()
            {
                _vertices.Clear();
                _normals.Clear();
                _colours.Clear();
                for (int i = 0; i < _triangles.Count; i++)
                {
                    _triangles[i].Clear();
                }
            }

            /// <summary>A box by its centre and size; <paramref name="lean"/> tips it about its own X axis, <paramref name="pitch"/> likewise after (degrees).</summary>
            public void Box(float x, float y, float z, float sx, float sy, float sz, Color colour, float lean = 0f, float pitch = 0f)
            {
                int slot = _colours.IndexOf(colour);
                if (slot < 0)
                {
                    slot = _colours.Count;
                    _colours.Add(colour);
                    if (_triangles.Count <= slot)
                    {
                        _triangles.Add(new List<int>(256));
                    }
                }
                List<int> triangles = _triangles[slot];
                var centre = new Vector3(x, y, z);
                Quaternion turn = Quaternion.Euler(lean + pitch, 0f, 0f);
                var half = new Vector3(sx * 0.5f, sy * 0.5f, sz * 0.5f);
                for (int face = 0; face < 6; face++)
                {
                    Vector3 normal = FaceNormals[face];
                    Vector3 u = FaceU[face];
                    Vector3 v = Vector3.Cross(normal, u);
                    int first = _vertices.Count;
                    for (int corner = 0; corner < 4; corner++)
                    {
                        float a = corner == 0 || corner == 3 ? -1f : 1f;
                        float b = corner < 2 ? -1f : 1f;
                        Vector3 local = Vector3.Scale(normal + u * a + v * b, half);
                        _vertices.Add(centre + turn * local);
                        _normals.Add(turn * normal);
                    }
                    triangles.Add(first);
                    triangles.Add(first + 2);
                    triangles.Add(first + 1);
                    triangles.Add(first);
                    triangles.Add(first + 3);
                    triangles.Add(first + 2);
                }
            }

            public Kind End(string name)
            {
                if (_vertices.Count == 0)
                {
                    return null;
                }
                var mesh = new Mesh { name = "RealLifeOverhaul Civic " + name };
                mesh.SetVertices(_vertices);
                mesh.SetNormals(_normals);
                mesh.subMeshCount = _colours.Count;
                var materials = new Material[_colours.Count];
                for (int i = 0; i < _colours.Count; i++)
                {
                    mesh.SetTriangles(_triangles[i], i);
                    materials[i] = Shapes.Paint(_colours[i]);
                }
                mesh.RecalculateBounds();
                mesh.UploadMeshData(true);
                return new Kind { Mesh = mesh, Materials = materials, OwnMesh = true };
            }

            private static readonly Vector3[] FaceNormals =
            {
                Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back
            };

            private static readonly Vector3[] FaceU =
            {
                Vector3.forward, Vector3.back, Vector3.right, Vector3.left, Vector3.left, Vector3.right
            };
        }
    }

    /// <summary>The four campaign colours: one per candidate slot of an election. Not any real party's.</summary>
    public static class CivicColours
    {
        private static readonly Color[] Parties =
        {
            new Color(0.80f, 0.42f, 0.08f),   // amber
            new Color(0.10f, 0.50f, 0.52f),   // teal
            new Color(0.48f, 0.22f, 0.55f),   // plum
            new Color(0.42f, 0.52f, 0.14f)    // olive
        };

        public static Color Party(int slot)
        {
            return Parties[(slot % Parties.Length + Parties.Length) % Parties.Length];
        }
    }
}
