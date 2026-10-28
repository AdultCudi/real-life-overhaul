using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using RealLifeOverhaul;

// The driven car's handling (Driving/HandlingRules.cs, HandlingModel.cs, HandlingTurn.cs, CurveKeys.cs) on the game's
// own eighteen cars, their figures read from the game's files (each prefab's springs, spring curves, dampers, bars,
// tyres, slip stiffness, force points and steering curves, and each vehicle type's top speed), on Empire Motors' two cars
// and on cars the catalogue places by its guess, whose figures the model cannot know (assumed, as said below): for every
// kind and level, the side grip against the game's own curve, the anti-roll bars, rolling over against what the tyres
// hold, the damping, every turn the game's car makes with the key held up to its own top speed, the roll, the dive, the
// steering and the brakes; with every build of the Garage's springs, weight and tyres on top; the custom shop's stopping
// figure against the car braking wheel by wheel. Then, over the sources, every value the handling puts on the car is put
// back on every way out, what it wrote is read back before the Garage's parts go on top, and only the driven car is touched.
internal static partial class Program
{
    private const float RadiusMargin = 0.03f;   // a turn with the key held comes out at most this much wider than the game's own: the
                                                // little roll added moves a little weight out, which costs a little grip
    private const float LeastLoad = 0.1f;       // of its load at rest an inner wheel keeps in those turns: it never lifts
    private const float WellDamped = 0.35f;     // the least damping ratio at any level: one swing, perhaps a faint second, then still
    private const float StillAfter = 0.002f;    // m left of a 3 cm push a second and a half on
    private const float KerbSpare = 0.005f;     // m: a kerb leaves at least the game's own travel, less this
    private const float ShopSpare = 1f;         // m the custom shop's stopping figure may differ from the car's own stop
    private const float DesignMs = 60f;         // ms one car's design may take here (the game's Mono is slower; it runs once, getting in)
    private const float BrakeShare = 0.88f;     // of the game's own full pedal the handling's gives at the least: the game's fronts lock and still pull their
                                                // most, the handling's stay turning, inside their tyres (HandlingModel.MostUse), and a short soft rear
                                                // spring (the Missam Villian's) swings its load and takes a little of its share
    private const float MostLag = 2f;           // times its settled share of the tyres an axle asks at its worst moment as the body dives:
                                                // the game's brakes are strong, so the rear's load swings; the shares are set for it

    private static readonly string[] HandlingKinds = { "honzamimic Economy", "vordtiaravic Saloon", "bima320 Saloon", "mersaidis500 Luxury", "missamvillian Luxury",
        "petrollsfanton Luxury", "limo Luxury", "vordpony Sports", "ferdinand112 Sports", "mersaidimgagt Sports", "anselmoaf90 Sports", "umcnunavut Utility",
        "vordv150 Utility", "umcdesert Van", "umcdesertdeliveryjob Van", "mersaididash Van", "deliverytruck Truck", "freighttruckt1 Truck",
        "empiremustanggt:vehicletype_mustanggt Sports", "empirebmwg82:vehicletype_empirebmwg82 Sports", "other:tall van Van", "other:low coupe Sports",
        "other:heavy truck Truck", "other:city car Economy" };

    // The game's own tyres on every one of its cars (ArcadeTireFrictionPreset: B 7.09, C 0.87, D 2, E 0.5), as its curve's keys
    // were saved: slip, value, slope. It never peaks: up to 1.84 sideways on.
    private static readonly float[] Arcade =
    {
        0f, 0f, 12.1844978f, 0.02f, 0.243689954f, 11.7634296f, 0.04f, 0.470537186f, 10.6454687f, 0.06f, 0.669508696f, 9.16311169f,
        0.08f, 0.837061644f, 7.63935661f, 0.1f, 0.975082934f, 6.27201653f, 0.12f, 1.08794224f, 5.13384008f, 0.14f, 1.18043649f, 4.222188f,
        0.16f, 1.25682974f, 3.50357056f, 0.18f, 1.32057929f, 2.93859601f, 0.2f, 1.37437356f, 2.49220467f, 0.22f, 1.42026746f, 1.92082977f,
        0.32f, 1.57496381f, 1.2143023f, 0.42f, 1.6631279f, 0.724394917f, 0.52f, 1.71984279f, 0.4804461f, 0.62f, 1.75921714f, 0.340925395f,
        0.72f, 1.78802788f, 0.253628433f, 0.82f, 1.80994284f, 0.195462048f, 0.92f, 1.8271203f, 0.154845089f, 1.02f, 1.84091187f, 0.137915581f
    };

    // The game's two steering curves (share of the lock by speed over 50 m/s): most cars', and the vans', trucks' and older cars'.
    private static readonly float[] SteerCars = { 0f, 1f, 0f, 0f, 0.3f, 0.5f, -0.6f, -0.6f, 1f, 0.2f, -0.1f, -0.1f };
    private static readonly float[] SteerVans = { 0f, 1f, 0f, 0f, 0.3f, 0.4f, -0.6f, -0.6f, 1f, 0.2f, -0.1f, 0.1f };
    private static readonly float[] DashSpring = { 0f, 0f, 0.259f, 0.259f, 1f, 1f, 2.031f, 2.031f };   // the Mersaidi Dash's spring curve, rising

