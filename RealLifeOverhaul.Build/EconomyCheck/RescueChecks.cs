using System;
using System.Collections.Generic;
using RealLifeOverhaul;

// How fast the emergency services answer (Traffic/ResponseRules, ResponseWords, TrafficAccidents.Units' Dispatch and self-check, the crews' pace in People):
// a player wrote that the emergency services get to an accident at a saunter (26 Nov 2026). The causes, read from the code: a unit was sent 5 to 13 s
// after the accident (3 to 22 for the patrol car), came from up to 170 m of road at 14 m/s (50 km/h, the traffic's own speed, so no quicker than the cars
// it came up behind), its crew carried the bag and wheeled the stretcher at a walking clip's 1.2 m/s, and the scene was planned for two to three minutes.
// Checked here, game-free, over the city's distances: the delays, the start, the drive and the time to the scene against what they were, the words that say
// when help comes (a promise that is kept), the crew's pace against what a walking clip can be played at, the scene's length, the self-check's verdict (each
// way it can fail), and over the sources the wiring: the speed and the planning of the bends set on every unit that answers a call, no red light that stops
// it, its lights and siren, a start out of the camera's sight, nothing in a Going Public guest's session and nothing per frame.
internal static partial class Program
{
    private static void Rescue()
    {
        RescueDelays();
        RescueStart();
        RescueDrive();
        RescueWords();
        RescueFoot();
        RescueScene();
        RescueVerdict();
        RescueSources();
        RescueTexts();
    }

    private static float RescueOldMean()
    {
        return ResponseRules.ArrivalSeconds((ResponseRules.Before.AmbulanceDelayLeast + ResponseRules.Before.AmbulanceDelayMost) * 0.5f, ResponseRules.Before.AmbulanceRoute, ResponseRules.Before.Speed);
    }

    private static float RescueTypical(bool call)
    {
        return call ? ResponseRules.TypicalArrival(false, ResponseRules.CallDelay) : ResponseRules.TypicalArrival(false);
    }

    private static void RescueDelays()
    {
        bool inAmbulance = true, inPolice = true;
        for (int i = 0; i <= 20; i++)
        {
            float roll = i / 20f;
            float a = ResponseRules.Delay(false, roll), p = ResponseRules.Delay(true, roll);
            inAmbulance &= a >= ResponseRules.AmbulanceDelayLeast && a <= ResponseRules.AmbulanceDelayMost;
            inPolice &= p >= ResponseRules.PoliceDelayLeast && p <= ResponseRules.PoliceDelayMost;
        }
        Check(inAmbulance && inPolice && ResponseRules.Delay(false, -3f) == ResponseRules.AmbulanceDelayLeast && ResponseRules.Delay(true, 9f) == ResponseRules.PoliceDelayMost,
            "rescue: the ambulance is sent 3 to 6 s after the call and the patrol car 3 to 9 s, whatever the roll (a roll outside 0 to 1 is held to it)");
        Check(ResponseRules.AmbulanceDelayMost < (ResponseRules.Before.AmbulanceDelayLeast + ResponseRules.Before.AmbulanceDelayMost) * 0.5f
              && ResponseRules.PoliceDelayMost < ResponseRules.Before.PoliceDelayMost * 0.5f && ResponseRules.PoliceBehindAmbulance < ResponseRules.Before.PoliceBehindAmbulance,
            "rescue: even the slowest dispatch is quicker than the old mean one: 6 s against 5 to 13 for the ambulance, 9 against 3 to 22 for the patrol car, and 3 s behind the ambulance, not 5");
        Check(ResponseRules.PoliceAt(4f, 5f, true, false, false) == 8f && ResponseRules.PoliceAt(9f, 5f, true, false, false) == 9f && ResponseRules.PoliceAt(4f, 5f, true, true, false) == 4f
              && ResponseRules.PoliceAt(4f, 5f, false, false, false) == 4f && ResponseRules.PoliceAt(4f, 5f, true, false, true) == 4f,
            "rescue: on a single lane the patrol car waits for the ambulance and goes 3 s after it; with the far end, no ambulance or a hit-and-run to chase it goes at its own time");
        Check(ResponseRules.CallDelay <= 1f && ResponseRules.CallDelay > 0f && ResponseRules.RetryUnseenAfter < ResponseRules.RetryAfter,
            "rescue: a 911 report sends whoever is waiting within a second, and a start that would show is looked for again sooner than a unit that could not be had");
    }

