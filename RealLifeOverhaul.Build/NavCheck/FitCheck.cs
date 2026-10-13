using System;
using System.Collections.Generic;
using System.Diagnostics;
using RealLifeOverhaul.FitOut;
using RealLifeOverhaul.Tech;

// The fit-out of the mod's buildings (FitOut/FitRules.cs, FitPlanner.cs, FitPlanner.Zones.cs): a tech company's
// headquarters behind its lobby, from bare floor and round the game's own design; a station's bare back office round its
// counter and cell; a data centre's floor round its server hall and its drive-in bay; the university's lecture rooms
// and library. Every plan: nothing on anything else, nothing at a door or in the street wall's windows, every seat and
// every piece walked up to reached from the door, no corner of the floor closed off, the floor fitted out to its edges,
// the same plan every time, and quickly.
internal static partial class Program
{
    private static RoomGrid FitFloor(float wide, float deep)
    {
        var g = new RoomGrid(0f, 0f, wide, deep, 0.3f);
        for (int r = 0; r < g.Rows; r++)
        {
            for (int c = 0; c < g.Cols; c++)
            {
                g.Centre(c, r, out float x, out float z);
                // (the walkable floor stops a walker's width short of the walls, as the game's navigation mesh does)
                if (x > 0.3f && x < wide - 0.3f && z > 0.3f && z < deep - 0.3f)
                {
                    g[c, r] = RoomGrid.Open;
                }
            }
        }
        return g;
    }

    private static void FitWall(RoomGrid g, float x0, float z0, float x1, float z1)
    {
        for (int r = 0; r < g.Rows; r++)
        {
            for (int c = 0; c < g.Cols; c++)
            {
                g.Centre(c, r, out float x, out float z);
                if (x >= x0 && x <= x1 && z >= z0 && z <= z1)
                {
                    g[c, r] = RoomGrid.Outside;
                }
            }
        }
    }

    private static FitSite FitSiteAt(FitKind kind, float doorX, float doorZ, float inX, float inZ, int seed = 7)
    {
        return new FitSite { Kind = kind, Seed = seed, DoorX = doorX, DoorZ = doorZ, InX = inX, InZ = inZ, Erosion = 0.3f };
    }

    private static bool Standing(FitPiece p) => p.Kept && (p.Role == FitRole.Floor || p.Role == FitRole.Seat);

    // Two standing pieces on the same floor: never, but a chair may be drawn in under its own desk or table.
    private static bool FitOverlaps(FitPlan plan, out string which)
    {
        which = null;
        List<FitPiece> ps = plan.Pieces;
        for (int a = 0; a < ps.Count; a++)
        {
            for (int b = a + 1; b < ps.Count; b++)
            {
                FitPiece p = ps[a], q = ps[b];
                if (!Standing(p) || !Standing(q) || p.Anchor == b || q.Anchor == a || (p.Design && q.Design))
                {
                    continue;
                }
                if (Math.Abs(p.CX - q.CX) < p.HalfX + q.HalfX - 0.02f && Math.Abs(p.CZ - q.CZ) < p.HalfZ + q.HalfZ - 0.02f)
                {
                    which = p.Prefab + " at " + p.CX.ToString("F2") + "," + p.CZ.ToString("F2") + " and " + q.Prefab + " at " + q.CX.ToString("F2") + "," + q.CZ.ToString("F2");
                    return true;
                }
            }
        }
        return false;
    }

    private static bool FitOnFixed(FitPlan plan, FitSite site, out string which)
    {
        which = null;
        foreach (FitPiece p in plan.Pieces)
        {
            if (!Standing(p))
            {
                continue;
            }
            foreach (FitRect r in site.Fixed)
            {
                if (p.CX + p.HalfX > r.X0 + 0.01f && p.CX - p.HalfX < r.X1 - 0.01f && p.CZ + p.HalfZ > r.Z0 + 0.01f && p.CZ - p.HalfZ < r.Z1 - 0.01f)
                {
                    which = p.Prefab + " at " + p.CX.ToString("F2") + "," + p.CZ.ToString("F2");
                    return true;
                }
            }
        }
        return false;
    }

