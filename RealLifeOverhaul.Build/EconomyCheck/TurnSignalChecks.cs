using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using RealLifeOverhaul;

// A player asked for automatic turn signals ("More cars", 3 Nov 2026). The game has two keys for the signals and nothing that
// signals a turn by itself, for the player's car (its traffic does, through its own driving AI). "Automatic turn signals"
// (auto_signals, on by default) switches the signal of the side the car turns to on once the car has been turning a moment, and
// off when the turn is made, through the game's own toggle, and never touches a signal the driver switched on. First the figures,
// then the rules driven through every kind of drive on a car that has the game's two signals (its toggle puts the other side off),
// then the driver's own signals, then the cost, then the sources, then the option and the texts. Offline: run with --turn-signals.
internal static partial class Program
{
    private static void TurnSignalChecks()
    {
        TurnSignalFigures();
        TurnSignalDrives();
        TurnSignalDriver();
        TurnSignalSpeed();
        TurnSignalSources();
        TurnSignalWords();
    }

    // A car with the game's two signals and the rules' state, looked at tenth of a second by tenth. The game's own toggle is the
    // one in VehicleBlinker: a side switched on puts the other side off, and only a side that is not as asked is toggled.
    private sealed class TurnSignalCar
    {
        public bool Left;
        public bool Right;
        public bool Both;                               // both signals showed at some look
        public int Tick;                                // looks so far
        public int Raised;                              // signals the rules put on
        public int Dropped;                             // signals the rules put off
        public int Closest = int.MaxValue;              // looks between the two nearest commands
        public readonly TurnSignalState State = new TurnSignalState();
        public readonly List<string> Said = new List<string>();
        private int _lastCommand = -1;

        public void Look(float speed, float steer, float turning, float dt)
        {
            Tick++;
            var look = new TurnSignalLook { Speed = speed, Steer = steer, Turning = turning, Left = Left, Right = Right };
            TurnSignalCommand command = TurnSignalRules.Step(State, look, dt);
            if (command != TurnSignalCommand.None)
            {
                Said.Add(command + "@" + Tick);
                Closest = _lastCommand >= 0 ? Math.Min(Closest, Tick - _lastCommand) : Closest;
                _lastCommand = Tick;
            }
            switch (command)
            {
                case TurnSignalCommand.LeftOn:
                    Raised++;
                    ToggleIf(true, true);
                    break;
                case TurnSignalCommand.RightOn:
                    Raised++;
                    ToggleIf(false, true);
                    break;
                case TurnSignalCommand.LeftOff:
                    Dropped++;
                    ToggleIf(true, false);
                    break;
                case TurnSignalCommand.RightOff:
                    Dropped++;
                    ToggleIf(false, false);
                    break;
            }
            Both |= Left && Right;
        }

        public void Run(float seconds, float speed, float steer, float turning)
        {
            int looks = (int)Math.Round(seconds / TurnSignalRules.LookEvery);
            for (int i = 0; i < looks; i++)
            {
                Look(speed, steer, turning, TurnSignalRules.LookEvery);
            }
        }

        private void ToggleIf(bool left, bool on)
        {
            if ((left ? Left : Right) != on)
            {
                Toggle(left);
            }
        }

        private void Toggle(bool left)
        {
            if (left)
            {
                Left = !Left;
                if (Left && Right)
                {
                    Toggle(false);
                }
            }
            else
            {
                Right = !Right;
                if (Left && Right)
                {
                    Toggle(true);
                }
            }
        }
    }

    // The look at which a command was given, the nth time (from 0), or -1.
    private static int TurnSignalTick(TurnSignalCar car, string command, int nth = 0)
    {
        int seen = 0;
        foreach (string said in car.Said)
        {
            if (said.StartsWith(command + "@", StringComparison.Ordinal))
            {
                if (seen == nth)
                {
                    return int.Parse(said.Substring(command.Length + 1), System.Globalization.CultureInfo.InvariantCulture);
                }
                seen++;
            }
        }
        return -1;
    }

