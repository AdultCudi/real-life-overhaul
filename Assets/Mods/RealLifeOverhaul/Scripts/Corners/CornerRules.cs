using System;
using System.Collections.Generic;
using System.Globalization;

namespace RealLifeOverhaul.Corners
{
    /// <summary>What a sign anchor of a corner building carries (blender/export/corner_signs.json).</summary>
    public enum SignKind
    {
        Fascia = 0,    // the shop's board over its window: the business's name in its colour
        Frieze = 1,    // the name cut along a stone frieze: in capitals
        Neon = 2,      // a glowing name over the corner
        Blade = 3,     // a board standing out of the wall, one anchor a side
        Address = 4    // the street number
    }

    /// <summary>One sign anchor in the model's own frame (x along front A, y up, z along front B, m from the street corner): its middle, the way it faces out, its size.</summary>
    public struct CornerSign
    {
        public SignKind Kind;
        public float X, Y, Z;          // m: its middle
        public float OutX, OutZ;       // its outward way on the ground, a unit vector
        public float Width, Height;     // m
    }

    /// <summary>
    /// One of the seven corner buildings (PropAssets/models/corner_*), read off the model: its two fronts' lengths, its height, the
    /// uses it is offered for, where its entrance is, how much of the corner the game's building beneath keeps clear, and its signs.
    /// </summary>
    public struct CornerModel
    {
        public string Id;              // its prop id in reallife_props_2
        public string NameKey;         // its name in build mode's list of designs
        public int Uses;               // bits of the uses it is offered for (UseHomes and the others)
        public float Wide;             // m: front A's length, along the model's x
        public float Deep;             // m: front B's length, along the model's z
        public float Height;           // m: its body's top (the storeys the plans count), masts and tanks left out
        public float Top;              // m: its highest point
        public float DoorX, DoorZ;     // m: the entrance, where the game's door is put (on the pavement in front of the doors)
        public float FaceX, FaceZ;     // m: the doors' face, where the game's door sign is carried to
        public float OutX, OutZ;       // the entrance's outward way, a unit vector
        public float DoorWidth;        // m: the entrance's face, the widest the game's door sign may be there
        public float Clear;            // m of the corner square the game's building beneath keeps off (a cut corner's entrance); 0 for a door on a front
        public CornerSign[] Signs;
    }

    /// <summary>A site at a block's corner in the world: its ground (as the district stands a building) and which end of its strip the corner is.</summary>
    public struct CornerGround
    {
        public float X, Z;             // m: the ground's middle
        public int Facing;             // degrees: the way its street lies (0 north, +Z)
        public float Width, Depth;     // m: along its street, back from it
        public bool Low;               // the corner is at the strip's low end (its west or south)
        public float Edge;             // m on the strip's axis: the block's edge at that end, the cross street's building line
    }

    /// <summary>Where a corner building stands: its root at the street corner, its turn (a quarter of a turn, one of four), and the room the site gives it.</summary>
    public struct CornerPose
    {
        public float X, Z;             // m: the street corner, the model's origin
        public int Yaw;                // degrees: 0, 90, 180 or 270
        public float Along;            // m of the site along its street from the corner
        public float Deep;             // m of the site back from its street
        public bool AlongX;            // the site's street runs along the world's x
    }

    /// <summary>One solid box of a corner building in its own frame: from, to on the ground (m) and its height.</summary>
    public struct CornerBox
    {
        public float X1, Z1, X2, Z2;
        public float Height;
    }

    /// <summary>
    /// The corner buildings of build mode (the owner, 26 Oct 2026: seven chosen; "make them live business signs. Whoever buys
    /// that can keep their own signs"): the models' facts, the four ways a model is turned to a block's two streets, whether it
    /// fits a corner site, the ground the copy engine's building beneath it takes, its solid boxes, its door, and its signs
    /// (what each says and in what colour). Game-free, the same on every machine (EconomyCheck's CornerChecks).
    /// COST: arithmetic, asked when a site is picked, a building stands or a registration changes; never per frame.
    /// </summary>
    public static class CornerRules
    {
        // The uses, in Construction's BuildUse order (the checks hold the two together).
        public const int UseHomes = 0;
        public const int UseShops = 1;
        public const int UseOffices = 2;
        public const int UseWarehouses = 3;

