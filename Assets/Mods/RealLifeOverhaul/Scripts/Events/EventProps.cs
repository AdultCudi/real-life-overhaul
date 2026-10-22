using System.Collections.Generic;
using UnityEngine;

namespace RealLifeOverhaul.Events
{
    /// <summary>
    /// The props of fires and street events by id: the modelled one when the prop bundle has it, else a stand-in of plain
    /// shapes to the size and pivot of the wish-list in docs/features/city-events.md, so a modelled prop drops in by id.
    /// Made when first wanted and kept on a <see cref="CityProps"/> shelf of its own; nothing is built or destroyed per scene.
    /// </summary>
    internal sealed class EventProps
    {
        public const string Helmet = "fire_helmet";
        public const string AirPack = "fire_air_pack";
        public const string Nozzle = "hose_nozzle";
        public const string Placard = "placard";
        public const string Banner = "banner_pole";
        public const string Bib = "runner_bib";
        public const string FilmCamera = "film_camera";
        public const string LightStand = "light_stand";
        public const string LightBar = "fire_light_bar";
        public const string Clapper = "directors_chair";
        public const string MortarRack = "firework_rack";
        public const string HoseCoil = "hose_coil";
        public const string Bin = "street_bin";

        private static readonly Color FireYellow = new Color(0.93f, 0.78f, 0.08f);
        private static readonly Color Reflective = new Color(0.86f, 0.90f, 0.62f);
        private static readonly Color Black = new Color(0.04f, 0.04f, 0.045f);
        private static readonly Color Steel = new Color(0.55f, 0.57f, 0.60f);
        private static readonly Color Brass = new Color(0.72f, 0.55f, 0.18f);
        private static readonly Color EngineRed = new Color(0.72f, 0.06f, 0.05f);
        private static readonly Color Board = new Color(0.93f, 0.92f, 0.86f);
        private static readonly Color Ink = new Color(0.10f, 0.12f, 0.36f);
        private static readonly Color Wood = new Color(0.52f, 0.38f, 0.22f);
        private static readonly Color Canvas = new Color(0.16f, 0.17f, 0.20f);
        private static readonly Color Lamp = new Color(1f, 0.96f, 0.84f);

        private readonly CityProps _shelf;
        private readonly Transform _root;
        private readonly HashSet<string> _noStandIn = new HashSet<string>();

        public EventProps(Transform parent)
        {
            _root = new GameObject("RealLifeOverhaul Event Props").transform;
            _root.SetParent(parent, false);
            _shelf = new CityProps(_root);
        }

        /// <summary>The shelf itself, handed to a CityPerson so that a clip can take its own tool.</summary>
        public CityProps Shelf => _shelf;

        /// <summary>A prop object, switched on; null only with neither a modelled prop nor a stand-in of that id.</summary>
        public Transform Take(string id)
        {
            Transform prop = _shelf.Take(id);   // one from the shelf, else the modelled prop
            if (prop != null || _noStandIn.Contains(id) || !Shapes.Available)
            {
                return prop;
            }
            var made = new GameObject(id);
            made.transform.SetParent(_root, false);
            if (!Build(id, made.transform))
            {
                Object.Destroy(made);
                _noStandIn.Add(id);
                return null;
            }
            return made.transform;
        }

        public Transform Stand(string id, Vector3 position, float heading)
        {
            Transform prop = Take(id);
            if (prop != null)
            {
                prop.SetParent(_root, true);
                prop.SetPositionAndRotation(position, Quaternion.Euler(0f, heading, 0f));
            }
            return prop;
        }

        public void Return(Transform prop)
        {
            _shelf.Return(prop);
        }

