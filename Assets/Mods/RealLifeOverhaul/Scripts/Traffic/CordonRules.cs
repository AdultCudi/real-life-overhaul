using System;

namespace RealLifeOverhaul
{
    /// <summary>The taped rectangle of a cordon in the road's frame at the scene: metres along the road (Back to Front) and across it (Left to Right).</summary>
    public readonly struct CordonBox
    {
        public readonly float Back, Front, Left, Right;

        public CordonBox(float back, float front, float left, float right)
        {
            Back = back;
            Front = front;
            Left = left;
            Right = right;
        }

        /// <summary>True if a point is inside the rectangle grown by a reach on every side (negative to shrink it).</summary>
        public bool Within(float along, float across, float reach)
        {
            return along > Back - reach && along < Front + reach && across > Left - reach && across < Right + reach;
        }
    }

    /// <summary>
    /// Who is inside a cordon and where they are put outside it, as plain geometry in the road's frame (NavCheck's CordonCheck
    /// runs every rule): anybody within or against the tape but the scene's own people is put straight out through the nearest
    /// side, nobody within Spacing of another. Gaps joins a line's open points into the runs a gate shuts. Nothing allocated.
    /// </summary>
    public static class CordonRules
    {
        public const float SideReach = 0.55f;   // metres beyond a side's tape its wall's cut reaches (half the wall and a walker's radius)
        public const float EndReach = 1.6f;     // ...beyond an end's tape, whose wall reaches 1.22 m out over its barriers
        public const float SideClear = 1.1f;    // metres beyond a side's tape somebody is put: its reach and a step
        public const float EndClear = 1.9f;     // ...beyond an end's tape
        public const float Spacing = 0.75f;     // metres kept between two people put outside: shoulder to shoulder, never one on another
        public const int SlideTries = 8;        // places along the tape tried (a step either way in turn) for somebody whose first place is taken
        public const float GateStep = 0.5f;     // metres between the points of the tape's line looked at for a walkway through it
        public const float GatePad = 0.5f;      // metres a gate reaches beyond the walkway it shuts, either side
        public const int Exits = 4;             // places outside a point has: one through each side

        /// <summary>True if somebody at a point counts as inside the tape: within it, or against it (within its walls' reach).</summary>
        public static bool Inside(in CordonBox box, float along, float across)
        {
            return along > box.Back - EndReach && along < box.Front + EndReach && across > box.Left - SideReach && across < box.Right + SideReach;
        }

        /// <summary>The places outside the tape for a point, through each side, nearest first (sides before ends on a tie); returns the first's distance.</summary>
        /// <param name="alongSide">For each place: true if it is beyond a side (it is slid along the road), false beyond an end (slid across).</param>
        public static float Out(in CordonBox box, float along, float across, float[] alongs, float[] acrosses, bool[] alongSide)
        {
            // Four places: each time the nearest side not yet given (the first of a tie in the fixed order).
            int given = 0;
            float first = 0f;
            for (int i = 0; i < Exits; i++)
            {
                int side = -1;
                float best = float.MaxValue;
                for (int s = 0; s < Exits; s++)
                {
                    float far = Far(box, s, along, across);
                    if ((given & (1 << s)) == 0 && (side < 0 || far < best))
                    {
                        side = s;
                        best = far;
                    }
                }
                given |= 1 << side;
                first = i == 0 ? best : first;
                alongs[i] = side == 2 ? box.Back - EndClear : side == 3 ? box.Front + EndClear : along;
                acrosses[i] = side == 0 ? box.Left - SideClear : side == 1 ? box.Right + SideClear : across;
                alongSide[i] = side < 2;
            }
            return first;
        }

        // Metres from a point to the place beyond side s: 0 left, 1 right, 2 back, 3 front.
        private static float Far(in CordonBox box, int side, float along, float across)
        {
            switch (side)
            {
                case 0: return across - (box.Left - SideClear);
                case 1: return box.Right + SideClear - across;
                case 2: return along - (box.Back - EndClear);
                default: return box.Front + EndClear - along;
            }
        }

