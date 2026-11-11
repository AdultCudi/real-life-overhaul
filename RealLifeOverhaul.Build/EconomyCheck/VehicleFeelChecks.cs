using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using RealLifeOverhaul;

// The sweep of 12 Nov 2026, two players' posts about the driven car's feel, and the repair of the first answer to them.
//
// (1) "The physics added by the mod could be compatible with other vehicle mods as well ... it makes other modded vehicles seem stiff."
//     A player's Player.log has the handling running on a Ferrari SF90 Spider, a BMW M4 G82 and an Audi R8 of other mods, and PASSING: it
//     read each car's own values, as it always does. But those cars ride at 3.98 to 4.65 Hz on springs of 237 000 to 279 000 N/m (the
//     game's own cars at 0.76 to 2.88 Hz), stand on about 6 cm of travel (a 10 cm kerb leaves them -4 cm), and lean 0.12 to 0.2 degrees
//     per g (the game's soft cars 4), so the wobble the handling adds had nothing to show on. A car whose type is not the game's and rides
//     stiffer than the game's cars of its kind is now given their ride (the option "Soft ride for other mods' cars"), in the first steps
//     of its design: the softest ride its travel at rest carries, the front keeping a third of it at the full pedal's dive and each
//     axle's outer spring a third in the hardest turn the car can take, the swing of a sudden turn counted (HandlingModel.RideCarries,
//     DiveCarries, RollCarries). The first version asked 0.8 Hz of every kind but a sports car whatever the travel, and put the front of
//     a 1700 kg saloon on 6 cm of travel 3.5 cm past its stops; the next counted the roll at 1 g of cornering, where the log's cars hold
//     about 3.6 g on their tyres as sports cars, and gave the Audi R8 of the log, as a saloon, 1.005 Hz, on which its outer springs would
//     go past their stops in the hardest turn. Softened, the BMW M4 G82 kept its own dampers on its softer springs, more than the physics
//     step carries on its light rear corner, and at the full pedal its rear asked 3.5 times its share of the tyres: the dampers now go with
//     the springs, and the design takes no ride whose full pedal asks either axle more than twice its share (HandlingModel.MostLag).
//
// (2) "Better brakes, there are some bugs while going fast and suddenly braking (especially the Ford pick up)." The handling's dampers are
//     lighter than the game's, so the body's first swing in a full-pedal dive goes about 40% further than the settled dive, and nothing
//     counted what that left of the front springs' travel: the Vord V150 kept 1.7 cm of 16.5 at the stock brakes (the game's own car 7.4) and
//     none with the custom shop's. The nose dive is held back, never below the game's own, until a third of the travel is left
//     (HandlingModel.DiveForTravel); the first version stopped there, and the pickup kept 4 cm. Now the front dampers' compression rate
//     firms up as far as it takes (HandlingModel.FirmFront), the full pedal is planned for the custom shop's parts that are on the car,
//     and what the parts add to the car's own brakes is held where the front springs would be put on their stops (HandlingModel.HeldForTravel).
//     The figures kept for the self-check line are those of the dive the car came to, the anti-squat's limits counted. The parts are read off
//     the car as the handling goes on; when the Garage lays them on the car being driven after that (brakes bought sitting in it, the road
//     turning wet or dry, a save loaded), the handling is now worked out again for them (GarageBridges.PartsLaid): it kept the pedal of a car
//     with no parts, and the shop's brakes came on top of it unheld. That first took everything of the mod's off the car for it, as for an option
//     changed, each time rain began or stopped under any build; nothing comes off for parts laid now, what they change is worked out with the rest
//     left on (HandlingRules.Frame and Relay, CarHandling.Relaid: the road turning wet or dry, below).
//
// The cars are built from the figures of the game's files as NavCheck's HandlingCheck has them, and from the player's log for the three
// cars of other mods. Offline: run with --vehicle-feel (RLO_FEEL_REPORT=1 prints the figures).
internal static partial class Program
{
    private static readonly bool FeelReport = Environment.GetEnvironmentVariable("RLO_FEEL_REPORT") == "1";

    private static void FeelSay(string line)
    {
        if (FeelReport)
        {
            Console.WriteLine(line);
        }
    }

    // The game's tyres on every one of its cars (ArcadeTireFrictionPreset), as its curve's keys were saved: slip, value, slope.
    private static readonly float[] FeelTyre =
    {
        0f, 0f, 12.1844978f, 0.02f, 0.243689954f, 11.7634296f, 0.04f, 0.470537186f, 10.6454687f, 0.06f, 0.669508696f, 9.16311169f,
        0.08f, 0.837061644f, 7.63935661f, 0.1f, 0.975082934f, 6.27201653f, 0.12f, 1.08794224f, 5.13384008f, 0.14f, 1.18043649f, 4.222188f,
        0.16f, 1.25682974f, 3.50357056f, 0.18f, 1.32057929f, 2.93859601f, 0.2f, 1.37437356f, 2.49220467f, 0.22f, 1.42026746f, 1.92082977f,
        0.32f, 1.57496381f, 1.2143023f, 0.42f, 1.6631279f, 0.724394917f, 0.52f, 1.71984279f, 0.4804461f, 0.62f, 1.75921714f, 0.340925395f,
        0.72f, 1.78802788f, 0.253628433f, 0.82f, 1.80994284f, 0.195462048f, 0.92f, 1.8271203f, 0.154845089f, 1.02f, 1.84091187f, 0.137915581f
    };

    // The game's two steering curves (share of the lock by speed over 50 m/s): most cars', and the vans', trucks' and older cars'.
    private static readonly float[] FeelSteerCars = { 0f, 1f, 0f, 0f, 0.3f, 0.5f, -0.6f, -0.6f, 1f, 0.2f, -0.1f, -0.1f };
    private static readonly float[] FeelSteerVans = { 0f, 1f, 0f, 0f, 0.3f, 0.4f, -0.6f, -0.6f, 1f, 0.2f, -0.1f, 0.1f };

    // The Mersaidi Dash's spring curve, which rises: time, value, in slope, out slope.
    private static readonly float[] FeelDashSpring = { 0f, 0f, 0.259f, 0.259f, 1f, 1f, 2.031f, 2.031f };

    private static readonly HandlingTuning FeelNeutral = HandlingTuning.Neutral;
    private static readonly HandlingTuning FeelOdd = new HandlingTuning(70, 150, 60, 120, 80, 70);   // the sliders NavCheck's spread check uses

    // One axle as the game's files have it (every car's tyres grip 1.3 to the side and 0.9 along on the front, slip stiffness 1.27, rated for 7000 N,
    // 30 N m of rolling resistance, dampers on the game's two-rate shape), the spring straight on its curve.
    private static void FeelAxle(AxleData axle, float z, float track, float top, float radius, float springForce, float springLength, float application,
        float damper, float bar)
    {
        axle.Z = z;
        axle.Track = track;
        axle.Top = top;
        axle.Radius = radius;
        axle.SpringForce = springForce;
        axle.SpringLength = springLength;
        axle.Spring.Straight();
        axle.SideGrip = 1.3f;
        axle.AlongGrip = 0.9f;
        axle.SideStiffness = 1.27f;
        axle.LoadRating = 7000f;
        axle.Rolling = 30f;
        axle.AntiRoll = bar;
        axle.BrakeShare = 1f;
        axle.Bump = damper;
        axle.Rebound = damper;
        axle.SlowBump = 1.4f;
        axle.FastBump = 0.6f;
        axle.BumpKnee = 0.06f;
        axle.SlowRebound = 1.6f;
        axle.FastRebound = 0.6f;
        axle.ReboundKnee = 0.05f;
        axle.Application = application;
        axle.AntiSquat = 0f;
    }

    private static CarChassis FeelCar(float mass, float comY, float comZ, float brakeTorque, float steerLock, float topKmh, float[] steer)
    {
        var car = new CarChassis { Mass = mass, ComY = comY, ComZ = comZ, BrakeTorque = brakeTorque, SteerLock = steerLock, TopKmh = topKmh, TyreD = 2f, TyrePeakSlip = 1f };
        for (int i = 0; i < FeelTyre.Length; i += 3)
        {
            car.Tyre.Add(FeelTyre[i], FeelTyre[i + 1], FeelTyre[i + 2], FeelTyre[i + 2]);
        }
        for (int i = 0; i < steer.Length; i += 4)
        {
            car.Steer.Add(steer[i], steer[i + 1], steer[i + 2], steer[i + 3]);
        }
        return car;
    }

    // The rear axle of the game's cars grips 1.5 both ways on a slip stiffness of 1.7 and gets 0.184 of the front's brake torque.
    private static CarChassis FeelGameCar(float mass, float comY, float comZ, float brakeTorque, float steerLock, float topKmh, float[] steer,
        float frontZ, float rearZ, float frontTrack, float rearTrack, float top, float radius, float springForce, float springLength, float application)
    {
        CarChassis car = FeelCar(mass, comY, comZ, brakeTorque, steerLock, topKmh, steer);
        FeelAxle(car.Front, frontZ, frontTrack, top, radius, springForce, springLength, application, 5000f, 4200f);
        FeelAxle(car.Rear, rearZ, rearTrack, top, radius, springForce, springLength, application, 5000f, 4200f);
        car.Rear.SideGrip = 1.5f;
        car.Rear.AlongGrip = 1.5f;
        car.Rear.SideStiffness = 1.7f;
        car.Rear.BrakeShare = 0.184f;
        return car;
    }

    // Cars of the game's own, by the type names HandlingCheck has them under.
    private static CarChassis FeelPickup() => FeelGameCar(1400f, 0.30f, 0.00f, 4000f, 45f, 80f, FeelSteerVans, 1.5109f, -1.6868f, 1.5496f, 1.5496f, 0.5630f, 0.4027f, 5000f, 0.60f, 0.2f);

    private static CarChassis FeelMimic() => FeelGameCar(1400f, 0.30f, 0.00f, 2500f, 35f, 60f, FeelSteerVans, 1.4073f, -1.2937f, 1.5038f, 1.5038f, 0.4698f, 0.3054f, 5000f, 0.60f, 0.2f);

    private static CarChassis FeelTiara() => FeelGameCar(1400f, 0.30f, 0.00f, 3500f, 35f, 70f, FeelSteerVans, 1.3970f, -1.2680f, 1.5038f, 1.5038f, 0.4805f, 0.3054f, 5000f, 0.60f, 0.2f);

    private static CarChassis FeelLytte() => FeelGameCar(1400f, 0.30f, 0.00f, 10000f, 35f, 80f, FeelSteerVans, 1.5475f, -1.2573f, 1.5778f, 1.5778f, 0.4193f, 0.3680f, 5000f, 0.20f, 0.2f);

    private static CarChassis FeelSupercar() => FeelGameCar(1400f, 0.30f, 0.00f, 10000f, 35f, 100f, FeelSteerCars, 1.3214f, -1.3069f, 1.6177f, 1.6346f, 0.4946f, 0.35f, 10000f, 0.25f, 0.8f);

    private static CarChassis FeelCourier() => FeelGameCar(1400f, 0.40f, 0.68f, 2500f, 42f, 55f, FeelSteerVans, 2.7580f, -1.3599f, 2.0264f, 1.7478f, 0.6100f, 0.43f, 5000f, 0.60f, 0.2f);

    // The Mersaidi Dash: 3.6 cm of travel at rest, a spring that rises, the centre of mass high and forward.
    private static CarChassis FeelDash()
    {
        CarChassis car = FeelGameCar(1400f, 0.60f, 0.80f, 2500f, 35f, 65f, FeelSteerVans, 2.4000f, -1.6200f, 1.7000f, 1.7000f, 0.3600f, 0.4027f, 5000f, 0.40f, 0.5f);
        foreach (AxleData axle in new[] { car.Front, car.Rear })
        {
            axle.Spring.Clear();
            for (int i = 0; i < FeelDashSpring.Length; i += 4)
            {
                axle.Spring.Add(FeelDashSpring[i], FeelDashSpring[i + 1], FeelDashSpring[i + 2], FeelDashSpring[i + 3]);
            }
        }
        return car;
    }

    // The cars NavCheck assumes for what another mod might bring, at the edges of the game's own: a tall van on soft springs (its anti-squat
    // limit binds at Full) and a heavy truck (at Heavy).
    private static CarChassis FeelTallVan() => FeelGameCar(1900f, 0.85f, 0.30f, 3000f, 38f, 60f, FeelSteerVans, 2.10f, -1.60f, 1.55f, 1.58f, 0.40f, 0.38f, 9000f, 0.45f, 0.4f);

    private static CarChassis FeelHeavyTruck() => FeelGameCar(4000f, 0.70f, 0.90f, 4000f, 45f, 50f, FeelSteerVans, 3.20f, -1.80f, 2.05f, 1.95f, 0.65f, 0.50f, 24000f, 0.40f, 0.3f);

    // A car of another mod: its mass, the centre of mass's height above the road, the springs' rates and the bars as a Player.log gives them, the travel
    // left to compress at rest, the force points' height and the dampers' rate; the wheelbase from the front axle at 1.2 m and the rear at -1.45 m of
    // the centre of mass, the track 1.62 m, the tyres, steering and brake shares those of a sports car (the log: shares 1 and 0.55). The springs straight.
    private static CarChassis FeelOther(float mass, float comHeight, float frontRate, float rearRate, float bar, float rest, float point, float damper, float brakeTorque,
        float frontZ = 1.2f, float rearZ = -1.45f, float topKmh = 250f)
    {
        CarChassis car = FeelCar(mass, 0f, 0f, brakeTorque, 35f, topKmh, FeelSteerCars);
        float weight = mass * HandlingModel.G;
        float wheelbase = frontZ - rearZ;
        FeelSpring(car.Front, frontZ, weight * (0f - rearZ) / (2f * wheelbase), frontRate, rest, point, damper, bar);
        FeelSpring(car.Rear, rearZ, weight * (frontZ - 0f) / (2f * wheelbase), rearRate, rest, point, damper, bar);
        car.Rear.SideGrip = 1.5f;
        car.Rear.AlongGrip = 1.5f;
        car.Rear.SideStiffness = 1.7f;
        car.Rear.BrakeShare = 0.55f;
        car.ComY = comHeight + (0.45f - rest - 0.34f);   // the spring tops 0.45 m up, the wheels 0.34 m in radius: the road is that far below the standing body
        return car;
    }

    // A spring of a rate, on the load an axle's wheel carries, with a travel left to compress at rest; its force point a height above the road.
    private static void FeelSpring(AxleData axle, float z, float load, float rate, float rest, float point, float damper, float bar)
    {
        float length = rest + load / rate;
        FeelAxle(axle, z, 1.62f, 0.45f, 0.34f, rate * length, length, point / length, damper, bar);
    }

    // A car of another mod from a ride in Hz instead of a rate: the spring that rides so under the load of each axle.
    private static CarChassis FeelOtherHz(float mass, float comHeight, float hz, float bar, float rest, float point, float damper, float brakeTorque)
    {
        float weight = mass * HandlingModel.G;
        float wheelbase = 1.2f + 1.45f;
        float k = 4f * (float)(Math.PI * Math.PI) * hz * hz;
        float frontRate = k * weight * 1.45f / (2f * wheelbase) / HandlingModel.G;
        float rearRate = k * weight * 1.2f / (2f * wheelbase) / HandlingModel.G;
        return FeelOther(mass, comHeight, frontRate, rearRate, bar, rest, point, damper, brakeTorque);
    }

    // The three cars of other mods the player's log read: the rates and bars as logged, the weight and the centre of mass's height as logged, the travel
    // from the kerb line (a 10 cm kerb leaves -4.065, -4.152 and -4.027 cm), the dampers that give the log's damping ratio (0.765, then 1 and 1: no swing
    // past rest at all), and the brake torque that gives the log's own full pedal on the assumed wheelbase (1.249, 1.35 and 1.33 g).
    private static readonly string[] FeelLoggedNames = { "Ferrari SF90 Spider", "BMW M4 G82", "Audi R8" };