    // The game's own cars as its files have them (prefab figures, each car at the game's 1400 kg but the Pony's 1200; every
    // car's tyres grip 1.3 and 1.5 to the side, 0.9 and 1.5 along, slip stiffness 1.27 and 1.7, rated for 7000 N, 30 N m of
    // rolling resistance, 4200 N/m bars, brake shares 1 and 0.184, dampers 5000 N s/m at 1.4 and 1.6 below 0.06 and 0.05 m/s
    // and 0.6 above), and the cars the model cannot read beforehand, with what it assumes for them.
    private static List<(string type, CarChassis car)> GameCars()
    {
        return new List<(string, CarChassis)>
        {
            Car("anselmoaf90", 1400f, 0.30f, 0.00f, 10000f, 35f, 100f, SteerCars, Axle(1.3214f, 1.6177f, 0.4946f, 0.35f, 10000f, 0.25f, 0.8f), Axle(-1.3069f, 1.6346f, 0.4946f, 0.35f, 10000f, 0.25f, 0.8f)),
            Car("bima320", 1400f, 0.30f, 0.00f, 5000f, 35f, 70f, SteerCars, Axle(1.5620f, 1.6141f, 0.4958f, 0.34f, 10000f, 0.30f, 0.75f), Axle(-1.3140f, 1.6141f, 0.4958f, 0.34f, 10000f, 0.30f, 0.8f)),
            Car("deliverytruck", 1400f, 0.40f, 0.68f, 2500f, 42f, 55f, SteerVans, Axle(2.7580f, 2.0264f, 0.6100f, 0.43f, 5000f, 0.60f, 0.2f), Axle(-1.3599f, 1.7478f, 0.6100f, 0.43f, 5000f, 0.60f, 0.2f)),
            Car("ferdinand112", 1400f, 0.30f, 0.00f, 7000f, 28f, 90f, SteerCars, Axle(1.2740f, 1.5860f, 0.5014f, 0.3332f, 19300f, 0.20f, 0.8f), Axle(-1.0830f, 1.5860f, 0.5014f, 0.3332f, 19300f, 0.20f, 0.8f)),
            Car("freighttruckt1", 1400f, 0.40f, 0.68f, 2500f, 65f, 50f, SteerVans, Axle(4.3710f, 2.0000f, 0.5946f, 0.5420f, 5000f, 0.60f, 0.2f), Axle(-2.5050f, 2.2400f, 0.5946f, 0.5432f, 5000f, 0.60f, 0.2f)),
            Car("honzamimic", 1400f, 0.30f, 0.00f, 2500f, 35f, 60f, SteerVans, Axle(1.4073f, 1.5038f, 0.4698f, 0.3054f, 5000f, 0.60f, 0.2f), Axle(-1.2937f, 1.5038f, 0.4698f, 0.3054f, 5000f, 0.60f, 0.2f)),
            Car("limo", 1400f, 0.30f, 0.0081f, 5000f, 40f, 80f, SteerCars, Axle(1.6816f, 1.5343f, 0.5291f, 0.3611f, 5000f, 0.50f, 0.48f), Axle(-2.0664f, 1.5343f, 0.5291f, 0.3611f, 5000f, 0.50f, 0.48f)),
            Car("mersaididash", 1400f, 0.60f, 0.80f, 2500f, 35f, 65f, SteerVans, Axle(2.4000f, 1.7000f, 0.3600f, 0.4027f, 5000f, 0.40f, 0.5f, DashSpring), Axle(-1.6200f, 1.7000f, 0.3600f, 0.4027f, 5000f, 0.40f, 0.5f, DashSpring)),
            Car("mersaidimgagt", 1400f, 0.30f, 0.00f, 10000f, 35f, 100f, SteerCars, Axle(1.3757f, 1.6514f, 0.4920f, 0.3332f, 10000f, 0.25f, 0.8f), Axle(-1.2710f, 1.6514f, 0.4920f, 0.3332f, 10000f, 0.25f, 0.8f)),
            Car("mersaidis500", 1400f, 0.30f, 0.00f, 10000f, 35f, 80f, SteerCars, Axle(1.7095f, 1.6206f, 0.5130f, 0.3689f, 19300f, 0.20f, 0.8f), Axle(-1.4190f, 1.6206f, 0.5130f, 0.3689f, 19300f, 0.20f, 0.8f)),
            Car("missamvillian", 1400f, 0.30f, 0.00f, 10000f, 35f, 80f, SteerVans, Axle(1.5475f, 1.5778f, 0.4193f, 0.3680f, 5000f, 0.20f, 0.2f), Axle(-1.2573f, 1.5778f, 0.4193f, 0.3680f, 5000f, 0.20f, 0.2f)),
            Car("petrollsfanton", 1400f, 0.30f, 0.32f, 10000f, 40f, 80f, SteerCars, Axle(2.0390f, 1.6205f, 0.5291f, 0.40f, 5000f, 0.50f, 0.48f), Axle(-1.4860f, 1.6205f, 0.5291f, 0.40f, 5000f, 0.50f, 0.48f)),
            Car("umcdesert", 1400f, 0.60f, 0.09f, 2500f, 35f, 50f, SteerVans, Axle(1.8230f, 1.6879f, 0.5342f, 0.4027f, 5000f, 0.60f, 0.5f), Axle(-1.6480f, 1.7000f, 0.5342f, 0.4027f, 5000f, 0.60f, 0.5f)),
            Car("umcdesertdeliveryjob", 1400f, 0.60f, 0.09f, 2500f, 35f, 50f, SteerVans, Axle(1.8230f, 1.6879f, 0.5342f, 0.4027f, 5000f, 0.60f, 0.5f), Axle(-1.6480f, 1.7000f, 0.5342f, 0.4027f, 5000f, 0.60f, 0.5f)),
            Car("umcnunavut", 1400f, 0.15f, 0.10f, 6000f, 35f, 80f, SteerCars, Axle(1.6397f, 1.7330f, 0.5546f, 0.4027f, 7000f, 0.40f, 0.8f, null, -0.873f), Axle(-1.3896f, 1.7330f, 0.5546f, 0.4027f, 7000f, 0.40f, 0.8f, null, -0.873f)),
            Car("vordpony", 1200f, 0.40f, 0.00f, 5500f, 40f, 80f, SteerCars, Axle(1.4170f, 1.5536f, 0.4955f, 0.3332f, 19300f, 0.20f, 0.8f), Axle(-1.3638f, 1.6036f, 0.4955f, 0.3332f, 19300f, 0.20f, 0.8f)),
            Car("vordtiaravic", 1400f, 0.30f, 0.00f, 3500f, 35f, 70f, SteerVans, Axle(1.3970f, 1.5038f, 0.4805f, 0.3054f, 5000f, 0.60f, 0.2f), Axle(-1.2680f, 1.5038f, 0.4805f, 0.3054f, 5000f, 0.60f, 0.2f)),
            Car("vordv150", 1400f, 0.30f, 0.00f, 4000f, 45f, 80f, SteerVans, Axle(1.5109f, 1.5496f, 0.5630f, 0.4027f, 5000f, 0.60f, 0.2f), Axle(-1.6868f, 1.5496f, 0.5630f, 0.4027f, 5000f, 0.60f, 0.2f)),
            // Empire Motors' two cars: their prefabs ship in their own mods, not read here. Assumed built on the game's own
            // sports car (the Mersaidi MGA GT's wheels, springs, tyres and steering) at the game's top tier, 100 km/h.
            Car("empiremustanggt:vehicletype_mustanggt", 1400f, 0.30f, 0.00f, 10000f, 35f, 100f, SteerCars, Axle(1.45f, 1.62f, 0.49f, 0.35f, 10000f, 0.25f, 0.8f), Axle(-1.30f, 1.62f, 0.49f, 0.35f, 10000f, 0.25f, 0.8f)),
            Car("empirebmwg82:vehicletype_empirebmwg82", 1400f, 0.30f, 0.00f, 10000f, 35f, 100f, SteerCars, Axle(1.40f, 1.60f, 0.49f, 0.34f, 10000f, 0.25f, 0.8f), Axle(-1.45f, 1.62f, 0.49f, 0.34f, 10000f, 0.25f, 0.8f)),
            // Cars another mod might bring that the catalogue places by its guess (CarCatalogue.Guess), at the edges of what
            // the game's own are: a tall narrow van on soft springs, a low coupe on stiff ones, a heavy truck, a light city car.
            Car("other:tall van", 1900f, 0.85f, 0.30f, 3000f, 38f, 60f, SteerVans, Axle(2.10f, 1.55f, 0.40f, 0.38f, 9000f, 0.45f, 0.4f), Axle(-1.60f, 1.58f, 0.40f, 0.38f, 9000f, 0.45f, 0.4f)),
            Car("other:low coupe", 1300f, 0.22f, 0.05f, 8000f, 32f, 120f, SteerCars, Axle(1.30f, 1.60f, 0.45f, 0.33f, 15000f, 0.15f, 0.9f), Axle(-1.20f, 1.62f, 0.45f, 0.33f, 15000f, 0.15f, 0.9f)),
            Car("other:heavy truck", 4000f, 0.70f, 0.90f, 4000f, 45f, 50f, SteerVans, Axle(3.20f, 2.05f, 0.65f, 0.50f, 24000f, 0.40f, 0.3f), Axle(-1.80f, 1.95f, 0.65f, 0.50f, 24000f, 0.40f, 0.3f)),
            Car("other:city car", 900f, 0.28f, 0.10f, 2000f, 38f, 55f, SteerVans, Axle(1.20f, 1.42f, 0.42f, 0.29f, 3600f, 0.45f, 0.3f), Axle(-1.15f, 1.42f, 0.42f, 0.29f, 3600f, 0.45f, 0.3f)),
        };
    }

    private static (string, CarChassis) Car(string type, float mass, float comY, float comZ, float brakeTorque, float steerLock, float topKmh, float[] steer, AxleData front, AxleData rear)
    {
        var car = new CarChassis { Mass = mass, ComY = comY, ComZ = comZ, BrakeTorque = brakeTorque, SteerLock = steerLock, TopKmh = topKmh, TyreD = 2f, TyrePeakSlip = 1f };
        for (int i = 0; i < Arcade.Length; i += 3)
        {
            car.Tyre.Add(Arcade[i], Arcade[i + 1], Arcade[i + 2], Arcade[i + 2]);
        }
        for (int i = 0; i < steer.Length; i += 4)
        {
            car.Steer.Add(steer[i], steer[i + 1], steer[i + 2], steer[i + 3]);
        }
        Copy(front, car.Front);
        Copy(rear, car.Rear);
        car.Rear.SideGrip = 1.5f;
        car.Rear.AlongGrip = 1.5f;
        car.Rear.SideStiffness = 1.7f;
        car.Rear.BrakeShare = 0.184f;
        return (type, car);
    }

    private static AxleData Axle(float z, float track, float top, float radius, float springForce, float springLength, float application, float[] spring = null, float antiSquat = 0f)
    {
        var axle = new AxleData
        {
            Z = z, Track = track, Top = top, Radius = radius, SpringForce = springForce, SpringLength = springLength, SideGrip = 1.3f, AlongGrip = 0.9f,
            SideStiffness = 1.27f, LoadRating = 7000f, Rolling = 30f, AntiRoll = 4200f, BrakeShare = 1f, Bump = 5000f, Rebound = 5000f, SlowBump = 1.4f,
            FastBump = 0.6f, BumpKnee = 0.06f, SlowRebound = 1.6f, FastRebound = 0.6f, ReboundKnee = 0.05f, Application = application, AntiSquat = antiSquat
        };
        if (spring == null)
        {
            axle.Spring.Straight();
        }
        else
        {
            for (int i = 0; i < spring.Length; i += 4)
            {
                axle.Spring.Add(spring[i], spring[i + 1], spring[i + 2], spring[i + 3]);
            }
        }
        return axle;
    }