    private static void TurnSignalFigures()
    {
        Check(TurnSignalRules.StraightSteer < TurnSignalRules.TurnSteer && TurnSignalRules.StraightRate < TurnSignalRules.TurnRate,
            "turn signals: a wheel or a swing that counts as straight is less than one that makes a turn, so a signal does not flicker on the line");
        Check(TurnSignalRules.TurnHold > 2f * TurnSignalRules.LookEvery && TurnSignalRules.TurnHold <= 0.6f && TurnSignalRules.MostGap < TurnSignalRules.TurnHold,
            "turn signals: a turn must last more than two looks and under a second to bring its signal on, and a late look alone never counts as that long (hold "
            + TurnSignalRules.TurnHold + " s, one look at most " + TurnSignalRules.MostGap + " s)");
        Check(TurnSignalRules.LeastOn >= 2f && TurnSignalRules.LeastOn <= 2.5f && TurnSignalRules.SwitchAfter <= TurnSignalRules.LeastOn,
            "turn signals: a signal stays up one full flash of the game's own (its cycle is 2 s) at least and not much more, and a weave may put it over no later than that (" + TurnSignalRules.LeastOn + " s)");
        Check(TurnSignalRules.CentreHold > 2f * TurnSignalRules.LookEvery && TurnSignalRules.CentreHold <= 0.4f,
            "turn signals: the wheel back near the middle for more than two looks and at most 0.4 s puts a made turn's signal off, as a real car's stalk cancels itself (" + TurnSignalRules.CentreHold + " s)");
        Check(TurnSignalRules.MinSpeed >= 2f && TurnSignalRules.MinSpeed > TurnSignalRules.StaySpeed && TurnSignalRules.StaySpeed > 0f && TurnSignalRules.ReverseSpeed < 0f,
            "turn signals: parking pace brings no signal, a stop in a turn keeps one, and only backing takes it off");
        Check(TurnSignalRules.GiveUp > TurnSignalRules.LeastOn + TurnSignalRules.CentreHold && TurnSignalRules.GiveUp >= 4f && TurnSignalRules.GiveUp <= 5f
              && TurnSignalRules.Rest >= 1f && TurnSignalRules.ReArm > 0f && TurnSignalRules.TurnDone > 0f,
            "turn signals: a signal that came on for nothing outlasts a made turn's (4 to 5 s against " + (TurnSignalRules.LeastOn + TurnSignalRules.CentreHold) + " s), and one signal rests a second before the next");
        var used = new TurnSignalState { Own = -1, Held = 1, HeldFor = 3f, HeldSwing = 40f, OnFor = 9f, Swing = 50f, StraightFor = 2f, CentredFor = 2f, RunFor = 7f, RestFor = 0f, Yielded = 1 };
        used.Reset();
        var fresh = new TurnSignalState();
        Check(used.Own == 0 && used.Held == 0 && used.HeldFor == 0f && used.HeldSwing == 0f && used.OnFor == 0f && used.Swing == 0f && used.StraightFor == 0f && used.CentredFor == 0f && used.RunFor == 0f
              && used.Yielded == 0 && used.RestFor == TurnSignalRules.Rest && fresh.Own == 0 && fresh.RestFor == TurnSignalRules.Rest && fresh.Yielded == 0,
            "turn signals: a car taken fresh, or let go and taken again, has nothing on, nothing held and may signal at once");
        Check(TurnSignalRules.Step(new TurnSignalState(), new TurnSignalLook { Speed = 10f, Steer = -1f, Turning = -40f }, 0f) == TurnSignalCommand.None
              && TurnSignalRules.Step(new TurnSignalState(), new TurnSignalLook { Speed = 10f, Steer = -1f, Turning = -40f }, -1f) == TurnSignalCommand.None,
            "turn signals: a look that covers no time asks for nothing");
    }