    private static CarChassis FeelLogged(int which)
    {
        float[] mass = { 1670f, 1775f, 1595f };
        float[] com = { 0.15f, 0.114f, 0.179f };
        float[] frontRate = { 273333f, 278571f, 253333f };
        float[] rearRate = { 273333f, 264286f, 237500f };
        float[] bars = { 7200f, 8400f, 11000f };
        float[] rest = { 0.05935f, 0.05848f, 0.05973f };
        float[] point = { 0.026f, 0.025f, 0.027f };
        float[] damper = { 17000f, 28000f, 24000f };
        float[] torque = { 2205.5f, 2539.5f, 2243.7f };
        return FeelOther(mass[which], com[which], frontRate[which], rearRate[which], bars[which], rest[which], point[which], damper[which], torque[which]);
    }

    // The N on a front and on a rear wheel at rest, and a ride in Hz as the model counts it.
    private static void FeelLoads(CarChassis car, out float front, out float rear)
    {
        float weight = car.Mass * HandlingModel.G;
        float wheelbase = car.Front.Z - car.Rear.Z;
        front = weight * (car.ComZ - car.Rear.Z) / (2f * wheelbase);
        rear = weight * (car.Front.Z - car.ComZ) / (2f * wheelbase);
    }

    private static float FeelRide(AxleData axle, float load)
    {
        float compression = HandlingModel.RestCompression(axle.Spring, axle.SpringForce, load);
        float rate = axle.SpringForce * axle.Spring.Slope(compression) / axle.SpringLength;
        return (float)Math.Sqrt(rate / (load / HandlingModel.G)) / (2f * (float)Math.PI);
    }

    private static float FeelRest(AxleData axle, float load)
    {
        return axle.SpringLength * (1f - HandlingModel.RestCompression(axle.Spring, axle.SpringForce, load));
    }

    private static string FeelF(float value) => value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

    private static string FeelCm(float metres) => FeelF(metres * 100f);

    // What the front keeps at the full pedal on the level's own dampers, the compression not firmed, at the dive the design came to: what the first fix
    // (the dive held to the game's own, nothing more) left.
    private static float FeelBare(CarChassis car, in HandlingTarget target, HandlingSetup setup)
    {
        float firm = setup.Front.Bump;
        setup.Front.Bump = firm / setup.FrontFirm;
        float left = HandlingModel.FrontLeftAt(car, target, setup, setup.DivePerG);
        setup.Front.Bump = firm;
        return left;
    }

    // What each axle's outer spring keeps of its travel at rest in the hardest turn a finished design's tyres hold, the swing of a sudden turn counted
    // (HandlingModel.RollLeft at the design's own HandlingSetup.Hold, the custom shop's tyres on top): the rule the self-check line reads.
    private static float FeelRollLeft(CarChassis car, HandlingSetup setup)
    {
        return HandlingModel.RollLeft(car, setup, setup.Hold * car.PartsGrip);
    }

    // Whether two cars have the same springs, to the bit.
    private static bool FeelSameSprings(CarChassis a, CarChassis b)
    {
        return a.Front.SpringForce == b.Front.SpringForce && a.Front.SpringLength == b.Front.SpringLength && a.Front.Application == b.Front.Application
               && a.Rear.SpringForce == b.Rear.SpringForce && a.Rear.SpringLength == b.Rear.SpringLength && a.Rear.Application == b.Rear.Application;
    }

    private static void VehicleFeelAll()
    {
        VehicleFeelRules();
        VehicleFeelSoftRide();
        VehicleFeelOtherKinds();
        VehicleFeelTravel();
        VehicleFeelBuilds();
        VehicleFeelBrakes();
        VehicleFeelVerdict();
        VehicleFeelSpread();
        VehicleFeelRelay();
        VehicleFeelSources();
        VehicleFeelWords();
    }

    // ---------------------------------------------------------------- the rules

    private static void VehicleFeelRules()
    {
        Check(CarCatalogue.IsGameCar("ba:vehicletype_vordv150") && CarCatalogue.IsGameCar("ba:vehicletype_anselmoaf90"),
            "vehicle feel: the game's own cars are told by the names of their types");
        Check(!CarCatalogue.IsGameCar("ferrarisf90spider-vehicle:vehicletype_ferrarisf90spider") && !CarCatalogue.IsGameCar("empiremustanggt:vehicletype_mustanggt")
              && !CarCatalogue.IsGameCar(string.Empty) && !CarCatalogue.IsGameCar(null),
            "vehicle feel: a car of another mod is any type that is not the game's, a missing name too");

        // The decision the driven car's handling takes: asked for the kind's ride, or not.
        Check(HandlingRules.SoftensCar("ferrarisf90spider-vehicle:vehicletype_ferrarisf90spider", true) && HandlingRules.SoftensCar("empiremustanggt:vehicletype_mustanggt", true)
              && HandlingRules.SoftensCar(null, true) && HandlingRules.SoftensCar(string.Empty, true),
            "vehicle feel: with the option on a car of another mod, or a type with no name, is asked for the kind's ride");
        Check(!HandlingRules.SoftensCar("ba:vehicletype_vordv150", true) && !HandlingRules.SoftensCar("ba:vehicletype_anselmoaf90", true) && !HandlingRules.SoftensCar("ba:vehicletype_umcnunavut", true),
            "vehicle feel: a car of the game's own is never asked for it, with the option on");
        Check(!HandlingRules.SoftensCar("ferrarisf90spider-vehicle:vehicletype_ferrarisf90spider", false) && !HandlingRules.SoftensCar(null, false)
              && !HandlingRules.SoftensCar("ba:vehicletype_vordv150", false),
            "vehicle feel: with the option off no car is asked for it");

        bool kindsRight = true;
        foreach (CarKind kind in new[] { CarKind.Economy, CarKind.Saloon, CarKind.Luxury, CarKind.Sports, CarKind.Utility, CarKind.Van, CarKind.Truck })
        {
            float ceiling = kind == CarKind.Sports ? HandlingRules.SportsRide : HandlingRules.SoftRide;
            kindsRight &= HandlingRules.RideCeiling(kind, HandlingLevel.Full) == ceiling && HandlingRules.RideCeiling(kind, HandlingLevel.Heavy) == ceiling
                          && HandlingRules.RideCeiling(kind, HandlingLevel.Light) == 0f && HandlingRules.RideCeiling(kind, HandlingLevel.Off) == 0f;
            HandlingTarget asked = HandlingRules.Target(kind, HandlingLevel.Full, FeelNeutral, true);
            HandlingTarget kept = HandlingRules.Target(kind, HandlingLevel.Full, FeelNeutral);
            HandlingTarget off = HandlingRules.Target(kind, HandlingLevel.Full, FeelNeutral, false);
            HandlingTarget light = HandlingRules.Target(kind, HandlingLevel.Light, FeelNeutral, true);
            kindsRight &= asked.RideHz == ceiling && kept.RideHz == 0f && off.RideHz == 0f && light.RideHz == 0f;
        }
        Check(kindsRight && HandlingRules.RideCeiling(CarKind.Untouched, HandlingLevel.Full) == 0f && HandlingRules.SportsRide == 1.7f && HandlingRules.SoftRide == 0.8f,
            "vehicle feel: a car of another mod is asked for the kind's ride at Full and Heavy (1.7 Hz for a sports car, 0.8 for the rest), never at Light or off, and a car of the game's own is asked for nothing");

        Check(HandlingModel.PartsFactor(1500f, 1000f, 1f) == 1.5f && HandlingModel.PartsFactor(900f, 1000f, 1f) == 0.9f && HandlingModel.PartsFactor(1200f, 0f, 1f) == 1f
              && HandlingModel.PartsFactor(1.2f, 1f, 0.01f) == 1.2f && HandlingModel.PartsFactor(1f, 0.005f, 0.01f) == 1f,
            "vehicle feel: a custom shop part's factor is the value with the parts on over the value under them, 1 where nothing is under them to tell it by");

        // The roll's use of the springs' travel: 2 degrees per g, a sudden turn swinging the body 1.2 times as far, a track of 1.6 m on 6 cm of travel at
        // the front and 8 at the rear. At 1 g the front's outer spring gives 0.042 rad x 0.8 m = 3.35 cm and keeps 0.44 of its travel; at 3 g, where the
        // game's tyres still hold, it would give 10 cm of its 6, and the rule says so: counted at 1 g it would not.
        var rollCar = new CarChassis();
        rollCar.Front.Track = 1.6f;
        rollCar.Rear.Track = 1.6f;
        var roll = new HandlingSetup { RollPerG = 2f, Overshoot = 1.2f };
        roll.Front.RestLength = 0.06f;
        roll.Rear.RestLength = 0.08f;
        float atOne = HandlingModel.RollLeft(rollCar, roll, 1f);
        float atThree = HandlingModel.RollLeft(rollCar, roll, 3f);
        Check(Math.Abs(atOne - 0.441495f) < 1e-4f && Math.Abs(atThree + 0.67552f) < 1e-4f && HandlingModel.RollKept(rollCar, roll, 1f) && !HandlingModel.RollKept(rollCar, roll, 3f),
            "vehicle feel: the outer spring of the shorter travel keeps " + FeelF(atOne) + " of it at 1 g of cornering and " + FeelF(atThree) + " (past its stops) at 3 g; a third kept passes, less fails");
        roll.Overshoot = 0.5f;
        float noSwing = HandlingModel.RollLeft(rollCar, roll, 1f);
        roll.RollPerG = -1f;
        Check(Math.Abs(noSwing - (1f - 2f / 57.29578f * 0.8f / 0.06f)) < 1e-4f && HandlingModel.RollLeft(rollCar, roll, 3f) == 1f,
            "vehicle feel: a swing below the steady lean counts as none (" + FeelF(noSwing) + "), and a car that does not lean keeps all its travel");
    }

    // ---------------------------------------------------------------- (1) the soft ride, on the three sports cars of the log

    private static void VehicleFeelSoftRide()
    {
        float ceiling = HandlingRules.RideCeiling(CarKind.Sports, HandlingLevel.Full);
        var stiffSetup = new HandlingSetup();
        var softSetup = new HandlingSetup();
        var lightSetup = new HandlingSetup();
        var scratch = new HandlingSetup();
        int whole = 0;
        for (int which = 0; which < FeelLoggedNames.Length; which++)
        {
            string name = FeelLoggedNames[which];
            CarChassis stock = FeelLogged(which);
            FeelLoads(stock, out float loadFront, out float loadRear);
            float ownFront = FeelRide(stock.Front, loadFront);
            float ownRear = FeelRide(stock.Rear, loadRear);
            float restFront = FeelRest(stock.Front, loadFront);
            float pointFront = stock.Front.Application * stock.Front.SpringLength;
            Check(ownFront >= 3.5f && ownFront <= 5f && ownRear >= 3.5f && ownRear <= 5f,
                "vehicle feel: the " + name + " rides at " + FeelF(ownFront) + "/" + FeelF(ownRear) + " Hz as the player's log read it (3.98 to 4.65), the game's own cars at 0.76 to 2.88");
            Check(restFront > 0.055f && restFront < 0.065f,
                "vehicle feel: the " + name + " stands on " + FeelCm(restFront) + " cm of travel at rest, the 6 or so the player's log says (a 10 cm kerb leaves -4 cm), not the 12 to 13 of the first fixtures");

            // The building block: every axle given the ride, the same travel left to compress, the force points as high above the road.
            CarChassis plain = FeelLogged(which);
            Check(HandlingModel.Soften(plain, ceiling), "vehicle feel: the " + name + ", stiffer than any sports car of the game's, is softened");
            Check(Math.Abs(FeelRide(plain.Front, loadFront) - ceiling) < 0.03f && Math.Abs(FeelRide(plain.Rear, loadRear) - ceiling) < 0.03f,
                "vehicle feel: the " + name + " softened rides at " + FeelF(FeelRide(plain.Front, loadFront)) + "/" + FeelF(FeelRide(plain.Rear, loadRear)) + " Hz, the ceiling's " + FeelF(ceiling));
            Check(Math.Abs(FeelRest(plain.Front, loadFront) - restFront) < 1e-4f && plain.Front.SpringLength > stock.Front.SpringLength,
                "vehicle feel: the " + name + " stands as high as it did: the same travel left to compress at rest (" + FeelCm(restFront) + " cm), the spring longer ("
                + FeelCm(stock.Front.SpringLength) + " -> " + FeelCm(plain.Front.SpringLength) + " cm)");
            Check(Math.Abs(plain.Front.Application * plain.Front.SpringLength - pointFront) < 1e-4f, "vehicle feel: the " + name + "'s force points stay as high above the road as they were");
            Check(Math.Abs(plain.Front.OwnFrequency - ownFront) < 0.01f && Math.Abs(plain.Rear.OwnFrequency - ownRear) < 0.01f && stock.Front.OwnFrequency == 0f,
                "vehicle feel: the " + name + " is told what it rode at before, for the self-check line");
            Check(!HandlingModel.Soften(plain, ceiling) && plain.Front.OwnFrequency == 0f, "vehicle feel: a car already at the ride asked is left as it is");
            CarChassis off = FeelLogged(which);
            Check(!HandlingModel.Soften(off, 0f) && FeelSameSprings(off, stock), "vehicle feel: with no ride asked the " + name + " keeps the springs its mod gave it");

            // The design: the ride the game's softest sports car has, where the car's travel at rest carries it, and where it does not the softest
            // ride it carries (on 6 cm the roll in the hardest turn its tyres hold can be what holds it).
            HandlingTarget asked = HandlingRules.Target(CarKind.Sports, HandlingLevel.Full, FeelNeutral, true);
            CarChassis stiff = FeelLogged(which);
            CarChassis soft = FeelLogged(which);
            string stiffWhy = HandlingModel.Design(stiff, HandlingRules.Target(CarKind.Sports, HandlingLevel.Full, FeelNeutral), stiffSetup);
            string softWhy = HandlingModel.Design(soft, asked, softSetup);
            Check(stiffWhy == null && softWhy == null, "vehicle feel: the " + name + " is designed stiff and softened (" + (stiffWhy ?? softWhy ?? "yes") + ")");
            if (stiffWhy != null || softWhy != null)
            {
                continue;
            }
            Check(which == 0 ? Math.Abs(stiffSetup.Front.GameZeta - 0.765f) < 0.07f : stiffSetup.Front.GameZeta > 0.93f,
                "vehicle feel: the " + name + "'s dampers swing as the player's log says (damping ratio " + (which == 0 ? "0.765" : "1") + "; here " + FeelF(stiffSetup.Front.GameZeta) + ")");
            float keep = HandlingModel.FrontKeep * softSetup.Front.RestLength;
            FeelSay("  " + name + " as a sports car at Full: ride " + FeelF(stiffSetup.Front.Frequency) + " -> " + FeelF(softSetup.Front.Frequency) + " Hz, front keeps " + FeelCm(stiffSetup.FrontLeft)
                    + " -> " + FeelCm(softSetup.FrontLeft) + " of " + FeelCm(softSetup.Front.RestLength) + " cm, roll " + FeelF(stiffSetup.RollPerG) + " -> " + FeelF(softSetup.RollPerG)
                    + ", dive " + FeelF(stiffSetup.DivePerG) + " -> " + FeelF(softSetup.DivePerG) + ", firm " + FeelF(softSetup.FrontFirm));
            bool carried = HandlingModel.RideCarries(plain, asked, scratch);
            whole += carried ? 1 : 0;
            Check(softSetup.Softened && softSetup.SoftHeld == !carried && (softSetup.SoftHeld || FeelSameSprings(soft, plain)) && !stiffSetup.Softened,
                "vehicle feel: the " + name + " is given the ride of the game's sports car, 1.7 Hz, " + (carried ? "for its travel at rest carries it" : "as far as its travel at rest carries it")
                + " (softened " + softSetup.Softened + ", held " + softSetup.SoftHeld + ")");
            float rollLeft = FeelRollLeft(soft, softSetup);
            Check(softSetup.FrontLeft >= keep - HandlingModel.TravelSpare && HandlingModel.RideCarries(soft, asked, scratch) && rollLeft >= HandlingModel.FrontKeep - 0.01f,
                "vehicle feel: the " + name + " softened keeps " + FeelCm(softSetup.FrontLeft) + " of its " + FeelCm(softSetup.Front.RestLength) + " cm at the full pedal (a third: " + FeelCm(keep)
                + "), and its outer springs " + FeelF(rollLeft) + " of their travel in the hardest turn its tyres hold (" + FeelF(softSetup.Hold) + " g)");
            Check(softSetup.Front.Frequency >= ceiling - 0.05f && softSetup.Front.Frequency <= 0.75f * stiffSetup.Front.Frequency && stiffSetup.Front.Frequency >= 3.5f,
                "vehicle feel: the " + name + "'s ride in the finished design is " + FeelF(stiffSetup.Front.Frequency) + " Hz stiff and " + FeelF(softSetup.Front.Frequency) + " softened (asked "
                + FeelF(ceiling) + ")");
            Check(Math.Abs(softSetup.Front.Zeta - 0.55f) <= 0.06f && Math.Abs(softSetup.Rear.Zeta - 0.55f) <= 0.06f,
                "vehicle feel: the " + name + "'s dampers are set for a damping ratio of 0.55 on the softer springs (" + FeelF(softSetup.Front.Zeta) + "/" + FeelF(softSetup.Rear.Zeta) + ")");
            Check(Math.Abs(softSetup.Front.RestLength - stiffSetup.Front.RestLength) < 1e-3f && Math.Abs(softSetup.ComHeight - stiffSetup.ComHeight) < 1e-3f,
                "vehicle feel: the " + name + " stands where it stood: " + FeelCm(softSetup.Front.RestLength) + " cm of travel at rest, the centre of mass " + FeelF(softSetup.ComHeight) + " m up");
            Check(softSetup.RollPerG > stiffSetup.RollPerG * 1.5f && softSetup.DivePerG > stiffSetup.DivePerG * 1.5f,
                "vehicle feel: the " + name + " now leans " + FeelF(softSetup.RollPerG) + " degrees per g (" + FeelF(stiffSetup.RollPerG) + " stiff) and dips its nose "
                + FeelF(softSetup.DivePerG) + " (" + FeelF(stiffSetup.DivePerG) + "): the wobble has something to show on");
            float kerb = HandlingModel.KerbLeft(soft.Front, softSetup.Front.SpringForce, softSetup.Front.SpringLength, softSetup.Front.Bump, softSetup.Front.Rebound,
                softSetup.Front.Load / HandlingModel.G, HandlingModel.Kerb);
            float kerbStiff = HandlingModel.KerbLeft(stiff.Front, stiffSetup.Front.SpringForce, stiffSetup.Front.SpringLength, stiffSetup.Front.Bump, stiffSetup.Front.Rebound,
                stiffSetup.Front.Load / HandlingModel.G, HandlingModel.Kerb);
            Check(Math.Abs(kerb - kerbStiff) < 0.002f,
                "vehicle feel: a " + FeelCm(HandlingModel.Kerb) + " cm kerb leaves the softened " + name + " " + FeelCm(kerb) + " cm of travel, as it left the stiff car (" + FeelCm(kerbStiff)
                + " cm): the softer spring takes none of the travel it has not got");

            // At Light, and with the ride not asked, the car has the design of its own springs.
            CarChassis light = FeelLogged(which);
            string lightWhy = HandlingModel.Design(light, HandlingRules.Target(CarKind.Sports, HandlingLevel.Light, FeelNeutral, true), lightSetup);
            CarChassis lightStiff = FeelLogged(which);
            string lightStiffWhy = HandlingModel.Design(lightStiff, HandlingRules.Target(CarKind.Sports, HandlingLevel.Light, FeelNeutral), stiffSetup);
            Check(lightWhy == null && lightStiffWhy == null && !lightSetup.Softened && FeelSameSprings(light, stock) && lightSetup.Front.SpringForce == stiffSetup.Front.SpringForce
                  && lightSetup.Front.Frequency == stiffSetup.Front.Frequency,
                "vehicle feel: at Light the " + name + " has the design of its own springs");
        }
        FeelSay("  the log's three cars as sports cars: " + whole + " of " + FeelLoggedNames.Length + " given the whole 1.7 Hz, the rest the softest ride their travel carries");

        // The game's own softest cars are left as they are (a ride within a tenth of the ceiling is not a change anyone sees).
        CarChassis supercar = FeelSupercar();
        FeelLoads(supercar, out float superFront, out _);
        CarChassis mimic = FeelMimic();
        FeelLoads(mimic, out float mimicFront, out _);
        Check(!HandlingModel.Soften(supercar, ceiling) && supercar.Front.OwnFrequency == 0f && !HandlingModel.Soften(mimic, HandlingRules.SoftRide) && mimic.Front.OwnFrequency == 0f,
            "vehicle feel: a car riding at the ceiling or within a tenth of it is left as it is (the Anselmo AF90 " + FeelF(FeelRide(supercar.Front, superFront)) + " Hz, the Honza Mimic "
            + FeelF(FeelRide(mimic.Front, mimicFront)) + ")");
        var held = new HandlingSetup();
        CarChassis supercarAsked = FeelSupercar();
        string supercarWhy = HandlingModel.Design(supercarAsked, HandlingRules.Target(CarKind.Sports, HandlingLevel.Full, FeelNeutral, true), held);
        Check(supercarWhy == null && !held.Softened && !held.SoftHeld && FeelSameSprings(supercarAsked, FeelSupercar()),
            "vehicle feel: a design asked for the ride of a car that has it already changes no spring and holds nothing back");
        var odd = new CarChassis { Mass = 50f };
        Check(!HandlingModel.Soften(odd, ceiling), "vehicle feel: a chassis that is no car is not softened");
        string oddWhy = HandlingModel.Design(odd, HandlingRules.Target(CarKind.Sports, HandlingLevel.Full, FeelNeutral, true), held);
        Check(oddWhy != null && !held.Softened, "vehicle feel: a chassis that is no car is refused by a design that asks for the ride, nothing softened");
    }

