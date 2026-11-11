using System;

namespace RealLifeOverhaul
{
    /// <summary>What the turn signal rules ask of the car after a look at it.</summary>
    public enum TurnSignalCommand
    {
        None = 0,        // leave the signals as they are
        LeftOn = 1,      // the left signal comes on (the game's own toggle puts the right one off with it)
        RightOn = 2,     // the right signal comes on
        LeftOff = 3,     // the left signal, which the rules put on, goes off
        RightOff = 4     // the right signal, which the rules put on, goes off
    }

    /// <summary>One look at the driven car, taken the same way for any car: the figures a turn is judged by.</summary>
    public struct TurnSignalLook
    {
        public float Speed;     // m/s along the car's nose; negative when it is backing
        public float Steer;     // the steering asked for, -1 full left to 1 full right
        public float Turning;   // degrees a second the nose is swinging, positive to the right and negative to the left
        public bool Left;       // the car's left signal shows, whoever switched it on
        public bool Right;      // the car's right signal shows
    }

    /// <summary>What the rules keep between two looks at one car; reset when a car is taken or let go.</summary>
    public sealed class TurnSignalState
    {
        public int Own;             // -1 the left signal, 1 the right, 0 none: the signal the rules switched on and still answer for
        public int Held;            // the way the car has been turning at the last looks (-1, 1), or 0
        public float HeldFor;       // seconds it has turned that way
        public float HeldSwing;     // degrees the nose swung that way in those seconds
        public float OnFor;         // seconds the signal in Own has been on
        public float Swing;         // degrees the nose has swung the signalled way since the turn began
        public float StraightFor;   // seconds the wheel has been straight and the nose steady
        public float RunFor;        // seconds of straight running at a pace, with the wheel straight
        public float RestFor;       // seconds since the rules last put a signal off
        public int Yielded;         // the side the driver put off, or went away from, in this turn: not raised again until the car runs straight

        public TurnSignalState()
        {
            Reset();
        }

        /// <summary>Forgets the car: nothing is on, nothing is held, and a signal may come on at once.</summary>
        public void Reset()
        {
            Own = 0;
            Held = 0;
            HeldFor = 0f;
            HeldSwing = 0f;
            OnFor = 0f;
            Swing = 0f;
            StraightFor = 0f;
            RunFor = 0f;
            RestFor = TurnSignalRules.Rest;
            Yielded = 0;
        }
    }

    /// <summary>
    /// When the car the player drives signals a turn by itself: the signal of the side it is turning to comes on once it has
    /// been turning a moment and goes off when the turn is made, and a signal the driver switched on is never touched.
    /// Game-free: EconomyCheck compiles this file. A pure function of the looks it is given, so the same drive gives the
    /// same signals on every machine. Cost: a few comparisons a look, no allocation.
    /// </summary>
    public static class TurnSignalRules
    {
        public const float LookEvery = 0.1f;       // seconds between two looks at a driven car
        public const float MostGap = 0.2f;         // seconds one look is taken to cover at most: a hitch is not a turn held
        public const float MinSpeed = 2.5f;        // m/s (9 km/h) forward: slower than this is parking, not a turn
        public const float StaySpeed = 0.5f;       // m/s forward: a signal already up stays through a stop in the middle of a turn
        public const float ReverseSpeed = -1.5f;   // m/s: backing at this pace or faster takes the signal off
        public const float TurnSteer = 0.5f;       // of full lock: the wheel a turn needs; a correction in a lane is less or shorter
        public const float TurnRate = 9f;          // degrees a second the nose must swing: the car is really turning, not only steered
        public const float TurnHold = 0.4f;        // seconds a turn must last before its signal comes on
        public const float StraightSteer = 0.2f;   // of full lock: under this the wheel is straight
        public const float StraightRate = 5f;      // degrees a second: under this the nose is steady
        public const float StraightHold = 0.8f;    // seconds straight after the turn before the signal goes off
        public const float TurnDone = 20f;         // degrees the nose must have swung the signalled way for the turn to count as made
        public const float LeastOn = 4f;           // seconds a signal stays up once on: two full flashes of the game's own (its cycle is 2 s)
        public const float SwitchAfter = 2f;       // seconds a signal stays on its side before the car turning the other way puts it over: a weave does not make it flicker
        public const float GiveUp = 6f;            // seconds of straight running, the turn never made, after which a signal that came on for nothing goes off
        public const float Rest = 1f;              // seconds between a signal going off and the next coming on
        public const float ReArm = 0.5f;           // seconds straight before a signal the driver put off may be raised again
        private const float Cap = 60f;             // seconds: a timer is held here, so a long drive never grows one without end

