using System;

namespace RealLifeOverhaul
{
    /// <summary>
    /// What the bus stop's panel says about the next bus of a line, and the one figure in it. Nothing of the game or the
    /// engine in it (the build's EconomyCheck runs it).
    ///
    /// THE PROMISE. Before a bus is on the road the figure is the timetable's: the seconds to the line's next call at the
    /// stop (TransitPlan.NextCall). The bus is put on the road so that it keeps it: as far back along its line as the
    /// seconds left take to drive, and held at its start out of view for what is over (TransitDirector). With the bus on
    /// the road the figure counts down, and follows the bus's own estimate (TransitPlan.Pace: the path as it will drive
    /// it, the pace traffic has been making, the lit junctions on the way, a red light it stands at) when that is sooner.
    ///
    /// DELAYED is a real delay only: the estimate has been later than the figure by more than the slack
    /// (TransitPlan.Slack: a few seconds, or a share of the figure) for some seconds while the bus is held up (a light
    /// that will not change, traffic, a closed road), or by twice the slack whatever the reason; the bus could only set
    /// out too late to keep the figure; or no bus has been able to set out for some seconds after it was due. The panel
    /// then shows DELAYED with the new figure, which is the estimate from then on.
    /// </summary>
    public sealed class TransitEta
    {
        public enum Mode { Timetable, EnRoute, Delayed, Here, None }

        private const float LateFor = 3f;        // seconds the estimate must be late by more than the slack before DELAYED
        private const float NoBusFor = 8f;       // seconds past due with no bus able to set out before DELAYED
        private const float RiseLeast = 30f;     // a delayed figure rises again only by this much: it is not to jitter

        private float _late;
        private float _noBus;

        public Mode State { get; private set; } = Mode.None;

        /// <summary>The figure, in real seconds. Shown while <see cref="Figure"/>.</summary>
        public float Seconds { get; private set; }

        /// <summary>The figure first shown for this call: what the bus is held to when it comes (the log's promise against the arrival).</summary>
        public float Promised { get; private set; }

        /// <summary>Seconds since <see cref="Promised"/> was made.</summary>
        public float Since { get; private set; }

        /// <summary>Delayed with a figure (a bus is on the road and its estimate is known); false while no bus could set out.</summary>
        public bool Known { get; private set; } = true;

        /// <summary>How often a figure has been set afresh: whoever shows it may only let it rise when this has changed.</summary>
        public int Epoch { get; private set; }

        public bool HasBus { get; private set; }

        /// <summary>A line's next call by the timetable: the figure is the seconds to it.</summary>
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

        /// <summary>The line does not run now (the night): nothing is promised.</summary>
        public void NoService()
        {
            State = Mode.None;
            Seconds = 0f;
            HasBus = false;
            _late = 0f;
            _noBus = 0f;
        }

        /// <summary>No bus on the road yet: the figure runs down. <paramref name="cannotSetOut"/>: it is due and none could be put on the road.</summary>
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

        /// <summary>A bus has been put on the road: it reckons to stand at the stop in <paramref name="seconds"/> (its hold at its start included).</summary>
        public void Assign(float seconds)
        {
            HasBus = true;
            _late = 0f;
            _noBus = 0f;
            if (State == Mode.Delayed)
            {
                Seconds = seconds;   // (late already: the figure is the bus's from now on)
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
                // It could only set out too late to keep the figure: that is a delay, and the figure is its own.
                State = Mode.Delayed;
                Known = true;
                Seconds = seconds;
                Epoch++;
                return;
            }
            if (seconds < Seconds - TransitPlan.Slack(Seconds))
            {
                Seconds = seconds;   // (sooner than the timetable: it follows)
            }
        }

        /// <summary>
        /// The bus on the road, each tick: its estimate of the seconds to the stop from where it is, and whether it is being
        /// held up (a light, traffic, a closed road).
        /// </summary>
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
                // Late: the figure is the estimate, down as the bus comes on; up again only by a good deal (a long hold).
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

        /// <summary>The bus that was coming is not coming (taken off the road, lost): DELAYED until another is on its way.</summary>
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

        /// <summary>True while a figure is shown: the timetable's, the bus's, or a delayed bus's own.</summary>
        public bool Figure => State == Mode.Timetable || State == Mode.EnRoute || (State == Mode.Delayed && Known);

        /// <summary>The same as another (a follower promoted to the front keeps what it was showing).</summary>
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