    // ---------------------------------------------------------------- (1) the soft ride on cars of every other kind: the travel is the limit

    private static void VehicleFeelOtherKinds()
    {
        CarKind[] kinds = { CarKind.Economy, CarKind.Saloon, CarKind.Luxury, CarKind.Utility, CarKind.Van, CarKind.Truck };
        HandlingLevel[] levels = { HandlingLevel.Full, HandlingLevel.Heavy };
        var setup = new HandlingSetup();
        var unguarded = new HandlingSetup();
        var scratch = new HandlingSetup();

        // The log's three cars, classed as any kind (CarCatalogue.Guess does that for every mod car under tier 85): 6 cm of travel at rest cannot carry
        // 0.8 Hz; the car is given the softest ride that it does carry, never less than its own comes to.
        int designs = 0, held = 0, bottomed = 0;
        bool auditaken = false;
        for (int which = 0; which < FeelLoggedNames.Length; which++)
        {
            foreach (CarKind kind in kinds)
            {
                foreach (HandlingLevel level in levels)
                {
                    string name = "the " + FeelLoggedNames[which] + " as a " + kind + " at " + level;
                    HandlingTarget asked = HandlingRules.Target(kind, level, FeelNeutral, true);
                    CarChassis stock = FeelLogged(which);
                    FeelLoads(stock, out float loadFront, out _);
                    float own = FeelRide(stock.Front, loadFront);
                    CarChassis car = FeelLogged(which);
                    string why = HandlingModel.Design(car, asked, setup);
                    designs++;
                    Check(why == null, "vehicle feel: " + name + " is designed (" + (why ?? "yes") + ")");
                    if (why != null)
                    {
                        continue;
                    }
                    // The same car given the ceiling's ride whatever its travel, as the first version did.
                    CarChassis plain = FeelLogged(which);
                    HandlingModel.Soften(plain, asked.RideHz);
                    HandlingTarget plainTarget = HandlingRules.Target(kind, level, FeelNeutral);
                    string plainWhy = HandlingModel.Design(plain, plainTarget, unguarded);
                    float bare = plainWhy == null ? FeelBare(plain, plainTarget, unguarded) : 0f;
                    float keep = HandlingModel.FrontKeep * setup.Front.RestLength;
                    float ride = setup.Front.GameFrequency;
                    bool bottoms = plainWhy == null && bare < keep;
                    held += setup.SoftHeld ? 1 : 0;
                    bottomed += bottoms ? 1 : 0;
                    FeelSay("  " + name + ": rides " + FeelF(ride) + " Hz (asked " + FeelF(asked.RideHz) + ", its own " + FeelF(own) + "), front keeps " + FeelCm(setup.FrontLeft) + " of " + FeelCm(setup.Front.RestLength)
                            + " cm, at the ceiling on the level's dampers it would keep " + FeelCm(bare) + " (" + FeelCm(unguarded.FrontLeft) + " with them firmed), roll " + FeelF(setup.RollPerG) + ", dive " + FeelF(setup.DivePerG) + " (game " + FeelF(setup.GameDivePerG)
                            + "), held " + setup.SoftHeld + ", firm " + FeelF(setup.FrontFirm));
                    float rollLeft = FeelRollLeft(car, setup);
                    Check(HandlingModel.TravelKept(setup, setup.FrontLeft)
                          && (setup.Softened
                              ? setup.FrontLeft >= keep - HandlingModel.TravelSpare && HandlingModel.RideCarries(car, asked, scratch) && rollLeft >= HandlingModel.FrontKeep - 0.01f
                              : FeelSameSprings(car, stock)),
                        "vehicle feel: " + name + " keeps " + FeelCm(setup.FrontLeft) + " of its " + FeelCm(setup.Front.RestLength) + " cm of travel at the full pedal's dive on the ride it was given, "
                        + FeelF(ride) + " Hz (a third: " + FeelCm(keep) + "; asked " + FeelF(asked.RideHz) + "), and its outer springs " + FeelF(rollLeft) + " of theirs in the hardest turn its tyres hold ("
                        + FeelF(setup.Hold) + " g)");
                    Check(ride >= asked.RideHz - 0.03f && ride <= own + 0.03f && (setup.Softened || setup.SoftHeld),
                        "vehicle feel: " + name + " rides between the ride asked (" + FeelF(asked.RideHz) + ") and its own (" + FeelF(own) + ") Hz: " + FeelF(ride));
                    Check(setup.SoftHeld == !HandlingModel.RideCarries(plain, asked, scratch) || plainWhy != null,
                        "vehicle feel: " + name + " is held off the ceiling's ride exactly when its travel does not carry that ride");
                    if (setup.Softened && setup.SoftHeld)
                    {
                        // No softer than it must be: a ride a tenth softer than the one it was given is one the travel does not carry.
                        CarChassis softer = FeelLogged(which);
                        HandlingModel.Soften(softer, ride * 0.9f);
                        bool softerDive = HandlingModel.DiveCarries(softer, asked, scratch);
                        Check(!HandlingModel.RideCarries(softer, asked, scratch),
                            "vehicle feel: " + name + " is given no more softness than its travel carries: at " + FeelF(ride * 0.9f) + " Hz, a tenth less, "
                            + (softerDive ? "its outer springs would keep less than a third of their travel in the hardest turn it can take" : "its front would keep less than a third of its travel at the full pedal"));
                    }
                    if (which == 2 && kind == CarKind.Saloon && level == HandlingLevel.Full)
                    {
                        auditaken = true;
                        Check(plainWhy == null && bare < 0f && setup.FrontLeft > 0f,
                            "vehicle feel: the Audi R8 as a Saloon at Full, given 0.8 Hz, keeps " + FeelCm(bare) + " cm at the full pedal on the level's dampers (below zero: past its stops); with the ride its travel carries "
                            + "it keeps " + FeelCm(setup.FrontLeft) + " cm");
                    }
                }
            }
        }
        Check(auditaken && held >= designs / 2 && bottomed >= designs / 3,
            "vehicle feel: the log's three cars as any other kind, 6 cm of travel at rest: " + held + " of " + designs + " designs are held off the ride asked, " + bottomed
            + " would have been on or near their stops at the full pedal without the guard");

        // The roll is counted in the hardest turn the car can take, not at 1 g of cornering: the Audi R8 as a Saloon at Full on 1.05 Hz, a ride its dive
        // carries (just stiffer than the 1.005 Hz it was given while the roll was counted at 1 g), keeps a third of its outer springs' travel at 1 g and
        // goes past their stops in the hardest turn its tyres hold. Designed on the ride with nothing held, as the level would have had it.
        CarChassis audi = FeelLogged(2);
        HandlingTarget audiAsked = HandlingRules.Target(CarKind.Saloon, HandlingLevel.Full, FeelNeutral, true);
        HandlingModel.Soften(audi, 1.05f);
        string audiWhy = HandlingModel.Design(audi, HandlingRules.Target(CarKind.Saloon, HandlingLevel.Full, FeelNeutral), unguarded);
        float audiAtOne = HandlingModel.RollLeft(audi, unguarded, 1f);
        float audiAtHold = FeelRollLeft(audi, unguarded);
        FeelSay("  the Audi R8 as a Saloon at Full on 1.05 Hz: roll " + FeelF(unguarded.RollPerG) + " degrees per g, swing " + FeelF(unguarded.Overshoot) + ", tyres hold " + FeelF(unguarded.Hold) + " g; the outer springs keep "
                + FeelF(audiAtOne) + " of their travel at 1 g and " + FeelF(audiAtHold) + " at the hold");
        Check(audiWhy == null && HandlingModel.DiveCarries(audi, audiAsked, scratch) && HandlingModel.RollKept(audi, unguarded, 1f) && audiAtHold < 0f && !HandlingModel.RollCarries(audi, audiAsked, scratch),
            "vehicle feel: the Audi R8 as a Saloon at Full on 1.05 Hz, a ride its dive carries, keeps " + FeelF(audiAtOne) + " of its outer springs' travel at 1 g of cornering but " + FeelF(audiAtHold)
            + " (past their stops) in the hardest turn its tyres hold, " + FeelF(unguarded.Hold) + " g: the ride is held to that turn (HandlingModel.RollCarries), not to 1 g");

        // A sweep of cars of other mods of every kind: weights, centres of mass, travel and brakes (the log's own 2300 N m and a sports car's 12000).
        int cars = 0, softened = 0, kept = 0, wouldBottom = 0, fails = 0, rollHeld = 0;
        string firstAt = null;
        foreach (float mass in new[] { 1300f, 2200f })
        {
            foreach (float com in new[] { 0.2f, 0.5f })
            {
                foreach (float rest in new[] { 0.05f, 0.08f })
                {
                    foreach (float torque in new[] { 2300f, 12000f })
                    {
                        foreach (CarKind kind in kinds)
                        {
                            foreach (HandlingLevel level in levels)
                            {
                                CarChassis stock = FeelOtherHz(mass, com, 3.5f, 6000f, rest, 0.04f, 11000f, torque);
                                CarChassis car = FeelOtherHz(mass, com, 3.5f, 6000f, rest, 0.04f, 11000f, torque);
                                HandlingTarget asked = HandlingRules.Target(kind, level, FeelNeutral, true);
                                string where = kind + " " + mass + " kg, centre of mass " + com + " m, " + FeelCm(rest) + " cm of travel, " + torque + " N m, " + level;
                                string why = HandlingModel.Design(car, asked, setup);
                                cars++;
                                if (why != null)
                                {
                                    Check(false, "vehicle feel: the sweep's " + where + " is designed (" + why + ")");
                                    continue;
                                }
                                float keep = HandlingModel.FrontKeep * setup.Front.RestLength;
                                float rollLeft = FeelRollLeft(car, setup);
                                bool good = setup.Softened
                                    ? setup.FrontLeft >= keep - HandlingModel.TravelSpare && HandlingModel.RideCarries(car, asked, scratch) && rollLeft >= HandlingModel.FrontKeep - 0.01f
                                    : FeelSameSprings(car, stock);
                                good &= HandlingModel.TravelKept(setup, setup.FrontLeft);
                                if (!good)
                                {
                                    fails++;
                                    firstAt = firstAt ?? where + ": softened " + setup.Softened + ", front keeps " + FeelCm(setup.FrontLeft) + " of " + FeelCm(setup.Front.RestLength) + ", the outer springs "
                                              + FeelF(rollLeft) + " of theirs at " + FeelF(setup.Hold) + " g";
                                }
                                softened += setup.Softened ? 1 : 0;
                                kept += !setup.Softened && setup.SoftHeld ? 1 : 0;
                                if (setup.Softened && setup.SoftHeld)
                                {
                                    // Held by the roll: a tenth softer, the front would still keep its third at the full pedal, the outer springs not theirs in the hardest turn.
                                    CarChassis softer = FeelOtherHz(mass, com, 3.5f, 6000f, rest, 0.04f, 11000f, torque);
                                    HandlingModel.Soften(softer, setup.Front.GameFrequency * 0.9f);
                                    rollHeld += HandlingModel.DiveCarries(softer, asked, scratch) && !HandlingModel.RollCarries(softer, asked, scratch) ? 1 : 0;
                                }
                                CarChassis plain = FeelOtherHz(mass, com, 3.5f, 6000f, rest, 0.04f, 11000f, torque);
                                HandlingModel.Soften(plain, asked.RideHz);
                                HandlingTarget plainTarget = HandlingRules.Target(kind, level, FeelNeutral);
                                wouldBottom += HandlingModel.Design(plain, plainTarget, unguarded) == null && FeelBare(plain, plainTarget, unguarded) < keep ? 1 : 0;
                            }
                        }
                    }
                }
            }
        }
        Check(fails == 0 && cars == 192,
            "vehicle feel: every car of the sweep (" + cars + ": kinds, weights, centres of mass, travel, brakes) is softened only as far as its front keeps a third of its travel at the full pedal and its outer springs a third"
            + " of theirs in the hardest turn its tyres hold, or keeps its own springs" + (firstAt != null ? "; " + fails + " did not, the first: " + firstAt : string.Empty));
        FeelSay("  sweep: " + cars + " cars, " + softened + " softened, " + kept + " kept as their mod made them, " + rollHeld + " held by the roll, " + wouldBottom
                + " short of a third of their travel at the ceiling's ride without the guard, " + fails + " failing");
        Check(rollHeld > 0, "vehicle feel: the sweep has cars that the roll holds, not the dive: " + rollHeld + " would keep a third of their front travel at the full pedal a tenth softer, but not of their outer springs' in the hardest turn");

        // A very tall car (a centre of mass 0.9 m up) on weak brakes: it is the roll, not the dive, that 6 cm of travel cannot carry at the ride asked.
        CarChassis tall = FeelOtherHz(2000f, 0.9f, 3.5f, 6000f, 0.06f, 0.05f, 11000f, 1500f);
        HandlingTarget tallAsked = HandlingRules.Target(CarKind.Van, HandlingLevel.Full, FeelNeutral, true);
        string tallWhy = HandlingModel.Design(tall, tallAsked, setup);
        float tallKeep = HandlingModel.FrontKeep * setup.Front.RestLength;
        float tallRide = setup.Front.GameFrequency;
        float tallRoll = FeelRollLeft(tall, setup);
        FeelSay("  a very tall van: rides " + FeelF(tallRide) + " Hz, roll " + FeelF(setup.RollPerG) + " degrees per g, tyres hold " + FeelF(setup.Hold) + " g, front keeps " + FeelCm(setup.FrontLeft) + " cm of "
                + FeelCm(setup.Front.RestLength) + ", the outer springs " + FeelF(tallRoll) + " of theirs");
        Check(tallWhy == null && setup.SoftHeld && setup.FrontLeft >= tallKeep - HandlingModel.TravelSpare
              && (setup.Softened
                  ? HandlingModel.RideCarries(tall, tallAsked, scratch) && tallRoll >= HandlingModel.FrontKeep - 0.01f
                  : FeelSameSprings(tall, FeelOtherHz(2000f, 0.9f, 3.5f, 6000f, 0.06f, 0.05f, 11000f, 1500f))),
            "vehicle feel: a very tall van (centre of mass 0.9 m) is given " + FeelF(tallRide) + " Hz, not the 0.8 asked, and its outer springs keep " + FeelF(tallRoll) + " of their 6 cm in the hardest turn its tyres hold ("
            + FeelF(setup.Hold) + " g, " + FeelF(setup.RollPerG) + " degrees per g)");
        CarChassis tallSofter = FeelOtherHz(2000f, 0.9f, 3.5f, 6000f, 0.06f, 0.05f, 11000f, 1500f);
        HandlingModel.Soften(tallSofter, tallRide * 0.9f);
        Check(HandlingModel.DiveCarries(tallSofter, tallAsked, scratch) && !HandlingModel.RollCarries(tallSofter, tallAsked, scratch),
            "vehicle feel: the very tall van, a tenth softer (" + FeelF(tallRide * 0.9f) + " Hz), would keep a third of its front travel at the full pedal's dive, but its outer springs would not keep a third of theirs "
            + "in the hardest turn it can take: it is the roll that holds it");
        Check(wouldBottom >= cars / 3 && softened >= cars / 4,
            "vehicle feel: the sweep has the failure in it: " + wouldBottom + " of " + cars + " cars would be short of a third of their travel at the ceiling's ride (and " + softened + " are softened, "
            + kept + " kept as their mod made them)");
    }

