using System;

namespace RealLifeOverhaul
{
    /// <summary>
    /// What the bus stop's panel says about the next bus, and the one figure in it. Nothing of
    /// the game or the engine in it (it is run in a console test).
    ///
    /// The figure (<see cref="Seconds"/>) only ever goes down. Before a bus is on the road it
    /// is the timetable's wait; with a bus on the road it is that bus's metres of line still
    /// to drive over the speed it has been making while moving, and never more than was shown
    /// before. When that cannot be kept (the bus stands for long, is much further off than
    /// was promised, was lost, or none could set out) the panel says DELAYED and shows no
    /// figure; once the bus has been moving again for a moment the figure is set afresh from
    /// where it now is, once, and goes down from there.
    /// </summary>
    public sealed class TransitEta
    {
        public enum Mode { Timetable, EnRoute, Delayed, Here }

        public const float StartSpeed = 7f;      // m/s assumed of a bus that has only just set out
        public const float LeastSpeed = 2.5f;
        private const float LateSlack = 12f;     // seconds a bus may be behind the figure before it is called late
        private const float LateFor = 6f;        // ...and for how long
        private const float StillFor = 12f;      // seconds standing (a red light is usually shorter) before DELAYED
        private const float NoBusFor = 6f;       // seconds past due with no bus able to set out before DELAYED
        private const float DelayShownLeast = 4f;
        private const float MovingFor = 2f;      // seconds it must have been moving again before a new figure is given
        private const float Moving = 1.5f;       // m/s

        private float _late;
        private float _still;
        private float _delayed;
        private float _moving;
        private float _lastMetres;

        public Mode State { get; private set; }

        /// <summary>The figure, in seconds. Not shown while <see cref="State"/> is Delayed or Here.</summary>
        public float Seconds { get; private set; }

        /// <summary>Metres a second the bus has been making while it moved.</summary>
        public float Speed { get; private set; } = StartSpeed;

        /// <summary>How often a figure has been set afresh: by the timetable, or coming out of a delay. Whoever shows it may only let it rise when this has changed.</summary>
        public int Epoch { get; private set; }

        public bool HasBus { get; private set; }

        /// <summary>A new wait by the timetable begins (arriving at the stop, or a bus has just left).</summary>
        public void Timetable(float seconds)
        {
            State = Mode.Timetable;
            Seconds = seconds > 0f ? seconds : 0f;
            HasBus = false;
            Epoch++;
            Clear();
        }

        /// <summary>No bus on the road: the timetable's wait runs down. <paramref name="cannotSetOut"/>: it is due and none could be sent.</summary>
        public void Wait(float dt, bool cannotSetOut)
        {
            HasBus = false;
            if (State == Mode.Here || State == Mode.EnRoute)
            {
                State = Mode.Timetable;
            }
            Seconds = Seconds > dt ? Seconds - dt : 0f;
            if (State == Mode.Delayed)
            {
                _delayed += dt;
                return;
            }
            _late = cannotSetOut ? _late + dt : 0f;
            if (_late > NoBusFor)
            {
                Delay();
            }
        }

        /// <summary>A bus has been put on the road, this many metres of line from the stop.</summary>
        public void Assign(float metres)
        {
            HasBus = true;
            Speed = StartSpeed;
            _lastMetres = metres;
            _late = 0f;
            _still = 0f;
            _moving = 0f;
            float raw = metres / StartSpeed;
            if (State == Mode.Delayed)
            {
                return;   // it stays DELAYED until this bus has been moving for a moment (Track)
            }
            if (raw > Seconds + LateSlack)
            {
                Delay();   // further off than was promised: said so, not counted up
                return;
            }
            State = Mode.EnRoute;
            if (raw < Seconds)
            {
                Seconds = raw;
            }
        }

        /// <summary>The bus on the road, each tick: metres of line still to drive, and whether it is being held (a light, a closure, traffic).</summary>
        public void Track(float dt, float metres, bool held)
        {
            if (dt <= 0f)
            {
                return;
            }
            HasBus = true;
            float progress = (_lastMetres - metres) / dt;
            _lastMetres = metres;
            if (progress < 0f)
            {
                progress = 0f;
            }
            if (progress > 30f)
            {
                progress = 30f;   // it was moved up the line out of view: not a speed
            }
            bool moving = progress >= Moving && !held;
            if (moving)
            {
                Speed += (progress - Speed) * (dt < 3f ? dt / 3f : 1f);   // only while moving: a red light is a wait, not a slower bus
                if (Speed < LeastSpeed)
                {
                    Speed = LeastSpeed;
                }
            }
            float raw = metres / Speed;
            if (State == Mode.Delayed)
            {
                _delayed += dt;
                _moving = moving ? _moving + dt : 0f;
                if (_delayed >= DelayShownLeast && _moving >= MovingFor)
                {
                    State = Mode.EnRoute;
                    Seconds = raw;
                    Epoch++;
                    _late = 0f;
                    _still = 0f;
                }
                return;
            }
            State = Mode.EnRoute;
            if (raw < Seconds)
            {
                Seconds = raw;
            }
            _still = progress < 0.3f || held ? _still + dt : 0f;
            _late = raw > Seconds + LateSlack ? _late + dt : 0f;
            if (_still > StillFor || _late > LateFor)
            {
                Delay();
            }
        }

        /// <summary>The bus that was coming is not coming (taken off the road, lost): DELAYED until another is moving.</summary>
        public void Lost()
        {
            HasBus = false;
            Delay();
        }

        public void Arrived()
        {
            State = Mode.Here;
            Seconds = 0f;
            HasBus = true;
            Clear();
        }

        private void Delay()
        {
            if (State != Mode.Delayed)
            {
                State = Mode.Delayed;
                _delayed = 0f;
            }
            _moving = 0f;
            _late = 0f;
            _still = 0f;
        }

        private void Clear()
        {
            _late = 0f;
            _still = 0f;
            _delayed = 0f;
            _moving = 0f;
        }
    }
}
