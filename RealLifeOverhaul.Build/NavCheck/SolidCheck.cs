using System;
using System.Diagnostics;
using RealLifeOverhaul;

// Checks for the rules of "traffic sees people in the road" (Traffic/Solid/SolidRules.cs, compiled as it ships): how much
// road a driver needs, what they make of somebody ahead and how hard they brake, when somebody waits at the kerb, the
// strip ahead of a car and the grid the people are put in (against a look at every person, on random streets), who gets
// one of the few stand-ins and that none jumps from person to person, when the horn sounds and how often, how long a car
// may be held, what is a bump and what a hit, the moment somebody is sent out to be close or too late, how many people
// can be hit in an hour at each setting, and, driven step by step as the game would drive it, that a careful car and a
// pedestrian who keeps the kerb rule never meet: 100,000 random cases. And what a look at 40 cars and 60 people costs.
//
// What is not here: anything of the game. Which layers its cars sense, whether the stand-ins are read by them, what the
// car does once the game holds it and what the people do about a horn are for the game to show, in the "Traffic check"
// lines of Player.log.
internal static partial class Program
{
    private static void Solid()
    {
        SolidStopping();
        SolidDriver();
        SolidKerb();
        SolidStrip();
        SolidGrid();
        SolidProxies();
        SolidHornAndHold();
        SolidContact();
        SolidStepOut();
        SolidRate();
        SolidNeverStrikes();
        SolidTiming();
    }

    private static void SolidStopping()
    {
        Check(SolidRules.StoppingDistance(0f, 5f) == 0f && SolidRules.StoppingDistance(-3f, 5f) == 0f, "solid: a car that stands needs no road to stop in");
        Check(Near(SolidRules.StoppingDistance(10f, 5f), 10f), "solid: 10 m/s with 5 m/s2 stops in 10 m");
        Check(Near(SolidRules.StoppingDistance(20f, 5f), 40f), "solid: twice the speed, four times the road");
        Check(Near(SolidRules.StoppingDistance(14f, SolidRules.HardMost), 10.89f), "solid: 50 km/h with all the tyres give stops in 10.9 m");
        Check(SolidRules.StoppingDistance(10f, 0f) < float.PositiveInfinity, "solid: no braking at all is still a number");

        Check(Near(SolidRules.Sight(0f), SolidRules.SightLeast), "solid: a standing car looks 4.5 m ahead");
        Check(Near(SolidRules.Sight(10f), 10f * 0.4f + 100f / 7f * 1.3f + 4.5f, 0.01f), "solid: at 36 km/h a car looks 27 m ahead: reaction, an ordinary stop and a third, the gap");
        Check(SolidRules.Sight(10f) > 10f * SolidRules.Reaction + SolidRules.StoppingDistance(10f, SolidRules.Comfort) + SolidRules.Gap, "solid: that is more than an ordinary stop takes");
        Check(Near(SolidRules.Sight(40f), SolidRules.SightMost) && SolidRules.Sight(-5f) == SolidRules.SightLeast, "solid: never over 60 m, and a negative speed is none");
        bool grows = true;
        for (float speed = 0f; speed < 30f; speed += 0.5f)
        {
            grows &= SolidRules.Sight(speed + 0.5f) >= SolidRules.Sight(speed);
        }
        Check(grows, "solid: the faster, the further it looks");

        Check(Near(SolidRules.GapFor(2.4f), 2.5f), "solid: a sensor that reaches 2.4 m beyond the nose leaves a 2.5 m gap");
        Check(Near(SolidRules.GapFor(6f), 6.1f), "solid: one that reaches 6 m leaves 6.1 m: where it has the person's stand-in");
        Check(SolidRules.GapFor(0f) == SolidRules.GapLeast && SolidRules.GapFor(-2f) == SolidRules.GapLeast, "solid: never under 0.8 m");
        Check(SolidRules.GapFor(30f) == SolidRules.GapMost, "solid: never over 8 m");
        Check(SolidRules.GapFor(4f) - 4f < SolidRules.ProxyRadius, "solid: at the gap the sensor's far end is inside the stand-in");
        Check(Near(SolidRules.SightFor(10f, 2.5f), SolidRules.Sight(10f)) && Near(SolidRules.SightFor(10f, 6f), SolidRules.Sight(10f) + 3.5f), "solid: a longer gap is looked that much further ahead for");
    }