    // ---------------------------------------------------------------- (2) the front springs' travel at the full pedal, on the game's own cars

    private static void VehicleFeelTravel()
    {
        var cars = new List<(string Name, CarKind Kind, Func<CarChassis> Make)>
        {
            ("Vord V150", CarKind.Utility, FeelPickup), ("Honza Mimic", CarKind.Economy, FeelMimic), ("Vord Tiara Vic", CarKind.Saloon, FeelTiara),
            ("Lytte L6", CarKind.Luxury, FeelLytte), ("Anselmo AF90", CarKind.Sports, FeelSupercar), ("Vord Courier D500", CarKind.Truck, FeelCourier),
            ("Mersaidi Dash", CarKind.Van, FeelDash), ("tall van", CarKind.Van, FeelTallVan), ("heavy truck", CarKind.Truck, FeelHeavyTruck)
        };
        var setup = new HandlingSetup();
        int designs = 0, clamped = 0, firmed = 0;
        float pickupBase = 0f, pickupFirm = 0f, pickupRest = 0f, pickupKept = 0f;
        foreach ((string name, CarKind kind, Func<CarChassis> make) in cars)
        {
            foreach (HandlingTuning tuning in new[] { FeelNeutral, FeelOdd })
            {
                foreach (HandlingLevel level in new[] { HandlingLevel.Light, HandlingLevel.Full, HandlingLevel.Heavy })
                {
                    CarChassis car = make();
                    HandlingTarget target = HandlingRules.Target(kind, level, tuning);
                    string why = HandlingModel.Design(car, target, setup);
                    string at = "the " + name + " at " + level + (tuning.Stamp == FeelNeutral.Stamp ? string.Empty : " with the sliders moved");
                    Check(why == null, "vehicle feel: " + at + " is designed (" + (why ?? "yes") + ")");
                    if (why != null)
                    {
                        continue;
                    }
                    designs++;
                    float keep = HandlingModel.FrontKeep * setup.Front.RestLength;
                    float game = Math.Max(0f, setup.GameDivePerG);
                    clamped += Math.Abs(setup.Front.AntiSquat) >= 0.999f ? 1 : 0;
                    firmed += setup.FrontFirm > 1.001f ? 1 : 0;
                    FeelSay("  " + at + ": front keeps " + FeelCm(setup.FrontLeft) + " of " + FeelCm(setup.Front.RestLength) + " cm (a third " + FeelCm(keep) + ", the game's own car " + FeelCm(setup.GameFrontLeft)
                            + "), dive " + FeelF(setup.DivePerG) + " (game " + FeelF(game) + "), held " + setup.DiveHeld + ", firm " + FeelF(setup.FrontFirm) + ", anti-squat " + FeelF(setup.Front.AntiSquat));
                    // The rule: a third of the travel kept, or what the game's own car keeps if that is less. Not the game's dive angle, which says nothing of the travel.
                    Check(HandlingModel.TravelKept(setup, setup.FrontLeft),
                        "vehicle feel: " + at + ": the full pedal's dive leaves the front springs " + FeelCm(setup.FrontLeft) + " of " + FeelCm(setup.Front.RestLength) + " cm, a third (" + FeelCm(keep)
                        + ") or what the game's own car keeps (" + FeelCm(setup.GameFrontLeft) + ")");
                    // What is stored is the car's own, at the dive it came to (the anti-squat's limits can change the dive the level asked for).
                    float again = HandlingModel.FrontLeftAt(car, target, setup, setup.DivePerG);
                    Check(Math.Abs(again - setup.FrontLeft) < 1e-4f,
                        "vehicle feel: " + at + ": the front travel kept (" + FeelCm(setup.FrontLeft) + " cm) is the travel at the dive the car came to, " + FeelF(setup.DivePerG) + " degrees per g (read again: " + FeelCm(again)
                        + " cm; anti-squat " + FeelF(setup.Front.AntiSquat) + ")");
                    Check(setup.DivePerG >= game - 0.05f && setup.DivePerG <= game * target.DiveScale + 0.05f || Math.Abs(setup.Front.AntiSquat) >= 0.999f,
                        "vehicle feel: " + at + " dives " + FeelF(setup.DivePerG) + " degrees per g, between the game's " + FeelF(game) + " and the level's " + FeelF(game * target.DiveScale));
                    if (name == "Vord V150" && tuning.Stamp == FeelNeutral.Stamp && level != HandlingLevel.Light)
                    {
                        // The pickup the post names: the dive held to the game's own is not enough, the lighter dampers' swing is what is left.
                        float unfirmed = FeelBare(car, target, setup);
                        Check(setup.DiveHeld && setup.DivePerG <= game + 0.1f && unfirmed < keep && setup.FrontFirm > 1.001f && setup.FrontLeft >= keep - HandlingModel.TravelSpare,
                            "vehicle feel: the Vord V150 at " + level + ": the dive held to the game's own (" + FeelF(setup.DivePerG) + " degrees per g) left " + FeelCm(unfirmed) + " cm of " + FeelCm(setup.Front.RestLength)
                            + " with the level's dampers; their compression firmed " + FeelF(setup.FrontFirm) + " times keeps " + FeelCm(setup.FrontLeft) + " cm (a third: " + FeelCm(keep) + ")");
                        if (level == HandlingLevel.Full)
                        {
                            pickupBase = unfirmed;
                            pickupFirm = setup.FrontFirm;
                            pickupRest = setup.Front.RestLength;
                            pickupKept = setup.FrontLeft;
                        }
                    }
                    if (name == "Anselmo AF90")
                    {
                        Check(!setup.DiveHeld && setup.FrontFirm == 1f && Math.Abs(setup.DivePerG - game * target.DiveScale) < 0.03f,
                            "vehicle feel: a sports car with travel to spare dives as the level asks and keeps its dampers at " + level + " (" + FeelF(setup.DivePerG) + ", " + FeelF(game * target.DiveScale) + " asked, firm "
                            + FeelF(setup.FrontFirm) + ")");
                    }
                }
            }
        }
        Check(pickupRest > 0.15f && pickupBase < HandlingModel.FrontKeep * pickupRest && pickupKept >= HandlingModel.FrontKeep * pickupRest - HandlingModel.TravelSpare && pickupKept > pickupBase + 0.01f,
            "vehicle feel: the Vord V150 at Full kept " + FeelCm(pickupBase) + " cm of " + FeelCm(pickupRest) + " at the full pedal with the dive held to the game's own and the level's dampers, and keeps " + FeelCm(pickupKept)
            + " cm now (firmed " + FeelF(pickupFirm) + " times)");
        Check(designs == cars.Count * 6 && clamped >= 1 && firmed >= 2,
            "vehicle feel: " + designs + " designs of the game's own cars and the cars NavCheck assumes, " + clamped + " where the anti-squat's limit binds (the stored figures are read again at the dive the car came to) and "
            + firmed + " where the front dampers were firmed");
    }

    // ---------------------------------------------------------------- (2) and (7) the custom shop's brakes, weight and tyres on the car

