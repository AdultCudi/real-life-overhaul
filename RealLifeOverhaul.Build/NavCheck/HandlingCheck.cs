using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using RealLifeOverhaul;

// The driven car's handling (Driving/HandlingRules.cs, Driving/HandlingModel.cs) on the game's own eighteen cars, whose
// figures below were read from the game's files: for every kind and level, and with every build of the Garage's
// springs, weight and tyres on top, the damping ratio from the body's swings in the game's own physics steps and the
// static sag against the travel (no bottoming out at rest or over a kerb); the body roll from the centre of mass, the
// tracks and the bars, and no tipping at the tyres' limit; the tyres' peak and plateau; the steering by speed; the
// brakes from the axle loads and the brake's ramp. Then, over the sources, every value the handling puts on the car is
// put back on every way out, the Garage's parts go on top of it, and only the car the player drives is ever touched.
internal static partial class Program
{
    private const float BandLowZeta = 0.20f;     // this ride's damping ratio: a swing or two, then still, never a pogo
    private const float BandHighZeta = 0.30f;    // (a usual road car is 0.35 to 0.5)
    private const float BandLowSag = 0.30f;      // the static sag's share of the spring's travel
    private const float BandHighSag = 0.68f;
    private const float KerbSpare = 0.01f;       // m of travel left over the kerb at the least

    private static readonly string[] HandlingKinds = { "honzamimic Economy", "vordtiaravic Saloon", "bima320 Saloon", "mersaidis500 Luxury", "missamvillian Luxury",
        "petrollsfanton Luxury", "limo Luxury", "vordpony Sports", "ferdinand112 Sports", "mersaidimgagt Sports", "anselmoaf90 Sports", "umcnunavut Utility",
        "vordv150 Utility", "umcdesert Van", "umcdesertdeliveryjob Van", "mersaididash Van", "deliverytruck Truck", "freighttruckt1 Truck" };

