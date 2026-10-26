using System;

namespace RealLifeOverhaul.Construction
{
    /// <summary>Build mode's tools, one at a time (the window's rows). Saved nowhere.</summary>
    public enum BuildTool
    {
        Land = 0,        // buy cells
        Zone = 1,        // paint a use on the player's own cells
        Place = 2,       // a building on a footprint of the player's cells
        Bulldoze = 3,    // a building of the player's knocked down (the demolition project)
        Join = 4,        // join or split the player's sites
        Refurbish = 5,   // a building of the player's refitted (the app's refit page)
        Count = 6
    }

    /// <summary>Where build mode stands.</summary>
    public enum BuildStep
    {
        Closed = 0,
        Idle = 1,         // a tool, nothing picked
        Dragging = 2,     // a press held on a cell: cells being picked
        Picked = 3,       // cells picked: the window says what they are and what can be done
        Ghost = 4,        // a design picked for the footprint: its ghost stands
        Confirming = 5    // the game's question box is up
    }

    /// <summary>What happens to build mode.</summary>
    public enum BuildInput
    {
        Open = 0,          // the Build mode button
        OpenInSession = 1, // ... in a Going Public session: view only
        Tool = 2,          // a tool's row
        Press = 3,         // a press on the map (on a cell or not)
        Release = 4,       // the press let go
        Clear = 5,         // the window's Clear row: what is picked let go
        Action = 6,        // an action's row (buy, a use, join, split, demolish, renovate), done on what is picked
        Design = 7,        // a design's row (the Place tool)
        Confirm = 8,       // the Place tool's Build here row: the question box opens
        Yes = 9,           // the question box said yes: the project is made
        No = 10,           // ... said no
        MapClosed = 11     // Esc, Cancel, a load, the option off: the map shut
    }

    /// <summary>Build mode's state: its step, its tool, and whether it may change anything.</summary>
    public struct BuildModeState
    {
        public BuildStep Step;
        public BuildTool Tool;
        public bool ViewOnly;   // a Going Public session: the layer, the sites and the overlays only

        public bool Open => Step != BuildStep.Closed;
    }

    /// <summary>What the cells under a placed design make of it: the red warnings and what stops it.</summary>
    public struct PlaceAsk
    {
        public bool Mine;          // every cell of the footprint is the player's
        public bool Session;       // a Going Public session
        public bool HasDesign;     // a design is picked
        public int Columns;        // the footprint's columns
        public int SiteColumns;    // the player's site's columns under it (at most the site's own)
        public int RowsNeeded;     // rows the design's depth needs
        public int RowsHad;        // the shallowest column's rows the player has under it
        public int Storeys;        // the design's
        public int Limit;          // the building code's storeys in force
        public int ParkingNeeded;  // spaces the plans would ask for
        public bool ParkingUnder;  // parking under the building chosen
        public bool Zoned;         // the use is among the site's zones
    }

    /// <summary>
    /// Build mode's rules (plans/build-mode.md section 5): its state machine, the colour a cell is drawn in, and a placed
    /// design's warnings. Game-free (EconomyCheck's BuildModeChecks), the same on every machine.
    /// COST: nothing per frame; a switch per input, a few comparisons per cell when the layer is coloured again.
    /// </summary>
    public static class BuildModeRules
    {
        // ---- the palette: one texel each in the layer's colour map

        public const int ForSale = 0;
        public const int Taken = 1;
        public const int Yours = 2;
        public const int Homes = 3;        // Yours painted, or a building's use, by BuildUse + Homes
        public const int Shops = 4;
        public const int Offices = 5;
        public const int Warehouses = 6;
        public const int Cleared = 7;
        public const int UnderWay = 8;     // a site under construction
        public const int Developer = 9;    // the city's developers' or a redevelopment's
        public const int Yard = 10;        // behind a building, its own
        public const int Picked = 11;
        public const int PickedBad = 12;
        public const int ZoneWaits = 13;   // a use asked of the council
        public const int Footprint = 14;   // the ghost's cells
        public const int Nothing = 15;     // not drawn
        public const int PaletteSize = 16;

