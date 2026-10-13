using System;
using RealLifeOverhaul;

// Checks for the rules of the live street (Traffic/Reckless/StreetRules.cs, compiled as it ships): who hears a horn and
// what they make of it by where they are and whose light it is, what a second and a third blast change, when an officer
// warns and when they fine, how many accidents fall due at each setting of the frequency option (against the old roll),
// which cars may be picked and which kind of driver they become, how such a driver brakes, sees and corners with and
// without an accident due, how hard a collision was from the speed the two met at, and how far up the road a car is when
// somebody steps out in front of it.
//
// What is not here: anything of the game. Which people are found, how they move, how the car is driven and what the
// accident scene does with it are for the game to show, in the "Horn:", "Reckless driver:" and "Jaywalker:" lines of
// Player.log.
internal static partial class Program
{
    private static void Streets()
    {
        StreetHorn();
        StreetReactions();
        StreetPolice();
        StreetFrequency();
        StreetPicking();
        StreetDriving();
        StreetOutcomes();
    }

    private static void StreetHorn()
    {
        // A blast is the horn going down, or held for another two seconds.
        Check(StreetRules.Blast(true, false, 0.1f), "street: the horn going down is a blast");
        Check(!StreetRules.Blast(true, true, 1.9f), "street: a horn held for 1.9 s is still the same blast");
        Check(StreetRules.Blast(true, true, 2f), "street: a horn held for 2 s is another blast");
        Check(!StreetRules.Blast(false, true, 10f), "street: a horn let go is no blast");
        Check(!StreetRules.Blast(false, false, 10f), "street: no horn, no blast");

        // Earshot: a cone of 50 degrees each side out to 14 m, and 4 m either side of the car.
        Check(StreetRules.Hears(10f, 0f), "street: 10 m dead ahead hears it");
        Check(StreetRules.Hears(13.9f, 0f), "street: 13.9 m dead ahead hears it");
        Check(!StreetRules.Hears(14.5f, 0f), "street: 14.5 m ahead is out of earshot");
        Check(StreetRules.Hears(8f, 9f), "street: 8 m ahead and 9 m to the side (48 degrees) hears it");
        Check(!StreetRules.Hears(8f, 10f), "street: 8 m ahead and 10 m to the side (51 degrees) does not");
        Check(StreetRules.Hears(8f, -9f) && !StreetRules.Hears(8f, -10f), "street: the cone is the same on the left");
        Check(StreetRules.Hears(0f, 3.9f) && StreetRules.Hears(0f, -3.9f), "street: 3.9 m beside the car hears it on either side");
        Check(!StreetRules.Hears(0f, 4.5f), "street: 4.5 m beside the car does not");
        Check(StreetRules.Hears(-2.5f, 2f), "street: just behind the car's middle, close beside, hears it");
        Check(!StreetRules.Hears(-6f, 0f), "street: 6 m behind the car does not");
        Check(!StreetRules.Hears(12f, 13f), "street: beyond 14 m on the diagonal is out of earshot");

        // In the car's way: ahead of it, within its sides and a step more.
        Check(StreetRules.InPath(6f, 0f, 0.95f), "street: 6 m ahead on the car's line is in its way");
        Check(StreetRules.InPath(6f, 1.8f, 0.95f), "street: 1.8 m off the line of a 1.9 m wide car is still in its way");
        Check(!StreetRules.InPath(6f, 1.9f, 0.95f), "street: 1.9 m off it is not");
        Check(!StreetRules.InPath(-1f, 0f, 0.95f), "street: behind the car's middle is not in its way");
        Check(!StreetRules.InPath(15f, 0f, 0.95f), "street: beyond earshot is not in its way");
    }

