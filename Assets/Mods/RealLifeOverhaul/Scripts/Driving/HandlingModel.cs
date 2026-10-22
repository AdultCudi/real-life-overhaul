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
        public float SpringLength;    // and its length, m
        public readonly CurveKeys Spring = new CurveKeys();   // its curve: compression to a share of the force (straight on all but the Mersaidi Dash)
        public float SideGrip;        // the game's tyre grip to the side (1.3 front, 1.5 rear on its own cars)
        public float AlongGrip;       // and along (0.9 front, 1.5 rear)
        public float SideStiffness;   // the slip stiffness to the side (1.27 front, 1.7 rear)
        public float LoadRating;      // N: the tyre's rated load
        public float Rolling;         // N m of rolling resistance
        public float AntiRoll;        // N per m of difference between the axle's two springs
        public float BrakeShare;      // the game's brake coefficient for the axle
        public float Bump;            // N s/m: the game's damper rates...
        public float Rebound;
        public float SlowBump;        // ...and its two-rate shape: the factor below the knee speed, the factor above, the knee
        public float FastBump;
        public float BumpKnee;
        public float SlowRebound;
        public float FastRebound;
        public float ReboundKnee;
        public float Application;     // the game's force application point distance (a share of the spring's length)
        public float AntiSquat;       // the game's anti-squat figure
    }

    /// <summary>What <see cref="HandlingModel.Design"/> needs to know about one car, read once when the player gets in.</summary>
    public sealed class CarChassis
    {
        public float Mass;            // kg, the game's
        public float ComY;            // the centre of mass in the car's own space
        public float ComZ;
        public float BrakeTorque;     // N m: the most brake torque a wheel is given, the game's
        public float SteerLock;       // degrees: the game's most steer angle
        public float TopKmh;          // the game's own top speed for the car (the vehicle type's limit), 0 if it has none
        public float TyreD;           // the tyres' preset: its D figure (the most along, over 1.35 and the load and grip)...
        public float TyrePeakSlip;    // ...and its peak slip, which the game's friction circle reads
        public readonly CurveKeys Tyre = new CurveKeys();    // the tyres' side force by slip, the game's
        public readonly CurveKeys Steer = new CurveKeys();   // the steering's share of the lock by speed over 50 m/s, the game's
        public readonly AxleData Front = new AxleData();
        public readonly AxleData Rear = new AxleData();
    }

    /// <summary>What the handling puts on one axle (per wheel where it says so), and what that axle should then do.</summary>
    public sealed class AxleSetup
    {
        public float SpringForce;     // N fully compressed, on the game's own curve
        public float SpringLength;    // m
        public float Bump;            // N s/m: the dampers' rates on the game's own two-rate shape
        public float Rebound;
        public float Application;     // the force application point distance: where the tyres' forces act, over the spring's length
        public float AntiSquat;       // the game's anti-squat figure: below zero the body pitches more under braking and pull
        public float LoadRating;      // N
        public float Rolling;         // N m of rolling resistance
        public float AntiRoll;        // N/m
        public float BrakeShare;      // the axle's brake coefficient
        public float AlongG;          // N a tyre holds along per N of load: the preset's D x 1.35 x the wheel's grip along

        public float Load;            // N per wheel at rest
        public float Rate;            // N/m of the spring at rest
        public float GameRate;        // and the game's
        public float Sag;             // m the spring is compressed at rest
        public float RestLength;      // m left to compress at rest: the bump travel (the game's own unless the softness slider moved)
        public float Frequency;       // Hz
        public float GameFrequency;
        public float Zeta;            // the damping ratio the body's swings die away at, as the model steps the corner
        public float GameZeta;        // and the game's own dampers'
    }

    /// <summary>Everything the handling puts on one car, and what the model says it does.</summary>
    public sealed class HandlingSetup
    {
        public float Mass;            // kg
        public float ComHeight;       // m: the centre of mass above the road at rest, the game's own
        public readonly AxleSetup Front = new AxleSetup();
        public readonly AxleSetup Rear = new AxleSetup();
        public readonly CurveKeys Tyre = new CurveKeys();    // the side force by slip: the game's own curve to the knee, flat past it
        public readonly CurveKeys Steer = new CurveKeys();   // the steering's share by speed (the game's own unless the slider moved)
        public bool SteerOwn;         // the game's own steering curve is kept on the car
        public float Knee;            // the slip, in the game's units, past which the tyres hold no more
        public float KneeDegrees;     // and as a front slip angle
        public float Hold;            // g the tyres hold at all, the car cornering
        public float GameHold;        // and the game's own tyres (at their most, sideways on)
        public float CityKmh;         // the speed the turn the game's car can make is measured at: its own top speed
        public float CityG;           // the g its own car turns at there with the steering key held
        public float CityCurve;       // the furthest along the curve any of its tyres is in that turn
        public float Raised;          // m the force points stand higher than the roll asked, so that the car slides before it rolls over
        public bool TipHeld;          // rolling over held the tyres' give or the roll back from what the level asked
        public float RollCentre;      // m above the road where the tyres' forces act (both axles' mean)
        public float GameRollCentre;
        public float RollPerG;        // degrees
        public float GameRollPerG;
        public float DivePerG;        // degrees of nose dive per g of braking (the same tail squat per g of pull)
        public float GameDivePerG;
        public float Overshoot;       // how far past its steady lean a sudden turn swings the body, at this ride's damping
        public float TipG;            // the cornering at which the car would roll over, the swing counted, g
        public float GameTipG;
        public float BrakeRise;       // s for the eased brake pedal from nothing to full
        public float BrakeG;          // the deceleration the full pedal gives, g
        public float BrakeTarget;     // the full pedal's deceleration the car is built for, g
        public float GameBrakeG;      // the game's own full pedal on this car, g (its fronts may lock)
        public float FrontLag;        // how much more of its tyres the front asks at its worst moment as the brakes come on than once settled
        public float RearLag;         // and the rear, at the deepest of the dive
        public float BrakeUse;        // the most of its tyres' grip along either axle asks for at the full pedal, at its worst moment
    }

    /// <summary>
    /// The driven car's handling as the game's vehicle physics (NWH Vehicle Physics 2) works it, game-free and built on the
    /// car's own game values: springs and bars scaled with the weight, the dampers' own shape with rates for the level, the
    /// tyre curve kept past the slip its own turns use and flat beyond, a little more roll and dive, the force points raised
    /// where it would roll over before it slides, and brakes shared so they never lock. <see cref="HandlingTurn"/> holds the
    /// car in a turn. Runs once per car and change of an option, a few milliseconds at most, never per frame.
    /// </summary>
    public static class HandlingModel
    {
        public const float G = 9.81f;                 // m/s2, the game's gravity
        public const float Step = 0.02f;              // s: the game's physics step
        public const float AlongFactor = 1.35f;       // the game's tyre: the most along = the preset's D x 1.35 x load x grip
        public const float MinTravel = 0.12f;         // m of bump travel the Garage never lowers a car below
        public const float Kerb = 0.10f;              // m: the kerb the self-check line reports the travel over
        public const float BounceStart = 0.03f;       // m: the disturbance the damping ratio is measured from
        public const float VisibleSwing = 0.005f;     // m: a swing of the body anyone sees
        public const float StillSwing = 0.00002f;     // m: a turn of the body smaller than this is the step's own noise, not a swing
        public const float TipMargin = 1.2f;          // a car rolls over only at this many times what its tyres hold (the Garage's semi-slicks give 1.2)
        public const float CityMargin = 1.15f;        // the knee lies this far past the furthest a tyre goes in the game's own hardest turn
        public const float CityLeast = 40f;           // km/h: the game's top speed is read as at least this...
        public const float CityMost = 120f;           // ...and at most this
        public const float MostLower = 0.15f;         // m the force points come down for roll at the most
        public const float LowestPoint = 0.02f;       // m: and never lower than this above the road
        public const float MostRaise = 1.5f;          // m they may go up so that a car slides before it rolls over
        public const float MarginSpare = 1.002f;      // a little past TipMargin, so float sums never leave a car just short of it
        public const float LeastKneeDegrees = 6f;     // degrees of front slip angle: the tyres never give before it
        public const float CityLift = 0.1f;           // the game's own hardest turn is one where its lightest wheel keeps this share of its load...
        public const float CitySlip = 20f;            // ...and its front tyres slip no more than this many degrees: past it the car is sliding, not holding a corner
        public const float RearKeep = 0.4f;           // the share of its load at rest a rear wheel keeps at the full pedal, the body settled in its dive
        public const float MostDamping = 2f;          // times the game's own damper rates for the weight at the most (the bounce slider): past it a light corner shakes at the step
        public const float MostUse = 0.93f;           // the most of what its tyres hold along either axle brakes with, the body settled in its dive
        public const float MostPeak = 0.95f;          // and at its worst moment as the brakes come on: no wheel locks on a dry road, the game's strong brakes and the
                                                      // custom shop's on top swinging the load more than the model's settled sums
        public const float GameBrakeOnset = 0.1f;     // s: the game's own brakes.actuationTime on its cars
        public const float InputRate = 60f;           // frames a second the eased pedal moves at (the car's input runs once a frame)
        public const float StopFrom = 100f / 3.6f;    // m/s: the speed the stopping distances are measured from
        public const int SteerKeys = 11;

        // m/s at which a steering curve the slider moved is written: close together where the lock falls away.
        private static readonly float[] SteerSpeeds = { 0f, 2.5f, 5f, 7.5f, 10f, 13f, 17f, 22f, 30f, 40f, 50f };

        private const float Radian = 57.29578f;       // degrees in a radian

        private static readonly float[] Felt = new float[150];   // the brake felt at each physics step while an axle's dive is stepped (3 s)
        private static readonly float[] Shares = new float[SteerKeys];   // a moved steering curve's share at each of SteerSpeeds
        private static readonly TurnCar Game = new TurnCar();     // the game's own car, as a turn sees it
        private static readonly TurnCar Own = new TurnCar();      // and the car with the handling on

        /// <summary>One car's handling for a target, all written into <paramref name="s"/>; why the car cannot have it, or null.</summary>
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
            if (!Usable(f) || !Usable(r) || car.Tyre.Count < 2 || car.Steer.Count < 1 || !(car.TyreD > 0.05f) || !(car.SteerLock > 1f))
            {
                return "its springs, tyres or steering are not a car's";
            }
            if (!(t.Softness > 0.1f) || !(t.Grip > 0.1f) || !(t.Steering > 0.1f) || !(t.DamperScale > 0.1f) || !(t.BrakeRise > 0.01f))
            {
                return "the target is not a car's";
            }

            // The weight: a share of the way to the kind's, never lighter than the game made the car. The springs, bars
            // and tyres' ratings carry it as the game's carry the game's weight.
            float mass = car.Mass + t.MassShare * (t.Mass > car.Mass ? t.Mass - car.Mass : 0f);
            float ratio = mass / car.Mass;
            s.Mass = mass;
            float gameFront = car.Mass * G * b / (2f * wheelbase);   // N per wheel at rest
            float gameRear = car.Mass * G * a / (2f * wheelbase);
            if (!Springs(f, gameFront, ratio, t.Softness, s.Front) || !Springs(r, gameRear, ratio, t.Softness, s.Rear))
            {
                return "its springs do not hold it up";
            }
            Carry(f, car.TyreD, ratio, s.Front);
            Carry(r, car.TyreD, ratio, s.Rear);

            // The centre of mass: the game's own, standing as high as the game had it (the springs keep the ride height).
            float ground = 0.5f * (f.Top - s.Front.RestLength - f.Radius + r.Top - s.Rear.RestLength - r.Radius);
            s.ComHeight = car.ComY - ground;

            Ride(f, t, ratio, s.Front);
            Ride(r, t, ratio, s.Rear);
            float zeta = s.Front.Zeta < s.Rear.Zeta ? s.Front.Zeta : s.Rear.Zeta;
            s.Overshoot = zeta < 0.99f ? 1f + (float)System.Math.Exp(-System.Math.PI * zeta / System.Math.Sqrt(1f - zeta * zeta)) : 1f;

            GameTurn(car, Game);
            s.GameRollPerG = HandlingTurn.RollPerG(Game);
            s.GameRollCentre = s.ComHeight - HandlingTurn.Arm(Game);
            s.GameHold = HandlingTurn.Hold(Game);
            s.GameTipG = HandlingTurn.Rollover(Game, 1f);
            Roll(car, t, s, a, b, wheelbase);
            Steering(car, t, s);
            Tyres(car, t, s);
            Tipping(car, t, s);
            Pitch(car, t, s, a, b);
            Brakes(car, t, s, wheelbase);
            return null;
        }

        private static bool Usable(AxleData axle)
        {
            return axle.SpringForce > 100f && axle.SpringLength > 0.05f && axle.Radius > 0.1f && axle.SideGrip > 0.05f && axle.AlongGrip > 0.05f
                   && axle.LoadRating > 100f && axle.SideStiffness > 0.05f && axle.Spring.Count >= 2 && axle.Bump >= 0f && axle.Rebound >= 0f;
        }

        // The game's own spring carrying the new weight: its force scaled with the weight and its length kept, so the car
        // stands as high and rides at the same frequency; the softness slider divides the rate, the spring then made as
        // much longer as it sinks further, so the travel over a kerb stays the game's. False if no spring of the curve's
        // shape holds the load.
        private static bool Springs(AxleData axle, float gameLoad, float ratio, float softness, AxleSetup s)
        {
            float gameCompression = RestCompression(axle.Spring, axle.SpringForce, gameLoad);
            float gameRest = axle.SpringLength * (1f - gameCompression);
            float gameRate = axle.SpringForce * axle.Spring.Slope(gameCompression) / axle.SpringLength;
            float load = gameLoad * ratio;
            s.Load = load;
            s.GameRate = gameRate;
            s.GameFrequency = Frequency(gameRate, gameLoad);
            if (!(gameCompression < 0.999f) || !(gameRate > 1f))
            {
                return false;
            }
            if (System.Math.Abs(softness - 1f) < 1e-4f)
            {
                s.SpringForce = axle.SpringForce * ratio;
                s.SpringLength = axle.SpringLength;
            }
            else
            {
                // The compression at rest for the softer (or firmer) rate on the same rest travel: rate = load x slope / (share x length).
                float wanted = gameRate * ratio / softness;
                float low = 0.001f;
                float high = 0.999f;
                for (int i = 0; i < 40; i++)
                {
                    float c = 0.5f * (low + high);
                    float rate = load * axle.Spring.Slope(c) * (1f - c) / (axle.Spring.Evaluate(c) * gameRest);
                    if (rate > wanted)
                    {
                        low = c;
                    }
                    else
                    {
                        high = c;
                    }
                }
                float compression = 0.5f * (low + high);
                s.SpringLength = gameRest / (1f - compression);
                s.SpringForce = load / axle.Spring.Evaluate(compression);
            }
            float settled = RestCompression(axle.Spring, s.SpringForce, load);
            s.Rate = s.SpringForce * axle.Spring.Slope(settled) / s.SpringLength;
            s.Sag = settled * s.SpringLength;
            s.RestLength = s.SpringLength - s.Sag;
            s.Frequency = Frequency(s.Rate, load);
            return s.Rate > 1f;
        }

        private static float Frequency(float rate, float load)
        {
            return (float)System.Math.Sqrt(rate / (load / G)) / (2f * (float)System.Math.PI);
        }

        // What the game's tyres, bars and wheels carry, scaled with the weight as the game's carry its own.
        private static void Carry(AxleData axle, float tyreD, float ratio, AxleSetup s)
        {
            s.AntiRoll = axle.AntiRoll * ratio;
            s.LoadRating = axle.LoadRating * ratio;
            s.Rolling = axle.Rolling * ratio;
            s.AlongG = tyreD * AlongFactor * axle.AlongGrip;
        }

        // The dampers: the game's own two-rate shape, their rates scaled so the body's swings die away at the level's
        // damping ratio, or at the game's own where that is lighter; at Light the game's own rates for the weight. The
        // bounce slider scales the rates on top. The rates are never more than the game's own for the weight: past them
        // a damper this strong on a light corner would shake at the physics step.
        private static void Ride(AxleData axle, in HandlingTarget t, float ratio, AxleSetup s)
        {
            s.GameZeta = Bounce(axle, axle.SpringForce, axle.SpringLength, axle.Bump, axle.Rebound, s.Load / ratio / G, out _, out _);
            float scale = ratio;   // the game's own damping ratio for the new weight on its springs
            if (t.Damping > 0f && t.Damping < s.GameZeta - 1e-3f)
            {
                float wanted = t.Damping;
                float low = 0.01f * ratio;
                float high = ratio;
                for (int i = 0; i < 22; i++)
                {
                    float middle = 0.5f * (low + high);
                    if (Bounce(axle, s.SpringForce, s.SpringLength, axle.Bump * middle, axle.Rebound * middle, s.Load / G, out _, out _) < wanted)
                    {
                        low = middle;
                    }
                    else
                    {
                        high = middle;
                    }
                }
                scale = 0.5f * (low + high);
            }
            float rates = scale * t.DamperScale;
            rates = rates < MostDamping * ratio ? rates : MostDamping * ratio;
            s.Bump = axle.Bump * rates;
            s.Rebound = axle.Rebound * rates;
            s.Zeta = Bounce(axle, s.SpringForce, s.SpringLength, s.Bump, s.Rebound, s.Load / G, out _, out _);
        }

        /// <summary>The game's two-rate damper on its own shape: positive speed is compression, the force pushes the body up.</summary>
        public static float DamperAt(AxleData shape, float bump, float rebound, float speed)
        {
            if (speed > 0f)
            {
                float v = speed < shape.BumpKnee ? speed * shape.SlowBump : shape.BumpKnee * shape.SlowBump + (speed - shape.BumpKnee) * shape.FastBump;
                return v * bump;
            }
            float up = -speed;
            float w = up < shape.ReboundKnee ? up * shape.SlowRebound : shape.ReboundKnee * shape.SlowRebound + (up - shape.ReboundKnee) * shape.FastRebound;
            return -w * rebound;
        }

        // The force a corner's spring and damper put on the body at a spring length and compression speed, as the game sums them.
        private static float Corner(AxleData shape, float force, float length, float bump, float rebound, float at, float compressing)
        {
            if (at >= length)
            {
                return 0f;   // the wheel hangs free
            }
            float sum = shape.Spring.Evaluate((length - at) / length) * force + DamperAt(shape, bump, rebound, compressing);
            return sum > 0f ? sum : 0f;
        }

        /// <summary>
        /// One corner holding <paramref name="mass"/> kg let go from <see cref="BounceStart"/> pushed down (half its travel
        /// if less), stepped as the game's physics steps it (spring and damper from the step's start, then the body's
        /// speed, then its place). The damping ratio its first swing past rest gives (1 if it never swings past);
        /// <paramref name="swings"/> counts those anyone sees and <paramref name="after"/> is what is left a second and a half on.
        /// </summary>
        public static float Bounce(AxleData shape, float force, float length, float bump, float rebound, float mass, out int swings, out float after)
        {
            return Bounce(shape, force, length, bump, rebound, mass, Push(shape, force, length, mass), out swings, out after);
        }

        /// <summary>The push Bounce measures a corner from: <see cref="BounceStart"/>, or half its travel at rest if less.</summary>
        public static float Push(AxleData shape, float force, float length, float mass)
        {
            float rest = SettledLength(shape, force, length, mass * G);
            return BounceStart < 0.5f * rest ? BounceStart : 0.5f * rest;   // inside the travel a car stands with at rest
        }

        /// <summary>As <see cref="Bounce(AxleData, float, float, float, float, float, out int, out float)"/>, from a push of <paramref name="push"/> m.</summary>
        public static float Bounce(AxleData shape, float force, float length, float bump, float rebound, float mass, float push, out int swings, out float after)
        {
            float rest = SettledLength(shape, force, length, mass * G);
            float at = rest - push;
            float previous = at;
            float speed = 0f;
            float last = -push;
            float direction = 0f;
            int overs = 0;
            float first = 0f;
            float second = 0f;
            swings = 0;
            after = 0f;
            for (int i = 0; i < 160; i++)
            {
                float held = Corner(shape, force, length, bump, rebound, at, (previous - at) / Step);
                previous = at;
                speed += (held / mass - G) * Step;
                at += speed * Step;
                float off = at - rest;
                float moving = off - last;
                if (direction != 0f && moving * direction < 0f)
                {
                    // the body turned: the step before was a swing's far point
                    float peak = last < 0f ? -last : last;
                    if (peak > StillSwing)
                    {
                        swings += peak > VisibleSwing ? 1 : 0;
                        if (last > 0f)
                        {
                            // past rest above it: the swings the push sets off, one a period
                            overs++;
                            first = overs == 1 ? last : first;
                            second = overs == 2 ? last : second;
                        }
                    }
                }
                direction = moving != 0f ? moving : direction;
                last = off;
                if (i == 74)
                {
                    after = off < 0f ? -off : off;
                }
            }
            if (overs >= 1)
            {
                if (!(first < push) || (overs >= 2 && !(second < first)))
                {
                    return 0f;   // the swings never die away
                }
                // The ratio from how far the first swing goes past rest, as a step's overshoot gives it: the swing anyone
                // sees (the game's dampers, softer above their knee, let the first swing go further than the later ones).
                float log = (float)System.Math.Log(first / push);
                return -log / (float)System.Math.Sqrt(System.Math.PI * System.Math.PI + log * log);
            }
            return 1f;   // damped through: it never swings past
        }

        /// <summary>
        /// The corner settled at rest, then the road under its wheel rising <paramref name="height"/> at once (a kerb climbed):
        /// the least length the spring comes to. At zero or below it has bottomed out.
        /// </summary>
        public static float KerbLeft(AxleData shape, float force, float length, float bump, float rebound, float mass, float height)
        {
            float at = SettledLength(shape, force, length, mass * G) - height;
            float previous = at + height;
            float speed = 0f;
            float least = at;
            for (int i = 0; i < 150; i++)
            {
                // Bottomed out, the game's wheel collider meets the road: a hard push, as a solid contact gives.
                float push = at <= 0f ? mass * G * 4f : Corner(shape, force, length, bump, rebound, at, (previous - at) / Step);
                previous = at;
                speed += (push / mass - G) * Step;
                at += speed * Step;
                least = at < least ? at : least;
            }
            return least;
        }

        // Where a corner comes to rest: its spring holding its load, found on its curve.
        private static float SettledLength(AxleData shape, float force, float length, float load)
        {
            return length * (1f - RestCompression(shape.Spring, force, load));
        }

        /// <summary>The compression at which a spring's curve holds a load, by halving (1 if it never does).</summary>
        public static float RestCompression(CurveKeys curve, float force, float load)
        {
            float low = 0f;
            float high = 1f;
            for (int i = 0; i < 40; i++)
            {
                float middle = (low + high) * 0.5f;
                if (curve.Evaluate(middle) * force < load)
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
        /// The Garage's suspension and weight parts on a spring of any curve (<paramref name="curve"/>: compression to a share
        /// of <paramref name="force"/>) holding <paramref name="load"/> N before them: the rate at rest times the stiffness
        /// and the weight factors, the load times the weight's, and the car <paramref name="ride"/> higher (below zero:
        /// lower), so a lighter car keeps its height and frequency. Lowered no more than <paramref name="mostLower"/>, a
        /// quarter of the travel, or to less than <see cref="MinTravel"/>; raised no more than <paramref name="mostRaise"/>.
        /// <paramref name="change"/> is the new height against the old, m; the roll centre moves with the body by as much.
        /// </summary>
        public static void FitSpring(System.Func<float, float> curve, float force, float length, float load, float weight, float stiffness, float ride, float mostLower,
            float mostRaise, out float newForce, out float newLength, out float change)
        {
            float compression = RestCompression(curve, force, load);
            float rest = length * (1f - compression);
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
            float heavy = weight > 0.1f ? weight : 0.1f;
            float rate = force * Slope(curve, compression) / length * stiff * heavy;
            float carried = load * heavy;
            float standing = rest + change;
            // The compression at rest for that rate on that travel: rate = load x slope x (1 - c) / (share x travel).
            float low = 0.001f;
            float high = 0.999f;
            for (int i = 0; i < 40; i++)
            {
                float c = 0.5f * (low + high);
                float share = curve(c);
                float at = share > 1e-6f ? carried * Slope(curve, c) * (1f - c) / (share * standing) : float.MaxValue;
                if (at > rate)
                {
                    low = c;
                }
                else
                {
                    high = c;
                }
            }
            float settled = 0.5f * (low + high);
            newLength = standing / (1f - settled);
            newForce = carried / curve(settled);
        }

        // A curve's slope at x, from either side of it (the curve as the game evaluates it, through a delegate).
        private static float Slope(System.Func<float, float> curve, float x)
        {
            float low = x > 0.001f ? x - 0.001f : 0f;
            float high = x < 0.999f ? x + 0.001f : 1f;
            return (curve(high) - curve(low)) / (high - low);
        }

        /// <summary>The centre of mass's height above the road at rest on the game's own springs, m.</summary>
        public static float GameHeight(CarChassis car)
        {
            AxleData f = car.Front;
            AxleData r = car.Rear;
            float wheelbase = f.Z - r.Z;
            float front = car.Mass * G * (car.ComZ - r.Z) / (2f * wheelbase);
            float rear = car.Mass * G * (f.Z - car.ComZ) / (2f * wheelbase);
            float restF = f.SpringLength * (1f - RestCompression(f.Spring, f.SpringForce, front));
            float restR = r.SpringLength * (1f - RestCompression(r.Spring, r.SpringForce, rear));
            return car.ComY - 0.5f * (f.Top - restF - f.Radius + r.Top - restR - r.Radius);
        }

        /// <summary>The game's own car as a turn sees it: its weight, springs, bars, force points, tyres and steering.</summary>
        public static void GameTurn(CarChassis car, TurnCar into)
        {
            AxleData f = car.Front;
            AxleData r = car.Rear;
            float wheelbase = f.Z - r.Z;
            into.Mass = car.Mass;
            into.Height = GameHeight(car);
            into.A = f.Z - car.ComZ;
            into.B = car.ComZ - r.Z;
            into.TrackF = f.Track;
            into.TrackR = r.Track;
            into.PointF = f.Application * f.SpringLength;
            into.PointR = r.Application * r.SpringLength;
            into.LoadF = car.Mass * G * into.B / (2f * wheelbase);
            into.LoadR = car.Mass * G * into.A / (2f * wheelbase);
            into.RollF = RollStiffness(f.SpringForce * f.Spring.Slope(RestCompression(f.Spring, f.SpringForce, into.LoadF)) / f.SpringLength, f.AntiRoll, f.Track);
            into.RollR = RollStiffness(r.SpringForce * r.Spring.Slope(RestCompression(r.Spring, r.SpringForce, into.LoadR)) / r.SpringLength, r.AntiRoll, r.Track);
            into.GripF = f.SideGrip;
            into.GripR = r.SideGrip;
            into.StiffF = f.SideStiffness;
            into.StiffR = r.SideStiffness;
            into.RatingF = f.LoadRating;
            into.RatingR = r.LoadRating;
            into.Lock = car.SteerLock;
            into.TyreScale = 1f;
            into.Tyre = car.Tyre;
            into.Steer = car.Steer;
        }

        /// <summary>The car with the handling on as a turn sees it, on a tyre curve (the handling's own, or the game's for comparison).</summary>
        public static void OwnTurn(CarChassis car, HandlingSetup s, CurveKeys tyre, TurnCar into)
        {
            AxleData f = car.Front;
            AxleData r = car.Rear;
            into.Mass = s.Mass;
            into.Height = s.ComHeight;
            into.A = f.Z - car.ComZ;
            into.B = car.ComZ - r.Z;
            into.TrackF = f.Track;
            into.TrackR = r.Track;
            into.PointF = s.Front.Application * s.Front.SpringLength;
            into.PointR = s.Rear.Application * s.Rear.SpringLength;
            into.LoadF = s.Front.Load;
            into.LoadR = s.Rear.Load;
            into.RollF = RollStiffness(s.Front.Rate, s.Front.AntiRoll, f.Track);
            into.RollR = RollStiffness(s.Rear.Rate, s.Rear.AntiRoll, r.Track);
            into.GripF = f.SideGrip;
            into.GripR = r.SideGrip;
            into.StiffF = f.SideStiffness;
            into.StiffR = r.SideStiffness;
            into.RatingF = s.Front.LoadRating;
            into.RatingR = s.Rear.LoadRating;
            into.Lock = car.SteerLock;
            into.TyreScale = 1f;
            into.Tyre = tyre;
            into.Steer = s.Steer;
        }

        /// <summary>N m per radian of body roll an axle's two springs and its bar hold.</summary>
        public static float RollStiffness(float rate, float bar, float track)
        {
            return rate * track * track * 0.5f + bar * track * track;
        }

        // The roll: the game's own roll per g and a little more (times the roll slider), from the force points brought
        // down by the same height on both axles, no more than MostLower and never nearer the road than LowestPoint.
        private static void Roll(CarChassis car, in HandlingTarget t, HandlingSetup s, float a, float b, float wheelbase)
        {
            float game = s.GameRollPerG > 0f ? s.GameRollPerG : 0f;
            float wanted = (game + t.RollAdd) * t.RollScale / Radian;
            float hold = RollStiffness(s.Front.Rate, s.Front.AntiRoll, car.Front.Track) + RollStiffness(s.Rear.Rate, s.Rear.AntiRoll, car.Rear.Track);
            float arm = wanted * hold / (s.Mass * G * (1f + wanted));
            float pointF = car.Front.Application * car.Front.SpringLength;
            float pointR = car.Rear.Application * car.Rear.SpringLength;
            float gameArm = s.ComHeight - (b * pointF + a * pointR) / wheelbase;
            float down = arm - gameArm;
            down = down < MostLower ? down : MostLower;
            float lowest = pointF < pointR ? pointF : pointR;
            down = down < lowest - LowestPoint ? down : lowest - LowestPoint;
            s.Front.Application = (pointF - down) / s.Front.SpringLength;
            s.Rear.Application = (pointR - down) / s.Rear.SpringLength;
            s.Raised = 0f;
        }

        // The tyres: the game's own curve (times the grip slider) to its knee, flat past it. The knee lies past the
        // furthest any tyre goes in the hardest turn the game's own car can make with the key held (at its own top speed),
        // by CityMargin, and at least at the level's slip angle: every corner the game's car takes at a speed, this one
        // takes too, and past the very limit it gives.
        private static void Tyres(CarChassis car, in HandlingTarget t, HandlingSetup s)
        {
            // The game's own top speed, or the fastest below it at which the game's own car still holds the road with the key
            // held: all four wheels down and its front tyres short of sliding (some of its tall or softly sprung cars would
            // lift a wheel at their top speed, and its sharpest steering slides there).
            float top = car.TopKmh > 0f ? (car.TopKmh < CityLeast ? CityLeast : car.TopKmh > CityMost ? CityMost : car.TopKmh) : 60f;
            s.CityKmh = top;
            while (s.CityKmh > CityLeast
                   && (!HandlingTurn.Steady(Game, s.CityKmh / 3.6f, out TurnState gameCity) || gameCity.Least < CityLift || gameCity.SlipF > CitySlip))
            {
                s.CityKmh -= 5f;
            }
            // How far along their curve the game's own car's tyres go in that turn (its curve times the grip slider): the
            // car with the handling on needs a little more as its weight moves out a little more, which CityMargin covers.
            Game.TyreScale = t.Grip;
            float need = 0f;
            if (HandlingTurn.Steady(Game, s.CityKmh / 3.6f, out TurnState city))
            {
                s.CityG = city.G;
                need = city.CurveF > city.CurveR ? city.CurveF : city.CurveR;
            }
            Game.TyreScale = 1f;
            s.CityCurve = need;
            float level = KneeAt(car, s, t.KneeDegrees);
            float knee = need * CityMargin;
            Knee(car, t.Grip, knee > level ? knee : level, s);
        }

        // The slip, in the game's units, of a front tyre at rest at a slip angle in degrees.
        private static float KneeAt(CarChassis car, HandlingSetup s, float degrees)
        {
            float softening = 1f - HandlingTurn.LoadSoftening * Share(s.Front.Load, s.Front.LoadRating);
            return degrees / HandlingTurn.SlipDegrees * car.Front.SideStiffness * softening;
        }

        private static float Share(float load, float rating)
        {
            float share = rating > 1f ? load / rating : 1f;
            return share > 1f ? 1f : share;
        }

        // The handling's tyre curve: the game's own keys before the knee (each segment exactly the game's), a key at the
        // knee with the game's own slope arriving, and flat from there; values and slopes times the grip slider.
        private static void Knee(CarChassis car, float grip, float knee, HandlingSetup s)
        {
            CurveKeys game = car.Tyre;
            float last = game.Time(game.Count - 1);
            CurveKeys own = s.Tyre;
            own.Clear();
            if (knee >= last)
            {
                for (int i = 0; i < game.Count; i++)
                {
                    own.Add(game.Time(i), game.Value(i) * grip, game.InSlope(i) * grip, game.OutSlope(i) * grip);
                }
                s.Knee = last;
            }
            else
            {
                for (int i = 0; i < game.Count && game.Time(i) < knee - 1e-4f; i++)
                {
                    own.Add(game.Time(i), game.Value(i) * grip, game.InSlope(i) * grip, game.OutSlope(i) * grip);
                }
                float value = game.Evaluate(knee) * grip;
                own.Add(knee, value, game.Slope(knee) * grip, 0f);
                own.Add(last > knee + 0.1f ? last : knee + 0.1f, value, 0f, 0f);
                s.Knee = knee;
            }
            float softening = 1f - HandlingTurn.LoadSoftening * Share(s.Front.Load, s.Front.LoadRating);
            s.KneeDegrees = s.Knee * HandlingTurn.SlipDegrees / (car.Front.SideStiffness * softening);
        }

        // The tipping: the car must slide before it rolls over, by TipMargin, the body's swing in a sudden turn counted.
        // Where it would not, the tyres give sooner (never before the game's own hardest turn, nor LeastKneeDegrees), and
        // where even that is not enough the force points go up, which takes some of the roll away. Rolling over is a sum of
        // the force points alone and the most the tyres hold (their rating aside) is their curve's flat part, so each step
        // is found directly.
        private static void Tipping(CarChassis car, in HandlingTarget t, HandlingSetup s)
        {
            OwnTurn(car, s, s.Tyre, Own);
            float tip = HandlingTurn.Rollover(Own, s.Overshoot);
            float grip = car.Front.SideGrip < car.Rear.SideGrip ? car.Front.SideGrip : car.Rear.SideGrip;
            float flat = tip / (TipMargin * MarginSpare * HandlingTurn.SideFactor * grip);   // the most the curve may hold
            s.TipHeld = s.Tyre.Value(s.Tyre.Count - 1) > flat;
            float floor = s.CityCurve * CityMargin;
            float least = KneeAt(car, s, LeastKneeDegrees);
            floor = floor > least ? floor : least;
            if (s.TipHeld && s.Knee > floor + 1e-4f)
            {
                // The knee where the game's curve (times the grip slider) reaches what the tyres may hold, never before the floor.
                float low = floor;
                float high = s.Knee;
                for (int i = 0; i < 24; i++)
                {
                    float middle = 0.5f * (low + high);
                    if (car.Tyre.Evaluate(middle) * t.Grip <= flat)
                    {
                        low = middle;
                    }
                    else
                    {
                        high = middle;
                    }
                }
                Knee(car, t.Grip, low, s);
            }
            float most = HoldMost(Own);
            if (tip < TipMargin * most)
            {
                float applicationF = s.Front.Application;
                float applicationR = s.Rear.Application;
                float need = TipMargin * MarginSpare * most;
                float low = 0f;
                float high = MostRaise;
                for (int i = 0; i < 30; i++)
                {
                    float middle = 0.5f * (low + high);
                    Raise(s, applicationF, applicationR, middle);
                    OwnTurn(car, s, s.Tyre, Own);
                    if (HandlingTurn.Rollover(Own, s.Overshoot) >= need)
                    {
                        high = middle;
                    }
                    else
                    {
                        low = middle;
                    }
                }
                s.Raised = high;
                Raise(s, applicationF, applicationR, high);
                OwnTurn(car, s, s.Tyre, Own);
            }
            s.TipG = HandlingTurn.Rollover(Own, s.Overshoot);
            s.Hold = HandlingTurn.Hold(Own);   // what its tyres hold, their rating counted: never more than the most above
            s.RollPerG = HandlingTurn.RollPerG(Own);
            s.RollCentre = s.ComHeight - HandlingTurn.Arm(Own);
        }

        // The most the tyres hold side on with no load rating to cap them, g: the flat of their curve, as the game's tyre
        // sums it, on the axle that grips the less. A lighter build of the Garage's, whose tyres the rating caps less, holds
        // no more than this.
        private static float HoldMost(TurnCar c)
        {
            float grip = c.GripF < c.GripR ? c.GripF : c.GripR;
            return c.Tyre.Most() * c.TyreScale * HandlingTurn.SideFactor * grip;
        }

        private static void Raise(HandlingSetup s, float applicationF, float applicationR, float up)
        {
            s.Front.Application = applicationF + up / s.Front.SpringLength;
            s.Rear.Application = applicationR + up / s.Rear.SpringLength;
        }

        // Nose dive per g of braking: the tyres' forces act at the force points' height, and the game's anti-squat figure
        // takes the wheel's radius times itself off the arm; the springs alone hold the pitch. The game's own dive times the
        // level's share (the weight moved onto the front grows by as much), by anti-squat, which the game keeps between -1 and 1.
        private static void Pitch(CarChassis car, in HandlingTarget t, HandlingSetup s, float a, float b)
        {
            float radius = 0.5f * (car.Front.Radius + car.Rear.Radius);
            float gameHold = 2f * s.Front.GameRate * a * a + 2f * s.Rear.GameRate * b * b;   // N m per radian
            float gameArm = s.ComHeight - s.GameRollCentre - radius * 0.5f * (car.Front.AntiSquat + car.Rear.AntiSquat);
            float gameWeight = car.Mass * G;
            s.GameDivePerG = gameHold - gameWeight * gameArm > 1f ? gameWeight * gameArm / (gameHold - gameWeight * gameArm) * Radian : 90f;
            float game = s.GameDivePerG > 0f ? s.GameDivePerG : 0f;
            float wanted = game * t.DiveScale / Radian;
            float hold = 2f * s.Front.Rate * a * a + 2f * s.Rear.Rate * b * b;
            float weight = s.Mass * G;
            float arm = wanted * hold / (weight * (1f + wanted));
            float squat = (s.ComHeight - s.RollCentre - arm) / radius;
            squat = squat > 1f ? 1f : squat < -1f ? -1f : squat;
            s.Front.AntiSquat = squat;
            s.Rear.AntiSquat = squat;
            float armNow = PitchArm(car, s);
            s.DivePerG = hold - weight * armNow > 1f ? weight * armNow / (hold - weight * armNow) * Radian : 90f;
        }

        /// <summary>The deceleration in g at which the rear, the body settled in its dive, keeps only <see cref="RearKeep"/> of its load (99 if never).</summary>
        public static float RearLimit(CarChassis car, HandlingSetup s)
        {
            float weight = s.Mass * G;
            float perG = weight * PitchArm(car, s) / (2f * (car.Front.Z - car.Rear.Z));
            float rolling = 2f * (s.Front.Rolling / car.Front.Radius + s.Rear.Rolling / car.Rear.Radius) / weight;
            return perG > 1f ? (1f - RearKeep) * s.Rear.Load / perG - rolling : 99f;
        }

        /// <summary>The arm the brakes and the engine pitch the body about: the centre of mass over the force points, and the anti-squat's part.</summary>
        public static float PitchArm(CarChassis car, HandlingSetup s)
        {
            return s.ComHeight - s.RollCentre - 0.5f * (car.Front.Radius + car.Rear.Radius) * s.Front.AntiSquat;
        }

        // The game's own steering curve; with the slider moved, its share at each speed times the slider (never past the
        // lock), the lock at walking pace kept.
        private static void Steering(CarChassis car, in HandlingTarget t, HandlingSetup s)
        {
            s.SteerOwn = System.Math.Abs(t.Steering - 1f) < 1e-4f;
            if (s.SteerOwn)
            {
                s.Steer.CopyFrom(car.Steer);
                return;
            }
            float[] share = Shares;
            for (int i = 0; i < SteerKeys; i++)
            {
                float game = car.Steer.Evaluate(SteerSpeeds[i] / HandlingTurn.SteerTop);
                float v = i == 0 ? game : game * t.Steering;
                share[i] = v > 1f ? 1f : v < 0.02f ? 0.02f : v;
            }
            s.Steer.Clear();
            for (int i = 0; i < SteerKeys; i++)
            {
                float at = SteerSpeeds[i] / HandlingTurn.SteerTop;
                float left = i > 0 ? (share[i] - share[i - 1]) / (at - SteerSpeeds[i - 1] / HandlingTurn.SteerTop) : 0f;
                float right = i < SteerKeys - 1 ? (share[i + 1] - share[i]) / (SteerSpeeds[i + 1] / HandlingTurn.SteerTop - at) : left;
                s.Steer.Add(at, share[i], i > 0 ? left : right, right);
            }
        }

        /// <summary>The full steering in degrees at a speed in m/s, as the game reads the setup's curve.</summary>
        public static float SteerAt(HandlingSetup s, float lockDegrees, float speed)
        {
            return s.Steer.Evaluate(speed / HandlingTurn.SteerTop) * lockDegrees;
        }

        // The full pedal's deceleration: the game's own on this car (BrakeFactor of it), each axle's brake share set for
        // it (PartsBrakes with no parts), and how much more of its tyres each axle asks at its worst moment as the body dives.
        private static void Brakes(CarChassis car, in HandlingTarget t, HandlingSetup s, float wheelbase)
        {
            s.GameBrakeG = GameBrakeG(car, s.ComHeight, s.GameRollCentre);
            s.BrakeTarget = s.GameBrakeG * t.BrakeFactor;
            s.BrakeRise = t.BrakeRise;
            s.BrakeG = PartsBrakes(car, s, 1f, 1f, 1f, out float front, out float rear, out float use);
            s.Front.BrakeShare = front;
            s.Rear.BrakeShare = rear;
            s.BrakeUse = use;
            float moved = s.Mass * G * s.BrakeG * PitchArm(car, s) / (2f * wheelbase);   // N per wheel at the full pedal
            BrakeRamp(s.BrakeRise, GameBrakeOnset, InputRate, 99f, Felt);
            s.FrontLag = BrakeLag(car.Front, s.Front, 1f, moved);
            s.RearLag = BrakeLag(car.Rear, s.Rear, 1f, -moved);
        }

        /// <summary>
        /// The game's own full pedal on the car, g: each wheel's brake torque (the most times its axle's share) and rolling
        /// resistance, as far as its tyre holds along at its load once the body has settled into the dive. The game's
        /// fronts lock where their torque is more than that, and a locked wheel there still pulls its most along.
        /// </summary>
        public static float GameBrakeG(CarChassis car, float comHeight, float rollCentre)
        {
            AxleData f = car.Front;
            AxleData r = car.Rear;
            float wheelbase = f.Z - r.Z;
            float weight = car.Mass * G;
            float nf = weight * (car.ComZ - r.Z) / (2f * wheelbase);
            float nr = weight * (f.Z - car.ComZ) / (2f * wheelbase);
            float arm = comHeight - rollCentre - 0.5f * (f.Radius + r.Radius) * 0.5f * (f.AntiSquat + r.AntiSquat);
            float decel = 1f;
            for (int i = 0; i < 40; i++)
            {
                float moved = weight * decel * arm / (2f * wheelbase);
                float front = AlongHeld(car, f, nf + moved, car.BrakeTorque * f.BrakeShare);
                float rear = AlongHeld(car, r, nr - moved, car.BrakeTorque * r.BrakeShare);
                decel = 2f * (front + rear) / weight;
            }
            return decel;
        }

        private static float AlongHeld(CarChassis car, AxleData axle, float load, float torque)
        {
            float most = car.TyreD * AlongFactor * axle.AlongGrip * (load > 0f ? (load < axle.LoadRating ? load : axle.LoadRating) : 0f);
            float asked = (torque + axle.Rolling) / axle.Radius;
            return asked < most ? asked : most;
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
            if (perG > 1f)
            {
                // Never so hard that the rear, the body settled in its dive, keeps less than RearKeep of its load.
                float lift = (1f - RearKeep) * nr / perG - rolling;
                wanted = wanted < lift ? wanted : lift;
            }
            BrakeRamp(s.BrakeRise, GameBrakeOnset, InputRate, 99f, Felt);
            float lagFront = 1f;
            float lagRear = 1f;
            float cap = MostUse;
            float a = Most(weight, nf, nr, mf, mr, perG, rollFront, rollRear, rolling, 1f, 1f, MostUse, wanted);   // the settled loads alone first
            for (int pass = 0; pass < 8; pass++)
            {
                // The axles' worst moments at this deceleration, and the most both take at them.
                lagFront = BrakeLag(car.Front, s.Front, massFactor, perG * (a + rolling));
                lagRear = BrakeLag(car.Rear, s.Rear, massFactor, -perG * (a + rolling));
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
        /// has settled into the dive: the corner's spring and damper holding its load times <paramref name="weight"/> (the
        /// Garage's weight part, whose springs keep the height and frequency), pressed down by <paramref name="moved"/> N at
        /// the full pedal (the front; below zero, lifted: the rear), the brakes coming on as <see cref="Felt"/> has them,
        /// stepped as the game's physics steps it. 1 is never more.
        /// </summary>
        private static float BrakeLag(AxleData shape, AxleSetup s, float weight, float moved)
        {
            float mass = s.Load * weight / G;
            float force = s.SpringForce * weight;
            float damping = (float)System.Math.Sqrt(weight);
            float bump = s.Bump * damping;
            float rebound = s.Rebound * damping;
            float settled = mass * G + moved;   // the load once the body has settled into the dive
            if (!(mass > 1f))
            {
                return 1f;
            }
            if (!(settled > 1f))
            {
                return 99f;   // the wheel would lift off the road
            }
            float at = SettledLength(shape, force, s.SpringLength, mass * G);
            float previous = at;
            float speed = 0f;
            float worst = 1f;
            for (int i = 0; i < Felt.Length; i++)
            {
                float load = Corner(shape, force, s.SpringLength, bump, rebound, at, (previous - at) / Step);   // the tyre's load: the spring and the damper
                float asked = Felt[i] * settled / (load > 1f ? load : 1f);
                worst = asked > worst ? asked : worst;
                previous = at;
                speed += ((load - moved * Felt[i]) / mass - G) * Step;
                at += speed * Step;
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
