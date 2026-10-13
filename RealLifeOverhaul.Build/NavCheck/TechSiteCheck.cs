using System;
using System.Collections.Generic;
using RealLifeOverhaul.Tech;

// The game-free parts of the tech sector's buildings (Tech/Sites/SitePlan.cs, RoomPlans.cs): the floor grid; a data
// centre's server room by tier and by racks bought (rows, density, hot and cold aisles, redundancy, the walkways, the
// player's own furniture left usable); a headquarters' lobby; a tech office's fittings; and which office building is
// which tech company's headquarters (the same on every machine, never one taken, never one twice).
internal static partial class Program
{
    private static RoomGrid Floor(float wide, float deep)
    {
        var g = new RoomGrid(0f, 0f, wide, deep, 0.3f);
        for (int r = 0; r < g.Rows; r++)
        {
            for (int c = 0; c < g.Cols; c++)
            {
                g[c, r] = RoomGrid.Open;
            }
        }
        return g;
    }

    private static void Furnish(RoomGrid g, float x0, float z0, float x1, float z1)
    {
        for (int r = 0; r < g.Rows; r++)
        {
            for (int c = 0; c < g.Cols; c++)
            {
                g.Centre(c, r, out float x, out float z);
                if (x >= x0 && x <= x1 && z >= z0 && z <= z1)
                {
                    g[c, r] = RoomGrid.Furniture;
                }
            }
        }
    }

    private static bool Overlapping(List<SitePiece> pieces, out string which)
    {
        which = null;
        for (int a = 0; a < pieces.Count; a++)
        {
            for (int b = a + 1; b < pieces.Count; b++)
            {
                SitePiece p = pieces[a], q = pieces[b];
                if (!p.Stands || !q.Stands)
                {
                    continue;
                }
                if (Math.Abs(p.X - q.X) < p.HalfX + q.HalfX - 0.001f && Math.Abs(p.Z - q.Z) < p.HalfZ + q.HalfZ - 0.001f)
                {
                    which = p.Kind + " at " + p.X.ToString("F2") + "," + p.Z.ToString("F2") + " and " + q.Kind + " at " + q.X.ToString("F2") + "," + q.Z.ToString("F2");
                    return true;
                }
            }
        }
        return false;
    }

    private static bool OnOpenFloor(RoomGrid g, List<SitePiece> pieces)
    {
        foreach (SitePiece p in pieces)
        {
            if (p.Stands && !g.AllOpen(p.X - p.HalfX + 0.01f, p.Z - p.HalfZ + 0.01f, p.X + p.HalfX - 0.01f, p.Z + p.HalfZ - 0.01f, null))
            {
                return false;
            }
        }
        return true;
    }

    private static int Racked(ServerRoom room) => room.Servers + room.Accelerators;