    private static void TurnSignalDrives()
    {
        // A left turn at 36 km/h: a second straight, two seconds with the wheel full over and the nose swinging 40 degrees a second, then straight.
        var left = new TurnSignalCar();
        left.Run(1f, 10f, 0f, 0f);
        left.Run(2f, 10f, -1f, -40f);
        bool inTurn = left.Left && !left.Right;
        left.Run(8f, 10f, 0f, 0f);
        int on = TurnSignalTick(left, "LeftOn"), off = TurnSignalTick(left, "LeftOff");
        Check(left.Said.Count == 2 && on >= 14 && on <= 15,
            "turn signals: a left turn begun at look 11 brings the left signal on 0.4 s into it, at look " + on + " (" + string.Join(", ", left.Said) + ")");
        Check(inTurn && left.Raised == 1 && left.Dropped == 1 && !left.Both,
            "turn signals: the left signal shows through the left turn and the right one never does");
        Check(off >= 34 && off <= 36 && (off - on) * TurnSignalRules.LookEvery >= TurnSignalRules.LeastOn - 0.15f && !left.Left && !left.Right,
            "turn signals: the left signal goes off once the turn is made and the wheel back, after its least time (wheel back at look 31, on at look " + on + ", off at " + off + ")");

        var right = new TurnSignalCar();
        right.Run(1f, 10f, 0f, 0f);
        right.Run(2f, 10f, 1f, 40f);
        bool rightInTurn = right.Right && !right.Left;
        right.Run(8f, 10f, 0f, 0f);
        Check(rightInTurn && TurnSignalTick(right, "RightOn") == on && TurnSignalTick(right, "RightOff") == off && right.Raised == 1 && right.Dropped == 1 && !right.Both && !right.Left && !right.Right,
            "turn signals: a right turn is the left turn the other way, the right signal on and off at the same looks");

        // Nothing is signalled that is not a turn.
        var straight = new TurnSignalCar();
        straight.Run(30f, 14f, 0f, 0f);
        Check(straight.Said.Count == 0 && !straight.Left && !straight.Right, "turn signals: a straight road brings no signal");

        var taps = new TurnSignalCar();
        for (int i = 0; i < 10; i++)
        {
            taps.Run(0.3f, 10f, 1f, 12f);
            taps.Run(0.3f, 10f, -1f, -12f);
        }
        Check(taps.Said.Count == 0, "turn signals: the taps on the keys that keep a car in its lane (0.3 s each way, ten times) bring no signal");

        var crawl = new TurnSignalCar();
        crawl.Run(10f, 1.5f, -1f, -30f);
        Check(crawl.Said.Count == 0, "turn signals: full lock at parking pace (1.5 m/s) brings no signal");

        var backing = new TurnSignalCar();
        backing.Run(10f, -3f, 1f, 30f);
        Check(backing.Said.Count == 0, "turn signals: full lock while backing brings no signal");

        var bend = new TurnSignalCar();
        bend.Run(10f, 20f, 0.3f, -6f);
        Check(bend.Said.Count == 0, "turn signals: a gentle bend (the wheel a third over, the nose swinging 6 degrees a second) brings no signal");

        var slide = new TurnSignalCar();
        slide.Run(10f, 10f, 0f, 40f);
        Check(slide.Said.Count == 0, "turn signals: a nose swinging with the wheel straight (a slide) brings no signal");

        // A roundabout: eight seconds of right turn, the signal on throughout and off once the car runs straight.
        var round = new TurnSignalCar();
        round.Run(1f, 8f, 0f, 0f);
        round.Run(8f, 8f, 0.8f, 30f);
        bool onAround = round.Right && !round.Left;
        round.Run(6f, 8f, 0f, 0f);
        Check(onAround && round.Raised == 1 && round.Dropped == 1 && !round.Right, "turn signals: a roundabout keeps the right signal on all the way round and puts it off after");

        // An S-bend: left, then right with no straight between. The signal goes over to the right, never both together.
        var bendS = new TurnSignalCar();
        bendS.Run(1f, 10f, 0f, 0f);
        bendS.Run(2.5f, 10f, -1f, -25f);
        bendS.Run(2.5f, 10f, 1f, 25f);
        bool overToRight = bendS.Right && !bendS.Left;
        bendS.Run(8f, 10f, 0f, 0f);
        Check(overToRight && bendS.Raised == 2 && bendS.Dropped == 1 && !bendS.Both && !bendS.Left && !bendS.Right,
            "turn signals: an S-bend puts the signal over to the second side without both showing, and puts it off after (" + string.Join(", ", bendS.Said) + ")");

        // A weave of half-second presses: no flicker, no two commands nearer than the time a signal stays on its side.
        var weave = new TurnSignalCar();
        weave.Run(1f, 10f, 0f, 0f);
        for (int i = 0; i < 8; i++)
        {
            weave.Run(0.5f, 10f, -1f, -25f);
            weave.Run(0.5f, 10f, 1f, 25f);
        }
        Check(weave.Closest >= (int)Math.Round(TurnSignalRules.SwitchAfter / TurnSignalRules.LookEvery) && !weave.Both,
            "turn signals: a weave of half-second presses puts the signal over no sooner than " + TurnSignalRules.SwitchAfter + " s after it came on (nearest commands " + weave.Closest + " looks apart)");

        // A signal that came on for nothing: the wheel over for half a second, then 20 seconds straight. It goes off after the give-up time of driving on.
        var falseStart = new TurnSignalCar();
        falseStart.Run(1f, 10f, 0f, 0f);
        falseStart.Run(0.5f, 10f, -1f, -12f);
        falseStart.Run(20f, 10f, 0f, 0f);
        int falseOff = TurnSignalTick(falseStart, "LeftOff");
        Check(falseStart.Raised == 1 && falseOff > 0 && (falseOff - 16) * TurnSignalRules.LookEvery >= TurnSignalRules.GiveUp - 0.25f && falseOff <= 80 && !falseStart.Left,
            "turn signals: a signal that came on for a turn never made goes off after " + TurnSignalRules.GiveUp + " s of driving on without turning its way (off at look " + falseOff + ")");

        // Held up in the middle of a turn: the signal stays through a long stop, then goes when the car is away straight.
        var queue = new TurnSignalCar();
        queue.Run(1f, 10f, 0f, 0f);
        queue.Run(1.5f, 10f, -1f, -30f);
        queue.Run(30f, 0f, -1f, 0f);
        bool kept = queue.Left;
        queue.Run(5f, 10f, 0f, 0f);
        Check(kept && queue.Dropped == 1 && !queue.Left, "turn signals: a stop of half a minute in the middle of a turn keeps the left signal on; it goes off after the car is away");

        // A turn that was never made, waiting straight at a junction: the signal is kept while the car does not move, then goes after the give-up time of running.
        var wait = new TurnSignalCar();
        wait.Run(1f, 10f, 0f, 0f);
        wait.Run(0.5f, 10f, -1f, -12f);
        wait.Run(30f, 0f, 0f, 0f);
        bool waiting = wait.Left;
        wait.Run(8f, 10f, 0f, 0f);
        Check(waiting && wait.Dropped == 1 && !wait.Left, "turn signals: a car waiting straight to turn keeps its signal while it stands, and loses it only after running straight");

        // Backing out takes it off at once.
        var reverse = new TurnSignalCar();
        reverse.Run(1f, 10f, 0f, 0f);
        reverse.Run(2f, 10f, -1f, -40f);
        bool wasOn = reverse.Left;
        reverse.Run(0.1f, -3f, 0f, 0f);
        Check(wasOn && !reverse.Left && reverse.Dropped == 1, "turn signals: backing at 3 m/s takes the signal off at the next look");

        // A late look (a hitch of the game) is not a turn held.
        var hitch = new TurnSignalCar();
        hitch.Look(10f, -1f, -40f, 5f);
        Check(hitch.Said.Count == 0, "turn signals: one look five seconds late counts as 0.2 s at most, which brings no signal");

        // One signal rests a second before the next.
        var rest = new TurnSignalCar();
        rest.Run(1f, 10f, 0f, 0f);
        rest.Run(2f, 10f, -1f, -40f);
        for (int i = 0; i < 200 && (rest.Left || rest.Dropped == 0); i++)
        {
            rest.Look(10f, 0f, 0f, TurnSignalRules.LookEvery);
        }
        int cancelled = rest.Tick;
        rest.Run(3f, 10f, -1f, -40f);
        int again = TurnSignalTick(rest, "LeftOn", 1);
        Check(rest.Dropped == 1 && again - cancelled >= (int)Math.Round(TurnSignalRules.Rest / TurnSignalRules.LookEvery) - 1 && again - cancelled <= 12,
            "turn signals: a second turn begun the look after a signal went off gets its signal a second later (off at look " + cancelled + ", on again at " + again + ")");

        // The same drive twice gives the same commands.
        var one = new TurnSignalCar();
        var two = new TurnSignalCar();
        foreach (TurnSignalCar car in new[] { one, two })
        {
            car.Run(1f, 10f, 0f, 0f);
            car.Run(2.5f, 10f, -1f, -25f);
            car.Run(2.5f, 10f, 1f, 25f);
            car.Run(6f, 12f, 0f, 0f);
        }
        Check(string.Join(",", one.Said) == string.Join(",", two.Said) && one.Said.Count == 3, "turn signals: the same drive gives the same commands every time");

        TurnSignalCancels();
    }