    private static void RescueStart()
    {
        bool lengths = true, minimums = true;
        for (int i = 0; i <= 20; i++)
        {
            float roll = i / 20f;
            float a = ResponseRules.RouteLength(false, roll), p = ResponseRules.RouteLength(true, roll);
            lengths &= a >= ResponseRules.AmbulanceRouteLeast && a <= ResponseRules.AmbulanceRouteMost && p >= ResponseRules.PoliceRouteLeast && p <= ResponseRules.PoliceRouteMost;
            minimums &= ResponseRules.RouteMinimum(false, false) <= a && ResponseRules.RouteMinimum(true, false) <= p
                        && ResponseRules.RouteMinimum(false, true) < ResponseRules.RouteMinimum(false, false) && ResponseRules.RouteMinimum(true, true) < ResponseRules.RouteMinimum(true, false);
        }
        Check(lengths && minimums, "rescue: the ambulance starts 100 to 130 m of road from its place and the patrol car 100 to 140 m, never under what a start out of sight needs, and a second try settles for a shorter run-in");
        Check(ResponseRules.AmbulanceRouteMost < ResponseRules.Before.AmbulanceRoute && ResponseRules.StartMost(false) == ResponseRules.AmbulanceRouteMost + ResponseRules.WalkOvershoot
              && ResponseRules.StartMost(true) > ResponseRules.StartMost(false),
            "rescue: the start is nearer than the 170 m it was (the farthest a start can come out, the walk and its last leg, is 175 m for the ambulance), and its limit is the walk's length and one leg");
    }

    private static void RescueDrive()
    {
        float oldSpeed = ResponseRules.Before.Speed;
        Check(ResponseRules.Speed > oldSpeed * 1.5f && ResponseRules.Speed <= 24f && ResponseRules.FireSpeed > oldSpeed && ResponseRules.FireSpeed < ResponseRules.Speed && ResponseRules.TrafficSpeed == oldSpeed,
            "rescue: an answering unit drives at 22 m/s (79 km/h), the fire engine at 18, where the traffic's own and the old pace was 14 (50 km/h); 22 is below the driver's fast mode (24), so the unit asks for the fast planning itself (Brisk)");
        bool monotone = true, quicker = true, shorter = true;
        float last = 0f;
        for (float metres = 10f; metres <= 400f; metres += 10f)
        {
            float now = ResponseRules.DriveSeconds(metres, ResponseRules.Speed);
            float before = ResponseRules.DriveSeconds(metres, ResponseRules.Before.Speed);
            monotone &= now > last && ResponseRules.DriveSeconds(metres, ResponseRules.Speed + 4f) <= now && (metres < 120f || ResponseRules.DriveSeconds(metres, ResponseRules.Speed + 4f) < now);
            quicker &= now <= before + 0.0001f && (metres < 100f || now < before);
            last = now;
        }
        for (float metres = 30f; metres <= 175f; metres += 5f)
        {
            // Over every road the new start can have, the whole answer (the slowest dispatch, the farthest start) against the old mean one.
            shorter &= ResponseRules.ArrivalSeconds(ResponseRules.AmbulanceDelayMost, metres, ResponseRules.Speed) < RescueOldMean();
        }
        Check(monotone && quicker, "rescue: the drive takes longer the further it is and no longer the faster, and over 10 to 400 m of road is never slower at 79 km/h than it was at 50 and quicker from 100 m (a short road never gets to speed)");
        float typical = RescueTypical(false);
        float oldTypical = RescueOldMean();
        Check(shorter && typical < oldTypical * 0.65f && typical > 8f && typical < 20f && oldTypical > 22f,
            "rescue: from the accident to the ambulance at the scene takes about " + typical.ToString("F0") + " s now (" + oldTypical.ToString("F0") + " s before); the slowest dispatch from the farthest start is still quicker than the old mean, and nothing is instant");
        Check(ResponseRules.DriveSeconds(0f, 22f) == 0f && ResponseRules.DriveSeconds(100f, 0f) == 0f && ResponseRules.ArrivalSeconds(4f, 0f, 22f) == 4f,
            "rescue: no road or no speed is no drive, and an arrival is the delay at the least");
    }

