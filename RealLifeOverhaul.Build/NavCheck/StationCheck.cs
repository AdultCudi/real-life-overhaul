using System;
using System.Collections.Generic;
using RealLifeOverhaul.Law;

// The game-free parts of a furnished police station (Law/StationGrid.cs): pieces of floor in the room's frame, the walkway
// rule (from the door the counter, the cell and every seat stay reachable, pieces in the way are taken out, fixed ones never),
// the place somebody stands to sit down, and how many people the station has.
internal static partial class Program
{
    private static StationGrid Room(float wide, float deep)
    {
        var grid = new StationGrid(-wide * 0.5f, 0f, wide * 0.5f, deep, 0.3f);
        for (int r = 0; r < grid.Rows; r++)
        {
            for (int c = 0; c < grid.Cols; c++)
            {
                grid.SetWalkable(c, r, true);
            }
        }
        return grid;
    }

    private static void Station()
    {
        // Pieces of floor.
        var a = new FloorRect(0f, 0f, 1f, 0.5f);
        Check(a.Overlaps(new FloorRect(1.9f, 0f, 1f, 0.5f)), "station: two pieces side by side that overlap by 0.1 m overlap");
        Check(!a.Overlaps(new FloorRect(2.1f, 0f, 1f, 0.5f)), "station: two pieces 0.1 m apart do not");
        Check(a.Overlaps(new FloorRect(2.1f, 0f, 1f, 0.5f), 0.06f), "station: ...unless each is grown by more than half the gap");
        Check(a.Contains(0.9f, 0.4f) && !a.Contains(1.1f, 0f) && a.Contains(1.1f, 0f, 0.15f), "station: a point inside, outside, and inside with a margin");

        // An empty room 8 m wide and 12 deep: everything is reachable, nothing is taken out.
        StationGrid room = Room(8f, 12f);
        var pieces = new List<FloorRect>();
        var keep = new List<bool>();
        var fixedOnes = new List<bool>();
        Check(room.Cols == 27 && room.Rows == 40, "station: the grid of an 8 x 12 m room in 0.3 m cells is 27 x 40 (" + room.Cols + " x " + room.Rows + ")");
        Check(room.Reachable(0f, 0.4f, 0f, 11f, pieces, keep, 0.24f, 0.9f), "station: the back of an empty room is reachable from the door");

        // A counter across the room, fixed, with a way round its right end; and a row of desks that closes that way.
        pieces.Add(new FloorRect(-0.8f, 5f, 3.2f, 0.4f));   // counter: from -4.0 to 2.4 aside
        keep.Add(true);
        fixedOnes.Add(true);
        Check(room.Reachable(0f, 0.4f, 0f, 9f, pieces, keep, 0.24f, 0.9f), "station: behind the counter is reachable round its open end");
        pieces.Add(new FloorRect(3.2f, 5f, 0.8f, 0.5f));    // a desk in the gap at the end
        keep.Add(true);
        fixedOnes.Add(false);
        pieces.Add(new FloorRect(-3f, 9.5f, 0.6f, 0.6f));   // a plant in a far corner, in nobody's way
        keep.Add(true);
        fixedOnes.Add(false);
        Check(!room.Reachable(0f, 0.4f, 0f, 9f, pieces, keep, 0.24f, 0.9f), "station: the desk in the gap closes the way behind the counter");
        var targetsA = new List<float> { 0f, 0f };
        var targetsD = new List<float> { 4.0f, 9f };   // the front of the counter, and the sergeant's side of it
        int removed = room.KeepWaysOpen(0f, 0.4f, targetsA, targetsD, pieces, keep, fixedOnes, 0.24f, 0.9f);
        Check(removed == 1 && !keep[1] && keep[0] && keep[2], "station: the walkway check takes out the desk in the gap and only it (removed " + removed + ")");
        Check(room.Reachable(0f, 0.4f, 0f, 9f, pieces, keep, 0.24f, 0.9f), "station: ...and behind the counter is reachable again");

        // A fixed piece closing the room from wall to wall: nothing to take out helps, nothing is taken out.
        StationGrid shut = Room(6f, 10f);
        var wall = new List<FloorRect> { new FloorRect(0f, 5f, 3.2f, 0.3f), new FloorRect(1f, 2.5f, 0.4f, 0.4f) };
        var wallKeep = new List<bool> { true, true };
        var wallFixed = new List<bool> { true, false };
        int none = shut.KeepWaysOpen(0f, 0.4f, new List<float> { 0f }, new List<float> { 8f }, wall, wallKeep, wallFixed, 0.24f, 0.9f);
        Check(none == 0 && wallKeep[1], "station: a target cut off by a fixed piece takes nothing else out");

        // A seat on furniture: the open floor nearest it, and that is reachable.
        var chairs = new List<FloorRect> { new FloorRect(2f, 6f, 0.3f, 0.3f) };
        var chairKeep = new List<bool> { true };
        bool open = room.NearestOpen(0f, 0.4f, 2f, 6f, 1.4f, chairs, chairKeep, 0.24f, out float sa, out float sd);
        float off = (float)Math.Sqrt((sa - 2f) * (sa - 2f) + (sd - 6f) * (sd - 6f));
        Check(open && off > 0.3f && off <= 1.4f && !chairs[0].Contains(sa, sd, 0.24f), "station: the place to stand by a chair is off the chair, within a step and a half (" + off.ToString("F2") + " m)");
        // A chair boxed in by fixed pieces on every side has no place to stand.
        var boxed = new List<FloorRect> { new FloorRect(2f, 6f, 1.6f, 0.3f), new FloorRect(2f, 7.5f, 1.6f, 0.3f), new FloorRect(0.6f, 6.75f, 0.3f, 1.2f), new FloorRect(3.4f, 6.75f, 0.3f, 1.2f) };
        var boxedKeep = new List<bool> { true, true, true, true };
        Check(!room.NearestOpen(0f, 0.4f, 2f, 6.75f, 0.5f, boxed, boxedKeep, 0.24f, out _, out _), "station: a seat closed in on every side has nowhere to stand within reach");

        // A narrow room where the walkable floor is a corridor: a piece across the corridor is taken out, one beside it is not.
        var corridor = new StationGrid(-3f, 0f, 3f, 10f, 0.3f);
        for (int r = 0; r < corridor.Rows; r++)
        {
            for (int c = 0; c < corridor.Cols; c++)
            {
                corridor.Centre(c, r, out float ca, out _);
                corridor.SetWalkable(c, r, Math.Abs(ca) < 1.0f);
            }
        }
        var across = new List<FloorRect> { new FloorRect(0f, 5f, 1.2f, 0.3f) };
        var acrossKeep = new List<bool> { true };
        int took = corridor.KeepWaysOpen(0f, 0.4f, new List<float> { 0f }, new List<float> { 9f }, across, acrossKeep, new List<bool> { false }, 0.24f, 0.9f);
        Check(took == 1 && !acrossKeep[0], "station: a piece across a corridor is taken out");

        // The people: modest, within the pool, nobody where there is no place for them.
        StationCrew big = StationCrew.For(6, 3, 5, 12f, true);
        Check(big.AtDesks == 2 && big.OnBreak == 1 && big.Walking == 1 && big.Visitors == 1 && big.Sergeant == 1, "station: a big furnished station has two at desks, one on a break, one walking, one visitor and the sergeant");
        Check(big.Total <= StationCrew.Most && big.Presence <= StationCrew.MostPresence, "station: ...six at most, four of them only there to be seen at most (" + big.Total + ", " + big.Presence + ")");
        StationCrew bare = StationCrew.For(6, 3, 5, 12f, false);
        Check(bare.Total == 1 && bare.Sergeant == 1, "station: the bare station (the option off) has the sergeant only, as before");
        StationCrew small = StationCrew.For(1, 1, 0, 5f, true);
        Check(small.AtDesks == 1 && small.OnBreak == 0 && small.Walking == 0 && small.Visitors == 0, "station: a small room has one at its one desk and nobody walking or on a break");
        bool within = true;
        for (int d = 0; d < 5; d++)
        {
            for (int b = 0; b < 3; b++)
            {
                for (int w = 0; w < 3; w++)
                {
                    for (float depth = 3f; depth < 16f; depth += 2f)
                    {
                        StationCrew crew = StationCrew.For(d, b, w, depth, true);
                        within &= crew.Total <= StationCrew.Most && crew.Presence <= StationCrew.MostPresence && crew.AtDesks <= d && (crew.OnBreak == 0 || b > 0)
                                  && (crew.Visitors == 0 || w > 0) && crew.Sergeant == 1;
                    }
                }
            }
        }
        Check(within, "station: for every room, the cast keeps to its seats, six people and four only there to be seen");
    }
}