        public const float CornerSlack = 1f;      // m: a site's end this near the block's edge is at the corner (a strip's spaces leave under a metre at its ends)
        public const float FitSlack = 0.05f;      // m a model may be longer than the room it is given (rounding of the exports)
        public const float CutInset = 0.5f;       // m inside a solid box each side for the walking meshes' carving: a carve reaches the walker's radius past its box
        public const float SignOut = 0.03f;       // m a sign's letters stand off its board, so they never flicker into it
        public const float AddressEvery = 0f;     // the address plate shows at all hours
        public const int MostDraws = 3;           // draw calls a corner building may cost: the atlas, the game's brick and glass

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const float Diag = 0.70710678f;

        /// <summary>The seven models, in build mode's order: the shops first, then the offices, the warehouse and the homes.</summary>
        public static readonly CornerModel[] Models =
        {
            new CornerModel
            {
                Id = "corner_bodega", NameKey = "reallife_corner_name_bodega", Uses = 1 << UseShops, Wide = 14.3f, Deep = 12.3f, Height = 20f, Top = 24.5f,
                DoorX = 0.92f, DoorZ = 0.92f, FaceX = 1.1f, FaceZ = 1.1f, OutX = -Diag, OutZ = -Diag, DoorWidth = 2.9f, Clear = 3f,
                Signs = new[]
                {
                    Sign(SignKind.Fascia, 5.7f, 3.675f, -0.19f, 0f, -1f, 6.5f, 0.65f),
                    Sign(SignKind.Fascia, -0.19f, 3.675f, 5.1f, -1f, 0f, 5.3f, 0.65f),
                    Sign(SignKind.Fascia, 1.066f, 3.675f, 1.066f, -Diag, -Diag, 2.89f, 0.65f),
                    Sign(SignKind.Address, 10.6f, 3.1f, -0.17f, 0f, -1f, 0.8f, 0.3f)
                }
            },
            new CornerModel
            {
                Id = "corner_diner", NameKey = "reallife_corner_name_diner", Uses = 1 << UseShops, Wide = 15.05f, Deep = 13.05f, Height = 10.2f, Top = 10.6f,
                DoorX = 0.77f, DoorZ = 0.77f, FaceX = 0.95f, FaceZ = 0.95f, OutX = -Diag, OutZ = -Diag, DoorWidth = 1.4f, Clear = 2.5f,
                Signs = new[]
                {
                    Sign(SignKind.Neon, 1.923f, 9.55f, 1.923f, -Diag, -Diag, 5.8f, 1.9f),
                    Sign(SignKind.Blade, 4.83f, 5.8f, -0.75f, 1f, 0f, 1f, 1.6f),
                    Sign(SignKind.Blade, 4.69f, 5.8f, -0.75f, -1f, 0f, 1f, 1.6f)
                }
            },
            new CornerModel
            {
                Id = "corner_bank", NameKey = "reallife_corner_name_bank", Uses = (1 << UseOffices) | (1 << UseShops), Wide = 18.2f, Deep = 16.2f, Height = 20.2f, Top = 20.24f,
                DoorX = 0.91f, DoorZ = 0.91f, FaceX = 1.09f, FaceZ = 1.09f, OutX = -Diag, OutZ = -Diag, DoorWidth = 1.86f, Clear = 3f,
                Signs = new[] { Sign(SignKind.Frieze, 11.25f, 11.7f, -0.11f, 0f, -1f, 13.34f, 0.6f) }
            },
            new CornerModel
            {
                Id = "corner_deco", NameKey = "reallife_corner_name_deco", Uses = 1 << UseOffices, Wide = 20.38f, Deep = 18.38f, Height = 52.2f, Top = 61.2f,
                DoorX = 1f, DoorZ = 1f, FaceX = 1.175f, FaceZ = 1.175f, OutX = -Diag, OutZ = -Diag, DoorWidth = 4.28f, Clear = 3.5f,
                Signs = new[] { Sign(SignKind.Address, 1.196f, 5.4f, 1.196f, -Diag, -Diag, 0.8f, 0.3f) }
            },
            new CornerModel
            {
                Id = "corner_glass", NameKey = "reallife_corner_name_glass", Uses = 1 << UseOffices, Wide = 22.05f, Deep = 20.05f, Height = 61.2f, Top = 70.65f,
                DoorX = 2.86f, DoorZ = 2.86f, FaceX = 3.04f, FaceZ = 3.04f, OutX = -Diag, OutZ = -Diag, DoorWidth = 1.5f, Clear = 5f,
                Signs = new CornerSign[0]
            },
            new CornerModel
            {
                Id = "corner_loft", NameKey = "reallife_corner_name_loft", Uses = (1 << UseWarehouses) | (1 << UseShops), Wide = 16.45f, Deep = 14.45f, Height = 26.4f, Top = 31.8f,
                DoorX = 1.25f, DoorZ = -0.4f, FaceX = 1.25f, FaceZ = -0.16f, OutX = 0f, OutZ = -1f, DoorWidth = 1.5f, Clear = 0f,
                Signs = new[] { Sign(SignKind.Fascia, 8f, 4.325f, -0.31f, 0f, -1f, 15.9f, 0.45f) }
            },
            new CornerModel
            {
                Id = "corner_prewar", NameKey = "reallife_corner_name_prewar", Uses = 1 << UseHomes, Wide = 20.75f, Deep = 18.3f, Height = 33.7f, Top = 33.7f,
                DoorX = 13.4f, DoorZ = -0.6f, FaceX = 13.4f, FaceZ = -0.34f, OutX = 0f, OutZ = -1f, DoorWidth = 3.2f, Clear = 1.5f,
                Signs = new[] { Sign(SignKind.Address, 13.425f, 4.1f, -0.31f, 0f, -1f, 0.8f, 0.3f) }
            }
        };