    // The game's own cars as its files have them (prefab figures, each car at the game's 1400 kg but the Pony's 1200):
    // the centre of mass, the brakes' torque per wheel, the steering lock, and per axle its place, track, spring tops,
    // wheel radius, spring, tyre grip, load rating, anti-roll bar, brake coefficient, dampers and force application point.
    private static List<(string type, CarChassis car)> GameCars()
    {
        return new List<(string, CarChassis)>
        {
            Car("anselmoaf90", 1400f, 0.3000f, 0.0000f, 10000f, 35f,
                Axle(1.3214f, 1.6177f, 0.4946f, 0.3500f, 10000f, 0.250f, 1.30f, 0.90f, 7000f, 4200f, 1.000f, 5000f, 5000f, 1.40f, 1.60f, 0.80f),
                Axle(-1.3069f, 1.6346f, 0.4946f, 0.3500f, 10000f, 0.250f, 1.50f, 1.50f, 7000f, 4200f, 0.184f, 5000f, 5000f, 1.40f, 1.60f, 0.80f)),
            Car("bima320", 1400f, 0.3000f, 0.0000f, 5000f, 35f,
                Axle(1.5620f, 1.6141f, 0.4958f, 0.3400f, 10000f, 0.300f, 1.30f, 0.90f, 7000f, 4200f, 1.000f, 5000f, 5000f, 1.40f, 1.60f, 0.75f),
                Axle(-1.3140f, 1.6141f, 0.4958f, 0.3400f, 10000f, 0.300f, 1.50f, 1.50f, 7000f, 4200f, 0.184f, 5000f, 5000f, 1.40f, 1.60f, 0.80f)),
            Car("deliverytruck", 1400f, 0.4000f, 0.6800f, 2500f, 42f,
                Axle(2.7580f, 2.0264f, 0.6100f, 0.4300f, 5000f, 0.600f, 1.30f, 0.90f, 7000f, 4200f, 1.000f, 5000f, 5000f, 1.40f, 1.60f, 0.20f),
                Axle(-1.3599f, 1.7478f, 0.6100f, 0.4300f, 5000f, 0.600f, 1.50f, 1.50f, 7000f, 4200f, 0.184f, 5000f, 5000f, 1.40f, 1.60f, 0.20f)),
            Car("ferdinand112", 1400f, 0.3000f, 0.0000f, 7000f, 28f,
                Axle(1.2740f, 1.5860f, 0.5014f, 0.3332f, 19300f, 0.200f, 1.30f, 0.90f, 7000f, 4200f, 1.000f, 5000f, 5000f, 1.40f, 1.60f, 0.80f),
                Axle(-1.0830f, 1.5860f, 0.5014f, 0.3332f, 19300f, 0.200f, 1.50f, 1.50f, 7000f, 4200f, 0.184f, 5000f, 5000f, 1.40f, 1.60f, 0.80f)),
            Car("freighttruckt1", 1400f, 0.4000f, 0.6800f, 2500f, 65f,
                Axle(4.3710f, 2.0000f, 0.5946f, 0.5420f, 5000f, 0.600f, 1.30f, 0.90f, 7000f, 4200f, 1.000f, 5000f, 5000f, 1.40f, 1.60f, 0.20f),
                Axle(-2.5050f, 2.2400f, 0.5946f, 0.5432f, 5000f, 0.600f, 1.50f, 1.50f, 7000f, 4200f, 0.184f, 5000f, 5000f, 1.40f, 1.60f, 0.20f)),
            Car("honzamimic", 1400f, 0.3000f, 0.0000f, 2500f, 35f,
                Axle(1.4073f, 1.5038f, 0.4698f, 0.3054f, 5000f, 0.600f, 1.30f, 0.90f, 7000f, 4200f, 1.000f, 5000f, 5000f, 1.40f, 1.60f, 0.20f),
                Axle(-1.2937f, 1.5038f, 0.4698f, 0.3054f, 5000f, 0.600f, 1.50f, 1.50f, 7000f, 4200f, 0.184f, 5000f, 5000f, 1.40f, 1.60f, 0.20f)),
            Car("limo", 1400f, 0.3000f, 0.0081f, 5000f, 40f,
                Axle(1.6816f, 1.5343f, 0.5291f, 0.3611f, 5000f, 0.500f, 1.30f, 0.90f, 7000f, 4200f, 1.000f, 5000f, 5000f, 1.40f, 1.60f, 0.48f),
                Axle(-2.0664f, 1.5343f, 0.5291f, 0.3611f, 5000f, 0.500f, 1.50f, 1.50f, 7000f, 4200f, 0.184f, 5000f, 5000f, 1.40f, 1.60f, 0.48f)),
            Car("mersaididash", 1400f, 0.6000f, 0.8000f, 2500f, 35f,
                Axle(2.4000f, 1.7000f, 0.3600f, 0.4027f, 5000f, 0.400f, 1.30f, 0.90f, 7000f, 4200f, 1.000f, 5000f, 5000f, 1.40f, 1.60f, 0.50f),
                Axle(-1.6200f, 1.7000f, 0.3600f, 0.4027f, 5000f, 0.400f, 1.50f, 1.50f, 7000f, 4200f, 0.184f, 5000f, 5000f, 1.40f, 1.60f, 0.50f)),
            Car("mersaidimgagt", 1400f, 0.3000f, 0.0000f, 10000f, 35f,
                Axle(1.3757f, 1.6514f, 0.4920f, 0.3332f, 10000f, 0.250f, 1.30f, 0.90f, 7000f, 4200f, 1.000f, 5000f, 5000f, 1.40f, 1.60f, 0.80f),
                Axle(-1.2710f, 1.6514f, 0.4920f, 0.3332f, 10000f, 0.250f, 1.50f, 1.50f, 7000f, 4200f, 0.184f, 5000f, 5000f, 1.40f, 1.60f, 0.80f)),
            Car("mersaidis500", 1400f, 0.3000f, 0.0000f, 10000f, 35f,
                Axle(1.7095f, 1.6206f, 0.5130f, 0.3689f, 19300f, 0.200f, 1.30f, 0.90f, 7000f, 4200f, 1.000f, 5000f, 5000f, 1.40f, 1.60f, 0.80f),
                Axle(-1.4190f, 1.6206f, 0.5130f, 0.3689f, 19300f, 0.200f, 1.50f, 1.50f, 7000f, 4200f, 0.184f, 5000f, 5000f, 1.40f, 1.60f, 0.80f)),
            Car("missamvillian", 1400f, 0.3000f, 0.0000f, 10000f, 35f,
                Axle(1.5475f, 1.5778f, 0.4193f, 0.3680f, 5000f, 0.200f, 1.30f, 0.90f, 7000f, 4200f, 1.000f, 5000f, 5000f, 1.40f, 1.60f, 0.20f),
                Axle(-1.2573f, 1.5778f, 0.4193f, 0.3680f, 5000f, 0.200f, 1.50f, 1.50f, 7000f, 4200f, 0.184f, 5000f, 5000f, 1.40f, 1.60f, 0.20f)),
            Car("petrollsfanton", 1400f, 0.3000f, 0.3200f, 10000f, 40f,
                Axle(2.0390f, 1.6205f, 0.5291f, 0.4000f, 5000f, 0.500f, 1.30f, 0.90f, 7000f, 4200f, 1.000f, 5000f, 5000f, 1.40f, 1.60f, 0.48f),
                Axle(-1.4860f, 1.6205f, 0.5291f, 0.4000f, 5000f, 0.500f, 1.50f, 1.50f, 7000f, 4200f, 0.184f, 5000f, 5000f, 1.40f, 1.60f, 0.48f)),
            Car("umcdesert", 1400f, 0.6000f, 0.0900f, 2500f, 35f,
                Axle(1.8230f, 1.6879f, 0.5342f, 0.4027f, 5000f, 0.600f, 1.30f, 0.90f, 7000f, 4200f, 1.000f, 5000f, 5000f, 1.40f, 1.60f, 0.50f),
                Axle(-1.6480f, 1.7000f, 0.5342f, 0.4027f, 5000f, 0.600f, 1.50f, 1.50f, 7000f, 4200f, 0.184f, 5000f, 5000f, 1.40f, 1.60f, 0.50f)),
            Car("umcdesertdeliveryjob", 1400f, 0.6000f, 0.0900f, 2500f, 35f,
                Axle(1.8230f, 1.6879f, 0.5342f, 0.4027f, 5000f, 0.600f, 1.30f, 0.90f, 7000f, 4200f, 1.000f, 5000f, 5000f, 1.40f, 1.60f, 0.50f),
                Axle(-1.6480f, 1.7000f, 0.5342f, 0.4027f, 5000f, 0.600f, 1.50f, 1.50f, 7000f, 4200f, 0.184f, 5000f, 5000f, 1.40f, 1.60f, 0.50f)),
            Car("umcnunavut", 1400f, 0.1500f, 0.1000f, 6000f, 35f,
                Axle(1.6397f, 1.7330f, 0.5546f, 0.4027f, 7000f, 0.400f, 1.30f, 0.90f, 7000f, 4200f, 1.000f, 5000f, 5000f, 1.40f, 1.60f, 0.80f),
                Axle(-1.3896f, 1.7330f, 0.5546f, 0.4027f, 7000f, 0.400f, 1.50f, 1.50f, 7000f, 4200f, 0.184f, 5000f, 5000f, 1.40f, 1.60f, 0.80f)),
            Car("vordpony", 1200f, 0.4000f, 0.0000f, 5500f, 40f,
                Axle(1.4170f, 1.5536f, 0.4955f, 0.3332f, 19300f, 0.200f, 1.30f, 0.90f, 7000f, 4200f, 1.000f, 5000f, 5000f, 1.40f, 1.60f, 0.80f),
                Axle(-1.3638f, 1.6036f, 0.4955f, 0.3332f, 19300f, 0.200f, 1.50f, 1.50f, 7000f, 4200f, 0.184f, 5000f, 5000f, 1.40f, 1.60f, 0.80f)),
            Car("vordtiaravic", 1400f, 0.3000f, 0.0000f, 3500f, 35f,
                Axle(1.3970f, 1.5038f, 0.4805f, 0.3054f, 5000f, 0.600f, 1.30f, 0.90f, 7000f, 4200f, 1.000f, 5000f, 5000f, 1.40f, 1.60f, 0.20f),
                Axle(-1.2680f, 1.5038f, 0.4805f, 0.3054f, 5000f, 0.600f, 1.50f, 1.50f, 7000f, 4200f, 0.184f, 5000f, 5000f, 1.40f, 1.60f, 0.20f)),
            Car("vordv150", 1400f, 0.3000f, 0.0000f, 4000f, 45f,
                Axle(1.5109f, 1.5496f, 0.5630f, 0.4027f, 5000f, 0.600f, 1.30f, 0.90f, 7000f, 4200f, 1.000f, 5000f, 5000f, 1.40f, 1.60f, 0.20f),
                Axle(-1.6868f, 1.5496f, 0.5630f, 0.4027f, 5000f, 0.600f, 1.50f, 1.50f, 7000f, 4200f, 0.184f, 5000f, 5000f, 1.40f, 1.60f, 0.20f)),
        };
    }

