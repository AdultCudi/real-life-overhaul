using System;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Where a two-wheeler may be left, in plain numbers so that NavCheck checks the rules <see cref="TwoWheelers"/>
    /// leaves a bike by. The game stands a scooter wherever its rider gets off and freezes it there, so a bike left
    /// against a post or a meter stays in it. On every getting off and on, its frame and bars boxes are looked for in
    /// anything solid (<see cref="Pins"/>), and the player's own bike found in something is moved to the nearest clear
    /// place (<see cref="Place"/>). A few multiplications a check; nothing allocated.
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

        /// <summary>
        /// The i-th place tried, in the bike's own frame (metres across and along): the nearest ring first, on each
        /// ring either side first (along the pavement), then ahead and behind, then the corners.
        /// </summary>
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

        /// <summary>
        /// True for a layer a bike must never be left in: street furniture, buildings, vehicles, anything solid. Never
        /// the ground, people (they walk off), trigger-only zones or a layer with no name.
        /// </summary>
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

        // Zone-only layers, left out as the mod's other room tests leave them (CitySpots, BikePlaces.Fits).
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

        /// <summary>
        /// Whether a bike is moved: it stands in something, it is the player's own (Going Public), and it is outdoors
        /// (indoors the street is not there to measure; it is looked at again when got on).
        /// </summary>
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
