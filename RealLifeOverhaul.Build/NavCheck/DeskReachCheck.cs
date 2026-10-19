using System;
using System.Collections.Generic;
using System.IO;
using RealLifeOverhaul.FitOut;
using RealLifeOverhaul.Tech;

// Where the player stands to sit at their desk (FitOut/DeskReach.cs), on a model of the game's walkable floor: the room's
// grid with what stands cut out of it and a walker's width kept off it, walked from the door. The places by a chair go out
// away from its desk, never under it; a desk that cannot be walked to is passed over for the next one round, the same every
// visit; a player beside a desk the game finds no way to is at it. And over the sources: the desk is chosen and clicked
// through those rules, on the floor as it is cut.
internal static partial class Program
{
    private const float DeskWalker = 0.3f;   // metres a walker keeps off what stands (the game's navigation mesh is cut so far round it)

    private static void DeskReaches()
    {
        // A tower's office floor as a player found it: a long table, and a desk turned 30 degrees hard against its end.
        RoomGrid g = FitFloor(14f, 10f);
        Furnish(g, 2f, 5.4f, 8f, 6.6f);                     // the long table with the other desks
        FurnishTurned(g, 8.9f, 6.3f, 0.7f, 0.4f, 30f);      // the player's desk against its end
        float yaw = 30f * (float)Math.PI / 180f;
        float faceX = (float)Math.Sin(yaw), faceZ = (float)Math.Cos(yaw);   // the chair faces the desk
        float seatX = 8.9f - faceX * 0.75f, seatZ = 6.3f - faceZ * 0.75f;
        bool[] reached = Walked(g, 7f, 0.35f);

        // What stood there before: 0.55 m ahead of the seat, under the desk.
        float oldX = seatX + faceX * 0.55f, oldZ = seatZ + faceZ * 0.55f;
        Check(!WalkedNear(g, reached, oldX, oldZ), "desk reach: 0.55 m ahead of the chair is under the turned desk, where nobody can walk (what the click was refused at)");
        Check(DeskReach.Spot(0, seatX, seatZ, 8.9f, 6.3f, faceX, faceZ, out float x0, out float z0) && WalkedNear(g, reached, x0, z0),
            "desk reach: the first place by the chair, out of it away from the desk, is walked to from the door");
        Check(StandModel(g, reached, seatX, seatZ, 8.9f, 6.3f, faceX, faceZ, out _), "desk reach: the player's desk at the end of the long table has a place to stand that is walked to from the door");

        // The places by a chair: away from the desk first, all within reach of the seat, none ahead of it into the desk.
        bool near = true, behind = true;
        int spots = 0;
        for (int n = 0; DeskReach.Spot(n, 0f, 0f, 0f, 1f, 0f, 1f, out float x, out float z); n++)
        {
            spots++;
            near &= x * x + z * z <= 1.2f * 1.2f;
            behind &= z <= 0.001f;   // the desk is ahead (+z): nothing tried under it
        }
        Check(spots == DeskReach.Spots && !DeskReach.Spot(DeskReach.Spots, 0f, 0f, 0f, 1f, 0f, 1f, out _, out _), "desk reach: " + DeskReach.Spots + " places tried by a chair, then no more");
        Check(near && behind, "desk reach: every place tried is within 1.2 m of the seat and none is ahead of it, under the desk");
        Check(DeskReach.Spot(0, 2f, 3f, 2f, 3.75f, 0f, 1f, out float ax, out float az) && Near(ax, 2f) && Near(az, 3f - DeskReach.Out),
            "desk reach: the first place is " + DeskReach.Out + " m straight out from the seat, away from the desk");
        Check(DeskReach.Spot(0, 1f, 1f, 1f, 1f, 1f, 0f, out float bx, out float bz) && Near(bx, 1f - DeskReach.Out) && Near(bz, 1f),
            "desk reach: a chair with no desk apart from it is stood up from at its back, the opposite of where it faces");

        // A place across a partition from the chair is not beside it, however near: the walk round is too long.
        RoomGrid split = FitFloor(14f, 10f);
        Furnish(split, 1f, 4.85f, 8f, 5.0f);                 // a partition behind the chair
        Furnish(split, 4.2f, 6.1f, 5.6f, 6.9f);              // the desk; its chair at (4.9, 5.55)
        bool[] both = Walked(split, 7f, 0.35f);
        int seatCell = NearestReached(split, both, 4.9f, 5.55f, DeskReach.SeatRadius), across = NearestReached(split, both, 4.9f, 4.4f, DeskReach.SampleRadius);
        Check(seatCell >= 0 && across >= 0 && !DeskReach.Beside(WalkedMetres(split, both, across, seatCell)) && DeskReach.Beside(0.85f) && !DeskReach.Beside(-1f),
            "desk reach: a place 0.85 m out from the chair but across a partition is not beside it (walked " + WalkedMetres(split, both, across, seatCell).ToString("F1") + " m round)");

        // A desk boxed in on every side is passed over; the next round that can be walked to is the player's.
        RoomGrid boxed = FitFloor(14f, 10f);
        Furnish(boxed, 2f, 2f, 4.4f, 2.3f);
        Furnish(boxed, 2f, 4.1f, 4.4f, 4.4f);
        Furnish(boxed, 2f, 2f, 2.3f, 4.4f);
        Furnish(boxed, 4.1f, 2f, 4.4f, 4.4f);
        Furnish(boxed, 2.9f, 3.4f, 3.5f, 3.9f);              // a desk inside the box, its chair at (3.2, 2.9)
        Furnish(boxed, 9f, 6f, 10.4f, 6.8f);                 // a desk out on the floor, its chair at (9.7, 5.5)
        bool[] open = Walked(boxed, 7f, 0.35f);
        var seats = new[] { (3.2f, 2.9f, 3.2f, 3.65f), (9.7f, 5.5f, 9.7f, 6.4f) };
        Func<int, bool> reachable = k => StandModel(boxed, open, seats[k].Item1, seats[k].Item2, seats[k].Item3, seats[k].Item4, 0f, 1f, out _);
        Check(!reachable(0) && reachable(1), "desk reach: a desk boxed in by partitions cannot be walked to; one on the open floor can");
        Check(DeskReach.First(0, 2, reachable) == 1 && DeskReach.First(1, 2, reachable) == 1, "desk reach: the seed's desk boxed in, the player's is the next one round that can be walked to");

        // The choice among a floor's desks: the seed's own when it can be walked to, then round, each asked once, the same every time.
        Check(DeskReach.First(3, 8, k => true) == 3, "desk reach: the seed's own desk when it can be walked to");
        Check(DeskReach.First(6, 8, k => k == 1) == 1 && DeskReach.First(6, 8, k => k == 7) == 7, "desk reach: otherwise the next round that can, past the last back to the first");
        Check(DeskReach.First(2, 5, k => false) == -1 && DeskReach.First(0, 0, k => true) == -1 && DeskReach.First(-1, 4, k => true) == -1,
            "desk reach: no desk when none can be walked to, a floor has none, or none was picked");
        var asked = new List<int>();
        DeskReach.First(4, 6, k => { asked.Add(k); return false; });
        Check(asked.Count == 6 && asked[0] == 4 && asked[1] == 5 && asked[2] == 0 && asked[5] == 3, "desk reach: each desk asked once, in a fixed order from the seed's ("
                                                                                                        + string.Join(" ", asked) + ")");
        bool same = true;
        for (int seed = 0; seed < 50; seed++)
        {
            int pick = FitPlanner.PickDesk(12, seed * 7919);
            Func<int, bool> some = k => (k * 7 + 3) % 5 != 0;
            same &= DeskReach.First(pick, 12, some) == DeskReach.First(FitPlanner.PickDesk(12, seed * 7919), 12, some);
        }
        Check(same, "desk reach: the same floor gives the same desk every visit and on every machine");

        // A path that stops short is no way there (the game's own test of a partial path); a player beside the desk is at it.
        Check(DeskReach.Ends(1f, 1f, 1.2f, 1.1f) && !DeskReach.Ends(1f, 1f, 1.4f, 1f), "desk reach: a path ending within " + DeskReach.EndWithin + " m of the place reaches it, and not 0.4 m short");
        Check(DeskReach.AtDesk(1.5f) && DeskReach.AtDesk(DeskReach.Near) && !DeskReach.AtDesk(3f), "desk reach: a player 1.5 m from the seat is at the desk, and not 3 m away");

        DeskReachSources();
    }

