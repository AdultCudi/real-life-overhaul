using System;
using System.Collections.Generic;
using System.IO;
using RealLifeOverhaul;

// The sweep of 12 Nov 2026, two players' posts about the driven car's feel.
//
// (1) "The physics added by the mod could be compatible with other vehicle mods as well ... it makes other modded vehicles seem stiff."
//     A player's Player.log has the handling running on a Ferrari SF90 Spider, a BMW M4 G82 and an Audi R8 of other mods, and PASSING: it
//     read each car's own values, as it always does. But those cars ride at 3.98 to 4.65 Hz on springs of 237 000 to 279 000 N/m (the
//     game's own cars at 0.76 to 2.88 Hz) and lean 0.12 to 0.2 degrees per g (the game's soft cars 4), so the wobble the handling adds had
//     nothing to show on, and a 10 cm kerb bottomed their springs out. A car whose type is not the game's and rides stiffer than the game's
//     cars of its kind is now given their ride before its design (HandlingModel.Soften, the option "Soft ride for other mods' cars").
//
// (2) "Better brakes, there are some bugs while going fast and suddenly braking (especially the Ford pick up)." The handling's dampers are
//     lighter than the game's, so the body's first swing in a full-pedal dive goes about 40% further than the settled dive, and nothing
//     counted what that left of the front springs' travel: the Vord V150 kept 1.7 cm of 16.5 at the stock brakes (the game's own car 7.4) and
//     none with the custom shop's. The nose dive is now held back, never below the game's own, until a third of the travel is left
//     (HandlingModel.DiveForTravel). The cars are built from the figures of the game's files as NavCheck's HandlingCheck has them.
//
// Offline: run with --vehicle-feel.
internal static partial class Program
{
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

    private static readonly HandlingTuning FeelNeutral = HandlingTuning.Neutral;

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