        /// <summary>The nearest point outside the tape by a margin, straight out through the nearest side; the point itself if outside already.</summary>
        public static void Outside(in CordonBox box, float along, float across, float margin, out float outAlong, out float outAcross)
        {
            outAlong = along;
            outAcross = across;
            if (!box.Within(along, across, margin))
            {
                return;
            }
            float toLeft = across - (box.Left - margin);
            float toRight = box.Right + margin - across;
            float toBack = along - (box.Back - margin);
            float toFront = box.Front + margin - along;
            float least = Math.Min(Math.Min(toLeft, toRight), Math.Min(toBack, toFront));
            if (least == toLeft)
            {
                outAcross = box.Left - margin;
            }
            else if (least == toRight)
            {
                outAcross = box.Right + margin;
            }
            else if (least == toBack)
            {
                outAlong = box.Back - margin;
            }
            else
            {
                outAlong = box.Front + margin;
            }
        }

        /// <summary>Slides a place outside the tape along it until it is Spacing from every place given out, up to SlideTries steps; false if none.</summary>
        public static bool Spread(in CordonBox box, bool alongSide, ref float along, ref float across, float[] takenAlong, float[] takenAcross, int taken)
        {
            if (!Inside(box, along, across) && Apart(along, across, takenAlong, takenAcross, taken))
            {
                return true;
            }
            for (int k = 1; k <= SlideTries; k++)
            {
                float step = ((k + 1) / 2) * Spacing * (k % 2 == 1 ? 1f : -1f);
                float a = alongSide ? along + step : along;
                float c = alongSide ? across : across + step;
                if (!Inside(box, a, c) && Apart(a, c, takenAlong, takenAcross, taken))
                {
                    along = a;
                    across = c;
                    return true;
                }
            }
            return false;
        }

