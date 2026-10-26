using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Schools
{
    /// <summary>One solid box of a campus look, in the model's own frame: its middle on the ground (x across, z back from the front wall) and its size.</summary>
    public struct LookBox
    {
        public float X, Z;               // m: the box's middle, x across (right as the model is faced from the street), z back from the front wall
        public float HalfX, HalfZ;       // m
        public float Height;             // m
    }

    /// <summary>The facts of one of Birchwood School's models (PropAssets/models/birchwood_*), read off the model: where its front wall and door are, what is solid.</summary>
    public struct LookModel
    {
        public string Id;
        public float HalfWidth;          // m: the body's half width (its plinth)
        public float Depth;              // m: the body's depth back from its front wall
        public float Front;              // m: what stands in front of the front wall (steps, porch, canopy, forecourt) at most
        public float Height;             // m: the highest point (weathervane, rooftop fence)
        public float BodyHeight;         // m: the walls' height, what the walking meshes and the map's box are cut to
        public float DoorOut;            // m: the door's front face in front of the front wall; 0 for a model whose door is no entrance (the blocks')
        public float BayHalfWidth;       // m: a bay that stands out of the front wall (the main school's entrance bay), 0 for none
        public float BayDepth;           // m: how far it stands out
    }

    /// <summary>
    /// Birchwood School's own look (docs/features/schools.md, "The look"): the three models designed in Blender and shipped in the
    /// second props bundle (reallife_props_2), stood in place of the copy engine's designs. Game-free: the models' facts, which of
    /// the game's materials draws the brick, the roof, the glass and the rooftop fence (by name, found once behind the loading
    /// screen), where a model stands on a door, or on a plot, the boxes it makes solid, and the log lines. EconomyCheck's
    /// CampusLookChecks. COST: arithmetic once a load.
    /// </summary>
    public static class CampusLookRules
    {
        public const string MainModel = "birchwood_main";
        public const string HallModel = "birchwood_hall";
        public const string JuniorModel = "birchwood_junior";

        public const float FenceClear = 0.5f;     // m between the fence and the nearest part of a model that stands out of its front wall
        public const float CutInset = 0.5f;       // m inside a box each side: a carve reaches the walker's radius past its box
        public const float MostAside = 9f;        // m the main school's look may stand from the middle of its lot to meet its door, at most
        public const int MostDraws = 5;           // draw calls a model may cost: the bundle's atlas and the game's materials it names (brick, roof, glass, a fence)
        public const float MostForward = 6f;      // m it may stand nearer or farther than the copy engine's footprint

        /// <summary>The models of the campus, in the order the director stands them: the main school, the hall, the junior block.</summary>
        public static readonly LookModel[] Models =
        {
            new LookModel { Id = MainModel, HalfWidth = 20.4f, Depth = 30.4f, Front = 4.1f, Height = 19.805f, BodyHeight = 15.2f, DoorOut = 1.55f, BayHalfWidth = 6f, BayDepth = 1.5f },
            new LookModel { Id = HallModel, HalfWidth = 15.4f, Depth = 24.4f, Front = 1.6f, Height = 11.925f, BodyHeight = 11.0f },
            new LookModel { Id = JuniorModel, HalfWidth = 15.4f, Depth = 24.4f, Front = 3.2f, Height = 10.85f, BodyHeight = 8.5f }
        };

        /// <summary>The facts of a model by its prop id; false for an id that is none of the campus's.</summary>
        public static bool Find(string id, out LookModel model)
        {
            for (int i = 0; i < Models.Length; i++)
            {
                if (string.Equals(Models[i].Id, id, StringComparison.Ordinal))
                {
                    model = Models[i];
                    return true;
                }
            }
            model = default;
            return false;
        }

        /// <summary>The prop's size as the bundle lists it (x across, y high, z front to back), from the facts: what the bundle's contents are held to.</summary>
        public static void Size(LookModel m, out float x, out float y, out float z)
        {
            x = m.HalfWidth * 2f;
            y = m.Height;
            z = m.Front + m.Depth;
        }

        // ---------------------------------------------------------------- the game's materials

        /// <summary>The kinds of the game's own material a model's slots may ask for (models.py GAME_KINDS), in the order of their submeshes' first use.</summary>
        public static readonly string[] Kinds = { "brick", "roof", "glass", "fence" };

        /// <summary>
        /// The names of the game's materials that draw a kind, best first (Build 3682, read from the game's level files: PropAssets/README.md,
        /// "Buildings"). The first the game has in memory is used; none, and a plain colour from the model stands in.
        /// </summary>
        public static string[] GameNames(string kind)
        {
            switch (kind)
            {
                case "brick":
                    return new[] { "M_Bricks_01", "M_Bricks01_01", "M_BricksStretcherBond_01" };   // red London-bond brick on the game's plain lit shader
                case "roof":
                    return new[] { "M_Roof_02", "M_Roof_01", "M_Roof" };                          // the game's flat roofing, laid by world position
                case "glass":
                    return new[] { "M_GlassBlueHue", "M_Glass_01", "M_Glass" };                   // opaque, reflective, pale blue: the game's window glass
                case "fence":
                    return new[] { "M_FenceWire_Occlussion", "M_FenceWire" };                     // chain-link, cut out by its alpha
                default:
                    return Array.Empty<string>();
            }
        }

        /// <summary>The kind a stand-in takes when the game has no material of the kind and none of the fence's: a fence is drawn as the glass is.</summary>
        public static string StandsInFor(string kind)
        {
            return kind == "fence" ? "glass" : kind;
        }

        // ---------------------------------------------------------------- where a model stands

        /// <summary>
        /// Where the main school's look stands so that its door is where the copy engine's building has its own (so the click, the
        /// walk to it and the way in all stay as they were): its root (the middle of its front wall's foot) and its turn in degrees.
        /// The model's front is its -Z; the door faces out <paramref name="doorFacing"/> degrees (0 is +Z), 1.55 m in front of the wall.
        /// </summary>
        public static void RootOnDoor(LookModel m, float doorX, float doorZ, float doorFacing, out float x, out float z, out float yaw)
        {
            yaw = Turn(doorFacing + 180f);
            double a = yaw * Math.PI / 180.0;
            float s = (float)Math.Sin(a), c = (float)Math.Cos(a);
            // The door in the model's frame is (0, -DoorOut): the root is that far behind it along the way the door faces.
            x = doorX + m.DoorOut * s;
            z = doorZ + m.DoorOut * c;
        }

        /// <summary>
        /// Where a block's look stands on its plot: its front wall <see cref="WallFromEdge"/> back from the plot's front edge (the block's
        /// edge, where the fence is), in the middle of the plot's width, its front where the plot's is (the plot's front at -Z turned by
        /// its yaw, as CivicWorks.StartPlot takes one). Its root (the middle of its front wall's foot) and its turn.
        /// </summary>
        public static void RootOnPlot(LookModel m, CampusBlockPlot plot, out float x, out float z, out float yaw)
        {
            yaw = Turn(plot.Yaw);
            double a = yaw * Math.PI / 180.0;
            float s = (float)Math.Sin(a), c = (float)Math.Cos(a);
            // The plot's front edge is half its depth from its middle along its front (-s, -c); the wall stands back of that.
            float fromMiddle = plot.Depth * 0.5f - WallFromEdge(m);
            x = plot.X - fromMiddle * s;
            z = plot.Z - fromMiddle * c;
        }

        /// <summary>m from a block's edge (and the plot's front edge) to a model's front wall: what stands out of it, and the fence's clearance.</summary>
        public static float WallFromEdge(LookModel m)
        {
            return m.Front + FenceClear;
        }

        /// <summary>
        /// The ground a model's body takes in its own frame, for the walking meshes and (for a block) the solid box: the body behind its
        /// front wall, and the main school's entrance bay in front of it. What stands out in front (steps, porch, canopy, forecourt)
        /// is not in the list: people walk onto it to the door.
        /// </summary>
        public static void Boxes(LookModel m, List<LookBox> into)
        {
            into.Clear();
            into.Add(new LookBox { X = 0f, Z = m.Depth * 0.5f, HalfX = m.HalfWidth, HalfZ = m.Depth * 0.5f, Height = m.BodyHeight });
            if (m.BayDepth > 0f && m.BayHalfWidth > 0f)
            {
                into.Add(new LookBox { X = 0f, Z = -m.BayDepth * 0.5f, HalfX = m.BayHalfWidth, HalfZ = m.BayDepth * 0.5f, Height = m.BodyHeight });
            }
        }

        /// <summary>A box as the walking meshes are cut by it: shrunk by <see cref="CutInset"/> a side, never under half a metre.</summary>
        public static void CarveSize(LookBox b, out float sizeX, out float sizeZ)
        {
            sizeX = Math.Max(0.5f, b.HalfX * 2f - 2f * CutInset);
            sizeZ = Math.Max(0.5f, b.HalfZ * 2f - 2f * CutInset);
        }

        /// <summary>
        /// Whether the main school's look may stand where its door puts it: how far its middle is from the middle of the lot's width
        /// (<paramref name="aside"/>) and from where the copy engine's footprint stood (<paramref name="forward"/>, along the front's
        /// way), against what it may be. Else why not, in a few words.
        /// </summary>
        public static string MainWhy(float aside, float forward)
        {
            if (Math.Abs(aside) > MostAside)
            {
                return "its door is " + Math.Abs(aside).ToString("F1", System.Globalization.CultureInfo.InvariantCulture)
                       + " m from the middle of its lot, more than the " + MostAside + " m a look may stand aside";
            }
            if (Math.Abs(forward) > MostForward)
            {
                return "its door would put the look " + Math.Abs(forward).ToString("F1", System.Globalization.CultureInfo.InvariantCulture)
                       + " m off where the copy engine's footprint stands, more than " + MostForward + " m";
            }
            return null;
        }

        /// <summary>An angle in degrees brought into [0, 360).</summary>
        public static float Turn(float degrees)
        {
            float t = degrees % 360f;
            return t < 0f ? t + 360f : t;
        }

        // ---------------------------------------------------------------- the log

        /// <summary>
        /// The one line a load writes about a model stood: its triangles and draw calls (a submesh each), where the game's materials
        /// stood in, and what the copy engine's design it replaces costs where that is known (the main school's); never a per-frame line.
        /// </summary>
        public static string StoodLine(string name, int triangles, int draws, int gameMaterials, int standIns, int replacedTriangles, int replacedDraws)
        {
            string versus = replacedDraws > 0
                ? "; the design it replaces draws " + replacedTriangles + " triangles in " + replacedDraws + " draw call(s)"
                : string.Empty;
            return name + ": " + triangles + " triangles, " + draws + " draw call(s) (" + gameMaterials + " of the game's own materials"
                   + (standIns > 0 ? ", " + standIns + " plain stand-in(s)" : string.Empty) + ")" + versus;
        }

        /// <summary>The line for Player.log when a model is not stood and the copy engine's design is used instead (or kept): why.</summary>
        public static string FallbackLine(string name, string why)
        {
            return name + " keeps the copy engine's design: " + (string.IsNullOrEmpty(why) ? "no reason given" : why);
        }
    }
}