        // The stand-ins, to the wish-list's sizes and pivots.
        private static bool Build(string id, Transform root)
        {
            Mesh cube = Shapes.Cube;
            switch (id)
            {
                case Helmet:
                    // Pivot: centre of the rim's lower edge. A dome, a ridge along it, a brim longer at the back.
                    Shapes.Piece("Dome", root, Shapes.Drum, Shapes.Paint(FireYellow), new Vector3(0f, 0.01f, 0f), new Vector3(0.25f, 0.15f, 0.28f));
                    Shapes.Piece("Ridge", root, cube, Shapes.Paint(FireYellow), new Vector3(0f, 0.165f, -0.01f), new Vector3(0.03f, 0.03f, 0.26f));
                    Shapes.Piece("Brim", root, cube, Shapes.Paint(FireYellow), new Vector3(0f, 0.012f, -0.035f), new Vector3(0.30f, 0.018f, 0.40f));
                    Shapes.Piece("Shield", root, cube, Shapes.Paint(Reflective, 0.6f), new Vector3(0f, 0.085f, 0.136f), new Vector3(0.09f, 0.08f, 0.012f));
                    return true;
                case AirPack:
                    // Pivot: the upper-chest bone. A cylinder on the back in its harness, and reflective bands front and back.
                    Shapes.Piece("Cylinder", root, Shapes.Drum, Shapes.Paint(FireYellow), new Vector3(0f, -0.22f, -0.205f), new Vector3(0.15f, 0.46f, 0.15f));
                    Shapes.Piece("Valve", root, Shapes.Drum, Shapes.Paint(Steel), new Vector3(0f, -0.27f, -0.205f), new Vector3(0.06f, 0.05f, 0.06f));
                    Shapes.Piece("Plate", root, cube, Shapes.Paint(Black), new Vector3(0f, 0f, -0.128f), new Vector3(0.26f, 0.40f, 0.02f));
                    Shapes.Piece("Band Back", root, cube, Shapes.Paint(Reflective, 0.6f), new Vector3(0f, -0.21f, -0.124f), new Vector3(0.34f, 0.05f, 0.014f));
                    Shapes.Piece("Band Front", root, cube, Shapes.Paint(Reflective, 0.6f), new Vector3(0f, -0.02f, 0.132f), new Vector3(0.32f, 0.05f, 0.012f));
                    Shapes.Piece("Strap L", root, cube, Shapes.Paint(Black), new Vector3(-0.11f, 0.06f, 0.128f), new Vector3(0.04f, 0.20f, 0.012f));
                    Shapes.Piece("Strap R", root, cube, Shapes.Paint(Black), new Vector3(0.11f, 0.06f, 0.128f), new Vector3(0.04f, 0.20f, 0.012f));
                    return true;
                case Nozzle:
                    // Pivot: the coupling at its back end; it points along +Z. 0.42 m long, gripped 0.10 and 0.30 m along it.
                    Barrel("Body", root, Shapes.Paint(EngineRed), 0.0f, 0.30f, 0.055f);
                    Barrel("Tip", root, Shapes.Paint(Brass), 0.30f, 0.42f, 0.04f);
                    Barrel("Coupling", root, Shapes.Paint(Brass), -0.05f, 0.0f, 0.07f);
                    Shapes.Piece("Bale", root, cube, Shapes.Paint(Black), new Vector3(0f, 0.05f, 0.20f), new Vector3(0.09f, 0.035f, 0.02f));
                    return true;
                case Placard:
                    // Held like the stop paddle (the clip's hand is 1.10 m above the pivot), but carried clear of the ground: the staff runs from 0.70 to 1.75 m.
                    Shapes.Piece("Staff", root, cube, Shapes.Paint(Wood), new Vector3(0f, 1.225f, 0f), new Vector3(0.028f, 1.05f, 0.028f));
                    Shapes.Piece("Board", root, cube, Shapes.Paint(Board), new Vector3(0f, 1.98f, 0.016f), new Vector3(0.56f, 0.42f, 0.012f));
                    Shapes.Piece("Line 1", root, cube, Shapes.Paint(Ink), new Vector3(0f, 2.07f, 0.024f), new Vector3(0.44f, 0.07f, 0.004f));
                    Shapes.Piece("Line 2", root, cube, Shapes.Paint(Ink), new Vector3(0f, 1.95f, 0.024f), new Vector3(0.36f, 0.05f, 0.004f));
                    Shapes.Piece("Line 3", root, cube, Shapes.Paint(EngineRed), new Vector3(0f, 1.85f, 0.024f), new Vector3(0.40f, 0.05f, 0.004f));
                    return true;
                case Banner:
                    // The same hold; a taller staff with a pennant-shaped cloth.
                    Shapes.Piece("Staff", root, cube, Shapes.Paint(Wood), new Vector3(0f, 1.45f, 0f), new Vector3(0.03f, 1.50f, 0.03f));
                    Shapes.Piece("Cloth", root, cube, Shapes.Paint(new Color(0.12f, 0.30f, 0.62f)), new Vector3(0.36f, 1.92f, 0f), new Vector3(0.70f, 0.48f, 0.008f));
                    Shapes.Piece("Stripe", root, cube, Shapes.Paint(new Color(0.92f, 0.80f, 0.20f)), new Vector3(0.36f, 1.92f, 0f), new Vector3(0.70f, 0.10f, 0.012f));
                    return true;
                case Bib:
                    // Pivot: the upper-chest bone. A numbered square on the chest.
                    Shapes.Piece("Bib", root, cube, Shapes.Paint(Board), new Vector3(0f, -0.07f, 0.134f), new Vector3(0.22f, 0.17f, 0.008f));
                    Shapes.Piece("Number", root, cube, Shapes.Paint(Black), new Vector3(0f, -0.07f, 0.139f), new Vector3(0.12f, 0.07f, 0.004f));
                    return true;
                case FilmCamera:
                    // Pivot: ground centre of the tripod; the lens looks along +Z, 1.45 m up.
                    Leg(root, 0f); Leg(root, 120f); Leg(root, 240f);
                    Shapes.Piece("Head", root, Shapes.Drum, Shapes.Paint(Black), new Vector3(0f, 1.28f, 0f), new Vector3(0.12f, 0.08f, 0.12f));
                    Shapes.Piece("Body", root, cube, Shapes.Paint(Canvas), new Vector3(0f, 1.47f, 0f), new Vector3(0.20f, 0.22f, 0.42f));
                    Barrel("Lens", root, Shapes.Paint(Black), 0.21f, 0.40f, 0.13f).localPosition += new Vector3(0f, 1.47f, 0f);
                    Shapes.Piece("Magazine", root, cube, Shapes.Paint(Black), new Vector3(0f, 1.63f, -0.08f), new Vector3(0.12f, 0.10f, 0.24f));
                    Shapes.Piece("Eyepiece", root, cube, Shapes.Paint(Black), new Vector3(-0.13f, 1.50f, -0.16f), new Vector3(0.06f, 0.06f, 0.14f));
                    return true;
                case LightStand:
                    // Pivot: ground centre; the lamp, 2.3 m up, shines along +Z.
                    Leg(root, 60f); Leg(root, 180f); Leg(root, 300f);
                    Shapes.Piece("Mast", root, Shapes.Drum, Shapes.Paint(Black), new Vector3(0f, 1.2f, 0f), new Vector3(0.045f, 1.0f, 0.045f));
                    Shapes.Piece("Housing", root, cube, Shapes.Paint(Black), new Vector3(0f, 2.32f, -0.03f), new Vector3(0.50f, 0.36f, 0.16f));
                    Shapes.Piece("Lamp", root, cube, Shapes.Paint(Lamp, 6f), new Vector3(0f, 2.32f, 0.055f), new Vector3(0.44f, 0.30f, 0.02f));
                    return true;
                case Clapper:
                    // Pivot: ground centre; a folding director's chair facing +Z.
                    Shapes.Piece("Seat", root, cube, Shapes.Paint(Canvas), new Vector3(0f, 0.62f, 0f), new Vector3(0.50f, 0.03f, 0.42f));
                    Shapes.Piece("Back", root, cube, Shapes.Paint(Canvas), new Vector3(0f, 0.98f, -0.21f), new Vector3(0.50f, 0.22f, 0.02f));
                    Shapes.Piece("Side L", root, cube, Shapes.Paint(Wood), new Vector3(-0.26f, 0.45f, 0f), new Vector3(0.03f, 0.90f, 0.42f));
                    Shapes.Piece("Side R", root, cube, Shapes.Paint(Wood), new Vector3(0.26f, 0.45f, 0f), new Vector3(0.03f, 0.90f, 0.42f));
                    return true;
                case MortarRack:
                    // Pivot: ground centre; a rack of launch tubes behind a low board.
                    Shapes.Piece("Base", root, cube, Shapes.Paint(Wood), new Vector3(0f, 0.05f, 0f), new Vector3(1.2f, 0.10f, 0.5f));
                    for (int i = 0; i < 5; i++)
                    {
                        Shapes.Piece("Tube", root, Shapes.Drum, Shapes.Paint(Black), new Vector3(-0.44f + i * 0.22f, 0.10f, 0f), new Vector3(0.12f, 0.45f, 0.12f));
                    }
                    return true;
                case HoseCoil:
                    // Pivot: ground centre; a rolled length of hose lying flat.
                    Shapes.Piece("Roll", root, Shapes.Drum, Shapes.Paint(FireYellow), Vector3.zero, new Vector3(0.52f, 0.09f, 0.52f));
                    Shapes.Piece("Core", root, Shapes.Drum, Shapes.Paint(Black), new Vector3(0f, 0.002f, 0f), new Vector3(0.16f, 0.092f, 0.16f));
                    Barrel("Coupling", root, Shapes.Paint(Brass), 0.26f, 0.36f, 0.07f).localPosition += new Vector3(0f, 0.045f, 0f);
                    return true;
                case Bin:
                    // Pivot: ground centre. A round litter bin with a band and an open top.
                    Shapes.Piece("Body", root, Shapes.Drum, Shapes.Paint(new Color(0.09f, 0.20f, 0.14f)), Vector3.zero, new Vector3(0.50f, 0.86f, 0.50f));
                    Shapes.Piece("Band", root, Shapes.Drum, Shapes.Paint(new Color(0.72f, 0.60f, 0.16f)), new Vector3(0f, 0.60f, 0f), new Vector3(0.515f, 0.07f, 0.515f));
                    Shapes.Piece("Rim", root, Shapes.Drum, Shapes.Paint(Black), new Vector3(0f, 0.86f, 0f), new Vector3(0.54f, 0.04f, 0.54f));
                    Shapes.Piece("Foot", root, Shapes.Drum, Shapes.Paint(Black), Vector3.zero, new Vector3(0.40f, 0.03f, 0.40f));
                    return true;
                default:
                    return false;
            }
        }

        // A round bar lying along +Z from one distance to another (the drum stands on its origin along Y).
        private static Transform Barrel(string name, Transform root, Material paint, float from, float to, float width)
        {
            Transform piece = Shapes.Piece(name, root, Shapes.Drum, paint, new Vector3(0f, 0f, from), new Vector3(width, to - from, width));
            piece.localRotation = Quaternion.Euler(90f, 0f, 0f);
            return piece;
        }

        // One leg of a tripod: from the ground 0.45 m out to the head 1.25 m up.
        private static void Leg(Transform root, float heading)
        {
            Vector3 foot = Quaternion.Euler(0f, heading, 0f) * new Vector3(0f, 0f, 0.45f);
            Vector3 top = new Vector3(0f, 1.25f, 0f);
            Transform leg = Shapes.Piece("Leg", root, Shapes.Cube, Shapes.Paint(Black), (foot + top) * 0.5f, new Vector3(0.03f, (top - foot).magnitude, 0.03f));
            leg.localRotation = Quaternion.FromToRotation(Vector3.up, (top - foot).normalized);
        }
    }
}