    private static void Copy(AxleData from, AxleData to)
    {
        to.Z = from.Z; to.Track = from.Track; to.Top = from.Top; to.Radius = from.Radius; to.SpringForce = from.SpringForce; to.SpringLength = from.SpringLength;
        to.Spring.CopyFrom(from.Spring); to.SideGrip = from.SideGrip; to.AlongGrip = from.AlongGrip; to.SideStiffness = from.SideStiffness;
        to.LoadRating = from.LoadRating; to.Rolling = from.Rolling; to.AntiRoll = from.AntiRoll; to.BrakeShare = from.BrakeShare; to.Bump = from.Bump;
        to.Rebound = from.Rebound; to.SlowBump = from.SlowBump; to.FastBump = from.FastBump; to.BumpKnee = from.BumpKnee; to.SlowRebound = from.SlowRebound;
        to.FastRebound = from.FastRebound; to.ReboundKnee = from.ReboundKnee; to.Application = from.Application; to.AntiSquat = from.AntiSquat;
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
    // lower (HandlingModel.FitSpring on the spring's own curve), dampers by the root of the springs' factor, the force
    // points moved with the body, bars by the factor, the weight by the weight part. What the car then does.
    private struct BuildResult
    {
        public float ZetaFront, ZetaRear, KerbFront, KerbRear, Tip, Hold, RateFront;
    }

    private static BuildResult OnBuild(CarChassis car, HandlingSetup s, float stiffness, float ride, float massFactor, float grip)
    {
        var result = new BuildResult();
        var built = new HandlingSetup();
        BuiltAxle(car.Front, s.Front, stiffness, ride, massFactor, built.Front, out float dropFront);
        BuiltAxle(car.Rear, s.Rear, stiffness, ride, massFactor, built.Rear, out float dropRear);
        built.Mass = s.Mass * massFactor;
        built.ComHeight = s.ComHeight + 0.5f * (dropFront + dropRear);   // the body stands where the springs now hold it
        built.Steer.CopyFrom(s.Steer);
        // Measured from the same push as the handling's own corner, so a part that leaves more travel is not read further into the dampers' softer speeds.
        float pushFront = HandlingModel.Push(car.Front, s.Front.SpringForce, s.Front.SpringLength, s.Front.Load / HandlingModel.G);
        float pushRear = HandlingModel.Push(car.Rear, s.Rear.SpringForce, s.Rear.SpringLength, s.Rear.Load / HandlingModel.G);
        result.ZetaFront = HandlingModel.Bounce(car.Front, built.Front.SpringForce, built.Front.SpringLength, built.Front.Bump, built.Front.Rebound, built.Front.Load / HandlingModel.G, pushFront, out _, out _);
        result.ZetaRear = HandlingModel.Bounce(car.Rear, built.Rear.SpringForce, built.Rear.SpringLength, built.Rear.Bump, built.Rear.Rebound, built.Rear.Load / HandlingModel.G, pushRear, out _, out _);
        result.KerbFront = HandlingModel.KerbLeft(car.Front, built.Front.SpringForce, built.Front.SpringLength, built.Front.Bump, built.Front.Rebound, built.Front.Load / HandlingModel.G, HandlingModel.Kerb);
        result.KerbRear = HandlingModel.KerbLeft(car.Rear, built.Rear.SpringForce, built.Rear.SpringLength, built.Rear.Bump, built.Rear.Rebound, built.Rear.Load / HandlingModel.G, HandlingModel.Kerb);
        result.RateFront = built.Front.Rate;
        var turn = new TurnCar();
        HandlingModel.OwnTurn(car, built, s.Tyre, turn);
        turn.GripF *= grip;
        turn.GripR *= grip;
        result.Hold = HandlingTurn.Hold(turn);
        result.Tip = HandlingTurn.Rollover(turn, s.Overshoot);
        return result;
    }

    private static void BuiltAxle(AxleData game, AxleSetup s, float stiffness, float ride, float massFactor, AxleSetup into, out float drop)
    {
        float load = s.Load * massFactor;
        HandlingModel.FitSpring(game.Spring.Evaluate, s.SpringForce, s.SpringLength, s.Load, massFactor, stiffness, ride, GarageCatalogue.MaxLower, GarageCatalogue.MaxRaise,
            out into.SpringForce, out into.SpringLength, out float change);
        float damping = (float)Math.Sqrt(stiffness * massFactor);
        into.Bump = s.Bump * damping;
        into.Rebound = s.Rebound * damping;
        into.Application = (s.Application * s.SpringLength + change) / into.SpringLength;   // the force point moves with the body
        into.AntiSquat = s.AntiSquat;
        into.AntiRoll = s.AntiRoll * stiffness;
        into.LoadRating = s.LoadRating;
        into.Load = load;
        float settled = HandlingModel.RestCompression(game.Spring, into.SpringForce, load);
        into.Rate = into.SpringForce * game.Spring.Slope(settled) / into.SpringLength;
        into.Sag = settled * into.SpringLength;
        into.RestLength = into.SpringLength - into.Sag;
        drop = into.RestLength - s.RestLength;
    }

    private static void Handling(bool print)
    {
        string scripts = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        List<(string type, CarChassis car)> cars = GameCars();
        var setup = new HandlingSetup();
        var twin = new HandlingSetup();
        var game = new TurnCar();
        var own = new TurnCar();
        HandlingTuning neutral = HandlingTuning.Neutral;
        int builds = 0;
        int brakeBuilds = 0;
        int turns = 0;
        float slowestMs = 0f;
        string slowestAt = null;
        var watch = new Stopwatch();
        if (print)
        {
            Console.WriteLine("handling: car, kind, level | kg | com h | front rate N/m, Hz, damping ratio | bars | roll deg/g (force points m) | dive | rolls over g | tyres hold g, give past deg (city turn g at km/h) | steer 5/50/100 | brakes g, shares, from 100 km/h m");
        }
        foreach ((string type, CarChassis car) in cars)
        {
            CarKind kind = KindOf(type);
            Check(kind != CarKind.Untouched, "handling: " + type + " is a car of a kind");
            HandlingModel.GameTurn(car, game);
            float gameHeight = HandlingModel.GameHeight(car);
            var levelMass = new float[4];
            var levelRoll = new float[4];
            var levelHeld = new bool[4];
            foreach (HandlingLevel level in new[] { HandlingLevel.Light, HandlingLevel.Full, HandlingLevel.Heavy })
            {
                HandlingTarget target = HandlingRules.Target(kind, level, neutral);
                watch.Restart();
                string why = HandlingModel.Design(car, target, setup);
                watch.Stop();
                float ms = (float)watch.Elapsed.TotalMilliseconds;
                if (ms > slowestMs)
                {
                    slowestMs = ms;
                    slowestAt = type + " at " + level;
                }
                Check(why == null, "handling: " + type + " at " + level + " is designed (" + (why ?? "yes") + ")");
                if (why != null)
                {
                    continue;
                }
                string name = type + " (" + kind + ", " + level + ")";
                float ratio = setup.Mass / car.Mass;
                levelMass[(int)level] = setup.Mass;
                levelRoll[(int)level] = setup.RollPerG;
                levelHeld[(int)level] = setup.TipHeld;

                // ---- the weight, the centre of mass, the springs and bars: the game's own, carrying a little more weight.
                float kindMass = HandlingRules.KindMass(kind);
                Check(setup.Mass >= car.Mass - 1e-3f && setup.Mass <= Math.Max(car.Mass, kindMass) + 1e-3f,
                    "handling: " + name + " weighs " + F(setup.Mass) + " kg, the game's " + F(car.Mass) + " and a share of the way to its kind's " + F(kindMass));
                Check(Near(setup.ComHeight, gameHeight, 1e-3f), "handling: " + name + "'s centre of mass stays the game's own, " + F(setup.ComHeight) + " m up (" + F(gameHeight) + ")");
                foreach ((string end, AxleSetup axle, AxleData data) in new[] { ("front", setup.Front, car.Front), ("rear", setup.Rear, car.Rear) })
                {
                    Check(Near(axle.Rate, axle.GameRate * ratio, axle.GameRate * ratio * 0.005f) && Near(axle.Frequency, axle.GameFrequency, axle.GameFrequency * 0.01f),
                        "handling: " + name + " " + end + " springs carry the weight at the game's own frequency: " + F(axle.GameRate) + " -> " + F(axle.Rate) + " N/m, " + F(axle.GameFrequency) + " -> " + F(axle.Frequency) + " Hz");
                    float gameRest = data.SpringLength * (1f - HandlingModel.RestCompression(data.Spring, data.SpringForce, axle.Load / ratio));
                    Check(Near(axle.RestLength, gameRest, 1e-3f) && axle.SpringLength == data.SpringLength,
                        "handling: " + name + " " + end + " stands as high as the game had it: " + F(axle.RestLength) + " m of travel at rest (the game's " + F(gameRest) + ")");
                    Check(axle.AntiRoll > 0f && Near(axle.AntiRoll, data.AntiRoll * ratio, 0.5f),
                        "handling: " + name + " " + end + " keeps the game's anti-roll bar for its weight: " + F(data.AntiRoll) + " -> " + F(axle.AntiRoll) + " N/m");
                    Check(Near(axle.LoadRating, data.LoadRating * ratio, 0.5f), "handling: " + name + " " + end + " tyres are rated for the weight as the game rated its own");
                }

                // ---- the ride: well damped, the game's own at Light, one swing at most at Full, a little more at Heavy.
                foreach ((string end, AxleSetup axle, AxleData data) in new[] { ("front", setup.Front, car.Front), ("rear", setup.Rear, car.Rear) })
                {
                    float zeta = HandlingModel.Bounce(data, axle.SpringForce, axle.SpringLength, axle.Bump, axle.Rebound, axle.Load / HandlingModel.G, out int swings, out float after);
                    float gameZeta = HandlingModel.Bounce(data, data.SpringForce, data.SpringLength, data.Bump, data.Rebound, axle.Load / ratio / HandlingModel.G, out int gameSwings, out float gameAfter);
                    float aim = level == HandlingLevel.Light ? gameZeta : Math.Min(gameZeta, target.Damping);
                    Check(zeta >= aim - 0.03f && zeta <= aim + 0.03f && zeta >= Math.Min(WellDamped, gameZeta - 0.03f),
                        "handling: " + name + " " + end + ": the body's swings die away at a damping ratio of " + F(zeta) + " (the game's " + F(gameZeta) + ", the level's " + (target.Damping > 0f ? F(target.Damping) : "the game's own") + ")");
                    int allowed = level == HandlingLevel.Light ? gameSwings : level == HandlingLevel.Full ? Math.Max(1, gameSwings) : Math.Max(2, gameSwings);
                    Check(swings <= allowed && after < Math.Max(StillAfter, gameAfter * 1.05f + 1e-4f), "handling: " + name + " " + end + ": " + swings + " swings anyone sees, then still ("
                        + F(after * 1000f) + " mm left after 1.5 s; the game's own " + F(gameAfter * 1000f) + ")");
                    float kerb = HandlingModel.KerbLeft(data, axle.SpringForce, axle.SpringLength, axle.Bump, axle.Rebound, axle.Load / HandlingModel.G, HandlingModel.Kerb);
                    float gameKerb = HandlingModel.KerbLeft(data, data.SpringForce, data.SpringLength, data.Bump, data.Rebound, axle.Load / ratio / HandlingModel.G, HandlingModel.Kerb);
                    Check(kerb >= gameKerb - KerbSpare, "handling: " + name + " " + end + ": a " + F(HandlingModel.Kerb * 100f) + " cm kerb leaves " + F(kerb * 100f) + " cm of travel (the game's " + F(gameKerb * 100f) + ")");
                    Check(axle.Rebound >= axle.Bump * 0.99f && Near(axle.Bump / data.Bump, axle.Rebound / data.Rebound, 1e-3f), "handling: " + name + " " + end + "'s dampers keep the game's own shape");
                }

                // ---- the side grip: the game's own curve to the knee, which lies past the slip the game's own hardest turn
                // with the key held uses (its own top speed) by CityMargin; flat past it, never falling.
                CurveKeys gameTyre = car.Tyre;
                float worstBelow = 0f;
                for (int i = 0; i <= 400; i++)
                {
                    float x = setup.Knee * i / 400f;
                    worstBelow = Math.Max(worstBelow, Math.Abs(setup.Tyre.Evaluate(x) - gameTyre.Evaluate(x)));
                }
                Check(worstBelow <= 1e-4f, "handling: " + name + "'s tyres are the game's own curve up to their knee (" + F(setup.KneeDegrees) + " degrees of slip; the most they differ there " + F(worstBelow) + ")");
                bool flat = true;
                for (int i = 0; i <= 100; i++)
                {
                    float x = setup.Knee + (1.2f - setup.Knee) * i / 100f;
                    flat &= Near(setup.Tyre.Evaluate(x), gameTyre.Evaluate(setup.Knee), 1e-4f);
                }
                Check(flat, "handling: " + name + "'s tyres hold " + F(gameTyre.Evaluate(setup.Knee)) + " past their knee, never less: they slide on, they never snap");
                bool topHeld = HandlingTurn.Steady(game, setup.CityKmh / 3.6f, out TurnState top);
                Check(topHeld && (top.SlipF <= HandlingModel.CitySlip + 0.01f && top.Least >= HandlingModel.CityLift - 1e-3f || setup.CityKmh <= HandlingModel.CityLeast)
                      && (setup.CityKmh >= Math.Min(car.TopKmh, HandlingModel.CityMost) - 0.01f || !HandlingTurn.Steady(game, (setup.CityKmh + 5f) / 3.6f, out TurnState past)
                          || past.SlipF > HandlingModel.CitySlip || past.Least < HandlingModel.CityLift),
                    "handling: " + name + " takes the game's own hardest turn at " + F(setup.CityKmh) + " km/h (its top " + F(car.TopKmh) + "), the fastest where the game's car still holds the road: front slip "
                    + F(top.SlipF) + " degrees, lightest wheel at " + F(top.Least * 100f) + "% of its load");
                Check(setup.Knee >= setup.CityCurve * HandlingModel.CityMargin - 1e-4f && setup.CityCurve > 0f,
                    "handling: " + name + "'s tyres give only past " + F(setup.Knee) + ", " + F(setup.Knee / setup.CityCurve) + " times the furthest the game's own car's tyres go in its hardest turn with the key held (" + F(setup.CityCurve) + ", at " + F(setup.CityKmh) + " km/h)");

                // ---- rolling over: only past what the tyres hold on the Garage's grippiest tyres, a sudden turn's swing counted.
                float grippiest = Most(PartSlot.Tyres);
                Check(setup.TipG >= grippiest * setup.Hold * 0.999f, "handling: " + name + " would roll over only at " + F(setup.TipG) + " g, " + F(setup.TipG / setup.Hold)
                    + " times the " + F(setup.Hold) + " g its tyres hold (" + F(grippiest) + " at least, the Garage's grippiest tyres; the game's own car: " + F(setup.GameTipG) + " against "
                    + F(setup.GameHold) + ")");

                // ---- every turn the game's own car makes with the key held, up to its own top speed: as tight, the tyres holding, no wheel lifting.
                HandlingModel.OwnTurn(car, setup, setup.Tyre, own);
                float worstWider = -1f;
                string widerAt = null;
                float leastLoad = 9f;
                string liftAt = null;
                string slidAt = null;
                for (float kmh = 20f; kmh <= setup.CityKmh + 0.01f; kmh = kmh + 10f > setup.CityKmh && kmh < setup.CityKmh ? setup.CityKmh : kmh + 10f)
                {
                    turns++;
                    float v = kmh / 3.6f;
                    bool held = HandlingTurn.Steady(own, v, out TurnState ours);
                    bool gameHeld = HandlingTurn.Steady(game, v, out TurnState theirs);
                    if (!held || !gameHeld)
                    {
                        slidAt = slidAt ?? F(kmh) + " km/h (no turn held)";
                        continue;
                    }
                    float wider = ours.Radius / theirs.Radius - 1f;
                    if (wider > worstWider)
                    {
                        worstWider = wider;
                        widerAt = F(kmh) + " km/h, " + F(ours.Radius) + " m against " + F(theirs.Radius) + " m at " + F(theirs.G) + " g";
                    }
                    if (ours.Least < leastLoad)
                    {
                        leastLoad = ours.Least;
                        liftAt = F(kmh) + " km/h";
                    }
                    if (ours.Limited && !theirs.Limited)
                    {
                        slidAt = slidAt ?? F(kmh) + " km/h";
                    }
                }
                Check(worstWider <= RadiusMargin && slidAt == null, "handling: " + name + " takes every turn the game's own car takes with the key held up to its "
                    + F(setup.CityKmh) + " km/h at most " + F(worstWider * 100f) + "% wider (" + widerAt + ")" + (slidAt != null ? ", but slides at " + slidAt : ""));
                Check(leastLoad >= LeastLoad, "handling: " + name + "'s inner wheels keep " + F(leastLoad * 100f) + "% of their load in those turns (at " + liftAt + ")");
                CityCorners(name, own, game, setup.CityKmh);

                // ---- the roll and the dive: the game's own and a little more, as far as rolling over allows.
                float gameRoll = Math.Max(0f, setup.GameRollPerG);
                float wantedRoll = gameRoll + target.RollAdd;
                Check(setup.RollPerG <= wantedRoll + 0.02f && (setup.RollPerG >= wantedRoll - 0.02f || setup.Raised > 0f || NearLimit(car, setup)),
                    "handling: " + name + " rolls " + F(setup.RollPerG) + " degrees per g (the game's " + F(setup.GameRollPerG) + ", asked " + F(wantedRoll)
                    + (setup.Raised > 0f ? "; the force points " + F(setup.Raised * 100f) + " cm higher so it slides before it rolls over" : "") + ")");
                Check(setup.DivePerG >= Math.Max(0f, setup.GameDivePerG) - 0.05f && setup.DivePerG <= Math.Max(0f, setup.GameDivePerG) * target.DiveScale + 0.05f
                      || Math.Abs(setup.Front.AntiSquat) >= 0.999f,
                    "handling: " + name + " dives " + F(setup.DivePerG) + " degrees per g of braking (the game's " + F(setup.GameDivePerG) + ", anti-squat " + F(setup.Front.AntiSquat) + ")");

                // ---- the steering: the game's own curve.
                Check(setup.SteerOwn && Near(HandlingModel.SteerAt(setup, car.SteerLock, 50f / 3.6f), car.SteerLock * car.Steer.Evaluate(50f / 3.6f / HandlingTurn.SteerTop), 1e-4f),
                    "handling: " + name + " steers as the game's own: " + F(HandlingModel.SteerAt(setup, car.SteerLock, 1.4f)) + ", " + F(HandlingModel.SteerAt(setup, car.SteerLock, 50f / 3.6f))
                    + ", " + F(HandlingModel.SteerAt(setup, car.SteerLock, 100f / 3.6f)) + " degrees at 5, 50, 100 km/h");

                // ---- the brakes: the game's own full pedal (Heavy a little less), never locking, the dive's worst moment counted.
                float arm = HandlingModel.PitchArm(car, setup);
                float wheelbase = car.Front.Z - car.Rear.Z;
                float moved = setup.Mass * HandlingModel.G * setup.BrakeG * arm / (2f * wheelbase);
                float frontForce = setup.Front.BrakeShare * car.BrakeTorque / car.Front.Radius;
                float rearForce = setup.Rear.BrakeShare * car.BrakeTorque / car.Rear.Radius;
                float frontHold = setup.Front.AlongG * (setup.Front.Load + moved);
                float rearHold = setup.Rear.AlongG * Math.Max(1f, setup.Rear.Load - moved);
                Check(frontForce <= frontHold * HandlingModel.MostUse * 1.001f && rearForce <= rearHold * HandlingModel.MostUse * 1.001f && setup.BrakeUse <= HandlingModel.MostPeak + 1e-3f,
                    "handling: " + name + "'s full pedal asks the front " + F(frontForce / frontHold) + " and the rear " + F(rearForce / rearHold) + " of what their tyres hold once the body has settled, and "
                    + F(setup.BrakeUse) + " at their worst moment as it dives: no lock on a dry road");
                Check(setup.FrontLag >= 1f && setup.FrontLag <= MostLag && setup.RearLag >= 1f && setup.RearLag <= MostLag, "handling: " + name + "'s axles ask " + F(setup.FrontLag) + " and "
                    + F(setup.RearLag) + " times their settled share of the tyres at their worst moment as the body dives (" + F(MostLag) + " at most)");
                float rearLimit = HandlingModel.RearLimit(car, setup);
                Check(Near(setup.BrakeTarget, setup.GameBrakeG * target.BrakeFactor, 1e-4f) && setup.BrakeG <= setup.BrakeTarget * 1.001f && setup.BrakeG >= Math.Min(setup.BrakeTarget, rearLimit) * BrakeShare,
                    "handling: " + name + " stops at " + F(setup.BrakeG) + " g with the full pedal, built for " + F(setup.BrakeTarget) + " (the game's own pedal on it: " + F(setup.GameBrakeG)
                    + (rearLimit < setup.BrakeTarget ? "; held where the rear keeps " + F(HandlingModel.RearKeep * 100f) + "% of its load, " + F(rearLimit) : "") + ")");
                Check(setup.Front.BrakeShare > 0f && setup.Front.BrakeShare <= 1f && setup.Rear.BrakeShare >= 0f && setup.Rear.BrakeShare <= 1f, "handling: " + name + "'s brake shares are the game's own range");

                // ---- with every Garage build of springs, weight and tyres on top, wet and dry: still well damped, a kerb no worse,
                // and it slides before it rolls over even on the grippiest tyres.
                float zetaLow = 1f, kerbLeast = 1f, tipLeast = 99f, stiffLeast = 99f;
                string zetaAt = null, kerbAt = null, tipAt = null, stiffAt = null;
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
                                float grip = tyre == null ? 1f : wet ? tyre.WetGrip : tyre.Grip;
                                BuildResult built = OnBuild(car, setup, spring != null ? spring.Stiff : 1f, spring != null ? spring.Ride : 0f, weight != null ? weight.Mass : 1f, grip);
                                builds++;
                                string build = (spring != null ? spring.Id : "stock springs") + ", " + (weight != null ? weight.Id : "stock weight") + ", "
                                               + (tyre != null ? tyre.Id : "stock tyres") + (wet ? ", wet" : ", dry");
                                float lowZ = Math.Min(built.ZetaFront, built.ZetaRear);
                                if (lowZ < zetaLow) { zetaLow = lowZ; zetaAt = build; }
                                float kerb = Math.Min(built.KerbFront - setup.Front.RestLength, built.KerbRear - setup.Rear.RestLength);
                                if (kerb < kerbLeast) { kerbLeast = kerb; kerbAt = build; }
                                float tip = built.Tip / built.Hold;
                                if (tip < tipLeast) { tipLeast = tip; tipAt = build; }
                                float stiffer = built.RateFront / (setup.Front.Rate * (weight != null ? weight.Mass : 1f));   // for its weight: a lighter car's springs carry it at the same frequency
                                if (spring != null && stiffer < stiffLeast) { stiffLeast = stiffer; stiffAt = build; }
                            }
                        }
                    }
                }
                Check(zetaLow >= Math.Min(WellDamped, Math.Min(setup.Front.Zeta, setup.Rear.Zeta)) - 0.04f, "handling: " + name + " with every Garage build: damping ratio " + F(zetaLow) + " at the least (" + zetaAt + ")");
                Check(kerbLeast >= -HandlingModel.Kerb - 0.05f, "handling: " + name + " with every Garage build: a kerb's travel moves at most by the part's own height (" + F(kerbLeast * 100f) + " cm, " + kerbAt + ")");
                Check(stiffLeast > 1f, "handling: " + name + " with every Garage spring: at least " + F(stiffLeast) + " times as stiff as the handling's own for its weight (" + stiffAt + ")");
                Check(tipLeast >= 0.998f, "handling: " + name + " with every Garage build: slides before it rolls over, at " + F(tipLeast) + " times what its tyres hold at the least (" + tipAt + ")");