    private static void VehicleFeelBuilds()
    {
        var cars = new List<(string Name, CarKind Kind, Func<CarChassis> Make)>
        {
            ("Vord V150", CarKind.Utility, FeelPickup), ("Lytte L6", CarKind.Luxury, FeelLytte), ("Mersaidi Dash", CarKind.Van, FeelDash), ("Honza Mimic", CarKind.Economy, FeelMimic),
            ("Audi R8", CarKind.Sports, () => FeelLogged(2))
        };
        // The custom shop's sport and race brakes (1.25 and 1.5 times the torque), the semi-slick tyres (1.2 times the grip), a lighter car (0.9 times the weight).
        (float Brakes, float Mass, float Grip)[] builds = { (1f, 1f, 1f), (1.25f, 1f, 1f), (1.5f, 1f, 1f), (1.5f, 1f, 1.2f), (1.5f, 0.9f, 1.2f) };
        int checkedBuilds = 0, held = 0;
        float pickupSport = 0f, pickupRace = 0f, pickupRest = 0f, pickupStock = 0f, firmStock = 0f, firmRace = 0f;
        foreach ((string name, CarKind kind, Func<CarChassis> make) in cars)
        {
            foreach (HandlingLevel level in new[] { HandlingLevel.Light, HandlingLevel.Full, HandlingLevel.Heavy })
            {
                HandlingTarget target = HandlingRules.Target(kind, level, FeelNeutral);
                float stockFirm = 1f;
                foreach ((float brakes, float mass, float grip) in builds)
                {
                    CarChassis car = make();
                    car.PartsBrakes = brakes;
                    car.PartsMass = mass;
                    car.PartsGrip = grip;
                    var setup = new HandlingSetup();
                    string why = HandlingModel.Design(car, target, setup);
                    string at = "the " + name + " at " + level + " with brakes x" + FeelF(brakes) + ", weight x" + FeelF(mass) + ", tyres x" + FeelF(grip);
                    Check(why == null, "vehicle feel: " + at + " is designed (" + (why ?? "yes") + ")");
                    if (why != null)
                    {
                        continue;
                    }
                    float decel = HandlingModel.PartsBrakes(car, setup, brakes, mass, grip, out _, out _, out _);
                    float own = HandlingModel.PartsBrakes(car, setup, 1f, mass, grip, out _, out _, out _);
                    float left = HandlingModel.PartsFrontLeft(car, setup, mass, decel);
                    float asked = setup.BrakeTarget * brakes / mass;
                    float leftAsked = HandlingModel.PartsFrontLeft(car, setup, mass, asked);
                    checkedBuilds++;
                    FeelSay("  " + at + ": the full pedal " + FeelF(decel) + " g (the parts ask " + FeelF(asked) + ", the car's own on this weight " + FeelF(own) + "), front keeps " + FeelCm(left) + " of "
                            + FeelCm(setup.Front.RestLength) + " cm (at the ask " + FeelCm(leftAsked) + ", needed " + FeelCm(HandlingModel.TravelNeeded(setup)) + "), firm " + FeelF(setup.FrontFirm));
                    Check(HandlingModel.TravelKept(setup, left),
                        "vehicle feel: " + at + ": the full pedal (" + FeelF(decel) + " g) leaves the front springs " + FeelCm(left) + " of " + FeelCm(setup.Front.RestLength) + " cm, the " + FeelCm(HandlingModel.TravelNeeded(setup))
                        + " they are held to");
                    Check(decel >= own - 1e-4f && decel <= asked + 1e-4f, "vehicle feel: " + at + " brakes at " + FeelF(decel) + " g, no less than the car's own brakes on that weight and tyres (" + FeelF(own) + ") and no more than the parts ask ("
                          + FeelF(asked) + ")");
                    if (asked > own + 0.005f && !HandlingModel.TravelKept(setup, leftAsked))
                    {
                        held++;
                        Check(decel < asked - 0.005f, "vehicle feel: " + at + ": the parts' full ask (" + FeelF(asked) + " g) would leave the front " + FeelCm(leftAsked) + " cm, so the pedal is held to " + FeelF(decel) + " g");
                    }
                    if (brakes == 1f && mass == 1f && grip == 1f)
                    {
                        stockFirm = setup.FrontFirm;
                        var bare = new HandlingSetup();
                        string bareWhy = HandlingModel.Design(make(), target, bare);
                        Check(bareWhy == null && bare.FrontLeft == setup.FrontLeft && bare.FrontFirm == setup.FrontFirm && bare.DivePerG == setup.DivePerG && bare.BrakeG == setup.BrakeG
                              && HandlingModel.PartsBrakes(car, setup, 1f, 1f, 1f, out _, out _, out _) == setup.BrakeG,
                            "vehicle feel: " + at + " is the design of the car with no parts, and the pedal the design gives (" + FeelF(setup.BrakeG) + " g)");
                    }
                    else
                    {
                        Check(setup.FrontFirm >= stockFirm - 1e-4f, "vehicle feel: " + at + ": the front dampers are firmed at least as much (" + FeelF(setup.FrontFirm) + ") as for the car with no parts (" + FeelF(stockFirm) + "), the nose dive planned for the pedal the parts give");
                    }
                    if (name == "Vord V150" && level == HandlingLevel.Full)
                    {
                        pickupRest = setup.Front.RestLength;
                        pickupStock = brakes == 1f && mass == 1f && grip == 1f ? left : pickupStock;
                        pickupSport = brakes == 1.25f ? left : pickupSport;
                        pickupRace = brakes == 1.5f && mass == 1f && grip == 1f ? left : pickupRace;
                        firmStock = brakes == 1f && mass == 1f && grip == 1f ? setup.FrontFirm : firmStock;
                        firmRace = brakes == 1.5f && mass == 1f && grip == 1.2f ? setup.FrontFirm : firmRace;
                    }
                }
            }
        }
        Check(checkedBuilds == cars.Count * 3 * builds.Length && held >= 3,
            "vehicle feel: " + checkedBuilds + " builds of the custom shop's parts on the game's soft cars and a sports car, " + held + " where the parts' full ask would have put the front springs below what they are held to");
        Check(firmStock > 1f && firmRace > firmStock * 1.3f,
            "vehicle feel: the nose dive and the dampers are planned for the full pedal the custom shop's parts give: the Vord V150's compression is firmed " + FeelF(firmStock) + " times with no parts on it and "
            + FeelF(firmRace) + " times with the race brakes and semi-slick tyres");
        Check(pickupRest > 0.15f && pickupStock >= HandlingModel.FrontKeep * pickupRest - HandlingModel.TravelSpare && pickupSport > 0.01f && pickupRace > 0f,
            "vehicle feel: the Vord V150 at Full keeps " + FeelCm(pickupStock) + " cm of " + FeelCm(pickupRest) + " at the stock brakes, " + FeelCm(pickupSport) + " with the sport brakes and " + FeelCm(pickupRace)
            + " with the race brakes, never on its stops (it went to 0.7 and -3.4 once the shop's parts were on)");

        // The parts laid on the car being driven after its handling went on (bought sitting in it, the road turning wet or dry, a save loaded): the
        // handling's pedal and dampers were worked out for no parts, and the sport brakes put 1.25 times its torque on top, unheld (the tyres take
        // that much: at Light, where nothing is held, the pickup brakes at it). Worked out again for the parts (CarTuning, told through
        // GarageBridges.PartsLaid), the pedal is held and the front keeps its third. At Full and Heavy, whose lighter dampers the dive swings on.
        foreach (HandlingLevel level in new[] { HandlingLevel.Full, HandlingLevel.Heavy })
        {
            HandlingTarget target = HandlingRules.Target(CarKind.Utility, level, FeelNeutral);
            CarChassis before = FeelPickup();
            var stale = new HandlingSetup();
            string staleWhy = HandlingModel.Design(before, target, stale);
            float staleLeft = HandlingModel.PartsFrontLeft(before, stale, 1f, stale.BrakeG * 1.25f);
            CarChassis again = FeelPickup();
            again.PartsBrakes = 1.25f;
            var redone = new HandlingSetup();
            string redoneWhy = HandlingModel.Design(again, target, redone);
            float redoneG = HandlingModel.PartsBrakes(again, redone, 1.25f, 1f, 1f, out _, out _, out _);
            float redoneLeft = HandlingModel.PartsFrontLeft(again, redone, 1f, redoneG);
            FeelSay("  the Vord V150 at " + level + ", the sport brakes laid on after the handling: " + FeelF(stale.BrakeG * 1.25f) + " g unheld leaves the front " + FeelCm(staleLeft) + " cm; worked out again, "
                    + FeelF(redoneG) + " g leaves " + FeelCm(redoneLeft) + " cm of " + FeelCm(redone.Front.RestLength));
            Check(staleWhy == null && redoneWhy == null && !HandlingModel.TravelKept(stale, staleLeft) && HandlingModel.TravelKept(redone, redoneLeft)
                  && redoneLeft >= HandlingModel.FrontKeep * redone.Front.RestLength - HandlingModel.TravelSpare,
                "vehicle feel: the Vord V150 at " + level + " with the sport brakes laid on after its handling went on keeps " + FeelCm(staleLeft) + " cm of its front travel at the full pedal ("
                + FeelF(stale.BrakeG * 1.25f) + " g); the handling worked out again for them keeps " + FeelCm(redoneLeft) + " cm (" + FeelF(redoneG) + " g), the third it is held to");
        }
    }

    // ---------------------------------------------------------------- (1) a softened car's brakes: the worst moments and the custom shop's stop

    // NavCheck's limits on the brakes of every car it designs: neither axle asks more than twice its settled share of its tyres at its worst moment as the
    // body dives, and with every Garage build the custom shop's stopping figure is the car's own stop within a metre and no wheel asks more than its tyre holds.
    private const float FeelMostLag = 2f;
    private const float FeelShopSpare = 1f;

    // The BMW M4 G82 of the player's log as a sports car at Heavy was softened to 2.37 Hz with its own dampers left on the softer springs: 28 000 N s/m on a
    // 400 kg rear corner, more than the game's 0.02 s physics step carries, so that corner's load jittered, the design read no damping ratio there (0) and kept
    // them as the car's own. At the full pedal its rear asked 3.498 times its settled share of the tyres, the car stopped 5.572 m further than the custom shop
    // said (42.014 m against 36.442 with the coilovers), and with the sport brakes, semi-slick tyres and coilovers a wheel asked 3836 times what its tyre held.
    // The dampers now go with a softened spring by the root of its factor, keeping the car's own damping (HandlingModel.Soften), and a ride at whose full pedal
    // either axle would ask more than HandlingModel.MostLag times its share is not one the design takes (RideCarries): it softens less, or not at all.
    // NavCheck's sums, on the log's three cars as sports cars at Full and Heavy with the soft ride asked, and every Garage build.
    private static void VehicleFeelBrakes()
    {
        // The building block: the BMW's dampers go with its springs.
        float ceiling = HandlingRules.RideCeiling(CarKind.Sports, HandlingLevel.Full);
        CarChassis stock = FeelLogged(1);
        CarChassis plain = FeelLogged(1);
        HandlingModel.Soften(plain, ceiling);
        float frontScale = plain.Front.OwnFrequency > 0f ? ceiling / plain.Front.OwnFrequency : 0f;
        float rearScale = plain.Rear.OwnFrequency > 0f ? ceiling / plain.Rear.OwnFrequency : 0f;
        Check(frontScale > 0f && rearScale > 0f
              && Math.Abs(plain.Front.Bump - stock.Front.Bump * frontScale) < 1f && Math.Abs(plain.Front.Rebound - stock.Front.Rebound * frontScale) < 1f
              && Math.Abs(plain.Rear.Bump - stock.Rear.Bump * rearScale) < 1f && Math.Abs(plain.Rear.Rebound - stock.Rear.Rebound * rearScale) < 1f,
            "vehicle feel: the BMW M4 G82 softened to " + FeelF(ceiling) + " Hz has its dampers go with its springs by the root of their factor, keeping its own damping: front "
            + FeelF(stock.Front.Bump) + " -> " + FeelF(plain.Front.Bump) + ", rear " + FeelF(stock.Rear.Bump) + " -> " + FeelF(plain.Rear.Bump) + " N s/m (it rode at "
            + FeelF(plain.Front.OwnFrequency) + "/" + FeelF(plain.Rear.OwnFrequency) + " Hz)");

        CarKind kind = CarKind.Sports;
        var setup = new HandlingSetup();
        int designs = 0, softened = 0, brakeBuilds = 0;
        foreach (HandlingLevel level in new[] { HandlingLevel.Full, HandlingLevel.Heavy })
        {
            HandlingTarget asked = HandlingRules.Target(kind, level, FeelNeutral, true);
            for (int which = 0; which < FeelLoggedNames.Length; which++)
            {
                CarChassis car = FeelLogged(which);
                string why = HandlingModel.Design(car, asked, setup);
                string name = "the " + FeelLoggedNames[which] + " as a sports car at " + level;
                designs++;
                Check(why == null, "vehicle feel: " + name + ", the soft ride asked, is designed (" + (why ?? "yes") + ")");
                if (why != null)
                {
                    continue;
                }
                softened += setup.Softened ? 1 : 0;
                name += setup.Softened ? ", softened to " + FeelF(setup.Front.GameFrequency) + " Hz" : ", on its own springs";
                Check(setup.Front.GameZeta > 0f && setup.Rear.GameZeta > 0f,
                    "vehicle feel: " + name + " rides on dampers the physics step carries: a damping ratio of " + FeelF(setup.Front.GameZeta) + " and " + FeelF(setup.Rear.GameZeta)
                    + " read on its springs (none, 0, where a corner's load jitters)");
                Check(setup.FrontLag >= 1f && setup.FrontLag <= FeelMostLag && setup.RearLag >= 1f && setup.RearLag <= FeelMostLag,
                    "vehicle feel: " + name + ": its axles ask " + FeelF(setup.FrontLag) + " and " + FeelF(setup.RearLag) + " times their settled share of the tyres at their worst moment as the body dives ("
                    + FeelF(FeelMostLag) + " at most)");

                // Every Garage build of brakes, weight, tyres and springs, the brakes coming on at either of the game's onsets, stepped wheel by wheel.
                float diffMost = 0f, useMost = 0f;
                string diffAt = null, useAt = null;
                foreach (PartDef spring in FeelPartsOf(PartSlot.Suspension))
                {
                    if (spring != null && spring.UtilityOnly && kind != CarKind.Utility && kind != CarKind.Van)
                    {
                        continue;
                    }
                    foreach (PartDef weight in FeelPartsOf(PartSlot.Weight))
                    {
                        float massFactor = weight != null ? weight.Mass : 1f;
                        var frontCorner = new AxleSetup();
                        var rearCorner = new AxleSetup();
                        FeelBuiltAxle(car.Front, setup.Front, spring != null ? spring.Stiff : 1f, spring != null ? spring.Ride : 0f, massFactor, frontCorner);
                        FeelBuiltAxle(car.Rear, setup.Rear, spring != null ? spring.Stiff : 1f, spring != null ? spring.Ride : 0f, massFactor, rearCorner);
                        foreach (PartDef tyre in FeelPartsOf(PartSlot.Tyres))
                        {
                            float grip = tyre != null ? tyre.Grip : 1f;
                            foreach (float onset in new[] { HandlingModel.GameBrakeOnset, 0.25f })
                            {
                                foreach (PartDef brake in FeelPartsOf(PartSlot.Brakes))
                                {
                                    float brakes = brake != null ? brake.Brake : 1f;
                                    float shop = HandlingModel.ShopStop(car, setup, brakes, massFactor, grip, onset);
                                    float stopped = FeelStop(car, setup, frontCorner, rearCorner, brakes, massFactor, grip, onset, out float worst);
                                    brakeBuilds++;
                                    string build = (brake != null ? brake.Id : "stock brakes") + ", " + (weight != null ? weight.Id : "stock weight") + ", " + (tyre != null ? tyre.Id : "stock tyres")
                                                   + ", " + (spring != null ? spring.Id : "stock springs") + ", onset " + FeelF(onset) + " s: shop " + FeelF(shop) + " m, car " + FeelF(stopped) + " m";
                                    if (Math.Abs(shop - stopped) > diffMost)
                                    {
                                        diffMost = Math.Abs(shop - stopped);
                                        diffAt = build;
                                    }
                                    if (worst > useMost)
                                    {
                                        useMost = worst;
                                        useAt = build;
                                    }
                                }
                            }
                        }
                    }
                }
                FeelSay("  " + name + ": lags " + FeelF(setup.FrontLag) + "/" + FeelF(setup.RearLag) + ", damping read " + FeelF(setup.Front.GameZeta) + "/" + FeelF(setup.Rear.GameZeta) + " -> "
                        + FeelF(setup.Front.Zeta) + "/" + FeelF(setup.Rear.Zeta) + ", shop and car within " + FeelF(diffMost) + " m (" + diffAt + "), a wheel asks " + FeelF(useMost) + " at the most (" + useAt + ")");
                Check(diffMost <= FeelShopSpare, "vehicle feel: " + name + " with every Garage build of brakes, weight, tyres and springs: the custom shop's stopping figure and the car's own stop agree within "
                                                 + FeelF(diffMost) + " m (" + (diffAt ?? "every build the same") + ")");
                Check(useMost <= 1f, "vehicle feel: " + name + " with every Garage build: no wheel asks more than its tyre holds as the body dives (" + FeelF(useMost) + ", " + useAt + ")");
            }
        }
        Check(designs == 6 && softened == designs && brakeBuilds >= designs * 2,
            "vehicle feel: the log's three cars as sports cars at Full and Heavy, the soft ride asked: " + softened + " of " + designs + " softened, " + brakeBuilds + " Garage builds stepped wheel by wheel");

        // The condition is the design's own choice, in the step that tries a ride's roll, and the softened car's dampers are put back with its springs between the rides tried.
        string model = ModSource(@"Driving\HandlingModel.cs");
        Check(model.Contains("public const float MostLag = 2f;") && MemberText(model, "private static int SoftRoll(").Contains("LagsCarried(")
              && MemberText(model, "public static bool RideCarries(").Contains("LagsCarried(") && MemberText(model, "private static void Restock(").Contains("car.Rear.Rebound = stock[9];"),
            "vehicle feel sources: a ride whose full pedal asks either axle more than HandlingModel.MostLag times its share is refused in the design's own steps, and the dampers are put back with the springs between rides");
        string doc = File.Exists(VehicleFeelRoot(@"docs\features\driving.md")) ? File.ReadAllText(VehicleFeelRoot(@"docs\features\driving.md")) : string.Empty;
        Check(doc.Contains("HandlingModel.MostLag"), "vehicle feel docs: docs/features/driving.md says a softened car's brakes are held to HandlingModel.MostLag");
    }

    // A Garage build's springs and weight on an axle of a design, as the Garage puts them on the car (NavCheck's HandlingCheck.BuiltAxle): the spring fitted
    // to its stiffness, ride height and weight, the dampers by the root of stiffness times weight, the force point moved with the body, the bar stiffened.
    private static void FeelBuiltAxle(AxleData game, AxleSetup s, float stiffness, float ride, float massFactor, AxleSetup into)
    {
        float load = s.Load * massFactor;
        HandlingModel.FitSpring(game.Spring.Evaluate, s.SpringForce, s.SpringLength, s.Load, massFactor, stiffness, ride, GarageCatalogue.MaxLower, GarageCatalogue.MaxRaise,
            out into.SpringForce, out into.SpringLength, out float change);
        float damping = (float)Math.Sqrt(stiffness * massFactor);
        into.Bump = s.Bump * damping;
        into.Rebound = s.Rebound * damping;
        into.Application = (s.Application * s.SpringLength + change) / into.SpringLength;
        into.AntiSquat = s.AntiSquat;
        into.AntiRoll = s.AntiRoll * stiffness;
        into.LoadRating = s.LoadRating;
        into.Load = load;
        float settled = HandlingModel.RestCompression(game.Spring, into.SpringForce, load);
        into.Rate = into.SpringForce * game.Spring.Slope(settled) / into.SpringLength;
        into.Sag = settled * into.SpringLength;
        into.RestLength = into.SpringLength - into.Sag;
    }