        private static CornerSign Sign(SignKind kind, float x, float y, float z, float outX, float outZ, float width, float height)
        {
            return new CornerSign { Kind = kind, X = x, Y = y, Z = z, OutX = outX, OutZ = outZ, Width = width, Height = height };
        }

        /// <summary>A model's facts by its prop id; false for an id that is none of the corner buildings'.</summary>
        public static bool Find(string id, out CornerModel model)
        {
            for (int i = 0; i < Models.Length && !string.IsNullOrEmpty(id); i++)
            {
                if (string.Equals(Models[i].Id, id, StringComparison.Ordinal))
                {
                    model = Models[i];
                    return true;
                }
            }
            model = default(CornerModel);
            return false;
        }

        /// <summary>True for a prop id that is one of the corner buildings'.</summary>
        public static bool IsCorner(string id) => Find(id, out _);

        /// <summary>True when a model is offered for a use (UseHomes and the others).</summary>
        public static bool Offers(CornerModel m, int use) => use >= 0 && use < 4 && (m.Uses & (1 << use)) != 0;

        // ---------------------------------------------------------------- the corner and the model's turn

        /// <summary>
        /// Which end of its strip a site touches a cross street at, if either: its low end (along the strip's axis) within
        /// <see cref="CornerSlack"/> of the block's edge there, else its high end. <paramref name="edge"/>: the block's edge at that end.
        /// </summary>
        public static bool End(float lo, float hi, bool streetLow, float edgeLow, bool streetHigh, float edgeHigh, out bool low, out float edge)
        {
            low = false;
            edge = 0f;
            if (streetLow && lo - edgeLow <= CornerSlack && lo - edgeLow >= -CornerSlack)
            {
                low = true;
                edge = edgeLow;
                return true;
            }
            if (streetHigh && edgeHigh - hi <= CornerSlack && edgeHigh - hi >= -CornerSlack)
            {
                edge = edgeHigh;
                return true;
            }
            return false;
        }

        /// <summary>The turn that puts the model's quarter (its +x, +z) into the world's quarter of the block from the corner (the signs of x and z inward).</summary>
        public static int YawFor(int inX, int inZ)
        {
            if (inX >= 0)
            {
                return inZ >= 0 ? 0 : 90;
            }
            return inZ >= 0 ? 270 : 180;
        }

        /// <summary>A point of the model's frame in the world, the model turned by <paramref name="yaw"/> about its root (as Quaternion.Euler(0, yaw, 0) turns it).</summary>
        public static void ToWorld(float rootX, float rootZ, int yaw, float lx, float lz, out float x, out float z)
        {
            Turn(yaw, lx, lz, out float dx, out float dz);
            x = rootX + dx;
            z = rootZ + dz;
        }

