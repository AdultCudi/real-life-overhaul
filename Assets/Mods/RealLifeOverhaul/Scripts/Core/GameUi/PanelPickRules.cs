namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// One panel's claim on the game's single panel box over a place (UI.Overlays.OverlayUI), measured from the player
    /// at the moment two panels want it.
    /// </summary>
    public struct PanelClaim
    {
        public float Distance;   // metres on the flat from the player to the place the panel is for
        public float Facing;     // the cosine of the angle between the way the player faces and the way to the place: 1 ahead, -1 behind
        public bool Meet;        // the place is a person the player came to meet
        public bool Game;        // the game's own panel for a place the player stands in (a pump, a repair bay, a lift), or another mod's

        public PanelClaim(float distance, float facing, bool meet, bool game)
        {
            Distance = distance;
            Facing = facing;
            Meet = meet;
            Game = game;
        }
    }

    /// <summary>
    /// Who has the game's one panel box over a place when the player is near two places that want it. The game has
    /// one such box (OverlayUI) and sends the interact key to whatever is in it (GameManager to
    /// OverlayUI.OnPlayerActionPressed), so the panel in the box is the one the key runs; the others wait.
    ///
    /// The rule:
    ///  - each place is counted by its distance on the flat, made longer the further it is from the way the player
    ///    faces: straight ahead as it is, straight behind twice as far (<see cref="BehindWeight"/>);
    ///  - a person the player came to meet counts as nearest of all once within talking range (<see cref="TalkRange"/>);
    ///  - a panel takes the box from the one in it only when it counts nearer by a margin (<see cref="KeepMetres"/> or
    ///    <see cref="KeepShare"/> of the other's count, whichever is more). Of two panels at most one can beat the
    ///    other by that margin, so the box never goes to and fro between them while the player stands still; a step or
    ///    a turn of the body towards the other place gives it the box at its owner's next look;
    ///  - the game's own panels (the filling station's, the repair bay's, the private driver's garage's and the lift's)
    ///    are put in the box by the game's own trigger while the player, or the player's car, stands in that place:
    ///    the player is at it, so it counts nearest and is never taken from. Another mod's panel is left alone too.
    ///
    /// The key: a panel's button runs only while that panel is in the box (<see cref="KeyToPanel"/>), and the mod's own
    /// interact key does nothing while the box shows a button on it (<see cref="KeyFree"/>), so one press runs one thing,
    /// the one the player sees.
    ///
    /// Where a person waits to be met (<see cref="Pick"/>): the first place to stand that is clear of every other panel's
    /// reach by a margin, else the one with the most room, so that where the street allows nothing else wants the box
    /// there at all.
    ///
    /// Plain arithmetic on plain numbers: no game types, nothing allocated. Checked offline (NavCheck, PanelPickCheck.cs).
    /// </summary>
    public static class PanelPickRules
    {
        public const float TalkRange = 3.5f;     // metres: a person met counts as nearest from this close; an outing's hello is offered from 3.4 m
        public const float BehindWeight = 1f;    // a place straight behind the player counts this much farther again: twice its distance
        public const float KeepMetres = 0.75f;   // counted metres a panel must be nearer than the one in the box: about one step...
        public const float KeepShare = 0.2f;     // ...or this share of the other's count, whichever is more, so that a sway of the body is not a turn
        public const float AtPlace = 0.3f;       // metres within which the player stands at the place: which way they face is not asked
        public const float StandClear = 0.6f;    // metres a person waiting to be met stands outside every other panel's reach, where the street allows

        /// <summary>The distance a claim counts as: metres, with what is behind the player made longer. 0 for the nearest of all.</summary>
        public static float Counted(in PanelClaim claim)
        {
            if (claim.Game || (claim.Meet && claim.Distance <= TalkRange))
            {
                return 0f;
            }
            float distance = claim.Distance > 0f ? claim.Distance : 0f;
            if (distance <= AtPlace)
            {
                return distance;
            }
            float facing = claim.Facing > 1f ? 1f : claim.Facing < -1f ? -1f : claim.Facing;
            return distance * (1f + BehindWeight * (1f - facing) * 0.5f);
        }

        /// <summary>Counted metres a panel must beat the one in the box by, for a box held at <paramref name="held"/> counted metres.</summary>
        public static float Margin(float held)
        {
            float share = KeepShare * (held > 0f ? held : 0f);
            return share > KeepMetres ? share : KeepMetres;
        }

        /// <summary>True when the panel that asks takes the box from the one in it; false keeps the box as it is.</summary>
        public static bool Takes(in PanelClaim holder, in PanelClaim asking)
        {
            if (holder.Game)
            {
                return false;
            }
            float held = Counted(holder);
            return Counted(asking) < held - Margin(held);
        }

        /// <summary>True when a panel's button may run on its key: only the panel in the box, with that button there and enabled.</summary>
        public static bool KeyToPanel(bool inBox, bool hasButton, bool enabled)
        {
            return inBox && hasButton && enabled;
        }

        /// <summary>True when the mod's own interact key may act (a door, a car): never while the box shows a button on that key.</summary>
        public static bool KeyFree(bool boxUp, bool boxHasButtonOnKey)
        {
            return !(boxUp && boxHasButtonOnKey);
        }

        /// <summary>
        /// Metres a point stands outside the nearest of the given reaches (negative inside one); a large number when
        /// there are none. The reaches are circles on the flat: a middle and a radius in metres.
        /// </summary>
        public static float Room(float x, float z, float[] busyX, float[] busyZ, float[] busyReach, int busy)
        {
            float least = float.MaxValue;
            for (int i = 0; i < busy; i++)
            {
                float dx = x - busyX[i];
                float dz = z - busyZ[i];
                float room = (float)System.Math.Sqrt(dx * dx + dz * dz) - busyReach[i];
                if (room < least)
                {
                    least = room;
                }
            }
            return least;
        }

        /// <summary>
        /// Of places to stand, given in order of preference, the first with <see cref="StandClear"/> metres of room
        /// outside every reach; if none has, the one with the most room (the earlier on a tie); -1 when there are none.
        /// </summary>
        public static int Pick(float[] x, float[] z, int count, float[] busyX, float[] busyZ, float[] busyReach, int busy)
        {
            int best = -1;
            float bestRoom = float.MinValue;
            for (int i = 0; i < count; i++)
            {
                float room = Room(x[i], z[i], busyX, busyZ, busyReach, busy);
                if (room >= StandClear)
                {
                    return i;
                }
                if (room > bestRoom)
                {
                    bestRoom = room;
                    best = i;
                }
            }
            return best;
        }
    }
}