    private static void SolidDriver()
    {
        Check(SolidRules.Need(0f, 5f, 2.5f) == 0f, "solid: a standing car needs no braking");
        Check(Near(SolidRules.Need(10f, 12.5f, 2.5f), 5f), "solid: 10 m/s with 10 m of room needs 5 m/s2");
        Check(SolidRules.Need(10f, 2.5f, 2.5f) > SolidRules.HardMost && SolidRules.Need(10f, 1f, 2.5f) < float.PositiveInfinity, "solid: no room left is more than the tyres give, and still a number");

        Check(SolidRules.Judge(10f, 40f, 2.5f) == SolidRules.Driver.Clear, "solid: somebody 40 m ahead at 36 km/h is nothing yet");
        Check(SolidRules.Judge(10f, 20f, 2.5f) == SolidRules.Driver.Ease, "solid: 20 m ahead is an ordinary stop (2.9 m/s2)");
        Check(SolidRules.Judge(10f, 10f, 2.5f) == SolidRules.Driver.Hard, "solid: 10 m ahead is a hard brake (6.7 m/s2)");
        Check(SolidRules.Judge(10f, 6f, 2.5f) == SolidRules.Driver.Late, "solid: 6 m ahead is too late (14 m/s2)");
        Check(SolidRules.Judge(0f, 1f, 2.5f) == SolidRules.Driver.Clear, "solid: a standing car judges nothing");
        Check(SolidRules.Judge(0.8f, 2.5f, 2.5f) == SolidRules.Driver.Ease && SolidRules.Judge(1.2f, 2.5f, 2.5f) == SolidRules.Driver.Late, "solid: at a crawl the end of a stop is never a hard brake; above it, a car at its gap and still moving is too late");
        Check(SolidRules.Need(0.5f, 2.4f, 2.5f) == SolidRules.Overrun && SolidRules.Brake(0.5f, 2.4f, 2.5f, true, false) == SolidRules.LastResort, "solid: a car inside its gap and still moving is braked with all it may be: it does not creep on");
        float exact = 2.5f + SolidRules.StoppingDistance(10f, SolidRules.HardMost);
        Check(SolidRules.Judge(10f, exact + 0.01f, 2.5f) == SolidRules.Driver.Hard && SolidRules.Judge(10f, exact - 0.05f, 2.5f) == SolidRules.Driver.Late, "solid: the line between a hard brake and too late is what the tyres give");

        Check(SolidRules.Brake(10f, 40f, 2.5f, false, false) == 0f, "solid: with room, a driver who has not begun does not brake");
        Check(SolidRules.Brake(10f, 40f, 2.5f, true, false) > 0f, "solid: one who has begun keeps at it (the car does not come on again)");
        Check(Near(SolidRules.Brake(10f, 12.5f, 2.5f, false, false), 5f), "solid: the braking is exactly what stops it at the gap");
        Check(SolidRules.Brake(10f, 3f, 2.5f, false, false) == SolidRules.LastResort, "solid: a careful driver may use whatever it takes");
        Check(SolidRules.Brake(10f, 3f, 2.5f, false, true) == SolidRules.HardMost, "solid: one with an accident owed has only what the tyres give");
        Check(SolidRules.Brake(0f, 3f, 2.5f, true, false) == 0f, "solid: a standing car is not braked");
        Check(SolidRules.EaseFrom < SolidRules.Comfort && SolidRules.Comfort < SolidRules.HardFrom && SolidRules.HardFrom < SolidRules.HardMost && SolidRules.HardMost < SolidRules.LastResort,
            "solid: begins to brake under an ordinary stop's rate, ordinary under hard, hard under the tyres' most, that under the last resort");
        Check(SolidRules.StoppingDistance(13.9f, SolidRules.LastResort) > 1.25f, "solid: the last resort is gentler than the game's own stop (50 km/h in 1.25 m)");

        Check(!SolidRules.MayStrike(false, 10f, 3f, 2.5f) && !SolidRules.MayStrike(false, 25f, 0.1f, 2.5f), "solid: with nothing owed no car may reach anybody, however late");
        Check(SolidRules.MayStrike(true, 10f, 6f, 2.5f), "solid: with one owed, a driver who cannot stop reaches them");
        Check(!SolidRules.MayStrike(true, 10f, 10f, 2.5f) && !SolidRules.MayStrike(true, 10f, 40f, 2.5f), "solid: and one who can stop, stops, owed or not");
    }

    private static void SolidKerb()
    {
        float safe = SolidRules.SafeToStep(10f);
        Check(Near(safe, 10f * 0.525f + 10f + 0.8f + 0.3f + 0.5f, 0.01f), "solid: at 36 km/h nobody steps out nearer than 16.9 m: a look, the reaction, a firm stop, the least gap, a step to spare");
        Check(SolidRules.MustWait(10f, safe - 0.1f) && !SolidRules.MustWait(10f, safe + 0.1f), "solid: nearer than that they wait, further they may go");
        Check(!SolidRules.MustWait(0.2f, 0.5f), "solid: nobody waits for a car that stands");
        Check(SolidRules.SafeToStep(-3f) == SolidRules.SafeToStep(0f), "solid: a negative speed is none");
        Check(SolidRules.SafeToStep(14f) > SolidRules.StoppingDistance(14f, SolidRules.HardMost) + 14f * SolidRules.Reaction, "solid: the kerb rule asks for more road than the car needs at its hardest");
        Check(Near(SolidRules.WaitFor(10f, 20f), 3f) && SolidRules.WaitFor(1f, 50f) == SolidRules.WaitMost && Near(SolidRules.WaitFor(10f, -4f), 1f), "solid: they wait until the car has passed and a second more, four at the most");

        float half = 0.95f + SolidRules.StripMargin;
        Check(SolidRules.StepsIn(half + 0.5f, -1.4f, half), "solid: half a metre from the strip's right edge, walking in at 1.4 m/s: about to step out");
        Check(SolidRules.StepsIn(-(half + 0.5f), 1.4f, half), "solid: the same from the left");
        Check(!SolidRules.StepsIn(half + 0.5f, 1.4f, half), "solid: walking away from it is not");
        Check(!SolidRules.StepsIn(half + 0.5f, -0.2f, half), "solid: drifting along the kerb is not");
        Check(!SolidRules.StepsIn(half + 1.1f, -0.6f, half), "solid: nor somebody who would not be in it within a second");
        Check(!SolidRules.StepsIn(half + SolidRules.StepBand + 0.1f, -3f, half), "solid: nor somebody more than a step and a bit away, however fast");
        Check(!SolidRules.StepsIn(half - 0.1f, -1.4f, half), "solid: somebody inside the strip already is not about to step in");
    }

