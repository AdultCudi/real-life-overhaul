using System;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Making way for an emergency vehicle on a call, as plain arithmetic (NavCheck's MakeWayCheck runs every rule). A unit with its
    /// lights on that is held up behind something does these in turn: it passes by the lane beside it, then by a lane two over or
    /// a lane its map is less sure of (Widen); what is in its way that is the mod's own (the bus at a stop with no bay) stands clear
    /// or goes round at once when a lit unit comes up behind it (UnitComesUp); ordinary standing traffic in its way is moved on
    /// (Clear); a unit still held up near enough to its place stands where it is and its crew walks the rest (Stands); the first
    /// time it is held up that long it is said once in the log (Tell). Nothing allocated.
    /// </summary>
    public static class MakeWayRules
    {
        public const float ComesUpWithin = 90f;     // metres behind a vehicle within which a lit unit driving its way counts as coming up on it
        public const float ComesUpAside = 5f;       // metres off the vehicle's line the unit may be (its lane, or the next)
        public const float ComesUpFacing = 0.5f;    // cosine of the angle between the two headings (60 degrees): the same way, not across or against
        public const float RoundAfter = 2f;         // seconds a bus held behind something standing still goes round it when a lit unit waits behind (otherwise RideRules.RoundStillAfter)
        public const float WidenAfter = 2.5f;       // seconds a lit unit is blocked before it looks two lanes over and takes a lane its map is less sure of
        public const float WidenSnap = 2f;          // times the usual distance a lane point may lie from where a pass would run, once widened
        public const float TellAfter = 4f;          // seconds a lit unit is blocked before it is said once in the log
        public const float StandAfter = 9f;         // seconds a unit held up on its way to a scene stands where it is, its crew walking the rest
        public const float WalkMost = 120f;         // metres of its route left within which the crew walks the rest (a jog of about half a minute)
        public const float ClearUnseen = 1.5f;      // seconds behind ordinary standing traffic, out of view, before that is moved on
        public const float ClearSeen = 4f;          // ...in view
        public const float TowGivesUpAfter = 40f;   // seconds the tow truck stands held up before it gives the wreck up (the visit's own limit is far longer)

        /// <summary>
        /// True if a lit unit counts as coming up on a vehicle: behind it by up to ComesUpWithin, within ComesUpAside of its line and
        /// heading its way.
        /// </summary>
        /// <param name="behind">Metres the unit is behind the vehicle's middle along its heading (negative: ahead of it).</param>
        /// <param name="aside">Metres the unit is off the vehicle's line, either side.</param>
        /// <param name="facing">Cosine of the angle between the unit's heading and the vehicle's.</param>
        public static bool UnitComesUp(float behind, float aside, float facing)
        {
            return behind > 0f && behind <= ComesUpWithin && Math.Abs(aside) <= ComesUpAside && facing >= ComesUpFacing;
        }

        /// <summary>Seconds a bus held behind something that stands still waits before it goes round it: short when a lit unit waits behind it.</summary>
        public static float RoundWaits(float usual, bool unitBehind)
        {
            return unitBehind ? Math.Min(usual, RoundAfter) : usual;
        }

        /// <summary>True when a bus standing at a stop should stand clear: it is in the lane (no bay) and a lit unit comes up behind it.</summary>
        public static bool BusStandsClear(bool inBay, bool unitBehind)
        {
            return !inBay && unitBehind;
        }

        /// <summary>True from WidenAfter seconds of being blocked: a lit unit looks further for a way past.</summary>
        public static bool Widen(float heldFor)
        {
            return heldFor >= WidenAfter;
        }

        /// <summary>How many sideways ways past a lit unit tries, in order: one lane left, one right, and once widened two lanes left, two right. Others try the first two.</summary>
        public static int Choices(bool lit, float heldFor)
        {
            return lit && Widen(heldFor) ? 4 : 2;
        }

        /// <summary>The way past number <paramref name="choice"/>, in metres to the right of its line (negative: left), for a lane that wide.</summary>
        public static float Offset(int choice, float lane)
        {
            float lanes = choice >= 2 ? 2f : 1f;
            return (choice % 2 == 0 ? -lanes : lanes) * lane;
        }

        /// <summary>How far a lane point may lie from where a pass would run: the usual, and more once widened (a lit unit only).</summary>
        public static float LaneSnap(float usual, bool lit, float heldFor)
        {
            return lit && Widen(heldFor) ? usual * WidenSnap : usual;
        }

        /// <summary>True the first time a lit unit has been blocked long enough to say so.</summary>
        public static bool Tell(float heldFor, bool told)
        {
            return !told && heldFor >= TellAfter;
        }

        /// <summary>True when a unit held up for this long with this much of its route left stands where it is and its crew walks the rest.</summary>
        public static bool Stands(float heldFor, float routeLeft)
        {
            return heldFor >= StandAfter && routeLeft <= WalkMost;
        }

        /// <summary>True when ordinary traffic standing in a unit's way, held up behind it this long, is moved on: sooner out of view than in view.</summary>
        public static bool Clear(float heldFor, bool seen)
        {
            return heldFor >= (seen ? ClearSeen : ClearUnseen);
        }

        /// <summary>True when the tow truck, held up this long, gives the wreck up instead of waiting out the visit's own limit.</summary>
        public static bool TowGivesUp(float heldFor)
        {
            return heldFor >= TowGivesUpAfter;
        }
    }
}