    private static (string, CarChassis) Car(string type, float mass, float comY, float comZ, float brakeTorque, float steerLock, AxleData front, AxleData rear)
    {
        var car = new CarChassis { Mass = mass, ComY = comY, ComZ = comZ, BrakeTorque = brakeTorque, SteerLock = steerLock };
        Copy(front, car.Front);
        Copy(rear, car.Rear);
        return (type, car);
    }

    private static AxleData Axle(float z, float track, float top, float radius, float springForce, float springLength, float side, float along, float rating,
        float antiRoll, float brakeShare, float bump, float rebound, float slowBump, float slowRebound, float application)
    {
        return new AxleData
        {
            Z = z, Track = track, Top = top, Radius = radius, SpringForce = springForce, SpringLength = springLength, SideGrip = side, AlongGrip = along,
            LoadRating = rating, AntiRoll = antiRoll, BrakeShare = brakeShare, Bump = bump, Rebound = rebound, SlowBump = slowBump, SlowRebound = slowRebound,
            Application = application
        };
    }

    private static void Copy(AxleData from, AxleData to)
    {
        to.Z = from.Z; to.Track = from.Track; to.Top = from.Top; to.Radius = from.Radius; to.SpringForce = from.SpringForce; to.SpringLength = from.SpringLength;
        to.SideGrip = from.SideGrip; to.AlongGrip = from.AlongGrip; to.LoadRating = from.LoadRating; to.AntiRoll = from.AntiRoll; to.BrakeShare = from.BrakeShare;
        to.Bump = from.Bump; to.Rebound = from.Rebound; to.SlowBump = from.SlowBump; to.SlowRebound = from.SlowRebound; to.Application = from.Application;
    }

    private static CarKind KindOf(string type)
    {
        foreach (string row in HandlingKinds)
        {
            if (row.StartsWith(type + " ", StringComparison.Ordinal))
            {
                return (CarKind)Enum.Parse(typeof(CarKind), row.Substring(type.Length + 1));
            }
        }
        return CarKind.Untouched;
    }

    // One Garage build on top of the handling, as GarageCar puts it on: springs stiffer and the car standing higher or
    // lower (HandlingModel.FitSpring), dampers by the root of the springs' factor (the same damping ratio), the roll
    // centre moved with the body, bars by the factor, the weight by the weight part. What the car then does.
    private struct BuildResult
    {
        public float ZetaFront, ZetaRear, SagFront, SagRear, KerbFront, KerbRear, Roll, Tip, RateFront;
    }

    private static BuildResult OnBuild(CarChassis car, HandlingSetup s, float stiffness, float ride, float massFactor, float sideFactor)
    {
        var result = new BuildResult();
        float mass = s.Mass * massFactor;
        AxleSetup front = BuiltAxle(s.Front, stiffness, ride, massFactor);
        AxleSetup rear = BuiltAxle(s.Rear, stiffness, ride, massFactor);
        result.ZetaFront = HandlingModel.Bounce(front, front.Load / HandlingModel.G, out _, out _);
        result.ZetaRear = HandlingModel.Bounce(rear, rear.Load / HandlingModel.G, out _, out _);
        result.SagFront = front.Sag / front.SpringLength;
        result.SagRear = rear.Sag / rear.SpringLength;
        result.KerbFront = HandlingModel.KerbLeft(front, front.Load / HandlingModel.G, HandlingModel.Kerb);
        result.KerbRear = HandlingModel.KerbLeft(rear, rear.Load / HandlingModel.G, HandlingModel.Kerb);
        result.RateFront = front.Rate;
        // The body stands where the springs now hold it: the centre of mass and the roll centre move with the ride.
        float dropFront = s.Front.RestLength - front.RestLength;
        float dropRear = s.Rear.RestLength - rear.RestLength;
        float com = s.ComHeight - 0.5f * (dropFront + dropRear);
        float centre = 0.5f * (front.Application * front.SpringLength + rear.Application * rear.SpringLength);   // where the Garage puts it
        result.Roll = HandlingModel.RollPerG(car, mass, com, centre, front.Rate, rear.Rate, front.AntiRoll, rear.AntiRoll);
        var built = new HandlingSetup { ComHeight = com, RollCentre = centre, RollPerG = result.Roll };
        float best = Math.Max(s.Front.SideG, s.Rear.SideG) * sideFactor;
        result.Tip = HandlingModel.TipG(car, built, best) / best;
        return result;
    }

    private static AxleSetup BuiltAxle(AxleSetup s, float stiffness, float ride, float massFactor)
    {
        var a = new AxleSetup();
        float rest = HandlingModel.RestCompression(c => HandlingModel.SpringAt(s, c) / s.SpringForce, s.SpringForce, s.Load * massFactor);
        HandlingModel.FitSpring(s.SpringForce, s.SpringLength, rest, stiffness, ride, GarageCatalogue.MaxLower, GarageCatalogue.MaxRaise, out a.SpringForce, out a.SpringLength, out float change);
        for (int i = 0; i < HandlingModel.CurveKeys; i++)
        {
            a.CurveAt[i] = s.CurveAt[i];
            a.CurveShare[i] = s.CurveShare[i];
        }
        a.Bump = s.Bump * (float)Math.Sqrt(stiffness);
        a.Rebound = s.Rebound * (float)Math.Sqrt(stiffness);
        a.Knee = s.Knee;
        a.Fast = s.Fast;
        a.Application = (s.Application * s.SpringLength + change) / a.SpringLength;   // the roll centre moves with the body
        a.AntiRoll = s.AntiRoll * stiffness;
        a.Load = s.Load * massFactor;
        a.Rate = s.Rate * stiffness;
        float compressed = HandlingModel.RestCompression(c => HandlingModel.SpringAt(a, c) / a.SpringForce, a.SpringForce, a.Load);
        a.Sag = compressed * a.SpringLength;
        a.RestLength = a.SpringLength - a.Sag;
        return a;
    }