    private static void SolidStrip()
    {
        SolidRules.Local(10f, 5f, 0f, 0f, 1f, 0f, out float ahead, out float side);
        Check(Near(ahead, 10f) && Near(side, -5f), "solid: for a car facing east, north is on its left (negative side)");
        SolidRules.Local(3f, 8f, 1f, 2f, 0f, 1f, out ahead, out side);
        Check(Near(ahead, 6f) && Near(side, 2f), "solid: for a car facing north, east is on its right");
        float diagonal = (float)Math.Sqrt(0.5);
        SolidRules.Local(5f, 5f, 0f, 0f, diagonal, diagonal, out ahead, out side);
        Check(Near(ahead, 7.071f, 0.001f) && Near(side, 0f, 0.001f), "solid: dead ahead of a car facing north-east");

        Check(SolidRules.InStrip(5f, 0f, 2f, 20f, 1.45f), "solid: 5 m ahead on the car's line is in its strip");
        Check(SolidRules.InStrip(2f, 1.45f, 2f, 20f, 1.45f) && SolidRules.InStrip(20f, -1.45f, 2f, 20f, 1.45f), "solid: the strip's edges count");
        Check(!SolidRules.InStrip(1.99f, 0f, 2f, 20f, 1.45f) && !SolidRules.InStrip(20.01f, 0f, 2f, 20f, 1.45f), "solid: behind its start and beyond its end do not");
        Check(!SolidRules.InStrip(5f, 1.46f, 2f, 20f, 1.45f) && !SolidRules.InStrip(5f, -1.46f, 2f, 20f, 1.45f), "solid: nor beside it");

        // How wide the strip is, and who in it is in the way.
        Check(Near(SolidRules.HalfStrip(0.95f), 1.45f), "solid: a car 1.9 m wide has a strip 1.45 m either side of its line");
        Check(Near(SolidRules.HalfStrip(1.1f), 1.5f), "solid: the margin ends 1.5 m from the line: the kerb of a lane is beyond it");
        Check(Near(SolidRules.HalfStrip(1.45f), 1.6f), "solid: a bus still has a hand's width beyond its side");
        Check(SolidRules.HalfStrip(1.25f) < 1.65f, "solid: somebody walking the kerb of a 3.3 m lane is not in a truck's strip");
        Check(SolidRules.InWay(1.4f, 0.95f, true) && SolidRules.InWay(-1.4f, 0.95f, true), "solid: somebody walking 1.4 m off a car's line is in its way");
        Check(!SolidRules.InWay(1.4f, 0.95f, false), "solid: somebody standing there (at the kerb, waiting for their light) is not");
        Check(SolidRules.InWay(1.2f, 0.95f, false) && SolidRules.InWay(0f, 0.95f, false), "solid: somebody standing where the car's body would touch them is");
        Check(!SolidRules.InWay(1.5f, 0.95f, true), "solid: beyond the strip nobody is");
    }

