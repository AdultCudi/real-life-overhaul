using System;
using RealLifeOverhaul;

// Where a two-wheeler is left (Garage/BikeClearRules.cs): a bike left with a parking meter's post anywhere in its frame or
// its bars is found to be in it and has a clear place within reach, on each of the four bikes' own boxes; a post only
// touched is not being in it; the places tried, nearest first and the same every time; which layers a bike may not be
// left in, and that only the player's own bike is ever moved. Then, over the sources, that the check is made every time a
// bike is got off and got on, and only then (no cost per frame), with the bike's own colliders and the player left out.
internal static partial class Program
{
    private const float PostRadius = 0.08f;   // m: a post 16 cm across, thicker than a parking meter's or a sign's
    private const float PostStep = 0.05f;     // m between the posts tried over a bike's footprint

    // The four bikes' boxes, measured over every post put inside them: has the first clear place been found, and how far off.
    private static void BikeClearBoxes(string kind, BikeRules.Box frame, BikeRules.Box bars)
    {
        float worst = 0f;
        int posts = 0, unpinned = 0, stranded = 0;
        float backMost = Math.Min(frame.Back, bars.Back), frontMost = Math.Max(frame.Front, bars.Front);
        float sideMost = Math.Max(frame.HalfWidth, bars.HalfWidth);
        for (float x = -sideMost; x <= sideMost + 1e-4f; x += PostStep)
        {
            for (float z = backMost; z <= frontMost + 1e-4f; z += PostStep)
            {
                if (!InBox(frame, x, z, 0f) && !InBox(bars, x, z, 0f))
                {
                    continue;   // not under either box: nothing to be in
                }
                posts++;
                if (!LeftIn(frame, bars, x, z, 0f, 0f))
                {
                    unpinned++;
                    continue;
                }
                float moved = -1f;
                for (int i = 0; i < BikeClearRules.Places; i++)
                {
                    BikeClearRules.Place(i, out float across, out float along);
                    if (!LeftIn(frame, bars, x, z, across, along))
                    {
                        moved = (float)Math.Sqrt(across * across + along * along);
                        break;
                    }
                }
                if (moved < 0f)
                {
                    stranded++;
                }
                worst = Math.Max(worst, moved);
            }
        }
        Check(posts > 50 && unpinned == 0, "bikes left: a post anywhere under a " + kind + "'s frame or bars is found to be in it (" + posts + " posts, " + unpinned + " missed)");
        Check(stranded == 0 && worst > 0f && worst <= 1.4f + 1e-4f,
            "bikes left: a " + kind + " left over a post has a clear place within 1.4 m (" + stranded + " without one; the furthest " + worst.ToString("F2") + " m)");
        float edge = frame.HalfWidth + PostRadius - BikeClearRules.Skin * 0.5f;
        Check(!InBox(frame, edge, frame.Z, BikeClearRules.Skin) && InBox(frame, edge - BikeClearRules.Skin, frame.Z, BikeClearRules.Skin),
            "bikes left: a post that only touches a " + kind + "'s frame (" + (BikeClearRules.Skin * 50f).ToString("F1") + " cm into it) is not being in it, and one further in is");
    }