                // ---- with every Garage build of brakes, weight, tyres and springs, the brakes coming on at either of the
                // game's onsets: the shop's stopping figure is what the car does, wheel by wheel, and no wheel locks.
                float diffMost = 0f, useMost = 0f;
                string diffAt = null, useAt = null, longerAt = null;
                foreach (PartDef spring in PartsOf(PartSlot.Suspension))
                {
                    if (spring != null && spring.UtilityOnly && kind != CarKind.Utility && kind != CarKind.Van)
                    {
                        continue;
                    }
                    foreach (PartDef weight in PartsOf(PartSlot.Weight))
                    {
                        float massFactor = weight != null ? weight.Mass : 1f;
                        var frontCorner = new AxleSetup();
                        var rearCorner = new AxleSetup();
                        BuiltAxle(car.Front, setup.Front, spring != null ? spring.Stiff : 1f, spring != null ? spring.Ride : 0f, massFactor, frontCorner, out _);
                        BuiltAxle(car.Rear, setup.Rear, spring != null ? spring.Stiff : 1f, spring != null ? spring.Ride : 0f, massFactor, rearCorner, out _);
                        foreach (PartDef tyre in PartsOf(PartSlot.Tyres))
                        {
                            float grip = tyre != null ? tyre.Grip : 1f;
                            foreach (float onset in new[] { HandlingModel.GameBrakeOnset, 0.25f })
                            {
                                float stockStop = -1f;
                                foreach (PartDef brake in PartsOf(PartSlot.Brakes))
                                {
                                    float brakes = brake != null ? brake.Brake : 1f;
                                    float shop = HandlingModel.ShopStop(car, setup, brakes, massFactor, grip, onset);
                                    float stopped = GameStop(car, setup, frontCorner, rearCorner, brakes, massFactor, grip, onset, out float worst);
                                    brakeBuilds++;
                                    string build = (brake != null ? brake.Id : "stock brakes") + ", " + (weight != null ? weight.Id : "stock weight") + ", " + (tyre != null ? tyre.Id : "stock tyres")
                                                   + ", " + (spring != null ? spring.Id : "stock springs") + ", onset " + F(onset) + " s: shop " + F(shop) + " m, car " + F(stopped) + " m";
                                    if (Math.Abs(shop - stopped) > diffMost) { diffMost = Math.Abs(shop - stopped); diffAt = build; }
                                    if (worst > useMost) { useMost = worst; useAt = build; }
                                    if (stockStop > 0f && shop > stockStop + 0.01f) { longerAt = build; }
                                    stockStop = stockStop < 0f ? shop : Math.Min(stockStop, shop);
                                }
                            }
                        }
                    }
                }
                Check(diffMost <= ShopSpare, "handling: " + name + " with every Garage build of brakes, weight, tyres and springs: the custom shop's stopping figure and the car's own stop agree within "
                                           + F(diffMost) + " m (" + (diffAt ?? "every build the same") + ")");
                Check(useMost <= 1f, "handling: " + name + " with every Garage build: no wheel asks more than its tyre holds as the body dives (" + F(useMost) + ", " + useAt + ")");
                Check(longerAt == null, "handling: " + name + "'s Garage brakes never make the stop longer" + (longerAt != null ? " (" + longerAt + ")" : ""));