    private static void SolidGrid()
    {
        var grid = new SolidRules.PeopleGrid(SolidRules.MostPeople);
        var found = new int[64];
        var ahead = new float[64];
        var side = new float[64];

        grid.Begin(0f, 0f);
        grid.Build();
        Check(grid.Count == 0 && grid.Strip(0f, 0f, 1f, 0f, 0f, 30f, 2f, found, ahead, side) == 0, "solid: an empty grid finds nobody");

        grid.Begin(100f, -50f);
        Check(grid.Add(100f, -50f) == 0 && grid.Add(4f, -50f) == 1, "solid: the window is 96 m each way round the player");
        Check(grid.Add(3.9f, -50f) == -1 && grid.Add(196f, -50f) == -1 && grid.Add(100f, 46f) == -1 && grid.Add(100f, -146.1f) == -1, "solid: outside it nobody is taken");
        grid.Build();
        Check(grid.Strip(90f, -50f, 1f, 0f, 0f, 20f, 1f, found, ahead, side) == 1 && found[0] == 0 && Near(ahead[0], 10f) && Near(side[0], 0f), "solid: one person 10 m ahead of a car is found, with how far");

        var small = new SolidRules.PeopleGrid(3);
        small.Begin(0f, 0f);
        Check(small.Add(1f, 1f) == 0 && small.Add(2f, 2f) == 1 && small.Add(3f, 3f) == 2 && small.Add(4f, 4f) == -1, "solid: a full grid takes nobody more");

        // Against a look at every person: random streets, people exactly on square borders, cars at any heading.
        var random = new Random(20261011);
        bool agrees = true;
        int strips = 0, hits = 0;
        var xs = new float[SolidRules.MostPeople];
        var zs = new float[SolidRules.MostPeople];
        for (int round = 0; round < 400 && agrees; round++)
        {
            float px = (float)(random.NextDouble() * 2000.0 - 1000.0);
            float pz = (float)(random.NextDouble() * 2000.0 - 1000.0);
            grid.Begin(px, pz);
            int people = random.Next(0, 120);
            for (int i = 0; i < people; i++)
            {
                float x, z;
                if (i % 5 == 0)
                {
                    // exactly on a border between two squares (and every fourth of these on a corner of four)
                    x = px - 96f + 8f * random.Next(1, 23);
                    z = i % 20 == 0 ? pz - 96f + 8f * random.Next(1, 23) : pz + (float)(random.NextDouble() * 180.0 - 90.0);
                }
                else
                {
                    x = px + (float)(random.NextDouble() * 190.0 - 95.0);
                    z = pz + (float)(random.NextDouble() * 190.0 - 95.0);
                }
                int person = grid.Add(x, z);
                if (person >= 0)
                {
                    xs[person] = x;
                    zs[person] = z;
                }
            }
            grid.Build();
            for (int car = 0; car < 12 && agrees; car++)
            {
                double heading = car < 4 ? car * Math.PI / 2.0 : random.NextDouble() * Math.PI * 2.0;
                float fx = (float)Math.Cos(heading), fz = (float)Math.Sin(heading);
                float cx = px + (float)(random.NextDouble() * 170.0 - 85.0);
                float cz = pz + (float)(random.NextDouble() * 170.0 - 85.0);
                float from = (float)(random.NextDouble() * 3.0);
                float to = from + (float)(random.NextDouble() * 60.0);
                float halfWidth = 0.8f + (float)(random.NextDouble() * 2.5);
                int count = grid.Strip(cx, cz, fx, fz, from, to, halfWidth, found, ahead, side);
                int brute = 0;
                long sum = 0, bruteSum = 0;
                for (int i = 0; i < grid.Count; i++)
                {
                    SolidRules.Local(xs[i], zs[i], cx, cz, fx, fz, out float a, out float s);
                    if (SolidRules.InStrip(a, s, from, to, halfWidth))
                    {
                        brute++;
                        bruteSum += i + 1;
                    }
                }
                for (int i = 0; i < count; i++)
                {
                    sum += found[i] + 1;
                }
                agrees &= count == brute && sum == bruteSum;
                strips++;
                hits += count;
            }
        }
        Check(agrees, "solid: the grid finds exactly whom a look at every person finds (" + strips + " strips, " + hits + " people found, borders and corners among them)");
        Check(hits > 500, "solid: and that was no empty test");

        // The same look twice gives the same answer.
        grid.Begin(0f, 0f);
        for (int i = 0; i < 60; i++)
        {
            grid.Add((float)(random.NextDouble() * 100.0 - 50.0), (float)(random.NextDouble() * 100.0 - 50.0));
        }
        grid.Build();
        var again = new int[64];
        int first = grid.Strip(-40f, 0f, 1f, 0f, 0f, 60f, 3f, found, ahead, side);
        int second = grid.Strip(-40f, 0f, 1f, 0f, 0f, 60f, 3f, again, ahead, side);
        bool same = first == second;
        for (int i = 0; i < first && same; i++)
        {
            same = found[i] == again[i];
        }
        Check(same, "solid: the same strip twice finds the same people in the same order");

        // More people in a strip than the caller has room for: as many as there is room for, and no fault.
        grid.Begin(0f, 0f);
        for (int i = 0; i < 100; i++)
        {
            grid.Add(i * 0.2f, 0f);
        }
        grid.Build();
        var few = new int[8];
        Check(grid.Strip(-5f, 0f, 1f, 0f, 0f, 60f, 1f, few, new float[8], new float[8]) == 8, "solid: a crowd in one strip fills the room there is, no more");
    }