    private static void StreetReactions()
    {
        StreetRules.Light none = StreetRules.Light.None, green = StreetRules.Light.Green, red = StreetRules.Light.Red;
        StreetRules.Place pavement = StreetRules.Place.Pavement, road = StreetRules.Place.Road, crossing = StreetRules.Place.Crossing;

        // Who is in the right.
        Check(StreetRules.InTheRight(crossing, green, true), "street: on a crossing with the walk light, in the right whatever the car's own light");
        Check(StreetRules.InTheRight(crossing, none, true), "street: on a crossing with the walk light and no light for the car, in the right");
        Check(!StreetRules.InTheRight(crossing, green, false), "street: on a crossing against the light with the car on green, in the wrong");
        Check(!StreetRules.InTheRight(crossing, none, false), "street: on a crossing against the light with no light for the car, in the wrong");
        Check(StreetRules.InTheRight(crossing, red, false), "street: the car's light is red: in the right even against their own");
        Check(StreetRules.InTheRight(road, red, false), "street: in the road away from a crossing, the car on red: in the right");
        Check(!StreetRules.InTheRight(road, green, false), "street: in the road away from a crossing, the car on green: in the wrong");
        Check(!StreetRules.InTheRight(road, none, false), "street: in the road with no light and no crossing: in the wrong");
        Check(!StreetRules.InTheRight(pavement, red, true), "street: nobody on the pavement is 'in the right': they are not in the way");

        // What they do.
        Check(StreetRules.React(crossing, true, green, true, 0f, 1f, 0.95f, 99) == StreetRules.Reaction.Glare, "street: in the right and in the way: stops and glares");
        Check(StreetRules.React(road, true, red, false, 0f, 1f, 0.95f, 0) == StreetRules.Reaction.Glare, "street: the car on red: glares, never hurries");
        Check(StreetRules.React(road, true, green, false, 0f, 1f, 0.95f, 99) == StreetRules.Reaction.Hurry, "street: in the wrong on the car's line: hurries on");
        Check(StreetRules.React(crossing, true, green, false, 0.5f, 1f, 0.95f, 99) == StreetRules.Reaction.Hurry, "street: crossing against the light, walking away from the car's line: hurries on");
        Check(StreetRules.React(road, true, none, false, 1.5f, -1f, 0.95f, 99) == StreetRules.Reaction.StepBack, "street: only just stepped in, walking inwards: steps back");
        Check(StreetRules.React(road, true, none, false, -1.5f, 1f, 0.95f, 99) == StreetRules.Reaction.StepBack, "street: the same from the other kerb");
        Check(StreetRules.React(road, true, none, false, 0.6f, -1f, 0.95f, 99) == StreetRules.Reaction.Hurry, "street: well into the car's way, walking inwards: onwards is the way out");
        Check(StreetRules.React(pavement, false, green, false, 3f, 0f, 0.95f, 49) == StreetRules.Reaction.Startle, "street: on the pavement, roll 49: looks round");
        Check(StreetRules.React(pavement, false, green, false, 3f, 0f, 0.95f, 50) == StreetRules.Reaction.None, "street: on the pavement, roll 50: does nothing");
        Check(StreetRules.React(crossing, false, red, true, 3f, 0f, 0.95f, 99) == StreetRules.Reaction.None, "street: on a crossing but not in the car's way: a bystander");
        int looked = 0;
        for (int roll = 0; roll < 100; roll++)
        {
            if (StreetRules.React(pavement, false, red, true, 3f, 0f, 0.95f, roll) == StreetRules.Reaction.Startle)
            {
                looked++;
            }
        }
        Check(looked == 50, "street: half the people on the pavement look round (" + looked + " of 100)");

        // The step back.
        Check(StreetRules.StepsBack(1.5f, -1f, 0.95f), "street: 0.35 m into the way and walking in: back is nearer");
        Check(!StreetRules.StepsBack(1.5f, 1f, 0.95f), "street: walking out of the way already: no step back");
        Check(!StreetRules.StepsBack(1.5f, 0f, 0.95f), "street: standing still: no step back");
        Check(!StreetRules.StepsBack(1.0f, -1f, 0.95f), "street: 0.85 m into the way: too far in to go back");
        Check(!StreetRules.StepsBack(2.5f, -1f, 0.95f), "street: not in the way yet: nothing to step back from");

        // A second and a third blast.
        Check(Near(StreetRules.GlareFor(1), 1.8f) && Near(StreetRules.GlareFor(2), 2.7f) && Near(StreetRules.GlareFor(3), 3.6f), "street: the glare lasts 1.8, 2.7 and 3.6 s");
        Check(Near(StreetRules.GlareFor(9), 3.6f) && Near(StreetRules.GlareFor(0), 1.8f), "street: the glare is never longer than the third blast's, nor shorter than the first's");
        Check(!StreetRules.Joins(1, 0), "street: nobody on the pavement joins in at the first blast");
        Check(StreetRules.Joins(2, 29) && !StreetRules.Joins(2, 30), "street: 30 in 100 join in at the second");
        Check(StreetRules.Joins(3, 59) && !StreetRules.Joins(3, 60) && StreetRules.Joins(7, 59), "street: 60 in 100 from the third");

        // Pace and gestures.
        Check(Near(StreetRules.HurryPace(1.2f), 3.2f), "street: a slow walker hurries at a jog (3.2 m/s)");
        Check(Near(StreetRules.HurryPace(1.8f), 3.96f), "street: a brisk walker at 2.2 times their pace");
        Check(Near(StreetRules.HurryPace(4f), 4.5f), "street: nobody sprints (4.5 m/s at the most)");
        Check(StreetRules.Waves(34) && !StreetRules.Waves(35), "street: 35 in 100 lift a hand");
        Check(StreetRules.Points(red, 29) && !StreetRules.Points(red, 30), "street: 30 in 100 point at the red light");
        Check(!StreetRules.Points(green, 0) && !StreetRules.Points(none, 0), "street: nobody points at a light that is not red");
    }

