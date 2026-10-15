using System;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The taped rectangle of a cordon (Traffic/SceneCordon), in the road's own frame at the scene: metres along the road
    /// from the scene (Back to Front) and across it (Left to Right). Nothing of the game or the engine in it.
    /// </summary>
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
    /// Who is inside a cordon, and where they are put outside it, as plain geometry in the road's own frame (no engine
    /// types, so that every rule is run in a console test: RealLifeOverhaul.Build/NavCheck/CordonCheck.cs).
    /// When the tape goes up, anybody standing within it or against it (within the reach of the wall cut out of the
    /// navigation mesh along it: SideReach, and EndReach at an end, whose wall is deeper for its barriers), but not
    /// the scene's own people, is put at the nearest place outside: straight out through the nearest side, a step
    /// beyond that reach (SideClear, EndClear). The other three sides are the next places, nearest first, for when the first is not
    /// ground they can stand on. Two people are never put within Spacing of each other: whoever comes later is slid
    /// along the tape, a step either way in turn, so three people who stood on top of each other (and of somebody
    /// lying in the road) end up side by side at the tape, each a body's width from the next.
    /// The walkways through an opening in the tape (a crossing that runs through it) are found as points along the
    /// tape's line; Gaps joins the open points into runs, each a gate across the opening for people on foot.
    /// Called on the one frame the tape goes up (or is run again): a few dozen people, nothing allocated.
    /// </summary>
    public static class CordonRules
    {
        public const float SideReach = 0.55f;   // metres beyond a side's tape its wall's cut reaches: half the wall (0.15) and a walker's radius; nearer is against the tape
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

        /// <summary>
        /// The places outside the tape for somebody at a point, straight out through each side, nearest first (sides
        /// before ends on a tie, then left, right, back, front). Writes Exits places; returns the distance to the first.
        /// </summary>
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

        /// <summary>
        /// The nearest point outside the tape by a margin, straight out through the nearest side: for a place handed to
        /// somebody who only waits there. The point itself if it is outside already.
        /// </summary>
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

        /// <summary>
        /// Moves a place outside the tape along the tape (along the road beyond a side, across it beyond an end) until it
        /// is at least Spacing from every place already given out: as it is if it is, else a step one way, then the other,
        /// then two steps, up to SlideTries. A place tried that would be inside (Inside) is passed over. False, and the place
        /// as it was, if none is free.
        /// </summary>
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

        /// <summary>
        /// Joins the open points of a straight line of tape (open: a walkway crosses there and no wall stands on it) into
        /// runs, each reaching GatePad beyond its first and last open point and kept within the line. Point i stands
        /// i * GateStep along the line (the last one at its end). Runs that touch are one. With more runs than there
        /// are gates, the last gate reaches over all that remain, so nothing is left open. Returns how many runs.
        /// </summary>
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
    }
}