    private static void SolidProxies()
    {
        var pool = new SolidRules.ProxyPool(4);
        var freed = new bool[4];
        var given = new bool[4];
        var tags = new int[4];

        pool.Begin();
        pool.Share(freed, given, tags);
        Check(pool.InUse == 0 && !given[0] && !freed[0], "solid: nobody wanted, no stand-in out");

        pool.Begin();
        pool.Want(11, 3f, 100);
        pool.Want(12, 1f, 101);
        pool.Share(freed, given, tags);
        Check(pool.InUse == 2 && pool.Holder(0) == 12 && pool.Holder(1) == 11 && given[0] && given[1] && tags[0] == 101 && tags[1] == 100, "solid: the most pressing takes the first stand-in");

        // The next look: the same two, the other way round in urgency, and a third. Nobody changes stand-in.
        pool.Begin();
        pool.Want(11, 0.5f, 200);
        pool.Want(13, 0.2f, 202);
        pool.Want(12, 4f, 201);
        pool.Share(freed, given, tags);
        Check(pool.Holder(0) == 12 && pool.Holder(1) == 11 && pool.Holder(2) == 13, "solid: whoever had one keeps the same one, however the order changes");
        Check(!given[0] && !given[1] && given[2] && !freed[0] && !freed[1] && !freed[2], "solid: only the newcomer's is newly given");
        Check(tags[0] == 201 && tags[1] == 200 && tags[2] == 202, "solid: each holder's number for this look comes back with them");

        // One leaves: theirs is freed, the others stay put.
        pool.Begin();
        pool.Want(12, 4f, 0);
        pool.Want(13, 0.2f, 1);
        pool.Share(freed, given, tags);
        Check(pool.Holder(0) == 12 && pool.Holder(1) == 0 && pool.Holder(2) == 13 && freed[1] && !freed[0] && !freed[2] && pool.InUse == 2, "solid: somebody who has left the strip gives theirs up, and nobody else moves");

        // More wanted than there are: the most pressing four, never more.
        pool.Begin();
        for (int i = 0; i < 9; i++)
        {
            pool.Want(50 + i, 9f - i, i);
        }
        pool.Share(freed, given, tags);
        bool top = true;
        for (int slot = 0; slot < 4; slot++)
        {
            top &= pool.Holder(slot) >= 55;
        }
        Check(pool.InUse == 4 && top, "solid: nine wanted, four stand-ins: the four most pressing have them");
        Check(freed[0] && given[0] && freed[2] && given[2], "solid: a stand-in can be taken from one person and given to another at the same look");

        // The same nine again: nothing moves at all.
        pool.Begin();
        for (int i = 8; i >= 0; i--)
        {
            pool.Want(50 + i, 9f - i, i);
        }
        int[] before = { pool.Holder(0), pool.Holder(1), pool.Holder(2), pool.Holder(3) };
        pool.Share(freed, given, tags);
        bool still = true;
        for (int slot = 0; slot < 4; slot++)
        {
            still &= pool.Holder(slot) == before[slot] && !freed[slot] && !given[slot];
        }
        Check(still, "solid: the same people at the next look, asked for in another order: no stand-in moves");

        pool.Begin();
        pool.Want(0, 1f, 0);
        pool.Want(77, 5f, 1);
        pool.Want(77, 2f, 2);
        pool.Want(77, 8f, 3);
        pool.Share(freed, given, tags);
        int holders = 0, tag = -1;
        for (int slot = 0; slot < 4; slot++)
        {
            if (pool.Holder(slot) == 77)
            {
                holders++;
                tag = tags[slot];
            }
        }
        Check(pool.InUse == 1 && holders == 1 && tag == 2, "solid: nobody (0) gets none, and one person wanted three times gets one, by their most pressing car");

        pool.Clear(freed);
        Check(pool.InUse == 0 && pool.Holder(0) == 0 && (freed[0] || freed[1] || freed[2] || freed[3]), "solid: all are taken back at once, and each that was out is said to be freed");

        // A long run of random looks: never more than the pool, no person on two, and nobody who stays wanted and stays among the most pressing loses theirs.
        var random = new Random(7);
        var big = new SolidRules.ProxyPool(SolidRules.Proxies);
        var bigFreed = new bool[SolidRules.Proxies];
        var bigGiven = new bool[SolidRules.Proxies];
        var bigTags = new int[SolidRules.Proxies];
        var had = new int[SolidRules.Proxies];
        bool sound = true;
        for (int look = 0; look < 3000 && sound; look++)
        {
            for (int slot = 0; slot < SolidRules.Proxies; slot++)
            {
                had[slot] = big.Holder(slot);
            }
            big.Begin();
            int wanted = random.Next(0, 12);
            for (int i = 0; i < wanted; i++)
            {
                big.Want(1 + random.Next(0, 14), (float)random.NextDouble() * 5f, i);
            }
            big.Share(bigFreed, bigGiven, bigTags);
            int used = 0;
            for (int slot = 0; slot < SolidRules.Proxies; slot++)
            {
                int holder = big.Holder(slot);
                used += holder != 0 ? 1 : 0;
                for (int other = slot + 1; other < SolidRules.Proxies && holder != 0; other++)
                {
                    sound &= big.Holder(other) != holder;
                }
                // (Fewer are ever wanted here than there are stand-ins: so whoever keeps theirs keeps the same one.)
                sound &= had[slot] == 0 || holder == 0 || holder == had[slot] || bigFreed[slot];
            }
            sound &= used == big.InUse && used <= SolidRules.Proxies && used <= wanted;
        }
        Check(sound, "solid: 3,000 random looks: never more than sixteen out, nobody on two, and no stand-in changes hands without being freed first");
    }

    private static void SolidHornAndHold()
    {
        SolidRules.Driver clear = SolidRules.Driver.Clear, ease = SolidRules.Driver.Ease, hard = SolidRules.Driver.Hard, late = SolidRules.Driver.Late;
        Check(SolidRules.HornDue(hard, clear, 100f) && SolidRules.HornDue(hard, ease, 100f) && SolidRules.HornDue(late, clear, 100f), "solid: the horn sounds at the look at which it became a hard brake");
        Check(!SolidRules.HornDue(hard, hard, 100f) && !SolidRules.HornDue(late, hard, 100f), "solid: not again while it stays one");
        Check(!SolidRules.HornDue(ease, clear, 100f) && !SolidRules.HornDue(clear, hard, 100f), "solid: and not for an ordinary stop");
        Check(!SolidRules.HornDue(hard, clear, 3.9f) && SolidRules.HornDue(hard, clear, 4f), "solid: never within four seconds of the car's last");
        Check(SolidRules.HornEvery >= 2f, "solid: which is no less than the game's own two seconds between horns");

        Check(!SolidRules.DawdleHorn(2.9f, 100f) && SolidRules.DawdleHorn(3f, 100f), "solid: a car that has stood behind somebody for three seconds sounds its horn");
        Check(!SolidRules.DawdleHorn(10f, 3.9f) && SolidRules.DawdleHorn(10f, 4f), "solid: and again every four seconds, no sooner");
        int horns = 0;
        float last = -100f;
        for (float held = 0f; held <= SolidRules.HoldMost; held += SolidRules.TickSeconds)
        {
            if (SolidRules.DawdleHorn(held, held - last))
            {
                horns++;
                last = held;
            }
        }
        Check(horns == 3, "solid: three horns in the twelve seconds a car may be held (" + horns + ")");

        Check(SolidRules.Held(0.2f, 3f, 2.5f) && SolidRules.Held(0f, 6f, 2.5f), "solid: a standing car with somebody up to 6 m ahead of a 2.5 m gap is held");
        Check(!SolidRules.Held(0.6f, 3f, 2.5f) && !SolidRules.Held(0f, 6.1f, 2.5f), "solid: one that moves, or has them further off, is not");
        Check(SolidRules.Held(0f, 11f, 8f), "solid: a longer gap is allowed for");
        Check(!SolidRules.MoveOn(11.9f) && SolidRules.MoveOn(12f), "solid: after 12 s the person is moved on");
        Check(!SolidRules.Remove(15.9f, false) && SolidRules.Remove(16f, false) && !SolidRules.Remove(60f, true), "solid: after 16 s somebody nobody can see is put elsewhere; never somebody in view");
        Check(SolidRules.HoldMost < 20f && SolidRules.RemoveAfter < 20f, "solid: both before the game itself would take a standing car off the road (20 s)");

        // Two cars in one look each held by somebody nobody can see: both people go back to the pool, each once.
        var queue = new string[SolidRules.PutAwayMost];
        int queued = 0;
        string first = "first", second = "second";
        bool both = SolidRules.Queue(queue, ref queued, first) && SolidRules.Queue(queue, ref queued, second);
        Check(both && queued == 2 && queue[0] == first && queue[1] == second, "solid: two people to put away in one look are both kept (" + queued + ")");
        Check(SolidRules.Queue(queue, ref queued, first) && queued == 2, "solid: one person holding two cars is put away once");
        for (int i = queued; i < SolidRules.PutAwayMost; i++)
        {
            SolidRules.Queue(queue, ref queued, "more" + i);
        }
        Check(!SolidRules.Queue(queue, ref queued, "one too many") && queued == SolidRules.PutAwayMost && queue[0] == first,
            "solid: past " + SolidRules.PutAwayMost + " in one look the car waits for the next look, and nobody queued is dropped");
    }