    private static void StreetPolice()
    {
        Check(StreetRules.For(3, false, false) == StreetRules.Penalty.None, "street: no officer near, no penalty");
        Check(StreetRules.For(9, false, true) == StreetRules.Penalty.None, "street: no fine without an officer near, however often, even after a warning");
        Check(StreetRules.For(2, true, false) == StreetRules.Penalty.None, "street: two in a minute beside an officer: nothing yet");
        Check(StreetRules.For(3, true, false) == StreetRules.Penalty.Warning, "street: three in a minute beside an officer: a warning");
        Check(StreetRules.For(5, true, false) == StreetRules.Penalty.Warning, "street: more than three, not yet warned: still a warning first");
        Check(StreetRules.For(1, true, true) == StreetRules.Penalty.Fine, "street: once warned, the next one beside an officer is fined");
        Check(StreetRules.For(0, true, true) == StreetRules.Penalty.None, "street: a blast at nobody in the right is never penalised");
    }

    private static void StreetFrequency()
    {
        // The option: 0.75 car accidents a game day of street time at the default, half as many again a point.
        Check(Near(StreetRules.PerDay(2), 0.75f, 0.0001f), "street: the default (2) is 0.75 accidents a game day of street time");
        Check(Near(StreetRules.PerDay(1), 0.5f, 0.0001f), "street: 1 is half an accident a game day");
        Check(Near(StreetRules.PerDay(10), 0.75f * (float)Math.Pow(1.5, 8), 0.01f) && StreetRules.PerDay(10) > 14.4f,
            "street: 10 is about 19 a day, still more than the old roll's 14.4 at 10 (" + StreetRules.PerDay(10).ToString("0.0") + ")");
        bool rising = true;
        for (int frequency = 1; frequency < 10; frequency++)
        {
            rising &= Near(StreetRules.PerDay(frequency + 1), StreetRules.PerDay(frequency) * StreetRules.StepFactor, 0.001f);
        }
        Check(rising, "street: each point of the option is half as many again as the one below");
        Check(StreetRules.PerDay(0) == 0f && StreetRules.PerDay(-3) == 0f, "street: nothing at frequency 0, and never less than nothing");

        // Against the old roll (frequency x 0.02 a look, 3 looks a game hour): about a quarter at the default, and fewer at every setting the presets use.
        float oldDay = 2 * 0.02f * 72f;
        Check(Near(oldDay, 2.88f, 0.001f) && StreetRules.PerDay(2) < oldDay / 3.5f, "street: the default is under a third of the old 2.88 a day");
        for (int frequency = 1; frequency <= 4; frequency++)
        {
            Check(StreetRules.PerDay(frequency) < frequency * 0.02f * 72f * 0.6f, "street: at " + frequency + " fewer than the old roll made, by far");
        }

        // The hours: the 24 average one, the small hours are quiet, the late afternoon busy.
        float sum = 0f, least = 9f, most = 0f;
        for (int hour = 0; hour < 24; hour++)
        {
            float f = StreetRules.HourFactor(hour);
            sum += f;
            least = Math.Min(least, f);
            most = Math.Max(most, f);
        }
        Check(Near(sum / 24f, 1f, 0.005f), "street: over the day the hours come to the average (" + (sum / 24f).ToString("0.000") + ")");
        Check(Near(least, StreetRules.HourFactor(3)) && least < 0.4f, "street: 03:00 is the quietest hour, about a third of the average");
        Check(most > 1.5f && StreetRules.HourFactor(17) > StreetRules.HourFactor(10), "street: the late afternoon is the busiest, busier than the morning");
        Check(StreetRules.HourFactor(24) == StreetRules.HourFactor(0) && StreetRules.HourFactor(-1) == StreetRules.HourFactor(23), "street: an hour outside 0 to 23 is taken round the clock");

        // A look, an hour, a day.
        float day = 0f;
        for (int hour = 0; hour < 24; hour++)
        {
            day += StreetRules.PerGameHour(2, 1f, hour);
        }
        Check(Near(day, StreetRules.PerDay(2), 0.01f), "street: a game day of looks owes the day's figure (" + day.ToString("0.000") + ")");
        Check(Near(StreetRules.Due(2, 1.4f, 12), StreetRules.Due(2, 1f, 12) * 1.4f, 0.00001f), "street: the weather multiplies it (x1.4 on ice)");
        Check(StreetRules.Due(2, 1.5f, 3) < StreetRules.Due(2, 1f, 16), "street: rain in the small hours still owes less than a dry late afternoon");
        Check(Near(StreetRules.PerHour(2, 1f), 0.75f / 24f * 60f, 0.001f), "street: an hour of play (sixty game hours) owes 1.9 at the default, the old roll owed 7.2");
        Check(StreetRules.Due(0, 1f, 12) == 0f && StreetRules.Due(2, -1f, 12) == 0f, "street: nothing falls due at frequency 0, and never less than nothing");

        // What is owing never passes 1.1, so one accident never has a second close behind it.
        float owing = 0f;
        for (int look = 0; look < 1000; look++)
        {
            owing = StreetRules.Accrue(owing, 10, 2f, 16);
        }
        Check(Near(owing, StreetRules.DueMost), "street: what is owing stops at " + StreetRules.DueMost);
        Check(StreetRules.DueMost - 1f < 0.2f, "street: after one accident is paid, at most a fifth of the next is owed already");
        Check(Near(StreetRules.Accrue(0f, 2, 1f, 12, StreetRules.PeopleShare), StreetRules.Accrue(0f, 2, 1f, 12) * 0.4f, 0.00001f), "street: a pedestrian is 0.4 of a car accident");
        int looks = 0;
        for (owing = 0f; owing < 1f && looks < 100000; looks++)
        {
            owing = StreetRules.Accrue(owing, 2, 1f, 13);
        }
        float minutes = looks * StreetRules.CheckSeconds / 60f;
        Check(minutes > 20f && minutes < 30f, "street: at the default at 13:00 an accident is owed after " + looks + " looks, " + minutes.ToString("0") + " minutes of play in the street (the old roll: 25 looks, 8 minutes)");
        looks = 0;
        for (owing = 0f; owing < 1f && looks < 100000; looks++)
        {
            owing = StreetRules.Accrue(owing, 2, 1f, 3);
        }
        Check(looks * StreetRules.CheckSeconds / 60f > 80f, "street: at 03:00 it takes more than an hour and a quarter of play");

        // Reckless drivers: three for each accident due, more at night, never certain.
        Check(Near(StreetRules.RecklessChance(2, 1f, 12, false), StreetRules.Due(2, 1f, 12) * 3f, 0.00001f), "street: at the default a reckless driver is three times as likely as an accident at a look");
        Check(Near(StreetRules.RecklessChance(2, 1f, 22, true), StreetRules.Due(2, 1f, 22) * 3f * 1.3f, 0.00001f), "street: and 1.3 times that again at night");
        Check(Near(StreetRules.RecklessChance(10, 40f, 16, true), StreetRules.RecklessMost), "street: never more than 85 in 100");
        Check(StreetRules.RecklessChance(1, 1f, 12, false) < StreetRules.RecklessChance(2, 1f, 12, false), "street: the frequency option still means more or fewer");
        Check(Near(StreetRules.JaywalkChance(2, 1f, 12), StreetRules.Due(2, 1f, 12) * 0.4f * 3f, 0.00001f), "street: somebody steps out three times as often as somebody is due to be hit");
        Check(Near(StreetRules.JaywalkChance(10, 80f, 16), StreetRules.JaywalkMost), "street: and never at more than half of the looks");
    }