    // The car braking from 100 km/h as the game's own chain brakes it, wheel by wheel (NavCheck's HandlingCheck.GameStop): the eased pedal at the input's rate,
    // the brakes' onset, each wheel's torque held to what its tyre takes at the load its spring and damper put on it as the body dives, each axle's corner with
    // the build's springs. Returns the metres; worst is the most of what its tyre holds any wheel asked for (past 1 it would lock).
    private static float FeelStop(CarChassis car, HandlingSetup s, AxleSetup frontCorner, AxleSetup rearCorner, float brakes, float massFactor, float grip, float onset,
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
            float frontLoad = FeelCorner(car.Front, frontCorner, frontLength, (frontBefore - frontLength) / step);
            float rearLoad = FeelCorner(car.Rear, rearCorner, rearLength, (rearBefore - rearLength) / step);
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
    private static float FeelCorner(AxleData shape, AxleSetup s, float length, float compressing)
    {
        if (length >= s.SpringLength)
        {
            return 0f;
        }
        return Math.Max(0f, shape.Spring.Evaluate((s.SpringLength - length) / s.SpringLength) * s.SpringForce + HandlingModel.DamperAt(shape, s.Bump, s.Rebound, compressing));
    }

    // No part, then each of the custom shop's parts for a slot.
    private static IEnumerable<PartDef> FeelPartsOf(PartSlot slot)
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

    // ---------------------------------------------------------------- (6) the self-check line's verdict

    private static void VehicleFeelVerdict()
    {
        // The line says FAIL when the front is on its stops: the rule it reads (HandlingModel.TravelKept) on a car of 10 cm of travel.
        var s = new HandlingSetup();
        s.Front.RestLength = 0.10f;
        s.GameFrontLeft = 0.08f;
        Check(!HandlingModel.TravelKept(s, -0.035f) && !HandlingModel.TravelKept(s, 0f) && !HandlingModel.TravelKept(s, 0.02f) && HandlingModel.TravelKept(s, HandlingModel.FrontKeep * 0.10f)
              && HandlingModel.TravelKept(s, 0.06f),
            "vehicle feel verdict: the front springs on or near their stops (-3.5, 0, 2 cm of 10) fail the line, a third kept or more passes");
        s.GameFrontLeft = 0.01f;
        Check(HandlingModel.TravelKept(s, 0.01f) && HandlingModel.TravelKept(s, 0.0095f) && !HandlingModel.TravelKept(s, 0.005f) && HandlingModel.TravelNeeded(s) == 0.01f,
            "vehicle feel verdict: where the game's own car keeps less than a third (1 cm of 10) the handling is held to that, to a millimetre");
        s.GameFrontLeft = 0.5f;
        Check(HandlingModel.TravelNeeded(s) == HandlingModel.FrontKeep * 0.10f, "vehicle feel verdict: where the game's own car keeps more than a third, a third is what is held to");

        // And a real design of the car the first fix left on 4 cm of 16.5: the line reads PASS on the figures now, and would have read FAIL on the old ones.
        CarChassis pickup = FeelPickup();
        var setup = new HandlingSetup();
        HandlingModel.Design(pickup, HandlingRules.Target(CarKind.Utility, HandlingLevel.Full, FeelNeutral), setup);
        HandlingTarget full = HandlingRules.Target(CarKind.Utility, HandlingLevel.Full, FeelNeutral);
        float now = HandlingModel.FrontLeftAt(pickup, full, setup, setup.DivePerG);
        float before = FeelBare(pickup, full, setup);
        Check(HandlingModel.TravelKept(setup, now) && !HandlingModel.TravelKept(setup, before),
            "vehicle feel verdict: the line's rule passes the Vord V150's front travel now (" + FeelCm(now) + " cm of " + FeelCm(setup.Front.RestLength) + ") and fails the " + FeelCm(before) + " cm it kept with the dive held and the level's dampers");
    }

    // ---------------------------------------------------------------- a design a step at a time, softening included

    private static void VehicleFeelSpread()
    {
        // A softened design a step at a time is the whole design, a whole design of another car between every two steps: each step fills the scratch it reads.
        var whole = new HandlingSetup();
        var staged = new HandlingSetup();
        var other = new HandlingSetup();
        var design = new HandlingDesign();
        CarChassis between = FeelSupercar();
        int compared = 0, same = 0, mostSteps = 0;
        double slowest = 0;
        var watch = new Stopwatch();
        foreach (int which in new[] { 0, 1, 2 })
        {
            foreach (CarKind kind in new[] { CarKind.Sports, CarKind.Saloon, CarKind.Utility, CarKind.Truck })
            {
                foreach (HandlingLevel level in new[] { HandlingLevel.Full, HandlingLevel.Heavy })
                {
                    HandlingTarget target = HandlingRules.Target(kind, level, FeelNeutral, true);
                    CarChassis a = FeelLogged(which);
                    CarChassis b = FeelLogged(which);
                    HandlingModel.Design(between, HandlingRules.Target(CarKind.Sports, HandlingLevel.Heavy, FeelNeutral), other);
                    string wholeWhy = HandlingModel.Design(a, target, whole);
                    design.Begin(b, target, staged);
                    int steps = 0;
                    bool more = true;
                    while (more)
                    {
                        HandlingModel.Design(between, HandlingRules.Target(CarKind.Sports, HandlingLevel.Light, FeelNeutral), other);
                        watch.Restart();
                        more = design.Step();
                        watch.Stop();
                        slowest = Math.Max(slowest, watch.Elapsed.TotalMilliseconds);
                        steps++;
                    }
                    compared++;
                    mostSteps = Math.Max(mostSteps, steps);
                    same += wholeWhy == design.Why && FeelSameSprings(a, b) && whole.FrontLeft == staged.FrontLeft && whole.DivePerG == staged.DivePerG && whole.FrontFirm == staged.FrontFirm
                            && whole.Front.Bump == staged.Front.Bump && whole.Front.SpringForce == staged.Front.SpringForce && whole.Softened == staged.Softened && whole.SoftHeld == staged.SoftHeld
                            && whole.Front.AntiSquat == staged.Front.AntiSquat && whole.BrakeG == staged.BrakeG && steps == design.Steps ? 1 : 0;
                }
            }
        }
        Check(compared == 24 && same == compared && mostSteps <= 2 * 8 + 17 + 8,
            "vehicle feel: a design of a car given the ride of its kind, a step at a time, is the whole design, a whole design of another car between every two steps (" + same + " of " + compared
            + ", at most " + mostSteps + " steps: 8, a ride tried in one or two steps (its dive, then its roll) for 16 more, and one for each 5 km/h of the game's hardest turn)");
        FeelSay("  the slowest step of a design with a ride asked: " + slowest.ToString("F3") + " ms, " + mostSteps + " steps at the most");
        Check(slowest < 2.0, "vehicle feel: the slowest step of those designs, a ride tried among them, takes " + slowest.ToString("F3") + " ms here (2 ms at most; NavCheck holds the design's own to 0.6 ms)");
    }

    // ---------------------------------------------------------------- the road turning wet or dry under the car being driven

    // Rain starting or stopping is folded into the stamp the Garage shows each car by (GarageDirector), so GarageCar.Show lays a build's parts on the car
    // being driven again, whatever they carry, and tells Driving (GarageBridges.PartsLaid). Car tuning took that as an option changed: the next frame took
    // the power, drag, eased pedals and handling off and worked the car's sums out again, the car on the game's own figures for the 15 frames or so they
    // take (a softened car of another mod up to 16 design steps more): a jolt in mid bend, under a build of brakes alone too, which carries nothing the
    // weather changes. Now parts laid take nothing off the car (HandlingRules.Frame): what they change is worked out with everything left on, nothing for
    // the parts the handling was made and its brakes shared for, the brakes shared out again for others, and for parts its design was not made for a
    // design made again beside the one on the car and put on in its place in one frame (HandlingRules.Relay, CarHandling.Relaid). Played here on the rule
    // car tuning asks each frame, its flag kept as OnUpdate keeps it, on the custom shop's own builds read as the handling reads them off the car, and on
    // the design's own steps; the sources are tied to them after.
    private static void VehicleFeelRelay()
    {
        Check(HandlingRules.Frame(false, false, false) == TuningFrame.Again && HandlingRules.Frame(false, true, false) == TuningFrame.Again
              && HandlingRules.Frame(false, false, true) == TuningFrame.Again && HandlingRules.Frame(true, false, false) == TuningFrame.Rest
              && HandlingRules.Frame(true, false, true) == TuningFrame.Carry,
            "vehicle feel relay: another car, or an option or slider changed, takes everything of the mod's off the car and begins its sums again; nothing changed, nothing is done; sums under way are carried on");
        Check(HandlingRules.Frame(true, true, false) == TuningFrame.Relay && HandlingRules.Frame(true, true, true) == TuningFrame.Carry && HandlingRules.Frame(false, true, true) == TuningFrame.Again,
            "vehicle feel relay: the Garage's parts laid on the car again are never an option changed: what they change is worked out with everything left on, once any sums under way are done");

        // A drive a frame at a time: got in (its sums some 15 frames), the open road, the rain starting, a slider moved and the rain stopping five frames
        // into its sums, the open road again. Each frame asks what OnUpdate asks, the Garage's word (CarTuning.PartsLaid, a flag set) given before it.
        const int sumsFrames = 15;
        bool laid = false;
        int sumsLeft = 0, offMidDrive = 0, relays = 0;
        string firstOff = null;
        for (int frame = 0; frame < 160; frame++)
        {
            bool changed = frame == 0 || frame == 95;      // got in; a slider moved
            bool rain = frame == 55 || frame == 100;       // the Garage lays the parts again for the rain starting, then stopping
            laid |= rain;
            switch (HandlingRules.Frame(!changed, laid, sumsLeft > 0))
            {
                case TuningFrame.Again:
                    if (!changed)
                    {
                        offMidDrive++;
                        firstOff = firstOff ?? (frame == 55 ? "the rain starting on the open road" : "the rain stopping while a slider's sums were under way");
                    }
                    laid = false;
                    sumsLeft = sumsFrames;
                    break;
                case TuningFrame.Relay:
                    laid = false;
                    relays++;
                    break;
                case TuningFrame.Carry:
                    sumsLeft--;
                    break;
            }
        }
        Check(offMidDrive == 0 && relays == 2 && !laid,
            "vehicle feel relay: a drive with the rain starting on the open road and stopping while a slider's sums are under way takes everything of the mod's off the car " + offMidDrive
            + " time(s) besides getting in and the slider (" + (firstOff ?? "never") + "); what the parts laid change is worked out with everything left on it " + relays
            + " time(s), the second once the sums are done (2 wanted)");

        // The builds of the custom shop as the handling reads them off the car, the road dry and wet (FeelFactors): with no tyres part the weather changes
        // nothing the handling was made or its brakes shared for.
        string[][] builds =
        {
            new[] { "brakes_sport" }, new[] { "brakes_race", "weight_2" }, new[] { "susp_coilover" }, new[] { "brakes_race", "tyres_semi" }, new[] { "tyres_sport" },
            new[] { "tyres_allweather", "brakes_sport" }
        };
        int weatherFree = 0, weatherBound = 0, wrong = 0;
        string firstWrong = null;
        foreach (string[] parts in builds)
        {
            GarageModifiers m = FeelBuild(parts);
            FeelFactors(m, false, true, out float dryBrakes, out float dryMass, out float dryGrip);
            FeelFactors(m, true, true, out float wetBrakes, out float wetMass, out float wetGrip);
            bool same = dryBrakes == wetBrakes && dryMass == wetMass && dryGrip == wetGrip;
            bool free = m.Grip == m.WetGrip;
            weatherFree += free ? 1 : 0;
            weatherBound += free ? 0 : 1;
            if (same != free || Math.Abs(dryBrakes - m.Brakes) > 1e-6f || Math.Abs(dryMass - m.Mass) > 1e-6f || Math.Abs(dryGrip - m.Grip) > 1e-6f || Math.Abs(wetGrip - m.WetGrip) > 1e-6f)
            {
                wrong++;
                firstWrong = firstWrong ?? string.Join(" and ", parts);
            }
        }
        Check(wrong == 0 && weatherFree == 3 && weatherBound == 3,
            "vehicle feel relay: read off the car as the handling reads it, a build with no tyres part (" + weatherFree + " of " + builds.Length + ") is the same wet and dry, one with tyres ("
            + weatherBound + ") differs in its grip alone (" + (firstWrong ?? "each as its parts say") + ")");

        // What the parts laid call for (HandlingRules.Relay): a handling made on a dry road, its design's factors read off the game's car as Begin reads
        // them and its brakes' off the handling's as Finish reads them; then the road turning wet, and the same parts laid again as they were.
        int nothing = 0, redesigned = 0, amiss = 0;
        string firstAmiss = null;
        foreach (string[] parts in builds)
        {
            GarageModifiers m = FeelBuild(parts);
            FeelFactors(m, false, false, out float designBrakes, out float designMass, out float designGrip);
            FeelFactors(m, false, true, out float sharedBrakes, out float sharedMass, out float sharedGrip);
            FeelFactors(m, true, true, out float wetBrakes, out float wetMass, out float wetGrip);
            var made = new CarChassis { PartsBrakes = designBrakes, PartsMass = designMass, PartsGrip = designGrip };
            PartsRelay wet = HandlingRules.Relay(made, sharedBrakes, sharedMass, sharedGrip, wetBrakes, wetMass, wetGrip);
            PartsRelay same = HandlingRules.Relay(made, sharedBrakes, sharedMass, sharedGrip, sharedBrakes, sharedMass, sharedGrip);
            bool free = m.Grip == m.WetGrip;
            nothing += wet == PartsRelay.Nothing ? 1 : 0;
            redesigned += wet == PartsRelay.Design ? 1 : 0;
            if (same != PartsRelay.Nothing || wet != (free ? PartsRelay.Nothing : PartsRelay.Design))
            {
                amiss++;
                firstAmiss = firstAmiss ?? string.Join(" and ", parts) + ": wet " + wet + ", laid again " + same;
            }
        }
        Check(amiss == 0 && nothing == 3 && redesigned == 3,
            "vehicle feel relay: the road turning wet under a build with no tyres part calls for nothing (" + nothing + " of " + builds.Length + ", the brakes alone among them), under one with tyres for the design made again ("
            + redesigned + "); parts laid again as they were call for nothing (" + (firstAmiss ?? "each so") + ")");

        // Brakes bought sitting in the car with the power option off (the options the same, so no sums begun): the design made again. The brakes shared for
        // other parts than the design's (a design made again and refused, its brakes shared for the parts at once): those alone. The parts the design and
        // the brakes are for: nothing. The weight read off the handling's heavier car is the factor read off the game's, to a float's rounding.
        var none = new CarChassis();
        var sport = new CarChassis { PartsBrakes = 1.25f };
        float lighter = HandlingModel.PartsFactor(1400f * 0.9f, 1400f, 1f), heavier = HandlingModel.PartsFactor(1637.5f * 0.9f, 1637.5f, 1f);
        var weighted = new CarChassis { PartsMass = lighter };
        Check(HandlingRules.Relay(none, 1f, 1f, 1f, 1.25f, 1f, 1f) == PartsRelay.Design && HandlingRules.Relay(sport, 1f, 1f, 1f, 1.25f, 1f, 1f) == PartsRelay.Brakes
              && HandlingRules.Relay(sport, 1.25f, 1f, 1f, 1.25f, 1f, 1f) == PartsRelay.Nothing && HandlingRules.Relay(sport, 1.25f, 1f, 1f, 1f, 1f, 1f) == PartsRelay.Design
              && HandlingRules.Relay(weighted, 1f, heavier, 1f, 1f, heavier, 1f) == PartsRelay.Nothing && HandlingRules.Relay(weighted, 1f, heavier, 1f, 1f, 0.95f, 1f) == PartsRelay.Design,
            "vehicle feel relay: brakes bought sitting in the car call for the design made again, brakes shared for other parts than the design's for those alone, the same parts for nothing, and a weight read off the handling's heavier car is the one read off the game's");

        // The design made again beside the one on the car (CarHandling.Relaid), on the design's own steps: from the car as it was read before its first
        // design (CarChassis.CopyFrom; a design softens a car of another mod's springs in place), into a spare chassis and setup (FeelRelaid).
        int relaidCars = 0, relaidSteps = 0, softenedCars = 0;
        string relaidAmiss = null;
        void Relaid(string name, Func<CarChassis> make, HandlingTarget target, float brakesDry, float gripDry, float brakesWet, float gripWet)
        {
            string why = FeelRelaid(make, target, brakesDry, gripDry, brakesWet, gripWet, out int steps, out bool softened);
            relaidCars++;
            relaidSteps = Math.Max(relaidSteps, steps);
            softenedCars += softened ? 1 : 0;
            relaidAmiss = relaidAmiss ?? (why != null ? name + ": " + why : null);
            FeelSay("  " + name + ", the road turning wet: the design made again beside the one on the car in " + steps + " steps" + (why != null ? ": " + why : ""));
        }
        Relaid("the Vord V150 at Full, race brakes and semi-slick tyres", FeelPickup, HandlingRules.Target(CarKind.Utility, HandlingLevel.Full, FeelNeutral), 1.5f, 1.2f, 1.5f, 0.75f);
        Relaid("the Vord V150 at Heavy, sport tyres", FeelPickup, HandlingRules.Target(CarKind.Utility, HandlingLevel.Heavy, FeelNeutral), 1f, 1.1f, 1f, 0.9f);
        for (int which = 0; which < FeelLoggedNames.Length; which++)
        {
            int car = which;
            Relaid("the " + FeelLoggedNames[which] + " softened at Full, semi-slick tyres", () => FeelLogged(car), HandlingRules.Target(CarKind.Sports, HandlingLevel.Full, FeelNeutral, true),
                1f, 1.2f, 1f, 0.75f);
        }
        Check(relaidAmiss == null && relaidCars == 2 + FeelLoggedNames.Length && softenedCars == FeelLoggedNames.Length,
            "vehicle feel relay: on " + relaidCars + " cars (" + softenedCars + " of other mods softened, " + relaidSteps + " steps at the most), the design made again for the road turning wet leaves the one on the car "
            + "untouched through every step, comes out as the design made from scratch for the parts to the bit, with the weight and steering of the one on the car (" + (relaidAmiss ?? "each so") + ")");

        // The spare is made from the car as read: every figure of it carried (each field filled, the copy's own figures and longer curves overwritten).
        var full = new CarChassis();
        var copy = new CarChassis();
        float value = 1f, stale = 500f;
        string unfilled = FeelFill(full, ref value) ?? FeelFill(copy, ref stale) ?? FeelFill(copy, ref stale);
        copy.CopyFrom(full);
        string missed = FeelDiffers(copy, full, "the car");
        Check(unfilled == null && missed == null && value > 50f,
            "vehicle feel relay: CarChassis.CopyFrom carries every figure and curve key of the car as read, " + (int)(value - 1f) + " of them (" + (unfilled != null ? "cannot fill " + unfilled : missed ?? "all") + ")");

        // What the rules and the steps above stand for in the code: the Garage lays the parts as FeelFactors reads them; car tuning hands parts laid to the
        // handling with nothing taken off; the handling reads them as Begin does, and makes the design again into the spare from the car as read.
        string garageCar = MemberText(ModSource(@"Garage\GarageCar.cs"), "private void Handling(");
        Check(garageCar.Contains("float grip = wet ? m.WetGrip : m.Grip;") && garageCar.Contains("wheel.LongitudinalFrictionGrip *= grip;") && garageCar.Contains("nwh.brakes.maxTorque = _brakeTorque * m.Brakes;")
              && garageCar.Contains("body.mass = _mass * m.Mass;"),
            "vehicle feel relay: the Garage lays the tyres' wet or dry grip, the brakes' torque and the weight on the car as the check reads them");
        string tuning = ModSource(@"Driving\CarTuning.cs");
        string update = MemberText(tuning, "private void OnUpdate(");
        int relayAt = update.IndexOf("else if (frame == TuningFrame.Relay)", StringComparison.Ordinal);
        int carryAt = update.IndexOf("Carry(ref slice, began);", StringComparison.Ordinal);
        string relayBranch = relayAt > 0 && carryAt > relayAt ? update.Substring(relayAt, carryAt - relayAt) : string.Empty;
        string relay = MemberText(tuning, "private bool Relay(");
        string carry = MemberText(tuning, "private void Carry(");
        string after = MemberText(tuning, "private Sums AfterHandling(");
        Check(update.Contains("bool same = vehicle == _seen && options == _options && handled == _handled;") && update.Contains("HandlingRules.Frame(same, _partsLaid, _sums != Sums.None)")
              && update.Contains("frame == TuningFrame.Rest") && update.Contains("frame == TuningFrame.Again") && relayBranch.Contains("_partsLaid = false;")
              && relayBranch.Contains("Relay(car, vehicle)") && !relayBranch.Contains("Restore(") && relay.Contains("_sums = Sums.Relaid;") && !relay.Contains("Restore(")
              && !relay.Contains("_handling.Begin(") && carry.Contains("_relaid = _handling.Relaid(_sumsCar);") && after.IndexOf("if (_relay)", StringComparison.Ordinal) >= 0
              && after.IndexOf("if (_relay)", StringComparison.Ordinal) < after.IndexOf("Sums.TuningBegin", StringComparison.Ordinal),
            "vehicle feel relay: car tuning asks the rule each frame, and hands parts laid to the handling with nothing taken off the car, nothing after but the self-check line");
        string handling = ModSource(@"Driving\CarHandling.cs");
        string begin = MemberText(handling, "public bool Begin(");
        string relaid = MemberText(handling, "public PartsRelay Relaid(");
        string finish = MemberText(handling, "public bool Finish(");
        string putOn = MemberText(handling, "private void PutOn(");
        string build = MemberText(handling, "private void Build(");
        int againAt = finish.IndexOf("if (again)", StringComparison.Ordinal);
        int elseAt = againAt > 0 ? finish.IndexOf("else", againAt, StringComparison.Ordinal) : -1;
        string swap = againAt > 0 && elseAt > againAt ? finish.Substring(againAt, elseAt - againAt) : string.Empty;
        Check(begin.IndexOf("_chassis.PartsGrip = ", StringComparison.Ordinal) < begin.IndexOf("_read.CopyFrom(_chassis);", StringComparison.Ordinal)
              && begin.IndexOf("_read.CopyFrom(_chassis);", StringComparison.Ordinal) < begin.IndexOf("_design.Begin(_chassis, target, _setup);", StringComparison.Ordinal)
              && relaid.IndexOf("LiftParts(car);", StringComparison.Ordinal) > 0 && relaid.IndexOf("LiftParts(car);", StringComparison.Ordinal) < relaid.IndexOf("LayParts(car);", StringComparison.Ordinal)
              && relaid.IndexOf("LayParts(car);", StringComparison.Ordinal) < relaid.IndexOf("HandlingRules.Relay(_chassis, _partsBrakes, _partsMass, _partsGrip, brakes, mass, grip)", StringComparison.Ordinal)
              && relaid.Contains("_chassisNext.CopyFrom(_read);") && relaid.Contains("_design.Begin(_chassisNext, _target, _setupNext);") && !relaid.Contains("TakeOff(")
              && !relaid.Contains("Restore(") && !relaid.Contains("PutOn(") && swap.Contains("Exchange();") && swap.Contains("PutOn(vehicle, body, false);") && !swap.Contains("Record(")
              && finish.IndexOf("LiftParts(car);", StringComparison.Ordinal) < againAt && putOn.IndexOf("wheel.FrictionPreset = _tyre;", StringComparison.Ordinal) < putOn.IndexOf("if (!whole)", StringComparison.Ordinal)
              && putOn.IndexOf("if (!whole)", StringComparison.Ordinal) < putOn.IndexOf("groundDetectionPreset = _road", StringComparison.Ordinal)
              && putOn.IndexOf("if (whole)", StringComparison.Ordinal) < putOn.IndexOf("body.mass = _setup.Mass;", StringComparison.Ordinal)
              && build.IndexOf("Tyres(_tyreNext, _tyre, _setupNext);", StringComparison.Ordinal) > 0
              && build.IndexOf("Tyres(_tyreNext, _tyre, _setupNext);", StringComparison.Ordinal) < build.IndexOf("FrontTyre(vehicle)", StringComparison.Ordinal),
            "vehicle feel relay: the handling reads the parts laid as it reads them getting in, makes the design again into the spare from the car as read, and puts it on over the last, its wheels and axles alone, nothing recorded again");
    }

    // A build of the custom shop's parts, by their ids, as the Garage adds them up.
    private static GarageModifiers FeelBuild(string[] parts)
    {
        var build = new GarageBuild();
        foreach (string id in parts)
        {
            PartDef part = GarageCatalogue.Part(id);
            build.Parts[(int)part.Slot] = id;
        }
        return build.Modifiers();
    }

    // The factors the handling reads off a car with a build on it, each the value with the parts on over the value under them (CarHandling), the parts
    // laid as GarageCar.Handling lays them: the brakes' torque times the part, the weight where it has one, the grip times the tyres' wet or dry grip.
    // The values under them are the Vord V150's, as the game made it (CarHandling.Begin reads them so for the design) or with the handling on (Finish
    // and Relaid read them so for the brakes).
    private static void FeelFactors(in GarageModifiers m, bool wet, bool handlingOn, out float brakes, out float mass, out float grip)
    {
        const float torque = 4000f, along = 0.9f;
        float weight = handlingOn ? 1637.5f : 1400f;
        brakes = HandlingModel.PartsFactor(torque * m.Brakes, torque, 1f);
        mass = HandlingModel.PartsFactor(m.Mass != 1f ? weight * m.Mass : weight, weight, 1f);
        grip = HandlingModel.PartsFactor(along * (wet ? m.WetGrip : m.Grip), along, 0.01f);
    }

    // A design made on a dry road and made again for the road turning wet, as CarHandling does both (Begin, then Relaid): the car as read copied before
    // its first design, the design made again from that copy into a spare chassis and setup on the same design's steps. After every step the design on
    // the car is checked against a twin of it made alike; the one made again against the design made from scratch for the wet road, and its weight and
    // steering against the one on the car, which PutOn leaves when it puts a design made again over the last. Null if all holds, else what did not.
    private static string FeelRelaid(Func<CarChassis> make, HandlingTarget target, float brakesDry, float gripDry, float brakesWet, float gripWet, out int steps, out bool softened)
    {
        steps = 0;
        softened = false;
        CarChassis onCar = make();
        CarChassis twin = make();
        onCar.PartsBrakes = twin.PartsBrakes = brakesDry;
        onCar.PartsGrip = twin.PartsGrip = gripDry;
        var read = new CarChassis();
        read.CopyFrom(onCar);   // as CarHandling.Begin keeps it, before the design softens the springs
        var setup = new HandlingSetup();
        var twinSetup = new HandlingSetup();
        var design = new HandlingDesign();
        design.Begin(onCar, target, setup);
        while (design.Step())
        {
        }
        var twinDesign = new HandlingDesign();
        twinDesign.Begin(twin, target, twinSetup);
        while (twinDesign.Step())
        {
        }
        if (design.Why != null || twinDesign.Why != null)
        {
            return "its design was refused (" + (design.Why ?? twinDesign.Why) + ")";
        }
        softened = setup.Softened;
        string alike = FeelDiffers(onCar, twin, "the car") ?? FeelDiffers(setup, twinSetup, "the setup");
        if (alike != null)
        {
            return "two designs of it made alike differ at " + alike;
        }

        var next = new CarChassis();
        next.CopyFrom(read);
        next.PartsBrakes = brakesWet;
        next.PartsGrip = gripWet;
        var setupNext = new HandlingSetup();
        design.Begin(next, target, setupNext);
        bool more = true;
        while (more)
        {
            more = design.Step();
            string touched = FeelDiffers(onCar, twin, "the car") ?? FeelDiffers(setup, twinSetup, "the setup");
            if (touched != null)
            {
                return "step " + design.Steps + " of the design made again changed the one on the car at " + touched;
            }
        }
        steps = design.Steps;
        if (design.Why != null)
        {
            return "the design made again was refused (" + design.Why + ")";
        }
        CarChassis fresh = make();
        fresh.PartsBrakes = brakesWet;
        fresh.PartsGrip = gripWet;
        var freshSetup = new HandlingSetup();
        string freshWhy = HandlingModel.Design(fresh, target, freshSetup);
        string unlike = freshWhy != null ? "made from scratch it was refused (" + freshWhy + ")" : FeelDiffers(setupNext, freshSetup, "the setup") ?? FeelDiffers(next, fresh, "the car");
        if (unlike != null)
        {
            return "the design made again is not the one made from scratch for the parts: " + unlike;
        }
        if (setupNext.Mass != setup.Mass || setupNext.SteerOwn != setup.SteerOwn || FeelDiffers(setupNext.Steer, setup.Steer, "the steering") != null)
        {
            return "the design made again has another weight or steering than the one on the car, which are left on it";
        }
        if (softened)
        {
            // Made from the car as it stands, softened by its first design, the design would not be the car's: the copy as read is what makes it so.
            var standing = new CarChassis();
            standing.CopyFrom(onCar);
            standing.PartsBrakes = brakesWet;
            standing.PartsGrip = gripWet;
            var standingSetup = new HandlingSetup();
            HandlingModel.Design(standing, target, standingSetup);
            if (FeelDiffers(standingSetup, freshSetup, "the setup") == null)
            {
                return "made from the softened car as it stands the design comes out the same, so the copy as read would not be needed";
            }
        }
        return null;
    }

    // Where two of the handling's objects differ, field by field down through the axles and curves (a curve by its keys), or null if nowhere.
    private static string FeelDiffers(object a, object b, string path)
    {
        if (a == null || b == null)
        {
            return a == b ? null : path;
        }
        Type type = a.GetType();
        if (type != b.GetType())
        {
            return path + " (of another type)";
        }
        if (a is CurveKeys curveA)
        {
            var curveB = (CurveKeys)b;
            if (curveA.Count != curveB.Count)
            {
                return path + " (" + curveA.Count + " keys against " + curveB.Count + ")";
            }
            for (int i = 0; i < curveA.Count; i++)
            {
                if (!curveA.Time(i).Equals(curveB.Time(i)) || !curveA.Value(i).Equals(curveB.Value(i)) || !curveA.InSlope(i).Equals(curveB.InSlope(i))
                    || !curveA.OutSlope(i).Equals(curveB.OutSlope(i)))
                {
                    return path + ", key " + i;
                }
            }
            return null;
        }
        if (type.IsPrimitive || type.IsEnum || a is string)
        {
            return a.Equals(b) ? null : path + " (" + a + " against " + b + ")";
        }
        if (a is Array arrayA)
        {
            var arrayB = (Array)b;
            if (arrayA.Length != arrayB.Length)
            {
                return path + " (" + arrayA.Length + " against " + arrayB.Length + ")";
            }
            for (int i = 0; i < arrayA.Length; i++)
            {
                string inner = FeelDiffers(arrayA.GetValue(i), arrayB.GetValue(i), path + "[" + i + "]");
                if (inner != null)
                {
                    return inner;
                }
            }
            return null;
        }
        foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            string inner = FeelDiffers(field.GetValue(a), field.GetValue(b), path + "." + field.Name);
            if (inner != null)
            {
                return inner;
            }
        }
        return null;
    }