    private static void RescueWords()
    {
        float slowestStaged = ResponseRules.ArrivalSeconds(ResponseRules.AmbulanceDelayMost, ResponseRules.StartMost(false), ResponseRules.Speed);
        float slowestCall = ResponseRules.ArrivalSeconds(ResponseRules.CallDelay, ResponseRules.StartMost(false), ResponseRules.Speed);
        float typicalStaged = RescueTypical(false);
        float typicalCall = RescueTypical(true);
        int said = ResponseRules.When(typicalStaged), saidCall = ResponseRules.When(typicalCall);
        float[] bounds = { 20f, 30f, 60f, 120f, float.MaxValue };
        Check(said == ResponseRules.WhenHalf && saidCall == ResponseRules.WhenTwenty,
            "rescue: told after an accident down the road the help is 'within half a minute' (" + typicalStaged.ToString("F0") + " s) and after a 911 report 'within twenty seconds' (" + typicalCall.ToString("F0") + " s)");
        Check(slowestStaged <= bounds[said] && slowestCall <= bounds[saidCall],
            "rescue: the promise is kept over every start the road can give: the slowest dispatch from the farthest start (" + slowestStaged.ToString("F0") + " s) is within what the accident's words say, and so is a report's (" + slowestCall.ToString("F0") + " s)");
        bool steps = true;
        int before = -1;
        for (float seconds = 0f; seconds <= 300f; seconds += 1f)
        {
            int when = ResponseRules.When(seconds);
            steps &= when >= before && when >= 0 && when <= ResponseRules.WhenFew && seconds * 1.5f <= bounds[when];
            before = when;
        }
        Check(steps && ResponseRules.When(0f) == ResponseRules.WhenTwenty && ResponseRules.When(500f) == ResponseRules.WhenFew,
            "rescue: the words only ever get longer as the help gets further, and every one covers its time with half as long again to spare");
        var seen = new HashSet<int>();
        bool inRange = true;
        for (int moment = 0; moment < 80; moment++)
        {
            int pick = ResponseRules.Pick(4, 1234, moment, 911);
            inRange &= pick >= 0 && pick < 4 && pick == ResponseRules.Pick(4, 1234, moment, 911);
            seen.Add(pick);
        }
        int differs = 0, salted = 0;
        for (int day = 0; day < 40; day++)
        {
            differs += ResponseRules.Pick(4, day, 0, 911) != ResponseRules.Pick(4, day + 1, 0, 911) ? 1 : 0;
            salted += ResponseRules.Pick(4, day, 3, 911) != ResponseRules.Pick(4, day, 3, 912) ? 1 : 0;
        }
        Check(inRange && seen.Count == 4 && differs > 15 && salted > 15 && ResponseRules.Pick(1, 5, 5, 5) == 0 && ResponseRules.Pick(0, 5, 5, 5) == 0,
            "rescue: one of four lines is picked by a roll of the day, the call and the line's own salt: the same every time it is asked, every line comes up, another day or another list reads differently");
    }

    private static void RescueFoot()
    {
        Check(ResponseRules.CrewPace > ResponseRules.Before.CrewPace && ResponseRules.WalkPace == ResponseRules.Before.CrewPace && ResponseRules.CrewPace <= ResponseRules.WalkPace * ResponseRules.ClipMost
              && ResponseRules.HeldPace(5f) == ResponseRules.WalkPace * ResponseRules.ClipMost && ResponseRules.HeldPace(1.2f) == 1.2f && ResponseRules.HeldPace(ResponseRules.CrewPace) == ResponseRules.CrewPace,
            "rescue: the crew carries the bag and wheels the stretcher at 1.8 m/s, not 1.2, the same clips played 1.5 times their own speed, never past the 1.6 times a walking clip is matched at");
        Check(!ResponseRules.Runs(2f) && !ResponseRules.Runs(ResponseRules.RunFrom) && ResponseRules.Runs(ResponseRules.RunFrom + 0.1f) && ResponseRules.Runs(40f),
            "rescue: somebody with free hands runs a way longer than 6 m to the casualty, the traffic post or the gate, and walks a step");
        bool treats = true;
        for (int i = 0; i <= 10; i++)
        {
            float roll = i / 10f;
            float alive = ResponseRules.TreatFor(false, true, roll), dead = ResponseRules.TreatFor(true, true, roll), driver = ResponseRules.TreatFor(false, false, roll);
            treats &= alive >= ResponseRules.TreatInjured && alive <= ResponseRules.TreatInjuredMost && alive <= ResponseRules.Before.TreatInjuredMost && dead >= ResponseRules.TreatFatalLeast
                      && dead <= ResponseRules.TreatFatalMost && driver >= ResponseRules.TreatDriverLeast && driver <= ResponseRules.TreatDriverMost;
        }
        Check(treats && ResponseRules.TreatInjuredMost < ResponseRules.Before.TreatInjuredMost, "rescue: the paramedics work 6 to 11 s over somebody hurt (9 to 16 before), 4 to 6 over somebody dying or dead and 6 to 10 at a driver's side");
    }