    private static void TechSites()
    {
        // ---------------------------------------------------------------- the grid
        RoomGrid l = Floor(9f, 9f);
        Furnish(l, 4.5f, 4.5f, 9f, 9f);   // an L of open floor: the far quarter is furniture
        Check(l.Largest(OpenCells(l), out float x0, out float z0, out float x1, out float z1) && Near((x1 - x0) * (z1 - z0), 9f * 4.5f, 0.6f),
            "sites: the largest rectangle of an L-shaped floor is one of its arms (" + (x1 - x0) + " x " + (z1 - z0) + ")");
        var reach = new bool[l.Count];
        Check(l.Reach(0.5f, 0.5f, null, reach) > 0 && !l.Reached(6f, 6f, reach) && l.Reached(8.5f, 1f, reach), "sites: the walk from a corner reaches the open floor and never the furniture");
        var near = new bool[l.Count];
        l.NearFurniture(0.6f, near);
        Check(l.CellOf(4.1f, 6f, out int nc, out int nr) && near[nr * l.Cols + nc] && l.CellOf(3.3f, 6f, out nc, out nr) && !near[nr * l.Cols + nc],
            "sites: the cells within 0.6 m of furniture are near it, those 1.2 m away are not");

        // ---------------------------------------------------------------- a server room: an empty warehouse 24 x 16 m, its door in a short wall
        int[] tierSlots = new int[4];
        for (int tier = 0; tier < 4; tier++)
        {
            int lastRows = 0;
            foreach (int racks in new[] { 0, 6, 40, 120, 600 })
            {
                RoomGrid g = Floor(24f, 16f);
                var room = new ServerRoom();
                ServerRoomPlan.Plan(g, 0.3f, 8f, 1f, 0f, tier, racks, racks >= 40 ? racks / 8 : 0, room);
                string at = "sites: tier " + (tier + 1) + ", " + racks + " racks: ";
                Check(room.Why == null, at + "a hall was found (" + room.Why + ")");
                Check(!Overlapping(room.Pieces, out string which), at + "no two pieces stand on the same floor (" + which + ")");
                Check(OnOpenFloor(g, room.Pieces), at + "every piece stands on open floor");
                Check(room.Unreachable == 0, at + "every aisle is reached from the door (" + room.Unreachable + " places are not)");
                Check(room.Network == (tier == 3 ? 2 : 1), at + "the network core: one rack, two at tier IV (" + room.Network + ")");
                int wanted = racks + (racks >= 40 ? racks / 8 : 0);
                Check(Racked(room) == Math.Min(wanted, room.Slots), at + "the racks shown are those bought while they fit, a full hall beyond (" + Racked(room) + " of " + room.Slots + ")");
                bool clear = true;
                foreach (SitePiece p in room.Pieces)
                {
                    if (!p.Stands)
                    {
                        continue;
                    }
                    float dx = Math.Max(0f, Math.Abs(p.X - 0.3f) - p.HalfX), dz = Math.Max(0f, Math.Abs(p.Z - 8f) - p.HalfZ);
                    clear &= dx * dx + dz * dz >= ServerRoomPlan.DoorClear * ServerRoomPlan.DoorClear - 0.6f || p.Kind == SitePieceKind.OpsDesk;
                    clear &= !(p.X - p.HalfX < 0.3f + ServerRoomPlan.CorridorLength && Math.Abs(p.Z - 8f) < p.HalfZ + ServerRoomPlan.CorridorHalf - 0.05f);
                }
                Check(clear, at + "nothing stands at the door or in the way straight in");
                int usedRows = RowsUsed(room);
                Check(usedRows >= lastRows, at + "rows only grow with the racks bought (" + lastRows + " then " + usedRows + ")");
                lastRows = usedRows;
                if (racks == 6)
                {
                    Check(usedRows == 1, at + "a few racks stand in one row (" + usedRows + ")");
                }
                if (racks == 600)
                {
                    tierSlots[tier] = room.Slots;
                    int ups = room.Ups;
                    int[] upsWanted = { 1, 2, 4, 8 };
                    Check(ups == upsWanted[tier], at + "battery cabinets by the tier's redundancy (" + ups + ", wanted " + upsWanted[tier] + ")");
                    int groups = Math.Max(1, room.Pairs);
                    int[] coolingLeast = { (groups + 1) / 2, groups, groups + 1, 2 * groups };
                    Check(room.Cooling >= coolingLeast[tier], at + "cooling by the tier: " + room.Cooling + " units for " + groups + " pairs (" + coolingLeast[tier] + " at least)");
                    Check(room.Accelerators > 0 && room.Servers > room.Accelerators, at + "a full hall keeps the proportions of servers and accelerators (" + room.Servers + " and " + room.Accelerators + ")");
                    Aisles(room, tier, at);
                    Check(room.Panels > 0 && room.Panels <= ServerRoomPlan.PanelsMost && room.CountOf(SitePieceKind.Vent) > 0, at + "the raised floor and its perforated cold aisles (" + room.Panels + " panels)");
                    Check(room.Trays >= RowsUsed(room), at + "a cable tray over every row with racks (" + room.Trays + ")");
                    Check(room.Patrol.Count >= 4, at + "a way for the technicians along the aisles");
                    Check(room.HasDesk, at + "the operations desk by the door");
                    Check(room.Carve, at + "an empty warehouse: the rows may be cut out of the floor");
                }
            }
        }
        Check(tierSlots[3] > tierSlots[0] * 1.2f, "sites: tier IV packs the same hall denser than tier I (" + tierSlots[3] + " slots against " + tierSlots[0] + ")");

        // The same plan twice: every machine and every visit agree.
        {
            RoomGrid a = Floor(18f, 30f), b = Floor(18f, 30f);
            var first = new ServerRoom();
            var second = new ServerRoom();
            ServerRoomPlan.Plan(a, 9f, 0.3f, 0f, 1f, 2, 80, 12, first);
            ServerRoomPlan.Plan(b, 9f, 0.3f, 0f, 1f, 2, 80, 12, second);
            bool same = first.Pieces.Count == second.Pieces.Count;
            for (int i = 0; same && i < first.Pieces.Count; i++)
            {
                same = first.Pieces[i].Kind == second.Pieces[i].Kind && first.Pieces[i].X == second.Pieces[i].X && first.Pieces[i].Z == second.Pieces[i].Z && first.Pieces[i].Yaw == second.Pieces[i].Yaw;
            }
            Check(same && first.Pieces.Count > 20, "sites: the same warehouse and racks give the same room every time (" + first.Pieces.Count + " pieces)");
            bool alongZ = true;
            foreach (SitePiece p in first.Pieces)
            {
                alongZ &= p.Kind != SitePieceKind.Tray || p.Yaw == 0 || p.Yaw == 180;
            }
            Check(alongZ, "sites: in a hall longer from north to south the rows run that way too");
        }

        // A warehouse with the player's shelving in it: the rows keep their distance, the shelves stay usable.
        {
            RoomGrid g = Floor(24f, 16f);
            Furnish(g, 14f, 0.3f, 23.7f, 2.0f);    // a run of shelves along the far side wall
            Furnish(g, 20f, 6f, 21f, 12f);         // and an island
            var room = new ServerRoom();
            ServerRoomPlan.Plan(g, 0.3f, 8f, 1f, 0f, 1, 60, 0, room);
            var nearShelves = new bool[g.Count];
            g.NearFurniture(ServerRoomPlan.FromFurniture - 0.05f, nearShelves);
            bool apart = true;
            foreach (SitePiece p in room.Pieces)
            {
                apart &= !p.Stands || g.AllOpen(p.X - p.HalfX + 0.01f, p.Z - p.HalfZ + 0.01f, p.X + p.HalfX - 0.01f, p.Z + p.HalfZ - 0.01f, nearShelves);
            }
            Check(room.Why == null && Racked(room) > 0, "sites: a warehouse with shelving still has a server room (" + room.Why + ")");
            Check(apart, "sites: nothing stands within 0.55 m of the player's shelving");
            Check(room.Carve && room.Unreachable == 0, "sites: the shelving can still be reached round the rows");
        }

        // Shelving that the rows would cut off from the door: the rows are only to be seen, not cut out of the floor.
        {
            RoomGrid g = Floor(12f, 9f);
            Furnish(g, 0f, 7.5f, 0.6f, 9f);          // a cabinet in a corner beside the door wall...
            var room = new ServerRoom();
            ServerRoomPlan.Plan(g, 0.3f, 4.5f, 1f, 0f, 3, 400, 0, room);
            Check(room.Why == null && Racked(room) > 0 && room.Unreachable == 0, "sites: a small, full tier IV room plans (" + room.Why + ", " + Racked(room) + " racks)");
        }

        // Too small a warehouse: nothing but the desk.
        {
            RoomGrid g = Floor(4.5f, 4.5f);
            var room = new ServerRoom();
            ServerRoomPlan.Plan(g, 0.3f, 2.2f, 1f, 0f, 2, 20, 0, room);
            Check(room.Why != null && Racked(room) == 0, "sites: a store room of 4.5 m square has no server room (" + room.Why + ")");
        }

        // ---------------------------------------------------------------- a headquarters' lobby: an empty office 14 x 12 m, the door in the middle of a long wall
        {
            RoomGrid g = Floor(14f, 12f);
            var lobby = new Lobby();
            LobbyPlan.Plan(g, 7f, 0.3f, 0f, 1f, lobby);
            Check(lobby.HasDesk && lobby.CountOf(SitePieceKind.LogoWall) == 1, "sites: a lobby has its reception desk and logo wall (" + lobby.Why + ")");
            Check(lobby.HasSeating && lobby.CountOf(SitePieceKind.Armchair) == 2 && lobby.CountOf(SitePieceKind.Sofa) == 1, "sites: and its seating");
            Check(lobby.CountOf(SitePieceKind.Plant) >= 2 && lobby.CountOf(SitePieceKind.Screen) >= 1, "sites: and plants and screens (" + lobby.CountOf(SitePieceKind.Plant) + ", " + lobby.CountOf(SitePieceKind.Screen) + ")");
            Check(!Overlapping(lobby.Pieces, out string which), "sites: nothing in the lobby stands on anything else (" + which + ")");
            Check(OnOpenFloor(g, lobby.Pieces) && lobby.Unreachable == 0, "sites: the desk's front, the receptionist's place and the seats are reached from the door");
            bool corridor = true;
            foreach (SitePiece p in lobby.Pieces)
            {
                corridor &= !p.Stands || p.Kind == SitePieceKind.Reception || p.Kind == SitePieceKind.LogoWall || Math.Abs(p.X - 7f) >= Math.Max(p.HalfX, p.HalfZ) + 0.9f;
            }
            Check(corridor, "sites: the way from the door to the desk is kept clear");
            Check(lobby.Depth >= 3f && lobby.Depth <= 6f && Near(lobby.FrontZ, lobby.Depth - 0.9f + 0.3f, 0.05f), "sites: the desk stands 3 to 6 m in (" + lobby.Depth + ")");

            RoomGrid shallow = Floor(10f, 3.6f);
            var small = new Lobby();
            LobbyPlan.Plan(shallow, 5f, 0.3f, 0f, 1f, small);
            Check(!small.HasDesk && small.Why != null && !Overlapping(small.Pieces, out _), "sites: a shallow unit has no desk across its way in (" + small.Why + ")");
        }

        // ---------------------------------------------------------------- a tech office's fittings
        {
            RoomGrid g = Floor(12f, 9f);
            Furnish(g, 2f, 6f, 6f, 7f);       // a row of the player's desks
            Furnish(g, 8f, 2f, 9f, 5f);       // and a cabinet
            var fit = new OfficeFit();
            OfficeFitPlan.Plan(g, 6f, 0.3f, 3, fit);
            var nearFurniture = new bool[g.Count];
            g.NearFurniture(OfficeFitPlan.FromFurniture - 0.05f, nearFurniture);
            bool apart = true;
            foreach (SitePiece p in fit.Pieces)
            {
                apart &= g.AllOpen(p.X - p.HalfX + 0.01f, p.Z - p.HalfZ + 0.01f, p.X + p.HalfX - 0.01f, p.Z + p.HalfZ - 0.01f, nearFurniture) || !p.Stands;
            }
            Check(fit.Pieces.Count == 3 && fit.Unreachable == 0, "sites: an office of 108 square metres takes the three fittings (" + fit.Pieces.Count + ", " + fit.Why + ")");
            Check(apart && !Overlapping(fit.Pieces, out _), "sites: no fitting stands on or near the player's furniture");
            var before = new bool[g.Count];
            var after = new bool[g.Count];
            var closed = new bool[g.Count];
            var sides = new bool[g.Count];
            g.Reach(6f, 0.3f, null, before);
            g.FurnitureSides(before, sides);
            g.Close(fit.Pieces, OfficeFitPlan.Body, closed);
            g.Reach(6f, 0.3f, closed, after);
            bool usable = true;
            for (int i = 0; i < sides.Length; i++)
            {
                usable &= !sides[i] || after[i];
            }
            Check(usable, "sites: the player's furniture can all be reached round the fittings");
            bool deep = true;
            foreach (SitePiece p in fit.Pieces)
            {
                deep &= (p.X - 6f) * (p.X - 6f) + (p.Z - 0.3f) * (p.Z - 0.3f) >= OfficeFitPlan.FromDoor * OfficeFitPlan.FromDoor;
            }
            Check(deep, "sites: nothing is fitted by the door");

            RoomGrid cupboard = Floor(4f, 4f);
            var none = new OfficeFit();
            OfficeFitPlan.Plan(cupboard, 2f, 0.3f, 3, none);
            Check(none.Pieces.Count == 0 && none.Why != null, "sites: a small office is left as it is (" + none.Why + ")");
        }

        // ---------------------------------------------------------------- headquarters
        {
            var companies = new List<string> { "Codeharbour Software", "Skyvault Systems", "Abacus Lane Software", "Shopfront Online", "Pixel Harbour Games", "Tallyrow Analytics",
                "Signpost Search", "Circuitry Works", "Quartzline Semiconductors", "Ledgerline Payments", "Signalhill Telecom", "Fibreway Networks" };
            var all = new List<HqCandidate>();
            for (int i = 0; i < 50; i++)
            {
                all.Add(new HqCandidate { Street = "Street " + (i % 7), Number = 10 + i, Sqm = 300 + (i * 37) % 900 });
            }
            int[] pick = new int[companies.Count], again = new int[companies.Count];
            HqPick.Pick(all, companies, null, pick);
            HqPick.Pick(all, companies, null, again);
            Check(Same(pick, again), "sites: the same city gives the same headquarters every time");
            Check(Distinct(pick) && Array.IndexOf(pick, -1) < 0, "sites: every company has a building and no two share one");
            var shuffled = new List<HqCandidate>(all);
            shuffled.Reverse();
            int[] fromShuffled = new int[companies.Count];
            HqPick.Pick(shuffled, companies, null, fromShuffled);
            bool sameBuildings = true;
            for (int k = 0; k < pick.Length; k++)
            {
                sameBuildings &= HqPick.Key(all[pick[k]].Street, all[pick[k]].Number) == HqPick.Key(shuffled[fromShuffled[k]].Street, shuffled[fromShuffled[k]].Number);
            }
            Check(sameBuildings, "sites: the order the game lists its buildings in does not change the pick");
            var sizes = new List<int>();
            foreach (HqCandidate c in all)
            {
                sizes.Add(c.Sqm);
            }
            sizes.Sort();
            int poolLeast = sizes[sizes.Count - companies.Count * HqPick.PoolPer];
            bool large = true;
            foreach (int i in pick)
            {
                large &= all[i].Sqm >= poolLeast;
            }
            Check(large, "sites: the headquarters are among the city's largest office buildings");
            // The player rents one of them: that company moves, the others mostly stay.
            int rented = pick[3];
            int[] after = new int[companies.Count];
            HqPick.Pick(all, companies, i => i == rented, after);
            int moved = 0;
            for (int k = 0; k < pick.Length; k++)
            {
                moved += pick[k] != after[k] ? 1 : 0;
            }
            Check(Array.IndexOf(after, rented) < 0 && after[3] != rented && Distinct(after), "sites: a building the player rents is never a headquarters");
            Check(moved <= 3, "sites: the company whose building is let moves, and few others (" + moved + " moved)");
            // Another of the mod's buildings and the player's own: passed over too.
            int[] withHeld = new int[companies.Count];
            HqPick.Pick(all, companies, i => i % 2 == 0, withHeld);
            bool odd = true;
            foreach (int i in withHeld)
            {
                odd &= i >= 0 && i % 2 == 1;
            }
            Check(odd && Distinct(withHeld), "sites: buildings held by other features are never taken");
            // Too few buildings: the rest have none.
            var few = all.GetRange(0, 5);
            int[] short_ = new int[companies.Count];
            HqPick.Pick(few, companies, null, short_);
            int none = 0;
            foreach (int i in short_)
            {
                none += i < 0 ? 1 : 0;
            }
            Check(none == companies.Count - 5 && Distinct(short_), "sites: with five buildings, five companies have one and seven none (" + none + ")");
            float r, gr, b;
            HqColours.Of(1, false, out r, out gr, out b);
            Check(HqColours.Count == companies.Count && b > r && b > gr, "sites: a colour for each company (Skyvault Systems is blue)");
        }
    }