    // Every field of a car as read given a figure of its own, down through its axles, each curve three keys more; the first field of a kind it cannot
    // fill named, else null.
    private static string FeelFill(object o, ref float next)
    {
        foreach (FieldInfo field in o.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            Type type = field.FieldType;
            if (type == typeof(float))
            {
                field.SetValue(o, next);
                next += 1f;
            }
            else if (type == typeof(CurveKeys))
            {
                var curve = (CurveKeys)field.GetValue(o);
                for (int k = 0; k < 3; k++)
                {
                    curve.Add(next, next + 0.25f, next + 0.5f, next + 0.75f);
                    next += 1f;
                }
            }
            else if (type == typeof(AxleData))
            {
                string inner = FeelFill(field.GetValue(o), ref next);
                if (inner != null)
                {
                    return inner;
                }
            }
            else
            {
                return o.GetType().Name + "." + field.Name;
            }
        }
        return null;
    }

    // ---------------------------------------------------------------- the sources

    private static string VehicleFeelRoot(string relative)
    {
        return System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..", relative));
    }

    private static void VehicleFeelSources()
    {
        string model = ModSource(@"Driving\HandlingModel.cs");
        string rules = ModSource(@"Driving\HandlingRules.cs");
        string handling = ModSource(@"Driving\CarHandling.cs");
        string tuning = ModSource(@"Driving\CarTuning.cs");
        string catalogue = ModSource(@"Driving\CarCatalogue.cs");
        string garageCar = ModSource(@"Garage\GarageCar.cs");
        string garageState = ModSource(@"Garage\GarageState.cs");
        string links = ModSource(@"Wiring\Wiring.DrivingLinks.cs");
        string settings = ModSource(@"Core\Settings.cs");
        string mod = ModSource(@"Core\RealLifeOverhaulMod.cs");
        string tree = ModSource(@"Meta\OptionsTree.cs");
        string features = ModSource(@"Meta\FeatureList.cs");
        Check(model.Length > 0 && rules.Length > 0 && handling.Length > 0 && tuning.Length > 0 && catalogue.Length > 0 && settings.Length > 0 && mod.Length > 0 && tree.Length > 0 && features.Length > 0
              && garageCar.Length > 0 && garageState.Length > 0 && links.Length > 0,
            "vehicle feel sources: the files are there");

        // What CarHandling.Begin asks is a rule of the game-free code, checked above on its behaviour; here only that Begin asks it so and nothing else softens.
        string begin = MemberText(handling, "public bool Begin(");
        Check(begin.Contains("HandlingRules.Target(spec.Kind, level, tuning, HandlingRules.SoftensCar(type.vehicleTypeName, Settings.HandlingOtherMods))")
              && !handling.Contains("HandlingModel.Soften(") && !begin.Contains("IsGameCar(")
              && begin.IndexOf("Read(car, vehicle, body, type.maxSpeed)", StringComparison.Ordinal) < begin.IndexOf("HandlingRules.SoftensCar(", StringComparison.Ordinal)
              && begin.IndexOf("HandlingRules.SoftensCar(", StringComparison.Ordinal) < begin.IndexOf("_design.Begin(", StringComparison.Ordinal) && !begin.Contains("PutOn("),
            "vehicle feel sources: Begin asks the kind's ride of a car after it is read through the rule (HandlingRules.SoftensCar), the design softens it in its first steps, and nothing of the car is touched");
        Check(begin.Contains("_chassis.PartsBrakes = HandlingModel.PartsFactor(vehicle.brakes.maxTorque, torqueUnder, 1f)") && begin.Contains("_chassis.PartsMass = HandlingModel.PartsFactor(body.mass, massUnder, 1f)")
              && begin.Contains("_chassis.PartsGrip = HandlingModel.PartsFactor(AnyGrip(vehicle), gripUnder, 0.01f)")
              && begin.IndexOf("torqueUnder = vehicle.brakes.maxTorque", StringComparison.Ordinal) < begin.IndexOf("LayParts(car)", StringComparison.Ordinal)
              && begin.IndexOf("LayParts(car)", StringComparison.Ordinal) < begin.IndexOf("_chassis.PartsBrakes", StringComparison.Ordinal)
              && begin.IndexOf("_chassis.PartsGrip", StringComparison.Ordinal) < begin.IndexOf("_design.Begin(", StringComparison.Ordinal),
            "vehicle feel sources: the custom shop's brakes, weight and tyres are read off the car once the parts lie on it again (against the car under them) and handed to the design before it begins");
        string line = MemberText(handling, "private string Line(");
        Check(line.Contains("HandlingModel.PartsFrontLeft(_chassis, _setup, _partsMass, _partsBrakeG)") && line.Contains("!HandlingModel.TravelKept(_setup, left)") && line.Contains("why = \"the front springs keep \"")
              && line.IndexOf("TravelKept(", StringComparison.Ordinal) < line.IndexOf("_line.Clear()", StringComparison.Ordinal) && line.Contains("Softened(gf, gr)") && line.Contains("Travel(left)")
              && MemberText(handling, "private string Travel(").Contains("_setup.FrontFirm") && MemberText(handling, "private string Softened(").Contains("SoftHeld"),
            "vehicle feel sources: the self-check line fails (FAIL, with the figures) when the front springs keep less than they are held to with the parts on the car, and says what was softened, held back and firmed");
        Check(line.Contains("float rollLeft = RollLeft();") && line.Contains("_setup.Softened && rollLeft < HandlingModel.FrontKeep - 0.01f")
              && line.IndexOf("rollLeft < HandlingModel.FrontKeep", StringComparison.Ordinal) < line.IndexOf("_line.Clear()", StringComparison.Ordinal)
              && MemberText(handling, "private float RollLeft(").Contains("HandlingModel.RollLeft(_chassis, _setup, _setup.Hold * _partsGrip)")
              && MemberText(handling, "private string Softened(").Contains("RollLeft()"),
            "vehicle feel sources: the self-check line fails a softened car whose outer springs keep less than a third of their travel in the hardest turn its tyres hold (the custom shop's tyres counted), and says what they keep");
        Check(MemberText(tuning, "private static long HandlingStamp(").Contains("Settings.HandlingOtherMods"),
            "vehicle feel sources: switching the option while driving puts the handling on the car again (it is in the stamp the car is put on by)");
        string pitch = MemberText(model, "private static void Pitch(");
        Check(pitch.Contains("DiveForTravel(") && pitch.Contains("FirmFront(car, s, hold, plan, s.DivePerG / Radian)") && pitch.Contains("Within(") && pitch.IndexOf("DivePerG = hold", StringComparison.Ordinal) < pitch.IndexOf("FirmFront(", StringComparison.Ordinal)
              && MemberText(model, "private static float DivePlan(").Contains("car.PartsBrakes") && MemberText(model, "private static float DivePlan(").Contains("car.PartsGrip")
              && model.Contains("wanted = HeldForTravel(car, s, massFactor, wanted);") && model.Contains("case DesignSoft:") && model.Contains("case DesignSoftRoll:")
              && MemberText(model, "private static int Dived(").Contains("DiveCarried(") && MemberText(model, "private static int SoftRoll(").Contains("RollCarried(")
              && model.Contains("public const float FrontKeep = 0.33f;") && model.Contains("public const float SoftenFrom = 1.1f;"),
            "vehicle feel sources: the nose dive and the dampers are asked of the front springs' travel after the anti-squat's limits, the plan counts the parts on the car, the parts' brakes are held to the travel, and the ride is searched in the design's own steps");
        Check(rules.Contains("public static float RideCeiling(CarKind kind, HandlingLevel level)") && rules.Contains("public float RideHz;") && rules.Contains("public static bool SoftensCar(string typeName, bool option)")
              && catalogue.Contains("public static bool IsGameCar(string typeName)"),
            "vehicle feel sources: the ride asked of a car of another mod is a rule of the handling, game-free");
        Check(settings.Contains("public const string HandlingOtherModsId = \"car_handling_modded\";") && settings.Contains("public static bool HandlingOtherMods = true;")
              && settings.Contains("HandlingOtherMods = Bool(modId, HandlingOtherModsId, HandlingOtherMods);"),
            "vehicle feel sources: the option is saved, on by default");
        Check(mod.Contains("AddToggle(Settings.HandlingOtherModsId, \"reallife_car_handling_modded\", Settings.HandlingOtherMods,") && tree.Contains(".Opts(\"car_handling\", \"car_handling_modded\")")
              && features.Contains("Settings.HandlingOtherModsId"),
            "vehicle feel sources: the option is on the options page under Car handling and is the driving feature's");

        // Only the car the player drives is softened: no other part of the mod asks for it.
        string scripts = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        var users = new List<string>();
        if (Directory.Exists(scripts))
        {
            foreach (string file in Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories))
            {
                string name = System.IO.Path.GetFileName(file);
                if (name != "HandlingModel.cs" && name != "CarHandling.cs" && name != "HandlingRules.cs")
                {
                    string code = File.ReadAllText(file);
                    if (code.Contains("HandlingModel.Soften(") || code.Contains("SoftensCar(") || code.Contains(".RideHz") || code.Contains("PartsBrakes ="))
                    {
                        users.Add(name);
                    }
                }
            }
        }
        Check(users.Count == 0, "vehicle feel sources: only the driven car's handling softens a car" + (users.Count > 0 ? " (also in " + string.Join(", ", users) + ")" : string.Empty));