    private static void RescueScene()
    {
        bool shorter = true;
        float[] oldMost = { 100f, 130f, 170f };
        for (int severity = 0; severity <= 2; severity++)
        {
            for (int i = 0; i <= 10; i++)
            {
                float roll = i / 10f;
                shorter &= ResponseRules.SceneTotal(severity, true, roll) <= oldMost[severity] - 15f && ResponseRules.SceneTotal(severity, false, roll) <= (severity == 2 ? 170f : severity == 1 ? 125f : 65f);
            }
        }
        Check(shorter && ResponseRules.SceneTotal(2, true, 1f) == 140f && ResponseRules.SceneTotal(0, true, 0f) == 55f && ResponseRules.SingleScene(1f) == ResponseRules.SingleMost
              && ResponseRules.SingleMost < ResponseRules.Before.SingleMost && ResponseRules.SceneTotal(2, true, 0f) < 125f,
            "rescue: a staged accident is planned for 55 to 80 s (minor), 75 to 105 (moderate) and 100 to 140 (severe), a person knocked down 90 to 130, each shorter than before; the scene still ends early when everyone has done");
        bool stays = true;
        for (int severity = 0; severity <= 2; severity++)
        {
            stays &= ResponseRules.Stay(true, severity, 1f) <= (severity == 2 ? 70f : severity == 1 ? 50f : 35f) - 10f && ResponseRules.Stay(false, severity, 1f) <= (severity == 2 ? 70f : 45f) - 10f
                     && ResponseRules.StayMost(true, severity) == ResponseRules.Stay(true, severity, 1f) && ResponseRules.Stay(true, severity, 0f) < ResponseRules.Stay(true, severity, 1f);
        }
        Check(stays, "rescue: a parked unit stays 14 to 50 s (the patrol car) and 18 to 50 s (the ambulance) at the most, each at least ten seconds under what it was, so the road opens sooner");
    }

    private static void RescueVerdict()
    {
        Check(ResponseRules.Verdict(false, 4f, 0, 118f, 17f, ResponseRules.Speed, true, true, true) == null && ResponseRules.Verdict(true, 8f, 0, 130f, 20f, ResponseRules.Speed, true, true, true) == null
              && ResponseRules.Verdict(false, 4f, 0, 118f, 17f, ResponseRules.Speed, true, false, false) == null,
            "rescue: the self-check passes an ambulance sent after 4 s from 118 m that is there after 17 s with its lights and siren on, a patrol car sent after 8 s, and no siren where the option is off");
        string late = ResponseRules.Verdict(false, 40f, 0, 118f, 53f, ResponseRules.Speed, true, true, true);
        string far = ResponseRules.Verdict(false, 4f, 0, 420f, 40f, ResponseRules.Speed, true, true, true);
        string slow = ResponseRules.Verdict(false, 4f, 0, 118f, 60f, ResponseRules.Speed, true, true, true);
        string old = ResponseRules.Verdict(false, 4f, 0, 118f, 17f, ResponseRules.Before.Speed, true, true, true);
        string dark = ResponseRules.Verdict(false, 4f, 0, 118f, 17f, ResponseRules.Speed, false, true, true);
        string quiet = ResponseRules.Verdict(false, 4f, 0, 118f, 17f, ResponseRules.Speed, true, false, true);
        Check(late != null && late.Contains("sent after 40 s") && far != null && far.Contains("420 m off") && slow != null && slow.Contains("the drive took") && old != null && old.Contains("top speed")
              && dark != null && dark.Contains("lights") && quiet != null && quiet.Contains("siren"),
            "rescue: the self-check fails, and says why, a dispatch after 40 s, a start 420 m off, a drive that took a minute, the old 50 km/h, no lights and no siren");
        Check(ResponseRules.Verdict(false, 12f, 2, 118f, 25f, ResponseRules.Speed, true, true, true) == null && ResponseRules.Verdict(false, 12f, 0, 118f, 25f, ResponseRules.Speed, true, true, true) != null,
            "rescue: a unit whose start showed twice and was looked for again is allowed the time each of those cost, and one that waited the same with no reason is not");
        string two = ResponseRules.Verdict(false, 40f, 0, 420f, 100f, 14f, false, false, true);
        Check(two != null && two.Contains("sent after") && two.Contains("m off") && two.Contains("top speed") && two.Contains("lights") && two.Contains("siren") && !two.EndsWith(";") && !two.EndsWith(" "),
            "rescue: every fault is told in one sentence, the way the log reads it");
    }