    // Every standing piece on the room's floor: its middle on open floor, or (backing onto a wall) within a walker's width of it.
    private static bool FitOnFloor(RoomGrid g, FitPlan plan, out string which)
    {
        which = null;
        foreach (FitPiece p in plan.Pieces)
        {
            if (!Standing(p))
            {
                continue;
            }
            bool on = g.CellOf(p.CX, p.CZ, out int c, out int r) && g.IsOpen(c, r);
            for (int k = 0; k < 4 && !on; k++)
            {
                float x = p.CX + (k == 0 ? 0.35f : k == 1 ? -0.35f : 0f), z = p.CZ + (k == 2 ? 0.35f : k == 3 ? -0.35f : 0f);
                on = g.CellOf(x, z, out c, out r) && g.IsOpen(c, r);
            }
            if (!on)
            {
                which = p.Prefab + " at " + p.CX.ToString("F2") + "," + p.CZ.ToString("F2");
                return false;
            }
        }
        return true;
    }

    private static bool FitClearOfDoor(FitPlan plan, FitSite site)
    {
        foreach (FitPiece p in plan.Pieces)
        {
            if (!Standing(p))
            {
                continue;
            }
            var foot = new FitRect(p.CX - p.HalfX, p.CZ - p.HalfZ, p.CX + p.HalfX, p.CZ + p.HalfZ, true);
            if (foot.Distance(site.DoorX, site.DoorZ) < FitPlanner.DoorClear - 0.01f)
            {
                return false;
            }
            foreach (FitSpot e in site.Exits)
            {
                if (foot.Distance(e.X, e.Z) < e.R - 0.01f)
                {
                    return false;
                }
            }
        }
        return true;
    }

    private static bool FitNoTallInWindows(FitPlan plan, FitSite site)
    {
        foreach (FitPiece p in plan.Pieces)
        {
            if (!site.Windows || !p.Kept || p.Design || !p.Tall || p.Role != FitRole.Floor)
            {
                continue;
            }
            float depth = Math.Min(Math.Abs((p.CX - p.HalfX - site.DoorX) * site.InX + (p.CZ - p.HalfZ - site.DoorZ) * site.InZ),
                Math.Abs((p.CX + p.HalfX - site.DoorX) * site.InX + (p.CZ + p.HalfZ - site.DoorZ) * site.InZ));
            if (depth < FitPlanner.FacadeBand - 0.01f)
            {
                return false;
            }
        }
        return true;
    }

    private static int FitCount(FitPlan plan, string prefab)
    {
        int n = 0;
        foreach (FitPiece p in plan.Pieces)
        {
            n += p.Kept && p.Prefab == prefab ? 1 : 0;
        }
        return n;
    }

    private static int FitZones(FitPlan plan, FitPlanner.ZoneType type) => plan.ZoneCounts[(int)type];

    // The lobby of a headquarters as fixed floor: its pieces, its screens and the places at the desk.
    private static void FitLobby(RoomGrid g, FitSite site, Lobby lobby)
    {
        LobbyPlan.Plan(g, site.DoorX, site.DoorZ, site.InX, site.InZ, lobby);
        foreach (SitePiece p in lobby.Pieces)
        {
            site.Fixed.Add(FitRect.Around(p.X, p.Z, p.HalfX, p.HalfZ, p.Stands, wall: p.Kind == SitePieceKind.Screen));
        }
        if (lobby.HasDesk)
        {
            site.Keep.Add(new FitSpot(lobby.StaffX, lobby.StaffZ, 0.5f));
            site.Keep.Add(new FitSpot(lobby.FrontX, lobby.FrontZ, 0.5f));
            float x0 = Math.Min(site.DoorX, lobby.FrontX) - 0.7f, x1 = Math.Max(site.DoorX, lobby.FrontX) + 0.7f;
            float z0 = Math.Min(site.DoorZ, lobby.FrontZ) - 0.7f, z1 = Math.Max(site.DoorZ, lobby.FrontZ) + 0.7f;
            site.KeepFree.Add(new FitRect(x0, z0, x1, z1, false));
        }
    }

