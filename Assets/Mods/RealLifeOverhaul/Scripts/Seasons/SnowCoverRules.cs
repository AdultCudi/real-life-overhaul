using System;

namespace RealLifeOverhaul.Seasons
{
    /// <summary>Where the lying snow's square is and how it moves: its middle, its half side, and the player's last place and pace.</summary>
    public struct SnowCoverState
    {
        public float Cx, Cy, Cz;   // the middle of the square, at the player's feet when it was laid
        public float Half;         // metres from the middle to each side
        public float Lx, Ly, Lz;   // the player's place in the last frame
        public float Vx, Vz;       // the player's pace over the ground, metres a second, smoothed
        public bool Laid;
    }

    /// <summary>
    /// The lying snow's square round the player, game-free (checked by EconomyCheck): how far the camera shows the ground
    /// (<see cref="Reach"/>), how big the square must be for that, and when and where it moves. It is looked at every
    /// frame, before the frame is drawn, and moved the moment the player could see past its edge, put ahead of them along
    /// the way they go; a few float operations a frame, nothing allocated.
    /// </summary>
    public static class SnowCoverRules
    {
        public const float ReachLeast = 60f;    // metres from the player the square reaches at the least
        public const float ReachMost = 300f;    // and at the most: the street camera's farthest ground, and the horizon's cap
        public const float Margin = 40f;        // metres the player may go before the square moves
        public const float LeadSeconds = 1.5f;  // the square is put this far ahead along the player's way
        public const float LeadShare = 0.8f;    //   but never more than this share of the margin, so it never moves again at once
        public const float SpanStep = 25f;      // the half side changes in these steps,
        public const float ShrinkAfter = 50f;   //   and shrinks only when this much too big
        public const float Rise = 4f;           // metres up or down the player may go before the square moves
        public const float PaceMost = 80f;      // metres a second: faster is a jump (a door, a load), not a pace
        private const float PaceEase = 6f;      // the pace follows the last frames' over about a sixth of a second
        private const float EyeLeast = 0.5f;

        /// <summary>
        /// Metres from the player to the farthest ground the camera shows: its top corners' rays to the ground at the
        /// player's feet, from <paramref name="eyeAbove"/> metres up and <paramref name="eyeAway"/> metres across, looking
        /// down by an angle whose sine is <paramref name="lookDown"/> and cosine <paramref name="lookFlat"/>, through a lens
        /// whose half angles' tangents are <paramref name="tanHalfHigh"/> and <paramref name="tanHalfWide"/>. A ray at or over
        /// the horizon reaches the cap.
        /// </summary>
        public static float Reach(float eyeAbove, float eyeAway, float lookDown, float lookFlat, float tanHalfHigh, float tanHalfWide)
        {
            if (!(eyeAbove >= EyeLeast))
            {
                eyeAbove = EyeLeast;   // a camera at or under the feet: taken as just over them
            }
            if (!(eyeAway >= 0f))
            {
                eyeAway = 0f;
            }
            float down = lookDown - tanHalfHigh * lookFlat;    // the top edge's ray, how much it falls for each metre along the look
            float along = lookFlat + tanHalfHigh * lookDown;   //   and how far it goes over the ground for it
            float across = (float)Math.Sqrt(along * along + tanHalfWide * tanHalfWide);
            if (!(down > 0f) || eyeAbove * across >= (ReachMost - eyeAway) * down)
            {
                return ReachMost;
            }
            float reach = eyeAway + eyeAbove * across / down;
            return reach < ReachLeast ? ReachLeast : reach;
        }

        /// <summary>The half side for <paramref name="reach"/>: grown at once, shrunk only when well too big, in steps.</summary>
        public static float HalfFor(float reach, float current)
        {
            float want = (float)Math.Ceiling((Math.Min(reach, ReachMost) + Margin) / SpanStep) * SpanStep;
            return current <= 0f || want > current || want < current - ShrinkAfter ? want : current;
        }

        /// <summary>Metres the square reaches from the player at the least, whichever way they look.</summary>
        public static float Covered(in SnowCoverState s, float px, float pz)
        {
            return s.Half - Math.Max(Math.Abs(px - s.Cx), Math.Abs(pz - s.Cz));
        }

        /// <summary>The square must move: the player could see past its edge, or has gone up or down too far.</summary>
        public static bool Moves(in SnowCoverState s, float px, float py, float pz, float reach)
        {
            return !s.Laid || Covered(s, px, pz) < reach || Math.Abs(py - s.Cy) > Rise;
        }

        /// <summary>The square laid round the player afresh (first shown, or after a jump): no pace known yet.</summary>
        public static void Lay(ref SnowCoverState s, float px, float py, float pz, float reach)
        {
            s.Half = HalfFor(reach, s.Half);
            s.Cx = px;
            s.Cy = py;
            s.Cz = pz;
            s.Lx = px;
            s.Ly = py;
            s.Lz = pz;
            s.Vx = s.Vz = 0f;
            s.Laid = true;
        }

        /// <summary>
        /// One frame: the player's pace followed, the square's size fitted to <paramref name="reach"/>, and the square moved
        /// ahead of the player when they could see past its edge. True when it moved (or its size changed): the projector is
        /// then placed again.
        /// </summary>
        public static bool Step(ref SnowCoverState s, float px, float py, float pz, float dt, float reach)
        {
            if (!s.Laid)
            {
                Lay(ref s, px, py, pz, reach);
                return true;
            }
            if (dt > 0f)
            {
                float vx = (px - s.Lx) / dt, vz = (pz - s.Lz) / dt;
                if (vx * vx + vz * vz > PaceMost * PaceMost)
                {
                    s.Vx = s.Vz = 0f;   // a jump: the square is laid round where the player came out
                }
                else
                {
                    float ease = Math.Min(1f, dt * PaceEase);
                    s.Vx += (vx - s.Vx) * ease;
                    s.Vz += (vz - s.Vz) * ease;
                }
            }
            s.Lx = px;
            s.Ly = py;
            s.Lz = pz;
            float half = HalfFor(reach, s.Half);
            bool resized = half != s.Half;
            s.Half = half;
            if (!Moves(s, px, py, pz, reach))
            {
                return resized;
            }
            Ahead(s.Vx, s.Vz, half, reach, out float ax, out float az);
            s.Cx = px + ax;
            s.Cy = py;
            s.Cz = pz + az;
            return true;
        }

        /// <summary>How far ahead of the player the square's middle is put: along their way, never so far it must move again at once.</summary>
        public static void Ahead(float vx, float vz, float half, float reach, out float ax, out float az)
        {
            float pace = (float)Math.Sqrt(vx * vx + vz * vz);
            float room = (half - reach) * LeadShare;
            if (pace < 0.5f || room <= 0f)
            {
                ax = az = 0f;
                return;
            }
            float lead = Math.Min(Math.Min(pace, PaceMost) * LeadSeconds, room);
            ax = vx / pace * lead;
            az = vz / pace * lead;
        }
    }
}