        /// <summary>The palette's colours (red, green, blue, alpha, 0 to 255): white for sale, grey taken, the zones as Cities Skylines colours them.</summary>
        public static readonly byte[][] Palette =
        {
            new byte[] { 242, 242, 242, 90 },   // for sale
            new byte[] { 90, 92, 98, 60 },      // taken
            new byte[] { 178, 128, 255, 130 },  // yours
            new byte[] { 76, 217, 89, 140 },    // homes
            new byte[] { 64, 128, 255, 140 },   // shops
            new byte[] { 51, 217, 230, 140 },   // offices
            new byte[] { 255, 204, 38, 140 },   // warehouses
            new byte[] { 217, 140, 77, 130 },   // cleared
            new byte[] { 255, 128, 26, 150 },   // under way
            new byte[] { 230, 77, 77, 115 },    // the developers'
            new byte[] { 64, 64, 64, 40 },      // yard
            new byte[] { 255, 255, 255, 190 },  // picked
            new byte[] { 255, 38, 38, 180 },    // picked, no site
            new byte[] { 204, 204, 128, 115 },  // a use waits on the council
            new byte[] { 102, 255, 102, 180 },  // the ghost's cells
            new byte[] { 0, 0, 0, 0 }           // nothing
        };

        // The warnings of a placed design (bits).
        public const int WarnNotYours = 1;
        public const int WarnCells = 2;       // the footprint needs more cells than the player has under it
        public const int WarnTall = 4;        // taller than the building code allows
        public const int WarnParking = 8;     // the plans ask for parking and none is chosen
        public const int WarnZone = 16;       // the use is not zoned there
        public const int WarnSession = 32;
        public const int WarnNoDesign = 64;
        public const int WarnCount = 7;

        // Those that stop Build here: the rest are said in red and left to the plans and the council, as in the app.
        public const int Stops = WarnNotYours | WarnCells | WarnZone | WarnSession | WarnNoDesign;

        /// <summary>The warning keys, by bit.</summary>
        public static readonly string[] WarnKeys =
        {
            "reallife_bld_mode_warn_not_yours", "reallife_bld_mode_warn_cells", "reallife_bld_mode_warn_tall", "reallife_bld_mode_warn_parking",
            "reallife_bld_mode_warn_zone", "reallife_bld_mode_warn_session", "reallife_bld_mode_warn_no_design"
        };

        /// <summary>The state after an input. <paramref name="tool"/>: the tool a Tool row picks; <paramref name="onCell"/>: a press on a cell of the grid.</summary>
        public static BuildModeState Next(BuildModeState s, BuildInput input, BuildTool tool, bool onCell)
        {
            if (input == BuildInput.MapClosed)
            {
                return new BuildModeState { Step = BuildStep.Closed, Tool = BuildTool.Land };
            }
            if (s.Step == BuildStep.Closed)
            {
                if (input == BuildInput.Open || input == BuildInput.OpenInSession)
                {
                    return new BuildModeState { Step = BuildStep.Idle, Tool = BuildTool.Land, ViewOnly = input == BuildInput.OpenInSession };
                }
                return s;
            }
            switch (input)
            {
                case BuildInput.Tool:
                    s.Tool = tool >= BuildTool.Land && tool < BuildTool.Count ? tool : BuildTool.Land;
                    s.Step = BuildStep.Idle;
                    return s;
                case BuildInput.Press:
                    if (s.Step == BuildStep.Confirming)
                    {
                        return s;   // the question box holds the map
                    }
                    if (onCell)
                    {
                        s.Step = BuildStep.Dragging;
                    }
                    return s;
                case BuildInput.Release:
                    if (s.Step == BuildStep.Dragging)
                    {
                        s.Step = BuildStep.Picked;
                    }
                    return s;
                case BuildInput.Clear:
                    if (s.Step == BuildStep.Ghost)
                    {
                        s.Step = BuildStep.Picked;
                    }
                    else if (s.Step != BuildStep.Confirming)
                    {
                        s.Step = BuildStep.Idle;
                    }
                    return s;
                case BuildInput.Action:
                    return s;   // done on what is picked; what is picked stays, written again
                case BuildInput.Design:
                    if (s.Tool == BuildTool.Place && (s.Step == BuildStep.Picked || s.Step == BuildStep.Ghost))
                    {
                        s.Step = BuildStep.Ghost;
                    }
                    return s;
                case BuildInput.Confirm:
                    if (s.Tool == BuildTool.Place && s.Step == BuildStep.Ghost && !s.ViewOnly)
                    {
                        s.Step = BuildStep.Confirming;
                    }
                    return s;
                case BuildInput.Yes:
                    if (s.Step == BuildStep.Confirming)
                    {
                        s.Step = BuildStep.Picked;
                        s.Tool = BuildTool.Land;   // the project made: its site picked again under the land tool
                    }
                    return s;
                case BuildInput.No:
                    if (s.Step == BuildStep.Confirming)
                    {
                        s.Step = BuildStep.Ghost;
                    }
                    return s;
                default:
                    return s;
            }
        }