    private static void StreetPicking()
    {
        // The cars that may be the reckless one: CrimeCars' rule, by name.
        Check(StreetRules.IsOrdinaryCarName("AI_Sedan_02"), "street: a saloon may be picked");
        Check(StreetRules.IsOrdinaryCarName("TiaraVic"), "street: the big sedan may be picked");
        string[] never = { "AI_Ambulance", "Taxi_Yellow", "CityBus_01", "DeliveryTruck", "PoliceCar", "Limo_Black", "FireEngine", "GarbageTruck", "TowTruck", "MiniVan", "ai_AMBULANCE_b" };
        foreach (string name in never)
        {
            Check(!StreetRules.IsOrdinaryCarName(name), "street: '" + name + "' is never picked");
        }
        Check(!StreetRules.IsOrdinaryCarName(null) && !StreetRules.IsOrdinaryCarName(string.Empty), "street: a vehicle with no name is never picked");

        Check(!StreetRules.InPickRange(19.9f) && StreetRules.InPickRange(20f), "street: no car nearer than 20 m");
        Check(StreetRules.InPickRange(120f) && !StreetRules.InPickRange(120.1f), "street: none further than 120 m");

        // The kinds: nobody drives drunk by day.
        bool drunkByDay = false, drunkByNight = false;
        int speeders = 0, runners = 0, tailgaters = 0;
        for (int roll = 0; roll < 100; roll++)
        {
            StreetRules.Kind day = StreetRules.Pick(roll, false);
            drunkByDay |= day == StreetRules.Kind.Drunk;
            drunkByNight |= StreetRules.Pick(roll, true) == StreetRules.Kind.Drunk;
            speeders += day == StreetRules.Kind.Speeder ? 1 : 0;
            runners += day == StreetRules.Kind.RedLight ? 1 : 0;
            tailgaters += day == StreetRules.Kind.Tailgater ? 1 : 0;
        }
        Check(!drunkByDay, "street: no drunk driver by day");
        Check(drunkByNight, "street: drunk drivers at night");
        Check(speeders == 45 && runners == 30 && tailgaters == 25, "street: by day 45 speeders, 30 light runners, 25 late brakers in 100 (" + speeders + ", " + runners + ", " + tailgaters + ")");
        Check(StreetRules.PickForPeople(StreetRules.Kind.Speeder, 59) == StreetRules.Kind.RedLight && StreetRules.PickForPeople(StreetRules.Kind.Speeder, 60) == StreetRules.Kind.Speeder,
            "street: with somebody on foot due to be hit, 60 in 100 are light runners");

        Check(Near(StreetRules.Lasts(0f), 20f) && Near(StreetRules.Lasts(1f), 60f) && Near(StreetRules.Lasts(7f), 60f) && Near(StreetRules.Lasts(-1f), 20f), "street: a reckless spell lasts 20 to 60 s");
    }