        /// <summary>
        /// One look, <paramref name="dt"/> seconds after the one before, answered with what to do to the car's signals.
        /// Nothing outside <paramref name="state"/> is kept. A signal that is up and was not switched on by the rules is the
        /// driver's: it is never touched and none is added beside it.
        /// </summary>
        public static TurnSignalCommand Step(TurnSignalState state, TurnSignalLook look, float dt)
        {
            if (dt <= 0f)
            {
                return TurnSignalCommand.None;
            }
            if (dt > MostGap)
            {
                dt = MostGap;
            }
            float rate = look.Turning < 0f ? -look.Turning : look.Turning;
            float wheel = look.Steer < 0f ? -look.Steer : look.Steer;
            bool going = look.Speed >= (state.Own != 0 ? StaySpeed : MinSpeed);
            int turning = going && wheel >= TurnSteer && rate >= TurnRate ? (look.Turning > 0f ? 1 : -1) : 0;
            bool straight = wheel < StraightSteer && rate < StraightRate;

            // The way the car has been turning, for how long, and how long it has run straight.
            if (turning != 0 && turning == state.Held)
            {
                state.HeldFor = Math.Min(state.HeldFor + dt, Cap);
                state.HeldSwing += rate * dt;
            }
            else
            {
                state.Held = turning;
                state.HeldFor = turning != 0 ? dt : 0f;
                state.HeldSwing = turning != 0 ? rate * dt : 0f;
            }
            state.StraightFor = straight ? Math.Min(state.StraightFor + dt, Cap) : 0f;
            state.RunFor = straight && look.Speed >= MinSpeed ? Math.Min(state.RunFor + dt, Cap) : 0f;
            state.RestFor = Math.Min(state.RestFor + dt, Rest);
            if (state.Yielded != 0 && state.StraightFor >= ReArm)
            {
                state.Yielded = 0;
            }

            if (state.Own != 0)
            {
                return Holding(state, look, dt, turning);
            }
            if (look.Left || look.Right)
            {
                return TurnSignalCommand.None;   // the driver's own signal is up
            }
            if (turning == 0 || turning == state.Yielded || state.HeldFor < TurnHold || state.RestFor < Rest)
            {
                return TurnSignalCommand.None;
            }
            state.Own = turning;
            state.OnFor = 0f;
            state.Swing = state.HeldSwing;
            return turning < 0 ? TurnSignalCommand.LeftOn : TurnSignalCommand.RightOn;
        }

        // A signal of the rules is up: whether the driver took it, whether the turn is made, whether the car now turns the other way.
        private static TurnSignalCommand Holding(TurnSignalState state, TurnSignalLook look, float dt, int turning)
        {
            bool ours = state.Own < 0 ? look.Left && !look.Right : look.Right && !look.Left;
            if (!ours)
            {
                // Put off by the driver, or put over to the other side: it is theirs now, and not raised again for this turn.
                state.Yielded = state.Own;
                state.Own = 0;
                state.OnFor = 0f;
                state.Swing = 0f;
                return TurnSignalCommand.None;
            }
            state.OnFor = Math.Min(state.OnFor + dt, Cap);
            float along = state.Own > 0 ? look.Turning : -look.Turning;
            if (along > 0f)
            {
                state.Swing += along * dt;
            }
            bool backing = look.Speed <= ReverseSpeed;
            bool made = state.Swing >= TurnDone && state.StraightFor >= StraightHold && state.OnFor >= LeastOn;
            if (backing || made || state.RunFor >= GiveUp)
            {
                TurnSignalCommand off = state.Own < 0 ? TurnSignalCommand.LeftOff : TurnSignalCommand.RightOff;
                state.Own = 0;
                state.OnFor = 0f;
                state.Swing = 0f;
                state.RestFor = 0f;
                return off;
            }
            if (turning != 0 && turning != state.Own && state.HeldFor >= TurnHold && state.OnFor >= SwitchAfter)
            {
                state.Own = turning;
                state.OnFor = 0f;
                state.Swing = state.HeldSwing;
                return turning < 0 ? TurnSignalCommand.LeftOn : TurnSignalCommand.RightOn;
            }
            return TurnSignalCommand.None;
        }
    }
}
