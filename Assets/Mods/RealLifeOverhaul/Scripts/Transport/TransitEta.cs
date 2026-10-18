using System;

namespace RealLifeOverhaul
{
    /// <summary>
    /// What a bus stop's panel says about a line's next bus: the timetable's figure, then the bus's own estimate when sooner,
    /// and DELAYED only for a real delay (late beyond the slack while held up, or no bus able to set out). EconomyCheck runs it.
    /// </summary>
    public sealed class TransitEta
    {
        public enum Mode { Timetable, EnRoute, Delayed, Here, None }

        private const float LateFor = 3f;        // seconds the estimate must be late by more than the slack before DELAYED
        private const float NoBusFor = 8f;       // seconds past due with no bus able to set out before DELAYED
        private const float RiseLeast = 30f;     // seconds a delayed figure must rise by before it is shown, so it does not jitter

        private float _late;
        private float _noBus;

        public Mode State { get; private set; } = Mode.None;

        /// <summary>The figure in real seconds, shown while <see cref="Figure"/>.</summary>
        public float Seconds { get; private set; }

        /// <summary>The figure first shown for this call, which the bus is held to.</summary>
        public float Promised { get; private set; }

        public float Since { get; private set; }   // seconds since Promised was made

        /// <summary>False while no bus could set out.</summary>
        public bool Known { get; private set; } = true;

        /// <summary>Counts each figure set afresh: a figure may rise only when this has changed.</summary>
        public int Epoch { get; private set; }

        public bool HasBus { get; private set; }

        public void Timetable(float seconds)
        {
            State = Mode.Timetable;
            Seconds = Math.Max(0f, seconds);
            Promised = Seconds;
            Since = 0f;
            Known = true;
            HasBus = false;
            _late = 0f;
            _noBus = 0f;
            Epoch++;
        }

        /// <summary>The line does not run now: nothing is promised.</summary>
        public void NoService()
        {
            State = Mode.None;
            Seconds = 0f;
            HasBus = false;
            _late = 0f;
            _noBus = 0f;
        }

        /// <summary>No bus on the road yet: the figure runs down; <paramref name="cannotSetOut"/> when none could be put on the road.</summary>
        public void Wait(float dt, bool cannotSetOut)
        {
            HasBus = false;
            Since += dt;
            if (State == Mode.None || State == Mode.Here)
            {
                return;
            }
            Seconds = Math.Max(0f, Seconds - dt);
            if (State == Mode.Delayed)
            {
                return;
            }
            _noBus = cannotSetOut && Seconds <= 0.01f ? _noBus + dt : 0f;
            if (_noBus > NoBusFor)
            {
                State = Mode.Delayed;
                Known = false;
                Epoch++;
            }
        }

        /// <summary>A bus is on the road, reckoning to stand at the stop in <paramref name="seconds"/>.</summary>
        public void Assign(float seconds)
        {
            HasBus = true;
            _late = 0f;
            _noBus = 0f;
            if (State == Mode.Delayed)
            {
                Seconds = seconds;   // late already: the figure is the bus's from now on
                Known = true;
                Epoch++;
                return;
            }
            if (State == Mode.None || State == Mode.Here)
            {
                Promised = seconds;
                Since = 0f;
                Seconds = seconds;
                State = Mode.EnRoute;
                Epoch++;
                return;
            }
            State = Mode.EnRoute;
            if (seconds > Seconds + TransitPlan.Slack(Seconds))
            {
                // It could only set out too late to keep the figure: a delay.
                State = Mode.Delayed;
                Known = true;
                Seconds = seconds;
                Epoch++;
                return;
            }
            if (seconds < Seconds - TransitPlan.Slack(Seconds))
            {
                Seconds = seconds;   // sooner than the timetable: it follows
            }
        }

        /// <summary>Each tick with the bus on the road: its estimate to the stop, and whether it is held up.</summary>
        public void Track(float dt, float estimate, bool held)
        {
            if (dt <= 0f)
            {
                return;
            }
            HasBus = true;
            Since += dt;
            Seconds = Math.Max(0f, Seconds - dt);
            if (State == Mode.Delayed)
            {
                // The figure follows the estimate down, and up again only by a good deal (a long hold).
                Known = true;
                if (estimate < Seconds || estimate > Seconds + RiseLeast)
                {
                    if (estimate > Seconds)
                    {
                        Epoch++;
                    }
                    Seconds = estimate;
                }
                return;
            }
            State = Mode.EnRoute;
            float slack = TransitPlan.Slack(Seconds);
            if (estimate < Seconds - slack)
            {
                Seconds = estimate;   // coming sooner than the figure: it follows
            }
            _late = estimate > Seconds + slack ? _late + dt : 0f;
            if (_late >= LateFor && (held || estimate > Seconds + 2f * slack))
            {
                State = Mode.Delayed;
                Known = true;
                Seconds = estimate;
                Epoch++;
                _late = 0f;
            }
        }

        /// <summary>The bus that was coming is not: DELAYED until another is on its way.</summary>
        public void Lost()
        {
            HasBus = false;
            if (State != Mode.Delayed)
            {
                Epoch++;
            }
            State = Mode.Delayed;
            Known = false;
            _late = 0f;
        }

        public void Arrived()
        {
            State = Mode.Here;
            Seconds = 0f;
            HasBus = true;
            _late = 0f;
            _noBus = 0f;
        }

        /// <summary>True while a figure is shown.</summary>
        public bool Figure => State == Mode.Timetable || State == Mode.EnRoute || (State == Mode.Delayed && Known);

        /// <summary>Takes another's state, as a follower promoted to the front keeps what it showed.</summary>
        public void CopyFrom(TransitEta other)
        {
            State = other.State;
            Seconds = other.Seconds;
            Promised = other.Promised;
            Since = other.Since;
            Known = other.Known;
            HasBus = other.HasBus;
            _late = other._late;
            _noBus = other._noBus;
            Epoch++;
        }
    }
}