    // --sites: each plan drawn in letters, a cell to a character (R racks, A accelerators, N network, C cooling, U battery
    // cabinets, D the desk, T the trays over the rows, ~ perforated tiles, # furniture, . open floor, * the door).
    private static void Draw(RoomGrid g, SitePlanBase plan, float doorX, float doorZ, string title)
    {
        Console.WriteLine(title);
        for (int r = g.Rows - 1; r >= 0; r--)
        {
            var line = new System.Text.StringBuilder(g.Cols);
            for (int c = 0; c < g.Cols; c++)
            {
                g.Centre(c, r, out float x, out float z);
                char ch = g[c, r] == RoomGrid.Furniture ? '#' : g[c, r] == RoomGrid.Open ? '.' : ' ';
                foreach (SitePiece p in plan.Pieces)
                {
                    if (!p.Contains(x, z, 0f))
                    {
                        continue;
                    }
                    char k = Letter(p.Kind);
                    if (p.Stands || ch == '.' || (ch == '~' && k == 'T'))
                    {
                        ch = k;
                    }
                }
                if (Math.Abs(x - doorX) < 0.16f && Math.Abs(z - doorZ) < 0.16f)
                {
                    ch = '*';
                }
                line.Append(ch);
            }
            Console.WriteLine("  " + line);
        }
    }

