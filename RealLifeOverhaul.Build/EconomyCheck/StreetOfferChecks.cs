using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using RealLifeOverhaul.Street;

// Who in the street is offered a talk (the F prompt, for standing people as well as walking ones): the reach
// and the angle, the nearest roughly in front, the one offered keeping it a step further and wider, the time in front counted
// on the same person without being reset by a missed tick, the grace that lets a passer-by be caught, who is never offered
// (busy at something), what is changed and given back for each kind of person, nothing allocated, and by source the offer's
// check lines, the gates in their order and the give-back on every way out.
internal static partial class Program
{
    private static void StreetOffers()
    {
        var watch = Stopwatch.StartNew();
        int before = _run, failedBefore = _failed;
        StreetOfferReach();
        StreetOfferAngle();
        StreetOfferNearest();
        StreetOfferDwell();
        StreetOfferGrace();
        StreetOfferPasserBy();
        StreetOfferKinds();
        StreetOfferCost();
        StreetOfferSources();
        Console.WriteLine("  street offer: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed, " + watch.ElapsedMilliseconds + " ms");
    }

    // A person at a distance and an angle (degrees, left positive) from a player at the origin facing +z.
    private static float OfferScore(float metres, float degrees, bool current, float up = 0f)
    {
        double r = degrees * Math.PI / 180.0;
        return StreetOffer.Score((float)(metres * Math.Sin(r)), up, (float)(metres * Math.Cos(r)), 0f, 1f, current);
    }

    private static void StreetOfferReach()
    {
        Check(StreetRules.Reach >= 3f && StreetRules.Reach <= 3.5f, "street offer: talking distance is 3 to 3.5 m (" + StreetRules.Reach + " m)");
        Check(OfferScore(3.0f, 0f, false) >= 0f && OfferScore(StreetRules.Reach - 0.01f, 0f, false) >= 0f, "street offer: somebody 3 m straight ahead is offered");
        Check(OfferScore(StreetRules.Reach + 0.2f, 0f, false) < 0f, "street offer: somebody beyond the reach is not");
        Check(OfferScore(StreetRules.Reach + 0.5f, 0f, true) >= 0f && OfferScore(StreetRules.LetGo + 0.1f, 0f, true) < 0f,
            "street offer: the one offered keeps it a step further (to " + StreetRules.LetGo + " m), not beyond");
        Check(OfferScore(2f, 0f, false, StreetRules.Height + 0.1f) < 0f && OfferScore(2f, 0f, false, -StreetRules.Height - 0.1f) < 0f && OfferScore(2f, 0f, false, 0.5f) >= 0f,
            "street offer: nobody on another level (a balcony, a bridge), a kerb's difference is nothing");
        Check(OfferScore(0f, 0f, false) == 0f, "street offer: somebody on the player's own spot is the nearest of all, not lost to a zero angle");
        Check(StreetRules.LetGo > StreetRules.Reach && StreetRules.GraceMetres >= StreetRules.LetGo && StreetRules.GraceMetres < StreetRules.WalkAway,
            "street offer: the reach, the step of give and the grace's distance stay under the distance that ends a talk, so a press always holds them");
    }

    private static void StreetOfferAngle()
    {
        Check(OfferScore(2f, 55f, false) >= 0f && OfferScore(2f, -55f, false) >= 0f, "street offer: 55 degrees to either side is in front");
        Check(OfferScore(2f, 65f, false) < 0f && OfferScore(2f, 180f, false) < 0f, "street offer: 65 degrees off, or behind, is not");
        Check(OfferScore(2f, 75f, true) >= 0f && OfferScore(2f, 85f, true) < 0f, "street offer: the one offered keeps it out to about 80 degrees (a passer-by drifting to the side), not beside or behind");
        Check(StreetOffer.Degrees(StreetRules.Facing) == 60 && StreetOffer.Degrees(1f) == 0 && StreetOffer.Degrees(-1f) == 180, "street offer: the log's angles are whole degrees");
        Check(StreetOffer.Score(0f, 0f, 2f, 0f, 0f, false) < 0f, "street offer: with no facing (looking straight down) nobody is in front");
        Check(StreetOffer.Score(0f, 0f, 2f, 0f, 7f, false) >= 0f && StreetOffer.Score(0f, 0f, 2f, 0f, 0.1f, false) >= 0f, "street offer: the facing's length does not matter");
    }

