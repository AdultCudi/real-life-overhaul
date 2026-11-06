using System;

namespace RealLifeOverhaul.Construction
{
    /// <summary>What a plot of the city's own districts is to the player, as the layer colours it. Saved nowhere.</summary>
    public enum PlotStatus
    {
        ForSale = 0,   // on the game's list of buildings for sale
        Yours = 1,     // the player's deed
        Rented = 2,    // a unit the player rents
        Rival = 3,     // a rival owns it
        ToLet = 4,     // for rent with nobody in
        Taken = 5,     // somebody else's, let
        Kept = 6,      // a place nobody sells or lets (a public building, the mod's own places), or another player's
        Count = 7
    }

    /// <summary>The pointer over build mode: the game's own over its interface, a crosshair over the map.</summary>
    public enum BuildCursor
    {
        Normal = 0,
        Cross = 1
    }

    /// <summary>What can be done with a plot from its tool panel, in the panel's order.</summary>
    public enum PlotAction
    {
        Buy = 0,
        Rent = 1,
        Refurbish = 2,
        Redevelop = 3,
        Bulldoze = 4,
        Count = 5
    }

    /// <summary>What is known of one plot (one of the game's buildings outside Uptown) for its colour, its card and its tools.</summary>
    public struct PlotFacts
    {
        public bool Owned;      // the player's deed of it
        public bool Rented;     // a unit of it the player rents
        public bool ForSale;    // on the game's list of buildings for sale
        public bool Rival;      // a rival owns it
        public bool ToLet;      // for rent with nobody in
        public bool Kept;       // a public building or one of the mod's own places: never sold or let
        public bool Foreign;    // another player's in a Going Public session
        public bool UnderWay;   // works under way at it: a site of the city's programme
        public bool WorksMine;  // ... the player's own works
        public int Progress;    // ... their share done, 0 to 100
        public int Use;         // its use (BuildUse) by the game's building type, -1 for none of the four
        public int ValueStep;   // its price a square metre among the city's, 0 (lowest) to BuildModeRules.ValueSteps - 1
    }

    /// <summary>
    /// Build mode over the whole city (the owner, 26 Oct 2026: "make it grids in all district", chosen "Grid + buildings"): outside
    /// Uptown every plot is one of the game's buildings, drawn as its outline and fill on the map and coloured by what it is to the
    /// player and by the game's own building type colours; its tools are the game's purchase and rent, refurbishing, the rebuild
    /// and the existing demolition's refusal. Square by square buying, zoning and placing stay in Uptown. The pointer over the map
    /// is a crosshair, as Cities Skylines has it. Game-free (EconomyCheck's BuildModeCityChecks), the same on every machine.
    /// COST: nothing per frame; a switch a plot when the layer is coloured, a few comparisons when the pointer's target changes.
    /// </summary>
    public static class BuildModeCityRules
    {
        public const string WhyUptownOnly = "reallife_bld_hint_why_uptown_only";   // Zone, Place and Join outside Uptown
        public const string WhyYours = "reallife_bld_plot_why_yours";
        public const string WhyRentedAlready = "reallife_bld_plot_why_rented";
        public const string WhyKept = "reallife_bld_plot_why_kept";
        public const string WhyForeign = "reallife_bld_plot_why_foreign";
        public const string WhyNotToLet = "reallife_bld_plot_why_not_to_let";
        public const string WhyNotYours = "reallife_bld_plot_why_not_yours";
        public const string WhyNotOwned = "reallife_bld_plot_why_not_owned";

        public const float TileSize = 256f;   // m: the plot layer is cut into squares this wide, each its own mesh, so the map's camera culls what it does not see
        public const float EdgeWidth = 0.6f;  // m of a plot's edge drawn as its line
        public const float WidestPlot = 300f; // m: a building's box wider than this is a renderer reporting a batch, not a building
        public const float NarrowestPlot = 1.5f;

        /// <summary>The actions' names, by PlotAction.</summary>
        public static readonly string[] ActionKeys =
        {
            "reallife_bld_plot_buy", "reallife_bld_plot_rent", "reallife_bld_plot_refurbish", "reallife_bld_plot_redevelop", "reallife_bld_plot_bulldoze"
        };

        /// <summary>The statuses' words for the plot's card, by PlotStatus.</summary>
        public static readonly string[] StatusKeys =
        {
            "reallife_bld_plot_status_forsale", "reallife_bld_plot_status_yours", "reallife_bld_plot_status_rented", "reallife_bld_plot_status_rival",
            "reallife_bld_plot_status_tolet", "reallife_bld_plot_status_taken", "reallife_bld_plot_status_kept"
        };