    private static void BikeClear(string scripts)
    {
        // ---- the places tried: nearest first, all different, the same every time, the last at the furthest a bike is moved.
        int places = BikeClearRules.Places;
        float last = 0f;
        bool rising = true, distinct = true, same = true;
        for (int i = 0; i < places; i++)
        {
            BikeClearRules.Place(i, out float a, out float b);
            BikeClearRules.Place(i, out float a2, out float b2);
            same &= a == a2 && b == b2;
            float d = (float)Math.Sqrt(a * a + b * b);
            rising &= d >= last - 1e-4f;
            last = d;
            for (int j = 0; j < i; j++)
            {
                BikeClearRules.Place(j, out float c, out float e);
                distinct &= Math.Abs(a - c) > 0.01f || Math.Abs(b - e) > 0.01f;
            }
        }
        BikeClearRules.Place(0, out float firstAcross, out float firstAlong);
        Check(places == 40 && rising && distinct && same && Near(last, BikeClearRules.MostShift),
            "bikes left: " + places + " places are tried round a bike, nearest first, all different, the same order every time, out to " + last.ToString("F1") + " m");
        Check(Math.Abs(firstAcross) >= 0.45f && firstAlong == 0f,
            "bikes left: the first place is to the side, far enough to clear a post under the widest bars (" + firstAcross.ToString("F2") + " m)");
        bool refused = false;
        try
        {
            BikeClearRules.Place(places, out _, out _);
        }
        catch (ArgumentOutOfRangeException)
        {
            refused = true;
        }
        Check(refused, "bikes left: a place past the last is refused, never made up");

        // ---- what a bike may not be left in, and who may be stood on.
        foreach (string layer in new[] { "StreetProps", "InteractiveItems", "Buildings", "BuildingWalls", "Vehicles", "ParkedVehicles", "AiVehicles", "PlayerVehicles", "Default" })
        {
            Check(BikeClearRules.Pins(layer), "bikes left: a bike is never left in what is on " + layer);
        }
        foreach (string layer in new[] { "Ground", "GroundUnplacable", "Roads", "Sidewalk", "Crosswalk",
                     "Player", "Human", "UI", "Ignore Raycast", "AutoParkSpots", "ParkingArea", "TransparentFX", "Water", "", null })
        {
            Check(!BikeClearRules.Pins(layer), "bikes left: " + (string.IsNullOrEmpty(layer) ? "a layer with no name" : layer) + " does not hold a bike in (the ground, people and zones)");
        }
        Check(BikeClearRules.Crowds("Player") && BikeClearRules.Crowds("Human") && !BikeClearRules.Crowds("StreetProps") && !BikeClearRules.Crowds("Sidewalk") && !BikeClearRules.Crowds(""),
            "bikes left: a bike is never moved onto a person");

        // ---- only the player's own bike, out in the street, and only when it is in something.
        int moves = 0;
        foreach (bool pinned in new[] { false, true })
        {
            foreach (bool mine in new[] { false, true })
            {
                foreach (bool indoors in new[] { false, true })
                {
                    if (BikeClearRules.Move(pinned, mine, indoors))
                    {
                        moves++;
                        Check(pinned && mine && !indoors, "bikes left: a bike is moved only when it is in something, the player's own and in the street");
                    }
                }
            }
        }
        Check(moves == 1 && !BikeClearRules.Move(true, false, false), "bikes left: another player's bike (Going Public) is never moved, even in a meter");
        Check(Near(BikeClearRules.Inner(0.5f), 0.5f - BikeClearRules.Skin, 1e-5f) && Near(BikeClearRules.Inner(0.02f), 0.01f, 1e-5f), "bikes left: the skin comes off each box, never below 1 cm");

        // ---- over the sources: the check is made on every getting off and on, and only then.
        string two = Source(scripts, @"Garage\TwoWheelers.cs");
        string frameBody = Body(two, "public void Frame(");
        string changed = Body(frameBody, "if (selected != _selected)");
        int off = changed.IndexOf("_riding.Dismounted();", StringComparison.Ordinal);
        int leftCheck = changed.IndexOf("Unpin(_riding, gm, \"left\")", StringComparison.Ordinal);
        int reassigned = changed.IndexOf("_riding = selected != null", StringComparison.Ordinal);
        int on = changed.IndexOf("_riding.Mounted(gm.playerController);", StringComparison.Ordinal);
        int onCheck = changed.IndexOf("Unpin(_riding, gm, \"got on\")", StringComparison.Ordinal);
        Check(off >= 0 && leftCheck > off && leftCheck < reassigned && on > reassigned && onCheck > on,
            "bikes left source: the place is checked right after a bike is got off (before the ride is let go of) and right after it is got on");
        Check(Count(frameBody, "Unpin(") == 2 && Count(two, "Unpin(") == 3,
            "bikes left source: the check is made only when the bike ridden changes, never on the frames between");
        Check(Body(two, "private void Fall(").Contains("scooter.ExitVehicle()") && Body(two, "private void Return(").Contains("mount.Scooter.ExitVehicle()"),
            "bikes left source: a fall and the dock's return get off through the game's own way off, so they come through the same check");
        string unpin = Body(two, "private void Unpin(");
        Check(unpin.Contains("GoingPublic.MineVehicle(mount.Scooter)") && unpin.Contains("BikeClearRules.Move(pinned, mine, BuildingManager.IsInsideBuilding)")
              && unpin.Contains("BikePlaces.PinMask") && unpin.Contains("GarageState.Bike(mount.Record.Id) != mount.Record") && unpin.Contains("catch (Exception e)"),
            "bikes left source: only the player's own bike that is still theirs is moved, by the rules, and a fault leaves it where it is");
        string spot = Body(two, "private static bool ClearSpot(");
        Check(spot.Contains("BikeClearRules.Place(i,") && spot.Contains("BikePlaces.RoomMask") && spot.Contains("BikePlaces.Unwalled(from, spot,") && spot.Contains("pass == 0 && !pavement")
              && spot.Contains("Quaternion.Euler(0f, bike.eulerAngles.y, 0f)"),
            "bikes left source: the new place is the rules' nearest, pavement first, clear of people too, never through a wall, and the bike stood upright");
        string mount = Source(scripts, @"Garage\BikeMount.cs");
        string pinnedBody = Body(mount, "public bool Pinned(");
        string boxed = Body(mount, "private bool Boxed(");
        Check(pinnedBody.Contains("Boxed(frame,") && pinnedBody.Contains("Boxed(_bars,") && boxed.Contains("Physics.OverlapBoxNonAlloc(") && boxed.Contains("BikeClearRules.Inner(")
              && boxed.Contains("other.transform.IsChildOf(transform)") && boxed.Contains("other.transform.IsChildOf(also)") && boxed.Contains("QueryTriggerInteraction.Ignore"),
            "bikes left source: both boxes are looked for, less the skin, without allocating, leaving out the bike's own colliders, the player and triggers");
    }

    // True if a post at (x, z) of the bike's frame, with the bike moved by (across, along), is in its frame or its bars,
    // each less the skin. The post stands from the ground to above the bars, so any box over it is in it.
    private static bool LeftIn(BikeRules.Box frame, BikeRules.Box bars, float x, float z, float across, float along)
    {
        return InBox(frame, x - across, z - along, BikeClearRules.Skin) || InBox(bars, x - across, z - along, BikeClearRules.Skin);
    }

    // A round post of PostRadius at (x, z) against a box seen from above, less a skin: the nearest point of the box to the
    // post's middle closer than its radius. With no skin and no radius: is the point under the box.
    private static bool InBox(BikeRules.Box box, float x, float z, float skin)
    {
        if (skin <= 0f)
        {
            return Math.Abs(x - box.X) <= box.HalfWidth && Math.Abs(z - box.Z) <= box.Length * 0.5f;
        }
        float dx = Math.Max(Math.Abs(x - box.X) - BikeClearRules.Inner(box.HalfWidth), 0f);
        float dz = Math.Max(Math.Abs(z - box.Z) - BikeClearRules.Inner(box.Length * 0.5f), 0f);
        return dx * dx + dz * dz < PostRadius * PostRadius;
    }
}