    private static void SolidContact()
    {
        Check(SolidRules.Touch(0f) == SolidRules.Contact.None && SolidRules.Touch(0.59f) == SolidRules.Contact.None, "solid: a car that has come to rest against somebody has done nothing");
        Check(SolidRules.Touch(0.6f) == SolidRules.Contact.Bump && SolidRules.Touch(2.19f) == SolidRules.Contact.Bump, "solid: at walking pace it is a bump");
        Check(SolidRules.Touch(2.2f) == SolidRules.Contact.Strike && SolidRules.Touch(20f) == SolidRules.Contact.Strike, "solid: from 8 km/h it is a hit");
        Check(SolidRules.Watch(10f, 10.9f) && !SolidRules.Watch(10f, 11.1f), "solid: a car within a second (and a metre) of somebody is watched");
        Check(!SolidRules.Watch(0.3f, 0.5f), "solid: one that hardly moves is not");
        Check(SolidRules.Watched <= 8 && SolidRules.Brakes <= 8 && SolidRules.Proxies == 16, "solid: a handful of cars watched and braked, sixteen stand-ins");
        Check(SolidRules.ProxyRadius > SolidRules.StripMargin, "solid: a stand-in is wider than the strip's margin, so whoever is in the strip is in the sensor");
    }

    private static void SolidStepOut()
    {
        // The moment somebody at the kerb sets off, for a hard brake or for none.
        float walk = (4f - SolidRules.KerbToStrip) / StreetRules.WalkPace;
        float close = SolidRules.StepOutAt(10f, 4f, false);
        float late = SolidRules.StepOutAt(10f, 4f, true);
        Check(Near(close, 10f * walk + 3f + 100f / 13f + 2.5f + 2.3f, 0.01f), "solid: close: the car covers their walk to its strip, then has a hard stop's road (" + close.ToString("0.0") + " m up the lane)");
        Check(Near(late, 10f * walk + 3f + 1f + 2.3f, 0.01f), "solid: late: a third of a second and a metre (" + late.ToString("0.0") + " m)");
        float leftClose = close - 10f * walk - SolidRules.NoseFromMiddle;
        float leftLate = late - 10f * walk - SolidRules.NoseFromMiddle;
        Check(SolidRules.Judge(10f, leftClose - 10f * SolidRules.SeenAfter, SolidRules.Gap) == SolidRules.Driver.Hard, "solid: seen then, close is a hard brake");
        Check(SolidRules.Judge(10f, leftLate, SolidRules.Gap) == SolidRules.Driver.Late, "solid: and late is too late");
        Check(SolidRules.StepOutAt(10f, 0.5f, true) == SolidRules.StepOutAt(10f, 1.5f, true), "solid: somebody at the strip's edge already has no walk to it");
        Check(SolidRules.StepOutAt(-5f, 4f, false) == SolidRules.StepOutAt(0f, 4f, false), "solid: a negative speed is none");
        Check(SolidRules.StepOutAt(10f, 4f, true, 2.8f) < late && Near(late - SolidRules.StepOutAt(10f, 4f, true, 2.8f), 10f * walk * 0.5f, 0.01f), "solid: somebody who walks twice as fast sets off with the car half their walk nearer");
        Check(SolidRules.StepOutAt(10f, 4f, true, 0f) < float.PositiveInfinity, "solid: no pace at all is still a number");
        bool ordered = true;
        for (float speed = 3f; speed < 20f; speed += 1f)
        {
            ordered &= SolidRules.StepOutAt(speed, 4f, true) < SolidRules.StepOutAt(speed, 4f, false)
                       && SolidRules.Judge(speed, SolidRules.StepOutAt(speed, 4f, true) - speed * walk - SolidRules.NoseFromMiddle, SolidRules.Gap) == SolidRules.Driver.Late;
        }
        Check(ordered, "solid: at every speed late is nearer than close, and too late for the driver");
    }

