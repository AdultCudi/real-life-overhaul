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

    /// <summary>What the layer shows, as Cities Skylines' info views (the modal's view buttons). Saved nowhere.</summary>
    public enum BuildView
    {
        Status = 0,      // for sale, the player's land and buildings, other owners', sites under way
        Zoning = 1,      // every cell by its council zoning, in the game's colours for its building types
        Value = 2,       // land value: each cell's price among the district's
        Occupancy = 3,   // buildings with room to let, and full ones
        UnderWay = 4,    // buildings under construction, their share built
        Count = 5
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
        // ---- the palette: one texel each in the layer's colour map; a cell's inside takes a fill entry, its edge a line entry

        public const int ForSale = 0;        // free land the player may buy
        public const int Taken = 1;          // another owner's building
        public const int YourLand = 2;       // the player's land with no building: a light fill in their colour
        public const int YourBuilding = 3;   // the player's building: a solid fill in their colour
        public const int Homes = 4;          // a use's zoning, by BuildUse + Homes, in the game's own colour for its building type
        public const int Shops = 5;
        public const int Offices = 6;
        public const int Warehouses = 7;
        public const int Mixed = 8;          // zoned for several uses, the plan's main one not among them
        public const int NoZone = 9;         // zoned for nothing
        public const int UnderWay = 10;      // a site under construction, the part still to build
        public const int UnderWayDone = 11;  // ... the part built, rows by the share done
        public const int Developer = 12;     // the city's developers' or a redevelopment's
        public const int Yard = 13;          // ground nobody sells (kept by the plan)
        public const int Picked = 14;
        public const int PickedBad = 15;
        public const int Footprint = 16;     // the ghost's cells
        public const int Vacant = 17;        // occupancy: a building with room to let
        public const int Let = 18;           // occupancy: a building with nobody to find
        public const int Nothing = 19;       // not drawn
        public const int Value = 20;         // land value: Value + 0 (lowest) to Value + ValueSteps - 1 (highest)
        public const int ValueSteps = 5;
        public const int LinePlain = 25;     // the thin grid line between cells
        public const int LineForSale = 26;   // round land for sale: bright, so buyable land reads on the dark map
        public const int LineZoned = 27;     // the player's land with a use in force: green (the owner, 26 Oct 2026)
        public const int LineDemolished = 28; // ground cleared, or a building coming down: red (the owner, 26 Oct 2026)
        public const int LineYours = 29;     // the player's land and buildings: their colour
        public const int LineWaits = 30;     // a use asked of the council
        public const int LinePicked = 31;
        public const int PaletteSize = 32;

        /// <summary>
        /// The palette (red, green, blue, alpha, 0 to 255). The four uses, for sale and the player's colour are the game's own
        /// (BuildingTypeData.mapFilterColor, CityMapFilters.forSaleFilterColor and playerFilterColor), written over these at the
        /// first opening (BuildModeGame.GameColours); these stand offline and wherever the game's colour is not found.
        /// </summary>
        public static readonly byte[][] Palette =
        {
            new byte[] { 242, 242, 242, 120 },  // for sale
            new byte[] { 60, 64, 72, 120 },     // another owner's building
            new byte[] { 178, 128, 255, 80 },   // your land
            new byte[] { 178, 128, 255, 205 },  // your building
            new byte[] { 76, 217, 89, 150 },    // homes
            new byte[] { 255, 160, 64, 150 },   // shops
            new byte[] { 64, 128, 255, 150 },   // offices (blue in the game)
            new byte[] { 255, 204, 38, 150 },   // warehouses
            new byte[] { 214, 90, 214, 150 },   // mixed
            new byte[] { 150, 150, 150, 70 },   // no zoning
            new byte[] { 255, 140, 30, 110 },   // under way, still to build
            new byte[] { 255, 170, 60, 220 },   // under way, built
            new byte[] { 130, 130, 150, 140 },  // the developers'
            new byte[] { 50, 50, 50, 50 },      // ground nobody sells
            new byte[] { 255, 255, 255, 200 },  // picked
            new byte[] { 255, 60, 60, 190 },    // picked, no site
            new byte[] { 110, 255, 110, 190 },  // the ghost's cells
            new byte[] { 240, 190, 90, 175 },   // vacant
            new byte[] { 90, 200, 140, 150 },   // let
            new byte[] { 0, 0, 0, 0 },          // nothing
            new byte[] { 40, 60, 140, 160 },    // land value, lowest
            new byte[] { 50, 130, 190, 160 },
            new byte[] { 90, 190, 150, 160 },
            new byte[] { 200, 210, 90, 160 },
            new byte[] { 255, 170, 60, 170 },   // land value, highest
            new byte[] { 255, 255, 255, 70 },   // the plain grid line
            new byte[] { 255, 255, 255, 190 },  // round land for sale
            new byte[] { 70, 220, 100, 255 },   // zoned: green
            new byte[] { 235, 60, 60, 255 },    // demolished: red
            new byte[] { 178, 128, 255, 255 },  // yours
            new byte[] { 240, 190, 90, 255 },   // waits on the council
            new byte[] { 255, 255, 255, 255 }   // picked
        };

        /// <summary>Colours the mixed zoning may take: the first far from every colour the game gives its types and the player.</summary>
        public static readonly byte[][] MixedChoices =
        {
            new byte[] { 214, 90, 214 }, new byte[] { 255, 110, 170 }, new byte[] { 150, 100, 255 }, new byte[] { 120, 255, 230 }, new byte[] { 255, 255, 140 }
        };

        public const int ColourApart = 9000;   // squared red, green and blue distance two colours of the layer keep to read apart

        /// <summary>The squared distance between two colours (red, green, blue).</summary>
        public static int Apart(byte[] a, byte[] b)
        {
            int r = a[0] - b[0], g = a[1] - b[1], u = a[2] - b[2];
            return r * r + g * g + u * u;
        }

        /// <summary>The first of the mixed zoning's choices far from every one of <paramref name="taken"/> (the game's colours); the farthest when none is.</summary>
        public static byte[] MixedColour(byte[][] taken)
        {
            byte[] best = MixedChoices[0];
            int bestNear = -1;
            for (int i = 0; i < MixedChoices.Length; i++)
            {
                int near = int.MaxValue;
                for (int k = 0; taken != null && k < taken.Length; k++)
                {
                    if (taken[k] != null)
                    {
                        near = Math.Min(near, Apart(MixedChoices[i], taken[k]));
                    }
                }
                if (near >= ColourApart)
                {
                    return MixedChoices[i];
                }
                if (near > bestNear)
                {
                    bestNear = near;
                    best = MixedChoices[i];
                }
            }
            return best;
        }

        /// <summary>The views' names, by BuildView (the modal's view buttons, as Cities Skylines' info views).</summary>
        public static readonly string[] ViewKeys =
        {
            "reallife_bld_mode_view_status", "reallife_bld_mode_view_zoning", "reallife_bld_mode_view_value", "reallife_bld_mode_view_occupancy", "reallife_bld_mode_view_underway"
        };

        /// <summary>The view a tool opens on: Buy land and Zone on the zoning (the owner, 26 Oct 2026: "How do I see the zoning?"), the rest on the land's status.</summary>
        public static BuildView ViewFor(BuildTool tool) => tool == BuildTool.Land || tool == BuildTool.Zone ? BuildView.Zoning : BuildView.Status;

        /// <summary>The palette entry a zoning shows in: a use painted, else none, else the plan's main use where it is zoned, else the one use, else mixed.</summary>
        public static int ZoneFill(int zones, int paint, int mainUse)
        {
            if (paint > 0)
            {
                return Homes + Math.Min(3, paint - 1);
            }
            if (zones == 0)
            {
                return NoZone;
            }
            if (mainUse >= 0 && mainUse < 4 && (zones & (1 << mainUse)) != 0)
            {
                return Homes + mainUse;
            }
            for (int u = 0; u < 4; u++)
            {
                if (zones == 1 << u)
                {
                    return Homes + u;
                }
            }
            return Mixed;
        }

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
            public int Progress;     // ... its share built, 0 to 100
            public bool Developer;   // the city's developers or a redevelopment on it
            public bool Building;    // a building of the district stands there
            public bool MyBuilding;  // ... the player's
            public bool Demolishing; // ... coming down (a demolition under way)
            public bool Vacant;      // ... with room to let (for rent, nobody in)
            public int Use;          // that building's use (BuildUse), -1 for none known
            public int Zones;        // the uses the council zones the column for (BuildCatalogue.Zone bits)
            public int MainUse;      // the plan's main use there (BuildCatalogue.ClassUse), -1 for none
            public int ValueStep;    // its land's price among the district's, 0 (lowest) to ValueSteps - 1
            public bool HasProject;  // a project of the player's on their land there (any stage): no second building placed on it
        }

        /// <summary>
        /// One cell's look in a view: its fill and its edge. The player's land and their building read apart (a light fill in their
        /// colour, a solid one), another owner's building in grey, land for sale white with a bright edge; a zoned cell's edge is
        /// green, a demolished or falling one's red (the owner, 26 Oct 2026); a site under way shows its share built by its rows.
        /// </summary>
        public static void Look(ColumnFacts c, int row, int rows, BuildView view, out int fill, out int line)
        {
            line = LinePlain;
            bool behind = c.Mine && row >= c.MineRows;   // behind the player's rows: theirs to buy deeper, nobody else's
            switch (view)
            {
                case BuildView.Zoning:
                    if (c.Mine && !behind)
                    {
                        fill = ZoneFill(c.Zones, c.PaintWaits ? 0 : c.Paint, c.MainUse);
                        line = c.Cleared ? LineDemolished : c.Paint > 0 ? (c.PaintWaits ? LineWaits : LineZoned) : LineYours;
                        return;
                    }
                    if (c.Building)
                    {
                        fill = c.Use >= 0 ? Homes + Math.Min(3, c.Use) : ZoneFill(c.Zones, 0, c.MainUse);
                        line = c.Demolishing ? LineDemolished : c.MyBuilding ? LineYours : LinePlain;
                        return;
                    }
                    fill = ZoneFill(c.Zones, 0, c.MainUse);
                    line = c.Free || behind ? LineForSale : LinePlain;
                    return;
                case BuildView.Value:
                    fill = Value + Math.Max(0, Math.Min(ValueSteps - 1, c.ValueStep));
                    line = c.Free || behind ? LineForSale : c.Mine ? LineYours : LinePlain;
                    return;
                case BuildView.Occupancy:
                    if (c.Building)
                    {
                        fill = c.Vacant ? Vacant : Let;
                        line = c.MyBuilding ? LineYours : LinePlain;
                        return;
                    }
                    fill = Nothing;
                    line = c.Free || behind ? LineForSale : c.Mine ? LineYours : LinePlain;
                    return;
                case BuildView.UnderWay:
                    if (c.UnderWay)
                    {
                        fill = Built(row, rows, c.Progress) ? UnderWayDone : UnderWay;
                        line = c.Demolishing ? LineDemolished : c.Mine || c.MyBuilding ? LineYours : LinePlain;
                        return;
                    }
                    fill = c.Developer ? Developer : Nothing;
                    line = c.Demolishing ? LineDemolished : LinePlain;
                    return;
            }
            if (c.UnderWay)
            {
                fill = Built(row, rows, c.Progress) ? UnderWayDone : UnderWay;
                line = c.Demolishing ? LineDemolished : c.Mine || c.MyBuilding ? LineYours : LinePlain;
                return;
            }
            if (c.Developer)
            {
                fill = Developer;
                return;
            }
            if (c.Mine)
            {
                if (behind)
                {
                    fill = ForSale;
                    line = LineForSale;
                    return;
                }
                if (c.Cleared)
                {
                    fill = YourLand;
                    line = LineDemolished;
                    return;
                }
                if (c.Paint > 0)
                {
                    fill = c.PaintWaits ? YourLand : Homes + Math.Min(3, c.Paint - 1);
                    line = c.PaintWaits ? LineWaits : LineZoned;
                    return;
                }
                fill = YourLand;
                line = LineYours;
                return;
            }
            if (c.Building)
            {
                fill = c.MyBuilding ? YourBuilding : Taken;
                line = c.Demolishing ? LineDemolished : c.MyBuilding ? LineYours : LinePlain;
                return;
            }
            fill = c.Free ? ForSale : Yard;
            line = c.Free ? LineForSale : LinePlain;
        }

        // A site under way is built from the street back: its first rows by the share done.
        private static bool Built(int row, int rows, int progress)
        {
            int done = rows <= 0 ? 0 : (Math.Max(0, Math.Min(100, progress)) * rows + 50) / 100;
            return row < done;
        }

        /// <summary>A price's land value step among the district's least and most (0 to ValueSteps - 1; all alike: the middle).</summary>
        public static int ValueStepOf(long price, long least, long most)
        {
            if (most <= least)
            {
                return ValueSteps / 2;
            }
            long step = (Math.Max(least, Math.Min(most, price)) - least) * ValueSteps / (most - least + 1);
            return (int)Math.Max(0, Math.Min(ValueSteps - 1, step));
        }

        /// <summary>A view's legend into the arrays given: each entry a fill (-1 for an edge alone), an edge and its text key; returns how many.</summary>
        public static int Legend(BuildView view, int[] fills, int[] lines, string[] keys)
        {
            int n = 0;
            switch (view)
            {
                case BuildView.Zoning:
                    n = Key(fills, lines, keys, n, Homes, LinePlain, "reallife_bld_use_homes");
                    n = Key(fills, lines, keys, n, Shops, LinePlain, "reallife_bld_use_shops");
                    n = Key(fills, lines, keys, n, Offices, LinePlain, "reallife_bld_use_offices");
                    n = Key(fills, lines, keys, n, Warehouses, LinePlain, "reallife_bld_use_warehouses");
                    n = Key(fills, lines, keys, n, Mixed, LinePlain, "reallife_bld_mode_key_mixed");
                    n = Key(fills, lines, keys, n, NoZone, LinePlain, "reallife_bld_mode_key_nozone");
                    n = Key(fills, lines, keys, n, -1, LineForSale, "reallife_bld_mode_key_forsale_edge");
                    n = Key(fills, lines, keys, n, -1, LineZoned, "reallife_bld_mode_key_zoned");
                    n = Key(fills, lines, keys, n, -1, LineWaits, "reallife_bld_mode_key_waits");
                    n = Key(fills, lines, keys, n, -1, LineDemolished, "reallife_bld_mode_key_demolished");
                    break;
                case BuildView.Value:
                    n = Key(fills, lines, keys, n, Value, LinePlain, "reallife_bld_mode_key_value_low");
                    n = Key(fills, lines, keys, n, Value + ValueSteps - 1, LinePlain, "reallife_bld_mode_key_value_high");
                    n = Key(fills, lines, keys, n, -1, LineForSale, "reallife_bld_mode_key_forsale_edge");
                    break;
                case BuildView.Occupancy:
                    n = Key(fills, lines, keys, n, Let, LinePlain, "reallife_bld_mode_key_let");
                    n = Key(fills, lines, keys, n, Vacant, LinePlain, "reallife_bld_mode_key_vacant");
                    n = Key(fills, lines, keys, n, -1, LineYours, "reallife_bld_mode_key_yours_edge");
                    break;
                case BuildView.UnderWay:
                    n = Key(fills, lines, keys, n, UnderWayDone, LinePlain, "reallife_bld_mode_key_built");
                    n = Key(fills, lines, keys, n, UnderWay, LinePlain, "reallife_bld_mode_key_tobuild");
                    n = Key(fills, lines, keys, n, Developer, LinePlain, "reallife_bld_mode_key_developer");
                    n = Key(fills, lines, keys, n, -1, LineDemolished, "reallife_bld_mode_key_demolished");
                    break;
                default:
                    n = Key(fills, lines, keys, n, ForSale, LineForSale, "reallife_bld_mode_key_forsale");
                    n = Key(fills, lines, keys, n, YourLand, LineYours, "reallife_bld_mode_key_yourland");
                    n = Key(fills, lines, keys, n, YourBuilding, LineYours, "reallife_bld_mode_key_yourbuilding");
                    n = Key(fills, lines, keys, n, Taken, LinePlain, "reallife_bld_mode_key_taken");
                    n = Key(fills, lines, keys, n, UnderWay, LinePlain, "reallife_bld_mode_key_underway");
                    n = Key(fills, lines, keys, n, Developer, LinePlain, "reallife_bld_mode_key_developer");
                    n = Key(fills, lines, keys, n, -1, LineZoned, "reallife_bld_mode_key_zoned");
                    n = Key(fills, lines, keys, n, -1, LineDemolished, "reallife_bld_mode_key_demolished");
                    break;
            }
            return n;
        }

        public const int LegendMost = 12;   // entries a view's legend has at most
        public const int LegendShort = 6;   // entries the legend beside the views shows (the owner, 26 Oct 2026: the legend "dominates" the tool panel); the rest in its tooltip

        /// <summary>The entries of a view's legend shown beside the views: its first <see cref="LegendShort"/> (the fills come first); the rest go in its tooltip.</summary>
        public static int LegendShown(int entries) => Math.Max(0, Math.Min(entries, LegendShort));

        /// <summary>
        /// Whether a drag with a tool may take a column (CellRules.RunOk): Buy land the land for sale and the player's own (to buy it
        /// deeper); Zone, Place and Join the player's own land; Bulldoze and Refurbish the player's buildings.
        /// </summary>
        public static bool Takes(BuildTool tool, ColumnFacts f)
        {
            switch (tool)
            {
                case BuildTool.Land: return f.Free || f.Mine;
                case BuildTool.Bulldoze:
                case BuildTool.Refurbish: return f.MyBuilding;
                default: return f.Mine;
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
        /// The cells picked as each column's depth (-1 none): a cell added takes its column to it from the street; a picked cell
        /// pressed again lets it and every cell behind it go (the add rule reversed); a column of another strip starts afresh.
        /// Returns true when anything changed.
        /// </summary>
        public static bool Toggle(int[] depth, int column, int row, bool remove)
        {
            if (column < 0 || column >= depth.Length || row < 0)
            {
                return false;
            }
            int had = depth[column];
            int now = remove ? (had >= row ? row - 1 : had) : Math.Max(had, row);
            depth[column] = now;
            return now != had;
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
