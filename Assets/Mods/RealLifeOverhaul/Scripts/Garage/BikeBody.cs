using System.Collections.Generic;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>What sets one two-wheeler apart: what it costs, how it goes, and the points its rider touches.</summary>
    public sealed class BikeDef
    {
        public BikeKind Kind;
        public string Name;            // locale key
        public string Prop;            // the modelled body: this id is the whole bike as one mesh, and <id>_frame, _fork, ... are its parts (PropAssets/README.md, "Garage")
        public string Ride, Coast, Stand;   // the rider's clips by what they are doing (CityAssets/README.md, "Garage"); Coast is a bicycle's only
        public bool Motor;
        public int Price;
        public int Colour;             // 0xRRGGBB of the frame or tank: the colour the modelled bike is painted in the atlas, and the tint of its paint parts

        // a bicycle: human-powered
        public float CruiseKmh;        // what steady pedalling holds on the flat
        public float PowerKw;          // the "engine" behind the pedals, sized for the game's 250 kg scooter body
        public float EnergyPerSecond;  // of pedalling

        // a motorbike: targets for the car model
        public float Seconds;          // 0-100 km/h
        public float TopKmh;

        // where the rider meets it, in the vehicle's own space (metres, origin on the ground, +Z forwards)
        public float RearZ, FrontZ, WheelRadius;
        public Vector2 Seat;           // (height of the seat's top, z)
        public Vector3 Grip;           // (x half width, height, z) of the middle of a hand grip, which lies along X: per model, measured on its bar by PropAssets/scripts/checks_garage.py
        public Vector2 Crank;          // bicycle: (height, z) of the bottom bracket
        public Vector3 Peg;            // motorbike: (x half width, height, z) of a footpeg
        public Vector2 HeadTop, HeadBottom;   // the steering axis: (height, z) of its two ends

        public bool Bicycle => !Motor;
    }

    /// <summary>The four two-wheelers.</summary>
    public static class BikeCatalogue
    {
        public const float CrankLength = 0.17f;
        public const float PedalOut = 0.13f;      // a pedal's middle from the centre line
        public const int HelmetPrice = 120;
        public const int DockFee = 15;            // a hire from a bike-share dock, until the end of the next day
        public const int LateFee = 25;            // a hired bicycle not brought back
        public const int BringFee = 20;           // "Bring it here" from further off than a short walk
        public const int HelmetFine = 60;
        public const int ExhaustFine = 120;
        public const float EnergyFloor = 14f;     // pedalling never takes the player below this (the game faints them near zero)

        // The game's scooter, as its prefab is built: where its two wheel controllers are.
        public const float ScooterRearZ = -0.651f;
        public const float ScooterFrontZ = 0.723f;

        public static readonly BikeDef[] All =
        {
            new BikeDef
            {
                Kind = BikeKind.CityBicycle, Name = "reallife_bike_name_city", Prop = "bicycle_city", Price = 350, Colour = 0x124D70,
                Ride = "cycle_ride", Coast = "cycle_coast", Stand = "cycle_stand",
                CruiseKmh = 24f, PowerKw = 1.2f, EnergyPerSecond = 0.06f,
                RearZ = -0.52f, FrontZ = 0.58f, WheelRadius = 0.34f, Seat = new Vector2(0.93f, -0.30f), Grip = new Vector3(0.26f, 1.14f, 0.16f),
                Crank = new Vector2(0.29f, -0.08f), HeadTop = new Vector2(1.00f, 0.36f), HeadBottom = new Vector2(0.70f, 0.44f)
            },
            new BikeDef
            {
                Kind = BikeKind.RoadBicycle, Name = "reallife_bike_name_road", Prop = "bicycle_road", Price = 900, Colour = 0x9E090A,
                Ride = "cycle_ride_road", Coast = "cycle_coast_road", Stand = "cycle_stand_road",
                CruiseKmh = 32f, PowerKw = 1.6f, EnergyPerSecond = 0.08f,
                // the grips are the TOPS of its drop bar, either side of the stem: the one part of a drop bar that lies along X
                RearZ = -0.49f, FrontZ = 0.55f, WheelRadius = 0.34f, Seat = new Vector2(0.95f, -0.29f), Grip = new Vector3(0.13f, 1.00f, 0.42f),
                Crank = new Vector2(0.28f, -0.07f), HeadTop = new Vector2(0.99f, 0.36f), HeadBottom = new Vector2(0.72f, 0.43f)
            },
            new BikeDef
            {
                Kind = BikeKind.StandardMotorbike, Name = "reallife_bike_name_standard", Prop = "motorbike_standard", Motor = true, Price = 7500, Colour = 0x090F2B,
                Ride = "moto_ride", Stand = "moto_stand",
                Seconds = 6.0f, TopKmh = 150f,
                RearZ = ScooterRearZ, FrontZ = ScooterFrontZ, WheelRadius = 0.33f, Seat = new Vector2(0.83f, -0.20f), Grip = new Vector3(0.31f, 1.075f, 0.30f),
                Peg = new Vector3(0.24f, 0.34f, -0.10f), HeadTop = new Vector2(0.98f, 0.40f), HeadBottom = new Vector2(0.80f, 0.47f)
            },
            new BikeDef
            {
                Kind = BikeKind.SportMotorbike, Name = "reallife_bike_name_sport", Prop = "motorbike_sport", Motor = true, Price = 18000, Colour = 0x9E090A,
                Ride = "moto_ride_sport", Stand = "moto_stand_sport",
                Seconds = 4.5f, TopKmh = 190f,
                // the grips are its clip-ons, on the fork legs under the top yoke
                RearZ = ScooterRearZ, FrontZ = ScooterFrontZ, WheelRadius = 0.33f, Seat = new Vector2(0.84f, -0.22f), Grip = new Vector3(0.27f, 0.915f, 0.42f),
                Peg = new Vector3(0.24f, 0.38f, -0.16f), HeadTop = new Vector2(0.96f, 0.41f), HeadBottom = new Vector2(0.78f, 0.48f)
            }
        };

        public static BikeDef Of(BikeKind kind)
        {
            return All[(int)kind];
        }
    }

    /// <summary>Collects boxes, tubes, rings and balls into one mesh. For the stand-in bodies only; run a handful of times per session.</summary>
    internal sealed class MeshMaker
    {
        private readonly List<Vector3> _points = new List<Vector3>(512);
        private readonly List<Vector3> _normals = new List<Vector3>(512);
        private readonly List<int> _triangles = new List<int>(1536);

        public bool Empty => _points.Count == 0;

        /// <summary>A round tube between two points, closed at both ends.</summary>
        public void Tube(Vector3 a, Vector3 b, float radius, int sides = 8)
        {
            Vector3 along = b - a;
            if (along.sqrMagnitude < 1e-8f)
            {
                return;
            }
            Quaternion turn = Quaternion.FromToRotation(Vector3.up, along.normalized);
            int start = _points.Count;
            for (int i = 0; i <= sides; i++)
            {
                float angle = i * Mathf.PI * 2f / sides;
                Vector3 spoke = turn * new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                _points.Add(a + spoke * radius);
                _normals.Add(spoke);
                _points.Add(b + spoke * radius);
                _normals.Add(spoke);
            }
            for (int i = 0; i < sides; i++)
            {
                int p = start + i * 2;
                Quad(p, p + 1, p + 3, p + 2);
            }
            Cap(a, -along.normalized, turn, radius, sides, true);
            Cap(b, along.normalized, turn, radius, sides, false);
        }

        private void Cap(Vector3 centre, Vector3 normal, Quaternion turn, float radius, int sides, bool flip)
        {
            int start = _points.Count;
            _points.Add(centre);
            _normals.Add(normal);
            for (int i = 0; i < sides; i++)
            {
                float angle = i * Mathf.PI * 2f / sides;
                _points.Add(centre + turn * new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius);
                _normals.Add(normal);
            }
            for (int i = 0; i < sides; i++)
            {
                int p = start + 1 + i;
                int q = start + 1 + (i + 1) % sides;
                if (flip)
                {
                    Triangle(start, p, q);
                }
                else
                {
                    Triangle(start, q, p);
                }
            }
        }

        /// <summary>A box with its own faces (flat shaded).</summary>
        public void Box(Vector3 centre, Vector3 size, Quaternion turn)
        {
            Vector3 h = size * 0.5f;
            Face(centre, turn, new Vector3(0f, 0f, 1f), new Vector3(1f, 0f, 0f), new Vector3(0f, 1f, 0f), h.z, h.x, h.y);
            Face(centre, turn, new Vector3(0f, 0f, -1f), new Vector3(-1f, 0f, 0f), new Vector3(0f, 1f, 0f), h.z, h.x, h.y);
            Face(centre, turn, new Vector3(1f, 0f, 0f), new Vector3(0f, 0f, -1f), new Vector3(0f, 1f, 0f), h.x, h.z, h.y);
            Face(centre, turn, new Vector3(-1f, 0f, 0f), new Vector3(0f, 0f, 1f), new Vector3(0f, 1f, 0f), h.x, h.z, h.y);
            Face(centre, turn, new Vector3(0f, 1f, 0f), new Vector3(1f, 0f, 0f), new Vector3(0f, 0f, -1f), h.y, h.x, h.z);
            Face(centre, turn, new Vector3(0f, -1f, 0f), new Vector3(1f, 0f, 0f), new Vector3(0f, 0f, 1f), h.y, h.x, h.z);
        }

        public void Box(Vector3 centre, Vector3 size)
        {
            Box(centre, size, Quaternion.identity);
        }

        private void Face(Vector3 centre, Quaternion turn, Vector3 normal, Vector3 right, Vector3 up, float out_, float halfRight, float halfUp)
        {
            int start = _points.Count;
            Vector3 n = turn * normal;
            Vector3 r = turn * right * halfRight;
            Vector3 u = turn * up * halfUp;
            Vector3 middle = centre + n * out_;
            _points.Add(middle - r - u);
            _points.Add(middle + r - u);
            _points.Add(middle + r + u);
            _points.Add(middle - r + u);
            for (int i = 0; i < 4; i++)
            {
                _normals.Add(n);
            }
            Quad(start, start + 1, start + 2, start + 3);
        }

        /// <summary>A ring round the X axis (a tyre, a rim).</summary>
        public void Ring(Vector3 centre, float major, float minor, int segments = 24, int sides = 6)
        {
            int start = _points.Count;
            for (int i = 0; i <= segments; i++)
            {
                float around = i * Mathf.PI * 2f / segments;
                var outward = new Vector3(0f, Mathf.Cos(around), Mathf.Sin(around));
                for (int j = 0; j <= sides; j++)
                {
                    float tilt = j * Mathf.PI * 2f / sides;
                    Vector3 normal = outward * Mathf.Cos(tilt) + Vector3.right * Mathf.Sin(tilt);
                    _points.Add(centre + outward * major + normal * minor);
                    _normals.Add(normal);
                }
            }
            int row = sides + 1;
            for (int i = 0; i < segments; i++)
            {
                for (int j = 0; j < sides; j++)
                {
                    int p = start + i * row + j;
                    Quad(p, p + row, p + row + 1, p + 1);
                }
            }
        }

        /// <summary>A ball with its own radius on each axis (a tank, a fairing, a helmet).</summary>
        public void Ball(Vector3 centre, Vector3 radii, int segments = 12, int rings = 8)
        {
            int start = _points.Count;
            for (int i = 0; i <= rings; i++)
            {
                float down = i * Mathf.PI / rings;
                for (int j = 0; j <= segments; j++)
                {
                    float around = j * Mathf.PI * 2f / segments;
                    var unit = new Vector3(Mathf.Sin(down) * Mathf.Cos(around), Mathf.Cos(down), Mathf.Sin(down) * Mathf.Sin(around));
                    _points.Add(centre + Vector3.Scale(unit, radii));
                    _normals.Add(new Vector3(unit.x / radii.x, unit.y / radii.y, unit.z / radii.z).normalized);
                }
            }
            int row = segments + 1;
            for (int i = 0; i < rings; i++)
            {
                for (int j = 0; j < segments; j++)
                {
                    int p = start + i * row + j;
                    Quad(p, p + 1, p + row + 1, p + row);
                }
            }
        }

        private void Quad(int a, int b, int c, int d)
        {
            Triangle(a, b, c);
            Triangle(a, c, d);
        }

        private void Triangle(int a, int b, int c)
        {
            _triangles.Add(a);
            _triangles.Add(b);
            _triangles.Add(c);
        }

        public Mesh Make(string name)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(_points);
            mesh.SetNormals(_normals);
            mesh.SetTriangles(_triangles, 0);
            mesh.RecalculateBounds();
            _points.Clear();
            _normals.Clear();
            _triangles.Clear();
            return mesh;
        }
    }

    /// <summary>
    /// The bodies the two-wheelers wear. A modelled body from the prop bundle when it has one
    /// (<c>bicycle_city</c>, <c>bicycle_road</c>, <c>motorbike_standard</c>, <c>motorbike_sport</c>): put
    /// together from its parts (<c>_frame</c>, <c>_fork</c>, <c>_wheel</c> or <c>_wheel_front</c> and
    /// <c>_wheel_rear</c>, <c>_crank</c>, <c>_pedal</c>), each hung on the object that turns it, so that the
    /// wheels spin, the cranks go round with the pedals level and the bars and front wheel steer; what is
    /// painted in the bike's colour is a part of its own on a neutral swatch (<c>_frame_paint</c>,
    /// <c>_fork_paint</c>) drawn with <see cref="PropLibrary.Tinted"/>, one shared material a colour. A bike
    /// that only stands on show in the model's own colour is the whole model as one mesh instead (one draw
    /// call). Until the bundle has a model, a stand-in built here from tubes, rings and boxes that still
    /// reads as what it is: a diamond frame, two spoked wheels, handlebar, saddle, cranks and pedals; or a
    /// tank, seat, engine, forks, exhaust and fat tyres. Each stand-in is a few shared meshes in three
    /// colours (paint, black, metal), made once per kind; a bike in the world is about a dozen renderers
    /// with nothing of their own. The parts that move are separate objects: the steering assembly, the two
    /// wheels, the crank. Nothing here runs per frame: <see cref="BikeMount"/> turns them, and only while ridden.
    /// </summary>
    public sealed class BikeBody
    {
        private const int Paint = 0, Black = 1, Metal = 2;

        // The neutral swatch the paint parts are modelled on (the atlas's "garage_white"): a tint is divided by
        // it, so that what is drawn is the colour asked for.
        private static readonly Color PaintBase = new Color(0.90f, 0.90f, 0.88f);

        private sealed class Meshes
        {
            public readonly Mesh[] Main = new Mesh[3];
            public readonly Mesh[] Steer = new Mesh[3];
            public readonly Mesh[] Wheel = new Mesh[3];
            public readonly Mesh[] Crank = new Mesh[3];
            public Mesh Pedal;
        }

        private static readonly Meshes[] Made = new Meshes[4];
        private static Mesh _helmet;
        private static Mesh _visor;

        public GameObject Root;
        public Transform Steer;         // turns about the steering axis
        public Transform FrontWheel;    // spin about their own X; null on a body that is one mesh
        public Transform RearWheel;
        public Transform Crank;         // bicycles: spins about its own X
        public Transform PedalLeft, PedalRight;
        public Transform HandLeft, HandRight;   // where the hands go, on the steering assembly
        public Vector3 SteerAxis;       // in the steering assembly's own space
        public bool Modelled;

        /// <summary>
        /// Builds a body under a parent, standing on the parent's origin and facing its +Z. colour is 0xRRGGBB,
        /// or negative for the model's own. still: it only stands on show and nothing of it will ever turn.
        /// </summary>
        public static BikeBody Build(BikeDef def, Transform parent, int colour, bool still = false)
        {
            if (!Shapes.Available)
            {
                return null;
            }
            var body = new BikeBody();
            body.Root = new GameObject("RealLifeOverhaul " + def.Kind);
            Transform root = body.Root.transform;
            root.SetParent(parent, false);

            var headTop = new Vector3(0f, def.HeadTop.x, def.HeadTop.y);
            var headBottom = new Vector3(0f, def.HeadBottom.x, def.HeadBottom.y);
            body.SteerAxis = (headTop - headBottom).normalized;
            body.Steer = Child("Steering", root, headTop);
            body.HandLeft = Child("Left Hand", body.Steer, new Vector3(-def.Grip.x, def.Grip.y, def.Grip.z) - headTop);
            body.HandRight = Child("Right Hand", body.Steer, new Vector3(def.Grip.x, def.Grip.y, def.Grip.z) - headTop);

            // A modelled body. In parts whenever it may move or is another colour than the model's; as the one
            // complete mesh when it only stands on show in its own colour, or when the bundle has no parts (an older
            // one): its wheels do not turn then and its bars do not steer. The rider's hands and feet are placed
            // the same either way.
            int rgb = colour >= 0 ? colour : def.Colour;
            bool parts = HasParts(def);
            if ((!parts || (still && rgb == def.Colour)) && PropLibrary.Has(def.Prop)
                && PropLibrary.TryGet(def.Prop, out Mesh modelled, out Material atlas) && modelled != null && atlas != null)
            {
                Shapes.Piece(def.Prop, root, modelled, atlas, Vector3.zero, Vector3.one);
                body.Modelled = true;
                return body;
            }
            if (parts && body.Assemble(def, root, headTop, rgb))
            {
                body.Modelled = true;
                return body;
            }

            Meshes meshes = Made[(int)def.Kind] ?? (Made[(int)def.Kind] = Make(def));
            Material[] paints =
            {
                Shapes.Paint(GarageCar.FromRgb(colour >= 0 ? colour : def.Colour)),
                Shapes.Paint(new Color(0.03f, 0.03f, 0.035f)),
                Shapes.Paint(def.Motor ? new Color(0.42f, 0.43f, 0.45f) : new Color(0.66f, 0.67f, 0.69f))
            };
            Pieces("Body", root, meshes.Main, paints);
            Pieces("Fork", body.Steer, meshes.Steer, paints);
            body.FrontWheel = Child("Front Wheel", body.Steer, new Vector3(0f, def.WheelRadius, def.FrontZ) - headTop);
            body.RearWheel = Child("Rear Wheel", root, new Vector3(0f, def.WheelRadius, def.RearZ));
            Pieces("Wheel", body.FrontWheel, meshes.Wheel, paints);
            Pieces("Wheel", body.RearWheel, meshes.Wheel, paints);
            if (def.Bicycle)
            {
                body.Crank = Child("Crank", root, new Vector3(0f, def.Crank.x, def.Crank.y));
                Pieces("Crank", body.Crank, meshes.Crank, paints);
                body.PedalLeft = Shapes.Piece("Pedal", root, meshes.Pedal, paints[Black], Vector3.zero, Vector3.one);
                body.PedalRight = Shapes.Piece("Pedal", root, meshes.Pedal, paints[Black], Vector3.zero, Vector3.one);
                body.Pedals(def, 0f);
            }
            return body;
        }

        private static bool HasParts(BikeDef def)
        {
            string id = def.Prop;
            return PropLibrary.Has(id + "_frame") && PropLibrary.Has(id + "_fork")
                   && (def.Bicycle
                       ? PropLibrary.Has(id + "_wheel") && PropLibrary.Has(id + "_crank") && PropLibrary.Has(id + "_pedal")
                       : PropLibrary.Has(id + "_wheel_front") && PropLibrary.Has(id + "_wheel_rear"));
        }

        // The modelled parts, each on the object BikeMount turns: frame on the root, fork on the steering assembly
        // (its pivot is the top of the steering axis), a wheel on each axle (the front one a child of the fork),
        // the crank set on the bottom bracket, a pedal either side kept level by Pedals. The paint parts are the
        // same pivots as frame and fork, in the tint. Nine renderers at most, all shared meshes and materials.
        private bool Assemble(BikeDef def, Transform root, Vector3 headTop, int rgb)
        {
            Color wanted = GarageCar.FromRgb(rgb);
            Material tint = PropLibrary.Tinted(new Color(Mathf.Clamp01(wanted.r / PaintBase.r), Mathf.Clamp01(wanted.g / PaintBase.g), Mathf.Clamp01(wanted.b / PaintBase.b)));
            string id = def.Prop;
            if (tint == null || Part(id + "_frame", root, null) == null)
            {
                return false;
            }
            Part(id + "_frame_paint", root, tint);
            Part(id + "_fork", Steer, null);
            Part(id + "_fork_paint", Steer, tint);
            FrontWheel = Child("Front Wheel", Steer, new Vector3(0f, def.WheelRadius, def.FrontZ) - headTop);
            RearWheel = Child("Rear Wheel", root, new Vector3(0f, def.WheelRadius, def.RearZ));
            Part(id + (def.Bicycle ? "_wheel" : "_wheel_front"), FrontWheel, null);
            Part(id + (def.Bicycle ? "_wheel" : "_wheel_rear"), RearWheel, null);
            if (def.Bicycle)
            {
                Crank = Child("Crank", root, new Vector3(0f, def.Crank.x, def.Crank.y));
                Part(id + "_crank", Crank, null);
                PedalLeft = Part(id + "_pedal", root, null);
                PedalRight = Part(id + "_pedal", root, null);
                if (PedalLeft == null || PedalRight == null)
                {
                    Crank = null;   // Pedals does nothing then
                }
                Pedals(def, 0f);
            }
            return true;
        }

        // One part, if the bundle has it (a bike need not have every paint part: the road bicycle's fork is black).
        private static Transform Part(string id, Transform parent, Material tint)
        {
            if (!PropLibrary.Has(id) || !PropLibrary.TryGet(id, out Mesh mesh, out Material atlas) || mesh == null || atlas == null)
            {
                return null;
            }
            return Shapes.Piece(id, parent, mesh, tint != null ? tint : atlas, Vector3.zero, Vector3.one);
        }

        /// <summary>Puts the cranks at an angle (radians; 0 = the right crank pointing forwards) with the pedals level.</summary>
        public void Pedals(BikeDef def, float angle)
        {
            if (Crank == null)
            {
                return;
            }
            Crank.localRotation = Quaternion.AngleAxis(-angle * Mathf.Rad2Deg, Vector3.right);
            PedalRight.localPosition = PedalAt(def, angle, 1f);
            PedalLeft.localPosition = PedalAt(def, angle + Mathf.PI, -1f);
        }

        /// <summary>The middle of a pedal, in the body's own space.</summary>
        public static Vector3 PedalAt(BikeDef def, float angle, float side)
        {
            return new Vector3(side * BikeCatalogue.PedalOut, def.Crank.x + Mathf.Sin(angle) * BikeCatalogue.CrankLength, def.Crank.y + Mathf.Cos(angle) * BikeCatalogue.CrankLength);
        }

        /// <summary>A helmet as its own object (it follows the rider's head; it is never put on the character's bones).</summary>
        public static GameObject Helmet(Transform parent)
        {
            if (!Shapes.Available)
            {
                return null;
            }
            var helmet = new GameObject("RealLifeOverhaul Helmet");
            helmet.transform.SetParent(parent, false);
            if (PropLibrary.TryGet("helmet", out Mesh modelled, out Material atlas) && modelled != null && atlas != null)
            {
                Shapes.Piece("helmet", helmet.transform, modelled, atlas, Vector3.zero, Vector3.one);
                return helmet;
            }
            if (_helmet == null)
            {
                var maker = new MeshMaker();
                maker.Ball(new Vector3(0f, 0.105f, 0.012f), new Vector3(0.132f, 0.142f, 0.150f), 14, 10);
                _helmet = maker.Make("RealLifeOverhaul Helmet");
                maker.Box(new Vector3(0f, 0.095f, 0.118f), new Vector3(0.19f, 0.07f, 0.075f));
                _visor = maker.Make("RealLifeOverhaul Visor");
            }
            Shapes.Piece("Shell", helmet.transform, _helmet, Shapes.Paint(new Color(0.08f, 0.08f, 0.09f)), Vector3.zero, Vector3.one);
            Shapes.Piece("Visor", helmet.transform, _visor, Shapes.Paint(new Color(0.02f, 0.03f, 0.05f)), Vector3.zero, Vector3.one);
            return helmet;
        }

        /// <summary>Called when the city unloads: the stand-in meshes go.</summary>
        public static void Dispose()
        {
            for (int i = 0; i < Made.Length; i++)
            {
                Meshes meshes = Made[i];
                if (meshes == null)
                {
                    continue;
                }
                Drop(meshes.Main);
                Drop(meshes.Steer);
                Drop(meshes.Wheel);
                Drop(meshes.Crank);
                if (meshes.Pedal != null)
                {
                    Object.Destroy(meshes.Pedal);
                }
                Made[i] = null;
            }
            if (_helmet != null)
            {
                Object.Destroy(_helmet);
                Object.Destroy(_visor);
                _helmet = null;
                _visor = null;
            }
        }

        private static void Drop(Mesh[] meshes)
        {
            for (int i = 0; i < meshes.Length; i++)
            {
                if (meshes[i] != null)
                {
                    Object.Destroy(meshes[i]);
                    meshes[i] = null;
                }
            }
        }

        private static Transform Child(string name, Transform parent, Vector3 at)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            return go.transform;
        }

        private static void Pieces(string name, Transform parent, Mesh[] meshes, Material[] paints)
        {
            for (int i = 0; i < meshes.Length; i++)
            {
                if (meshes[i] != null)
                {
                    Shapes.Piece(name, parent, meshes[i], paints[i], Vector3.zero, Vector3.one);
                }
            }
        }

        // ---------------------------------------------------------------- the stand-ins

        private static Meshes Make(BikeDef def)
        {
            var meshes = new Meshes();
            var paint = new MeshMaker();
            var black = new MeshMaker();
            var metal = new MeshMaker();
            if (def.Bicycle)
            {
                Bicycle(def, meshes, paint, black, metal);
            }
            else
            {
                Motorbike(def, meshes, paint, black, metal);
            }
            return meshes;
        }

        private static void Keep(Mesh[] into, string name, MeshMaker paint, MeshMaker black, MeshMaker metal)
        {
            into[Paint] = paint.Empty ? null : paint.Make("RealLifeOverhaul " + name + " paint");
            into[Black] = black.Empty ? null : black.Make("RealLifeOverhaul " + name + " black");
            into[Metal] = metal.Empty ? null : metal.Make("RealLifeOverhaul " + name + " metal");
        }

        private static Vector3 P(float x, float y, float z)
        {
            return new Vector3(x, y, z);
        }

        private static void Bicycle(BikeDef def, Meshes meshes, MeshMaker paint, MeshMaker black, MeshMaker metal)
        {
            bool city = def.Kind == BikeKind.CityBicycle;
            float tube = city ? 0.019f : 0.016f;
            float r = def.WheelRadius;
            Vector3 crank = P(0f, def.Crank.x, def.Crank.y);
            Vector3 headTop = P(0f, def.HeadTop.x, def.HeadTop.y);
            Vector3 headBottom = P(0f, def.HeadBottom.x, def.HeadBottom.y);
            Vector3 cluster = P(0f, def.Seat.x - 0.13f, def.Seat.y + 0.045f);   // where seat tube, top tube and seat stays meet
            Vector3 rear = P(0f, r, def.RearZ);
            Vector3 front = P(0f, r, def.FrontZ);

            // the frame
            paint.Tube(crank, cluster, tube);
            paint.Tube(cluster, Vector3.Lerp(headBottom, headTop, 0.85f), tube);
            paint.Tube(crank, Vector3.Lerp(headBottom, headTop, 0.12f), tube * 1.15f);
            paint.Tube(headBottom, headTop, tube * 1.2f);
            for (int side = -1; side <= 1; side += 2)
            {
                paint.Tube(crank + P(side * 0.035f, 0f, 0f), rear + P(side * 0.065f, 0f, 0f), 0.011f, 6);
                paint.Tube(cluster + P(side * 0.02f, 0f, 0f), rear + P(side * 0.065f, 0f, 0f), 0.009f, 6);
            }
            metal.Tube(cluster, P(0f, def.Seat.x - 0.035f, def.Seat.y + 0.012f), 0.013f, 6);                    // seat post
            black.Box(P(0f, def.Seat.x - 0.02f, def.Seat.y), P(city ? 0.17f : 0.13f, 0.04f, 0.27f));            // saddle
            black.Box(P(0f, def.Seat.x - 0.022f, def.Seat.y + 0.12f), P(0.06f, 0.034f, 0.08f));                 // its nose
            metal.Tube(crank + P(0.045f, 0f, 0f), crank + P(0.053f, 0f, 0f), 0.095f, 16);                       // chainring
            metal.Tube(rear + P(0.045f, 0f, 0f), rear + P(0.053f, 0f, 0f), 0.035f, 10);                         // sprocket
            black.Box((crank + rear) * 0.5f + P(0.049f, 0.062f, 0f), P(0.006f, 0.008f, (crank - rear).magnitude), Quaternion.LookRotation(crank - rear + P(0f, 0.06f, 0f)));
            black.Box((crank + rear) * 0.5f + P(0.049f, -0.062f, 0f), P(0.006f, 0.008f, (crank - rear).magnitude), Quaternion.LookRotation(crank - rear - P(0f, 0.06f, 0f)));
            if (city)
            {
                // a carrier over the back wheel, on two stays
                float deck = r * 2f + 0.07f;
                metal.Box(P(0f, deck, def.RearZ - 0.03f), P(0.13f, 0.012f, 0.36f));
                for (int side = -1; side <= 1; side += 2)
                {
                    metal.Tube(rear + P(side * 0.07f, 0f, 0f), P(side * 0.06f, deck, def.RearZ - 0.16f), 0.006f, 6);
                    metal.Tube(P(side * 0.06f, deck, def.RearZ + 0.14f), cluster + P(side * 0.02f, -0.06f, 0f), 0.006f, 6);
                }
            }
            Keep(meshes.Main, def.Kind + " frame", paint, black, metal);

            // the steering assembly, about the top of the head tube
            for (int side = -1; side <= 1; side += 2)
            {
                paint.Tube(headBottom + P(side * 0.045f, 0f, 0f) - headTop, front + P(side * 0.055f, 0f, 0f) - headTop, 0.012f, 6);
            }
            paint.Box(headBottom - headTop, P(0.12f, 0.03f, 0.04f));
            Vector3 stem = P(0f, def.Grip.y - 0.05f, def.HeadTop.y - 0.03f);
            metal.Tube(Vector3.zero, stem - headTop, 0.012f, 6);
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 grip = P(side * def.Grip.x, def.Grip.y, def.Grip.z) - headTop;
                Vector3 inner = grip + P(-side * 0.02f, 0f, 0.06f);
                metal.Tube(stem - headTop, inner, 0.011f, 6);
                black.Tube(inner, grip + P(side * 0.01f, 0f, -0.06f), 0.017f, 8);
            }
            Keep(meshes.Steer, def.Kind + " fork", paint, black, metal);

            // a wheel, about its axle
            black.Ring(Vector3.zero, r - (city ? 0.019f : 0.013f), city ? 0.019f : 0.013f);
            metal.Ring(Vector3.zero, r - (city ? 0.043f : 0.031f), 0.008f, 24, 4);
            metal.Tube(P(-0.045f, 0f, 0f), P(0.045f, 0f, 0f), 0.017f, 8);
            for (int i = 0; i < 12; i++)
            {
                float angle = i * Mathf.PI * 2f / 12f;
                float side = i % 2 == 0 ? 0.03f : -0.03f;
                metal.Tube(P(side, 0f, 0f), P(0f, Mathf.Cos(angle), Mathf.Sin(angle)) * (r - (city ? 0.045f : 0.033f)), 0.0035f, 4);
            }
            Keep(meshes.Wheel, def.Kind + " wheel", paint, black, metal);

            // the cranks, about the bottom bracket: the right one forwards, the left one back
            metal.Tube(P(-0.075f, 0f, 0f), P(0.075f, 0f, 0f), 0.013f, 8);
            metal.Tube(P(0.075f, 0f, 0f), P(0.085f, 0f, BikeCatalogue.CrankLength), 0.011f, 6);
            metal.Tube(P(-0.075f, 0f, 0f), P(-0.085f, 0f, -BikeCatalogue.CrankLength), 0.011f, 6);
            Keep(meshes.Crank, def.Kind + " crank", paint, black, metal);
            black.Box(Vector3.zero, P(0.09f, 0.022f, 0.075f));
            meshes.Pedal = black.Make("RealLifeOverhaul pedal");
        }

        private static void Motorbike(BikeDef def, Meshes meshes, MeshMaker paint, MeshMaker black, MeshMaker metal)
        {
            bool sport = def.Kind == BikeKind.SportMotorbike;
            float r = def.WheelRadius;
            Vector3 headTop = P(0f, def.HeadTop.x, def.HeadTop.y);
            Vector3 rear = P(0f, r, def.RearZ);
            Vector3 front = P(0f, r, def.FrontZ);
            Vector3 pivot = P(0f, 0.42f, -0.14f);   // where the swingarm hangs from the frame
            float seat = def.Seat.x;

            // tank, tail, and on the sport bike a fairing round the headstock
            paint.Ball(P(0f, seat + 0.03f, 0.13f), P(0.125f, 0.13f, 0.27f));
            paint.Box(P(0f, seat - 0.03f, def.Seat.y - 0.36f), P(0.20f, 0.09f, 0.30f), Quaternion.Euler(sport ? -14f : -6f, 0f, 0f));
            if (sport)
            {
                paint.Ball(P(0f, seat + 0.02f, 0.50f), P(0.19f, 0.20f, 0.25f));
                paint.Box(P(0f, 0.36f, 0.16f), P(0.30f, 0.26f, 0.62f));   // the belly pan
            }
            black.Box(P(0f, seat - 0.035f, def.Seat.y - 0.02f), P(0.25f, 0.07f, 0.46f));   // the seat: its top is the def's seat height
            for (int side = -1; side <= 1; side += 2)
            {
                black.Tube(headTop + P(side * 0.07f, -0.06f, 0f), pivot + P(side * 0.10f, 0f, 0f), 0.022f, 6);                       // frame
                black.Tube(pivot + P(side * 0.10f, 0.02f, 0f), P(side * 0.09f, seat - 0.07f, def.Seat.y - 0.40f), 0.018f, 6);        // subframe
                metal.Tube(pivot + P(side * 0.10f, 0f, 0f), rear + P(side * 0.10f, 0f, 0f), 0.025f, 6);                              // swingarm
                metal.Tube(P(side * 0.11f, seat - 0.10f, def.Seat.y - 0.18f), rear + P(side * 0.115f, 0.03f, 0.06f), 0.016f, 6);     // shock
                metal.Tube(P(side * 0.13f, def.Peg.y, def.Peg.z), P(side * (def.Peg.x + 0.045f), def.Peg.y, def.Peg.z), 0.012f, 6);  // footpeg
            }
            metal.Box(P(0f, 0.43f, 0.13f), P(0.27f, 0.30f, 0.40f));                              // engine
            metal.Tube(P(0.15f, 0.30f, 0.28f), P(0.16f, 0.30f, -0.22f), 0.035f, 8);              // exhaust: header along the engine
            metal.Tube(P(0.16f, 0.30f, -0.22f), P(0.18f, 0.50f, def.RearZ + 0.02f), 0.05f, 10);  // ...and the silencer up past the wheel
            black.Box(P(0f, seat - 0.20f, def.RearZ - 0.24f), P(0.16f, 0.10f, 0.012f));          // number plate
            Keep(meshes.Main, def.Kind + " body", paint, black, metal);

            // forks, headlamp and bars, about the top of the headstock
            for (int side = -1; side <= 1; side += 2)
            {
                metal.Tube(P(side * 0.09f, 0.02f, 0f), front + P(side * 0.09f, 0f, 0f) - headTop, 0.022f, 8);
                black.Tube(P(side * (def.Grip.x - 0.055f), def.Grip.y, def.Grip.z) - headTop, P(side * (def.Grip.x + 0.055f), def.Grip.y, def.Grip.z) - headTop, 0.018f, 8);
                metal.Tube(P(side * 0.07f, 0.03f, -0.02f), P(side * (def.Grip.x - 0.05f), def.Grip.y, def.Grip.z) - headTop, 0.012f, 6);
            }
            metal.Box(P(0f, 0.02f, 0f), P(0.24f, 0.03f, 0.07f));   // the yoke
            if (!sport)
            {
                metal.Tube(P(0f, -0.10f, 0.10f), P(0f, -0.10f, 0.19f), 0.085f, 12);   // a round headlamp
            }
            paint.Box(front + P(0f, r + 0.035f, 0f) - headTop, P(0.13f, 0.02f, 0.34f));   // front mudguard
            Keep(meshes.Steer, def.Kind + " fork", paint, black, metal);

            // a wheel, about its axle
            black.Ring(Vector3.zero, r - 0.06f, 0.06f, 24, 8);
            metal.Ring(Vector3.zero, r - 0.125f, 0.018f, 24, 6);
            metal.Tube(P(-0.07f, 0f, 0f), P(0.07f, 0f, 0f), 0.035f, 10);
            metal.Tube(P(-0.012f, 0f, 0f), P(0.012f, 0f, 0f), r - 0.20f, 16);   // brake disc
            for (int i = 0; i < 5; i++)
            {
                float angle = i * Mathf.PI * 2f / 5f;
                metal.Tube(Vector3.zero, P(0f, Mathf.Cos(angle), Mathf.Sin(angle)) * (r - 0.125f), 0.013f, 6);
            }
            Keep(meshes.Wheel, def.Kind + " wheel", paint, black, metal);
        }
    }
}