    // Signals that stayed on long after the turn (12 Nov 2026): like a real car's self-cancelling stalk, the signal of a turn made goes off once
    // the wheel is back near the middle, however the road bends after it (the game's angle assist keeps the nose following the road with the
    // keys let go), and a signal that came on for nothing goes off after a while of driving on without turning its way, bend or no bend.
    private static void TurnSignalCancels()
    {
        // A left turn, then the wheel let go on a road that keeps bending gently (7 degrees a second either way): off within a second of the wheel centring.
        foreach (float bending in new[] { -7f, 7f })
        {
            var bendAfter = new TurnSignalCar();
            bendAfter.Run(1f, 10f, 0f, 0f);
            bendAfter.Run(2.2f, 10f, -1f, -40f);
            int centred = bendAfter.Tick + 1;
            bendAfter.Run(10f, 10f, 0f, bending);
            int bendOff = TurnSignalTick(bendAfter, "LeftOff");
            Check(bendOff > 0 && bendOff - centred <= 10 && bendAfter.Raised == 1 && bendAfter.Dropped == 1 && !bendAfter.Left && !bendAfter.Right,
                "turn signals: after a left turn the signal goes off within a second of the wheel centring, with the road bending on at " + bending
                + " degrees a second (wheel centred at look " + centred + ", off at " + bendOff + "; " + string.Join(", ", bendAfter.Said) + ")");
        }

        // A short clean 90 degree turn (45 degrees a second for two seconds): off within 2.5 s of coming on, one flash of the game's own and the wheel back.
        var corner = new TurnSignalCar();
        corner.Run(1f, 10f, 0f, 0f);
        corner.Run(2f, 10f, -1f, -45f);
        corner.Run(6f, 10f, 0f, 0f);
        int cornerOn = TurnSignalTick(corner, "LeftOn"), cornerOff = TurnSignalTick(corner, "LeftOff");
        Check(cornerOn > 0 && cornerOff > cornerOn && (cornerOff - cornerOn) * TurnSignalRules.LookEvery <= 2.5f + 0.05f && corner.Raised == 1 && corner.Dropped == 1 && !corner.Left,
            "turn signals: a short clean 90 degree turn has its signal off within 2.5 s of it coming on (on at look " + cornerOn + ", off at " + cornerOff + ")");

        // A signal that came on for nothing, then a road bending gently its way (the wheel a third over, 6 degrees a second): off within the fallback.
        var bendNothing = new TurnSignalCar();
        bendNothing.Run(1f, 10f, 0f, 0f);
        bendNothing.Run(0.5f, 10f, -1f, -12f);
        int bendFrom = bendNothing.Tick + 1;
        bendNothing.Run(20f, 10f, -0.3f, -6f);
        int nothingOff = TurnSignalTick(bendNothing, "LeftOff");
        Check(nothingOff > 0 && (nothingOff - bendFrom) * TurnSignalRules.LookEvery <= TurnSignalRules.GiveUp + 0.25f && TurnSignalRules.GiveUp <= 5f
              && bendNothing.Raised == 1 && !bendNothing.Left,
            "turn signals: a signal that came on for nothing goes off within " + TurnSignalRules.GiveUp + " s on a road bending gently its way, the wheel a third over (bend from look "
            + bendFrom + ", off at " + nothingOff + ")");

        // The same with the wheel let go and the nose following a bend the other way.
        var driftNothing = new TurnSignalCar();
        driftNothing.Run(1f, 10f, 0f, 0f);
        driftNothing.Run(0.5f, 10f, -1f, -12f);
        int driftFrom = driftNothing.Tick + 1;
        driftNothing.Run(20f, 10f, 0f, 7f);
        int driftOff = TurnSignalTick(driftNothing, "LeftOff");
        Check(driftOff > 0 && (driftOff - driftFrom) * TurnSignalRules.LookEvery <= TurnSignalRules.GiveUp + 0.25f && TurnSignalRules.GiveUp <= 5f
              && driftNothing.Raised == 1 && !driftNothing.Left,
            "turn signals: a signal that came on for nothing goes off within " + TurnSignalRules.GiveUp + " s on a road bending gently the other way, the wheel let go (bend from look "
            + driftFrom + ", off at " + driftOff + ")");
    }