        // The Garage's parts laid on the car being driven, or taken off, by the Garage itself: Driving is told through the Garage's bridge (wired in Wiring), and
        // what they change is worked out with everything left on the car (VehicleFeelRelay). Not when Driving lifts and lays them itself around its own changes.
        string show = MemberText(garageCar, "public void Show(");
        Check(show.Contains("Handling(m, wet);") && show.Contains("laid = true;") && show.Contains("laid = _handling;") && show.Contains("laid |= Cleared();")
              && show.IndexOf("Handling(m, wet);", StringComparison.Ordinal) < show.IndexOf("laid = true;", StringComparison.Ordinal)
              && show.IndexOf("if (laid)", StringComparison.Ordinal) > show.IndexOf("catch (Exception e)", StringComparison.Ordinal)
              && MemberText(garageCar, "public void Clear(").Contains("if (Cleared())") && MemberText(garageCar, "private bool Cleared(").Contains("bool had = _handling;")
              && MemberText(garageCar, "private void Laid(").Contains("GarageBridges.PartsLaid(_vehicle)")
              && !MemberText(garageCar, "public bool LiftHandling(").Contains("Laid()") && !MemberText(garageCar, "public void LayHandling(").Contains("Laid()")
              && !MemberText(garageCar, "private void Handling(").Contains("Laid()") && !MemberText(garageCar, "private void ClearHandling(").Contains("Laid()"),
            "vehicle feel sources: the Garage tells Driving when it lays its handling parts on a car or takes them off (a build put on or off, the road wet or dry, the shop's preview), never as Driving lifts and lays them");
        Check(garageState.Contains("public static Action<VehicleController> PartsLaid;") && links.Contains("GarageBridges.PartsLaid = PartsLaid;") && links.Contains("GarageBridges.PartsLaid = null;")
              && links.Contains("CarTuning.PartsLaid(vehicle);") && links.Contains("Fault(\"DrivingLinks.PartsLaid\", e);"),
            "vehicle feel sources: the Garage's bridge for it is filled in by Wiring.DrivingLinks and emptied when the links are undone, a fault in it caught");
        string update = MemberText(tuning, "private void OnUpdate(");
        string partsLaid = MemberText(tuning, "public static void PartsLaid(");
        Check(update.Contains("HandlingRules.Frame(same, _partsLaid, _sums != Sums.None)") && update.Contains("_partsLaid = false;")
              && update.IndexOf("_partsLaid = false;", StringComparison.Ordinal) < update.IndexOf("Begin(car, vehicle, options, handled);", StringComparison.Ordinal)
              && update.IndexOf("Restore();", StringComparison.Ordinal) < update.IndexOf("_partsLaid = false;", StringComparison.Ordinal)
              && partsLaid.Contains("car.vehicleController == tuning._seen") && partsLaid.Contains("tuning._partsLaid = true;") && !partsLaid.Contains("Restore(") && !partsLaid.Contains("Begin("),
            "vehicle feel sources: told the parts on the car being driven changed, car tuning sets a flag and nothing else, works out what they change from the next frame with everything left on, and drops the flag when another car or option begins everything again");

        string doc = File.Exists(VehicleFeelRoot(@"docs\features\driving.md")) ? File.ReadAllText(VehicleFeelRoot(@"docs\features\driving.md")) : string.Empty;
        Check(doc.Contains("**Cars of other mods.**") && doc.Contains("HandlingModel.Soften") && doc.Contains("HandlingModel.RideCarries") && doc.Contains("car_handling_modded") && doc.Contains("CarCatalogue.IsGameCar")
              && doc.Contains("**The front springs' travel at the full pedal.**") && doc.Contains("HandlingModel.DiveForTravel") && doc.Contains("HandlingModel.FirmFront") && doc.Contains("HandlingModel.HeldForTravel")
              && doc.Contains("HandlingModel.FrontKeep") && doc.Contains("HandlingModel.RollCarries") && doc.Contains("GarageBridges.PartsLaid") && !doc.Contains("HandlingModel.RollG"),
            "vehicle feel docs: docs/features/driving.md says what is done for the cars of other mods and for the front springs' travel");
        string garage = File.Exists(VehicleFeelRoot(@"docs\features\garage.md")) ? File.ReadAllText(VehicleFeelRoot(@"docs\features\garage.md")) : string.Empty;
        Check(garage.Contains("HandlingModel.HeldForTravel") && garage.Contains("GarageBridges.PartsLaid") && !garage.Contains("can bring the front to its stops"),
            "vehicle feel docs: docs/features/garage.md says the brakes part is held to the front springs' travel, no longer that it can put them on their stops");
    }

    // ---------------------------------------------------------------- the words

    private static void VehicleFeelWords()
    {
        Dictionary<string, string> en = LocaleFile("en");
        Check(en.TryGetValue("reallife_car_handling_modded", out string label) && label.Length > 0 && label.Length <= 60 && label.Split(' ').Length <= 9 && !label.EndsWith("."),
            "vehicle feel words: the option has a label in English that fits the page (at most 9 words, 60 characters, no full stop)");
        Check(en.TryGetValue("reallife_opt_tip_car_handling_modded", out string tip) && tip.Length > 0 && tip.Length <= 190 && tip.Split(' ').Length <= 32 && tip.EndsWith("."),
            "vehicle feel words: the option has its one sentence in English (at most 32 words and 190 characters, as the options page holds every tip to, and a full stop)");
        Check(tip != null && tip.Contains("as far as its springs' travel allows"), "vehicle feel words: the option's tip says that the ride is given as far as the springs' travel allows");
        // The soft ride is asked only at Full and Heavy (HandlingRules.RideCeiling, 0 at every other level for every kind): the tip and What's new say so.
        bool levels = true;
        foreach (CarKind kind in Enum.GetValues(typeof(CarKind)))
        {
            levels &= HandlingRules.RideCeiling(kind, HandlingLevel.Light) == 0f && HandlingRules.RideCeiling(kind, HandlingLevel.Off) == 0f;
        }
        levels &= HandlingRules.RideCeiling(CarKind.Sports, HandlingLevel.Full) > 0f && HandlingRules.RideCeiling(CarKind.Sports, HandlingLevel.Heavy) > 0f;
        Check(levels && tip != null && tip.Contains("only at Full or Heavy car handling, never at Light"),
            "vehicle feel words: the option's tip says plainly that the soft ride comes only at Full or Heavy car handling and never at Light, as the rule asks it");
        // What's new: looked for in every row, by its English line, so that it is found before a release and after it numbers the row.
        string soft = "Cars from other mods that ride on springs far stiffer than the game's now get the soft ride, body roll and nose dive of the game's cars of their kind when car handling is at Full or Heavy, "
                      + "never at Light, as far as the travel of their springs allows, and the new option Soft ride for other mods' cars, on by default, switches that off.";
        string fix = "Fixed: braking hard from speed dipped the nose of the Vord V150 pickup and other softly sprung cars so far that the front springs nearly reached their stops and the car jolted; "
                     + "where the springs have little travel the nose now dips no further than the game's own, their front dampers firm up, and the custom shop's brakes add stopping power only as far as the front springs keep their travel.";
        Check(WhatsNewHas(en, soft) && WhatsNewHas(en, fix), "vehicle feel words: What's new has the soft ride for other mods' cars and the fix of the pickup's braking (any row)");
        bool plain = true;
        foreach (string text in new[] { label ?? string.Empty, tip ?? string.Empty, soft, fix })
        {
            plain &= text.IndexOf('–') < 0 && text.IndexOf('—') < 0 && text.IndexOf('!') < 0 && !text.Contains(" - ") && !text.Contains("--");
        }
        Check(plain, "vehicle feel words: no dash and no exclamation mark");
        // The eleven languages carry the changed lines: the same keys, and each What's new line longer than the old one by the clause it gained.
        foreach (string code in new[] { "de", "es", "fr", "ko", "nl", "pl", "pt", "ru", "tr", "zh-cn" })
        {
            Dictionary<string, string> words = LocaleFile(code);
            bool has = words.TryGetValue("reallife_car_handling_modded", out string l) && l.Length > 0 && words.TryGetValue("reallife_opt_tip_car_handling_modded", out string t) && t.Length > 0
                       && words.TryGetValue("reallife_changes_next", out string next) && next.Contains("Vord V150") && next.Length > 0;
            Check(has, "vehicle feel words: " + code + " has the option's label and tip and the What's new row that names the pickup");
        }
    }
}
