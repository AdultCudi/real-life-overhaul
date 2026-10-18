namespace RealLifeOverhaul
{
    /// <summary>One axle of a car as it is read from the game: the car's own space in metres, figures per wheel.</summary>
    public sealed class AxleData
    {
        public float Z;               // where the axle stands along the car (forward is +)
        public float Track;           // between its two wheels
        public float Top;             // height of the spring tops (the wheel controllers) in the car's own space
        public float Radius;
        public float SpringForce;     // the game's spring: its force fully compressed, N
        public float SpringLength;    // and its length, m (the game's springs are straight lines between the two)
        public float SideGrip;        // the game's tyre grip to the side (1.3 front, 1.5 rear on its own cars)
        public float AlongGrip;       // and along (0.9 front, 1.5 rear)
        public float LoadRating;      // N: the tyre's rated load
        public float AntiRoll;        // N per m of difference between the axle's two springs
        public float BrakeShare;      // the game's brake coefficient for the axle
        public float Bump;            // N s/m: the game's damper rates and their low-speed factors
        public float Rebound;
        public float SlowBump;
        public float SlowRebound;
        public float Application;     // the game's force application point distance (a share of the spring's length)
    }

    /// <summary>What <see cref="HandlingModel.Design"/> needs to know about one car, read once when the player gets in.</summary>
    public sealed class CarChassis
    {
        public float Mass;            // kg, the game's
        public float ComY;            // the centre of mass in the car's own space
        public float ComZ;
        public float BrakeTorque;     // N m: the most brake torque a wheel is given, the game's
        public float SteerLock;       // degrees: the game's most steer angle
        public readonly AxleData Front = new AxleData();
        public readonly AxleData Rear = new AxleData();
    }

    /// <summary>What the handling puts on one axle (per wheel where it says so), and what that axle should then do.</summary>
    public sealed class AxleSetup
    {
        public float SpringForce;     // N fully compressed, at the end of the bump stop
        public float SpringLength;    // m
        public readonly float[] CurveAt = new float[HandlingModel.CurveKeys];      // the spring's curve: compression (share of the length)...
        public readonly float[] CurveShare = new float[HandlingModel.CurveKeys];   // ...to force (share of SpringForce), straight between
        public float Bump;            // N s/m below the damper's knee
        public float Rebound;
        public float Knee;            // m/s
        public float Fast;            // share of the rate above the knee
        public float Application;     // the force application point distance: the roll centre over the spring's length
        public float AntiSquat;       // the game's anti-squat figure: below zero the body pitches more under braking and pull
        public float LoadRating;      // N
        public float Rolling;         // N m of rolling resistance
        public float AntiRoll;        // N/m
        public float BrakeShare;      // the axle's brake coefficient
        public float TyreB;           // the axle's friction preset: the magic formula's figures for the side force...
        public float TyreC;
        public float TyreSide;
        public float TyreE;
        public float TyreAlong;       // ...and its peak along, which the game reads apart from the curve
        public float SideStiffness;   // the wheels' side slip stiffness

        public float Load;            // N per wheel at rest
        public float Rate;            // N/m of the spring before its bump stop
        public float Sag;             // m the spring is compressed at rest
        public float RestLength;      // m left to compress at rest: the bump travel
        public float Raised;          // m the car stands higher on this axle than the game had it, for travel
        public float Frequency;       // Hz
        public float Zeta;            // the damping ratio designed
        public float PeakSlip;        // the side force's peak, in the game's slip units
        public float PeakDegrees;     // and in degrees of slip angle
        public float SideG;           // the axle's grip to the side on the game's own tyres, g
        public float AlongG;          // and along
    }

    /// <summary>Everything the handling puts on one car, and what the model says it does.</summary>
    public sealed class HandlingSetup
    {
        public float Mass;            // kg
        public float ComY;            // the centre of mass's height in the car's own space
        public float ComHeight;       // m above the road at rest
        public float ComHeightBefore; // the game's, on the game's springs
        public readonly AxleSetup Front = new AxleSetup();
        public readonly AxleSetup Rear = new AxleSetup();
        public readonly float[] SteerAt = new float[HandlingModel.SteerKeys];      // speed over 50 m/s, as the game's curve reads it
        public readonly float[] SteerShare = new float[HandlingModel.SteerKeys];   // share of the lock at that speed
        public readonly float[] SmoothAt = new float[2];
        public readonly float[] SmoothTime = new float[2];   // s
        public float BrakeRise;       // s for the eased brake pedal from nothing to full
        public float RollCentre;      // m above the road
        public float RollPerG;        // degrees
        public float DivePerG;        // degrees of nose dive per g of braking (the same tail squat per g of pull)
        public float BrakeG;          // the deceleration the full pedal gives, g
        public float BrakeTarget;     // the full pedal's deceleration the kind is built for, g
        public float FrontLag;        // how much more of its tyres the front asks at its worst moment as the brakes come on than once settled
        public float RearLag;         // and the rear, at the deepest of the dive
        public float BrakeUse;        // the most of its tyres' grip along either axle asks for at the full pedal, at its worst moment
        public float TipG;            // the cornering at which the car would tip, g
    }

    /// <summary>
    /// The driven car's handling as the game's own vehicle physics (NWH Vehicle Physics 2) works it, game-free: a quarter
    /// car on the game's spring and two-rate damper, stepped as the physics steps it; the roll and the pitch of the body on
    /// its springs and anti-roll bars about the point where the game applies the tyres' forces; the tyres' magic formula
    /// as the game reads it (the side force from the preset's curve, the force along from its peak figure); the steering
    /// by speed; the brakes from the axle loads at the design deceleration, each axle's at its worst moment as the body
    /// dives, and the stop they give. <see cref="Design"/> turns a kind's
    /// <see cref="HandlingTarget"/> into one car's values. Allocation-free once the setup exists; runs once per car and
    /// change of an option, never per frame.
    /// </summary>
    public static class HandlingModel
    {
        public const float G = 9.81f;                 // m/s2, the game's gravity
        public const float Step = 0.02f;              // s: the game's physics step
        public const float SideFactor = 1.9f;         // the game's tyre: side force = curve x 1.9 x load x grip
        public const float AlongFactor = 1.35f;       // and the most along = the preset's D x 1.35 x load x grip
        public const float GameFrontSide = 1.3f;      // the game's own front tyres' grip to the side, on every one of its cars
        public const float GameFrontAlong = 0.9f;     // and along
        public const float SlipDegrees = 90f;         // the game's side slip of 1 is 90 degrees of slip angle
        public const float LoadSoftening = 0.4f;      // the game's tyre: its slip stiffness falls by this times its load over its rating
        public const float MinTravel = 0.12f;         // m of bump travel at rest at the least: a kerb and 2 cm to spare
        public const float Kerb = 0.10f;              // m: the kerb every car climbs without its springs bottoming out
        public const float StopShare = 0.35f;         // the last share of the bump travel, where the bump stop works
        public const float StopRateFirst = 3f;        // times the spring's rate, over the first half of the bump stop
        public const float StopRateLast = 9f;         // and over the last half
        public const float ReboundRatio = 1.6f;       // rebound damping against bump: the body is let back up slower than it is pushed down
        public const float DamperKnee = 0.2f;         // m/s: the wobble's speeds are below it; a kerb's are above, where the damper softens
        public const float DamperFast = 0.5f;         // the rate's share above the knee
        public const float BounceStart = 0.03f;       // m: the disturbance the damping ratio is measured from
        public const float VisibleSwing = 0.005f;     // m: a swing of the body anyone sees
        public const float TyreCurvature = -2f;       // the magic formula's E: a road tyre's rounded rise, slope at nothing about twice the peak over its slip
        public const float SmoothAtRest = 0.05f;      // s: the game's own steering smoothing at a standstill
        public const float SteerTop = 50f;            // m/s: the game's speed curves end at this speed
        public const float RollOvershoot = 1.45f;     // a sudden turn swings the body this far past its steady lean, at this ride's damping
        public const float MostUse = 0.95f;           // the most of what its tyres hold along either axle brakes with, the body settled in its dive
        public const float MostPeak = 0.98f;          // and at its worst moment as the brakes come on: no wheel locks on a dry road
        public const float GameBrakeOnset = 0.1f;     // s: the game's own brakes.actuationTime on its cars
        public const float InputRate = 60f;           // frames a second the eased pedal moves at (the car's input runs once a frame)
        public const float StopFrom = 100f / 3.6f;    // m/s: the speed the stopping distances are measured from
        public const int CurveKeys = 4;
        public const int SteerKeys = 11;

        // m/s at which the steering curve is written: close together where the lock falls away.
        private static readonly float[] SteerSpeeds = { 0f, 2.5f, 5f, 7.5f, 10f, 13f, 17f, 22f, 30f, 40f, 50f };

        private const float Radian = 57.29578f;       // degrees in a radian

        private static readonly float[] Felt = new float[150];   // the brake felt at each physics step while an axle's dive is stepped (3 s)

        /// <summary>
        /// One car's handling for a target. Returns why the car cannot be given it (then nothing is to be changed), or
        /// null. Everything on <paramref name="s"/> is written.
        /// </summary>
        public static string Design(CarChassis car, in HandlingTarget t, HandlingSetup s)
        {
            AxleData f = car.Front;
            AxleData r = car.Rear;
            float wheelbase = f.Z - r.Z;
            float a = f.Z - car.ComZ;   // centre of mass to the front axle
            float b = car.ComZ - r.Z;   // and to the rear
            if (!(car.Mass > 100f) || !(wheelbase > 1f) || !(a > 0.1f) || !(b > 0.1f) || !(f.Track > 0.8f) || !(r.Track > 0.8f))
            {
                return "its axles are not where a car's are";
            }
            if (!Usable(f) || !Usable(r))
            {
                return "its springs or tyres are not a car's";
            }
            if (!(t.RideHz > 0.3f) || !(t.Damping > 0.02f) || !(t.SideGrip > 0.1f) || !(t.AlongGrip > 0.1f))
            {
                return "the target is not a car's";
            }

            float mass = car.Mass + (t.Mass - car.Mass) * t.MassShare;
            s.Mass = mass;
            float gameFront = car.Mass * G * b / (2f * wheelbase);   // N per wheel at rest
            float gameRear = car.Mass * G * a / (2f * wheelbase);
            Ride(f, mass * G * b / (2f * wheelbase), gameFront, t, s.Front);
            Ride(r, mass * G * a / (2f * wheelbase), gameRear, t, s.Rear);

            // The centre of mass over the new ride: the game's own, raised towards the kind's where the game's is lower.
            float ground = 0.5f * (f.Top - s.Front.RestLength - f.Radius + r.Top - s.Rear.RestLength - r.Radius);
            float gameGround = 0.5f * (f.Top - GameRest(f, gameFront) - f.Radius + r.Top - GameRest(r, gameRear) - r.Radius);
            s.ComHeightBefore = car.ComY - gameGround;
            float h = car.ComY - ground;
            if (t.ComHeight > h)
            {
                h += (t.ComHeight - h) * t.ComShare;
            }
            s.ComHeight = h;
            s.ComY = ground + h;

            Roll(car, t, s, mass, h);
            Pitch(car, t, s, mass, h, a, b);
            Tyres(car, t, s);
            Steering(car, t, s, wheelbase);
            Brakes(car, t, s, mass, wheelbase);
            s.TipG = TipG(car, s, s.Front.SideG > s.Rear.SideG ? s.Front.SideG : s.Rear.SideG);
            return null;
        }

        // The game's springs are straight lines: what is left of one at rest.
        private static float GameRest(AxleData axle, float load)
        {
            float share = load / axle.SpringForce;
            return axle.SpringLength * (1f - (share > 1f ? 1f : share));
        }

        private static bool Usable(AxleData axle)
        {
            return axle.SpringForce > 100f && axle.SpringLength > 0.05f && axle.Radius > 0.1f && axle.SideGrip > 0.05f && axle.AlongGrip > 0.05f
                   && axle.LoadRating > 100f;
        }

        // ---------------------------------------------------------------- the ride: springs, bump stop, dampers

        // One corner's spring and dampers for the ride frequency and damping ratio, the car standing as high as the
        // game had it (or higher, where the game left too little travel for a kerb).
        private static void Ride(AxleData axle, float load, float gameLoad, in HandlingTarget t, AxleSetup s)
        {
            float m = load / G;
            float w = 2f * (float)System.Math.PI * t.RideHz;
            float rate = w * w * m;
            float sag = load / rate;
            float gameRest = GameRest(axle, gameLoad);
            float rest = gameRest > MinTravel ? gameRest : MinTravel;
            float length = rest + sag;
            float stop = StopShare * rest;
            float atStop = rate * (length - stop);
            float atHalf = atStop + StopRateFirst * rate * stop * 0.5f;
            float atEnd = atHalf + StopRateLast * rate * stop * 0.5f;
            s.SpringForce = atEnd;
            s.SpringLength = length;
            s.CurveAt[0] = 0f;
            s.CurveShare[0] = 0f;
            s.CurveAt[1] = (length - stop) / length;
            s.CurveShare[1] = atStop / atEnd;
            s.CurveAt[2] = (length - stop * 0.5f) / length;
            s.CurveShare[2] = atHalf / atEnd;
            s.CurveAt[3] = 1f;
            s.CurveShare[3] = 1f;
            float mean = 2f * t.Damping * (float)System.Math.Sqrt(rate * m);   // N s/m for the damping ratio
            s.Bump = 2f * mean / (1f + ReboundRatio);
            s.Rebound = s.Bump * ReboundRatio;
            s.Knee = DamperKnee;
            s.Fast = DamperFast;
            s.LoadRating = axle.LoadRating * load / gameLoad;   // the tyre rated for the weight it carries, as the game rated its own
            s.Rolling = t.RollingShare * load * axle.Radius;
            s.Load = load;
            s.Rate = rate;
            s.Sag = sag;
            s.RestLength = rest;
            s.Raised = rest - gameRest;
            s.Frequency = t.RideHz;
            s.Zeta = t.Damping;
        }

        /// <summary>The spring's force at a compression (0 extended, 1 fully compressed), as the game evaluates its curve: straight between the keys.</summary>
        public static float SpringAt(AxleSetup s, float compression)
        {
            float c = compression < 0f ? 0f : compression > 1f ? 1f : compression;
            for (int i = 1; i < CurveKeys; i++)
            {
                if (c <= s.CurveAt[i])
                {
                    float span = s.CurveAt[i] - s.CurveAt[i - 1];
                    float share = span > 1e-6f ? s.CurveShare[i - 1] + (s.CurveShare[i] - s.CurveShare[i - 1]) * (c - s.CurveAt[i - 1]) / span : s.CurveShare[i];
                    return share * s.SpringForce;
                }
            }
            return s.SpringForce;
        }

        /// <summary>The game's two-rate damper: positive speed is compression, the force pushes the body up.</summary>
        public static float DamperAt(AxleSetup s, float speed)
        {
            if (speed > 0f)
            {
                float bump = speed < s.Knee ? speed : s.Knee + (speed - s.Knee) * s.Fast;
                return bump * s.Bump;
            }
            float up = -speed;
            float rebound = up < s.Knee ? up : s.Knee + (up - s.Knee) * s.Fast;
            return -rebound * s.Rebound;
        }

        /// <summary>
        /// One corner of the car let go from <see cref="BounceStart"/> pushed down, stepped as the game's physics steps it
        /// (spring and damper from the step's start, then the body's speed, then its place). Returns the damping ratio the
        /// swings decay at; <paramref name="swings"/> counts those anyone sees and <paramref name="after2s"/> is what is
        /// left of the motion two seconds on.
        /// </summary>
        public static float Bounce(AxleSetup s, float mass, out int swings, out float after2s)
        {
            float rest = SettledLength(s, mass);
            float length = rest - BounceStart;
            float previous = length;
            float speed = 0f;
            float last = -BounceStart;
            float lastPeak = 0f;
            int peaks = 0;
            float first = 0f;
            float third = 0f;
            swings = 0;
            after2s = 0f;
            float direction = 0f;
            for (int i = 0; i < 200; i++)
            {
                float force = Corner(s, length, (previous - length) / Step);
                previous = length;
                speed += (force / mass - G) * Step;
                length += speed * Step;
                float off = length - rest;
                float moving = off - last;
                if (direction != 0f && moving * direction < 0f)
                {
                    // the body turned: the step before was a swing's far point
                    peaks++;
                    lastPeak = last < 0f ? -last : last;
                    if (peaks == 1)
                    {
                        first = lastPeak;
                    }
                    else if (peaks == 3)
                    {
                        third = lastPeak;
                    }
                    if (lastPeak > VisibleSwing)
                    {
                        swings++;
                    }
                }
                if (moving != 0f)
                {
                    direction = moving;
                }
                last = off;
                if (i == 99)
                {
                    after2s = off < 0f ? -off : off;
                }
            }
            if (!(first > 0f) || !(third > 0f) || third >= first)
            {
                return third > 0f ? 0f : 1f;   // no full swing: damped through
            }
            float decrement = (float)System.Math.Log(first / third);
            return decrement / (float)System.Math.Sqrt(4f * System.Math.PI * System.Math.PI + decrement * decrement);
        }

        /// <summary>
        /// The corner settled at rest, then the road under its wheel rising <paramref name="height"/> at once (a kerb climbed,
        /// the game's wheel being solid): the least length the spring comes to. At zero or below it has bottomed out.
        /// </summary>
        public static float KerbLeft(AxleSetup s, float mass, float height)
        {
            float length = SettledLength(s, mass) - height;
            float previous = length + height;
            float speed = 0f;
            float least = length;
            for (int i = 0; i < 150; i++)
            {
                // Bottomed out, the game's wheel collider meets the road: a hard push, as a solid contact gives.
                float force = length <= 0f ? mass * G * 4f : Corner(s, length, (previous - length) / Step);
                previous = length;
                speed += (force / mass - G) * Step;
                length += speed * Step;
                least = length < least ? length : least;
            }
            return least;
        }

        // The force a corner's spring and damper put on the body at a spring length and compression speed.
        private static float Corner(AxleSetup s, float length, float compressing)
        {
            if (length >= s.SpringLength)
            {
                return 0f;   // the wheel hangs free
            }
            float force = SpringAt(s, (s.SpringLength - length) / s.SpringLength) + DamperAt(s, compressing);
            return force > 0f ? force : 0f;
        }

        // Where a corner comes to rest: its spring holding its load (found on the curve, which is straight until the bump stop).
        private static float SettledLength(AxleSetup s, float mass)
        {
            float low = 0f;
            float high = 1f;
            for (int i = 0; i < 40; i++)
            {
                float middle = (low + high) * 0.5f;
                if (SpringAt(s, middle) < mass * G)
                {
                    low = middle;
                }
                else
                {
                    high = middle;
                }
            }
            return s.SpringLength * (1f - (low + high) * 0.5f);
        }

        /// <summary>
        /// The compression at which a spring curve holds a load, by halving: <paramref name="curve"/> gives the force share
        /// at a compression. For the Garage's parts on whatever spring the car has, the game's or the handling's.
        /// </summary>
        public static float RestCompression(System.Func<float, float> curve, float force, float load)
        {
            float low = 0f;
            float high = 1f;
            for (int i = 0; i < 30; i++)
            {
                float middle = (low + high) * 0.5f;
                if (curve(middle) * force < load)
                {
                    low = middle;
                }
                else
                {
                    high = middle;
                }
            }
            return (low + high) * 0.5f;
        }

        /// <summary>
        /// The Garage's suspension part on a spring: its rate times <paramref name="stiffness"/> and the car standing
        /// <paramref name="ride"/> higher (below zero: lower) at rest, the curve's shape kept. Lowered by no more than
        /// <paramref name="mostLower"/>, a quarter of the bump travel, or what would leave less travel than
        /// <see cref="MinTravel"/>; raised by no more than <paramref name="mostRaise"/>. A stiffer spring sinks less, so the
        /// length is chosen for the height, not only shortened by the drop. <paramref name="change"/> is the height the car
        /// then stands at against before, m; the suspension's roll centre moves with the body by as much.
        /// </summary>
        public static void FitSpring(float force, float length, float restCompression, float stiffness, float ride, float mostLower, float mostRaise,
            out float newForce, out float newLength, out float change)
        {
            float sag = restCompression * length;
            float rest = length - sag;
            if (ride < 0f)
            {
                float lower = -ride;
                lower = lower > mostLower ? mostLower : lower;
                lower = lower > rest * 0.25f ? rest * 0.25f : lower;
                float spare = rest - MinTravel;
                lower = lower > spare ? (spare > 0f ? spare : 0f) : lower;
                change = -lower;
            }
            else
            {
                change = ride > mostRaise ? mostRaise : ride;
            }
            float stiff = stiffness > 0.1f ? stiffness : 0.1f;
            newLength = rest + change + sag / stiff;
            newForce = stiff * force * newLength / length;
        }

        // ---------------------------------------------------------------- the body's roll and pitch

        // Body roll per g: the side force acts at the roll centre, the springs and the anti-roll bars hold the body, and a
        // rolled body's weight pushes it further. The bars give what the springs alone do not; where even the springs
        // alone hold the body too stiffly, the roll centre comes down instead, to the road at the lowest.
        private static void Roll(CarChassis car, in HandlingTarget t, HandlingSetup s, float mass, float h)
        {
            float tf = car.Front.Track;
            float tr = car.Rear.Track;
            float springs = s.Front.Rate * tf * tf * 0.5f + s.Rear.Rate * tr * tr * 0.5f;   // N m per radian
            float theta = t.RollPerG / Radian;
            float centre = t.RollCentre < h - 0.05f ? t.RollCentre : h - 0.05f;
            centre = centre > 0f ? centre : 0f;
            float arm = h - centre;
            float weight = mass * G;
            float needed = weight * arm * (1f + 1f / theta);
            float bars = needed - springs;
            if (bars < 0f)
            {
                float armWanted = theta * springs / (weight * (1f + theta));
                centre = h - armWanted;
                centre = centre > 0f ? centre : 0f;
                centre = centre < t.RollCentre ? centre : t.RollCentre;
                arm = h - centre;
                bars = 0f;
            }
            float frontShare = car.Rear.Z < car.ComZ && car.Front.Z > car.ComZ ? (car.ComZ - car.Rear.Z) / (car.Front.Z - car.Rear.Z) : 0.5f;
            s.Front.AntiRoll = bars * frontShare / (tf * tf);
            s.Rear.AntiRoll = bars * (1f - frontShare) / (tr * tr);
            s.RollCentre = centre;
            s.Front.Application = centre / s.Front.SpringLength;
            s.Rear.Application = centre / s.Rear.SpringLength;
            float holding = springs + bars - weight * arm;
            s.RollPerG = holding > 1f ? weight * arm / holding * Radian : 90f;
        }

        /// <summary>Degrees of body roll per g on a setup with its own spring rates, bars and roll centre (for a Garage build on top).</summary>
        public static float RollPerG(CarChassis car, float mass, float comHeight, float rollCentre, float frontRate, float rearRate, float frontBar, float rearBar)
        {
            float tf = car.Front.Track;
            float tr = car.Rear.Track;
            float holding = frontRate * tf * tf * 0.5f + rearRate * tr * tr * 0.5f + frontBar * tf * tf + rearBar * tr * tr;
            float push = mass * G * (comHeight - rollCentre);
            return holding - push > 1f ? push / (holding - push) * Radian : 90f;
        }

        // Nose dive per g of braking: the tyres' force acts at the roll centre's height, and the game's anti-squat figure
        // adds the wheel's radius times itself to the arm; the springs alone hold the pitch.
        private static void Pitch(CarChassis car, in HandlingTarget t, HandlingSetup s, float mass, float h, float a, float b)
        {
            float pitch = 2f * s.Front.Rate * a * a + 2f * s.Rear.Rate * b * b;   // N m per radian
            float radius = 0.5f * (car.Front.Radius + car.Rear.Radius);
            float theta = t.DivePerG / Radian;
            float weight = mass * G;
            float armWanted = theta * pitch / (weight * (1f + theta));
            float natural = h - s.RollCentre;
            float squat = (armWanted - natural) / radius;   // the share of the wheel's radius the arm still wants
            squat = squat > 1f ? 1f : squat < -1f ? -1f : squat;
            s.Front.AntiSquat = -squat;
            s.Rear.AntiSquat = -squat;
            float arm = natural + radius * squat;
            float holding = pitch - weight * arm;
            s.DivePerG = holding > 1f ? weight * arm / holding * Radian : 90f;
        }

        /// <summary>The arm the brakes and the engine pitch the body about: the centre of mass over the roll centre, and the anti-squat's part.</summary>
        public static float PitchArm(CarChassis car, HandlingSetup s)
        {
            return s.ComHeight - s.RollCentre - 0.5f * (car.Front.Radius + car.Rear.Radius) * s.Front.AntiSquat;
        }

        /// <summary>
        /// The cornering, in g, at which the car would lift its inside wheels and tip on a flat road: its narrower track's
        /// half against the arm of the side force at the roll centre, with the body leaning its weight outwards as far as a
        /// sudden turn swings it (<see cref="RollOvershoot"/>).
        /// </summary>
        public static float TipG(CarChassis car, HandlingSetup s, float cornering)
        {
            float half = 0.5f * (car.Front.Track < car.Rear.Track ? car.Front.Track : car.Rear.Track);
            float arm = s.ComHeight - s.RollCentre;
            if (!(arm > 0.01f))
            {
                return 99f;
            }
            float lean = (float)System.Math.Sin(s.RollPerG / Radian * cornering * RollOvershoot) * arm;
            return (half - lean) / arm;
        }

        // ---------------------------------------------------------------- the tyres

        // Each axle's friction preset. The side force is the curve, peaking at the slip angle asked for and keeping most of
        // its peak past it; its level is set on the game's own front grip, the rear's from the car's own front-to-rear grip,
        // so wet roads, worn tyres and the Garage's tyres (which scale the grip) still count. The peak along is the
        // preset's D, which the game reads apart from the curve.
        private static void Tyres(CarChassis car, in HandlingTarget t, HandlingSetup s)
        {
            float side = t.SideGrip;
            float rearSide = side * (1f + t.RearSurplus);
            float sideFront = side / (SideFactor * GameFrontSide);
            float sideRear = rearSide / (SideFactor * GameFrontSide) * (car.Front.SideGrip / car.Rear.SideGrip);
            float alongFront = t.AlongGrip / (AlongFactor * GameFrontAlong);
            float alongRear = alongFront * (car.Front.AlongGrip / car.Rear.AlongGrip);
            float shape = 2f - 2f / (float)System.Math.PI * (float)System.Math.Asin(t.Plateau);
            Tyre(s.Front, t.PeakSlip, shape, sideFront, alongFront, car.Front);
            Tyre(s.Rear, t.PeakSlip * t.RearPeakFactor, shape, sideRear, alongRear, car.Rear);
        }

        private static void Tyre(AxleSetup s, float peakDegrees, float shape, float side, float along, AxleData game)
        {
            s.SideStiffness = 1f;
            float softening = 1f - LoadSoftening * s.Load / s.LoadRating;
            float peak = peakDegrees / SlipDegrees * s.SideStiffness * softening;
            s.TyreC = shape;
            s.TyreE = TyreCurvature;
            s.TyreSide = side;
            s.TyreAlong = along > side ? along : side;   // never under the side peak, where the game's own cap would flatten the curve
            s.TyreB = PeakB(peak, shape, TyreCurvature);
            s.PeakSlip = peak;
            s.PeakDegrees = peakDegrees;
            s.SideG = side * SideFactor * game.SideGrip;
            s.AlongG = s.TyreAlong * AlongFactor * game.AlongGrip;
        }

        /// <summary>The magic formula as the game samples it into a preset's curve: D sin(C atan(Bx - E(Bx - atan Bx))).</summary>
        public static float MagicFormula(float b, float c, float d, float e, float slip)
        {
            float x = slip < 0f ? -slip : slip;
            float bx = b * x;
            return d * (float)System.Math.Sin(c * System.Math.Atan(bx - e * (bx - System.Math.Atan(bx))));
        }

        // The B that puts the curve's peak at a slip: there C atan(phi) is a right angle.
        private static float PeakB(float peak, float c, float e)
        {
            float phi = (float)System.Math.Tan(System.Math.PI / (2.0 * c));
            float low = 0.5f;
            float high = 400f;
            for (int i = 0; i < 50; i++)
            {
                float b = (low + high) * 0.5f;
                float bx = b * peak;
                float at = bx - e * (bx - (float)System.Math.Atan(bx));
                if (at < phi)
                {
                    low = b;
                }
                else
                {
                    high = b;
                }
            }
            return (low + high) * 0.5f;
        }

        // ---------------------------------------------------------------- the steering

        // The full steering at each speed: the lock at walking pace; then the bend the front tyres can hold at their peak
        // grip at that speed, plus their peak slip angle (and a little more, so that a car pushed too hard runs wide).
        private static void Steering(CarChassis car, in HandlingTarget t, HandlingSetup s, float wheelbase)
        {
            float lockDegrees = car.SteerLock > 5f ? car.SteerLock : 35f;
            for (int i = 0; i < SteerKeys; i++)
            {
                float speed = SteerSpeeds[i];
                float degrees = speed < 0.5f ? lockDegrees : wheelbase * t.SideGrip * G / (speed * speed) * Radian + s.Front.PeakDegrees * t.SteerReach;
                float share = degrees / lockDegrees;
                s.SteerAt[i] = speed / SteerTop;
                s.SteerShare[i] = share > 1f ? 1f : share < 0.05f ? 0.05f : share;
            }
            s.SmoothAt[0] = 0f;
            s.SmoothTime[0] = SmoothAtRest;
            s.SmoothAt[1] = 1f;
            s.SmoothTime[1] = t.SteerSmoothing > SmoothAtRest ? t.SteerSmoothing : SmoothAtRest;
        }

        /// <summary>The full steering in degrees at a speed in m/s, as the game reads the curve: straight between its keys.</summary>
        public static float SteerAt(HandlingSetup s, float lockDegrees, float speed)
        {
            float x = speed / SteerTop;
            if (x <= s.SteerAt[0])
            {
                return s.SteerShare[0] * lockDegrees;
            }
            for (int i = 1; i < SteerKeys; i++)
            {
                if (x <= s.SteerAt[i])
                {
                    float share = s.SteerShare[i - 1] + (s.SteerShare[i] - s.SteerShare[i - 1]) * (x - s.SteerAt[i - 1]) / (s.SteerAt[i] - s.SteerAt[i - 1]);
                    return share * lockDegrees;
                }
            }
            return s.SteerShare[SteerKeys - 1] * lockDegrees;
        }

        // ---------------------------------------------------------------- the brakes

        // The full pedal's deceleration for the kind and each axle's brake share for it (PartsBrakes with no parts), and how
        // much more of its tyres each axle asks at its worst moment as the body dives.
        private static void Brakes(CarChassis car, in HandlingTarget t, HandlingSetup s, float mass, float wheelbase)
        {
            s.BrakeTarget = t.BrakeShare * t.AlongGrip;
            s.BrakeRise = t.BrakeRise;
            s.BrakeG = PartsBrakes(car, s, 1f, 1f, 1f, out float front, out float rear, out float use);
            s.Front.BrakeShare = front;
            s.Rear.BrakeShare = rear;
            s.BrakeUse = use;
            float moved = mass * G * s.BrakeG * PitchArm(car, s) / (2f * wheelbase);   // N per wheel at the full pedal
            BrakeRamp(s.BrakeRise, GameBrakeOnset, InputRate, 99f, Felt);
            s.FrontLag = BrakeLag(s.Front, s.Front.Load / G, moved);
            s.RearLag = BrakeLag(s.Rear, s.Rear.Load / G, -moved);
        }

        /// <summary>
        /// The full pedal's deceleration in g with the Garage's brakes, weight and tyres on top (each a factor, 1 for none),
        /// and each axle's brake share for it: both axles ask the same share of their tyres at their own worst moment as the
        /// body dives, at most <see cref="MostPeak"/> then and <see cref="MostUse"/> once settled, so no wheel locks on a dry
        /// road and the parts raise it only as far as the tyres take it. <paramref name="use"/> is the most either axle asks.
        /// </summary>
        public static float PartsBrakes(CarChassis car, HandlingSetup s, float brakes, float massFactor, float grip, out float front, out float rear, out float use)
        {
            float weight = s.Mass * massFactor * G;
            float nf = s.Front.Load * massFactor;   // N on a wheel at rest
            float nr = s.Rear.Load * massFactor;
            float mf = s.Front.AlongG * grip;       // what each N of load holds along
            float mr = s.Rear.AlongG * grip;
            float rollFront = s.Front.Rolling / car.Front.Radius;   // N a wheel's rolling resistance asks of its tyre, all along
            float rollRear = s.Rear.Rolling / car.Rear.Radius;
            float rolling = 2f * (rollFront + rollRear) / weight;   // g
            float perG = weight * PitchArm(car, s) / (2f * (car.Front.Z - car.Rear.Z));   // N moved onto a front wheel by each g of slowing
            perG = perG > 0f ? perG : 0f;
            float wanted = s.BrakeTarget * brakes / massFactor;
            BrakeRamp(s.BrakeRise, GameBrakeOnset, InputRate, 99f, Felt);
            float lagFront = 1f;
            float lagRear = 1f;
            float cap = MostUse;
            float a = Most(weight, nf, nr, mf, mr, perG, rollFront, rollRear, rolling, 1f, 1f, MostUse, wanted);   // the settled loads alone first
            for (int pass = 0; pass < 4; pass++)
            {
                // The axles' worst moments at this deceleration, and the most both take at them.
                lagFront = BrakeLag(s.Front, nf / G, perG * (a + rolling));
                lagRear = BrakeLag(s.Rear, nr / G, -perG * (a + rolling));
                cap = MostUse * (lagFront < lagRear ? lagFront : lagRear);
                cap = cap < MostPeak ? cap : MostPeak;
                a = Most(weight, nf, nr, mf, mr, perG, rollFront, rollRear, rolling, lagFront, lagRear, cap, wanted);
            }
            // The front's load lags the brakes as they come on, the rear's swings past its settled share at the deepest of the dive.
            float frontHold = mf * (nf + perG * (a + rolling)) / lagFront;   // N a front wheel's tyre takes at its worst moment
            float rearHold = mr * (nr - perG * (a + rolling)) / lagRear;
            rearHold = rearHold > 1f ? rearHold : 1f;
            float share = (weight * a * 0.5f + rollFront + rollRear) / (frontHold + rearHold);   // of the tyres, the same on both axles
            float torque = car.BrakeTorque * brakes > 1f ? car.BrakeTorque * brakes : 1f;
            float frontForce = share * frontHold - rollFront;
            float rearForce = share * rearHold - rollRear;
            front = frontForce * car.Front.Radius / torque;
            rear = rearForce * car.Rear.Radius / torque;
            if (front > 1f)
            {
                // Brakes too weak for the front's share: the front at their most, the rest to the rear as far as its tyres take it.
                front = 1f;
                float rest = weight * a * 0.5f - torque / car.Front.Radius;
                float rearMost = cap * rearHold - rollRear;
                rear = (rest < rearMost ? rest : rearMost) * car.Rear.Radius / torque;
            }
            rear = rear > 1f ? 1f : rear > 0f ? rear : 0f;
            front = front > 0f ? front : 0f;
            float decel = 2f * torque * (front / car.Front.Radius + rear / car.Rear.Radius) / weight;
            float frontNow = mf * (nf + perG * (decel + rolling)) / lagFront;
            float rearNow = mr * (nr - perG * (decel + rolling)) / lagRear;
            float frontUse = (front * torque / car.Front.Radius + rollFront) / (frontNow > 1f ? frontNow : 1f);
            float rearUse = (rear * torque / car.Rear.Radius + rollRear) / (rearNow > 1f ? rearNow : 1f);
            use = frontUse > rearUse ? frontUse : rearUse;
            return decel;
        }

        // The most braking, at most wanted g, at which both axles ask cap of what their tyres take at their worst moments,
        // the rolling resistance's part counted: weight a / 2 + rollFront + rollRear = cap (mf (nf + perG (a + rolling)) /
        // lagFront + mr (nr - perG (a + rolling)) / lagRear).
        private static float Most(float weight, float nf, float nr, float mf, float mr, float perG, float rollFront, float rollRear, float rolling, float lagFront, float lagRear,
            float cap, float wanted)
        {
            float slope = perG * (mf / lagFront - mr / lagRear);   // N of hold the axles gain together for each g of slowing
            float below = weight * 0.5f - cap * slope;
            float most = below > 1f ? (cap * (mf * nf / lagFront + mr * nr / lagRear + slope * rolling) - rollFront - rollRear) / below : wanted;
            return wanted < most ? wanted : most;
        }

        /// <summary>
        /// How much more of its tyres' grip an axle asks for at its worst moment as the brakes come on than once the body
        /// has settled into the dive: the corner's spring and damper holding <paramref name="mass"/> kg, pressed down by
        /// <paramref name="moved"/> N at the full pedal (the front; below zero, lifted: the rear), the brakes coming on as
        /// <see cref="Felt"/> has them, stepped as the game's physics steps it. The front's load lags the brakes at first,
        /// the rear's swings past its settled share at the deepest of the dive; 1 is never more.
        /// </summary>
        private static float BrakeLag(AxleSetup s, float mass, float moved)
        {
            float settled = mass * G + moved;   // the load once the body has settled into the dive
            if (!(mass > 1f))
            {
                return 1f;
            }
            if (!(settled > 1f))
            {
                return 99f;   // the wheel would lift off the road
            }
            float length = SettledLength(s, mass);
            float previous = length;
            float speed = 0f;
            float worst = 1f;
            for (int i = 0; i < Felt.Length; i++)
            {
                float load = Corner(s, length, (previous - length) / Step);   // the tyre's load: the spring and the damper
                float asked = Felt[i] * settled / (load > 1f ? load : 1f);
                worst = asked > worst ? asked : worst;
                previous = length;
                speed += ((load - moved * Felt[i]) / mass - G) * Step;
                length += speed * Step;
            }
            return worst;
        }

        /// <summary>
        /// The metres a car stops in from <see cref="StopFrom"/> with the brake key held from the start, as the game runs
        /// it: the eased pedal at <see cref="InputRate"/> frames a second, the brakes' own onset of <paramref name="onset"/>
        /// s (brakes.actuationTime), the full pedal's <paramref name="decel"/> g and the tyres' rolling resistance,
        /// <paramref name="rolling"/> g, each physics step's speed first, then its place.
        /// </summary>
        public static float StopMetres(float decel, float rolling, float rise, float onset)
        {
            float pedal = 0f;
            float brake = 0f;
            float time = 0f;
            float nextFrame = 0f;
            float frame = 1f / InputRate;
            float rate = Step / (onset > 0.001f ? onset : 0.001f);
            float speed = StopFrom;
            float metres = 0f;
            for (int i = 0; i < 5000 && speed > 0f; i++)
            {
                while (nextFrame <= time)
                {
                    pedal = HandlingRules.BrakePedal(pedal, 1f, frame, rise);
                    nextFrame += frame;
                }
                brake = brake + rate < pedal ? brake + rate : pedal;
                speed -= (brake * decel + rolling) * G * Step;
                speed = speed > 0f ? speed : 0f;
                metres += speed * Step;
                time += Step;
            }
            return metres;
        }

        /// <summary>
        /// For the custom shop: the metres from 100 km/h the car the handling is on stops in with a build of the Garage's
        /// brakes, weight and tyres on top (each a factor, 1 for none), the brakes' own onset <paramref name="onset"/> s:
        /// the axles' brake shares and the tyres' grip along (<see cref="PartsBrakes"/>), then the eased pedal's rise.
        /// </summary>
        public static float ShopStop(CarChassis car, HandlingSetup s, float brakes, float massFactor, float grip, float onset)
        {
            float decel = PartsBrakes(car, s, brakes, massFactor, grip, out _, out _, out _);
            float rolling = 2f * (s.Front.Rolling / car.Front.Radius + s.Rear.Rolling / car.Rear.Radius) / (s.Mass * massFactor * G);
            return StopMetres(decel, rolling, s.BrakeRise, onset);
        }

        /// <summary>The whole metres a car of a kind takes from 100 km/h with the full pedal on, at the whole feel: for the Guide.</summary>
        public static float KindPedalStop(CarKind kind)
        {
            HandlingTarget t = HandlingRules.Target(kind, HandlingLevel.Full, HandlingTuning.Neutral);
            return (float)System.Math.Round(StopFrom * StopFrom / (2f * t.BrakeShare * t.AlongGrip * G));
        }

        /// <summary>The whole metres a car of a kind stops in from 100 km/h at the whole feel, the brakes' own onset <paramref name="onset"/> s: for the Guide.</summary>
        public static float KindStop(CarKind kind, float onset)
        {
            HandlingTarget t = HandlingRules.Target(kind, HandlingLevel.Full, HandlingTuning.Neutral);
            return (float)System.Math.Round(StopMetres(t.BrakeShare * t.AlongGrip, t.RollingShare, t.BrakeRise, onset));
        }

        /// <summary>
        /// The brake felt over time after the key goes down at t = 0 and comes up at <paramref name="release"/>: the eased
        /// pedal (<see cref="HandlingRules.BrakePedal"/>) at <paramref name="frames"/> a second, then the game's own rate
        /// limit (brakes.actuationTime). Writes the felt brake per physics step into <paramref name="felt"/>.
        /// </summary>
        public static void BrakeRamp(float rise, float actuation, float frames, float release, float[] felt)
        {
            float pedal = 0f;
            float brake = 0f;
            float time = 0f;
            float frame = 1f / frames;
            float nextFrame = 0f;
            float rate = 1f / (actuation > 0.001f ? actuation : 0.001f);
            for (int i = 0; i < felt.Length; i++)
            {
                while (nextFrame <= time)
                {
                    pedal = HandlingRules.BrakePedal(pedal, nextFrame < release ? 1f : 0f, frame, rise);
                    nextFrame += frame;
                }
                float step = rate * Step;
                brake = pedal > brake ? (brake + step < pedal ? brake + step : pedal) : (brake - step > pedal ? brake - step : pedal);
                felt[i] = brake;
                time += Step;
            }
        }
    }
}