    private static void TurnSignalDriver()
    {
        // A signal the driver put on before the turn is theirs: never touched, never added to, never put off.
        var own = new TurnSignalCar();
        own.Left = true;
        own.Run(1f, 10f, 0f, 0f);
        own.Run(2f, 10f, -1f, -40f);
        own.Run(10f, 10f, 0f, 0f);
        Check(own.Said.Count == 0 && own.Left && !own.Right, "turn signals: the driver's own left signal is left on through the turn and after it");

        var otherSide = new TurnSignalCar();
        otherSide.Right = true;
        otherSide.Run(1f, 10f, 0f, 0f);
        otherSide.Run(2f, 10f, -1f, -40f);
        otherSide.Run(10f, 10f, 0f, 0f);
        Check(otherSide.Said.Count == 0 && otherSide.Right && !otherSide.Left, "turn signals: the driver's right signal is not changed by a turn to the left");

        // The driver puts ours off in the turn: it is not raised again in that turn, but is for the next.
        var cancel = new TurnSignalCar();
        cancel.Run(1f, 10f, 0f, 0f);
        cancel.Run(1.5f, 10f, -1f, -30f);
        cancel.Left = false;
        cancel.Run(2.5f, 10f, -1f, -30f);
        int afterFirst = cancel.Raised;
        cancel.Run(1f, 10f, 0f, 0f);
        cancel.Run(1.5f, 10f, -1f, -30f);
        Check(afterFirst == 1 && cancel.Raised == 2 && cancel.Left,
            "turn signals: a signal the driver put off in a turn stays off for that turn and comes on again for the next (" + string.Join(", ", cancel.Said) + ")");

        // The driver puts the other side on: ours is not put back, theirs is not put off.
        var swap = new TurnSignalCar();
        swap.Run(1f, 10f, 0f, 0f);
        swap.Run(1.5f, 10f, -1f, -30f);
        swap.Left = false;
        swap.Right = true;
        swap.Run(10f, 10f, 0f, 0f);
        Check(swap.Said.Count == 1 && swap.Right && !swap.Left && swap.Dropped == 0, "turn signals: a driver who puts the other side on keeps it, and the rules do nothing more");

        // A driver's signal appearing while the car is held for a turn stops a signal being raised beside it.
        var late = new TurnSignalCar();
        late.Run(1f, 10f, 0f, 0f);
        late.Run(0.2f, 10f, -1f, -40f);
        late.Right = true;
        late.Run(3f, 10f, -1f, -40f);
        Check(late.Said.Count == 0 && late.Right && !late.Left, "turn signals: a driver's signal switched on as the turn begins is not raised beside");

        // A car let go and taken again starts clean.
        var again = new TurnSignalCar();
        again.Run(1f, 10f, 0f, 0f);
        again.Run(1.5f, 10f, -1f, -30f);
        bool held = again.State.Own != 0;
        again.State.Reset();
        Check(held && again.State.Own == 0 && again.State.RestFor == TurnSignalRules.Rest, "turn signals: resetting the state forgets the signal held");
    }

