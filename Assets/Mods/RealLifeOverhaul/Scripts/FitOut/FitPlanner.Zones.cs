using System;
using System.Collections.Generic;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul.FitOut
{
    /// <summary>
    /// The fit-out planner's rooms for bare floor, each in a box with its back on a wall where it has one, every piece by
    /// the game's own figure for its size: desks, meeting, kitchen, lounge, printer, server, filing, waiting, stores,
    /// staging and plants, and for a working day's desk one preset room (an office's row, nurses, teacher, service, back
    /// desk). Each kind of building gets its own rooms in turn (TechOrder and the rest), up to so many of each.
    /// </summary>
    public static partial class FitPlanner
    {
        /// <summary>The kinds of room the plan lays out.</summary>
        public enum ZoneType { Desks, Meeting, Kitchen, Lounge, Printer, Server, Filing, Waiting, Stores, Staging, Plants, Nurses, Teacher, Service, BackDesk }

        public const int ZoneTypeCount = 15;          // the members of ZoneType
        private const float ChairOut = 0.75f;         // the game seats somebody this far out from a desk's pivot, along its front

        // The rooms each kind of building is given, in turn, and how many of each at most (by ZoneType).
        private static readonly ZoneType[] TechOrder =
        {
            ZoneType.Desks, ZoneType.Meeting, ZoneType.Kitchen, ZoneType.Desks, ZoneType.Lounge, ZoneType.Server, ZoneType.Printer, ZoneType.Desks,
            ZoneType.Meeting, ZoneType.Lounge, ZoneType.Printer
        };
        private static readonly int[] TechMost = { 99, 3, 1, 2, 2, 1, 0, 0, 0, 0, 99, 0, 0, 0, 0 };
        private static readonly ZoneType[] CivicOrder = { ZoneType.Desks, ZoneType.Filing, ZoneType.Meeting, ZoneType.Kitchen, ZoneType.Desks, ZoneType.Printer, ZoneType.Waiting, ZoneType.Filing };
        private static readonly int[] CivicMost = { 99, 2, 1, 0, 1, 0, 3, 1, 0, 0, 99, 0, 0, 0, 0 };
        private static readonly ZoneType[] DataOrder = { ZoneType.Stores, ZoneType.Staging, ZoneType.Desks, ZoneType.Kitchen, ZoneType.Stores, ZoneType.Staging, ZoneType.Filing };
        private static readonly int[] DataMost = { 1, 0, 1, 0, 0, 0, 2, 0, 4, 3, 99, 0, 0, 0, 0 };

        // A working day's desk: one room of its own kind, no plants, nothing else.
        private static readonly ZoneType[] RowOrder = { ZoneType.Desks };
        private static readonly int[] RowMost = { 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
        private static readonly ZoneType[] WardOrder = { ZoneType.Nurses };
        private static readonly int[] WardMost = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0 };
        private static readonly ZoneType[] ClassOrder = { ZoneType.Teacher };
        private static readonly int[] ClassMost = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0 };
        private static readonly ZoneType[] ServiceOrder = { ZoneType.Service };
        private static readonly int[] ServiceMost = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0 };
        private static readonly ZoneType[] BackOrder = { ZoneType.BackDesk };
        private static readonly int[] BackMost = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1 };
        public const int RowDesks = 4;                // desks of an office's row at most, the player's among them
        private static readonly string[] WardCabinets = { "smallcabinet", "bigcabinet" };
        private static readonly string[] ClassCabinets = { "smallcabinet" };
        private static readonly string[] BackCabinets = { "smallcabinet", "recyclingbinpaper" };
        private static readonly string[] ServiceFiling = { "officelockers", "bigcabinet" };

        private static readonly string[] PrinterRun = { "printer", "smallcabinet", "recyclingbinpaper" };
        private static readonly string[] ServerRun = { "rlo:closetrack", "rlo:closetrack", "smallcabinet" };
        private static readonly string[] FilingRun = { "officelockers", "bigcabinet", "storageshelf", "smallcabinet", "bigcabinet" };
        private static readonly string[] DataFilingRun = { "officelockers", "officelockers", "metalshelf", "bigcabinet" };
        private static readonly string[] PalletKinds = { "decorativepalletwithboxes01", "decorativepalletwithboxes02", "decorativepalletwithboxes03" };
        private static readonly string[] PlantKinds = { "groundplant03", "groundplant01", "groundplant05", "groundplant02" };

        // What the game side makes ready before the plan, for each kind of building.
        private static readonly string[] TechPieces =
        {
            "officedesk1", "officechair2", "computer", "computermonitor", "deskplant01", "largemeetingtable", "table1", "regularchair", "wallmountedtv",
            "counter1", "cheapcoffeemachine", "standardfridge", "watercooler", "sofaluxury", "coffeetable1", "armchair1", "printer", "smallcabinet",
            "recyclingbinpaper", "rlo:closetrack", "groundplant01", "groundplant02", "groundplant03", "groundplant04", "groundplant05", "ceilinglampsquare"
        };

        private static readonly string[] CivicPieces =
        {
            "officedesk1", "officechair", "computer", "computermonitor", "deskplant01", "largemeetingtable", "table1", "regularchair", "wallmountedtv",
            "counter1", "cheapcoffeemachine", "standardfridge", "watercooler", "printer", "smallcabinet", "recyclingbinpaper", "officelockers", "bigcabinet",
            "storageshelf", "waitingroomchairs", "groundplant01", "groundplant02", "groundplant03", "groundplant05", "ceilinglampsquare"
        };

        private static readonly string[] DataPieces =
        {
            "officedesk1", "officechair", "computer", "computermonitor", "deskplant01", "table1", "regularchair", "counter1", "cheapcoffeemachine",
            "standardfridge", "watercooler", "officelockers", "metalshelf", "bigcabinet", "storageshelf", "rlo:bench", "decorativepalletwithboxes01",
            "decorativepalletwithboxes02", "decorativepalletwithboxes03", "trashbin", "groundplant01", "groundplant02", "groundplant03", "groundplant05",
            "ceilinglampwarehouse"
        };

        // Each preset's: the game's own furniture only.
        private static readonly string[] RowPieces = { "officedesk1", "officechair2", "computer", "computermonitor", "deskplant01" };
        private static readonly string[] WardPieces = { "officedesk1", "officechair", "computer", "computermonitor", "deskplant01", "smallcabinet", "bigcabinet" };
        private static readonly string[] ClassPieces = { "officedesk1", "officechair", "computer", "computermonitor", "deskplant01", "smallcabinet" };
        private static readonly string[] ServicePieces = { "officedesk1", "officechair", "regularchair", "computer", "computermonitor", "deskplant01", "officelockers", "bigcabinet" };
        private static readonly string[] BackPieces = { "officedesk1", "officechair", "computer", "computermonitor", "deskplant01", "smallcabinet", "recyclingbinpaper" };

        /// <summary>Every piece the rooms of a kind of building may lay out, the game's and the mod's.</summary>
        public static IReadOnlyList<string> Pieces(FitKind kind)
        {
            switch (kind)
            {
                case FitKind.CivicOffice: return CivicPieces;
                case FitKind.DataCentre: return DataPieces;
                case FitKind.OfficeRow: return RowPieces;
                case FitKind.Ward: return WardPieces;
                case FitKind.Classroom: return ClassPieces;
                case FitKind.ServiceDesk: return ServicePieces;
                case FitKind.BackOffice: return BackPieces;
                default: return TechPieces;
            }
        }

        /// <summary>The player's desk among so many desks of a fitted floor, picked by a seed; -1 for none (FitDesks).</summary>
        public static int PickDesk(int seats, int seed) => seats <= 0 ? -1 : TechRandom.Range(seed, DeskSalt, seats, 0, 0, seats - 1);

        private const int DeskSalt = 9211;   // the seed's salt for the desk picked

        /// <summary>True for the kinds that are a preset of a working day's desk: one room of their own, not a whole floor.</summary>
        public static bool IsPreset(FitKind kind) => kind >= FitKind.OfficeRow;

        private static ZoneType[] Order(FitKind kind, out int[] most)
        {
            switch (kind)
            {
                case FitKind.CivicOffice: most = CivicMost; return CivicOrder;
                case FitKind.DataCentre: most = DataMost; return DataOrder;
                case FitKind.OfficeRow: most = RowMost; return RowOrder;
                case FitKind.Ward: most = WardMost; return WardOrder;
                case FitKind.Classroom: most = ClassMost; return ClassOrder;
                case FitKind.ServiceDesk: most = ServiceMost; return ServiceOrder;
                case FitKind.BackOffice: most = BackMost; return BackOrder;
                default: most = TechMost; return TechOrder;
            }
        }

        // How much of a box each kind of room takes at most, along its wall and out from it; the rest is left for the next.
        private static void ZoneSize(Work w, ZoneType type, out float u, out float v)
        {
            FitSite s = w.Site;
            FitSize desk = s.Size("officedesk1"), chair = s.Size("officechair2"), table = s.Size("table1"), seat = s.Size("regularchair");
            float behind = ChairOut + chair.D * 0.5f + 0.35f, pair = desk.D + 2f * behind;
            float deskW = Math.Max(0.9f, desk.W);
            switch (type)
            {
                case ZoneType.Desks when IsPreset(s.Kind):
                    u = RowDesks * deskW + 0.4f;
                    v = desk.D + behind + 0.5f;
                    return;
                case ZoneType.Nurses:
                    u = 2f * deskW + s.Size("smallcabinet").W + s.Size("bigcabinet").W + 0.8f;
                    v = desk.D + behind + 0.5f;
                    return;
                case ZoneType.Teacher:
                case ZoneType.BackDesk:
                    u = deskW + s.Size("smallcabinet").W + s.Size("recyclingbinpaper").W + 0.8f;
                    v = desk.D + behind + 0.5f;
                    return;
                case ZoneType.Service:
                    u = 2f * deskW + s.Size("officelockers").W + s.Size("bigcabinet").W + 1.0f;
                    v = behind + desk.D + seat.D + 0.9f;
                    return;
                case ZoneType.Desks:
                    u = 5f * Math.Max(0.9f, desk.W) + 0.4f;
                    v = desk.D + behind + 0.6f + pair + 0.4f;
                    return;
                case ZoneType.Meeting:
                    FitSize big = s.Size("largemeetingtable");
                    u = big.W + 2f * (seat.D + 0.55f) + 0.6f;
                    v = 1.3f + Math.Min(big.D, 4f) + seat.D + 1.2f;
                    return;
                case ZoneType.Kitchen:
                    u = 4f * s.Size("counter1").W + s.Size("standardfridge").W + 1.0f;
                    v = s.Size("counter1").D + 1.1f + 2f * seat.D + table.D + 0.8f;
                    return;
                case ZoneType.Lounge:
                    u = s.Size("sofaluxury").W + 1.6f;
                    v = s.Size("sofaluxury").D + 0.45f + s.Size("coffeetable1").D + 0.5f + s.Size("armchair1").D + 0.9f;
                    return;
                case ZoneType.Printer:
                case ZoneType.Server:
                    u = 3.0f;
                    v = 2.2f;
                    return;
                case ZoneType.Filing:
                case ZoneType.Waiting:
                    u = 6.0f;
                    v = 2.4f;
                    return;
                case ZoneType.Stores:
                    u = 3f * (s.Size("storageshelf").W + 0.15f) + s.Size("rlo:bench").W + 0.6f;
                    v = s.Size("storageshelf").D + 1.4f + s.Size("decorativepalletwithboxes01").D + 0.6f;
                    return;
                default:
                    u = v = 99f;
                    return;
            }
        }

        // False if the room does not fit; the caller takes up whatever it put down.
        private static bool Lay(Work w, ZoneType type, Box b, int zone)
        {
            switch (type)
            {
                case ZoneType.Desks: return Desks(w, b, zone);
                case ZoneType.Meeting: return MeetingRoom(w, b, zone);
                case ZoneType.Kitchen: return Kitchen(w, b, zone);
                case ZoneType.Lounge: return Lounge(w, b, zone);
                case ZoneType.Printer: return WallRun(w, b, zone, PrinterRun, 2, false, FitUse.Stop);   // free-standing where there is no wall
                case ZoneType.Server: return WallRun(w, b, zone, ServerRun, 2, true, FitUse.Stop);
                case ZoneType.Filing: return WallRun(w, b, zone, w.Site.Kind == FitKind.DataCentre ? DataFilingRun : FilingRun, 3, true, FitUse.None);
                case ZoneType.Waiting: return Waiting(w, b, zone);
                case ZoneType.Stores: return Stores(w, b, zone);
                case ZoneType.Staging: return Staging(w, b, zone);
                case ZoneType.Nurses: return DeskRun(w, b, zone, 2, WardCabinets);
                case ZoneType.Teacher: return DeskRun(w, b, zone, 1, ClassCabinets);
                case ZoneType.Service: return Service(w, b, zone);
                case ZoneType.BackDesk: return DeskRun(w, b, zone, 1, BackCabinets);
                default: return Plants(w, b, zone, true);
            }
        }

        // Open-plan desks: pairs of rows back to back, each desk with its chair, a computer and a monitor on it.
        private static bool Desks(Work w, Box b, int zone)
        {
            FitKind kind = w.Site.Kind;
            const string desk = "officedesk1";
            string chair = kind == FitKind.TechOffice || kind == FitKind.OfficeRow ? "officechair2" : "officechair";
            FitSize d = w.Site.Size(desk), c = w.Site.Size(chair);
            float deskW = Math.Max(0.9f, d.W), deskD = Math.Max(0.5f, d.D);
            float behind = ChairOut + c.D * 0.5f + 0.35f;              // from a desk's pivot to the back of the room its sitter needs
            float pair = deskD + 2f * behind;                          // two rows back to back with their chairs
            int most = kind == FitKind.OfficeRow ? RowDesks : kind == FitKind.DataCentre ? 2 : kind == FitKind.CivicOffice ? 8 : 12;
            int perRow = Math.Min(5, (int)((b.LU - 0.2f) / deskW));
            if (perRow < 1)
            {
                return false;
            }
            float u0 = (b.LU - perRow * deskW) * 0.5f + deskW * 0.5f;
            int placed = 0;
            float v = 0.1f;
            bool backed = false;
            float row = deskD + behind + 0.6f;                         // a row with its back to the wall, and the walkway behind its chairs
            bool backFirst = b.WallV0 && !(b.LV >= pair + 0.2f && b.LV < row + pair + 0.1f);   // unless a pair of rows fills the room better
            if (backFirst)
            {
                // one row with its back to the wall, sitters facing it, before the pairs in the middle
                float vA = b.WallBack + deskD * 0.5f + 0.02f;
                if (vA + deskD * 0.5f + behind <= b.LV - 0.1f)
                {
                    for (int k = 0; k < perRow && placed < most; k++)
                    {
                        placed += Workstation(w, b, zone, desk, chair, u0 + k * deskW, vA, 0f, -1f) ? 1 : 0;
                    }
                    v = vA + deskD * 0.5f + behind + 0.6f;
                    backed = true;
                }
            }
            while (placed < most && v + pair <= b.LV - 0.1f)
            {
                float vA = v + behind, vB = vA + deskD;
                for (int k = 0; k < perRow && placed < most; k++)
                {
                    placed += Workstation(w, b, zone, desk, chair, u0 + k * deskW, vA, 0f, 1f) ? 1 : 0;
                    if (placed < most)
                    {
                        placed += Workstation(w, b, zone, desk, chair, u0 + k * deskW, vB, 0f, -1f) ? 1 : 0;
                    }
                }
                v += pair + 0.6f;
            }
            if (placed < most && !backed && v + deskD + behind <= b.LV - 0.1f)
            {
                // room for one more row: its sitters with their backs to the room
                for (int k = 0; k < perRow && placed < most; k++)
                {
                    placed += Workstation(w, b, zone, desk, chair, u0 + k * deskW, v + behind, 0f, 1f) ? 1 : 0;
                }
            }
            if (placed == 0)
            {
                return false;
            }
            if (!IsPreset(kind))
            {
                Plants(w, b, zone, false);
            }
            return true;
        }

        // A desk whose sitter faces (fu, fv), with its chair, a computer and a monitor; false if the desk could not go there.
        private static bool Workstation(Work w, Box b, int zone, string desk, string chair, float u, float v, float fu, float fv)
        {
            // the desk's front looks back at its sitter; the chair stands out along it, facing the desk
            int deskAt = w.Put(b, desk, FitRole.Floor, FitUse.None, u, v, -fu, -fv, -1, zone, centred: false);
            if (deskAt < 0)
            {
                return false;
            }
            FitPiece d = w.Into.Pieces[deskAt];
            w.Put(b, chair, FitRole.Seat, FitUse.Desk, u - fu * ChairOut, v - fv * ChairOut, fu, fv, deskAt, zone, centred: false);
            FitSize s = w.Site.Size(desk);
            w.OnTop(deskAt, "computer", 0f, 0.05f, 0f, s.Top);
            w.OnTop(deskAt, "computermonitor", -s.W * 0.36f, 0.05f, 45f, s.Top);
            if (((int)Math.Floor(d.X * 7f + d.Z * 3f) & 3) == 0)
            {
                w.OnTop(deskAt, "deskplant01", s.W * 0.38f, -s.D * 0.2f, 0f, s.Top);
            }
            return true;
        }

        // A meeting room: a table pointing at a screen on the wall behind it, chairs down both sides and one at the end.
        private static bool MeetingRoom(Work w, Box b, int zone)
        {
            string tableId = "largemeetingtable";
            FitSize t = w.Site.Size(tableId);
            FitSize c = w.Site.Size("regularchair");
            float side = c.D + 0.55f;                        // a chair and room to pull it out, either side of the table
            bool turned = false;
            if (b.LU < t.W + 2f * side || b.LV < Math.Min(t.D, 4f) + 2.2f)
            {
                tableId = "table1";
                FitSize small = w.Site.Size(tableId);
                // turned so its long side runs out from the wall: the room's u is then its own z
                t = new FitSize(small.D, small.W, small.H, small.CZ, small.CX, small.Top);
                turned = true;
                if (b.LU < t.W + 2f * side || b.LV < t.D + 2.2f)
                {
                    return false;
                }
            }
            float along = Math.Min(t.D, 4f), across = t.W;
            float vMid = b.WallV0 ? 1.3f + along * 0.5f : b.LV * 0.5f, uMid = b.LU * 0.5f;
            int tableAt = w.Put(b, tableId, FitRole.Floor, FitUse.None, uMid, vMid, turned ? 1f : 0f, turned ? 0f : 1f, -1, zone, centred: true);
            if (tableAt < 0)
            {
                return false;
            }
            int perSide = Math.Max(1, Math.Min(4, (int)((along - 0.1f) / 0.75f)));
            for (int k = 0; k < perSide; k++)
            {
                float v = vMid - along * 0.5f + along * (k + 0.5f) / perSide;
                w.Put(b, "regularchair", FitRole.Seat, FitUse.Meeting, uMid - across * 0.5f - c.D * 0.5f - 0.1f, v, 1f, 0f, tableAt, zone, centred: true);
                w.Put(b, "regularchair", FitRole.Seat, FitUse.Meeting, uMid + across * 0.5f + c.D * 0.5f + 0.1f, v, -1f, 0f, tableAt, zone, centred: true);
            }
            if (vMid + along * 0.5f + c.D + 0.9f <= b.LV)
            {
                w.Put(b, "regularchair", FitRole.Seat, FitUse.Meeting, uMid, vMid + along * 0.5f + c.D * 0.5f + 0.1f, 0f, -1f, tableAt, zone, centred: true);
            }
            if (b.WallV0)
            {
                w.Hang(b, "wallmountedtv", uMid, 1.8f, zone);
            }
            Plants(w, b, zone, false);
            return true;
        }

        // Counters with the coffee machine on the first, the fridge at the end, a water cooler, a table and chairs if room.
        private static bool Kitchen(Work w, Box b, int zone)
        {
            if (!b.WallV0 || b.LU < 2.4f)
            {
                return false;
            }
            FitSize counter = w.Site.Size("counter1"), fridge = w.Site.Size("standardfridge");
            float counterW = Math.Max(0.4f, counter.W);
            bool tall = !b.FacadeV0;
            int count = Math.Min(4, (int)((b.LU - 0.2f - (tall ? fridge.W + 0.05f : 0f)) / counterW));
            if (count < 2)
            {
                return false;
            }
            float u = 0.1f + counterW * 0.5f;
            int firstCounter = -1;
            for (int k = 0; k < count; k++)
            {
                int at = w.AgainstWall(b, "counter1", u, FitUse.None, zone);
                firstCounter = firstCounter < 0 ? at : firstCounter;
                u += counterW;
            }
            if (firstCounter < 0)
            {
                return false;
            }
            w.OnTop(firstCounter, "cheapcoffeemachine", 0f, 0f, 0f, counter.Top, FitUse.Stop);
            float end = u - counterW * 0.5f;
            if (tall && w.AgainstWall(b, "standardfridge", end + 0.05f + fridge.W * 0.5f, FitUse.Stop, zone) >= 0)
            {
                end += 0.05f + fridge.W;
            }
            FitSize table = w.Site.Size("table1"), chair = w.Site.Size("regularchair");
            float vTable = b.WallBack + counter.D + 1.1f + chair.D + table.D * 0.5f;
            if (vTable + table.D * 0.5f + chair.D + 0.5f <= b.LV && b.LU >= table.W + 0.4f)
            {
                float uTable = b.LU * 0.5f;
                int t = w.Put(b, "table1", FitRole.Floor, FitUse.None, uTable, vTable, 0f, 1f, -1, zone, centred: true);
                if (t >= 0)
                {
                    w.Put(b, "regularchair", FitRole.Seat, FitUse.Lounge, uTable, vTable - table.D * 0.5f - chair.D * 0.5f - 0.05f, 0f, 1f, t, zone, centred: true);
                    w.Put(b, "regularchair", FitRole.Seat, FitUse.Lounge, uTable, vTable + table.D * 0.5f + chair.D * 0.5f + 0.05f, 0f, -1f, t, zone, centred: true);
                }
            }
            if (!b.FacadeV0 && end + 0.6f <= b.LU - 0.1f)
            {
                w.AgainstWall(b, "watercooler", end + 0.35f, FitUse.Stop, zone);
            }
            return true;
        }

        // A lounge: a sofa against the wall, a low table in front, two armchairs facing the sofa, a plant at its end.
        private static bool Lounge(Work w, Box b, int zone)
        {
            if (!b.WallV0)
            {
                return false;
            }
            FitSize sofa = w.Site.Size("sofaluxury"), table = w.Site.Size("coffeetable1"), arm = w.Site.Size("armchair1");
            if (b.LU < sofa.W + 0.4f || b.LV < sofa.D + table.D + arm.D + 1.6f)
            {
                return false;
            }
            float uMid = b.LU * 0.5f;
            if (w.AgainstWall(b, "sofaluxury", uMid, FitUse.Lounge, zone) < 0)
            {
                return false;
            }
            float vTable = b.WallBack + sofa.D + 0.45f + table.D * 0.5f;
            w.Put(b, "coffeetable1", FitRole.Floor, FitUse.None, uMid, vTable, 0f, 1f, -1, zone, centred: true);
            float vArm = vTable + table.D * 0.5f + 0.5f + arm.D * 0.5f;
            float gap = Math.Max(arm.W * 0.5f + 0.05f, Math.Min(sofa.W * 0.5f - arm.W * 0.5f, 0.6f));
            w.Put(b, "armchair1", FitRole.Floor, FitUse.Lounge, uMid - gap, vArm, 0f, -1f, -1, zone, centred: true);
            w.Put(b, "armchair1", FitRole.Floor, FitUse.Lounge, uMid + gap, vArm, 0f, -1f, -1, zone, centred: true);
            if (uMid + sofa.W * 0.5f + 0.75f <= b.LU)
            {
                w.AgainstWall(b, "groundplant04", uMid + sofa.W * 0.5f + 0.45f, FitUse.None, zone);
            }
            return true;
        }

        // A run of pieces backing onto the wall, a little apart.
        private static bool WallRun(Work w, Box b, int zone, string[] run, int least, bool tall, FitUse use)
        {
            if ((tall && !b.WallV0) || (tall && b.FacadeV0))
            {
                return false;   // what is tall stands against a wall; a printer corner may stand free
            }
            float u = 0.15f;
            int placed = 0;
            for (int k = 0; k < run.Length; k++)
            {
                FitSize s = w.Site.Size(run[k]);
                if (u + s.W > b.LU - 0.1f)
                {
                    break;
                }
                placed += w.AgainstWall(b, run[k], u + s.W * 0.5f, k == 0 ? use : FitUse.None, zone) >= 0 ? 1 : 0;
                u += s.W + 0.1f;
            }
            if (placed < least)
            {
                return false;
            }
            Plants(w, b, zone, false);
            return true;
        }

        // Waiting seats against a wall, a water cooler beside them and a plant.
        private static bool Waiting(Work w, Box b, int zone)
        {
            if (!b.WallV0)
            {
                return false;
            }
            FitSize seats = w.Site.Size("waitingroomchairs");
            int count = Math.Min(2, (int)((b.LU - 1.2f) / (seats.W + 0.2f)));
            if (count < 1)
            {
                return false;
            }
            float u = 0.2f + seats.W * 0.5f;
            int placed = 0;
            for (int k = 0; k < count; k++)
            {
                placed += w.AgainstWall(b, "waitingroomchairs", u, FitUse.Waiting, zone) >= 0 ? 1 : 0;
                u += seats.W + 0.2f;
            }
            if (placed == 0)
            {
                return false;
            }
            if (!b.FacadeV0)
            {
                w.AgainstWall(b, "watercooler", u - seats.W * 0.5f + 0.4f, FitUse.Stop, zone);
            }
            Plants(w, b, zone, false);
            return true;
        }

        // A data centre's stores: shelving along the wall, a workbench, and a pallet of boxes.
        private static bool Stores(Work w, Box b, int zone)
        {
            if (!b.WallV0 || b.FacadeV0)
            {
                return false;
            }
            FitSize shelf = w.Site.Size("storageshelf"), bench = w.Site.Size("rlo:bench"), pallet = w.Site.Size("decorativepalletwithboxes01");
            float u = 0.15f;
            int placed = 0;
            for (int k = 0; k < 3 && u + shelf.W <= b.LU - 0.1f; k++)
            {
                placed += w.AgainstWall(b, "storageshelf", u + shelf.W * 0.5f, FitUse.None, zone) >= 0 ? 1 : 0;
                u += shelf.W + 0.15f;
            }
            if (u + bench.W <= b.LU - 0.1f)
            {
                placed += w.AgainstWall(b, "rlo:bench", u + bench.W * 0.5f, FitUse.Stop, zone) >= 0 ? 1 : 0;
            }
            if (b.LV >= b.WallBack + shelf.D + 1.4f + pallet.D + 0.2f && b.LU >= pallet.W + 0.4f)
            {
                w.Put(b, "decorativepalletwithboxes01", FitRole.Floor, FitUse.None, 0.2f + pallet.W * 0.5f, b.WallBack + shelf.D + 1.4f + pallet.D * 0.5f, 0f, 1f, -1, zone, centred: true);
            }
            return placed >= 2;
        }

        // A data centre's staging floor, free-standing: pallets waiting to be racked, a workbench and a bin, a walkway apart.
        private static bool Staging(Work w, Box b, int zone)
        {
            FitSize pallet = w.Site.Size(PalletKinds[0]), bench = w.Site.Size("rlo:bench");
            if (b.LU < pallet.W + bench.W + 1.4f || b.LV < pallet.D + 0.6f)
            {
                return false;
            }
            float v0 = (b.WallV0 ? b.WallBack : 0.2f);
            int placed = w.Put(b, "rlo:bench", FitRole.Floor, FitUse.Stop, 0.2f + bench.W * 0.5f, v0 + bench.D * 0.5f + 0.05f, 0f, 1f, -1, zone, centred: true) >= 0 ? 1 : 0;
            float u = 0.2f + bench.W + 1.1f;
            for (int k = 0; k < 2 && u + pallet.W <= b.LU - 0.1f; k++)
            {
                for (int row = 0; row < 2; row++)
                {
                    float v = v0 + 0.1f + pallet.D * 0.5f + row * (pallet.D + 1.0f);
                    if (v + pallet.D * 0.5f > b.LV - 0.1f)
                    {
                        break;
                    }
                    placed += w.Put(b, PalletKinds[(k * 2 + row + w.Site.Seed) % PalletKinds.Length], FitRole.Floor, FitUse.None, u + pallet.W * 0.5f, v, 0f, 1f, -1, zone, centred: true) >= 0 ? 1 : 0;
                }
                u += pallet.W + 1.0f;
            }
            FitSize bin = w.Site.Size("trashbin");
            if (b.LV >= v0 + bench.D + 1.0f + bin.D)
            {
                w.Put(b, "trashbin", FitRole.Floor, FitUse.None, 0.2f + bin.W * 0.5f, v0 + bench.D + 0.9f + bin.D * 0.5f, 0f, 1f, -1, zone, centred: true);
            }
            return placed >= 2;
        }

        // Desks side by side, sitters with their backs to the wall facing the room, then the cabinets beside them (the tall
        // ones only against a wall). False if not one desk has room.
        private static bool DeskRun(Work w, Box b, int zone, int desks, string[] cabinets)
        {
            const string desk = "officedesk1", chair = "officechair";
            FitSize d = w.Site.Size(desk), c = w.Site.Size(chair);
            float deskW = Math.Max(0.9f, d.W), deskD = Math.Max(0.5f, d.D);
            float back = b.WallV0 ? b.WallBack : 0.2f;
            float behind = ChairOut + c.D * 0.5f + 0.35f;
            // the chair between the wall and the desk
            float vDesk = back + behind + deskD * 0.5f;
            if (vDesk + deskD * 0.5f > b.LV - 0.2f)
            {
                return false;
            }
            float u = 0.15f;
            int placed = 0;
            for (int k = 0; k < desks && u + deskW <= b.LU - 0.1f; k++)
            {
                placed += Workstation(w, b, zone, desk, chair, u + deskW * 0.5f, vDesk, 0f, 1f) ? 1 : 0;
                u += deskW + 0.05f;
            }
            if (placed == 0)
            {
                return false;
            }
            u += 0.25f;
            for (int k = 0; k < cabinets.Length; k++)
            {
                FitSize s = w.Site.Size(cabinets[k]);
                if (u + s.W > b.LU - 0.1f || (s.H > 1.3f && (!b.WallV0 || b.FacadeV0)))
                {
                    continue;
                }
                if (w.AgainstWall(b, cabinets[k], u + s.W * 0.5f, FitUse.None, zone) >= 0)
                {
                    u += s.W + 0.1f;
                }
            }
            return true;
        }

        // Service desks, each clerk's back to the wall and a visitor's chair across the desk, then the filing along the wall.
        // False if not one desk has room.
        private static bool Service(Work w, Box b, int zone)
        {
            const string desk = "officedesk1", chair = "officechair", visitor = "regularchair";
            FitSize d = w.Site.Size(desk), c = w.Site.Size(chair), s = w.Site.Size(visitor);
            float deskW = Math.Max(0.9f, d.W), deskD = Math.Max(0.5f, d.D);
            float back = b.WallV0 ? b.WallBack : 0.2f;
            float behind = ChairOut + c.D * 0.5f + 0.35f;
            float vDesk = back + behind + deskD * 0.5f;
            float vVisitor = vDesk + deskD * 0.5f + s.D * 0.5f + 0.15f;
            if (vVisitor + s.D * 0.5f + 0.3f > b.LV)
            {
                return false;
            }
            float u = 0.15f;
            int placed = 0;
            for (int k = 0; k < 2 && u + deskW <= b.LU - 0.1f; k++)
            {
                float uDesk = u + deskW * 0.5f;
                int before = w.Into.Pieces.Count;
                if (Workstation(w, b, zone, desk, chair, uDesk, vDesk, 0f, 1f))
                {
                    placed++;
                    w.Put(b, visitor, FitRole.Seat, FitUse.Waiting, uDesk, vVisitor, 0f, -1f, before, zone, centred: true);
                }
                u += deskW + 0.6f;
            }
            if (placed == 0)
            {
                return false;
            }
            for (int k = 0; k < ServiceFiling.Length && b.WallV0 && !b.FacadeV0; k++)
            {
                FitSize f = w.Site.Size(ServiceFiling[k]);
                if (u + f.W > b.LU - 0.1f)
                {
                    break;
                }
                if (w.AgainstWall(b, ServiceFiling[k], u + f.W * 0.5f, FitUse.None, zone) >= 0)
                {
                    u += f.W + 0.1f;
                }
            }
            return true;
        }

        // Plants in the box's corners on its wall, where nothing of the room stands; alone, in all four, two at least.
        private static bool Plants(Work w, Box b, int zone, bool alone)
        {
            FitSize p = w.Site.Size(PlantKinds[0]);
            float u0 = p.W * 0.5f + 0.05f, u1 = b.LU - p.W * 0.5f - 0.05f;
            float v0 = (b.WallV0 ? b.WallBack : 0.2f) + p.D * 0.5f + 0.05f, v1 = b.LV - p.D * 0.5f - 0.05f;
            int placed = 0;
            for (int corner = 0; corner < (alone ? 4 : 2); corner++)
            {
                float u = (corner & 1) == 0 ? u0 : u1, v = (corner & 2) == 0 ? v0 : v1;
                if (u1 - u0 < p.W + 0.3f && (corner & 1) == 1)
                {
                    continue;
                }
                if (w.Free(b, u, v, p.W * 0.5f + 0.15f, p.D * 0.5f + 0.15f, zone))
                {
                    placed += w.Put(b, PlantKinds[(corner + (w.Site.Seed & 3)) & 3], FitRole.Floor, FitUse.None, u, v, 0f, 1f, -1, zone, centred: true) >= 0 ? 1 : 0;
                }
            }
            return !alone || placed >= 2;
        }
    }
}