        /// <summary>A way of the model's frame in the world (no move): Unity's turn about the vertical.</summary>
        public static void Turn(int yaw, float lx, float lz, out float x, out float z)
        {
            switch (((yaw % 360) + 360) % 360)
            {
                case 90:
                    x = lz;
                    z = -lx;
                    return;
                case 180:
                    x = -lx;
                    z = -lz;
                    return;
                case 270:
                    x = -lz;
                    z = lx;
                    return;
                default:
                    x = lx;
                    z = lz;
                    return;
            }
        }

        /// <summary>A way in the world as degrees round the vertical (0 = +Z, 90 = +X), as the game turns things.</summary>
        public static float Degrees(float x, float z)
        {
            double a = Math.Atan2(x, z) * 180.0 / Math.PI;
            return (float)(a < 0 ? a + 360.0 : a);
        }

        /// <summary>
        /// Where a corner building stands on a corner site: its root at the street corner (the block's edge on the strip's axis, the
        /// street's front line across it), its turn so its two fronts face the two streets, and the room the site gives it.
        /// </summary>
        public static CornerPose Pose(CornerGround g)
        {
            int facing = ((g.Facing % 360) + 360) % 360;
            bool alongX = facing == 0 || facing == 180;
            float mid = alongX ? g.X : g.Z, across = alongX ? g.Z : g.X;
            int inward = facing == 0 || facing == 90 ? -1 : 1;                 // into the block, across its street
            float front = across - inward * g.Depth * 0.5f;                    // the street's front line
            int alongSign = g.Low ? 1 : -1;
            float far = g.Low ? mid + g.Width * 0.5f : mid - g.Width * 0.5f;   // the site's far end along its street
            var p = new CornerPose
            {
                AlongX = alongX, Along = (far - g.Edge) * alongSign, Deep = g.Depth,
                Yaw = alongX ? YawFor(alongSign, inward) : YawFor(inward, alongSign)
            };
            p.X = alongX ? g.Edge : front;
            p.Z = alongX ? front : g.Edge;
            return p;
        }

        /// <summary>How far a model reaches along its site's street and back from it, turned as the pose turns it.</summary>
        public static void Extents(CornerModel m, CornerPose p, out float along, out float deep)
        {
            bool wideOnX = p.Yaw == 0 || p.Yaw == 180;    // front A along the world's x
            bool wideAlong = wideOnX == p.AlongX;
            along = wideAlong ? m.Wide : m.Deep;
            deep = wideAlong ? m.Deep : m.Wide;
        }

        /// <summary>True when the model stands within its corner site: no longer along the street than the site from the corner, no deeper.</summary>
        public static bool Fits(CornerModel m, CornerPose p)
        {
            Extents(m, p, out float along, out float deep);
            return along <= p.Along + FitSlack && deep <= p.Deep + FitSlack;
        }

        /// <summary>
        /// The ground the copy engine's building beneath a corner building stands on (a lot facing the site's street): the site less
        /// the model's <see cref="CornerModel.Clear"/> at the corner, so its walls and its carving keep off the corner's entrance.
        /// Its middle, width along the street and depth (the site's whole depth: the game's buildings are deeper than most corner models).
        /// </summary>
        public static void CopyGround(CornerModel m, CornerGround g, CornerPose p, out float x, out float z, out float width, out float depth)
        {
            int alongSign = g.Low ? 1 : -1;
            width = Math.Max(0f, p.Along - m.Clear);
            depth = g.Depth;
            float along = g.Edge + alongSign * (m.Clear + width * 0.5f);
            x = p.AlongX ? along : g.X;
            z = p.AlongX ? g.Z : along;
        }

        /// <summary>The model's solid body in its own frame: one box, or two round the corner square its entrance keeps (<see cref="CornerModel.Clear"/>).</summary>
        public static void Boxes(CornerModel m, List<CornerBox> into)
        {
            into.Clear();
            if (m.Clear <= 0f)
            {
                into.Add(new CornerBox { X1 = 0f, Z1 = 0f, X2 = m.Wide, Z2 = m.Deep, Height = m.Height });
                return;
            }
            into.Add(new CornerBox { X1 = m.Clear, Z1 = 0f, X2 = m.Wide, Z2 = m.Deep, Height = m.Height });
            into.Add(new CornerBox { X1 = 0f, Z1 = m.Clear, X2 = m.Clear, Z2 = m.Deep, Height = m.Height });
        }