    private static char Letter(SitePieceKind kind)
    {
        switch (kind)
        {
            case SitePieceKind.Rack: return 'R';
            case SitePieceKind.AiRack: return 'A';
            case SitePieceKind.NetworkRack: return 'N';
            case SitePieceKind.Cooling: return 'C';
            case SitePieceKind.Ups: return 'U';
            case SitePieceKind.OpsDesk: return 'D';
            case SitePieceKind.Tray: return 'T';
            case SitePieceKind.Vent: return '~';
            case SitePieceKind.Floor: return '.';
            case SitePieceKind.Reception: return 'D';
            case SitePieceKind.LogoWall: return 'L';
            case SitePieceKind.Sofa: return 'S';
            case SitePieceKind.Armchair: return 'a';
            case SitePieceKind.LowTable: return 't';
            case SitePieceKind.Plant: return 'p';
            case SitePieceKind.Screen: return 'V';
            case SitePieceKind.ClosetRack: return 'R';
            case SitePieceKind.Whiteboard: return 'W';
            case SitePieceKind.TechBench: return 'B';
            default: return '?';
        }
    }

    private static void DrawSites()
    {
        foreach (int tier in new[] { 0, 3 })
        {
            RoomGrid g = Floor(24f, 16f);
            Furnish(g, 14f, 0.3f, 23.7f, 2.0f);
            var room = new ServerRoom();
            ServerRoomPlan.Plan(g, 0.3f, 8f, 1f, 0f, tier, 70, 12, room);
            Draw(g, room, 0.3f, 8f, "tier " + (tier + 1) + ", 70 racks and 12 of accelerators: " + room.Slots + " slots, " + room.Servers + " + " + room.Accelerators + " shown, "
                                     + room.Pairs + " pairs, " + room.Cooling + " cooling, " + room.Ups + " cabinets, desk " + room.HasDesk + ", carve " + room.Carve);
        }
        RoomGrid h = Floor(14f, 12f);
        var lobby = new Lobby();
        LobbyPlan.Plan(h, 7f, 0.3f, 0f, 1f, lobby);
        Draw(h, lobby, 7f, 0.3f, "a lobby of 14 x 12 m (" + lobby.Pieces.Count + " pieces)");
        RoomGrid o = Floor(12f, 9f);
        Furnish(o, 2f, 6f, 6f, 7f);
        Furnish(o, 8f, 2f, 9f, 5f);
        var fit = new OfficeFit();
        OfficeFitPlan.Plan(o, 6f, 0.3f, 3, fit);
        Draw(o, fit, 6f, 0.3f, "an office of 12 x 9 m (" + fit.Pieces.Count + " fittings)");
    }

