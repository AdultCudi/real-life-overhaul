namespace RealLifeOverhaul.GameUi
{
    /// <summary>One panel's claim on the game's single panel box (OverlayUI), measured from the player when two panels want it.</summary>
    public struct PanelClaim
    {
        public float Distance;   // metres on the flat from the player to the place the panel is for
        public float Facing;     // cosine between the player's facing and the way to the place: 1 ahead, -1 behind
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
    /// Who has the game's one panel box when the player is near two places that want it. The game sends the interact key
    /// to whatever is in the box, so the panel in it is the one the key runs; the others wait.
    ///
    ///  - Each place counts by its distance on the flat, longer the further it is from the player's facing: straight
    ///    behind twice as far (<see cref="BehindWeight"/>). A person come to meet counts nearest of all within
    ///    <see cref="TalkRange"/>.
    ///  - A panel takes the box only when it counts nearer by a margin (<see cref="KeepMetres"/> or <see cref="KeepShare"/>
    ///    of the other's count, whichever is more), so the box never goes to and fro while the player stands still.
    ///  - The game's own panels (filling station, repair bay, private driver's garage, lift) come with the trigger the
    ///    player stands in: nearest, never taken from. Another mod's panel is left alone too.
    ///
    /// A panel's button runs only while it is in the box (<see cref="KeyToPanel"/>), and the mod's own interact key does
    /// nothing while the box shows a button on it (<see cref="KeyFree"/>): one press runs one thing. A person waiting to be
    /// met stands where nothing else wants the box if the street allows (<see cref="Pick"/>).
    ///
    /// Game-free arithmetic, nothing allocated. Checked offline (NavCheck PanelPickCheck).
    /// </summary>
    public static class PanelPickRules
    {
        public const float TalkRange = 3.5f;     // metres: a person met counts nearest from this close; an outing's hello is offered from 3.4 m
        public const float BehindWeight = 1f;    // a place straight behind counts this much farther again: twice its distance
        public const float KeepMetres = 0.75f;   // counted metres a panel must be nearer than the one in the box: about a step...
        public const float KeepShare = 0.2f;     // ...or this share of the other's count, whichever is more, so a sway of the body is no turn
        public const float AtPlace = 0.3f;       // metres within which the player stands at the place: their facing is not asked
        public const float StandClear = 0.6f;    // metres a waiting person stands outside every other panel's reach, where the street allows

        /// <summary>A claim's counted metres, what is behind the player made longer; 0 for the nearest of all.</summary>
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

        /// <summary>The counted metres to beat a box held at <paramref name="held"/>.</summary>
        public static float Margin(float held)
        {
            float share = KeepShare * (held > 0f ? held : 0f);
            return share > KeepMetres ? share : KeepMetres;
        }

        /// <summary>The asking panel takes the box from the one in it.</summary>
        public static bool Takes(in PanelClaim holder, in PanelClaim asking)
        {
            if (holder.Game)
            {
                return false;
            }
            float held = Counted(holder);
            return Counted(asking) < held - Margin(held);
        }

        /// <summary>A panel's button may run on its key: only the panel in the box, with that button there and enabled.</summary>
        public static bool KeyToPanel(bool inBox, bool hasButton, bool enabled)
        {
            return inBox && hasButton && enabled;
        }

        /// <summary>The mod's own interact key may act (a door, a car): never while the box shows a button on that key.</summary>
        public static bool KeyFree(bool boxUp, bool boxHasButtonOnKey)
        {
            return !(boxUp && boxHasButtonOnKey);
        }

        /// <summary>
        /// Metres a point stands outside the nearest reach (negative inside one; very large with none). Reaches are circles on
        /// the flat: a middle and a radius in metres.
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
        /// Of places to stand in order of preference, the first with <see cref="StandClear"/> metres outside every reach; else
        /// the one with most room, the earlier on a tie; -1 when there are none.
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