    // The checks every plan keeps, said for one room.
    private static void FitKeeps(RoomGrid g, FitSite site, FitPlan plan, string at)
    {
        Check(plan.Why == null, at + "planned (" + plan.Why + ")");
        Check(!FitOverlaps(plan, out string which), at + "no two pieces stand on the same floor (" + which + ")");
        Check(!FitOnFixed(plan, site, out which), at + "nothing stands on what was already there (" + which + ")");
        Check(FitOnFloor(g, plan, out which), at + "every piece stands on the room's floor (" + which + ")");
        Check(FitClearOfDoor(plan, site), at + "nothing stands at the door or at another way out");
        Check(FitNoTallInWindows(plan, site), at + "nothing tall stands in the windows of the street wall");
        Check(plan.Unreached == 0, at + "every seat and every piece walked up to is reached from the door (" + plan.Unreached + " not)");
        Check(plan.Pockets == 0, at + "no part of the floor is closed off (" + plan.Pockets + ")");
    }

    private static void Fits()
    {
        // ---------------------------------------------------------------- the rules on the game's names
        Check(!FitRoles.Fitted("cashregister") && !FitRoles.Fitted("jobboardforsale") && !FitRoles.Fitted("handtruckspawner") && FitRoles.Fitted("officedesk1"),
            "fit: a business's trade (tills, job boards, spawners) is never fitted, an office's furniture is");
        Check(FitRoles.IsChair("officechair2") && FitRoles.IsChair("regularchair") && !FitRoles.IsChair("armchair1") && !FitRoles.IsChair("chaircinema") && !FitRoles.IsChair("waitingroomchairs"),
            "fit: a chair is drawn up to a desk or table; an armchair, a cinema seat and a row of waiting seats are not");
        Check(FitRoles.Surface("officedesk1") == true && FitRoles.Surface("largemeetingtable") == false && FitRoles.Surface("deskplant01") == null && FitRoles.Surface("sofaluxury") == null,
            "fit: what a chair is drawn up to: a desk, a table; never a desk plant or a sofa");
        Check(FitRoles.Spare("calculator") == 2 && FitRoles.Spare("painting50x70") == 1 && FitRoles.Spare("officedesk1") == 0, "fit: the desk clutter goes first, then the decoration");
        Check(FitRoles.IsLamp("ceilinglamptube") && FitRoles.IsFlat("carpetbigmodern") && FitRoles.IsStop("cheapcoffeemachine") && FitRoles.IsStop("printer"),
            "fit: lamps, carpets, and what somebody walks up to");

        // ---------------------------------------------------------------- a tech company's headquarters, bare behind its lobby: 21 x 29 m, the door in a short wall
        double slowest = 0;
        {
            RoomGrid g = FitFloor(21f, 29f);
            FitSite site = FitSiteAt(FitKind.TechOffice, 10.5f, 0.35f, 0f, 1f);
            var lobby = new Lobby();
            FitLobby(g, site, lobby);
            var plan = new FitPlan();
            var watch = Stopwatch.StartNew();
            FitPlanner.Plan(g, site, null, plan);
            slowest = Math.Max(slowest, watch.Elapsed.TotalMilliseconds);
            string at = "fit: a bare headquarters of 21 x 29 m: ";
            Check(lobby.HasDesk && lobby.CountOf(SitePieceKind.LogoWall) == 1, at + "its lobby stands (" + lobby.Why + ")");
            FitKeeps(g, site, plan, at);
            Check(plan.Seats(FitUse.Desk) >= 16, at + "open-plan desks, each with its chair (" + plan.Seats(FitUse.Desk) + ")");
            Check(FitCount(plan, "computer") == FitCount(plan, "officedesk1") && FitCount(plan, "computermonitor") == FitCount(plan, "officedesk1"),
                at + "a computer and a monitor on every desk (" + FitCount(plan, "computer") + " of " + FitCount(plan, "officedesk1") + ")");
            Check(FitZones(plan, FitPlanner.ZoneType.Meeting) >= 1 && FitCount(plan, "wallmountedtv") >= 1 && plan.Seats(FitUse.Meeting) >= 4,
                at + "a meeting room with a table, chairs and a screen (" + FitZones(plan, FitPlanner.ZoneType.Meeting) + ")");
            Check(FitZones(plan, FitPlanner.ZoneType.Kitchen) == 1 && FitCount(plan, "cheapcoffeemachine") == 1, at + "a kitchen with its coffee machine");
            Check(FitZones(plan, FitPlanner.ZoneType.Lounge) >= 1 && FitCount(plan, "sofaluxury") >= 1, at + "a lounge");
            Check(FitZones(plan, FitPlanner.ZoneType.Server) == 1 && FitCount(plan, "rlo:closetrack") == 2, at + "a server closet");
            Check(FitCount(plan, "printer") >= 1, at + "a printer");
            Check(FitCount(plan, "groundplant03") + FitCount(plan, "groundplant01") + FitCount(plan, "groundplant05") + FitCount(plan, "groundplant02") + FitCount(plan, "groundplant04") >= 4, at + "plants");
            Check(plan.HasLamps, at + "lamps over the rooms");
            Check(plan.BareAfter <= plan.FloorArea * 0.10f, at + "fitted out across its whole floor (" + plan.BareAfter.ToString("F0") + " of " + plan.FloorArea.ToString("F0") + " square metres still bare, " + plan.BareBefore.ToString("F0") + " before)");
            Check(plan.Stops.Count >= 4, at + "places for the staff to walk to (" + plan.Stops.Count + ")");
            Check(plan.Kept <= FitPlanner.MostPieces, at + "no more pieces than a visit is given (" + plan.Kept + ")");
            FitCrew crew = FitCrew.For(FitKind.TechOffice, plan.Seats(FitUse.Desk), plan.Seats(FitUse.Meeting), plan.Seats(FitUse.Lounge), 0, plan.Stops.Count, 9);
            Check(crew.AtDesks >= 3 && crew.Walking == 2 && crew.Total <= 9, at + "staff at the desks and two walking (" + crew.AtDesks + " and " + crew.Walking + ")");

            // The same building, the same plan.
            var again = new FitPlan();
            FitPlanner.Plan(g, site, null, again);
            bool same = again.Pieces.Count == plan.Pieces.Count;
            for (int i = 0; same && i < plan.Pieces.Count; i++)
            {
                same = again.Pieces[i].Prefab == plan.Pieces[i].Prefab && again.Pieces[i].X == plan.Pieces[i].X && again.Pieces[i].Z == plan.Pieces[i].Z && again.Pieces[i].Kept == plan.Pieces[i].Kept;
            }
            Check(same, at + "the same plan every time");

            // Another building of the same size starts its rooms elsewhere in the turn.
            FitSite other = FitSiteAt(FitKind.TechOffice, 10.5f, 0.35f, 0f, 1f, seed: 8);
            FitLobby(g, other, new Lobby());
            var otherPlan = new FitPlan();
            FitPlanner.Plan(g, other, null, otherPlan);
            FitKeeps(g, other, otherPlan, "fit: another headquarters of the same size: ");
        }

        // ---------------------------------------------------------------- the same headquarters round the game's design for the unit
        {
            RoomGrid g = FitFloor(21f, 29f);
            FitSite site = FitSiteAt(FitKind.TechOffice, 10.5f, 0.35f, 0f, 1f);
            FitLobby(g, site, new Lobby());
            var design = FitDesign(21f, 29f, out int deskCount);
            var plan = new FitPlan();
            var watch = Stopwatch.StartNew();
            FitPlanner.Plan(g, site, design, plan);
            slowest = Math.Max(slowest, watch.Elapsed.TotalMilliseconds);
            string at = "fit: a headquarters round the game's design: ";
            FitKeeps(g, site, plan, at);
            Check(!plan.DesignGivenUp && plan.KeptDesign > design.Count / 2, at + "most of the design stands (" + plan.KeptDesign + " of " + design.Count + ")");
            Check(plan.DesignClashed > 0, at + "what stood where the lobby is was left out (" + plan.DesignClashed + ")");
            bool dependents = true;
            foreach (FitPiece p in plan.Pieces)
            {
                dependents &= !p.Kept || p.Anchor < 0 || plan.Pieces[p.Anchor].Kept;
            }
            Check(dependents, at + "nothing stands on a desk left out, no chair at one");
            int blocking = -1;
            for (int i = 0; i < plan.Pieces.Count; i++)
            {
                blocking = plan.Pieces[i].Prefab == "bigcabinet" && plan.Pieces[i].Design ? i : blocking;
            }
            Check(blocking >= 0 && !plan.Pieces[blocking].Kept, at + "the design's cabinet across the washroom's doorway is left out, the washroom reached");
            Check(plan.Seats(FitUse.Desk) >= deskCount / 2, at + "its desks keep their chairs (" + plan.Seats(FitUse.Desk) + ")");
        }

        // ---------------------------------------------------------------- a design with more than a visit is given: the clutter goes first
        {
            RoomGrid g = FitFloor(30f, 30f);
            FitSite site = FitSiteAt(FitKind.TechOffice, 15f, 0.35f, 0f, 1f);
            site.Zones = false;
            var design = new List<FitPiece>();
            for (int row = 0; row < 12; row++)
            {
                for (int k = 0; k < 10; k++)
                {
                    float x = 3f + k * 2.4f, z = 6f + row * 1.9f;
                    design.Add(DesignPiece("officedesk1", FitRole.Floor, x, z, 0f, 0.8f, 0.45f));
                    design.Add(DesignPiece("calculator", FitRole.OnTop, x + 0.3f, z, 0.74f, 0.1f, 0.1f));
                    design.Add(DesignPiece("mousepad", FitRole.OnTop, x - 0.3f, z, 0.74f, 0.1f, 0.1f));
                    design.Add(DesignPiece("computer", FitRole.OnTop, x, z, 0.74f, 0.25f, 0.15f));
                }
            }
            var plan = new FitPlan();
            FitPlanner.Plan(g, site, design, plan);
            Check(plan.Kept <= FitPlanner.MostPieces && plan.DesignCapped > 0, "fit: a design of " + design.Count + " pieces is held to " + FitPlanner.MostPieces + " (" + plan.Kept + ")");
            Check(FitCount(plan, "computer") > FitCount(plan, "calculator"), "fit: the calculators go before the computers (" + FitCount(plan, "calculator") + " and " + FitCount(plan, "computer") + ")");
        }

        // ---------------------------------------------------------------- a police station's back office, bare round its counter and cell: 16 x 24 m
        {
            RoomGrid g = FitFloor(16f, 24f);
            FitSite site = FitSiteAt(FitKind.CivicOffice, 8f, 0.35f, 0f, 1f);
            site.Fixed.Add(new FitRect(5.5f, 4.6f, 10.5f, 5.5f, true));     // the counter
            site.Fixed.Add(new FitRect(0.3f, 20f, 5f, 23.7f, true));        // the cell
            site.Fixed.Add(new FitRect(0.3f, 1.5f, 1f, 4f, true));          // the waiting bench
            site.KeepFree.Add(new FitRect(7.2f, 0.3f, 8.8f, 4.6f, false));  // the way to the counter
            site.Keep.Add(new FitSpot(8f, 4.1f, 0.4f));                     // in front of it
            site.Keep.Add(new FitSpot(8f, 6.2f, 0.4f));                     // the sergeant's side
            site.Keep.Add(new FitSpot(2.6f, 19.3f, 0.4f));                  // the cell's bars
            var plan = new FitPlan();
            var watch = Stopwatch.StartNew();
            FitPlanner.Plan(g, site, null, plan);
            slowest = Math.Max(slowest, watch.Elapsed.TotalMilliseconds);
            string at = "fit: a station's bare back office: ";
            FitKeeps(g, site, plan, at);
            Check(plan.Seats(FitUse.Desk) >= 4 && FitZones(plan, FitPlanner.ZoneType.Filing) >= 1, at + "desks and filing (" + plan.Seats(FitUse.Desk) + " desks)");
            Check(plan.BareAfter <= plan.FloorArea * 0.12f, at + "no floor left bare (" + plan.BareAfter.ToString("F0") + " of " + plan.FloorArea.ToString("F0") + " square metres)");
            Check(FitCount(plan, "rlo:closetrack") == 0 && FitCount(plan, "board01") == 0, at + "nothing of another kind of building");
        }

        // ---------------------------------------------------------------- a data centre's floor round its hall, with a drive-in bay: 30 x 24 m
        {
            RoomGrid g = FitFloor(30f, 24f);
            FitSite site = FitSiteAt(FitKind.DataCentre, 0.35f, 12f, 1f, 0f);
            site.Windows = false;
            site.Fixed.Add(new FitRect(9f, 3f, 27f, 21f, true));             // the server hall
            site.KeepFree.Add(new FitRect(8f, 2f, 28f, 22f, false));         // its walkway round
            site.Exits.Add(new FitSpot(29.6f, 12f, 4f));                     // the drive-in bay
            var plan = new FitPlan();
            FitPlanner.Plan(g, site, null, plan);
            string at = "fit: a data centre's floor round its hall: ";
            FitKeeps(g, site, plan, at);
            Check(FitZones(plan, FitPlanner.ZoneType.Stores) >= 1 && FitZones(plan, FitPlanner.ZoneType.Staging) >= 1 && FitCount(plan, "rlo:bench") >= 1, at + "stores, staging and a workbench (" + FitZones(plan, FitPlanner.ZoneType.Stores) + " and " + FitZones(plan, FitPlanner.ZoneType.Staging) + ")");
            Check(plan.BareAfter <= plan.FloorArea * 0.2f, at + "little floor left bare beside the way in (" + plan.BareAfter.ToString("F0") + " of " + plan.FloorArea.ToString("F0") + ")");
            Check(FitCount(plan, "officedesk1") <= 2 && FitCount(plan, "wallmountedtv") == 0, at + "a small staff desk at most, no meeting room");
        }

        // ---------------------------------------------------------------- the university: 24 x 30 m
        {
            RoomGrid g = FitFloor(24f, 30f);
            FitSite site = FitSiteAt(FitKind.Campus, 12f, 0.35f, 0f, 1f);
            var plan = new FitPlan();
            FitPlanner.Plan(g, site, null, plan);
            string at = "fit: the university: ";
            FitKeeps(g, site, plan, at);
            Check(FitZones(plan, FitPlanner.ZoneType.Lecture) >= 1 && FitCount(plan, "board01") >= 1, at + "a lecture room with its board (" + FitZones(plan, FitPlanner.ZoneType.Lecture) + ")");
            Check(FitZones(plan, FitPlanner.ZoneType.Library) >= 1, at + "a library");
            Check(plan.BareAfter <= plan.FloorArea * 0.12f, at + "no floor left bare (" + plan.BareAfter.ToString("F0") + " of " + plan.FloorArea.ToString("F0") + ")");
        }

        // ---------------------------------------------------------------- a room with a back room through a doorway, and a column: nothing closes either off
        {
            RoomGrid g = FitFloor(18f, 20f);
            FitWall(g, 0f, 13.7f, 7.5f, 14.3f);      // the back room's wall, a doorway of 1.4 m in it
            FitWall(g, 8.9f, 13.7f, 18f, 14.3f);
            FitWall(g, 12f, 6f, 12.9f, 6.9f);        // a column
            FitSite site = FitSiteAt(FitKind.TechOffice, 9f, 0.35f, 0f, 1f);
            var plan = new FitPlan();
            FitPlanner.Plan(g, site, null, plan);
            FitKeeps(g, site, plan, "fit: a room with a back room and a column: ");
            bool doorway = true;
            foreach (FitPiece p in plan.Pieces)
            {
                doorway &= !Standing(p) || !(p.CZ - p.HalfZ < 15f && p.CZ + p.HalfZ > 13f && p.CX + p.HalfX > 7.5f && p.CX - p.HalfX < 8.9f);
            }
            Check(doorway, "fit: nothing stands in the doorway to the back room");
        }

        // ---------------------------------------------------------------- a small office: nothing is forced in
        {
            RoomGrid g = FitFloor(4.5f, 4.5f);
            FitSite site = FitSiteAt(FitKind.TechOffice, 2.25f, 0.35f, 0f, 1f);
            var plan = new FitPlan();
            FitPlanner.Plan(g, site, null, plan);
            Check(plan.Why == null && plan.Zones == 0 && plan.Kept == 0, "fit: a room of 4.5 m square is left as it is (" + plan.Kept + " pieces)");
        }

        // ---------------------------------------------------------------- who is about, within the people to spare
        {
            FitCrew few = FitCrew.For(FitKind.TechOffice, 24, 6, 4, 0, 8, 3);
            Check(few.Total == 3 && few.AtDesks == 3, "fit: with three people to spare, all three at desks (" + few.AtDesks + ")");
            FitCrew none = FitCrew.For(FitKind.CivicOffice, 6, 0, 0, 2, 4, 0);
            Check(none.Total == 0, "fit: with nobody to spare, nobody");
            FitCrew campus = FitCrew.For(FitKind.Campus, 24, 0, 2, 0, 6, 12);
            Check(campus.AtDesks >= 4 && campus.Walking == 2 && campus.InLounge == 1, "fit: students at the desks, two walking, one in the lounge");
        }

        Check(slowest < 1500, "fit: a plan takes under a second and a half here (" + slowest.ToString("F0") + " ms at most); it is made once a visit, off the main thread");
    }