    private static void StreetDriving()
    {
        // Pace: only the speeder is well over the traffic's.
        Check(StreetRules.Cruise(StreetRules.Kind.Speeder, 0f) >= 18f && StreetRules.Cruise(StreetRules.Kind.Speeder, 1f) < 24f, "street: a speeder does 65 to 85 km/h, below the driver's fast-drive speed");
        Check(StreetRules.Cruise(StreetRules.Kind.Drunk, 1f) <= 13f, "street: a drunk is no faster than the traffic");
        Check(StreetRules.Cruise(StreetRules.Kind.RedLight, 1f) <= 16f && StreetRules.Cruise(StreetRules.Kind.Tailgater, 1f) <= 15.5f, "street: the others are a little over it at the most");

        // Lights.
        Check(!StreetRules.ObeysLights(StreetRules.Kind.RedLight), "street: the red-light runner does not stop for a light");
        Check(StreetRules.ObeysLights(StreetRules.Kind.Speeder) && StreetRules.ObeysLights(StreetRules.Kind.Tailgater) && StreetRules.ObeysLights(StreetRules.Kind.Drunk), "street: every other kind does");

        // Braking and sight. An emergency driver sees about 12 m ahead at town speeds and 20 m at a speeder's, and stops 2.5 m short.
        const float gap = 2.5f;
        foreach (StreetRules.Kind kind in new[] { StreetRules.Kind.Speeder, StreetRules.Kind.RedLight, StreetRules.Kind.Tailgater, StreetRules.Kind.Drunk })
        {
            float fastest = StreetRules.Cruise(kind, 1f);
            float look = 12f + Math.Max(0f, fastest - 14f);
            float safe = StreetRules.StopsFrom(StreetRules.Stopping(false, 0.5f), StreetRules.Foresight(kind, false, 0.5f), look, gap);
            Check(safe > fastest, "street: a " + kind + " with a margin can stop from its fastest (" + fastest.ToString("0.0") + " m/s) for something standing in the road (stops from " + safe.ToString("0.0") + ")");
            float slowest = StreetRules.Cruise(kind, 0f);
            float slowLook = 12f + Math.Max(0f, slowest - 14f);
            for (int step = 0; step <= 4; step++)
            {
                float roll = step / 4f;
                float late = StreetRules.StopsFrom(StreetRules.Stopping(true, roll), StreetRules.Foresight(kind, true, roll), slowLook, gap);
                Check(late < slowest, "street: a " + kind + " with an accident due (roll " + roll.ToString("0.00") + ") cannot stop even from its slowest (" + slowest.ToString("0.0") + " m/s; stops from " + late.ToString("0.0") + ")");
            }
        }
        Check(StreetRules.Stopping(true, 0f) < StreetRules.Stopping(true, 1f) && StreetRules.Stopping(true, 1f) < StreetRules.Stopping(false, 0f), "street: braking with an accident due is always less than with a margin");
        Check(StreetRules.Foresight(StreetRules.Kind.Tailgater, true, 0f) < StreetRules.Foresight(StreetRules.Kind.Speeder, true, 0f), "street: the tailgater sees least of all");

        // Bends. Dry, a 10 m bend holds 9.2 m/s; a car is lost 20% above what the road holds.
        Check(Near(StreetRules.BendLimit(10f, 1f), 9.22f, 0.01f), "street: a 10 m bend holds 9.2 m/s in the dry");
        Check(StreetRules.BendLimit(10f, 0.7f) < StreetRules.BendLimit(10f, 1f), "street: and less in the wet");
        Check(!StreetRules.LosesIt(11f, 10f, 1f) && StreetRules.LosesIt(11.2f, 10f, 1f), "street: lost above 11.1 m/s (20% over)");
        Check(!StreetRules.LosesIt(40f, 60f, 1f), "street: a bend wider than 40 m is no bend");
        Check(!StreetRules.LosesIt(40f, 0f, 1f), "street: no radius, no bend");
        foreach (float grip in new[] { 1f, 0.85f, 0.7f, 0.5f })
        {
            foreach (StreetRules.Kind kind in new[] { StreetRules.Kind.Speeder, StreetRules.Kind.RedLight, StreetRules.Kind.Tailgater, StreetRules.Kind.Drunk })
            {
                float through = (float)Math.Sqrt(StreetRules.PlannedGrip(kind, false, 1f, grip) * 10f);   // the speed the driver plans a 10 m bend at
                Check(!StreetRules.LosesIt(through, 10f, grip), "street: a " + kind + " with a margin holds a 10 m bend at grip " + grip.ToString("0.00"));
            }
        }
        float pushed = (float)Math.Sqrt(StreetRules.PlannedGrip(StreetRules.Kind.Speeder, true, 1f, 1f) * 10f);
        Check(StreetRules.LosesIt(pushed, 10f, 1f), "street: a speeder with an accident due who pushes hardest loses a 10 m bend even in the dry");
        float mild = (float)Math.Sqrt(StreetRules.PlannedGrip(StreetRules.Kind.Speeder, true, 0f, 1f) * 10f);
        Check(!StreetRules.LosesIt(mild, 10f, 1f) && StreetRules.LosesIt(mild, 10f, 0.6f), "street: one who pushes least holds it in the dry and loses it in the wet");
        float steady = (float)Math.Sqrt(StreetRules.PlannedGrip(StreetRules.Kind.Tailgater, true, 1f, 0.6f) * 10f);
        Check(!StreetRules.LosesIt(steady, 10f, 0.6f), "street: a late braker corners as carefully as anybody, accident due or not");

        Check(StreetRules.Sway(false, 1f) <= 1f && StreetRules.Sway(true, 0f) >= 1.3f, "street: a drunk wanders within the lane, and over its edge when an accident is due");
    }