        /// <summary>True if a place is at least Spacing from every place given out so far.</summary>
        public static bool Apart(float along, float across, float[] takenAlong, float[] takenAcross, int taken)
        {
            for (int i = 0; i < taken; i++)
            {
                float a = along - takenAlong[i];
                float c = across - takenAcross[i];
                if (a * a + c * c < Spacing * Spacing - 1e-4f)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>Joins a straight line's open points into runs reaching GatePad beyond them, touching runs as one; the last gate takes what is left.</summary>
        public static int Gaps(bool[] open, int count, float length, float[] from, float[] to, int most)
        {
            int runs = 0;
            for (int i = 0; i < count && most > 0; i++)
            {
                if (!open[i])
                {
                    continue;
                }
                int last = i;
                while (last + 1 < count && open[last + 1])
                {
                    last++;
                }
                float start = Math.Max(0f, At(i, count, length) - GatePad);
                float end = Math.Min(length, At(last, count, length) + GatePad);
                if (runs > 0 && start <= to[runs - 1])
                {
                    to[runs - 1] = Math.Max(to[runs - 1], end);
                }
                else if (runs < most)
                {
                    from[runs] = start;
                    to[runs] = end;
                    runs++;
                }
                else
                {
                    to[runs - 1] = end;   // no gate left: the last one is made to reach this far
                }
                i = last;
            }
            return runs;
        }

        /// <summary>How many points a line of tape of a length is looked at in (one every GateStep, both ends included).</summary>
        public static int Points(float length)
        {
            return length <= 0f ? 1 : (int)Math.Ceiling(length / GateStep) + 1;
        }

        /// <summary>Where along a line point i of Points(length) stands.</summary>
        public static float At(int index, int count, float length)
        {
            return index >= count - 1 ? Math.Max(0f, length) : index * GateStep;
        }

        // ---------------------------------------------------------------- the officer who waves a unit through

        public const float PassAhead = 45f;     // metres before the traffic post at which a unit coming up the open lane is waved through
        public const float PassBeyond = 3f;     // metres past the post its tail is before the officer goes back
        public const float PassMost = 8f;       // seconds a pass lasts at the most
        public const float PassClear = 0.3f;    // metres kept between where the officer stands aside and a passing unit's look ahead: a person's own width again
        public const float LookWider = 0.45f;   // metres a unit's look ahead reaches beyond its side for somebody on foot (EmergencyDriver: 0.15 and 0.3)
        public const float OwnLaneAhead = 5f;   // metres ahead of a unit's nose it still drives in its own lane before it has pulled out
        public const float LaneHalf = 1.75f;    // metres either side of a lane's line

        /// <summary>Where the officer on the traffic post is: on the post, letting a unit past, or called back to the car.</summary>
        public enum Pass { Post, Waving, Board }

        /// <summary>
        /// The officer's next state. On the post, a unit due is waved through (the post's own job paused); waving, it ends when
        /// the unit is past, and if the officers were called back meanwhile, they board then, not before.
        /// </summary>
        /// <param name="due">A unit has reached the open lane (or is leaving by it).</param>
        /// <param name="over">The unit is past, gone, or the pass has lasted its most.</param>
        /// <param name="recalled">The officers have been called back to their car.</param>
        public static Pass NextPass(Pass state, bool due, bool over, bool recalled)
        {
            switch (state)
            {
                case Pass.Post:
                    return recalled ? Pass.Board : due ? Pass.Waving : Pass.Post;
                case Pass.Waving:
                    return !over ? Pass.Waving : recalled ? Pass.Board : Pass.Post;
                default:
                    return Pass.Board;
            }
        }

        /// <summary>True if a unit coming up the open lane is to be waved through now: in that lane, facing along it, its nose within reach of the post.</summary>
        /// <param name="toPost">Metres from its nose to the post, along the way it faces.</param>
        /// <param name="fromLane">Metres from its middle to the open lane's line.</param>
        /// <param name="squarely">How squarely it faces along the road: 1 dead along it.</param>
        public static bool PassDue(float toPost, float fromLane, float squarely)
        {
            return toPost > -1f && toPost <= PassAhead && fromLane <= LaneHalf + 0.5f && squarely >= 0.7f;
        }

        /// <summary>True once a pass is over: the unit's tail is past the post, it has gone, or the most a pass lasts is up.</summary>
        public static bool PassOver(float tailPastPost, bool gone, float passFor)
        {
            return gone || tailPastPost >= PassBeyond || passFor >= PassMost;
        }

        /// <summary>Metres from a place to a way a unit drives (its line across the road), past the unit's side and its look ahead; negative inside.</summary>
        public static float Clearance(float placeAcross, float wayAcross, float unitHalfWidth)
        {
            return Math.Abs(placeAcross - wayAcross) - unitHalfWidth - LookWider;
        }

        /// <summary>
        /// Which place beside the open lane the officer takes: the first clear by PassClear of the open lane and, for a unit
        /// leaving from the scene, of its own lane where it still drives in it (just ahead of its nose); with none clear, the
        /// clearest. Places are in the road's frame (along, across); clearance gives the chosen one's.
        /// </summary>
        /// <param name="own">The unit leaves from its own lane at the scene (ownAcross, its nose at ownNose along the road).</param>
        public static int PassPlace(float[] along, float[] across, int places, float laneAcross, float unitHalfWidth, bool own, float ownAcross, float ownNose,
            float facing, out float clearance)
        {
            int best = -1;
            float most = float.MinValue;
            for (int i = 0; i < places; i++)
            {
                float clear = Clearance(across[i], laneAcross, unitHalfWidth);
                float ahead = (along[i] - ownNose) * (facing >= 0f ? 1f : -1f);
                if (own && ahead > -1f && ahead < OwnLaneAhead)
                {
                    clear = Math.Min(clear, Clearance(across[i], ownAcross, unitHalfWidth));
                }
                if (clear >= PassClear)
                {
                    clearance = clear;
                    return i;
                }
                if (clear > most)
                {
                    most = clear;
                    best = i;
                }
            }
            clearance = most;
            return best;
        }
    }
}