    private static FitPiece DesignPiece(string prefab, FitRole role, float x, float z, float y, float halfX, float halfZ, float yaw = 0f)
    {
        return new FitPiece { Prefab = prefab, Role = role, X = x, Z = z, Y = y, Yaw = yaw, CX = x, CZ = z, HalfX = halfX, HalfZ = halfZ, Anchor = -1, Height = y > 0.1f ? 0.4f : 0.8f };
    }

    // A design for a 21 x 29 m office, made the way the game's are: rows of desks with chairs and computers over the whole
    // floor (some where the lobby is), a meeting table with chairs, a kitchen run, and a washroom along the back wall behind
    // a partition whose doorway a cabinet stands in.
    private static List<FitPiece> FitDesign(float wide, float deep, out int desks)
    {
        var d = new List<FitPiece>();
        desks = 0;
        for (float z = 4f; z < deep - 7f; z += 4f)
        {
            for (float x = 2.5f; x < wide - 2f; x += 2f)
            {
                d.Add(DesignPiece("officedesk1", FitRole.Floor, x, z, 0f, 0.9f, 0.45f, 0f));
                d.Add(DesignPiece("officechair2", FitRole.Seat, x, z + 0.75f, 0f, 0.31f, 0.31f, 180f));
                d.Add(DesignPiece("computer", FitRole.OnTop, x, z, 0.74f, 0.25f, 0.15f));
                d.Add(DesignPiece("computermonitor", FitRole.OnTop, x - 0.6f, z, 0.74f, 0.2f, 0.1f, 45f));
                desks++;
            }
        }
        d.Add(DesignPiece("largemeetingtable", FitRole.Floor, 16f, deep - 5f, 0f, 0.6f, 1.5f));
        d.Add(DesignPiece("regularchair", FitRole.Seat, 15f, deep - 5f, 0f, 0.25f, 0.25f, 90f));
        d.Add(DesignPiece("regularchair", FitRole.Seat, 17f, deep - 5f, 0f, 0.25f, 0.25f, 270f));
        d.Add(DesignPiece("counter1", FitRole.Floor, 2f, deep - 3f, 0f, 0.5f, 0.33f, 180f));
        d.Add(DesignPiece("counter1", FitRole.Floor, 3f, deep - 3f, 0f, 0.5f, 0.33f, 180f));
        d.Add(DesignPiece("cheapcoffeemachine", FitRole.OnTop, 2f, deep - 3f, 0.88f, 0.18f, 0.18f, 180f));
        // The washroom: a partition across the back with a doorway at x 9..10.4, and a cabinet standing in that doorway.
        for (float x = 0.5f; x < 9f; x += 1f)
        {
            d.Add(DesignPiece("windowedroomdivider", FitRole.Floor, x + 0.5f, deep - 1.9f, 0f, 0.5f, 0.05f));
        }
        for (float x = 10.4f; x < 13.4f; x += 1f)
        {
            d.Add(DesignPiece("windowedroomdivider", FitRole.Floor, x + 0.5f, deep - 1.9f, 0f, 0.5f, 0.05f));
        }
        d.Add(DesignPiece("windowedroomdivider", FitRole.Floor, 13.95f, deep - 1.1f, 0f, 0.05f, 0.8f));
        d.Add(DesignPiece("toiletstall", FitRole.Floor, 3f, deep - 0.8f, 0f, 0.5f, 0.5f, 180f));
        d.Add(DesignPiece("sink", FitRole.Floor, 6f, deep - 0.6f, 0f, 0.3f, 0.25f, 180f));
        d.Add(DesignPiece("bigcabinet", FitRole.Floor, 9.7f, deep - 1.9f, 0f, 0.7f, 0.25f));
        d.Add(DesignPiece("ceilinglamptube", FitRole.Lamp, 10f, 14f, 3.5f, 0.6f, 0.1f));
        d.Add(DesignPiece("cashregister", FitRole.Floor, 18f, 8f, 0f, 0.3f, 0.3f));
        return d;
    }