        /// <summary>A box as the walking meshes are carved by it: shrunk by <see cref="CutInset"/> a side, never under half a metre.</summary>
        public static void Carve(CornerBox b, out float sizeX, out float sizeZ)
        {
            sizeX = Math.Max(0.5f, b.X2 - b.X1 - 2f * CutInset);
            sizeZ = Math.Max(0.5f, b.Z2 - b.Z1 - 2f * CutInset);
        }

        /// <summary>True when a point of the model's frame is inside a box (its carving, not its shell).</summary>
        public static bool InCarve(CornerBox b, float x, float z)
        {
            return x > b.X1 + CutInset && x < b.X2 - CutInset && z > b.Z1 + CutInset && z < b.Z2 - CutInset;
        }

        /// <summary>The entrance in the world: where the game's door goes and the way it faces out (degrees).</summary>
        public static void Door(CornerModel m, CornerPose p, out float x, out float z, out float facing)
        {
            ToWorld(p.X, p.Z, p.Yaw, m.DoorX, m.DoorZ, out x, out z);
            Turn(p.Yaw, m.OutX, m.OutZ, out float ox, out float oz);
            facing = Degrees(ox, oz);
        }

        /// <summary>The doors' face in the world, where the game's door sign is carried to.</summary>
        public static void Face(CornerModel m, CornerPose p, out float x, out float z)
        {
            ToWorld(p.X, p.Z, p.Yaw, m.FaceX, m.FaceZ, out x, out z);
        }

        // ---------------------------------------------------------------- the signs

        public const int ShowsNothing = 0;   // a blank board
        public const int ShowsName = 1;      // the business's name
        public const int ShowsLease = 2;     // "For lease": a unit to let
        public const int ShowsNumber = 3;    // the street number

        /// <summary>
        /// What an anchor shows: the address its number always; a name anchor the business's name while one trades there, "For lease"
        /// while the unit is to let, else nothing (a unit kept empty by its owner).
        /// </summary>
        public static int Shows(SignKind kind, bool business, bool toLet)
        {
            if (kind == SignKind.Address)
            {
                return ShowsNumber;
            }
            return business ? ShowsName : toLet ? ShowsLease : ShowsNothing;
        }

        /// <summary>A sign's words for what it shows: the name, the lease words, the number; null for a blank board. A frieze is cut in capitals.</summary>
        public static string Words(SignKind kind, int shows, string name, string lease, int number)
        {
            string words;
            switch (shows)
            {
                case ShowsName:
                    words = string.IsNullOrEmpty(name) ? null : name.Trim();
                    break;
                case ShowsLease:
                    words = lease;
                    break;
                case ShowsNumber:
                    words = number > 0 ? number.ToString(Inv) : null;
                    break;
                default:
                    words = null;
                    break;
            }
            if (string.IsNullOrEmpty(words))
            {
                return null;
            }
            return kind == SignKind.Frieze ? words.ToUpperInvariant() : words;
        }

        /// <summary>A colour's brightness as the eye reads it, 0 to 1 (Rec. 709 weights on the 0..1 channels).</summary>
        public static float Lightness(float r, float g, float b) => 0.2126f * r + 0.7152f * g + 0.0722f * b;

        public const float ReadsApart = 0.22f;   // the lightness two colours keep apart to read one on the other (red on a dark board reads, black does not)

        /// <summary>How light the board an anchor's letters stand on is: a dark sign board, the bank's pale stone frieze, the wall a plate is on.</summary>
        public static float Board(SignKind kind) => kind == SignKind.Frieze ? 0.8f : 0.1f;