    private static void StreetOfferNearest()
    {
        float ahead3 = OfferScore(3.0f, 0f, false), side26 = OfferScore(2.6f, 55f, false), near15 = OfferScore(1.5f, 40f, false);
        Check(ahead3 < side26, "street offer: somebody straight ahead at 3 m beats somebody a little nearer at the side (2.6 m, 55 degrees)");
        Check(near15 < ahead3, "street offer: somebody plainly nearer and roughly in front (1.5 m, 40 degrees) beats them both");
        Check(OfferScore(2f, 10f, false) < OfferScore(2.5f, 10f, false) && OfferScore(2f, 10f, false) < OfferScore(2f, 40f, false),
            "street offer: nearer is better at the same angle, straighter is better at the same distance");
    }

    private static void StreetOfferDwell()
    {
        Check(StreetRules.Dwell >= StreetRules.Tick && StreetRules.Dwell <= 0.5f && StreetRules.Tick <= 0.25f, "street offer: the box comes after a moment in front (" + StreetRules.Dwell + " s, looked at every " + StreetRules.Tick + " s)");
        var aim = new StreetAim();
        float t = 0f, dt = StreetRules.Tick;
        aim.Step(7, false, t);
        Check(aim.Key == 7 && !aim.Ready(), "street offer: not at the first sight");
        aim.Step(7, true, t += dt);
        aim.Step(0, true, t += dt);           // a tick out of the front, still near
        aim.Step(9, true, t += dt);           // somebody else nearer for a tick
        bool kept = aim.Key == 7 && aim.Since == 0f;
        aim.Step(7, true, t += dt);
        Check(kept && aim.Key == 7 && aim.Since == 0f && aim.Ready(), "street offer: the time in front is counted on the same person and not reset by a tick without them or with somebody else nearer");
        var fresh = new StreetAim();
        fresh.Step(7, false, 0f);
        fresh.Step(0, true, 0.6f);            // out of the front at once, still near: kept, but never seen in front long enough
        Check(fresh.Key == 7 && !fresh.Ready(), "street offer: a grace never makes up the time in front (somebody glimpsed once is not offered)");
        var steady = new StreetAim();
        int ready = -1;
        for (int i = 0; i < 6 && ready < 0; i++)
        {
            steady.Step(3, true, i * dt);
            ready = steady.Ready() ? i : -1;
        }
        Check(ready >= 1 && ready * dt <= StreetRules.Dwell + dt, "street offer: somebody standing in front gets the box on look " + (ready + 1) + " (" + (ready * dt).ToString("0.0") + " s after the first)");
    }

    private static void StreetOfferGrace()
    {
        var aim = new StreetAim();
        float dt = StreetRules.Tick, t = 0f;
        for (; t <= 0.6f; t += dt)
        {
            aim.Step(5, true, t);
        }
        float last = t - dt;
        Check(aim.Ready(), "street offer: offered after standing in front");
        bool held = true;
        for (; t <= last + StreetRules.Grace - 0.01f; t += dt)
        {
            held &= !aim.Step(0, true, t) && aim.Key == 5 && aim.Ready() && aim.InGrace(t);
        }
        Check(held, "street offer: the offer stays " + StreetRules.Grace + " s on somebody who steps out of the front while near (a passer-by can be caught)");
        Check(aim.Step(0, true, last + StreetRules.Grace + dt) && aim.Key == 0, "street offer: after the grace the box goes");
        var away = new StreetAim();
        away.Step(5, true, 0f);
        away.Step(5, true, dt);
        Check(away.Step(0, false, 2f * dt) && away.Key == 0, "street offer: no grace once they are further than " + StreetRules.GraceMetres + " m or gone");
        var other = new StreetAim();
        other.Step(5, true, 0f);
        other.Step(6, true, dt);
        bool stays = other.Key == 5;
        Check(stays && other.Step(6, false, 2f * dt) && other.Key == 6 && !other.Ready(), "street offer: somebody else takes the box only when the first has gone, and waits their own moment in front");
        var closed = new StreetAim();
        closed.Step(5, true, 0f);
        closed.Clear();
        Check(closed.Key == 0 && !closed.Ready() && !closed.InGrace(1f), "street offer: a closed gate clears it at once, with no grace");
    }

