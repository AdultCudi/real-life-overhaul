using System;
using RealLifeOverhaul;

// The game-free rules of getting out of the driving school's car (Licences/Driving/CarExitRules.cs): the places tried
// around the car, what counts as inside it, when a player out of it is taken to be stuck, and when the car may go.
internal static partial class Program
{
    private static void CarExit()
    {
        const float halfWidth = 0.9f, halfLength = 2.1f;

        // The places: six, each outside the body with room to spare, the driver's door first.
        bool allOut = true;
        for (int i = 0; i < CarExitRules.Places; i++)
        {
            CarExitRules.Place(i, halfWidth, halfLength, out float across, out float along);
            allOut &= !CarExitRules.InBody(across, along, halfWidth, halfLength);
            allOut &= Math.Abs(across) >= halfWidth + CarExitRules.Margin || Math.Abs(along) >= halfLength + CarExitRules.Margin;
        }
        Check(allOut, "car exit: every place tried is outside the car's body and its margin");
        CarExitRules.Place(0, halfWidth, halfLength, out float a0, out float l0);
        Check(a0 < 0f && Math.Abs(l0) < halfLength, "car exit: the first place is beside the driver's door (the left side, along the body)");
        CarExitRules.Place(1, halfWidth, halfLength, out float a1, out float l1);
        Check(a1 > 0f && Near(l1, l0), "car exit: the second is the passenger's door, square across from it");
        CarExitRules.Place(2, halfWidth, halfLength, out float a2, out float l2);
        Check(Near(a2, 0f) && l2 < -halfLength, "car exit: the third is behind the tail");
        CarExitRules.Place(5, halfWidth, halfLength, out float a5, out float l5);
        Check(Near(a5, 0f) && l5 > halfLength, "car exit: the last is ahead of the nose");
        bool distinct = true;
        for (int i = 0; i < CarExitRules.Places; i++)
        {
            for (int j = i + 1; j < CarExitRules.Places; j++)
            {
                CarExitRules.Place(i, halfWidth, halfLength, out float ai, out float li);
                CarExitRules.Place(j, halfWidth, halfLength, out float aj, out float lj);
                distinct &= Math.Abs(ai - aj) + Math.Abs(li - lj) > 1f;
            }
        }
        Check(distinct, "car exit: no two places are within a metre of each other (a blocked one is not tried twice)");
        bool threw = false;
        try
        {
            CarExitRules.Place(CarExitRules.Places, halfWidth, halfLength, out _, out _);
        }
        catch (ArgumentOutOfRangeException)
        {
            threw = true;
        }
        Check(threw, "car exit: there is no seventh place");
        Check(CarExitRules.SampleRadius < CarExitRules.DoorOut + halfWidth,
            "car exit: the walkable ground looked for around a door place cannot reach across to the other side of the car");

        // Inside the body.
        Check(CarExitRules.InBody(0f, 0f, halfWidth, halfLength), "car exit: the car's middle is inside it");
        Check(CarExitRules.InBody(halfWidth + 0.1f, 0f, halfWidth, halfLength), "car exit: a hand's width from the side is still inside it (the margin)");
        Check(!CarExitRules.InBody(halfWidth + CarExitRules.Margin + 0.05f, 0f, halfWidth, halfLength), "car exit: beyond the margin is out");
        Check(!CarExitRules.InBody(0f, -(halfLength + CarExitRules.Margin + 0.05f), halfWidth, halfLength), "car exit: behind the tail beyond the margin is out");

        // Stuck: the cases that left a player against the car's side until the game was restarted.
        float t = 5f, d = 1.5f;
        Check(!CarExitRules.Stuck(false, t, d, false, false, false, true, false), "car exit: on the mesh beside the car, shown and free: not stuck");
        Check(CarExitRules.Stuck(false, t, d, false, false, false, false, true), "car exit: shown again but inside the car and off the mesh (the exit stopped before its warp): stuck");
        Check(CarExitRules.Stuck(false, t, d, false, false, false, false, false), "car exit: off the mesh beside the car (the warp refused): stuck");
        Check(CarExitRules.Stuck(false, t, 0.2f, true, true, true, false, true), "car exit: still under the car, hidden and held (the exit stopped at its event): stuck");
        Check(CarExitRules.Stuck(false, t, d, false, true, false, true, false), "car exit: on the mesh but still held as in a vehicle (cannot walk): stuck");
        Check(!CarExitRules.Stuck(true, t, d, true, true, true, false, true), "car exit: at the wheel is never stuck (the game put them back, or they got back in)");
        Check(!CarExitRules.Stuck(false, CarExitRules.SettleSeconds - 0.1f, d, false, false, false, false, true), "car exit: not judged before the game's own exit has had its frame");
        Check(!CarExitRules.Stuck(false, CarExitRules.WatchFor + 1f, d, false, false, false, false, false), "car exit: not judged once half a minute has gone");
        Check(!CarExitRules.Stuck(false, t, CarExitRules.StuckNear + 1f, false, false, false, false, false), "car exit: not judged once the player is well away from the car");

        // The car goes back only once the exit is over.
        Check(!CarExitRules.MayTakeBack(false, 0.5f, false), "car exit: not taken back within a moment of the player getting out");
        Check(CarExitRules.MayTakeBack(false, CarExitRules.KeepAfterExit + 0.1f, false), "car exit: taken back once the moment has passed");
        Check(!CarExitRules.MayTakeBack(false, 60f, true), "car exit: never taken back with the player stuck under it");
        Check(!CarExitRules.MayTakeBack(true, 60f, false), "car exit: never taken back with the player at the wheel");
        Check(CarExitRules.KeepAfterExit >= CarExitRules.SettleSeconds, "car exit: the car stays at least until a stuck player would be seen");
    }
}