    // A car of another mod as the player's log read three of them: the springs' rates and the anti-roll bars as logged (Ferrari SF90 Spider 273 333 N/m and 7 200,
    // BMW M4 G82 278 571 and 264 286 and 8 400, Audi R8 253 333 and 237 500 and 11 000), their forces and lengths worked from round numbers, the weight, wheelbase,
    // tyres and steering those of a sports car, the dampers firm enough to be well over the damping ratio of 0.55 on springs that stiff. They stand with 12 to 13 cm of
    // travel at rest; the log's own cars stand with about 6 (it says a 10 cm kerb leaves -4 cm), and softening keeps the travel, so it is as short after as before.
    private static CarChassis FeelModded(int which)
    {
        float[] mass = { 1670f, 1775f, 1595f };
        float[] frontForce = { 41000f, 39000f, 38000f };
        float[] frontLength = { 0.15f, 0.14f, 0.15f };
        float[] rearForce = { 41000f, 37000f, 38000f };
        float[] rearLength = { 0.15f, 0.14f, 0.16f };
        float[] bars = { 7200f, 8400f, 11000f };
        CarChassis car = FeelCar(mass[which], 0.125f, 0f, 12000f, 35f, 340f, FeelSteerCars);
        FeelAxle(car.Front, 1.2f, 1.62f, 0.45f, 0.34f, frontForce[which], frontLength[which], 0.2f, 14000f, bars[which]);
        FeelAxle(car.Rear, -1.45f, 1.62f, 0.45f, 0.34f, rearForce[which], rearLength[which], 0.2f, 14000f, bars[which]);
        car.Rear.SideGrip = 1.5f;
        car.Rear.AlongGrip = 1.5f;
        car.Rear.SideStiffness = 1.7f;
        car.Rear.BrakeShare = 0.55f;
        return car;
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

    private static void VehicleFeelAll()
    {
        VehicleFeelRules();
        VehicleFeelSoftRide();
        VehicleFeelTravel();
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
    }

    // ---------------------------------------------------------------- (1) the soft ride

    private static void VehicleFeelSoftRide()
    {
        string[] names = { "Ferrari SF90 Spider", "BMW M4 G82", "Audi R8" };
        float ceiling = HandlingRules.RideCeiling(CarKind.Sports, HandlingLevel.Full);
        var stiffSetup = new HandlingSetup();
        var softSetup = new HandlingSetup();
        var lightSetup = new HandlingSetup();
        for (int which = 0; which < names.Length; which++)
        {
            string name = names[which];
            CarChassis stiff = FeelModded(which);
            CarChassis soft = FeelModded(which);
            FeelLoads(stiff, out float loadFront, out float loadRear);
            float ownFront = FeelRide(stiff.Front, loadFront);
            float ownRear = FeelRide(stiff.Rear, loadRear);
            Check(ownFront >= 3.5f && ownFront <= 5f && ownRear >= 3.5f && ownRear <= 5f,
                "vehicle feel: the " + name + " rides at " + FeelF(ownFront) + "/" + FeelF(ownRear) + " Hz as the player's log read it (3.98 to 4.65), the game's own cars at 0.76 to 2.88");

            float restFront = FeelRest(stiff.Front, loadFront);
            float restRear = FeelRest(stiff.Rear, loadRear);
            float pointFront = stiff.Front.Application * stiff.Front.SpringLength;
            bool changed = HandlingModel.Soften(soft, ceiling);
            Check(changed, "vehicle feel: the " + name + ", stiffer than any sports car of the game's, is softened");
            Check(Math.Abs(FeelRide(soft.Front, loadFront) - ceiling) < 0.03f && Math.Abs(FeelRide(soft.Rear, loadRear) - ceiling) < 0.03f,
                "vehicle feel: the " + name + " now rides at " + FeelF(FeelRide(soft.Front, loadFront)) + "/" + FeelF(FeelRide(soft.Rear, loadRear)) + " Hz, the ceiling's " + FeelF(ceiling));
            Check(Math.Abs(FeelRest(soft.Front, loadFront) - restFront) < 1e-4f && Math.Abs(FeelRest(soft.Rear, loadRear) - restRear) < 1e-4f,
                "vehicle feel: the " + name + " stands as high as it did: the same travel left to compress at rest (" + FeelF(restFront * 100f) + " cm), the spring longer ("
                + FeelF(stiff.Front.SpringLength * 100f) + " -> " + FeelF(soft.Front.SpringLength * 100f) + " cm)");
            Check(Math.Abs(soft.Front.Application * soft.Front.SpringLength - pointFront) < 1e-4f,
                "vehicle feel: the " + name + "'s force points stay as high above the road as they were");
            Check(Math.Abs(soft.Front.OwnFrequency - ownFront) < 0.01f && Math.Abs(soft.Rear.OwnFrequency - ownRear) < 0.01f && stiff.Front.OwnFrequency == 0f,
                "vehicle feel: the " + name + " is told what it rode at before, for the self-check line");
            CarChassis again = FeelModded(which);
            HandlingModel.Soften(again, ceiling);
            Check(!HandlingModel.Soften(again, ceiling) && again.Front.OwnFrequency == 0f, "vehicle feel: a car already at the ride asked is left as it is");

            CarChassis off = FeelModded(which);
            Check(!HandlingModel.Soften(off, 0f) && off.Front.SpringForce == stiff.Front.SpringForce && off.Front.SpringLength == stiff.Front.SpringLength
                  && off.Rear.SpringForce == stiff.Rear.SpringForce && off.Rear.SpringLength == stiff.Rear.SpringLength,
                "vehicle feel: with no ride asked (Light, the option off, a car of the game's own) the " + name + " keeps the springs its mod gave it");
            CarChassis light = FeelModded(which);
            Check(!HandlingModel.Soften(light, HandlingRules.RideCeiling(CarKind.Sports, HandlingLevel.Light)) && light.Front.SpringForce == stiff.Front.SpringForce,
                "vehicle feel: at Light the " + name + " keeps its springs");

            // The design on the stiff car and on the softened one.
            HandlingTarget target = HandlingRules.Target(CarKind.Sports, HandlingLevel.Full, FeelNeutral, true);
            string stiffWhy = HandlingModel.Design(stiff, HandlingRules.Target(CarKind.Sports, HandlingLevel.Full, FeelNeutral), stiffSetup);
            string softWhy = HandlingModel.Design(soft, target, softSetup);
            Check(stiffWhy == null && softWhy == null, "vehicle feel: the " + name + " is designed stiff and softened (" + (stiffWhy ?? softWhy ?? "yes") + ")");
            if (stiffWhy != null || softWhy != null)
            {
                continue;
            }
            Check(softSetup.Front.Frequency >= ceiling - 0.05f && softSetup.Front.Frequency <= ceiling + 0.05f && stiffSetup.Front.Frequency >= 3.5f,
                "vehicle feel: the " + name + "'s ride in the finished design is " + FeelF(stiffSetup.Front.Frequency) + " Hz stiff and " + FeelF(softSetup.Front.Frequency) + " softened");
            Check(Math.Abs(softSetup.Front.Zeta - 0.55f) <= 0.06f && Math.Abs(softSetup.Rear.Zeta - 0.55f) <= 0.06f,
                "vehicle feel: the " + name + "'s dampers are set for a damping ratio of 0.55 on the softer springs (" + FeelF(softSetup.Front.Zeta) + "/" + FeelF(softSetup.Rear.Zeta) + ")");
            Check(Math.Abs(softSetup.Front.RestLength - stiffSetup.Front.RestLength) < 1e-3f && Math.Abs(softSetup.ComHeight - stiffSetup.ComHeight) < 1e-3f,
                "vehicle feel: the " + name + " stands where it stood: " + FeelF(softSetup.Front.RestLength * 100f) + " cm of travel at rest, the centre of mass " + FeelF(softSetup.ComHeight) + " m up");
            Check(softSetup.RollPerG > stiffSetup.RollPerG * 1.5f && softSetup.DivePerG > stiffSetup.DivePerG * 1.5f,
                "vehicle feel: the " + name + " now leans " + FeelF(softSetup.RollPerG) + " degrees per g (" + FeelF(stiffSetup.RollPerG) + " stiff) and dips its nose "
                + FeelF(softSetup.DivePerG) + " (" + FeelF(stiffSetup.DivePerG) + "): the wobble has something to show on");
            float kerb = HandlingModel.KerbLeft(soft.Front, softSetup.Front.SpringForce, softSetup.Front.SpringLength, softSetup.Front.Bump, softSetup.Front.Rebound,
                softSetup.Front.Load / HandlingModel.G, HandlingModel.Kerb);
            float kerbStiff = HandlingModel.KerbLeft(stiff.Front, stiffSetup.Front.SpringForce, stiffSetup.Front.SpringLength, stiffSetup.Front.Bump, stiffSetup.Front.Rebound,
                stiffSetup.Front.Load / HandlingModel.G, HandlingModel.Kerb);
            Check(kerb > 0.01f && Math.Abs(kerb - kerbStiff) < 0.002f,
                "vehicle feel: a " + FeelF(HandlingModel.Kerb * 100f) + " cm kerb leaves the softened " + name + " " + FeelF(kerb * 100f) + " cm of travel, as it left the stiff car (" + FeelF(kerbStiff * 100f)
                + " cm): the softer spring takes none of the travel");

            // The same car at Light is the stiff car's design, bit for bit in the figures that count.
            string lightWhy = HandlingModel.Design(light, HandlingRules.Target(CarKind.Sports, HandlingLevel.Light, FeelNeutral, true), lightSetup);
            string lightStiff = HandlingModel.Design(FeelModded(which), HandlingRules.Target(CarKind.Sports, HandlingLevel.Light, FeelNeutral), stiffSetup);
            Check(lightWhy == null && lightStiff == null && lightSetup.Front.SpringForce == stiffSetup.Front.SpringForce && lightSetup.Front.Frequency == stiffSetup.Front.Frequency,
                "vehicle feel: at Light the " + name + " has the design of its own springs");
        }

        // The game's own softest cars are left as they are (a ride within a tenth of the ceiling is not a change anyone sees).
        CarChassis supercar = FeelSupercar();
        FeelLoads(supercar, out float superFront, out _);
        CarChassis mimic = FeelMimic();
        FeelLoads(mimic, out float mimicFront, out _);
        Check(!HandlingModel.Soften(supercar, ceiling) && supercar.Front.OwnFrequency == 0f && !HandlingModel.Soften(mimic, HandlingRules.SoftRide) && mimic.Front.OwnFrequency == 0f,
            "vehicle feel: a car riding at the ceiling or within a tenth of it is left as it is (the Anselmo AF90 " + FeelF(FeelRide(supercar.Front, superFront)) + " Hz, the Honza Mimic "
            + FeelF(FeelRide(mimic.Front, mimicFront)) + ")");
        var odd = new CarChassis { Mass = 50f };
        Check(!HandlingModel.Soften(odd, ceiling), "vehicle feel: a chassis that is no car is not softened");
    }

    // ---------------------------------------------------------------- (2) the front springs' travel at the full pedal

    private static void VehicleFeelTravel()
    {
        var cars = new List<(string Name, CarKind Kind, CarChassis Car)>
        {
            ("Vord V150", CarKind.Utility, FeelPickup()), ("Honza Mimic", CarKind.Economy, FeelMimic()), ("Vord Tiara Vic", CarKind.Saloon, FeelTiara()),
            ("Lytte L6", CarKind.Luxury, FeelLytte()), ("Anselmo AF90", CarKind.Sports, FeelSupercar()), ("Vord Courier D500", CarKind.Truck, FeelCourier())
        };
        var setup = new HandlingSetup();
        float pickupOld = 0f, pickupNew = 0f, pickupRest = 0f;
        foreach ((string name, CarKind kind, CarChassis car) in cars)
        {
            foreach (HandlingLevel level in new[] { HandlingLevel.Light, HandlingLevel.Full, HandlingLevel.Heavy })
            {
                HandlingTarget target = HandlingRules.Target(kind, level, FeelNeutral);
                string why = HandlingModel.Design(car, target, setup);
                Check(why == null, "vehicle feel: the " + name + " is designed at " + level + " (" + (why ?? "yes") + ")");
                if (why != null)
                {
                    continue;
                }
                float keep = HandlingModel.FrontKeep * setup.Front.RestLength;
                float game = Math.Max(0f, setup.GameDivePerG);
                Check(setup.FrontLeft >= keep - 1e-4f || setup.DivePerG <= game + 0.05f,
                    "vehicle feel: the " + name + " at " + level + ": the full pedal's dive leaves the front springs " + FeelF(setup.FrontLeft * 100f) + " of " + FeelF(setup.Front.RestLength * 100f)
                    + " cm, a third (" + FeelF(keep * 100f) + ") or the dive is the game's own (" + FeelF(setup.DivePerG) + " against " + FeelF(game) + ")");
                Check(setup.DivePerG >= game - 0.05f && setup.DivePerG <= game * target.DiveScale + 0.05f,
                    "vehicle feel: the " + name + " at " + level + " dives " + FeelF(setup.DivePerG) + " degrees per g, between the game's " + FeelF(game) + " and the level's " + FeelF(game * target.DiveScale));
                if (name == "Vord V150" && level == HandlingLevel.Full)
                {
                    pickupRest = setup.Front.RestLength;
                    pickupNew = setup.FrontLeft;
                    pickupOld = HandlingModel.FrontLeftAt(car, target, setup, game * target.DiveScale);
                    Check(setup.DiveHeld && setup.DivePerG <= game + 0.1f,
                        "vehicle feel: the pickup's extra dive is held back to the game's own, " + FeelF(setup.DivePerG) + " degrees per g against the game's " + FeelF(game));
                }
                if (name == "Anselmo AF90")
                {
                    Check(!setup.DiveHeld && Math.Abs(setup.DivePerG - game * target.DiveScale) < 0.03f,
                        "vehicle feel: a sports car with travel to spare dives as the level asks at " + level + " (" + FeelF(setup.DivePerG) + ", " + FeelF(game * target.DiveScale) + " asked)");
                }
            }
        }

        // The pickup, as the model had it before: 2.37 degrees per g at the full pedal left its front springs under 3 cm, a fifth of what the game's own car keeps.
        Check(pickupRest > 0.15f && pickupOld > -0.05f && pickupOld < 0.03f && pickupOld < HandlingModel.FrontKeep * pickupRest,
            "vehicle feel: the Vord V150 with the dive it had (the game's times 1.2) kept " + FeelF(pickupOld * 100f) + " cm of " + FeelF(pickupRest * 100f) + " at the full pedal: a bump on the way was its stops");
        Check(pickupNew > 0.03f && pickupNew > pickupOld + 0.01f,
            "vehicle feel: the Vord V150 now keeps " + FeelF(pickupNew * 100f) + " cm of " + FeelF(pickupRest * 100f) + ", " + FeelF((pickupNew - pickupOld) * 100f) + " cm more");

        // A design a step at a time is the whole design, a whole design of another car between two steps: the guard fills the scratch it reads in its own step.
        // The brake's ramp it reads is left by whatever designed last, so the whole design is run just after a Heavy design (the brakes rise in 0.4 s) and each step
        // of the staged one just after a Light design (0.15 s), the pickup's own Full 0.25: a step that read what an earlier one left would dive differently.
        CarChassis pickup = FeelPickup();
        HandlingTarget full = HandlingRules.Target(CarKind.Utility, HandlingLevel.Full, FeelNeutral);
        var whole = new HandlingSetup();
        var staged = new HandlingSetup();
        var other = new HandlingSetup();
        var design = new HandlingDesign();
        CarChassis between = FeelSupercar();
        HandlingModel.Design(between, HandlingRules.Target(CarKind.Sports, HandlingLevel.Heavy, FeelNeutral), other);
        string wholeWhy = HandlingModel.Design(pickup, full, whole);
        design.Begin(pickup, full, staged);
        int steps = 0;
        while (design.Step())
        {
            HandlingModel.Design(between, HandlingRules.Target(CarKind.Sports, HandlingLevel.Light, FeelNeutral), other);
            steps++;
        }
        Check(wholeWhy == null && design.Why == null && staged.DivePerG == whole.DivePerG && staged.FrontLeft == whole.FrontLeft && staged.DiveHeld == whole.DiveHeld
              && staged.Front.AntiSquat == whole.Front.AntiSquat && staged.Front.BrakeShare == whole.Front.BrakeShare && steps >= 7,
            "vehicle feel: the pickup's design a step at a time (" + (steps + 1) + " steps), another car's design between every two, is the whole design");
        var twice = new HandlingSetup();
        HandlingModel.Design(pickup, full, twice);
        Check(twice.DivePerG == whole.DivePerG && twice.FrontLeft == whole.FrontLeft, "vehicle feel: the same car twice gives the same dive");
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
        string settings = ModSource(@"Core\Settings.cs");
        string mod = ModSource(@"Core\RealLifeOverhaulMod.cs");
        string tree = ModSource(@"Meta\OptionsTree.cs");
        string features = ModSource(@"Meta\FeatureList.cs");
        Check(model.Length > 0 && rules.Length > 0 && handling.Length > 0 && tuning.Length > 0 && catalogue.Length > 0 && settings.Length > 0 && mod.Length > 0 && tree.Length > 0 && features.Length > 0,
            "vehicle feel sources: the files are there");

        string begin = MemberText(handling, "public bool Begin(");
        Check(begin.Contains("CarCatalogue.IsGameCar(type.vehicleTypeName)") && begin.Contains("Settings.HandlingOtherMods") && begin.Contains("HandlingModel.Soften(_chassis, target.RideHz)")
              && begin.IndexOf("Read(car, vehicle, body, type.maxSpeed)", StringComparison.Ordinal) < begin.IndexOf("HandlingModel.Soften(", StringComparison.Ordinal)
              && begin.IndexOf("HandlingModel.Soften(", StringComparison.Ordinal) < begin.IndexOf("_design.Begin(", StringComparison.Ordinal) && !begin.Contains("PutOn("),
            "vehicle feel sources: a car whose type is not the game's, with the option on, is softened after it is read and before its design, and nothing of the car is touched");
        Check(MemberText(handling, "private string Line(").Contains("Softened(gf, gr)") && MemberText(handling, "private string Line(").Contains("Travel()")
              && MemberText(handling, "private string Travel(").Contains("_setup.FrontLeft") && MemberText(handling, "private string Softened(").Contains("OwnFrequency"),
            "vehicle feel sources: the self-check line says what a softened car rode at and what the front springs keep at the full pedal");
        Check(MemberText(tuning, "private static long HandlingStamp(").Contains("Settings.HandlingOtherMods"),
            "vehicle feel sources: switching the option while driving puts the handling on the car again (it is in the stamp the car is put on by)");
        Check(MemberText(model, "private static void Pitch(").Contains("DiveForTravel(") && MemberText(model, "private static float DiveForTravel(").Contains("FrontKeep")
              && MemberText(model, "private static float DiveForTravel(").Contains("BrakeRamp(t.BrakeRise, GameBrakeOnset, InputRate, 99f, Felt)")
              && model.Contains("public const float FrontKeep = 0.33f;") && model.Contains("public const float SoftenFrom = 1.1f;"),
            "vehicle feel sources: the nose dive is asked of the front springs' travel in the design's own step, the pedal's ramp filled there, never read from an earlier step");
        Check(rules.Contains("public static float RideCeiling(CarKind kind, HandlingLevel level)") && rules.Contains("public float RideHz;") && catalogue.Contains("public static bool IsGameCar(string typeName)"),
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
                if (name != "HandlingModel.cs" && name != "CarHandling.cs" && File.ReadAllText(file).Contains("HandlingModel.Soften("))
                {
                    users.Add(name);
                }
            }
        }
        Check(users.Count == 0, "vehicle feel sources: only the driven car's handling softens a car" + (users.Count > 0 ? " (also in " + string.Join(", ", users) + ")" : string.Empty));

        string doc = File.Exists(VehicleFeelRoot(@"docs\features\driving.md")) ? File.ReadAllText(VehicleFeelRoot(@"docs\features\driving.md")) : string.Empty;
        Check(doc.Contains("**Cars of other mods.**") && doc.Contains("HandlingModel.Soften") && doc.Contains("car_handling_modded") && doc.Contains("CarCatalogue.IsGameCar")
              && doc.Contains("**The front springs' travel at the full pedal.**") && doc.Contains("HandlingModel.DiveForTravel") && doc.Contains("HandlingModel.FrontKeep"),
            "vehicle feel docs: docs/features/driving.md says what is done for the cars of other mods and for the front springs' travel");
    }

