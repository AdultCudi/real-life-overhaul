using System;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Where a two-wheeler may be left, in plain numbers, so that NavCheck (BikeCheck.cs) checks the very rules
    /// <see cref="TwoWheelers"/> leaves a bike by.
    ///
    /// The game stands a scooter wherever its rider gets off and holds it there (ScooterController.ExitVehicle turns its
    /// physics off, and with it every constraint is frozen): nothing asks whether that place has room. A rider who comes
    /// off against a parking meter, a post or a bench (the physics step that stops the bike is the one in which its box
    /// went into the thing), or who pulls up with the bars over a meter's head, leaves the bike in it, held there; got on
    /// again, its boxes are inside the meter and it cannot be ridden out. So the moment a bike is left, and again the
    /// moment it is got on, its frame box and its bars box (<see cref="BikeRules.Frame"/>, <see cref="BikeRules.Bars"/>)
    /// are looked for in anything solid (<see cref="Pins"/>); a bike of the player's own found in something is stood on the
    /// nearest place tried (<see cref="Place"/>) that is clear of it and of people, on pavement first.
    ///
    /// A switch and a few multiplications, asked once when a bike is got off or on. Nothing is allocated.
    /// </summary>
    public static class BikeClearRules
    {
        public const float Skin = 0.03f;     // m off every face of the boxes: a resting touch (a post just met, a wall leant on) is not being in it
        public const float MostShift = 2.6f; // m: the furthest a bike is moved; with nothing clear by then it stays, and "Bring it here" is the way out
        public const int Ways = 8;           // places on each ring: either side, ahead and behind, the four corners

        // m from where it stands: the first ring clears a post under the middle of the widest bars (0.37 m) and a thick post's half.
        private static readonly float[] Rings = { 0.5f, 0.9f, 1.4f, 2.0f, MostShift };
        private const float Corner = 0.70710678f;   // a corner's share of a ring along each axis

        /// <summary>How many places are tried.</summary>
        public static int Places => Rings.Length * Ways;

        /// <summary>
        /// The i-th place tried, in the bike's own frame (metres across to its right and along to its front): the nearest ring
        /// first, and on each ring to either side first (along the pavement a bike by the kerb usually is), then ahead and
        /// behind, then the four corners. The same order on every machine.
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
        /// True for a layer whose things a bike must never be left in: street furniture (parking meters, posts, lamps, bins,
        /// benches), buildings and walls, vehicles, and any other solid thing. Never the ground the bike stands on (the road,
        /// the pavement and its kerb, crossings), people (they walk off), the zones that are only triggers, or a layer with
        /// no name (nothing is on it).
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

        // The layers that are only zones, as the mod's other room tests leave them out (CitySpots, BikePlaces.Fits).
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
        /// Whether a bike is moved: it stands in something, it is the player's own (in a Going Public session never another
        /// player's), and it is out in the street (from indoors the street round it is not there to be measured; it is
        /// looked at again when it is got on).
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