        /// <summary>A plot's status: kept and another player's first (nothing can be done there), then the player's own, then for sale, a rival's, to let, else taken.</summary>
        public static PlotStatus Status(PlotFacts f)
        {
            if (f.Foreign || (f.Kept && !f.Owned && !f.Rented))
            {
                return PlotStatus.Kept;
            }
            if (f.Owned)
            {
                return PlotStatus.Yours;
            }
            if (f.Rented)
            {
                return PlotStatus.Rented;
            }
            if (f.ForSale)
            {
                return PlotStatus.ForSale;
            }
            if (f.Rival)
            {
                return PlotStatus.Rival;
            }
            return f.ToLet ? PlotStatus.ToLet : PlotStatus.Taken;
        }

        /// <summary>
        /// A plot's look in a view, its fill and its edge from build mode's palette (BuildModeRules): the land and buildings view by
        /// its status (works under way first), zoning as its building type in the game's colours, land value by its price a square
        /// metre, occupancy let or with room, under construction the works under way at it. The player's edge is theirs in every view.
        /// </summary>
        public static void Look(PlotFacts f, BuildView view, out int fill, out int line)
        {
            PlotStatus s = Status(f);
            bool mine = s == PlotStatus.Yours || s == PlotStatus.Rented;
            line = mine ? BuildModeRules.LineYours : s == PlotStatus.ForSale ? BuildModeRules.LineForSale : BuildModeRules.LinePlain;
            switch (view)
            {
                case BuildView.Zoning:
                    fill = f.Use >= 0 && f.Use < 4 ? BuildModeRules.Homes + f.Use : BuildModeRules.NoZone;
                    return;
                case BuildView.Value:
                    fill = BuildModeRules.Value + Math.Max(0, Math.Min(BuildModeRules.ValueSteps - 1, f.ValueStep));
                    return;
                case BuildView.Occupancy:
                    fill = s == PlotStatus.Kept ? BuildModeRules.Nothing : f.ToLet ? BuildModeRules.Vacant : BuildModeRules.Let;
                    return;
                case BuildView.UnderWay:
                    fill = !f.UnderWay ? BuildModeRules.Nothing : f.Progress >= 50 ? BuildModeRules.UnderWayDone : BuildModeRules.UnderWay;
                    return;
            }
            if (f.UnderWay)
            {
                fill = BuildModeRules.UnderWay;
                return;
            }
            switch (s)
            {
                case PlotStatus.Yours: fill = BuildModeRules.YourBuilding; return;
                case PlotStatus.Rented: fill = BuildModeRules.YourLand; return;
                case PlotStatus.ForSale: fill = BuildModeRules.ForSale; return;
                case PlotStatus.Rival: fill = BuildModeRules.Rival; return;
                case PlotStatus.ToLet: fill = BuildModeRules.Vacant; return;
                case PlotStatus.Kept: fill = BuildModeRules.Yard; return;
                default: fill = BuildModeRules.Taken; return;
            }
        }