    // ---------------------------------------------------------------- the words

    private static void VehicleFeelWords()
    {
        Dictionary<string, string> en = LocaleFile("en");
        Check(en.TryGetValue("reallife_car_handling_modded", out string label) && label.Length > 0 && label.Length <= 60 && label.Split(' ').Length <= 9 && !label.EndsWith("."),
            "vehicle feel words: the option has a label in English that fits the page (at most 9 words, 60 characters, no full stop)");
        Check(en.TryGetValue("reallife_opt_tip_car_handling_modded", out string tip) && tip.Length > 0 && tip.Length <= 190 && tip.Split(' ').Length <= 32 && tip.EndsWith("."),
            "vehicle feel words: the option has its one sentence in English (at most 32 words, 190 characters, a full stop)");
        // What's new: looked for in every row, by its English line, so that it is found before a release and after it numbers the row.
        string soft = "Cars from other mods that ride on springs far stiffer than the game's now get the soft ride, body roll and nose dive of the game's cars of their kind, and the new option "
                      + "Soft ride for other mods' cars, on by default, switches that off.";
        string fix = "Fixed: braking hard from speed dipped the nose of the Vord V150 pickup and other softly sprung cars so far that the front springs nearly reached their stops and the car jolted; "
                     + "where the springs have little travel the nose now dips no further than the game's own.";
        Check(WhatsNewHas(en, soft) && WhatsNewHas(en, fix), "vehicle feel words: What's new has the soft ride for other mods' cars and the fix of the pickup's braking (any row)");
        bool plain = true;
        foreach (string text in new[] { label ?? string.Empty, tip ?? string.Empty, soft, fix })
        {
            plain &= text.IndexOf('\u2013') < 0 && text.IndexOf('\u2014') < 0 && text.IndexOf('!') < 0 && !text.Contains(" - ") && !text.Contains("--");
        }
        Check(plain, "vehicle feel words: no dash and no exclamation mark");
    }
}