    private static void RescueSources()
    {
        string units = ModSource(@"Traffic\TrafficAccidents.Units.cs"), accidents = ModSource(@"Traffic\TrafficAccidents.cs"), staging = ModSource(@"Traffic\TrafficAccidents.Staging.cs");
        string driver = ModSource(@"Traffic\EmergencyDriver.cs"), sense = ModSource(@"Traffic\EmergencyDriver.Sense.cs"), unit = ModSource(@"Traffic\ResponseUnit.cs");
        string rules = ModSource(@"Traffic\ResponseRules.cs"), words = ModSource(@"Traffic\ResponseWords.cs"), tools = ModSource(@"Traffic\TrafficTools.cs");
        string dispatch = MemberText(units, "private void Dispatch(Attendance attendance, float now)");
        string pulled = MemberText(units, "private void Pulled(Attendance attendance, EmergencyDriver driver, float fromScene, float now)");
        Check(driver.Contains("public const float DefaultCruiseSpeed = 14f;") && driver.Contains("public const float FastFrom = 24f;"),
            "rescue: the driver's own default pace is still 14 m/s and its fast mode starts above 24, the figures the rules are set against");
        Check(dispatch.Contains("unit.Driver.Urgent(ResponseRules.Speed)") && dispatch.Contains("attendance.Quick = !chasing;") && dispatch.Contains("ResponseRules.RouteLength(false, Random.value)")
              && dispatch.Contains("ResponseRules.RouteLength(true, Random.value)") && dispatch.Contains("ResponseRules.RouteMinimum(") && dispatch.Contains("bool unseen = attendance.Tries < 2;")
              && dispatch.Contains("attendance.At = now + ResponseRules.RetryUnseenAfter;") && dispatch.Contains("attendance.At = now + ResponseRules.RetryAfter;") && !units.Contains("AmbulanceRouteLength"),
            "rescue: every ambulance and patrol car a scene sends drives at the speed of an emergency (a patrol car that will chase keeps the chase's pace), from a start the rules give, out of the camera's sight on the first two tries");
        Check(accidents.Contains("ResponseRules.Delay(false, Random.value), now + ResponseRules.Delay(true, Random.value)") && staging.Contains("ResponseRules.Delay(false, Random.value)") && staging.Contains("ResponseRules.Delay(true, Random.value)")
              && staging.Contains("ResponseRules.SceneTotal((int)_severity, police, Random.value)") && accidents.Contains("ResponseRules.SingleScene(Random.value)")
              && !staging.Contains("Random.Range(5f, 13f)") && !accidents.Contains("Random.Range(4f, 10f)") && !units.Contains("_ambulance.At + 5f"),
            "rescue: a staged accident and somebody knocked down are sent and timed by the rules, and none of the old figures is left");
        int soundingAt = pulled.IndexOf("bool sounding = unit.Sounding;", StringComparison.Ordinal);
        Check(soundingAt > 0 && soundingAt < pulled.IndexOf("unit.Park();", StringComparison.Ordinal) && pulled.Contains("ResponseCheck(attendance, now, how, lit, sounding);")
              && units.Contains("Traffic check (response): {Name(attendance)}") && units.Contains("PASS.") && units.Contains("\"FAIL, \" + why"),
            "rescue: the response's self-check line, 'Traffic check (response): ...: PASS' or 'FAIL, why', is written once as a unit parks, with its lights and siren read before the parking puts the siren out");
        string dress = MemberText(unit, "private bool Dress()");
        Check(dress.Contains("Driver.Lit = true;") && dress.Contains("TrafficEffects.CreateFlasher(Car, \"RealLifeOverhaul Emergency Lights\"") && dress.Contains("TrafficEffects.CreateFlasher(Car, \"RealLifeOverhaul Police Lights\"")
              && dress.Contains("Settings.Siren") && dress.Contains("TrafficEffects.AddSiren(_kit, Car, SirenTone.Wail)") && dress.Contains("TrafficEffects.AddSiren(_kit, Car, SirenTone.Yelp)")
              && unit.Contains("public bool Flashing => InUse && _kit != null;") && unit.Contains("public bool Sounding => InUse && _siren != null && _siren.isPlaying;"),
            "rescue: an ambulance and a patrol car are dressed with their lights on from the moment they are put on the road, and a siren while the Sirens option is on (the game has no light bar or siren of its own to switch on: its decompiled code has none)");
        string[] driving = { driver, sense, unit, units, ModSource(@"Traffic\EmergencyDriver.Way.cs"), ModSource(@"Traffic\EmergencyDriver.Manner.cs"), ModSource(@"Traffic\TrafficAccidents.Leaving.cs") };
        bool noLights = true;
        foreach (string file in driving)
        {
            noLights &= !file.Contains("RouteGates.") && !file.Contains(".HoldAt =");
        }
        Check(noLights && ModSource(@"Transport\Taxi\TaxiCab.cs").Contains("RouteGates.Open(") && ModSource(@"Traffic\EmergencyDriver.Manner.cs").Contains("public int HoldAt"),
            "rescue: an answering unit never waits at a red light: no accident unit asks the red-light rules (RouteGates, which the called taxi and the bus keep to) or holds at a point");
        Check(driver.Contains("public void Urgent(float speed)") && driver.Contains("public bool Brisk { get; private set; }") && driver.Contains("Brisk = false;") && driver.Contains("bool fast = CruiseSpeed > FastFrom || Brisk;")
              && driver.Contains("CruiseSpeed > FastFrom || Brisk || _bendGrip > 0f ? BendLimit(position)") && sense.Contains("CruiseSpeed > FastFrom || Brisk ? FastBraking : Braking") && driver.Contains("Mathf.Max(speed, DefaultCruiseSpeed + 1f)"),
            "rescue: a unit asking for the emergency pace plans its bends and its braking like a fast drive (a bend is slowed for before it, a car ahead is stopped behind in room), and every Begin gives that back");
        string fire = ModSource(@"Events\FireEngine.cs") + ModSource(@"Events\FireScene.Response.cs"), arrest = ModSource(@"Crime\ArrestSequence.cs");
        Check(fire.Contains("Urgent(ResponseRules.FireSpeed)") && fire.Contains("patrol.Driver.Urgent(ResponseRules.Speed)") && arrest.Contains("car.Driver.Urgent(ResponseRules.Speed);"),
            "rescue: the fire engine and its patrol car, and an officer sent to an arrest, come at the speed of an emergency too: one rule for every road response");
        string spawn = MemberText(unit, "public bool Spawn(UnitKind kind, Waypoint anchor, float routeLength, float minimumLength, bool unseen = false)");
        Check(spawn.Contains("!TrafficTools.Ready") && spawn.Contains("shown = unseen && walked >= minimumLength && RouteScratch.Count > 0 && Shows(RouteScratch[0].position);") && spawn.Contains("StartShown = true;")
              && spawn.Contains("if (unseen && Shows(RouteScratch[i].position))") && unit.Contains("(camera.transform.position - point).sqrMagnitude < NearCamera * NearCamera") && unit.Contains("StreetSpots.IsInView(point)"),
            "rescue: an ambulance or patrol car is never put on the road where the camera could see it appear: a start that shows is walked again, and the unit is not placed at all if every start shows");
        Check(tools.Contains("public static bool Ready => Street && !GoingPublic.TrafficMirrored;") && !rules.Contains("GoingPublic") && !words.Contains("GoingPublic") && !rules.Contains("UnityEngine") && !rules.Contains("Random")
              && !rules.Contains("Time.") && !words.Contains("Send(") && !rules.Contains("Network"),
            "rescue: in a Going Public session a guest's traffic is the host's and none of this runs there (TrafficTools.Ready), the rules read neither the game nor the clock nor draw a random number, and nothing is sent");
        string tick = MemberText(units, "private void TickAttendance(Attendance attendance, float now)");
        Check(!driver.Contains("ResponseRules") && !sense.Contains("ResponseRules") && !driver.Contains("ResponseWords") && !tick.Contains("ResponseWords") && !tick.Contains("Localiz") && !words.Contains("Update("),
            "rescue: nothing is added to a frame: the driver does not read the rules in its steps, the words are made when a call is told, and a start is looked at when a unit is put on the road");
        string clips = ModSource(@"People\ActorClips.cs"), stretcher = ModSource(@"People\Stretcher.cs"), walks = ModSource(@"People\Stretcher.Walks.cs"), medics = ModSource(@"People\AccidentCast.Medics.cs");
        string care = ModSource(@"People\AccidentCast.Care.cs"), officer = ModSource(@"People\SceneOfficer.cs"), animator = ModSource(@"CityLife\Animation\ActivityAnimator.cs");
        Check(clips.Contains("public const float CrewPace = ResponseRules.CrewPace;") && clips.Contains("public const float WalkPace = ResponseRules.WalkPace;") && animator.Contains("float least = 0.35f, float most = 1.6f")
              && ResponseRules.ClipMost == 1.6f && stretcher.Contains("by.Gait(SceneClips.PushCart, SceneClips.CrewPace)") && stretcher.Contains("second.Gait(SceneClips.PullCart, SceneClips.CrewPace)")
              && walks.Contains("SceneClips.CrewPace") && !walks.Contains("SceneClips.WalkPace") && !stretcher.Contains("SceneClips.WalkPace") && !care.Contains("SceneClips.WalkPace")
              && medics.Contains("treats.Gait(SceneClips.CarryBag, SceneClips.CrewPace)") && medics.Contains("medic.Gait(SceneClips.CarryBag, SceneClips.CrewPace)") && medics.Contains("ResponseRules.TreatFor(_fatal, down, Random.value)"),
            "rescue: the stretcher is pushed and the bag carried at the crew's brisk pace, and the figure a clip can be played at is the one the animator matches to the ground");
        Check(medics.Contains("SceneClips.PaceFor(straight)") && !medics.Contains("Pace.Jog : Pace.Walk") && care.Contains("_medicA.Board(_ambulanceDoor, Pace.Jog, 40f);") && officer.Contains("SceneClips.PaceTo(here, brief.BodyKneel)")
              && officer.Contains("SceneClips.PaceTo(here, brief.TrafficPost)") && officer.Contains("SceneClips.PaceTo(here, brief.EntryPost)") && clips.Contains("return ResponseRules.Runs(metres) ? Pace.Run : Pace.Walk;"),
            "rescue: paramedics and officers run to the casualty, the traffic post and the gate when it is a way off (the game's run), and jog back to the ambulance when they have no stretcher");
        string contacts = ModSource(@"Phone\PhoneContacts.cs"), ped = ModSource(@"Ragdoll\PedestrianIncidents.cs"), pedTraffic = ModSource(@"Ragdoll\PedestrianIncidents.Traffic.cs"), pedStaged = ModSource(@"Ragdoll\PedestrianIncidents.Staged.cs");
        Check(contacts.Contains("ReportAccident(out arriveIn)") && contacts.Contains("ResponseWords.Pick(ResponseWords.Reported, 911), new { when = ResponseWords.When(arriveIn) }") && contacts.Contains("ResponseWords.Pick(ResponseWords.Yours, 912)")
              && ped.Contains("ResponseWords.Pick(ResponseWords.Struck, 913)") && pedTraffic.Contains("TrafficAccidents.NoticeHelp(") && pedStaged.Contains("TrafficAccidents.NoticeHelp(") && staging.Contains("NoticeHelp(_severity == Severity.Severe")
              && accidents.Contains("Mathf.Min(_ambulance.At, now + ResponseRules.CallDelay)") && accidents.Contains("Mathf.Min(_police.At, now + ResponseRules.CallDelay)"),
            "rescue: every call says when help comes: the 911 report (and sends the units within a second), the ambulance asked for, the pedestrian the player hit, a pedestrian and an accident down the road");
        string crash = ModSource(@"Traffic\CrashConsequences.cs");
        Check(crash.Contains("LifeEvents.Instance.Injure(48, 25f, \"reallife_crash_severe\");") && crash.Contains("LifeEvents.Instance.SendToHospital();"),
            "rescue: the player's own crash that ends in hospital is the black-screen ambulance and says so in its text (it reaches the player in moments); no road unit is driven for it");
    }