    private static bool[] OpenCells(RoomGrid g)
    {
        var open = new bool[g.Count];
        for (int i = 0; i < open.Length; i++)
        {
            open[i] = g.At(i) == RoomGrid.Open;
        }
        return open;
    }

    private static int RowsUsed(ServerRoom room)
    {
        var rows = new HashSet<int>();
        foreach (SitePiece p in room.Pieces)
        {
            if ((p.Kind == SitePieceKind.Rack || p.Kind == SitePieceKind.AiRack) && p.Row >= 0)
            {
                rows.Add(p.Row);
            }
        }
        return rows.Count;
    }

    // The racks of two rows facing each other are a cold aisle apart; backs facing each other, a hot aisle.
    private static void Aisles(ServerRoom room, int tier, string at)
    {
        float coldLeast = float.MaxValue, hotLeast = float.MaxValue;
        var racks = room.Pieces.FindAll(p => p.Kind == SitePieceKind.Rack || p.Kind == SitePieceKind.AiRack || p.Kind == SitePieceKind.NetworkRack);
        foreach (SitePiece a in racks)
        {
            foreach (SitePiece b in racks)
            {
                if (a.Row == b.Row || Math.Abs(a.X - b.X) > 0.01f && Math.Abs(a.Z - b.Z) > 0.01f)
                {
                    continue;
                }
                bool alongX = a.Yaw == 0 || a.Yaw == 180;   // fronts to +-Z: the rows run along X
                float gap = alongX ? Math.Abs(a.Z - b.Z) - a.HalfZ - b.HalfZ : Math.Abs(a.X - b.X) - a.HalfX - b.HalfX;
                float ax = (float)Math.Sin(a.Yaw * Math.PI / 180), az = (float)Math.Cos(a.Yaw * Math.PI / 180);
                bool facing = (b.X - a.X) * ax + (b.Z - a.Z) * az > 0f;   // a's front looks towards b
                if (facing)
                {
                    coldLeast = Math.Min(coldLeast, gap);
                }
                else
                {
                    hotLeast = Math.Min(hotLeast, gap);
                }
            }
        }
        Check(hotLeast >= ServerRoomPlan.HotAisle[tier] - 0.01f, at + "hot aisles " + ServerRoomPlan.HotAisle[tier] + " m or wider between the racks' backs (" + hotLeast.ToString("F2") + ")");
        Check(coldLeast == float.MaxValue || coldLeast >= ServerRoomPlan.ColdAisle[tier] - 0.01f, at + "cold aisles " + ServerRoomPlan.ColdAisle[tier] + " m or wider between the racks' fronts (" + coldLeast.ToString("F2") + ")");
    }

    private static bool Same(int[] a, int[] b)
    {
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i] != b[i])
            {
                return false;
            }
        }
        return true;
    }

    private static bool Distinct(int[] picks)
    {
        var seen = new HashSet<int>();
        foreach (int i in picks)
        {
            if (i >= 0 && !seen.Add(i))
            {
                return false;
            }
        }
        return true;
    }
}