    private static void SolidRate()
    {
        // The pedestrians' share of the accident bank: the most that ordinary traffic and reckless drivers together can hit.
        Check(Near(SolidRules.StrikesPerHourMost(1, 1f), 0.5f), "solid: frequency 1 owes 0.5 pedestrians an hour of play at the most (1.25 accidents, 0.4 of them people)");
        Check(Near(SolidRules.StrikesPerHourMost(3, 1f), 1.125f) && Near(SolidRules.StrikesPerHourMost(10, 1f), 19.22f, 0.05f), "solid: frequency 3: 1.1; frequency 10: about 19");
        Check(SolidRules.StrikesPerHourMost(0, 1f) == 0f && SolidRules.StrikesPerHourMost(3, 0f) == 0f, "solid: none at frequency 0");
        Check(Near(SolidRules.StrikesPerHourMost(3, 1.5f), 1.6875f), "solid: bad weather raises it as it raises every accident");

        // The bank itself, as the director keeps it: what falls due at each look, kept until it is a whole person, never more than 1.1.
        // At 20:00 the road is about as busy as the day's average (1.05 of it).
        float owing = 0f;
        int owed = 0, looks = 0;
        float perLook = StreetRules.Due(3, 1f, 20) * StreetRules.PeopleShare;
        while (owing < 1f)
        {
            owing = StreetRules.Accrue(owing, 3, 1f, 20, StreetRules.PeopleShare);
            looks++;
        }
        Check(looks == (int)Math.Ceiling(1f / perLook - 0.001f) || looks == (int)Math.Ceiling(1f / perLook - 0.001f) + 1,
            "solid: at frequency 3 a pedestrian is first owed after " + looks + " looks, " + (looks / 3) + " minutes of street time");
        const int span = 1800;   // ten hours of play
        for (int look = 0; look < span; look++)
        {
            owing = StreetRules.Accrue(owing, 3, 1f, 20, StreetRules.PeopleShare);
            if (owing >= 1f)
            {
                owing -= 1f;   // somebody was hit at the first chance: the most there can be
                owed++;
            }
        }
        int most = (int)Math.Ceiling(span * perLook);
        Check(owed >= most - 1 && owed <= most, "solid: paid out at every chance, that is " + owed + " in ten hours of play, never more than the bank holds (" + most + ")");
        owing = 0f;
        for (int look = 0; look < 5000; look++)
        {
            owing = StreetRules.Accrue(owing, 10, 2f, 16, StreetRules.PeopleShare);
        }
        Check(owing <= StreetRules.DueMost, "solid: with nobody ever hit the debt stops at 1.1: an hour without one is not followed by a string of them");
        Check(!SolidRules.MayStrike(owing - 1f >= 1f, 10f, 3f, 2.5f), "solid: so two can never be owed at once");
    }

    // A car driven step by step as the game would drive it (50 steps a second, the traffic system adding its
    // acceleration step whenever it is not braked), first seen by the driver up to a tick late; somebody steps into its
    // way no nearer than the kerb rule allows. With nothing owed it must never reach them. How near it came is returned.
    private static float SolidDrive(float speed, float distance, float gap, float power, float seenAfter, bool owed, out float hardest, out float impact)
    {
        const float step = 0.02f;
        bool braking = false;
        float time = 0f;
        float nearest = distance;
        hardest = 0f;
        impact = 0f;
        for (int i = 0; i < 3000; i++)
        {
            float brake = time >= seenAfter ? SolidRules.Brake(speed, distance, gap, braking, owed) : 0f;
            if (brake > 0f)
            {
                braking = true;
                hardest = speed > SolidRules.Crawl ? Math.Max(hardest, brake) : hardest;   // (the last of a stop, at a crawl, is no hard brake)
                float off = brake * step;
                speed -= off > speed ? speed : off;
            }
            else if (!(braking && speed <= 0f))
            {
                speed += power;
            }
            distance -= speed * step;
            time += step;
            nearest = Math.Min(nearest, distance);
            if (distance <= 0f)
            {
                impact = speed;
                return nearest;
            }
            if (braking && speed <= 0f)
            {
                return nearest;
            }
        }
        return nearest;
    }

    private static void SolidNeverStrikes()
    {
        var random = new Random(1234567);
        int reached = 0, struck = 0;
        float closest = float.MaxValue;
        const int cases = 100000;
        for (int i = 0; i < cases; i++)
        {
            float speed = 0.6f + (float)random.NextDouble() * 24.4f;                 // walking pace to 90 km/h
            float gap = SolidRules.GapLeast + (float)random.NextDouble() * (SolidRules.GapMost - SolidRules.GapLeast);
            float power = 0.02f + (float)random.NextDouble() * 0.04f;                // the traffic system's step: 1 to 3 m/s2
            float seenAfter = (float)random.NextDouble() * SolidRules.TickSeconds;   // anywhere between two looks
            // Half of them step out at the very limit of the kerb rule, the rest anywhere beyond it.
            float distance = SolidRules.SafeToStep(speed) + (i % 2 == 0 ? 0f : (float)random.NextDouble() * 40f);
            float nearest = SolidDrive(speed, distance, gap, power, seenAfter, false, out _, out float impact);
            closest = Math.Min(closest, nearest);
            reached += nearest <= 0f ? 1 : 0;
            struck += SolidRules.Touch(impact) == SolidRules.Contact.Strike || SolidRules.MayStrike(false, speed, distance, gap) ? 1 : 0;
        }
        Check(reached == 0 && struck == 0, "solid: nothing owed, the kerb rule kept: in " + cases + " random cases no car reaches anybody (" + reached + " did; the nearest any came was " + closest.ToString("0.00") + " m)");
        Check(closest >= SolidRules.PersonRadius, "solid: and none comes within a body's width");

        // Seen in good time, from the far end of its strip, it is an ordinary stop at the gap: no hard brake, so no horn.
        bool gentle = true, atGap = true;
        for (float speed = 3f; speed <= 22f; speed += 0.5f)
        {
            float nearest = SolidDrive(speed, SolidRules.SightFor(speed, SolidRules.Gap), SolidRules.Gap, 0.03f, SolidRules.TickSeconds, false, out float hardest, out _);
            gentle &= hardest < SolidRules.HardFrom;
            atGap &= nearest > SolidRules.Gap - 0.4f && nearest < SolidRules.Gap + 1.5f;
        }
        Check(gentle, "solid: somebody seen from the end of the strip is stopped for without a hard brake at any speed up to 80 km/h");
        Check(atGap, "solid: and the car comes to rest at its gap, give or take a metre");

        // An accident owed, and somebody too close: it reaches them still moving, at less than it was doing.
        float left = SolidDrive(10f, 4f, SolidRules.Gap, 0.03f, 0.05f, true, out float most, out float hit);
        Check(left <= 0f && hit > SolidRules.StrikeFrom && hit < 10f && most == SolidRules.HardMost, "solid: one owed, 4 m ahead at 36 km/h: braked with all the tyres give, and hit at " + (hit * 3.6f).ToString("0") + " km/h");
        float spared = SolidDrive(10f, 4f, SolidRules.Gap, 0.03f, 0.05f, false, out _, out float none);
        Check(spared > 0f && none == 0f, "solid: the same with nothing owed: stopped, " + spared.ToString("0.0") + " m short");

        // The same case twice gives the same answer to the last digit.
        float one = SolidDrive(13.3f, 22.2f, 3.1f, 0.031f, 0.07f, false, out float h1, out _);
        float two = SolidDrive(13.3f, 22.2f, 3.1f, 0.031f, 0.07f, false, out float h2, out _);
        Check(one == two && h1 == h2, "solid: the same drive twice is the same drive");
    }