    private static void RescueTexts()
    {
        Dictionary<string, string> en = EnglishOf();
        string[] whens = { "reallife_help_when_20", "reallife_help_when_30", "reallife_help_when_60", "reallife_help_when_120", "reallife_help_when_long" };
        string[] withWhen =
        {
            "reallife_911_dispatched", "reallife_911_dispatched_2", "reallife_911_dispatched_3", "reallife_911_dispatched_4", "reallife_pedhit_scene", "reallife_pedhit_scene_2", "reallife_pedhit_scene_3", "reallife_pedhit_scene_4",
            "reallife_accident_single_moderate", "reallife_accident_single_severe", "reallife_accident_moderate", "reallife_accident_severe", "reallife_pedstruck_start_1", "reallife_pedstruck_start_2", "reallife_pedstruck_start_3"
        };
        string[] without = { "reallife_911_ambulance_sent", "reallife_911_ambulance_sent_2", "reallife_911_ambulance_sent_3", "reallife_911_ambulance_sent_4", "reallife_crash_severe" };
        bool present = true, placeholders = true, style = true, length = true;
        string longest = "";
        foreach (string when in whens)
        {
            if (en.TryGetValue(when, out string phrase) && phrase.Length > longest.Length)
            {
                longest = phrase;
            }
        }
        var all = new List<string>(withWhen);
        all.AddRange(without);
        all.AddRange(whens);
        foreach (string key in all)
        {
            if (!en.TryGetValue(key, out string text) || text.Length == 0)
            {
                present = false;
                continue;
            }
            style &= text.IndexOf(';') < 0 && text.IndexOf('!') < 0 && text.IndexOf('—') < 0 && text.IndexOf('–') < 0 && !text.Contains(" - ");
            length &= text.Replace("{when}", longest).Length <= 240;
        }
        foreach (string key in withWhen)
        {
            placeholders &= en.TryGetValue(key, out string text) && text.Contains("{when}");
        }
        foreach (string key in without)
        {
            placeholders &= en.TryGetValue(key, out string text) && !text.Contains("{when}");
        }
        string words = ModSource(@"Traffic\ResponseWords.cs"), staging = ModSource(@"Traffic\TrafficAccidents.Staging.cs"), pedestrian = ModSource(@"Ragdoll\PedestrianIncidents.cs"), crash = ModSource(@"Traffic\CrashConsequences.cs");
        bool keysInCode = true;
        foreach (string key in all)
        {
            keysInCode &= words.Contains("\"" + key + "\"") || staging.Contains("\"" + key + "\"") || pedestrian.Contains("\"" + key + "\"") || crash.Contains("\"" + key + "\"");
        }
        Check(present && placeholders && style && length, "rescue: the lines about help coming are all in the English texts (" + all.Count + "), those that say when have {when} and the others none, none with a dash, a semicolon or an exclamation mark, each at most 240 characters with the longest words");
        Check(keysInCode, "rescue: every line is named whole in the code, never built from a prefix");
        Check(en.TryGetValue("reallife_changes_next", out _) && WhatsNewHas(en, "Ambulances and police now answer a call like an emergency"),
            "rescue: What's new says it, once, in a row of its own or any numbered version's");
        Check(en.TryGetValue("reallife_guide_traffic", out string guide) && guide.Contains("Help answers like an emergency.") && guide.Contains("tells you when they will arrive") && !guide.Contains("sends the units at once. Both"),
            "rescue: the Guide's accidents page tells how quickly help comes and that a call says when");
    }
}