    // A walker passing the player at walking pace, a little to the side: the box must come up and stay long enough to press.
    private static void StreetOfferPasserBy()
    {
        float shown = PasserBy(1.5f, 1.4f), fast = PasserBy(1.2f, 2.0f), headOn = PasserBy(0.6f, 1.6f), wide = PasserBy(3.4f, 1.4f);
        Check(shown >= 1.5f, "street offer: a walker passing 1.5 m to the side at 1.4 m/s has the box over them for " + shown.ToString("0.0") + " s (1.5 s at least)");
        Check(fast >= 1f, "street offer: a brisk walker (2 m/s, 1.2 m to the side) still has it " + fast.ToString("0.0") + " s (1 s at least)");
        Check(headOn >= 1.5f, "street offer: somebody walking straight at the player and round them has it " + headOn.ToString("0.0") + " s");
        Check(wide == 0f, "street offer: somebody passing 3.4 m away never gets it (outside talking distance)");
    }

    // Seconds the box is up over somebody walking along x = side from 8 m ahead to 8 m behind the player (facing +z).
    private static float PasserBy(float side, float speed)
    {
        var aim = new StreetAim();
        float dt = StreetRules.Tick, up = 0f;
        int key = 11;
        for (float t = 0f; t < 16f / speed; t += dt)
        {
            float z = 8f - speed * t;
            bool current = aim.Key == key;
            bool inFront = StreetOffer.Score(side, 0f, z, 0f, 1f, current) >= 0f;
            bool near = side * side + z * z <= StreetRules.GraceMetres * StreetRules.GraceMetres;
            aim.Step(inFront ? key : 0, near, t);
            if (aim.Key == key && aim.Ready())
            {
                up += dt;
            }
        }
        return up;
    }

    private static void StreetOfferKinds()
    {
        Check(StreetOffer.Offered(StreetFigure.Walker) && StreetOffer.Offered(StreetFigure.Standing) && StreetOffer.Offered(StreetFigure.Seated) && !StreetOffer.Offered(StreetFigure.Busy),
            "street offer: walkers, people standing about and people seated are offered; people busy at something are not");
        Check(StreetOffer.Stops(StreetFigure.Walker) && !StreetOffer.Stops(StreetFigure.Standing) && !StreetOffer.Stops(StreetFigure.Seated),
            "street offer: only a walker's walking is paused and its agent stopped (and given back)");
        Check(StreetOffer.TurnsBody(StreetFigure.Standing) && !StreetOffer.TurnsBody(StreetFigure.Seated) && !StreetOffer.TurnsBody(StreetFigure.Walker),
            "street offer: somebody standing is turned on the spot by the mod (a walker by the game's own turn), somebody seated never");
        Check(!StreetOffer.Changes(StreetFigure.Seated) && StreetOffer.Changes(StreetFigure.Standing) && StreetOffer.Changes(StreetFigure.Walker),
            "street offer: somebody seated is left exactly as posed: nothing of theirs is changed, so nothing needs giving back");
        Check(StreetOffer.Lift(StreetFigure.Seated) < StreetOffer.Lift(StreetFigure.Standing) && StreetOffer.Lift(StreetFigure.Standing) == StreetOffer.Lift(StreetFigure.Walker),
            "street offer: the box stands lower over somebody seated");
    }