    private static void StreetOutcomes()
    {
        Check(StreetRules.Severity(0f) == 0 && StreetRules.Severity(7.9f) == 0, "street: under 8 m/s (29 km/h) a collision is minor");
        Check(StreetRules.Severity(8f) == 1 && StreetRules.Severity(13.9f) == 1, "street: from 8 m/s moderate");
        Check(StreetRules.Severity(14f) == 2 && StreetRules.Severity(40f) == 2, "street: from 14 m/s (50 km/h) severe");

        Check(StreetRules.Flees(StreetRules.Kind.Drunk, false, 59) && !StreetRules.Flees(StreetRules.Kind.Drunk, false, 60), "street: 60 in 100 drunks drive off after hurting somebody");
        Check(StreetRules.Flees(StreetRules.Kind.Tailgater, false, 19) && !StreetRules.Flees(StreetRules.Kind.Tailgater, false, 20), "street: 20 in 100 late brakers do");
        Check(StreetRules.Flees(StreetRules.Kind.Tailgater, true, 29) && !StreetRules.Flees(StreetRules.Kind.Tailgater, true, 30), "street: 10 more when somebody died");

        Check(StreetRules.PatrolNotices(StreetRules.Kind.Speeder) && StreetRules.PatrolNotices(StreetRules.Kind.RedLight), "street: a patrol car goes after speed and red lights");
        Check(!StreetRules.PatrolNotices(StreetRules.Kind.Tailgater) && !StreetRules.PatrolNotices(StreetRules.Kind.Drunk), "street: and not after what it cannot see from the road");
        Check(StreetRules.PullsOver(64) && !StreetRules.PullsOver(65), "street: 65 in 100 stop for the patrol car");

        Check(StreetRules.NearMiss(8f, 0f, 0.95f, 10f), "street: 8 m ahead of a car doing 10 m/s is a near miss");
        Check(!StreetRules.NearMiss(8f, 0f, 0.95f, 5f), "street: not at walking pace");
        Check(!StreetRules.NearMiss(8f, 2f, 0.95f, 10f) && !StreetRules.NearMiss(10f, 0f, 0.95f, 10f), "street: nor beside its way, nor further ahead");

        // Stepping out: the car covers the walk to the lane, and then has room to stop in, or has not.
        float walk = 3.5f / StreetRules.WalkPace;   // 2.5 s to a lane 3.5 m away
        float inTime = StreetRules.JaywalkCarDistance(10f, 3.5f, false);
        float late = StreetRules.JaywalkCarDistance(10f, 3.5f, true);
        Check(Near(inTime, 10f * walk + 10f + 8f, 0.01f), "street: a driver at 10 m/s is left their stopping distance and 8 m (" + inTime.ToString("0.0") + " m)");
        Check(Near(late, 10f * walk + 2f, 0.01f), "street: somebody due to be hit leaves them 2 m (" + late.ToString("0.0") + " m)");
        Check(inTime - 10f * walk > 10f * 10f / (2f * StreetRules.TrafficBraking), "street: in time means more room than the car needs to stop");
        Check(late - 10f * walk < 10f * 10f / (2f * StreetRules.TrafficBraking), "street: late means less");
        Check(StreetRules.JaywalkCarDistance(10f, -1f, true) >= 0f, "street: never a negative distance");
    }
}