    // --fit: each plan drawn in letters, a cell to a character (D desks, c chairs, T tables, K kitchen, S sofas and armchairs,
    // P printers, R the server closet, F filing and stores, W waiting seats, B shelves, p plants, # what was there, . floor, * the door).
    private static void DrawFits()
    {
        RoomGrid g = FitFloor(21f, 29f);
        FitSite site = FitSiteAt(FitKind.TechOffice, 10.5f, 0.35f, 0f, 1f);
        FitLobby(g, site, new Lobby());
        var plan = new FitPlan();
        FitPlanner.Plan(g, site, null, plan);
        DrawFit(g, site, plan, "a bare headquarters of 21 x 29 m: " + plan.Kept + " pieces, " + plan.Zones + " rooms");
        var withDesign = new FitPlan();
        FitPlanner.Plan(g, site, FitDesign(21f, 29f, out _), withDesign);
        DrawFit(g, site, withDesign, "the same round a design: " + withDesign.KeptDesign + " of the design's pieces, " + withDesign.Zones + " rooms of the plan's");
        RoomGrid u = FitFloor(24f, 30f);
        FitSite campus = FitSiteAt(FitKind.Campus, 12f, 0.35f, 0f, 1f);
        var lectures = new FitPlan();
        FitPlanner.Plan(u, campus, null, lectures);
        DrawFit(u, campus, lectures, "the university of 24 x 30 m: " + lectures.Zones + " rooms");
        RoomGrid dc = FitFloor(30f, 24f);
        FitSite centre = FitSiteAt(FitKind.DataCentre, 0.35f, 12f, 1f, 0f);
        centre.Windows = false;
        centre.Fixed.Add(new FitRect(9f, 3f, 27f, 21f, true));
        centre.KeepFree.Add(new FitRect(8f, 2f, 28f, 22f, false));
        centre.Exits.Add(new FitSpot(29.6f, 12f, 4f));
        var floor = new FitPlan();
        FitPlanner.Plan(dc, centre, null, floor);
        DrawFit(dc, centre, floor, "a data centre's floor of 30 x 24 m: " + floor.Zones + " rooms");
    }

