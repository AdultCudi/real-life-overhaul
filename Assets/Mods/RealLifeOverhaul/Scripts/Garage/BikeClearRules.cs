using System;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Where a two-wheeler may be left, in plain numbers so that NavCheck checks the rules <see cref="TwoWheelers"/>
    /// leaves a bike by: the game freezes a scooter where it was got off, so one left in a post stays in it. A few
    /// multiplications a check.
    /// </summary>
    public static class BikeClearRules
    {
        public const float Skin = 0.03f;     // m off every face of the boxes: a resting touch (a post just met, a wall leant on) is not being in it
        public const float MostShift = 2.6f; // m: the furthest a bike is moved; with nothing clear it stays, and "Bring it here" is the way out
        public const int Ways = 8;           // places on each ring: either side, ahead and behind, the four corners

        // metres from where it stands; the first ring clears a post under the middle of the widest bars (0.37 m)
        private static readonly float[] Rings = { 0.5f, 0.9f, 1.4f, 2.0f, MostShift };
        private const float Corner = 0.70710678f;   // a corner's share of a ring along each axis

        public static int Places => Rings.Length * Ways;

        /// <summary>The i-th place tried, in the bike's frame (metres): nearest ring first, either side, ahead and behind, corners.</summary>
        public static void Place(int i, out float across, out float along)
        {
            if (i < 0 || i >= Places)
            {
                throw new ArgumentOutOfRangeException(nameof(i));
            }
            float ring = Rings[i / Ways];
            switch (i % Ways)
            {
                case 0: across = ring; along = 0f; break;
                case 1: across = -ring; along = 0f; break;
                case 2: across = 0f; along = ring; break;
                case 3: across = 0f; along = -ring; break;
                case 4: across = ring * Corner; along = ring * Corner; break;
                case 5: across = -ring * Corner; along = ring * Corner; break;
                case 6: across = ring * Corner; along = -ring * Corner; break;
                default: across = -ring * Corner; along = -ring * Corner; break;
            }
        }

        /// <summary>True for a layer a bike must never be left in: anything solid, never the ground, people or zones.</summary>
        public static bool Pins(string layer)
        {
            if (string.IsNullOrEmpty(layer) || Zone(layer))
            {
                return false;
            }
            ContactKind kind = CarContactRules.KindOfLayer(layer);
            return kind != ContactKind.Ground && kind != ContactKind.Person;
        }

        /// <summary>True for a layer of people: a bike is never stood on somebody.</summary>
        public static bool Crowds(string layer)
        {
            return !string.IsNullOrEmpty(layer) && CarContactRules.KindOfLayer(layer) == ContactKind.Person;
        }

        // Zone-only layers, left out as the mod's other room tests leave them.
        private static bool Zone(string layer)
        {
            switch (layer)
            {
                case "UI":
                case "Ignore Raycast":
                case "AutoParkSpots":
                case "ParkingArea":
                case "TransparentFX":
                case "Water":
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>A bike is moved if it stands in something, is the player's own (Going Public) and is outdoors.</summary>
        public static bool Move(bool pinned, bool mine, bool indoors)
        {
            return pinned && mine && !indoors;
        }

        /// <summary>A box's half size less the skin, never below 1 cm, so that a thin box still finds what it is in.</summary>
        public static float Inner(float half)
        {
            return Math.Max(0.01f, half - Skin);
        }
    }
}