                // ---- the same car twice gives the same answer.
                HandlingModel.Design(car, target, twin);
                Check(twin.Front.SpringForce == setup.Front.SpringForce && twin.Rear.Bump == setup.Rear.Bump && twin.Knee == setup.Knee && twin.Front.Application == setup.Front.Application
                      && twin.Front.BrakeShare == setup.Front.BrakeShare && twin.Front.AntiSquat == setup.Front.AntiSquat, "handling: " + name + " comes out the same every time");

                if (print)
                {
                    Console.WriteLine("handling: " + type + ", " + kind + ", " + level + " | " + F(car.Mass) + " -> " + F(setup.Mass) + " | " + F(setup.ComHeight) + " | "
                                      + F(setup.Front.GameRate) + " -> " + F(setup.Front.Rate) + ", " + F(setup.Front.Frequency) + " Hz, " + F(setup.Front.GameZeta) + " -> " + F(setup.Front.Zeta) + " | "
                                      + F(car.Front.AntiRoll) + " -> " + F(setup.Front.AntiRoll) + " | " + F(setup.GameRollPerG) + " -> " + F(setup.RollPerG) + " (" + F(setup.GameRollCentre) + " -> "
                                      + F(setup.RollCentre) + ") | " + F(setup.GameDivePerG) + " -> " + F(setup.DivePerG) + " | " + F(setup.GameTipG) + " -> " + F(setup.TipG) + " | "
                                      + F(setup.GameHold) + " -> " + F(setup.Hold) + ", " + F(setup.KneeDegrees) + " (" + F(setup.CityG) + " at " + F(setup.CityKmh) + ") | "
                                      + F(HandlingModel.SteerAt(setup, car.SteerLock, 1.4f)) + "/" + F(HandlingModel.SteerAt(setup, car.SteerLock, 50f / 3.6f)) + "/"
                                      + F(HandlingModel.SteerAt(setup, car.SteerLock, 100f / 3.6f)) + " | " + F(setup.GameBrakeG) + " -> " + F(setup.BrakeG) + ", " + F(setup.Front.BrakeShare) + "/"
                                      + F(setup.Rear.BrakeShare) + ", " + F(HandlingModel.ShopStop(car, setup, 1f, 1f, 1f, 0.25f)) + " m");
                }
            }

            // ---- the levels against each other: Light the lightest, Heavy the heaviest; each rolls at least as much as the one
            // below it, except where rolling over holds the higher one's roll back.
            Check(levelMass[1] <= levelMass[2] && levelMass[2] <= levelMass[3] && (levelHeld[2] || levelRoll[1] <= levelRoll[2] + 0.02f) && (levelHeld[3] || levelRoll[2] <= levelRoll[3] + 0.02f),
                "handling: " + type + " at Light, Full and Heavy weighs " + F(levelMass[1]) + ", " + F(levelMass[2]) + ", " + F(levelMass[3]) + " kg and rolls " + F(levelRoll[1]) + ", "
                + F(levelRoll[2]) + ", " + F(levelRoll[3]) + " degrees per g" + (levelHeld[2] || levelHeld[3] ? " (rolling over holds " + (levelHeld[2] ? "Full's" : "") + (levelHeld[2] && levelHeld[3] ? " and " : "")
                + (levelHeld[3] ? "Heavy's" : "") + " back)" : ""));
        }
        Check(builds > 1000, "handling: every car with every Garage build of springs, weight and tyres, wet and dry (" + builds + ")");
        Check(brakeBuilds > 5000, "handling: every car's stop with every Garage build of brakes, weight, tyres and springs, at both onsets (" + brakeBuilds + ")");
        Check(turns > 400, "handling: every car's turns with the key held up to its own top speed (" + turns + ")");
        Check(slowestMs <= DesignMs, "handling: one car's design takes " + F(slowestMs) + " ms at the most (" + slowestAt + "; " + F(DesignMs) + " at most, once, getting in)");

        // ---- the levels: almost the game's at Light, more of everything at Heavy.
        HandlingTarget light = HandlingRules.Target(CarKind.Saloon, HandlingLevel.Light, neutral);
        HandlingTarget full = HandlingRules.Target(CarKind.Saloon, HandlingLevel.Full, neutral);
        HandlingTarget heavy = HandlingRules.Target(CarKind.Saloon, HandlingLevel.Heavy, neutral);
        Check(light.MassShare < full.MassShare && full.MassShare < heavy.MassShare && light.RollAdd < full.RollAdd && full.RollAdd < heavy.RollAdd && light.KneeDegrees > full.KneeDegrees
              && full.KneeDegrees > heavy.KneeDegrees && light.Damping == 0f && full.Damping > heavy.Damping && heavy.Damping >= WellDamped && light.BrakeRise < full.BrakeRise && full.BrakeRise < heavy.BrakeRise
              && light.DiveScale < full.DiveScale && full.DiveScale < heavy.DiveScale,
            "handling: Light is almost the game's own, Heavy has more weight, roll, give and bounce than Full, and still well damped");
        Check(HandlingRules.Level(0) == HandlingLevel.Off && HandlingRules.Level(3) == HandlingLevel.Heavy && HandlingRules.Level(7) == HandlingLevel.Full && HandlingRules.Level(-1) == HandlingLevel.Full,
            "handling: an unknown choice of the option is the whole feel");
        Check(Near(HandlingRules.LevelMass(CarKind.Truck, HandlingLevel.Full, HandlingRules.GameMass) - HandlingRules.GameMass, 400f, 0.5f)
              && HandlingRules.LevelMass(CarKind.Economy, HandlingLevel.Heavy, HandlingRules.GameMass) == HandlingRules.GameMass,
            "handling: the Guide's figure: a truck of the game's weight carries 400 kg more at Full, and no car is made lighter than the game made it");

        // ---- the sliders: each moves its own figure, within the bounds, and the design follows.
        var tuned = new HandlingTuning(150, 150, 150, 150, 150, 150);
        HandlingTarget soft = HandlingRules.Target(CarKind.Saloon, HandlingLevel.Full, tuned);
        Check(soft.Softness > full.Softness && soft.DamperScale < full.DamperScale && soft.RollScale > full.RollScale && soft.Grip > full.Grip && soft.Steering > full.Steering && soft.BrakeRise < full.BrakeRise,
            "handling: the sliders soften the springs, lighten the damping, add roll, grip, steering reach and a quicker brake");
        var wild = new HandlingTuning(0, 999, -5, 151, 49, 1000);
        Check(wild.Softness == HandlingTuning.Least && wild.Bounce == HandlingTuning.Most && wild.Roll == HandlingTuning.Least && wild.Grip == HandlingTuning.Most && wild.Steering == HandlingTuning.Least
              && wild.Brakes == HandlingTuning.Most, "handling: a slider's value is kept inside 50 to 150 per cent");
        Check(HandlingTuning.Neutral.Stamp != tuned.Stamp && new HandlingTuning(100, 100, 100, 100, 100, 101).Stamp != HandlingTuning.Neutral.Stamp, "handling: any slider moved changes the stamp the car is put on again by");
        Sliders(cars[1].car, setup, twin);

        // ---- a car that is not a car is refused, and nothing is put on it.
        var odd = new CarChassis { Mass = 1400f, ComY = 0.3f, ComZ = 0f, BrakeTorque = 3000f, SteerLock = 35f };
        Check(HandlingModel.Design(odd, full, setup) != null, "handling: a car with no axles is refused");
        CarChassis bima = GameCars()[1].car;
        bima.Rear.SpringForce = 0f;
        Check(HandlingModel.Design(bima, full, setup) != null, "handling: a car with a spring of no strength is refused");
        CarChassis bald = GameCars()[1].car;
        bald.Tyre.Clear();
        Check(HandlingModel.Design(bald, full, setup) != null, "handling: a car whose tyres were not read is refused");

        BrakeRamps(neutral);

        // ---- the Garage's springs on the game's own car (the handling off): stiffer, and as much lower as the part says.
        GarageSprings();
        Check(Near(HandlingModel.TipMargin, Most(PartSlot.Tyres), 1e-4f), "handling: rolling over is held at the Garage's grippiest tyres' grip, " + F(Most(PartSlot.Tyres)) + " times the car's own");

        HandlingSources(scripts);
    }

    // The Garage's springs and weight on the game's own springs (the handling off), straight and rising: stiffer by the part,
    // standing as much lower as it says, a lighter car at the same height and frequency, never into the travel a kerb needs.
    private static void GarageSprings()
    {
        foreach ((string what, AxleData axle, float load) in new[] { ("the BIMA 320's straight spring", GameCars()[1].car.Front, 1400f * HandlingModel.G * 1.314f / (2f * 2.876f)),
                     ("the Mersaidi Dash's rising spring", GameCars()[7].car.Rear, 1400f * HandlingModel.G * 1.6f / (2f * 4.02f)) })
        {
            float rest = axle.SpringLength * (1f - HandlingModel.RestCompression(axle.Spring, axle.SpringForce, load));
            float rate = axle.SpringForce * axle.Spring.Slope(HandlingModel.RestCompression(axle.Spring, axle.SpringForce, load)) / axle.SpringLength;
            foreach ((float stiff, float ride, float weight) in new[] { (1.25f, -0.02f, 1f), (1.5f, -0.04f, 1f), (1.1f, 0.04f, 1f), (1f, 0f, 0.9f), (1.25f, -0.02f, 0.95f) })
            {
                HandlingModel.FitSpring(axle.Spring.Evaluate, axle.SpringForce, axle.SpringLength, load, weight, stiff, ride, GarageCatalogue.MaxLower, GarageCatalogue.MaxRaise,
                    out float force, out float length, out float change);
                float settled = HandlingModel.RestCompression(axle.Spring, force, load * weight);
                float newRest = length * (1f - settled);
                float newRate = force * axle.Spring.Slope(settled) / length;
                Check(Near(newRest - rest, change, 2e-3f) && (ride >= 0f || change < 0f || rest - HandlingModel.MinTravel < 1e-3f) && Near(newRate / rate, stiff * weight, 0.03f * stiff * weight),
                    "handling: the Garage's parts (springs " + F(stiff) + ", ride " + F(ride * 100f) + " cm, weight " + F(weight) + ") on " + what + ": " + F((newRest - rest) * 100f)
                    + " cm higher at rest, " + F(newRate / rate) + " times the rate");
            }
            HandlingModel.FitSpring(axle.Spring.Evaluate, axle.SpringForce, axle.SpringLength, load, 1f, 1.5f, -0.2f, 0.2f, GarageCatalogue.MaxRaise, out _, out float tightLength, out float drop);
            Check(rest + drop >= Math.Min(rest, HandlingModel.MinTravel) - 1e-3f, "handling: " + what + " is never lowered below the travel a kerb needs (" + F((rest + drop) * 100f) + " cm left)");
        }
    }

    // The city's corners at the game's own speed for each: the fastest the game's own car takes the corner's radius with the
    // key held (up to its own top speed). The car with the handling on takes it there as tightly, within RadiusMargin.
    private static void CityCorners(string name, TurnCar own, TurnCar game, float topKmh)
    {
        foreach ((string corner, float radius) in new[] { ("a right turn at a junction", 9f), ("a left turn across a junction", 15f), ("a bend in an avenue", 30f) })
        {
            float speed = 0f;
            for (float kmh = 10f; kmh <= topKmh + 0.01f; kmh += 2f)
            {
                if (HandlingTurn.Steady(game, kmh / 3.6f, out TurnState theirs) && theirs.Radius <= radius)
                {
                    speed = kmh;
                }
            }
            if (!(speed > 0f))
            {
                continue;   // the game's own car does not take it at all
            }
            bool held = HandlingTurn.Steady(own, speed / 3.6f, out TurnState ours);
            Check(held && ours.Radius <= radius * (1f + RadiusMargin) && ours.Least >= LeastLoad && !ours.Limited,
                "handling: " + name + " takes " + corner + " (" + F(radius) + " m) at the game's own " + F(speed) + " km/h: " + F(ours.Radius) + " m, " + F(ours.G) + " g, the inner wheels at "
                + F(ours.Least * 100f) + "% of their load");
        }
    }

    // Whether the roll asked could not be had for the force points' limits (MostLower, LowestPoint).
    private static bool NearLimit(CarChassis car, HandlingSetup s)
    {
        float lowered = car.Front.Application * car.Front.SpringLength - s.Front.Application * s.Front.SpringLength;
        return lowered >= HandlingModel.MostLower - 1e-3f || s.Front.Application * s.Front.SpringLength <= HandlingModel.LowestPoint + 1e-3f
               || s.Rear.Application * s.Rear.SpringLength <= HandlingModel.LowestPoint + 1e-3f;
    }

    // The sliders on one car: softer springs keep the ride height, lighter damping swings more, more roll leans more, more grip
    // holds more and rolling over still comes later, more steering reach turns further at speed, quicker brakes build sooner.
    private static void Sliders(CarChassis car, HandlingSetup a, HandlingSetup b)
    {
        HandlingModel.Design(car, HandlingRules.Target(CarKind.Saloon, HandlingLevel.Full, HandlingTuning.Neutral), a);
        foreach ((string what, HandlingTuning tuning) in new[]
                 {
                     ("softness 150", new HandlingTuning(150, 100, 100, 100, 100, 100)), ("bounce 150", new HandlingTuning(100, 150, 100, 100, 100, 100)),
                     ("roll 150", new HandlingTuning(100, 100, 150, 100, 100, 100)), ("grip 150", new HandlingTuning(100, 100, 100, 150, 100, 100)),
                     ("steering 150", new HandlingTuning(100, 100, 100, 100, 150, 100)), ("brakes 150", new HandlingTuning(100, 100, 100, 100, 100, 150)),
                     ("every slider at 50", new HandlingTuning(50, 50, 50, 50, 50, 50)), ("every slider at 150", new HandlingTuning(150, 150, 150, 150, 150, 150))
                 })
        {
            string why = HandlingModel.Design(car, HandlingRules.Target(CarKind.Saloon, HandlingLevel.Full, tuning), b);
            Check(why == null && b.TipG >= HandlingModel.TipMargin * b.Hold * 0.999f, "handling: the BIMA 320 with " + what + " is designed and still slides before it rolls over (" + (why ?? F(b.TipG / b.Hold)) + ")");
            if (why != null)
            {
                continue;
            }
            switch (what)
            {
                case "softness 150":
                    Check(b.Front.Rate < a.Front.Rate * 0.7f && Near(b.Front.RestLength, a.Front.RestLength, 1e-3f), "handling: the softness slider softens the springs (" + F(a.Front.Rate) + " -> " + F(b.Front.Rate)
                        + " N/m) and keeps the travel at rest (" + F(b.Front.RestLength) + " m)");
                    break;
                case "bounce 150":
                    Check(b.Front.Zeta < a.Front.Zeta, "handling: the bounce slider lightens the damping (" + F(a.Front.Zeta) + " -> " + F(b.Front.Zeta) + ")");
                    break;
                case "roll 150":
                    Check(b.RollPerG > a.RollPerG, "handling: the roll slider leans the body further (" + F(a.RollPerG) + " -> " + F(b.RollPerG) + " degrees per g)");
                    break;
                case "grip 150":
                    Check(b.Hold > a.Hold, "handling: the grip slider makes the tyres hold more (" + F(a.Hold) + " -> " + F(b.Hold) + " g)");
                    break;
                case "steering 150":
                    Check(!b.SteerOwn && HandlingModel.SteerAt(b, car.SteerLock, 100f / 3.6f) > HandlingModel.SteerAt(a, car.SteerLock, 100f / 3.6f)
                          && Near(HandlingModel.SteerAt(b, car.SteerLock, 0f), car.SteerLock, 1e-3f),
                        "handling: the steering slider turns the wheels further at speed and keeps the lock at walking pace");
                    break;
                case "brakes 150":
                    Check(b.BrakeRise < a.BrakeRise, "handling: the brakes slider builds the brakes sooner (" + F(a.BrakeRise) + " -> " + F(b.BrakeRise) + " s)");
                    break;
            }
        }
    }

    // The brake's ramp: builds progressively, a tap is gentle, it lets go quickly.
    private static void BrakeRamps(in HandlingTuning neutral)
    {
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
                Check(jump <= 0.25f, what + " never jumps more than a quarter in a physics step (" + F(jump) + ")");
                HandlingModel.BrakeRamp(rise, actuation, 60f, 0.1f, felt);
                float tap = 0f;
                float gone = -1f;
                for (int i = 0; i < felt.Length; i++)
                {
                    tap = Math.Max(tap, felt[i]);
                    gone = gone < 0f && i * HandlingModel.Step > 0.1f && felt[i] < 0.05f ? i * HandlingModel.Step - 0.1f : gone;
                }
                Check(tap >= HandlingRules.PedalDeadZone && tap <= 0.7f, what + ": a tap of a tenth of a second brakes gently (" + F(tap) + "), and counts");
                Check(gone >= 0f && gone <= HandlingRules.BrakeFall + actuation + 0.05f, what + ": let go, it is off in " + F(gone) + " s");
            }
        }
        Check(HandlingRules.BrakePedal(0f, 0.03f, 0.016f, 0.5f) <= 0.03f && HandlingRules.BrakePedal(0.5f, 0.2f, 0.016f, 0.5f) < 0.5f && HandlingRules.BrakePedal(0.5f, 0.2f, 0.016f, 0.5f) >= 0.2f,
            "handling: the eased pedal never goes past what is asked, either way");
    }

    // The car braking from 100 km/h as the game's own chain brakes it, wheel by wheel: the eased pedal at the input's rate;
    // the brakes' onset (Brakes.cs: a MoveTowards over actuationTime each physics step); each wheel's torque, the pedal
    // times the brakes' most times its axle's share and never past the most, and its rolling resistance; each held to what
    // its tyre takes at the load its spring and damper put on it as the body dives (each axle's corner, with the build's
    // springs, pushed down or lifted by the weight the braking moves). Returns the metres; worst is the most of what its
    // tyre holds any wheel asked for (past 1 it would lock and the ABS come on).
    private static float GameStop(CarChassis car, HandlingSetup s, AxleSetup frontCorner, AxleSetup rearCorner, float brakes, float massFactor, float grip, float onset,
        out float worst)
    {
        HandlingModel.PartsBrakes(car, s, brakes, massFactor, grip, out float frontShare, out float rearShare, out _);
        float step = HandlingModel.Step;
        float mass = s.Mass * massFactor;
        float most = car.BrakeTorque * brakes;
        float arm = HandlingModel.PitchArm(car, s);
        float wheelbase = car.Front.Z - car.Rear.Z;
        float frontGrip = s.Front.AlongG * grip;
        float rearGrip = s.Rear.AlongG * grip;
        float frontMass = frontCorner.Load / HandlingModel.G;
        float rearMass = rearCorner.Load / HandlingModel.G;
        float frontLength = frontCorner.RestLength, rearLength = rearCorner.RestLength;
        float frontBefore = frontLength, rearBefore = rearLength, frontSpeed = 0f, rearSpeed = 0f;
        float pedal = 0f, felt = 0f, time = 0f, nextFrame = 0f, frame = 1f / HandlingModel.InputRate;
        float speed = HandlingModel.StopFrom, metres = 0f;
        worst = 0f;
        for (int i = 0; i < 5000 && speed > 0f; i++)
        {
            while (nextFrame <= time)
            {
                pedal = HandlingRules.BrakePedal(pedal, 1f, frame, s.BrakeRise);
                nextFrame += frame;
            }
            felt = Math.Min(pedal, felt + step / Math.Max(0.001f, onset));
            float frontLoad = Corner(car.Front, frontCorner, frontLength, (frontBefore - frontLength) / step);
            float rearLoad = Corner(car.Rear, rearCorner, rearLength, (rearBefore - rearLength) / step);
            float frontWant = (Math.Min(felt * most * frontShare, most) + s.Front.Rolling) / car.Front.Radius;
            float rearWant = (Math.Min(felt * most * rearShare, most) + s.Rear.Rolling) / car.Rear.Radius;
            float frontHold = frontGrip * frontLoad, rearHold = rearGrip * rearLoad;
            worst = Math.Max(worst, Math.Max(frontWant / Math.Max(1f, frontHold), rearWant / Math.Max(1f, rearHold)));
            float slowing = 2f * (Math.Min(frontWant, frontHold) + Math.Min(rearWant, rearHold)) / mass;
            float moved = mass * slowing * arm / (2f * wheelbase);   // N a front wheel gains and a rear wheel loses as the body dives
            frontBefore = frontLength;
            rearBefore = rearLength;
            frontSpeed += ((frontLoad - moved) / frontMass - HandlingModel.G) * step;
            rearSpeed += ((rearLoad + moved) / rearMass - HandlingModel.G) * step;
            frontLength += frontSpeed * step;
            rearLength += rearSpeed * step;
            speed = Math.Max(0f, speed - slowing * step);
            metres += speed * step;
            time += step;
        }
        return metres;
    }

    // A corner's spring and damper at a length and compression speed, as the game sums them for the tyre's load.
    private static float Corner(AxleData shape, AxleSetup s, float length, float compressing)
    {
        if (length >= s.SpringLength)
        {
            return 0f;
        }
        return Math.Max(0f, shape.Spring.Evaluate((s.SpringLength - length) / s.SpringLength) * s.SpringForce + HandlingModel.DamperAt(shape, s.Bump, s.Rebound, compressing));
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

    // The grippiest the Garage's parts of a slot make the tyres, dry or wet.
    private static float Most(PartSlot slot)
    {
        float most = 1f;
        foreach (PartDef part in GarageCatalogue.Parts)
        {
            if (part.Slot == slot)
            {
                most = Math.Max(most, Math.Max(part.Grip, part.WetGrip));
            }
        }
        return most;
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
        string apply = Body(handling, "private void PutOn(") + Body(handling, "private void BrakesForParts(");
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
        Check(written.Count >= 15 && missing.Count == 0, "handling source: all " + written.Count + " of the game's values the handling writes are written back when it comes off"
                                                          + (missing.Count > 0 ? " (not: " + string.Join(", ", missing) + ")" : ""));
        Check(apply.Contains("AddAndOnboardNewComponent(") && back.Contains("Components.Remove(") && back.Contains("Destroy(") && back.Contains("VC_Disable("),
            "handling source: the game's ABS module is put on the car and taken off it again, destroyed");
        Check(Count(apply, ".FrictionPreset = ") >= 1 && Count(back, ".FrictionPreset = ") >= 1 && apply.Contains("groundDetectionPreset = ") && back.Contains("groundDetectionPreset = "),
            "handling source: the tyres' preset and the ground's preset are the handling's while it is on, the game's again after");
        Check(!apply.Contains("centerOfMass") && !apply.Contains("forceCurve") && !apply.Contains("LateralFrictionStiffness") && !apply.Contains("slowBump"),
            "handling source: the centre of mass, the springs' curves, the tyres' slip stiffness and the dampers' shape stay the game's own");

        // The Garage's parts always lie on top: taken off before the handling changes anything, put back after; what the
        // handling wrote is read back before they go back on top of it.
        string begin = Body(handling, "public bool Begin(");
        string on = Body(handling, "public bool Finish(");
        string off = Body(handling, "public void Restore(");
        Check(Before(on, "LiftParts", "PutOn(") && Before(on, "PutOn(", "LayParts") && Before(off, "LiftParts", "TakeOff(") && Before(off, "TakeOff(", "LayParts")
              && off.Contains("_design.Cancel()"),
            "handling source: the Garage's parts are lifted before the handling goes on or comes off, and laid back on top after; coming off drops a design under way");
        Check(Before(on, "PutOn(", "_took = Took(") && Before(on, "_took = Took(", "LayParts") && Body(handling, "private string Line(").Contains("string why = _took;")
              && !Body(handling, "private string Line(").Contains("SpringMaxLength"),
            "handling source: what the handling wrote is read back before the Garage's parts go on top, and the getting-in line reports that, never the parts' own springs as a fault");
        Check(Before(on, "LayParts", "BrakesForParts(") && Body(handling, "private void BrakesForParts(").Contains("HandlingModel.PartsBrakes("),
            "handling source: the brakes are shared out for the Garage's parts once they lie on top, by the model's own sums");
        string shop = Source(scripts, @"Garage\GarageTuning.cs");
        Check(Body(shop, "public static GarageFigures Figures(").Contains("CarTuning.HandlingStop(") && Body(handling, "public float ShopStop(").Contains("HandlingModel.ShopStop("),
            "handling source: the custom shop's stopping figure comes from the handling's own model when the handling is on the car");
        Check(Body(handling, "private string Read(").Contains("_chassis.TopKmh = topKmh") && Before(begin, "LiftParts", "Read(car, vehicle, body, type.maxSpeed)")
              && Before(begin, "Read(car, vehicle, body, type.maxSpeed)", "LayParts") && !begin.Contains("PutOn(")
              && Body(handling, "private static string ReadAxle(").Contains("axle.SideStiffness = a.LateralFrictionStiffness"),
            "handling source: each car's own game values are read when the player gets in: its top speed, tyres, slip stiffness, springs, dampers and steering");

        // Every way out comes through CarTuning.Restore, which takes the handling off after its own numbers.
        string restore = Body(tuning, "private void Restore(");
        string update = Body(tuning, "private void OnUpdate(");
        Check(restore.Contains("_handling.Restore(") && Body(tuning, "private void OnDestroy(").Contains("Restore()") && update.Contains("Restore();")
              && Count(update, "Restore();") >= 2, "handling source: leaving the car, a change of car or option, a fault and the city going all take the handling off (CarTuning.Restore)");
        string carry = Body(tuning, "private void Carry(");
        Check(Before(restore, "PutBackTuning(", "_handling.Restore(") && Body(tuning, "private void PutBackTuning(").Contains("box.shiftDuration")
              && Before(carry, "_handling.Finish()", "TuneBegin(") && Body(tuning, "private Sums AfterHandling(").Contains("Sums.TuningBegin")
              && restore.Contains("_solve.Cancel()") && restore.Contains("_sums = Sums.None"),
            "handling source: the handling goes on before the car's power is worked out from its weight, and comes off after it; coming off drops the sums under way");

        // Only the car the player drives: it comes from the game's selected vehicle alone, and no other part of the mod names the handling.
        Check(update.Contains("gm.selectedVehicle as CarController") && begin.Contains("GoingPublic.MineVehicle("), "handling source: the handling is only for the car the player drives, and their own (Going Public)");
        var others = new List<string>();
        foreach (string file in Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories))
        {
            string name = Path.GetFileName(file);
            if (name == "CarHandling.cs" || name == "CarTuning.cs" || name == "HandlingRules.cs" || name == "HandlingModel.cs" || name == "HandlingTurn.cs" || name == "GarageCar.cs")
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
        Check(handlingPart.Contains("HandlingModel.FitSpring(CurveOf(wheel)") && handlingPart.Contains("float damping = Mathf.Sqrt(stiffness * weight)")
              && handlingPart.Contains("DamperBumpRate *= damping") && handlingPart.Contains("DamperReboundRate *= damping") && !handlingPart.Contains("*= m.Stiffness;"),
            "handling source: the Garage's springs stand the car higher or lower by the part's ride on the spring's own curve, a lighter car at the same height, and its dampers follow the root of the springs' and the weight's factors");
        Check(garage.Contains("public bool LiftHandling(") && garage.Contains("public void LayHandling("), "handling source: the Garage can lift its handling parts off a car and lay them back");
    }

    private static bool Before(string code, string first, string second)
    {
        int a = code.IndexOf(first, StringComparison.Ordinal);
        int b = code.IndexOf(second, a < 0 ? 0 : a, StringComparison.Ordinal);
        return a >= 0 && b > a;
    }
}