        /// <summary>What is known of one column of cells for its colour.</summary>
        public struct ColumnFacts
        {
            public bool Free;        // for sale now
            public bool Mine;        // a site of the player's
            public int MineRows;     // rows of it the player has
            public int Paint;        // 0 none, else BuildUse + 1
            public bool PaintWaits;  // the paint's use is asked of the council
            public bool Cleared;     // ground the player cleared
            public bool UnderWay;    // a site under construction on it
            public bool Developer;   // the city's developers or a redevelopment on it
            public bool Building;    // a building of the district stands there
            public bool MyBuilding;  // ... the player's
            public int Use;          // that building's use (BuildUse), -1 for none known
            public bool Back;        // its rows past MineRows are behind the player's site (for sale only to the player)
        }

        /// <summary>The palette entry of one cell of a column: a site under way and the developers' whole, the player's rows by their paint, a building's by its use under the zone tool.</summary>
        public static int Status(ColumnFacts c, int row, BuildTool tool)
        {
            if (c.UnderWay)
            {
                return UnderWay;
            }
            if (c.Developer)
            {
                return Developer;
            }
            if (c.Mine)
            {
                if (row >= c.MineRows)
                {
                    return ForSale;   // behind the player's rows: theirs to buy deeper, nobody else's
                }
                if (c.Paint > 0)
                {
                    return c.PaintWaits ? ZoneWaits : Homes + Math.Min(3, c.Paint - 1);
                }
                return c.Cleared ? Cleared : Yours;
            }
            if (c.Building)
            {
                if (c.MyBuilding)
                {
                    return tool == BuildTool.Zone && c.Use >= 0 ? Homes + Math.Min(3, c.Use) : Yours;
                }
                return tool == BuildTool.Zone && c.Use >= 0 ? Homes + Math.Min(3, c.Use) : Taken;
            }
            return c.Free ? ForSale : Taken;
        }

        /// <summary>The warnings of a design placed on a footprint (bits of the Warn constants).</summary>
        public static int Warnings(PlaceAsk a)
        {
            int w = 0;
            if (a.Session)
            {
                w |= WarnSession;
            }
            if (!a.HasDesign)
            {
                w |= WarnNoDesign;
            }
            if (!a.Mine)
            {
                w |= WarnNotYours;
            }
            if (a.Columns > a.SiteColumns || a.Columns <= 0 || (a.HasDesign && a.RowsNeeded > a.RowsHad))
            {
                w |= WarnCells;
            }
            if (a.HasDesign && a.Limit > 0 && a.Storeys > a.Limit)
            {
                w |= WarnTall;
            }
            if (a.HasDesign && a.ParkingNeeded > 0 && !a.ParkingUnder)
            {
                w |= WarnParking;
            }
            if (!a.Zoned)
            {
                w |= WarnZone;
            }
            return w;
        }

        /// <summary>True when the warnings let Build here go on to the question box.</summary>
        public static bool MayBuild(int warnings) => (warnings & Stops) == 0;

        public const int DeveloperDemandLeast = 35;   // the city's developers start a building only where its use's demand is this or more
        public const int CouncilLiftPerPoint = 3;     // thousandths of a rezoning's odds each demand point over or under 50 is worth

        /// <summary>True when the developers may start a building of a use with this demand (none known, as in a Going Public session: as before demand).</summary>
        public static bool DeveloperMay(int demand) => demand < 0 || demand >= DeveloperDemandLeast;

        /// <summary>Thousandths a rezoning's odds move for the district's demand for the use asked: a wanted use passes more often (none known: none).</summary>
        public static int CouncilLift(int demand) => demand < 0 ? 0 : (Math.Max(0, Math.Min(100, demand)) - 50) * CouncilLiftPerPoint;

        /// <summary>The paint a use row gives: BuildUse + 1, or 0 for none.</summary>
        public static int PaintOf(int use) => use < 0 || use >= (int)BuildUse.Count ? 0 : use + 1;
    }
}
