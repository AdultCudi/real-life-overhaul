namespace RealLifeOverhaul
{
    /// <summary>One car as a steady turn sees it, game-free: <see cref="HandlingTurn"/> holds it in a turn as the game's physics does.</summary>
    public sealed class TurnCar
    {
        public float Mass;             // kg
        public float Height;           // m: the centre of mass above the road at rest
        public float A;                // m from the centre of mass to the front axle
        public float B;                // and to the rear axle
        public float TrackF;           // m between an axle's wheels
        public float TrackR;
        public float PointF;           // m above the road where the game applies an axle's tyre forces (its force application point)
        public float PointR;
        public float RollF;            // N m per radian of body roll an axle's springs and anti-roll bar hold
        public float RollR;
        public float LoadF;            // N on one of the axle's wheels at rest
        public float LoadR;
        public float GripF;            // the wheels' grip to the side (the game's: 1.3 front, 1.5 rear)
        public float GripR;
        public float StiffF;           // and their slip stiffness to the side (the game's: 1.27 front, 1.7 rear)
        public float StiffR;
        public float RatingF;          // N: the tyres' load rating
        public float RatingR;
        public float Lock;             // degrees: the steering's most
        public float TyreScale = 1f;   // the side force curve's level against its keys
        public CurveKeys Tyre;         // the side force by slip, both axles
        public CurveKeys Steer;        // the steering's share of the lock by speed over 50 m/s

        public float Wheelbase => A + B;
    }

    /// <summary>A steady turn: what <see cref="HandlingTurn.Steady"/> found.</summary>
    public struct TurnState
    {
        public float G;                // the lateral acceleration, g
        public float Radius;           // m
        public float SlipF;            // degrees of slip angle at the front axle, and the rear
        public float SlipR;
        public float CurveF;           // the furthest along its curve a front tyre is (the inner wheel's, in the game's slip units)
        public float CurveR;
        public float Roll;             // degrees of body roll
        public float Least;            // the lightest wheel's load against its load at rest
        public bool Limited;           // the tyres ran out before the key's angle was reached: the car runs wide
    }

    /// <summary>
    /// A car holding a steady turn with the steering key held, as the game's vehicle physics works it: the lock times its
    /// curve by speed, the game's Ackermann sum, each tyre's side force from its curve at its slip, and the body rolling
    /// about the force points as each axle's share of the roll stiffness moves the weight out. Game-free; a turn is a few
    /// thousand curve readings, run when the player gets in and in NavCheck, never per frame.
    /// </summary>
    public static class HandlingTurn
    {
        public const float SideFactor = 1.9f;      // the game's tyre: side force = curve x 1.9 x load x grip
        public const float SlipDegrees = 90f;      // the game's side slip of 1 is 90 degrees of slip angle
        public const float LoadSoftening = 0.4f;   // the game's tyre: its slip falls by this times its load over its rating
        public const float SteerTop = 50f;         // m/s: the game's steering curves end at this speed
        private const float Radian = 57.29578f;    // degrees in a radian
        private const float MostSlip = 89f;        // degrees of slip angle: a tyre sideways on

        /// <summary>The steering's angle in degrees with the key held, at a speed in m/s: the lock times the curve.</summary>
        public static float SteerInput(TurnCar c, float speed)
        {
            return c.Lock * c.Steer.Evaluate(speed / SteerTop);
        }

        /// <summary>The front wheels' mean angle in degrees for a steering angle, by the game's own Ackermann sum (Steering.cs).</summary>
        public static float WheelAngle(float input, float track, float wheelbase)
        {
            float f = input / Radian;
            if (f < 1e-6f || !(wheelbase > 0.001f))
            {
                return input;
            }
            float sin = (float)System.Math.Sin(f);
            float cos = (float)System.Math.Cos(f);
            float inner = (float)System.Math.Atan(4f * track * sin / (2f * wheelbase * cos - track * sin));
            float outer = (float)System.Math.Atan(4f * track * sin / (2f * wheelbase * cos + track * sin));
            return 0.5f * (inner + outer) * Radian;
        }

        /// <summary>One tyre's side force in N at a slip angle in degrees, as the game's wheel sums it.</summary>
        public static float Force(TurnCar c, float load, float grip, float stiff, float rating, float slipDegrees)
        {
            if (!(load > 0f))
            {
                return 0f;
            }
            float share = load / rating;
            share = share > 1f ? 1f : share;
            float slip = slipDegrees / SlipDegrees * stiff * (1f - LoadSoftening * share);
            return c.Tyre.Evaluate(slip) * c.TyreScale * SideFactor * (load < rating ? load : rating) * grip;
        }

        /// <summary>Where along its curve a tyre is at a slip angle (the game's slip units).</summary>
        public static float CurveAt(float load, float stiff, float rating, float slipDegrees)
        {
            float share = load > 0f ? load / rating : 0f;
            share = share > 1f ? 1f : share;
            return slipDegrees / SlipDegrees * stiff * (1f - LoadSoftening * share);
        }

        /// <summary>Degrees of body roll per g of cornering.</summary>
        public static float RollPerG(TurnCar c)
        {
            float weight = c.Mass * HandlingModel.G;
            float arm = Arm(c);
            float hold = c.RollF + c.RollR - weight * arm;
            return hold > 1f ? weight * arm / hold * Radian : 90f;
        }

        /// <summary>The height of the centre of mass over where the tyres' forces act, m: what the cornering leans the body by.</summary>
        public static float Arm(TurnCar c)
        {
            return c.Height - (c.B * c.PointF + c.A * c.PointR) / c.Wheelbase;
        }

        /// <summary>
        /// The cornering in g at which the car would roll over: half its track, weighted by where its weight sits, against
        /// the arm, the body leaning out <paramref name="overshoot"/> times its steady lean. 99 with the forces at or above
        /// the centre of mass.
        /// </summary>
        public static float Rollover(TurnCar c, float overshoot)
        {
            float arm = Arm(c);
            if (!(arm > 0.01f))
            {
                return 99f;
            }
            float half = 0.5f * (c.B * c.TrackF + c.A * c.TrackR) / c.Wheelbase;
            float rate = RollPerG(c) / Radian;
            float g = half / arm;
            for (int i = 0; i < 30; i++)
            {
                float lean = rate * g * overshoot;
                lean = lean < 1.5f ? lean : 1.5f;
                g = (half - (float)System.Math.Sin(lean) * arm) / arm;
                g = g > 0f ? g : 0f;
            }
            return g;
        }

        /// <summary>The most lateral g the car's tyres hold at all, at any slip, the weight moving out as it corners.</summary>
        public static float Hold(TurnCar c)
        {
            float low = 0f;
            float high = 60f;
            for (int i = 0; i < 22; i++)
            {
                float ay = 0.5f * (low + high);
                Loads(c, ay, out float frontOut, out float frontIn, out float rearOut, out float rearIn, out _);
                float l = c.Wheelbase;
                bool held = MostAxle(c, frontOut, frontIn, c.GripF, c.StiffF, c.RatingF, out _) >= c.Mass * ay * c.B / l
                            && MostAxle(c, rearOut, rearIn, c.GripR, c.StiffR, c.RatingR, out _) >= c.Mass * ay * c.A / l;
                if (held)
                {
                    low = ay;
                }
                else
                {
                    high = ay;
                }
            }
            return low / HandlingModel.G;
        }

        /// <summary>
        /// The steady turn at <paramref name="speed"/> m/s with the steering key held, or the most the tyres hold if they run
        /// out first (Limited). False if the car holds no turn at that speed.
        /// </summary>
        public static bool Steady(TurnCar c, float speed, out TurnState st)
        {
            st = default;
            if (!(speed > 0.5f))
            {
                return false;
            }
            float wheel = WheelAngle(SteerInput(c, speed), c.TrackF, c.Wheelbase);
            float most = Hold(c) * HandlingModel.G;
            if (!(most > 0f))
            {
                return false;
            }
            if (Needs(c, speed, most, out float atMost, out st) && atMost <= wheel)
            {
                st.Limited = true;   // the key asks more than the tyres hold: the car turns at their most and runs wide
                return true;
            }
            float low = 0f;
            float high = most;
            for (int i = 0; i < 26; i++)
            {
                float ay = 0.5f * (low + high);
                if (Needs(c, speed, ay, out float angle, out _) && angle <= wheel)
                {
                    low = ay;
                }
                else
                {
                    high = ay;
                }
            }
            return low > 0f && Needs(c, speed, low, out _, out st);
        }

        // The wheel angle a turn at lateral acceleration ay asks at a speed, and the turn's state; false if the tyres cannot hold it.
        private static bool Needs(TurnCar c, float speed, float ay, out float angle, out TurnState st)
        {
            st = default;
            angle = 0f;
            Loads(c, ay, out float frontOut, out float frontIn, out float rearOut, out float rearIn, out float roll);
            float l = c.Wheelbase;
            float slipF = Slip(c, frontOut, frontIn, c.GripF, c.StiffF, c.RatingF, c.Mass * ay * c.B / l);
            float slipR = Slip(c, rearOut, rearIn, c.GripR, c.StiffR, c.RatingR, c.Mass * ay * c.A / l);
            if (slipF < 0f || slipR < 0f)
            {
                return false;
            }
            float radius = speed * speed / ay;
            angle = l / radius * Radian + slipF - slipR;
            st.G = ay / HandlingModel.G;
            st.Radius = radius;
            st.SlipF = slipF;
            st.SlipR = slipR;
            st.CurveF = CurveAt(frontIn, c.StiffF, c.RatingF, slipF);
            st.CurveR = CurveAt(rearIn, c.StiffR, c.RatingR, slipR);
            st.Roll = roll * Radian;
            float leastF = c.LoadF > 1f ? frontIn / c.LoadF : 1f;
            float leastR = c.LoadR > 1f ? rearIn / c.LoadR : 1f;
            st.Least = leastF < leastR ? leastF : leastR;
            return true;
        }

        // Each wheel's load cornering at ay m/s2, each axle taking its share of the roll stiffness; an outer wheel carries
        // at most the whole axle once the inner one lifts.
        private static void Loads(TurnCar c, float ay, out float frontOut, out float frontIn, out float rearOut, out float rearIn, out float roll)
        {
            float weight = c.Mass * HandlingModel.G;
            float arm = Arm(c);
            float hold = c.RollF + c.RollR - weight * arm;
            roll = hold > 1f ? c.Mass * ay * arm / hold : 3f;
            float moveF = c.RollF * roll / c.TrackF;
            float moveR = c.RollR * roll / c.TrackR;
            moveF = moveF < c.LoadF ? moveF : c.LoadF;   // an inner wheel off the road moves no more of the axle's weight out
            moveR = moveR < c.LoadR ? moveR : c.LoadR;
            frontOut = c.LoadF + moveF;
            frontIn = c.LoadF - moveF;
            rearOut = c.LoadR + moveR;
            rearIn = c.LoadR - moveR;
        }

        // The least slip angle in degrees at which an axle's two tyres give the force, on the rising side; -1 if they cannot.
        private static float Slip(TurnCar c, float outer, float inner, float grip, float stiff, float rating, float want)
        {
            if (MostAxle(c, outer, inner, grip, stiff, rating, out float atMost) < want)
            {
                return -1f;
            }
            float low = 0f;
            float high = atMost;
            for (int i = 0; i < 28; i++)
            {
                float middle = 0.5f * (low + high);
                if (Force(c, outer, grip, stiff, rating, middle) + Force(c, inner, grip, stiff, rating, middle) < want)
                {
                    low = middle;
                }
                else
                {
                    high = middle;
                }
            }
            return high;
        }

        // The most an axle's two tyres give at any slip angle (sampled every 3 degrees), and the least angle it comes at.
        private static float MostAxle(TurnCar c, float outer, float inner, float grip, float stiff, float rating, out float atAngle)
        {
            float most = 0f;
            atAngle = MostSlip;
            for (float a = 3f; a < MostSlip; a += 3f)
            {
                float f = Force(c, outer, grip, stiff, rating, a) + Force(c, inner, grip, stiff, rating, a);
                if (f > most)
                {
                    most = f;
                    atAngle = a;
                }
            }
            float side = Force(c, outer, grip, stiff, rating, MostSlip) + Force(c, inner, grip, stiff, rating, MostSlip);
            if (side > most)
            {
                most = side;
                atAngle = MostSlip;
            }
            return most;
        }
    }
}