    private static void Handling(bool print)
    {
        string scripts = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        List<(string type, CarChassis car)> cars = GameCars();
        var setup = new HandlingSetup();
        var twin = new HandlingSetup();
        HandlingTuning neutral = HandlingTuning.Neutral;
        var rollByKind = new Dictionary<CarKind, float>();
        var countByKind = new Dictionary<CarKind, int>();
        float worstZetaLow = 1f, worstZetaHigh = 0f;
        int builds = 0;
        if (print)
        {
            Console.WriteLine("handling: car, kind | kg before -> after | com h | front k, sag, travel, length, zeta | roll deg/g, centre, bars | dive | grip side f/r, along | brake g, shares | tip g");
        }
        foreach ((string type, CarChassis car) in cars)
        {
            CarKind kind = KindOf(type);
            Check(kind != CarKind.Untouched, "handling: " + type + " is a car of a kind");
            foreach (HandlingLevel level in new[] { HandlingLevel.Light, HandlingLevel.Full, HandlingLevel.Heavy })
            {
                HandlingTarget target = HandlingRules.Target(kind, level, neutral);
                string why = HandlingModel.Design(car, target, setup);
                Check(why == null, "handling: " + type + " at " + level + " is designed (" + (why ?? "yes") + ")");
                if (why != null)
                {
                    continue;
                }
                string name = type + " (" + kind + ", " + level + ")";

                // ---- the ride: the damping ratio from the swings, the sag against the travel, no bottoming out.
                foreach ((string end, AxleSetup axle) in new[] { ("front", setup.Front), ("rear", setup.Rear) })
                {
                    float zeta = HandlingModel.Bounce(axle, axle.Load / HandlingModel.G, out int swings, out float after2s);
                    worstZetaLow = Math.Min(worstZetaLow, zeta);
                    worstZetaHigh = Math.Max(worstZetaHigh, zeta);
                    Check(zeta >= BandLowZeta && zeta <= BandHighZeta, "handling: " + name + " " + end + ": the body's swings decay at a damping ratio of " + F(zeta) + " (band " + F(BandLowZeta) + " to " + F(BandHighZeta) + ")");
                    Check(swings >= 1 && swings <= 2 && after2s < 0.002f, "handling: " + name + " " + end + ": a swing or two anyone sees and then still (" + swings + " over 5 mm, " + F(after2s * 1000f) + " mm left after 2 s)");
                    float share = axle.Sag / axle.SpringLength;
                    Check(share >= BandLowSag && share <= BandHighSag, "handling: " + name + " " + end + ": the static sag is " + F(share) + " of the spring's travel (" + F(axle.Sag) + " of " + F(axle.SpringLength) + " m)");
                    Check(axle.RestLength >= HandlingModel.MinTravel - 1e-4f && axle.Sag < axle.SpringLength * axle.CurveAt[1], "handling: " + name + " " + end + ": at rest it stands on the straight part of the spring with "
                        + F(axle.RestLength) + " m to compress (at least " + F(HandlingModel.MinTravel) + ")");
                    float left = HandlingModel.KerbLeft(axle, axle.Load / HandlingModel.G, HandlingModel.Kerb);
                    Check(left >= KerbSpare, "handling: " + name + " " + end + ": a " + F(HandlingModel.Kerb * 100f) + " cm kerb leaves " + F(left * 100f) + " cm of travel (never bottoms out)");
                    Check(axle.Frequency >= 0.9f && axle.Frequency <= 1.7f, "handling: " + name + " " + end + ": rides at " + F(axle.Frequency) + " Hz (soft, long travel)");
                    Check(axle.Rebound > axle.Bump, "handling: " + name + " " + end + ": lets the body back up slower than it pushes it down (rebound " + F(axle.Rebound) + " against bump " + F(axle.Bump) + " N s/m)");
                }

                // ---- the body's roll and the weight moving forward and back.
                Check(setup.RollPerG >= 1.5f && setup.RollPerG <= target.RollPerG * 1.02f, "handling: " + name + " rolls " + F(setup.RollPerG) + " degrees per g (target " + F(target.RollPerG) + ")");
                Check(setup.RollCentre >= 0f && setup.RollCentre <= target.RollCentre + 1e-4f && setup.Front.AntiRoll >= 0f && setup.Rear.AntiRoll >= 0f,
                    "handling: " + name + " has its roll centre on or above the road (" + F(setup.RollCentre) + " m) and bars that hold, never push (" + F(setup.Front.AntiRoll) + ", " + F(setup.Rear.AntiRoll) + " N/m)");
                Check(setup.DivePerG >= 0.5f && setup.DivePerG <= target.DivePerG * 1.05f && setup.Front.AntiSquat >= -1f && setup.Front.AntiSquat <= 1f,
                    "handling: " + name + " dives " + F(setup.DivePerG) + " degrees per g of braking (target " + F(target.DivePerG) + ", anti-squat " + F(setup.Front.AntiSquat) + ")");
                Check(setup.ComHeight >= setup.ComHeightBefore - 1e-4f && setup.ComHeight < 1.2f, "handling: " + name + "'s centre of mass is " + F(setup.ComHeight) + " m up (the game's " + F(setup.ComHeightBefore) + ")");
                if (level == HandlingLevel.Full)
                {
                    rollByKind[kind] = (rollByKind.TryGetValue(kind, out float sum) ? sum : 0f) + setup.RollPerG;
                    countByKind[kind] = (countByKind.TryGetValue(kind, out int n) ? n : 0) + 1;
                }

                // ---- the tyres: a peak where asked, most of it kept well past it, the rear holding a little more.
                foreach ((string end, AxleSetup axle) in new[] { ("front", setup.Front), ("rear", setup.Rear) })
                {
                    float peakValue = HandlingModel.MagicFormula(axle.TyreB, axle.TyreC, axle.TyreSide, axle.TyreE, axle.PeakSlip);
                    float before = HandlingModel.MagicFormula(axle.TyreB, axle.TyreC, axle.TyreSide, axle.TyreE, axle.PeakSlip * 0.9f);
                    float after = HandlingModel.MagicFormula(axle.TyreB, axle.TyreC, axle.TyreSide, axle.TyreE, axle.PeakSlip * 1.1f);
                    float far = HandlingModel.MagicFormula(axle.TyreB, axle.TyreC, axle.TyreSide, axle.TyreE, Math.Min(1.02f, axle.PeakSlip * 3f));
                    float half = HandlingModel.MagicFormula(axle.TyreB, axle.TyreC, axle.TyreSide, axle.TyreE, axle.PeakSlip * 0.5f);
                    Check(peakValue >= before && peakValue >= after && Near(peakValue, axle.TyreSide, axle.TyreSide * 0.01f), "handling: " + name + " " + end + " tyres give their most (" + F(peakValue) + ") at " + F(axle.PeakDegrees) + " degrees of slip");
                    Check(far >= target.Plateau * axle.TyreSide * 0.97f, "handling: " + name + " " + end + " tyres keep " + F(far / axle.TyreSide) + " of their peak at three times its slip: they slide, never snap");
                    Check(half / axle.TyreSide >= 0.75f && half / axle.TyreSide <= 0.93f, "handling: " + name + " " + end + " tyres give " + F(half / axle.TyreSide) + " of their peak at half its slip: they build progressively");
                    Check(axle.PeakSlip > 0.04f && axle.PeakSlip < 0.22f, "handling: " + name + " " + end + " peak (" + F(axle.PeakSlip) + ") lies where the game's preset curve is sampled closely");
                    Check(axle.TyreAlong >= axle.TyreSide, "handling: " + name + " " + end + " peak along (" + F(axle.TyreAlong) + ") never caps the side force (" + F(axle.TyreSide) + ")");
                }
                Check(setup.Rear.SideG > setup.Front.SideG && setup.Rear.SideG <= setup.Front.SideG * (1f + target.RearSurplus) * 1.001f,
                    "handling: " + name + "'s rear tyres hold a little more (" + F(setup.Rear.SideG) + " g against " + F(setup.Front.SideG) + "): pushed too hard it runs wide");
                Check(setup.Rear.PeakDegrees > setup.Front.PeakDegrees, "handling: " + name + "'s rear tyres peak later (" + F(setup.Rear.PeakDegrees) + " degrees against " + F(setup.Front.PeakDegrees) + "): the tail lets go gently");
                Check(Near(setup.Front.AlongG, setup.Rear.AlongG, 0.02f), "handling: " + name + " brakes and pulls on both axles alike (" + F(setup.Front.AlongG) + " and " + F(setup.Rear.AlongG) + " g along)");

                // ---- no tipping at the limit, even on the Garage's grippiest tyres.
                float semi = GarageCatalogue.Part("tyres_semi").Grip;
                float own = Math.Max(setup.Front.SideG, setup.Rear.SideG);
                float best = own * semi;
                float tip = HandlingModel.TipG(car, setup, best);
                Check(tip >= best * 1.1f && HandlingModel.TipG(car, setup, own) >= own * 1.25f, "handling: " + name + " would tip only at " + F(tip) + " g, past its grippiest tyres' " + F(best)
                    + " g, and at " + F(HandlingModel.TipG(car, setup, own)) + " g on its own tyres' " + F(own) + " g, a sudden turn's swing of the body counted");

                // ---- the steering by speed.
                float lockDegrees = car.SteerLock;
                float walking = HandlingModel.SteerAt(setup, lockDegrees, 1.4f);
                float town = HandlingModel.SteerAt(setup, lockDegrees, 50f / 3.6f);
                float fast = HandlingModel.SteerAt(setup, lockDegrees, 100f / 3.6f);
                float top = HandlingModel.SteerAt(setup, lockDegrees, 50f);
                Check(Near(walking, lockDegrees, 0.01f), "handling: " + name + " steers to its full lock (" + F(lockDegrees) + " degrees) at walking pace: " + F(walking));
                Check(town < lockDegrees && fast < town && top <= fast, "handling: " + name + " steers calmer as it goes: " + F(walking) + ", " + F(town) + ", " + F(fast) + ", " + F(top) + " degrees at 5, 50, 100, 180 km/h");
                Check(fast >= setup.Front.PeakDegrees && top >= setup.Front.PeakDegrees, "handling: " + name + " can still reach its front tyres' limit at speed (" + F(top) + " degrees at 180 km/h, peak slip " + F(setup.Front.PeakDegrees) + ")");
                bool down = true;
                for (int i = 1; i < HandlingModel.SteerKeys; i++)
                {
                    down &= setup.SteerShare[i] <= setup.SteerShare[i - 1] + 1e-6f && setup.SteerAt[i] > setup.SteerAt[i - 1];
                }
                Check(down, "handling: " + name + "'s steering never grows with speed");
                Check(setup.SmoothTime[1] > setup.SmoothTime[0] && Near(setup.SmoothTime[0], HandlingModel.SmoothAtRest, 1e-4f), "handling: " + name + "'s wheels follow the key more gently at speed (" + F(setup.SmoothTime[0]) + " to " + F(setup.SmoothTime[1]) + " s)");

                // ---- the brakes: the full pedal inside the tyres, the front and the rear at their limit together.
                float arm = HandlingModel.PitchArm(car, setup);
                float wheelbase = car.Front.Z - car.Rear.Z;
                float decel = setup.BrakeG;
                float moved = setup.Mass * HandlingModel.G * decel * arm / (2f * wheelbase);
                float frontForce = 2f * setup.Front.BrakeShare * car.BrakeTorque / car.Front.Radius;
                float rearForce = 2f * setup.Rear.BrakeShare * car.BrakeTorque / car.Rear.Radius;
                float frontHold = 2f * setup.Front.AlongG * (setup.Front.Load + moved);
                float rearHold = 2f * setup.Rear.AlongG * Math.Max(0f, setup.Rear.Load - moved);
                Check(frontForce <= frontHold * 1.001f && rearForce <= rearHold * 1.001f, "handling: " + name + "'s full pedal stays inside the tyres (front " + F(frontForce / frontHold) + ", rear " + F(rearForce / Math.Max(1f, rearHold)) + " of what they hold): no lock on a dry road");
                Check(decel >= 0.5f && decel <= target.BrakeShare * target.AlongGrip * 1.001f, "handling: " + name + " stops at " + F(decel) + " g with the full pedal (design " + F(target.BrakeShare * target.AlongGrip) + ")");
                Check(setup.Front.BrakeShare > 0f && setup.Front.BrakeShare <= 1f && setup.Rear.BrakeShare >= 0f && setup.Rear.BrakeShare <= 1f, "handling: " + name + "'s brake shares are the game's own range");

                // ---- with every Garage build of springs, weight and tyres on top, wet and dry: the worst of them, named.
                float zetaLow = 1f, zetaHigh = 0f, sagLow = 1f, sagHigh = 0f, kerbLeast = 1f, tipLeast = 99f, stiffLeast = 99f;
                string zetaLowAt = null, zetaHighAt = null, sagAt = null, kerbAt = null, tipAt = null, stiffAt = null;
                foreach (PartDef spring in PartsOf(PartSlot.Suspension))
                {
                    if (spring != null && spring.UtilityOnly && kind != CarKind.Utility && kind != CarKind.Van)
                    {
                        continue;
                    }
                    foreach (PartDef weight in PartsOf(PartSlot.Weight))
                    {
                        foreach (PartDef tyre in PartsOf(PartSlot.Tyres))
                        {
                            foreach (bool wet in new[] { false, true })
                            {
                                float stiffness = spring != null ? spring.Stiff : 1f;
                                float ride = spring != null ? spring.Ride : 0f;
                                float massFactor = weight != null ? weight.Mass : 1f;
                                float grip = tyre == null ? 1f : wet ? tyre.WetGrip : tyre.Grip;
                                BuildResult built = OnBuild(car, setup, stiffness, ride, massFactor, grip);
                                builds++;
                                string build = (spring != null ? spring.Id : "stock springs") + ", " + (weight != null ? weight.Id : "stock weight") + ", "
                                               + (tyre != null ? tyre.Id : "stock tyres") + (wet ? ", wet" : ", dry");
                                float lowZ = Math.Min(built.ZetaFront, built.ZetaRear), highZ = Math.Max(built.ZetaFront, built.ZetaRear);
                                if (lowZ < zetaLow) { zetaLow = lowZ; zetaLowAt = build; }
                                if (highZ > zetaHigh) { zetaHigh = highZ; zetaHighAt = build; }
                                float lowS = Math.Min(built.SagFront, built.SagRear), highS = Math.Max(built.SagFront, built.SagRear);
                                if (lowS < sagLow || highS > sagHigh) { sagAt = build; }
                                sagLow = Math.Min(sagLow, lowS);
                                sagHigh = Math.Max(sagHigh, highS);
                                float kerb = Math.Min(built.KerbFront, built.KerbRear);
                                if (kerb < kerbLeast) { kerbLeast = kerb; kerbAt = build; }
                                if (built.Tip < tipLeast) { tipLeast = built.Tip; tipAt = build; }
                                if (spring != null && built.RateFront / setup.Front.Rate < stiffLeast) { stiffLeast = built.RateFront / setup.Front.Rate; stiffAt = build; }
                            }
                        }
                    }
                }
                Check(zetaLow >= BandLowZeta && zetaHigh <= BandHighZeta, "handling: " + name + " with every Garage build: damping ratio " + F(zetaLow) + " (" + zetaLowAt + ") to " + F(zetaHigh)
                                                                          + " (" + zetaHighAt + "), band " + F(BandLowZeta) + " to " + F(BandHighZeta));
                Check(sagLow >= BandLowSag * 0.8f && sagHigh <= BandHighSag, "handling: " + name + " with every Garage build: static sag " + F(sagLow) + " to " + F(sagHigh) + " of the travel (" + sagAt + ")");
                Check(kerbLeast >= KerbSpare, "handling: " + name + " with every Garage build: a kerb leaves at least " + F(kerbLeast * 100f) + " cm of travel (" + kerbAt + ")");
                Check(stiffLeast > 1f, "handling: " + name + " with every Garage spring: at least " + F(stiffLeast) + " times as stiff as the handling's own (" + stiffAt + ")");
                Check(tipLeast >= 1.1f, "handling: " + name + " with every Garage build: tips only at " + F(tipLeast) + " times its grip at the least, a sudden turn counted (" + tipAt + ")");

                // ---- the same car twice gives the same answer.
                HandlingModel.Design(car, target, twin);
                Check(twin.Front.SpringForce == setup.Front.SpringForce && twin.Rear.Bump == setup.Rear.Bump && twin.Front.TyreB == setup.Front.TyreB && twin.SteerShare[5] == setup.SteerShare[5]
                      && twin.Front.BrakeShare == setup.Front.BrakeShare && twin.ComY == setup.ComY, "handling: " + name + " comes out the same every time");

                if (print && level == HandlingLevel.Full)
                {
                    Console.WriteLine("handling: " + type + ", " + kind + " | " + F(car.Mass) + " -> " + F(setup.Mass) + " | " + F(setup.ComHeightBefore) + " -> " + F(setup.ComHeight) + " | "
                                      + F(setup.Front.Rate) + ", " + F(setup.Front.Sag) + ", " + F(setup.Front.RestLength) + (setup.Front.Raised > 0.001f ? " (+" + F(setup.Front.Raised) + ")" : "") + ", "
                                      + F(setup.Front.SpringLength) + ", " + F(HandlingModel.Bounce(setup.Front, setup.Front.Load / HandlingModel.G, out _, out _)) + " | "
                                      + F(setup.RollPerG) + ", " + F(setup.RollCentre) + ", " + F(setup.Front.AntiRoll) + "/" + F(setup.Rear.AntiRoll) + " | " + F(setup.DivePerG) + " (" + F(setup.Front.AntiSquat) + ") | "
                                      + F(setup.Front.SideG) + "/" + F(setup.Rear.SideG) + ", " + F(setup.Front.AlongG) + " | " + F(setup.BrakeG) + ", " + F(setup.Front.BrakeShare) + "/" + F(setup.Rear.BrakeShare) + " | "
                                      + F(HandlingModel.TipG(car, setup, Math.Max(setup.Front.SideG, setup.Rear.SideG) * semi)) + " | steer " + F(HandlingModel.SteerAt(setup, car.SteerLock, 50f / 3.6f)) + "/"
                                      + F(HandlingModel.SteerAt(setup, car.SteerLock, 100f / 3.6f)) + "/" + F(HandlingModel.SteerAt(setup, car.SteerLock, 50f)));
                }
            }
        }
        Check(builds > 1000, "handling: every car with every Garage build of springs, weight and tyres, wet and dry (" + builds + ")");
        Check(Mean(rollByKind, countByKind, CarKind.Van) > Mean(rollByKind, countByKind, CarKind.Sports) && Mean(rollByKind, countByKind, CarKind.Utility) > Mean(rollByKind, countByKind, CarKind.Sports),
            "handling: a van (" + F(Mean(rollByKind, countByKind, CarKind.Van)) + ") and an SUV (" + F(Mean(rollByKind, countByKind, CarKind.Utility)) + ") roll more than a sports car (" + F(Mean(rollByKind, countByKind, CarKind.Sports)) + " degrees per g)");

        // ---- the levels: lighter, the whole feel, heavier and slidier.
        HandlingTarget light = HandlingRules.Target(CarKind.Saloon, HandlingLevel.Light, neutral);
        HandlingTarget full = HandlingRules.Target(CarKind.Saloon, HandlingLevel.Full, neutral);
        HandlingTarget heavy = HandlingRules.Target(CarKind.Saloon, HandlingLevel.Heavy, neutral);
        Check(light.MassShare < full.MassShare && heavy.Mass > full.Mass && light.SideGrip > full.SideGrip && heavy.SideGrip < full.SideGrip && light.RollPerG < full.RollPerG
              && heavy.RearSurplus < full.RearSurplus && light.RearSurplus > full.RearSurplus && heavy.PeakSlip > full.PeakSlip,
            "handling: Light is lighter and grippier, Heavy heavier and slidier, than the whole feel");
        HandlingTarget van = HandlingRules.Target(CarKind.Van, HandlingLevel.Full, neutral);
        HandlingTarget sports = HandlingRules.Target(CarKind.Sports, HandlingLevel.Full, neutral);
        Check(van.Mass > sports.Mass && van.RollPerG > sports.RollPerG && van.SideGrip < sports.SideGrip, "handling: a van is heavier, rollier and less grippy than a sports car");
        Check(HandlingRules.Level(0) == HandlingLevel.Off && HandlingRules.Level(3) == HandlingLevel.Heavy && HandlingRules.Level(7) == HandlingLevel.Full && HandlingRules.Level(-1) == HandlingLevel.Full,
            "handling: an unknown choice of the option is the whole feel");

        // ---- the sliders: each moves its own figure, within the bounds.
        var tuned = new HandlingTuning(150, 150, 150, 150, 150, 150);
        HandlingTarget soft = HandlingRules.Target(CarKind.Saloon, HandlingLevel.Full, tuned);
        Check(soft.RideHz < full.RideHz && soft.Damping < full.Damping && soft.RollPerG > full.RollPerG && soft.SideGrip > full.SideGrip && soft.SteerReach > full.SteerReach && soft.BrakeRise < full.BrakeRise,
            "handling: the sliders soften the springs, lighten the damping, add roll, grip, steering reach and a quicker brake");
        var wild = new HandlingTuning(0, 999, -5, 151, 49, 1000);
        Check(wild.Softness == HandlingTuning.Least && wild.Bounce == HandlingTuning.Most && wild.Roll == HandlingTuning.Least && wild.Grip == HandlingTuning.Most && wild.Steering == HandlingTuning.Least
              && wild.Brakes == HandlingTuning.Most, "handling: a slider's value is kept inside 50 to 150 per cent");
        Check(HandlingTuning.Neutral.Stamp != tuned.Stamp && new HandlingTuning(100, 100, 100, 100, 100, 101).Stamp != HandlingTuning.Neutral.Stamp, "handling: any slider moved changes the stamp the car is put on again by");

        // ---- a car that is not a car is refused, and nothing is put on it.
        var odd = new CarChassis { Mass = 1400f, ComY = 0.3f, ComZ = 0f, BrakeTorque = 3000f, SteerLock = 35f };
        Check(HandlingModel.Design(odd, full, setup) != null, "handling: a car with no axles is refused");
        CarChassis bima = GameCars()[1].car;
        bima.Rear.SpringForce = 0f;
        Check(HandlingModel.Design(bima, full, setup) != null, "handling: a car with a spring of no strength is refused");

        // ---- the brake's ramp: builds progressively, a tap is gentle, it lets go quickly.
        var felt = new float[60];
        foreach (float actuation in new[] { 0.1f, 0.25f })
        {
            foreach (HandlingLevel level in new[] { HandlingLevel.Light, HandlingLevel.Full, HandlingLevel.Heavy })
            {
                float rise = HandlingRules.Target(CarKind.Saloon, level, neutral).BrakeRise;
                HandlingModel.BrakeRamp(rise, actuation, 60f, 99f, felt);
                float half = -1f, most = -1f, jump = 0f;
                for (int i = 0; i < felt.Length; i++)
                {
                    half = half < 0f && felt[i] >= 0.5f ? i * HandlingModel.Step : half;
                    most = most < 0f && felt[i] >= 0.95f ? i * HandlingModel.Step : most;
                    jump = Math.Max(jump, felt[i] - (i > 0 ? felt[i - 1] : 0f));
                }
                string what = "handling: the brake at " + level + " (the game's own ramp " + F(actuation) + " s)";
                Check(half >= rise * 0.35f && half <= rise * 0.95f + actuation, what + " reaches half in " + F(half) + " s (slow at first)");
                Check(most > 0f && most <= rise + actuation + 0.05f, what + " reaches full in " + F(most) + " s (design " + F(rise) + ")");
                Check(jump <= 0.2f, what + " never jumps more than a fifth in a physics step (" + F(jump) + ")");
                HandlingModel.BrakeRamp(rise, actuation, 60f, 0.1f, felt);
                float tap = 0f;
                float gone = -1f;
                for (int i = 0; i < felt.Length; i++)
                {
                    tap = Math.Max(tap, felt[i]);
                    gone = gone < 0f && i * HandlingModel.Step > 0.1f && felt[i] < 0.05f ? i * HandlingModel.Step - 0.1f : gone;
                }
                Check(tap >= HandlingRules.PedalDeadZone && tap <= 0.45f, what + ": a tap of a tenth of a second brakes gently (" + F(tap) + "), and counts");
                Check(gone >= 0f && gone <= HandlingRules.BrakeFall + actuation + 0.05f, what + ": let go, it is off in " + F(gone) + " s");
            }
        }
        Check(HandlingRules.BrakePedal(0f, 0.03f, 0.016f, 0.5f) <= 0.03f && HandlingRules.BrakePedal(0.5f, 0.2f, 0.016f, 0.5f) < 0.5f && HandlingRules.BrakePedal(0.5f, 0.2f, 0.016f, 0.5f) >= 0.2f,
            "handling: the eased pedal never goes past what is asked, either way");

        // ---- the Garage's springs on the game's own car (the handling off): stiffer, and as much lower as the part says.
        AxleData game = GameCars()[1].car.Front;
        float gameShare = 1400f * HandlingModel.G * 1.314f / (2f * 2.876f) / game.SpringForce;
        HandlingModel.FitSpring(game.SpringForce, game.SpringLength, gameShare, 1.25f, -0.02f, GarageCatalogue.MaxLower, GarageCatalogue.MaxRaise, out float sportForce, out float sportLength, out float sportDrop);
        float gameRest = game.SpringLength * (1f - gameShare);
        float sportRest = sportLength - gameShare * game.SpringLength / 1.25f;
        Check(Near(gameRest - sportRest, 0.02f, 0.001f) && Near(sportDrop, -0.02f, 1e-4f) && Near(sportForce / sportLength, 1.25f * game.SpringForce / game.SpringLength, 1f),
            "handling: the Garage's sports springs on the game's own spring stand the car " + F((gameRest - sportRest) * 100f) + " cm lower at rest and " + F(sportForce / sportLength / (game.SpringForce / game.SpringLength)) + " times as stiff");
        HandlingModel.FitSpring(10000f, 0.25f, 0.5f, 1.5f, -0.04f, GarageCatalogue.MaxLower, GarageCatalogue.MaxRaise, out _, out float tight, out _);
        Check(tight - 0.5f * 0.25f / 1.5f >= HandlingModel.MinTravel - 1e-4f, "handling: a car is never lowered below the travel a kerb needs (" + F(tight - 0.5f * 0.25f / 1.5f) + " m left)");

        HandlingSources(scripts);
        Check(worstZetaLow >= BandLowZeta && worstZetaHigh <= BandHighZeta, "handling: every car's damping ratio lies between " + F(worstZetaLow) + " and " + F(worstZetaHigh));
    }