    // The player's desk chosen, and walked to when clicked, through the rules above, on the floor once it is cut.
    private static void DeskReachSources()
    {
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        string view = Code(File.ReadAllText(Path.Combine(root, "FitOut", "FitView.cs")));
        string game = Code(File.ReadAllText(Path.Combine(root, "Careers", "DeskGame.cs")));
        string day = Code(File.ReadAllText(Path.Combine(root, "Careers", "CareersDirector.DeskDay.cs")));
        string work = Code(File.ReadAllText(Path.Combine(root, "Careers", "DeskWork.cs")));
        int seats = view.IndexOf("private void Seats(", StringComparison.Ordinal), wait = view.IndexOf("CarveProbe(r, out", StringComparison.Ordinal);
        int seated = view.IndexOf("Seats(made)", StringComparison.Ordinal), mine = view.IndexOf("private int PlayerSeat()", StringComparison.Ordinal);
        Check(wait > 0 && seated > wait && seats > 0, "desk reach sources: a fitted floor's seats are measured once the navigation system has cut what stands out of the floor");
        Check(mine > 0 && view.IndexOf("DeskReach.First(", mine, StringComparison.Ordinal) > mine && view.IndexOf("FitReach.StandBy(", mine, StringComparison.Ordinal) > mine,
            "desk reach sources: the player's desk on a fitted floor is the first one round from the seed's that can be walked to from the door");
        Check(game.Contains("DeskReach.First(") && game.Contains("Reachable(Found[n], from)"), "desk reach sources: a station of the game's is the player's only if they can walk to it");
        Check(day.Contains("DeskGame.WalkTo(desk,") && work.Contains("DeskGame.WalkTo(_desk,") && !day.Contains("WalkTo(desk.Stand") && !work.Contains("WalkTo(_desk.Stand"),
            "desk reach sources: a fitted desk is walked to with another place by its chair when the game finds no way to its own");
    }