    private static void TurnSignalSpeed()
    {
        TurnSignalLoop(2000, out _);   // the rules' code compiled before the measured loop, so the loop's count is its own
        var watch = System.Diagnostics.Stopwatch.StartNew();
        int commands = TurnSignalLoop(1000000, out long bytes);
        double ms = watch.Elapsed.TotalMilliseconds;
        Check(commands > 0 && bytes == 0, "turn signals: a million looks of a weaving drive allocate nothing (" + bytes + " bytes) and ask for signals (" + commands + ")");
        Check(ms < 400.0, "turn signals: a million looks take under 400 ms (" + ms.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " ms; ten looks a second are 0.0002 ms of a second)");
    }

    // The loop is in a method of its own that the compiler optimises whole, so that its allocation count is the rules' own.
    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static int TurnSignalLoop(int looks, out long bytes)
    {
        var state = new TurnSignalState();
        int commands = 0;
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < looks; i++)
        {
            int phase = (i / 15) % 4;
            var look = new TurnSignalLook
            {
                Speed = 10f,
                Steer = phase == 0 ? -1f : phase == 2 ? 1f : 0f,
                Turning = phase == 0 ? -30f : phase == 2 ? 30f : 0f,
                Left = state.Own < 0,
                Right = state.Own > 0
            };
            if (TurnSignalRules.Step(state, look, TurnSignalRules.LookEvery) != TurnSignalCommand.None)
            {
                commands++;
            }
        }
        bytes = GC.GetAllocatedBytesForCurrentThread() - before;
        return commands;
    }

    private static void TurnSignalSources()
    {
        string settings = ModSource(@"Core\Settings.cs");
        string mod = ModSource(@"Core\RealLifeOverhaulMod.cs");
        string tree = ModSource(@"Meta\OptionsTree.cs");
        string list = ModSource(@"Meta\FeatureList.cs");
        string director = ModSource(@"Driving\TurnSignals.cs");
        string game = ModSource(@"Driving\TurnSignalGame.cs");
        string rules = ModSource(@"Driving\TurnSignalRules.cs");
        Check(settings.Contains("AutoSignalsId = \"auto_signals\"") && settings.Contains("public static bool AutoSignals = true;") && settings.Contains("AutoSignals = Bool(modId, AutoSignalsId, AutoSignals);")
              && mod.Contains("AddToggle(Settings.AutoSignalsId, \"reallife_car_auto_signals\", Settings.AutoSignals,") && mod.Contains("v => Settings.AutoSignals = v)")
              && tree.Contains("\"speedometer\", \"auto_signals\", \"radio_hud\"") && list.Contains("Settings.SpeedometerId, Settings.AutoSignalsId"),
            "turn signals: the option is saved, on by default, on the options page in the wheel's section and among the driving feature's");
        Check(mod.Contains("Guard.Add<TurnSignals>(_root)") && mod.Contains("TurnSignals.Logger = context.Logger"), "turn signals: the component is added at the city's start and given the mod's log");

        // The director: guarded, timed, the player's own car only, given back on every way out.
        Check(director.Contains("Guard.For(\"driving\", \"TurnSignals\", typeof(TurnSignals))") && director.Contains("HotSpots.Slot(\"Driving: TurnSignals.Update\")")
              && MemberText(director, "private void Update()").Contains("using var hotSpot = HotSpots.Time(HotUpdate);")
              && MemberText(director, "private void Update()").Contains("Guard.Fail(FrameGuard, e, this)"),
            "turn signals: the component's frame method is behind the guard and timed by name");
        string want = MemberText(director, "private CarController Want()");
        Check(want.Contains("Settings.AutoSignals") && want.Contains("LoadScene.isLoading") && want.Contains("GoingPublic.MineVehicle(own)") && want.Contains("car.controlledByPlayer"),
            "turn signals: only the player's own car, driven by them, with the option on and no load under way");
        string update = MemberText(director, "private void OnUpdate()");
        Check(update.Length > 0 && !update.Contains("new ") && !update.Contains("GetComponent") && !update.Contains("Find") && !update.Contains("ToString") && update.Contains("Time.time")
              && update.Contains("if (now < _nextLook)"),
            "turn signals: a frame costs one comparison with the clock when nothing is followed, and the frame method allocates and looks nothing up");
        Check(MemberText(director, "private void Update()").Contains("Release(") && MemberText(director, "private void OnDestroy()").Contains("Release(")
              && MemberText(director, "private void Fault(Exception e)").Contains("Release(") && MemberText(director, "private void Look(float now)").Contains("Release("),
            "turn signals: the car is let go, and its signal put off, when the feature is switched off after faults, the city goes, something unexpected happens and the car changes or the option goes off");
        string release = MemberText(director, "private void Release(string why)");
        Check(release.Contains("bool was = own < 0 ? left : right;") && release.Contains("if (was)") && release.Contains("TurnSignalGame.Toggle(signals, own < 0)") && release.Contains("_state.Reset();"),
            "turn signals: a signal the rules put on is put off at the let go only if it is still on, and the state is forgotten");
        string apply = MemberText(director, "private void Apply(");
        Check(apply.Contains("if (shows != on)") && apply.Contains("TurnSignalGame.Toggle(_signals, left)") && apply.Contains("if (on && !Shows(left))") && apply.Contains("_refused = _car;"),
            "turn signals: the game's toggle is done once, only for a signal that is not already as asked, and a signal put on that does not show where the game keeps it is put back and the car left alone");
        Check(!director.Contains("PlayerAction") && !director.Contains("Input.") && !director.Contains("Keys."), "turn signals: the player's keys are the game's; the component reads none");

        // The game's own signal object is named in one file only, and the rules know nothing of the game.
        string scripts = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        var elsewhere = new List<string>();
        int files = 0;
        if (Directory.Exists(scripts))
        {
            foreach (string path in Directory.EnumerateFiles(scripts, "*.cs", SearchOption.AllDirectories))
            {
                files++;
                if (System.IO.Path.GetFileName(path) == "TurnSignalGame.cs")
                {
                    continue;
                }
                string code = File.ReadAllText(path);
                if (code.Contains("VehicleBlinker") || code.Contains("ToggleLeftBlinker") || code.Contains("ToggleRightBlinker") || code.Contains("_IsLeftBlinkerOn"))
                {
                    elsewhere.Add(System.IO.Path.GetFileName(path));
                }
            }
        }
        Check(files > 500 && elsewhere.Count == 0 && game.Contains("ToggleLeftBlinker") && game.Contains("ToggleRightBlinker"),
            "turn signals: the game's signal object and its shader values are named in TurnSignalGame alone (" + files + " files read; also in: " + string.Join(", ", elsewhere) + ")");
        Check(!rules.Contains("UnityEngine") && !rules.Contains("Random") && !rules.Contains("DateTime") && !rules.Contains("TickCount"),
            "turn signals: the rules name nothing of the game and take no random number or clock, so every machine signals alike");
        string project = File.Exists(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\EconomyCheck.csproj"))
            ? File.ReadAllText(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\EconomyCheck.csproj")) : string.Empty;
        Check(project.Contains(@"Scripts\Driving\TurnSignalRules.cs"), "turn signals: the rules are compiled into this checker");
    }

    private static void TurnSignalWords()
    {
        Dictionary<string, string>[] langs = LanguageWords(out string[] codes);
        if (!LanguagesRead("turn signals", langs, codes))
        {
            return;
        }
        string lacking = string.Empty;
        foreach (string key in new[] { "reallife_car_auto_signals", "reallife_opt_tip_auto_signals" })
        {
            string l = LanguagesLacking(langs, codes, key);
            lacking += l.Length > 0 ? " " + key + " (" + l + ")" : string.Empty;
        }
        Check(lacking.Length == 0, "turn signals: the option's label and sentence are in every language:" + lacking);
        bool label = langs[0].TryGetValue("reallife_car_auto_signals", out string name) && name.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length <= 9
                     && name.Length <= 60 && !name.EndsWith(".", StringComparison.Ordinal) && name.Contains("turn signals");
        bool tip = langs[0].TryGetValue("reallife_opt_tip_auto_signals", out string sentence) && sentence.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length <= 32
                   && sentence.Length <= 190 && sentence.EndsWith(".", StringComparison.Ordinal) && sentence.Contains("turn signal") && sentence.Contains("yourself");
        Check(label && tip, "turn signals: the label is short and names the signals, and the sentence under it says what comes on, when it goes off and that the driver's own is left alone");

        // What's new: the line is in one row (the next row before a release, the version's own after), as a feature's line with no mark, found by its English words.
        const string line = "The car you drive now switches its turn signal on when you turn and off when the turn is done, and leaves a signal you switched on yourself alone.";
        int rows = 0;
        bool plain = true;
        foreach (KeyValuePair<string, string> row in langs[0])
        {
            if (row.Key.StartsWith("reallife_changes_", StringComparison.Ordinal) && !row.Key.EndsWith("_title", StringComparison.Ordinal) && row.Value.Contains(line))
            {
                rows++;
                plain &= !row.Value.Contains("Fixed: " + line) && !row.Value.Contains("  ") && !row.Value.Contains('!') && !row.Value.Contains((char)0x2014) && !row.Value.Contains((char)0x2013);
            }
        }
        Check(rows == 1 && plain, "turn signals: What's new has the feature's line once, with no fix mark, double space, dash or exclamation mark (" + rows + " row(s))");

        string page = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\docs\features\driving.md"));
        string doc = File.Exists(page) ? File.ReadAllText(page) : string.Empty;
        Check(doc.Contains("## Turn signals") && doc.Contains("TurnSignalRules") && doc.Contains("--turn-signals"), "turn signals: the driving page says what they do, where the code is and how they are checked");
    }
}