    private static IEnumerable<PartDef> PartsOf(PartSlot slot)
    {
        yield return null;
        foreach (PartDef part in GarageCatalogue.Parts)
        {
            if (part.Slot == slot)
            {
                yield return part;
            }
        }
    }

    private static float Mean(Dictionary<CarKind, float> sums, Dictionary<CarKind, int> counts, CarKind kind)
    {
        return sums.TryGetValue(kind, out float sum) && counts.TryGetValue(kind, out int n) && n > 0 ? sum / n : 0f;
    }

    private static string F(float value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    // ---------------------------------------------------------------- over the sources

    private static void HandlingSources(string scripts)
    {
        string handling = Source(scripts, @"Driving\CarHandling.cs");
        string tuning = Source(scripts, @"Driving\CarTuning.cs");
        string garage = Source(scripts, @"Garage\GarageCar.cs");
        Check(handling.Length > 0 && tuning.Length > 0 && garage.Length > 0, "handling source: CarHandling, CarTuning and GarageCar are there");

        // Every value of the game's the handling writes when it puts itself on, it writes back when it comes off.
        string apply = Body(handling, "private void PutOn(");
        string back = Body(handling, "private void TakeOff(");
        var written = new HashSet<string>(StringComparer.Ordinal);
        var restored = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match m in Regex.Matches(apply, @"\.(\w+)\s*=(?!=)"))
        {
            written.Add(m.Groups[1].Value);
        }
        foreach (Match m in Regex.Matches(back, @"\.(\w+)\s*=(?!=)"))
        {
            restored.Add(m.Groups[1].Value);
        }
        var missing = new List<string>();
        foreach (string member in written)
        {
            if (!restored.Contains(member))
            {
                missing.Add(member);
            }
        }
        Check(written.Count >= 20 && missing.Count == 0, "handling source: all " + written.Count + " of the game's values the handling writes are written back when it comes off"
                                                          + (missing.Count > 0 ? " (not: " + string.Join(", ", missing) + ")" : ""));
        Check(apply.Contains("AddAndOnboardNewComponent(") && back.Contains("Components.Remove(") && back.Contains("Destroy(") && back.Contains("VC_Disable("),
            "handling source: the game's ABS module is put on the car and taken off it again, destroyed");
        Check(Count(apply, ".FrictionPreset = ") >= 1 && Count(back, ".FrictionPreset = ") >= 1 && apply.Contains("groundDetectionPreset = ") && back.Contains("groundDetectionPreset = "),
            "handling source: the tyres' presets and the ground's preset are the handling's while it is on, the game's again after");

        // The Garage's parts always lie on top: taken off before the handling changes anything, put back after.
        string on = Body(handling, "public bool Apply(");
        string off = Body(handling, "public void Restore(");
        Check(Before(on, "LiftParts", "PutOn(") && Before(on, "PutOn(", "LayParts") && Before(off, "LiftParts", "TakeOff(") && Before(off, "TakeOff(", "LayParts"),
            "handling source: the Garage's parts are lifted before the handling goes on or comes off, and laid back on top after");

        // Every way out comes through CarTuning.Restore, which takes the handling off after its own numbers.
        string restore = Body(tuning, "private void Restore(");
        string update = Body(tuning, "private void OnUpdate(");
        Check(restore.Contains("_handling.Restore(") && Body(tuning, "private void OnDestroy(").Contains("Restore()") && update.Contains("Restore();")
              && Count(update, "Restore();") >= 2, "handling source: leaving the car, a change of car or option, a fault and the city going all take the handling off (CarTuning.Restore)");
        Check(Before(restore, "PutBackTuning(", "_handling.Restore(") && Body(tuning, "private void PutBackTuning(").Contains("box.shiftDuration") && Before(update, "_handling.Apply(", "Apply(car, vehicle)"),
            "handling source: the handling goes on before the car's power is worked out from its weight, and comes off after it");

        // Only the car the player drives: it comes from the game's selected vehicle alone, and no other part of the mod names the handling.
        Check(update.Contains("gm.selectedVehicle as CarController") && on.Contains("GoingPublic.MineVehicle("), "handling source: the handling is only for the car the player drives, and their own (Going Public)");
        var others = new List<string>();
        foreach (string file in Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories))
        {
            string name = Path.GetFileName(file);
            if (name == "CarHandling.cs" || name == "CarTuning.cs" || name == "HandlingRules.cs" || name == "HandlingModel.cs" || name == "GarageCar.cs")
            {
                continue;
            }
            string code = Code(File.ReadAllText(file));
            if (code.Contains("CarHandling") || code.Contains("HandlingModel.Design(") || code.Contains("HandlingSetup"))
            {
                others.Add(file.Substring(scripts.Length + 1));
            }
        }
        Check(others.Count == 0, "handling source: traffic, the bus, the tow truck and every other part of the mod leave the handling alone" + (others.Count > 0 ? " (named in " + string.Join(", ", others) + ")" : ""));
        Check(Count(tuning, "new CarHandling(") == 1, "handling source: there is one handling, CarTuning's");

        // The Garage's springs go on through the same rule the checks above use, and its dampers keep the damping ratio.
        string handlingPart = Body(garage, "private void Handling(");
        Check(handlingPart.Contains("HandlingModel.FitSpring(") && handlingPart.Contains("HandlingModel.RestCompression(") && handlingPart.Contains("float damping = Mathf.Sqrt(")
              && handlingPart.Contains("DamperBumpRate *= damping") && handlingPart.Contains("DamperReboundRate *= damping") && !handlingPart.Contains("*= m.Stiffness;"),
            "handling source: the Garage's springs stand the car higher or lower by the part's ride, and its dampers follow the root of the springs' factor");
        Check(garage.Contains("public bool LiftHandling(") && garage.Contains("public void LayHandling("), "handling source: the Garage can lift its handling parts off a car and lay them back");
    }

    private static bool Before(string code, string first, string second)
    {
        int a = code.IndexOf(first, StringComparison.Ordinal);
        int b = code.IndexOf(second, a < 0 ? 0 : a, StringComparison.Ordinal);
        return a >= 0 && b > a;
    }
}