        /// <summary>
        /// The letters' colour on their board: the business's own (its logo's), else the game's colour for its sign's light, else
        /// cream on a dark board and dark bronze on a pale one; the first that reads against the board. The address plate is brass.
        /// </summary>
        public static void Colour(SignKind kind, float r, float g, float b, float sr, float sg, float sb, out float cr, out float cg, out float cb)
        {
            if (kind == SignKind.Address)
            {
                cr = 0.86f;
                cg = 0.72f;
                cb = 0.42f;
                return;
            }
            float board = Board(kind);
            if (Math.Abs(Lightness(r, g, b) - board) >= ReadsApart)
            {
                cr = r;
                cg = g;
                cb = b;
                return;
            }
            if (Math.Abs(Lightness(sr, sg, sb) - board) >= ReadsApart)
            {
                cr = sr;
                cg = sg;
                cb = sb;
                return;
            }
            bool pale = board > 0.5f;
            cr = pale ? 0.22f : 0.95f;
            cg = pale ? 0.16f : 0.9f;
            cb = pale ? 0.1f : 0.78f;
        }

        /// <summary>
        /// True when the game's door sign, carried to the entrance, covers an anchor (the same face within half a metre, the two
        /// rectangles overlapping): that anchor is left blank so the business's own door sign is not drawn over. Sizes in m, middles
        /// and ways in the model's frame.
        /// </summary>
        public static bool Covered(CornerSign s, float signX, float signY, float signZ, float signOutX, float signOutZ, float signWidth, float signHeight)
        {
            if (s.Kind == SignKind.Address || signWidth <= 0f || signHeight <= 0f)
            {
                return false;
            }
            if (s.OutX * signOutX + s.OutZ * signOutZ < 0.9f)
            {
                return false;   // another face
            }
            float dx = signX - s.X, dz = signZ - s.Z;
            float off = Math.Abs(dx * s.OutX + dz * s.OutZ);
            if (off > 0.5f)
            {
                return false;
            }
            float side = Math.Abs(dx * -s.OutZ + dz * s.OutX);   // along the face
            return side < (s.Width + signWidth) * 0.5f && Math.Abs(signY - s.Y) < (s.Height + signHeight) * 0.5f;
        }

        /// <summary>The scale the game's door sign takes to fit the entrance's face: 1, or less where the sign is wider.</summary>
        public static float SignScale(CornerModel m, float signWidth)
        {
            return signWidth <= m.DoorWidth || signWidth <= 0f ? 1f : m.DoorWidth / signWidth;
        }

        // ---------------------------------------------------------------- the log

        /// <summary>The one line a corner building stood writes: its triangles and draw calls, the game's materials, its signs, where its door is.</summary>
        public static string StoodLine(string id, string address, int triangles, int draws, int gameMaterials, int standIns, int signs, int signsBlank, float doorMissed)
        {
            return "Corners: " + id + " stands at " + address + ": " + triangles.ToString(Inv) + " triangles, " + draws.ToString(Inv) + " draw call(s) (" + gameMaterials.ToString(Inv)
                   + " of the game's own materials" + (standIns > 0 ? ", " + standIns.ToString(Inv) + " plain stand-in(s)" : string.Empty) + "), " + signs.ToString(Inv) + " sign(s) drawn"
                   + (signsBlank > 0 ? " (" + signsBlank.ToString(Inv) + " blank)" : string.Empty) + ", the door "
                   + doorMissed.ToString("F2", Inv) + " m from the entrance; the copy engine's building beneath it, its look hidden.";
        }

        /// <summary>The line for Player.log when a corner building's model is not stood and the copy engine's building shows its own look: why.</summary>
        public static string FallbackLine(string id, string address, string why)
        {
            return "Corners: " + (id ?? "(none)") + " at " + (address ?? "(none)") + " keeps the copy engine's look: " + (string.IsNullOrEmpty(why) ? "no reason given" : why) + ".";
        }

        /// <summary>The game's materials that draw a kind, best first (Build 3682): the brick and the glass the corner buildings wear.</summary>
        public static string[] GameNames(string kind)
        {
            switch (kind)
            {
                case "brick":
                    return new[] { "M_Bricks_01", "M_Bricks01_01", "M_BricksStretcherBond_01" };
                case "glass":
                    return new[] { "M_Glass", "M_GlassBlueHue", "M_Glass_01" };
                default:
                    return Array.Empty<string>();
            }
        }

        /// <summary>The kinds of the game's materials the corner buildings name (models.py GAME_KINDS among them).</summary>
        public static readonly string[] Kinds = { "brick", "glass" };
    }
}
