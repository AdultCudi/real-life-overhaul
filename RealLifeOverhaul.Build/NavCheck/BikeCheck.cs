using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using RealLifeOverhaul;

// Bicycles and motorbikes (Garage/BikeRules.cs): how fast a bicycle pulls away and settles at its cruising speed, the
// rider's pose changing without a jump, the lean by speed, and the boxes the world meets measured on the four bikes'
// own numbers (read from Garage/BikeBody.cs); then, over the sources, that the boxes are put on and taken off as the
// game does a ridden scooter's, that the drivetrain and the name are given back, that the bike's name is written where
// the game writes the scooter's, that people are struck by a two-wheeler too, and that no page of the Guide still says
// the game calls them electric scooters. Where one is left is BikeClearCheck.cs, its mass on getting on BikeTurnCheck.cs.
internal static partial class Program
{
    private const float BikeStep = 0.02f;          // s: the game's physics step
    private const float BikeRolling = 35f;         // N: two bicycle tyres' rolling at 6 N m on 0.34 m wheels

    private static void Bikes()
    {
        string scripts = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));

        // ---- pulling away: walking pace within a second, near cruising speed within a few, and steady pedalling settles there.
        foreach ((string name, BikeRules.Ride ride) in new[] { ("city bicycle", BikeRules.City), ("road bicycle", BikeRules.Road) })
        {
            float m = BikeRules.ScooterMass;
            float drag = BikeRules.Drag(ride, m, BikeRolling);
            float cruise = ride.CruiseKmh / 3.6f;
            PullAway(ride, m, drag, 1f, 60f, out float walk, out float near, out float top, out float jerk);
            Check(walk > 0f && walk <= 1.0f, "bikes: a " + name + " reaches walking pace within a second: " + walk.ToString("F2") + " s");
            Check(near > 0f && near <= 6.5f, "bikes: a " + name + " reaches 90% of its cruising speed (" + ride.CruiseKmh + " km/h) within a few seconds: " + near.ToString("F2") + " s");
            Check(Math.Abs(top - cruise) / cruise < 0.01f, "bikes: steady pedalling on a " + name + " settles at its cruising speed: " + (top * 3.6f).ToString("F1") + " km/h");
            Check(jerk < 0.2f, "bikes: the " + name + "'s push changes smoothly as it gathers speed (most change in a step " + jerk.ToString("F3") + " m/s2)");
            PullAway(ride, m, drag, 0.5f, 60f, out float tiredWalk, out _, out float tiredTop, out _);
            Check(tiredWalk > walk && tiredWalk <= 2f && tiredTop < top, "bikes: a tired rider is slower to pull away (" + tiredWalk.ToString("F2") + " s to walking pace) and rides slower (" + (tiredTop * 3.6f).ToString("F0") + " km/h)");
            Check(BikeRules.Pedal(ride, m, 5f, 0f, 1f, 0f) == 0f, "bikes: letting go of the " + name + " pushes nothing (it coasts)");
            float backTop = 0f;
            float v = 0f;
            for (float t = 0f; t < 10f; t += BikeStep)
            {
                v += (BikeRules.Pedal(ride, m, v, 0f, 1f, 1f) / m) * BikeStep;
                backTop = Math.Min(backTop, v);
            }
            Check(backTop < -0.3f && backTop >= -BikeRules.BackMost, "bikes: holding the brake at a standstill walks a " + name + " back, no faster than " + BikeRules.BackMost + " m/s: " + backTop.ToString("F2"));
            // Before this fix: the scooter's engine and gearbox weighed on the wheel like another 275 kg even with the clutch out.
            PullAwayHeavy(ride, m, drag, 275f, out float oldWalk);
            Check(oldWalk > walk, "bikes: the drivetrain let go is what makes the start quicker (" + walk.ToString("F2") + " s against " + oldWalk.ToString("F2") + " s with the gearbox on the wheel)");
        }
        Check(BikeRules.FreeWheelRatio * BikeRules.FreeWheelRatio * 4.32f * 4.32f * 0.1f < 0.01f,
            "bikes: with the final drive let go, the scooter's gearbox weighs nothing on the wheel");

        // ---- a motorbike is quicker off the mark than a bicycle (its 0-100 average against a bicycle's hardest push).
        string catalogue = Source(scripts, @"Garage\BikeBody.cs");
        foreach (Match moto in Regex.Matches(catalogue, @"Kind = BikeKind\.(\w+Motorbike)[^}]*?Seconds = ([0-9.]+)f"))
        {
            float seconds = float.Parse(moto.Groups[2].Value, CultureInfo.InvariantCulture);
            float average = 100f / 3.6f / seconds;
            Check(average > Math.Max(BikeRules.City.StartPush, BikeRules.Road.StartPush), "bikes: a " + moto.Groups[1].Value + " pulls away harder than a bicycle (" + average.ToString("F1") + " m/s2 on average to 100)");
        }

        // ---- the pose: start, ride, freewheel, stop, walk back: each change once, none while the last is fading in, no flicker.
        PoseRun(new[] { (1.0f, 0f, false), (4f, 1f, true), (3f, 1f, false), (6f, 0f, false), (2f, -0.8f, false) },
            "start, ride, freewheel, stop, walk back", out string seen, out bool overlapped);
        Check(seen == "Stand Ride Coast Stand", "bikes: a ride's poses change once each, in order: " + seen);
        Check(!overlapped, "bikes: no pose change while the last is still fading in");
        PoseRun(new[] { (0.5f, 0f, false), (3f, 1f, true), (0.1f, 1f, false), (0.1f, 1f, true), (0.1f, 1f, false), (0.1f, 1f, true), (3f, 1f, true) },
            "dabs of the throttle", out string dabs, out _);
        Check(dabs == "Stand Ride", "bikes: dabs of the throttle do not flick the pose: " + dabs);
        Check(BikeRules.Want(BikeRules.Pose.Stand, 0f, true, true, true, true) == BikeRules.Pose.Ride && BikeRules.HoldFor(BikeRules.Pose.Stand, BikeRules.Pose.Ride) <= 0.05f,
            "bikes: pushing off from a stop is at once");
        Check(BikeRules.Want(BikeRules.Pose.Ride, 0f, false, false, false, true) == BikeRules.Pose.Ride, "bikes: without a stopped clip the rider is never put in it");
        Check(BikeRules.Want(BikeRules.Pose.Ride, 6f, false, true, true, false) == BikeRules.Pose.Ride, "bikes: a motorbike does not freewheel");
        float worstLegs = 0f, last = BikeRules.Smooth(0f);
        for (int i = 1; i <= 18; i++)   // the legs' 0.3 s fade at 60 frames a second
        {
            float now = BikeRules.Smooth(i / 18f);
            worstLegs = Math.Max(worstLegs, now - last);
            last = now;
        }
        Check(BikeRules.Smooth(0f) == 0f && BikeRules.Smooth(1f) == 1f && worstLegs < 0.1f && BikeRules.Smooth(1f / 18f) < 0.02f,
            "bikes: the feet leave and reach the pedals eased, no jump in a frame (most " + worstLegs.ToString("F3") + ")");
        float shown = 0f;
        float worstSteer = 0f;
        for (int i = 0; i < 120; i++)   // the physics' steering at its 160 degrees a second, in 50 Hz steps of 3.2 degrees, drawn at 144 frames a second
        {
            float target = Math.Min(30f, 3.2f * (float)Math.Floor(i * 50f / 144f));
            float next = BikeRules.Ease(shown, target, 1f / 144f, 0.06f);
            worstSteer = Math.Max(worstSteer, Math.Abs(next - shown));
            shown = next;
        }
        Check(worstSteer < 1.5f, "bikes: the bars follow the steering eased, not in the physics' 3.2 degree steps (most in a frame " + worstSteer.ToString("F2") + ")");
        Check(Near(BikeRules.Ease(3f, 9f, 0.5f, 0f), 9f) && Near(BikeRules.Ease(3f, 9f, 0f, 0.06f), 3f), "bikes: easing over no time is the target, and no frame moves nothing");

        // ---- the lean: none standing, a little at a walk, the full 33 degrees from cruising speed, gentle at a crawl.
        Check(BikeRules.LeanMost(0f) == 0f && BikeRules.LeanMost(BikeRules.WalkingPace) < 6f && Near(BikeRules.LeanMost(10f), 33f) && Near(BikeRules.LeanMost(40f), 33f),
            "bikes: the lean grows with the speed: " + BikeRules.LeanMost(BikeRules.WalkingPace).ToString("F1") + " degrees at a walk (the game's scooter: 33 from 1 m/s)");
        bool leanRises = true;
        for (float s = 0f; s < 30f; s += 0.5f)
        {
            leanRises &= BikeRules.LeanMost(s + 0.5f) >= BikeRules.LeanMost(s) && BikeRules.LeanRate(s + 0.5f) >= BikeRules.LeanRate(s);
        }
        Check(leanRises && BikeRules.LeanRate(0f) < 220f && BikeRules.LeanMost(-5f) == BikeRules.LeanMost(5f), "bikes: lean and its rate only rise with speed, either way, and a crawl leans slower than the game's 220 degrees a second");

        // ---- the boxes the world meets, on the four bikes' own numbers.
        MatchCollection defs = Regex.Matches(catalogue,
            @"Kind = BikeKind\.(\w+).*?(Motor = true)?.*?RearZ = (-?[0-9.]+|Scooter\w+)f?, FrontZ = (-?[0-9.]+|Scooter\w+)f?, WheelRadius = ([0-9.]+)f, Seat = new Vector2\(([0-9.]+)f, ([0-9.\-]+)f\), Grip = new Vector3\(([0-9.]+)f, ([0-9.]+)f, ([0-9.\-]+)f\)",
            RegexOptions.Singleline);
        Check(defs.Count == 4, "bikes: the four bikes' numbers are read from BikeBody.cs: " + defs.Count);
        foreach (Match def in defs)
        {
            string kind = def.Groups[1].Value;
            bool motor = kind.EndsWith("Motorbike", StringComparison.Ordinal);
            float rear = Number(def.Groups[3].Value), front = Number(def.Groups[4].Value), radius = Number(def.Groups[5].Value);
            float seat = Number(def.Groups[6].Value), seatZ = Number(def.Groups[7].Value);
            float gripX = Number(def.Groups[8].Value), gripY = Number(def.Groups[9].Value), gripZ = Number(def.Groups[10].Value);
            BikeRules.Box frame = BikeRules.Frame(rear, front, radius, seat, motor);
            BikeRules.Box bars = BikeRules.Bars(gripX, gripY, gripZ, seatZ);
            Check(frame.Back <= rear - radius && frame.Front >= front + radius, "bikes: a " + kind + "'s frame box runs from wheel to wheel (" + frame.Back.ToString("F2") + " to " + frame.Front.ToString("F2") + " m)");
            Check(frame.Length > 1.06f + 0.5f, "bikes: a " + kind + "'s frame box is well longer than the scooter's 1.06 m: " + frame.Length.ToString("F2"));
            Check(frame.Bottom >= BikeRules.KerbClearance - 0.001f && frame.Top >= 2f * radius - 0.001f, "bikes: a " + kind + "'s frame box clears a kerb and covers the wheels' tops");
            Check(BikeRules.LowestAt(frame, BikeRules.LeanMostDeg + BikeRules.LeanMargin) >= BikeRules.LeanClearance - 0.001f
                  && BikeRules.LowestAt(bars, BikeRules.LeanMostDeg + BikeRules.LeanMargin) >= BikeRules.LeanClearance,
                "bikes: at full lean and more a " + kind + "'s boxes stay off the road (lowest " + BikeRules.LowestAt(frame, 38f).ToString("F3") + " m)");
            Check(bars.HalfWidth >= gripX + 0.05f && bars.Top >= gripY && bars.Bottom < gripY && bars.Front >= gripZ && bars.Back <= seatZ,
                "bikes: a " + kind + "'s bars box takes the grips and the hands on them, from the saddle forward");
            BikeClearBoxes(kind, frame, bars);   // a bike left over a post (BikeClearCheck.cs)
        }

        // ---- over the sources: the boxes, the drivetrain, the lean and the name put on and given back.
        string mount = Source(scripts, @"Garage\BikeMount.cs");
        string mounted = Body(mount, "public void Mounted(");
        string dismounted = Body(mount, "public void Dismounted(");
        string detach = Body(mount, "public void Detach(");
        string shape = Body(mount, "private void Shape(");
        string tune = Body(mount, "private void Tune(");
        string step = Body(mount, "private void OnFixedUpdate(");
        Check(shape.Contains("_scooter.vehicleCollider as BoxCollider") && shape.Contains("BikeRules.Frame(") && shape.Contains("_frame.size ="),
            "bikes source: the game's own box of the scooter is made the frame's (BikeMount.Shape)");
        Check(shape.Contains("BikeRules.Bars(") && shape.Contains("isTrigger = false") && shape.Contains("LayerHelper.VehiclesLayerIndex") && shape.Contains("_bars.enabled = false"),
            "bikes source: the bars box is solid, on the Vehicles layer, and off until ridden");
        Check(Body(mount, "private void BarsOn(").Contains("_bars.gameObject.layer = LayerHelper.VehiclesLayerIndex") && Body(mount, "private void BarsOn(").Contains("_bars.enabled = true") && mounted.Contains("BarsOn()") && dismounted.Contains("BarsOff()"),
            "bikes source: the bars box is on while ridden, as the game puts a ridden scooter's own box on Vehicles, and off again after");
        Check(detach.Contains("_frame.center = _frameCentreWas") && detach.Contains("_frame.size = _frameSizeWas") && detach.Contains("_obstacle.size = _obstacleSizeWas")
              && detach.Contains("customOverlayHeaderKey = _headerWas") && detach.Contains("canRun = _canRunWas") && detach.Contains("controlType = _clutchTypeWas")
              && detach.Contains("clutchInput = _clutchInputWas") && detach.Contains("maxLeanAngle = _leanMostWas") && detach.Contains("leanAngleMaxDelta = _leanRateWas"),
            "bikes source: Detach gives back the box, the obstacle, the name, the engine, the clutch and the lean exactly");
        Check(tune.Contains("canRun = false") && tune.Contains("ClutchControlType.Manual") && tune.Contains("finalGearRatio = BikeRules.FreeWheelRatio") && tune.Contains("BikeRules.Drag("),
            "bikes source: a bicycle's drivetrain is let go and its drag comes from the rules");
        Check(step.Contains("BikeRules.Pedal(") && step.Contains("_effort") && !mount.Contains("PushOff"), "bikes source: the rider's push is the rules' one, with the effort (energy) in it");
        Check(Body(mount, "private void Name(").Contains("customOverlayHeaderKey = Def.Name"), "bikes source: what the pointer is on is headed with the bike's own name");

        string names = Source(scripts, @"Garage\BikeNames.cs");
        string two = Source(scripts, @"Garage\TwoWheelers.cs");
        Check(names.Contains("label.Key = _riding.Def.Name") && names.Contains("Delegate.Combine(_onNotice, Notifications.onShow)") && names.Contains("list[at] = BikeCatalogue.Of(bike.Kind).Name"),
            "bikes source: the panel, the messages (first on the game's event) and the car park's list get the bike's name");
        Check(two.Contains("_names.Ride(_riding)") && two.Contains("_names.Frame()") && two.Contains("_names.Quick()") && two.Contains("_names.Dispose()"),
            "bikes source: the names are looked after on getting on and off, each frame of a ride, four times a second, and let go at the end");
        string strikes = Source(scripts, @"Ragdoll\CarStrikes.cs");
        Check(strikes.Contains("selected is ScooterController") && strikes.Contains("!_twoWheeler") && strikes.Contains("StrikePedestrian(walker, thrown, hit, body)")
              && strikes.Contains("PedestrianIncidents.KnockedBelow"),
            "bikes source: people are struck by a two-wheeler too, knocked down and never more, and it is not the player's car to the traffic");

        // ---- the review's points: traffic told when the bars box goes, the box off before the rider is handed back, a fault
        // letting go of the ride, sizes in the collider's own axes, the mass kept, Detach's order, and the box measured once.
        string barsOff = Body(mount, "public void BarsOff(");
        Check(barsOff.Contains("Manager.TriggerColliderRemovedEvent(_bars)") && barsOff.IndexOf("TriggerColliderRemovedEvent", StringComparison.Ordinal) < barsOff.IndexOf("_bars.enabled = false", StringComparison.Ordinal)
              && Count(mount, "_bars.enabled = false") == 2 && !mount.Contains("Destroy(_bars"),
            "bikes source: the bars box is only ever switched off through BarsOff, which tells traffic first (else a car waits for it for ever)");
        string fall = Body(two, "private void Fall(");
        Check(fall.Contains("mount.BarsOff()") && fall.IndexOf("mount.BarsOff()", StringComparison.Ordinal) < fall.IndexOf("scooter.ExitVehicle()", StringComparison.Ordinal),
            "bikes source: a fall takes the bars box off before the game hands the rider back to physics");
        Check(mount.Contains("_onExit = _onExit ?? Guard.Wrap<VehicleController>(Exited)") && Body(mount, "private void Exited(").Contains("BarsOff()") && mounted.Contains("HookExit(true)")
              && dismounted.Contains("HookExit(false)") && Body(mount, "private void OnDestroy(").Contains("BarsOff()"),
            "bikes source: the bars box also goes inside the game's own getting off (onExitVehicle, made once through the guard) and when the scooter is destroyed");
        string slow = Body(two, "public void Slow(");
        string fault = slow.Substring(Math.Max(0, slow.LastIndexOf("catch (Exception e)", StringComparison.Ordinal)));
        Check(fault.Contains("_riding?.Dismounted()") && fault.Contains("_riding = null") && fault.Contains("_names.Dispose()"),
            "bikes source: when the two-wheelers switch off after a fault, the ride's box and the name's hook are let go");
        BikeRules.SizeAlong(1f, 0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f, 0.3f, 0.9f, 1.8f, out float ix, out float iy, out float iz);
        BikeRules.SizeAlong(0f, 0f, -1f, 0f, 1f, 0f, 1f, 0f, 0f, 0.3f, 0.9f, 1.8f, out float qx, out float qy, out float qz);
        float c45 = (float)Math.Sqrt(0.5);
        BikeRules.SizeAlong(-1f, 0f, 0f, 0f, 1f, 0f, 0f, 0f, -1f, 0.3f, 0.9f, 1.8f, out float tx, out _, out float tz);
        BikeRules.SizeAlong(c45, 0f, c45, 0f, 1f, 0f, -c45, 0f, c45, 1f, 1f, 1f, out float hx, out _, out float hz);
        Check(Near(ix, 0.3f) && Near(iy, 0.9f) && Near(iz, 1.8f) && Near(qx, 1.8f) && Near(qz, 0.3f) && Near(qy, 0.9f) && hx > 1.4f && hz > 1.4f && Near(tx, 0.3f) && Near(tz, 1.8f),
            "bikes: a box's size is given along the collider's own axes (a quarter turn swaps width and length, never a negative size)");
        Check(shape.Contains("_frame.size = SizeIn(space,") && shape.Contains("_obstacle.size = SizeIn(space,") && mount.Contains("_frame.transform.localEulerAngles"),
            "bikes source: the frame box and the obstacle are sized in their own axes, and the log line says how the box is turned");
        Check(Body(mount, "public static BikeMount Attach(").Contains("mount.KeepMass();") && Body(mount, "private void BarsOn(").Contains("PutMassBack()")
              && barsOff.Contains("PutMassBack()") && detach.Contains("PutMassBack()"),
            "bikes source: the body's centre of mass and inertia are read before the boxes change and written back after every change");
        int restoreBox = detach.IndexOf("_frame.size = _frameSizeWas", StringComparison.Ordinal), restoreRest = detach.IndexOf("forceRenderingOff = false", StringComparison.Ordinal);
        Check(restoreBox > 0 && restoreBox < restoreRest && detach.IndexOf("_obstacle.size = _obstacleSizeWas", StringComparison.Ordinal) < restoreRest
              && detach.IndexOf("customOverlayHeaderKey = _headerWas", StringComparison.Ordinal) < restoreRest && Count(detach, "catch (Exception e)") >= 5,
            "bikes source: Detach puts the box, the obstacle, the name and the mass back first, each on its own, before anything that could throw");
        string lookup = Body(strikes, "private void Lookup(");
        Check(lookup.Contains("_measureAgain > 0") && lookup.Contains("_measureAgain--") && lookup.Contains("_measureAgain = MeasureLooks"),
            "bikes source: a two-wheeler's box is measured when it is got on and a few looks after, not twice a second all ride");

        BikeClear(scripts);   // where a bike is left: the rules and, over the sources, when they are asked (BikeClearCheck.cs)
        BikeTurn(scripts, catalogue);   // a bike got on after a save loads turns on the game's own inertia (BikeTurnCheck.cs)

        // ---- no page of the Guide says the game calls a bicycle or a motorbike an electric scooter, in any language.
        string locales = Path.Combine(scripts, "..", "Locales");
        foreach (string language in new[] { "en", "pt", "zh-cn", "de", "fr", "es", "ru", "nl", "tr" })
        {
            string file = Path.Combine(locales, language + ".json");
            if (!File.Exists(file))
            {
                continue;
            }
            string text = File.ReadAllText(file);
            Check(!text.Contains("calls a bicycle or a motorbike an electric scooter") && !text.Contains("moto de patinete el"), "bikes: the Known limits page of " + language + " no longer says the game calls them electric scooters");
        }
    }

    // A bicycle pulled away from a standstill with the throttle held: seconds to walking pace and to 90% of cruising speed,
    // the speed after 'seconds', and the most the acceleration changed in one step.
    private static void PullAway(BikeRules.Ride ride, float mass, float drag, float effort, float seconds, out float walk, out float near, out float top, out float jerk)
    {
        walk = near = -1f;
        jerk = 0f;
        float v = 0f, lastA = float.NaN;
        float cruise = ride.CruiseKmh / 3.6f;
        for (float t = BikeStep; t <= seconds; t += BikeStep)
        {
            float a = (BikeRules.Pedal(ride, mass, v, 1f, effort, 0f) - (v > 0f ? BikeRolling : 0f) - BikeRules.Air(drag, v)) / mass;
            if (!float.IsNaN(lastA))
            {
                jerk = Math.Max(jerk, Math.Abs(a - lastA));
            }
            lastA = a;
            v += a * BikeStep;
            if (walk < 0f && v >= BikeRules.WalkingPace)
            {
                walk = t;
            }
            if (near < 0f && v >= 0.9f * cruise)
            {
                near = t;
            }
        }
        top = v;
    }

    // The same start with the scooter's gearbox still on the wheel (another 'extra' kg to get going): seconds to walking pace.
    private static void PullAwayHeavy(BikeRules.Ride ride, float mass, float drag, float extra, out float walk)
    {
        walk = 99f;
        float v = 0f;
        for (float t = BikeStep; t <= 10f; t += BikeStep)
        {
            float a = (BikeRules.Pedal(ride, mass, v, 1f, 1f, 0f) - (v > 0f ? BikeRolling : 0f) - BikeRules.Air(drag, v)) / (mass + extra);
            v += a * BikeStep;
            if (v >= BikeRules.WalkingPace)
            {
                walk = t;
                return;
            }
        }
    }

    // The pose as BikeMount steps it at 60 frames a second, over stretches of (seconds, speed m/s, throttle held):
    // the poses shown in turn, and whether any change came while the last was still fading in.
    private static void PoseRun((float seconds, float speed, bool pushing)[] stretches, string what, out string seen, out bool overlapped)
    {
        const float dt = 1f / 60f;
        BikeRules.Pose riding = BikeRules.Pose.Stand;
        float other = 0f, age = BikeRules.PoseFade;
        seen = riding.ToString();
        overlapped = false;
        foreach ((float seconds, float speed, bool pushing) in stretches)
        {
            for (float t = 0f; t < seconds; t += dt)
            {
                age += dt;
                BikeRules.Pose want = BikeRules.Want(riding, speed, pushing, true, true, true);
                if (want == riding)
                {
                    other = 0f;
                    continue;
                }
                other += dt;
                if (other < BikeRules.HoldFor(riding, want) || age < BikeRules.PoseFade)
                {
                    continue;
                }
                overlapped |= age < BikeRules.PoseFade;
                riding = want;
                age = 0f;
                other = 0f;
                seen += " " + riding;
            }
        }
    }

    private static float Number(string text)
    {
        if (text == "ScooterRearZ")
        {
            return BikeCatalogueRear;
        }
        if (text == "ScooterFrontZ")
        {
            return BikeCatalogueFront;
        }
        return float.Parse(text, CultureInfo.InvariantCulture);
    }

    private const float BikeCatalogueRear = -0.651f;    // BikeCatalogue.ScooterRearZ: where the game's scooter has its rear wheel
    private const float BikeCatalogueFront = 0.723f;    // ...and its front one
}