        /// <summary>A view's legend over the city's districts into the arrays given (as BuildModeRules.Legend); returns how many.</summary>
        public static int Legend(BuildView view, int[] fills, int[] lines, string[] keys)
        {
            int n = 0;
            switch (view)
            {
                case BuildView.Status:
                    n = Key(fills, lines, keys, n, BuildModeRules.ForSale, BuildModeRules.LineForSale, "reallife_bld_mode_key_forsale");
                    n = Key(fills, lines, keys, n, BuildModeRules.YourBuilding, BuildModeRules.LineYours, "reallife_bld_mode_key_yourbuilding");
                    n = Key(fills, lines, keys, n, BuildModeRules.YourLand, BuildModeRules.LineYours, "reallife_bld_plot_key_rented");
                    n = Key(fills, lines, keys, n, BuildModeRules.Rival, BuildModeRules.LinePlain, "reallife_bld_plot_key_rival");
                    n = Key(fills, lines, keys, n, BuildModeRules.Vacant, BuildModeRules.LinePlain, "reallife_bld_plot_key_tolet");
                    n = Key(fills, lines, keys, n, BuildModeRules.Taken, BuildModeRules.LinePlain, "reallife_bld_mode_key_taken");
                    n = Key(fills, lines, keys, n, BuildModeRules.UnderWay, BuildModeRules.LinePlain, "reallife_bld_mode_key_underway");
                    n = Key(fills, lines, keys, n, BuildModeRules.Yard, BuildModeRules.LinePlain, "reallife_bld_plot_key_kept");
                    return n;
                case BuildView.Zoning:
                    n = Key(fills, lines, keys, n, BuildModeRules.Homes, BuildModeRules.LinePlain, "reallife_bld_use_homes");
                    n = Key(fills, lines, keys, n, BuildModeRules.Shops, BuildModeRules.LinePlain, "reallife_bld_use_shops");
                    n = Key(fills, lines, keys, n, BuildModeRules.Offices, BuildModeRules.LinePlain, "reallife_bld_use_offices");
                    n = Key(fills, lines, keys, n, BuildModeRules.Warehouses, BuildModeRules.LinePlain, "reallife_bld_use_warehouses");
                    n = Key(fills, lines, keys, n, BuildModeRules.NoZone, BuildModeRules.LinePlain, "reallife_bld_plot_key_other");
                    n = Key(fills, lines, keys, n, -1, BuildModeRules.LineForSale, "reallife_bld_mode_key_forsale_edge");
                    return n;
                case BuildView.Value:
                    n = Key(fills, lines, keys, n, BuildModeRules.Value, BuildModeRules.LinePlain, "reallife_bld_plot_key_value_low");
                    n = Key(fills, lines, keys, n, BuildModeRules.Value + BuildModeRules.ValueSteps - 1, BuildModeRules.LinePlain, "reallife_bld_plot_key_value_high");
                    n = Key(fills, lines, keys, n, -1, BuildModeRules.LineYours, "reallife_bld_mode_key_yours_edge");
                    return n;
                case BuildView.Occupancy:
                    n = Key(fills, lines, keys, n, BuildModeRules.Let, BuildModeRules.LinePlain, "reallife_bld_mode_key_let");
                    n = Key(fills, lines, keys, n, BuildModeRules.Vacant, BuildModeRules.LinePlain, "reallife_bld_mode_key_vacant");
                    n = Key(fills, lines, keys, n, -1, BuildModeRules.LineYours, "reallife_bld_mode_key_yours_edge");
                    return n;
                default:
                    n = Key(fills, lines, keys, n, BuildModeRules.UnderWayDone, BuildModeRules.LinePlain, "reallife_bld_plot_key_works_late");
                    n = Key(fills, lines, keys, n, BuildModeRules.UnderWay, BuildModeRules.LinePlain, "reallife_bld_plot_key_works_early");
                    n = Key(fills, lines, keys, n, -1, BuildModeRules.LineYours, "reallife_bld_mode_key_yours_edge");
                    return n;
            }
        }

        private static int Key(int[] fills, int[] lines, string[] keys, int n, int fill, int line, string key)
        {
            if (n >= fills.Length || n >= lines.Length || n >= keys.Length)
            {
                return n;
            }
            fills[n] = fill;
            lines[n] = line;
            keys[n] = key;
            return n + 1;
        }

        /// <summary>
        /// Why a tool cannot be used where the map looks now: in Uptown as ever (BuildModeHintRules.ToolWhy); over the rest of the
        /// city, which is built up, Zone, Place and Join are Uptown's alone and Bulldoze has nothing it may knock down, while Buy land
        /// (click a building to buy or rent it) and Refurbish (click a building of yours) stay open; a Going Public session as ever.
        /// </summary>
        public static string ToolWhyAt(BuildTool tool, BuildHoldings h, bool viewOnly, bool inUptown)
        {
            if (inUptown)
            {
                return BuildModeHintRules.ToolWhy(tool, h, viewOnly);
            }
            if (tool == BuildTool.Land)
            {
                return null;
            }
            if (viewOnly)
            {
                return BuildModeHintRules.WhySession;
            }
            switch (tool)
            {
                case BuildTool.Refurbish:
                    return null;
                case BuildTool.Bulldoze:
                    return "reallife_bld_answer_not_district";
                default:
                    return WhyUptownOnly;
            }
        }

        /// <summary>The pointer: the game's own while build mode is shut, over its interface, a question box, the tour or the design window; else a crosshair over the map.</summary>
        public static BuildCursor Cursor(bool open, bool overInterface, bool question, bool held)
        {
            return open && !overInterface && !question && !held ? BuildCursor.Cross : BuildCursor.Normal;
        }

        /// <summary>Why the player cannot buy a plot now (a text key), or null: the game's own offer on its BizMan page then decides the price.</summary>
        public static string BuyWhy(PlotFacts f, bool session)
        {
            if (session)
            {
                return BuildModeHintRules.WhySession;
            }
            if (f.Owned)
            {
                return WhyYours;
            }
            if (f.Foreign)
            {
                return WhyForeign;
            }
            return f.Kept ? WhyKept : null;
        }

        /// <summary>Why the player cannot rent a plot now, or null: the game's rent on its BizMan page then takes the deposit.</summary>
        public static string RentWhy(PlotFacts f, bool session)
        {
            if (session)
            {
                return BuildModeHintRules.WhySession;
            }
            if (f.Rented)
            {
                return WhyRentedAlready;
            }
            if (f.Foreign)
            {
                return WhyForeign;
            }
            if (f.Kept && !f.Owned)
            {
                return WhyKept;
            }
            return f.ToLet || f.Owned ? null : WhyNotToLet;
        }