    // The walkable floor from a door: open cells a walker's width clear of what stands.
    private static bool[] Walked(RoomGrid g, float doorX, float doorZ)
    {
        var near = new bool[g.Count];
        g.NearFurniture(DeskWalker, near);
        var reached = new bool[g.Count];
        g.Reach(doorX, doorZ, near, reached);
        return reached;
    }

    // As the game samples a place onto its walkable floor and finds a whole path there: a reached cell within the sampling radius.
    private static bool WalkedNear(RoomGrid g, bool[] reached, float x, float z) => NearestReached(g, reached, x, z, DeskReach.SampleRadius) >= 0;

    private static int NearestReached(RoomGrid g, bool[] reached, float x, float z, float within)
    {
        int best = -1;
        float bestSqr = within * within;
        for (int i = 0; i < g.Count; i++)
        {
            g.Centre(i, out float cx, out float cz);
            float sqr = (cx - x) * (cx - x) + (cz - z) * (cz - z);
            if (reached[i] && sqr <= bestSqr)
            {
                best = i;
                bestSqr = sqr;
            }
        }
        return best;
    }

    // Metres walked over the reached floor from one cell to another, a cell a step; -1 for no way.
    private static float WalkedMetres(RoomGrid g, bool[] reached, int from, int to)
    {
        var steps = new int[g.Count];
        for (int i = 0; i < steps.Length; i++)
        {
            steps[i] = -1;
        }
        var queue = new Queue<int>();
        steps[from] = 0;
        queue.Enqueue(from);
        while (queue.Count > 0)
        {
            int i = queue.Dequeue();
            if (i == to)
            {
                return steps[i] * g.Cell;
            }
            int c = i % g.Cols, r = i / g.Cols;
            foreach (int n in new[] { c + 1 < g.Cols ? i + 1 : -1, c > 0 ? i - 1 : -1, r + 1 < g.Rows ? i + g.Cols : -1, r > 0 ? i - g.Cols : -1 })
            {
                if (n >= 0 && reached[n] && steps[n] < 0)
                {
                    steps[n] = steps[i] + 1;
                    queue.Enqueue(n);
                }
            }
        }
        return -1f;
    }

    // FitReach.StandBy on the model: the first place by the chair that is walked to from the door, and beside the seat.
    private static bool StandModel(RoomGrid g, bool[] reached, float seatX, float seatZ, float deskX, float deskZ, float faceX, float faceZ, out int which)
    {
        int seat = NearestReached(g, reached, seatX, seatZ, DeskReach.SeatRadius);
        for (which = 0; DeskReach.Spot(which, seatX, seatZ, deskX, deskZ, faceX, faceZ, out float x, out float z); which++)
        {
            int at = NearestReached(g, reached, x, z, DeskReach.SampleRadius);
            if (at >= 0 && (seat < 0 || DeskReach.Beside(WalkedMetres(g, reached, at, seat))))
            {
                return true;
            }
        }
        which = -1;
        return false;
    }

    // A piece turned on the floor: the cells inside its turned rectangle.
    private static void FurnishTurned(RoomGrid g, float cx, float cz, float halfW, float halfD, float yawDegrees)
    {
        float yaw = yawDegrees * (float)Math.PI / 180f, sin = (float)Math.Sin(yaw), cos = (float)Math.Cos(yaw);
        for (int r = 0; r < g.Rows; r++)
        {
            for (int c = 0; c < g.Cols; c++)
            {
                g.Centre(c, r, out float x, out float z);
                float dx = x - cx, dz = z - cz;
                float u = dx * cos - dz * sin, v = dx * sin + dz * cos;   // into the piece's own frame
                if (Math.Abs(u) <= halfW && Math.Abs(v) <= halfD && g[c, r] == RoomGrid.Open)
                {
                    g[c, r] = RoomGrid.Furniture;
                }
            }
        }
    }
}