    private static void StreetOfferCost()
    {
        TimeStreetOffer(out double us, out long allocated);
        Check(allocated == 0, "street offer: a pass over 60 people and the aim's tick allocate nothing (" + allocated + " bytes)");
        Check(us < 20.0, "street offer: a pass over 60 people takes " + us.ToString("0.00") + " microseconds (under 20)");
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static void TimeStreetOffer(out double us, out long allocated)
    {
        var aim = new StreetAim();
        float sink = 0f;
        for (int i = 0; i < 60; i++)
        {
            sink += StreetOffer.Score(i * 0.1f, 0f, 3f - i * 0.05f, 0.2f, 0.9f, i == 3);   // (warm)
        }
        const int n = 2000;
        long before = GC.GetAllocatedBytesForCurrentThread();
        var watch = Stopwatch.StartNew();
        for (int k = 0; k < n; k++)
        {
            int best = 0;
            float bestScore = float.MaxValue;
            for (int i = 0; i < 60; i++)
            {
                float s = StreetOffer.Score(i * 0.1f - 3f, 0f, 3f - i * 0.05f, 0.2f, 0.9f, i == 3);
                if (s >= 0f && s < bestScore)
                {
                    bestScore = s;
                    best = i + 1;
                }
            }
            aim.Step(best, true, k * StreetRules.Tick);
            sink += aim.Ready() ? 1f : 0f;
        }
        us = watch.Elapsed.TotalMilliseconds * 1000.0 / n;
        allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        if (sink < -1f)
        {
            Console.WriteLine(sink);
        }
    }

    // ---------------------------------------------------------------- the sources

    private static void StreetOfferSources()
    {
        string director = TextsSource(@"Street\StreetDirector.cs"), talk = TextsSource(@"Street\StreetDirector.Talk.cs"), game = TextsSource(@"Street\StreetGame.cs") + TextsSource(@"Street\StreetHold.cs");
        string horn = TextsSource(@"People\HornReactions.cs"), tests = TextsSource(@"Street\StreetDirector.Tests.cs");
        int offer = director.IndexOf("private void Offer(float now)", StringComparison.Ordinal);
        int offerEnd = offer >= 0 ? director.IndexOf("private void Aim(", offer, StringComparison.Ordinal) : -1;
        string body = offer >= 0 && offerEnd > offer ? director.Substring(offer, offerEnd - offer) : string.Empty;
        int check = director.IndexOf("private void Check(float now", StringComparison.Ordinal);
        string checkBody = check >= 0 ? director.Substring(check, Math.Min(900, director.Length - check)) : string.Empty;
        Check(checkBody.Contains("if (!Meta.Dev.Verbose || now < _nextCheck || !StreetGame.OutOfDoorsOnFoot)") && checkBody.Contains("_nextCheck = now + CheckEvery;")
              && director.Contains("private const float CheckEvery = 5f;"),
            "street offer: its check line is written only with Developer diagnostics on, on foot out of doors, at most every 5 s, and nothing is built before that test");
        Check(director.Contains("\"Street talk check (offer): nothing offered: \"") && director.Contains("StreetGame.LastWalkers") && director.Contains("StreetGame.LastFigures")
              && director.Contains("StreetGame.NearUsable") && director.Contains("StreetOffer.Degrees(StreetGame.NearCos)") && director.Contains("why = \"closed: \" + closed;"),
            "street offer: the check line names the closed gate, the people out, and the nearest one's distance, angle and whether they can be talked to");
        Check(director.Contains("\"Street talk check (offer): the box is up over \"") && body.Contains("if (up && !_shown)")
              && talk.Contains("\"Street talk check (offer): Talk pressed for \""),
            "street offer: a line when the box goes up (once a person) and one when Talk is pressed");
        Check(body.Contains("Check(now, closed, at, player.transform.forward);") && !body.Contains("Logger?.Info(\"Street talk check (offer): nothing"),
            "street offer: the tick itself builds no line: the check line is behind its own time test");
        int closedAt = director.IndexOf("private static string Closed()", StringComparison.Ordinal);
        string gates = closedAt >= 0 ? director.Substring(closedAt, Math.Min(1500, director.Length - closedAt)) : string.Empty;
        int g1 = gates.IndexOf("StreetGame.NotOnFoot()", StringComparison.Ordinal), g2 = gates.IndexOf("StreetGame.DialogOpen", StringComparison.Ordinal),
            g3 = gates.IndexOf("JobDirector.OnShift", StringComparison.Ordinal), g4 = gates.IndexOf("JobDirector.TakesInteractKey", StringComparison.Ordinal),
            g5 = gates.IndexOf("SocialDirector.Busy", StringComparison.Ordinal), g6 = gates.IndexOf("SocialDirector.TakesInteractKey", StringComparison.Ordinal);
        Check(g1 >= 0 && g1 < g2 && g2 < g3 && g3 < g4 && g4 < g5 && g5 < g6, "street offer: the gates in their order: the game's own state, the dialogue panel, the jobs, the social life (each has the key before street talk)");
        Check(game.Contains("public static bool OnFootOutside() => NotOnFoot() == null;") && game.Contains("LifeEvents.IsSafeForEvent()") && game.Contains("Wiring.Scenes.PlayerHeld"),
            "street offer: on foot out of doors is one rule, the one the check line names");
        Check(body.Contains("_aim.Step(") && body.Contains("_aim.Clear();") && body.Contains("StreetGame.Near(_offered, _offeredWalker, at)") && body.Contains("!_aim.Ready()"),
            "street offer: the director aims through StreetAim (the time in front without reset, the grace), and a closed gate clears it");
        Check(game.Contains("StreetPeople.Standing(Logger)") && game.Contains("if (!StreetOffer.Offered(k))") && game.Contains("GetComponent<Homeless>()") && game.Contains("GetComponent<StreetPerformer>()")
              && game.Contains("GetComponent<StationaryAiBehavior>()") && game.Contains("\"Bench\""),
            "street offer: the people standing about and on benches are looked at too; performers, the homeless and the pools of the pool, the golf course and the machines are not");
        Check(game.Contains("!RagdollSystem.Holds(h)") && game.Contains("!HornReactions.Reacts(h)") && game.Contains("Usable(h) && !Theirs(h.transform)") && game.Contains("Usable(p) && !Theirs(body.transform)"),
            "street offer: a figure held by the ragdolls or turned by a horn, or another player's in a Going Public session, is never offered");
        Check(horn.Contains("Street.StreetDirector.Holds(figure)") && horn.Contains("public static bool Reacts(BaseHuman figure)") && game.Contains("HornReactions.LetGo(body);"),
            "street offer: a horn's look round leaves somebody talking alone, and one under way is put back before a talk");
        Check(game.Contains("_stood = body.transform.rotation;") && game.Contains("Turning(body.transform, _stood)") && game.Contains("body.transform.rotation = _stood;")
              && game.Contains("_back.transform.rotation = _backTo;") && game.Contains("_stood = _backTo;"),
            "street offer: somebody standing turns back exactly as they stood on every way out (turning back, at once when unloading, kept if taken again mid-turn)");
        Check(game.Contains("if (!_turned || _body == null)") && game.Contains("else if (StreetOffer.TurnsBody(_kind))"),
            "street offer: somebody seated is never turned (only a standing figure's turn is the mod's)");
        Check(game.Contains("p.enabled = _ped;") && game.Contains("agent.isStopped = _stopped;") && game.Contains("p.pedestrianWalkingAiBehavior.enabled = _walk;") && talk.Contains("_hold.Release();"),
            "street offer: a stopped walker is given back as found");
        Check(tests.Contains("StreetPeople.Standing(Logger)") && tests.Contains("Begin(best, bestWalker, bestKind, Time.unscaledTime)"),
            "street offer: the options page's test talks to the nearest walking, standing or seated person");
    }
}