        /// <summary>Why a plot cannot be refurbished before the construction game is asked: a session, or not the player's (owned or rented); null to ask it.</summary>
        public static string RefurbishWhy(PlotFacts f, bool session)
        {
            if (session)
            {
                return BuildModeHintRules.WhySession;
            }
            return f.Owned || f.Rented ? null : WhyNotYours;
        }

        /// <summary>Why a plot cannot be rebuilt before the construction game is asked: a session, or not the player's own deed; null to ask it.</summary>
        public static string RedevelopWhy(PlotFacts f, bool session)
        {
            if (session)
            {
                return BuildModeHintRules.WhySession;
            }
            return f.Owned ? null : WhyNotOwned;
        }

        // ------------------------------------------------------------------ the plot's ground

        /// <summary>
        /// A building's outline from its upright box (the world's x and z) and its turn (degrees): the box of a rectangle a by b
        /// turned by the angle is a |cos| + b |sin| wide and a |sin| + b |cos| deep, solved for a and b; near 45 degrees, or a box
        /// the turn cannot come from, the upright box itself. Corners into <paramref name="x"/> and <paramref name="z"/> (4 each)
        /// in order round it. False for a box too small or too wide to be one building.
        /// </summary>
        public static bool Outline(float minX, float minZ, float maxX, float maxZ, float yawDegrees, float[] x, float[] z)
        {
            float w = maxX - minX, d = maxZ - minZ;
            if (w < NarrowestPlot || d < NarrowestPlot || w > WidestPlot || d > WidestPlot)
            {
                return false;
            }
            double turn = yawDegrees * Math.PI / 180.0;
            double c = Math.Abs(Math.Cos(turn)), s = Math.Abs(Math.Sin(turn));
            double cx = (minX + maxX) * 0.5, cz = (minZ + maxZ) * 0.5;
            double den = c * c - s * s;
            double a = 0.0, b = 0.0;
            bool turned = Math.Abs(den) > 0.2 && s > 0.02;
            if (turned)
            {
                a = (w * c - d * s) / den;
                b = (d * c - w * s) / den;
                turned = a >= NarrowestPlot && b >= NarrowestPlot;
            }
            if (!turned)
            {
                x[0] = minX; z[0] = minZ;
                x[1] = maxX; z[1] = minZ;
                x[2] = maxX; z[2] = maxZ;
                x[3] = minX; z[3] = maxZ;
                return true;
            }
            double cos = Math.Cos(turn), sin = Math.Sin(turn);
            for (int k = 0; k < 4; k++)
            {
                double u = (k == 1 || k == 2 ? 0.5 : -0.5) * a, v = (k >= 2 ? 0.5 : -0.5) * b;
                x[k] = (float)(cx + u * cos + v * sin);
                z[k] = (float)(cz - u * sin + v * cos);
            }
            return true;
        }

        /// <summary>True when a point lies inside the outline given by its four corners in order (either way round).</summary>
        public static bool Inside(float[] x, float[] z, float px, float pz)
        {
            int sign = 0;
            for (int k = 0; k < 4; k++)
            {
                int n = (k + 1) % 4;
                float cross = (x[n] - x[k]) * (pz - z[k]) - (z[n] - z[k]) * (px - x[k]);
                int here = cross > 0f ? 1 : cross < 0f ? -1 : 0;
                if (here != 0)
                {
                    if (sign != 0 && here != sign)
                    {
                        return false;
                    }
                    sign = here;
                }
            }
            return true;
        }

        /// <summary>The layer's square a point stands in, by <see cref="TileSize"/>: a whole number from its column and row (the same everywhere).</summary>
        public static int Tile(float x, float z)
        {
            int col = (int)Math.Floor(x / TileSize), row = (int)Math.Floor(z / TileSize);
            return (row + 4096) * 8192 + (col + 4096);
        }

        /// <summary>A price a square metre's step among the city's least and most (BuildModeRules.ValueStepOf on whole units).</summary>
        public static int ValueStep(float pricePerSqm, float least, float most)
        {
            return BuildModeRules.ValueStepOf((long)Math.Round(pricePerSqm), (long)Math.Round(least), (long)Math.Round(most));
        }

        /// <summary>True when a point on the ground lies in Uptown's squares' rectangle (with its slack): the map over Uptown.</summary>
        public static bool InRect(float x, float z, float minX, float minZ, float maxX, float maxZ) => minX < maxX && x >= minX && x <= maxX && z >= minZ && z <= maxZ;
    }
}