    private static void SolidTiming()
    {
        // One look's worth of the rules: 60 people into the grid, 40 cars' strips, the stand-ins shared out.
        var random = new Random(99);
        var grid = new SolidRules.PeopleGrid(SolidRules.MostPeople);
        var pool = new SolidRules.ProxyPool(SolidRules.Proxies);
        var found = new int[48];
        var ahead = new float[48];
        var side = new float[48];
        var freed = new bool[SolidRules.Proxies];
        var given = new bool[SolidRules.Proxies];
        var tags = new int[SolidRules.Proxies];
        var px = new float[60];
        var pz = new float[60];
        var cx = new float[40];
        var cz = new float[40];
        var fx = new float[40];
        var fz = new float[40];
        var speed = new float[40];
        for (int i = 0; i < 60; i++)
        {
            // People along four streets, some of them in the road.
            bool eastWest = i % 2 == 0;
            float along = (float)(random.NextDouble() * 170.0 - 85.0);
            float across = (i % 4 < 2 ? -30f : 30f) + (float)(random.NextDouble() * 12.0 - 6.0);
            px[i] = eastWest ? along : across;
            pz[i] = eastWest ? across : along;
        }
        for (int i = 0; i < 40; i++)
        {
            bool eastWest = i % 2 == 0;
            float along = (float)(random.NextDouble() * 170.0 - 85.0);
            float lane = (i % 4 < 2 ? -30f : 30f) + (i % 8 < 4 ? -1.7f : 1.7f);
            cx[i] = eastWest ? along : lane;
            cz[i] = eastWest ? lane : along;
            float way = i % 8 < 4 ? 1f : -1f;
            fx[i] = eastWest ? way : 0f;
            fz[i] = eastWest ? 0f : way;
            speed[i] = (float)random.NextDouble() * 14f;
        }
        int total = 0;
        double best = double.MaxValue;
        for (int batch = 0; batch < 7; batch++)
        {
            Stopwatch watch = Stopwatch.StartNew();
            const int looks = 2000;
            for (int look = 0; look < looks; look++)
            {
                grid.Begin(0f, 0f);
                for (int i = 0; i < 60; i++)
                {
                    grid.Add(px[i], pz[i]);
                }
                grid.Build();
                pool.Begin();
                for (int c = 0; c < 40; c++)
                {
                    float sight = SolidRules.SightFor(speed[c], 2.5f);
                    int count = grid.Strip(cx[c], cz[c], fx[c], fz[c], 1f, 2.6f + sight, 0.95f + SolidRules.StripMargin + SolidRules.StepBand, found, ahead, side);
                    for (int k = 0; k < count; k++)
                    {
                        float distance = ahead[k] - 2.3f - SolidRules.PersonRadius;
                        if (side[k] <= 1.45f && side[k] >= -1.45f && distance > -1.5f)
                        {
                            SolidRules.Driver judged = SolidRules.Judge(speed[c], distance, 2.5f);
                            total += (int)judged;
                            pool.Want(found[k] + 1, distance / Math.Max(1f, speed[c]), found[k]);
                        }
                    }
                }
                pool.Share(freed, given, tags);
            }
            watch.Stop();
            best = Math.Min(best, watch.Elapsed.TotalMilliseconds * 1000.0 / looks);
        }
        Check(total > 0 && pool.InUse > 0, "solid: the timed look found people in the cars' way and shared stand-ins out (" + pool.InUse + " in use)");
        Check(best < 15.0, "solid: the rules of one look at 40 cars and 60 people take " + best.ToString("0.0") + " microseconds here (under 15 wanted: the game's runtime is 4 to 8 times slower, and the look's engine calls come on top)");
        Console.WriteLine("      solid: one look's rules for 40 cars and 60 people: " + best.ToString("0.00") + " microseconds (best of 7 batches of 2,000)");
    }
}