    private static void DrawFit(RoomGrid g, FitSite site, FitPlan plan, string title)
    {
        Console.WriteLine(title);
        for (int r = g.Rows - 1; r >= 0; r--)
        {
            var line = new System.Text.StringBuilder(g.Cols);
            for (int c = 0; c < g.Cols; c++)
            {
                g.Centre(c, r, out float x, out float z);
                char ch = g[c, r] == RoomGrid.Open ? '.' : ' ';
                foreach (FitRect f in site.Fixed)
                {
                    if (f.Solid && f.Distance(x, z) <= 0f)
                    {
                        ch = '#';
                    }
                }
                foreach (FitPiece p in plan.Pieces)
                {
                    if (Standing(p) && p.Contains(x, z, 0f))
                    {
                        ch = FitLetter(p);
                    }
                }
                if (ch == '.' && FitFar(site, plan, x, z))
                {
                    ch = ',';
                }
                if (Math.Abs(x - site.DoorX) < 0.16f && Math.Abs(z - site.DoorZ) < 0.16f)
                {
                    ch = '*';
                }
                line.Append(ch);
            }
            Console.WriteLine("  " + line);
        }
    }

    // Floor further than the plan's bare reach from anything that stands (drawn ',').
    private static bool FitFar(FitSite site, FitPlan plan, float x, float z)
    {
        foreach (FitRect f in site.Fixed)
        {
            if (f.Distance(x, z) <= FitPlanner.BareReach)
            {
                return false;
            }
        }
        foreach (FitPiece p in plan.Pieces)
        {
            if (p.Kept && (p.Role == FitRole.Floor || p.Role == FitRole.Seat || p.Role == FitRole.Flat)
                && new FitRect(p.CX - p.HalfX, p.CZ - p.HalfZ, p.CX + p.HalfX, p.CZ + p.HalfZ, true).Distance(x, z) <= FitPlanner.BareReach)
            {
                return false;
            }
        }
        return true;
    }

    private static char FitLetter(FitPiece p)
    {
        string id = p.Prefab ?? string.Empty;
        string[] parts = { "desk", "table", "counter", "fridge", "cooler", "sofa", "armchair", "printer", "bin", "closetrack", "waiting", "bookshelf", "plant", "cabinet", "locker", "shelf", "bench", "pallet" };
        const string letters = "DTKKKSSPPRWBpFFFFF";
        if (p.Role == FitRole.Seat)
        {
            return 'c';
        }
        for (int i = 0; i < parts.Length; i++)
        {
            if (id.Contains(parts[i]))
            {
                return letters[i];
            }
        }
        return '?';
    }
}
