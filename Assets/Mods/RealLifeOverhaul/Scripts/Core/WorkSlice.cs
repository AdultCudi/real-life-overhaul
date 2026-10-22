using System.Diagnostics;

namespace RealLifeOverhaul
{
    /// <summary>
    /// How much of a piece of work spread over frames one frame may do: what fits in a budget of milliseconds, and
    /// always the first item so the work is never stuck. A heavy item is done only as the first of a slice. A struct
    /// made at each slice's start, nothing allocated; <see cref="More()"/> costs one count and a clock read every
    /// <c>lookEvery</c> items. Game-free.
    /// </summary>
    public struct WorkSlice
    {
        private readonly long _until;      // the clock's reading at which the slice is over
        private readonly int _lookEvery;   // items between two looks at the clock
        private int _done;                 // items handed out in this slice
        private bool _over;

        private WorkSlice(long until, int lookEvery)
        {
            _until = until;
            _lookEvery = lookEvery < 1 ? 1 : lookEvery;
            _done = 0;
            _over = false;
        }

        /// <summary>A slice of <paramref name="milliseconds"/> from now; zero or less: one item a slice.</summary>
        public static WorkSlice Of(double milliseconds, int lookEvery)
        {
            long now = Stopwatch.GetTimestamp();
            double ticks = milliseconds > 0.0 ? milliseconds * Stopwatch.Frequency / 1000.0 : 0.0;
            long until = ticks >= long.MaxValue - (double)now ? long.MaxValue : now + (long)ticks;
            return new WorkSlice(until, milliseconds > 0.0 ? lookEvery : 1);
        }

        /// <summary>A slice with no end: all the work at once (behind the loading screen, where a long frame is not seen).</summary>
        public static WorkSlice Whole => new WorkSlice(long.MaxValue, int.MaxValue);

        /// <summary>True once <see cref="More()"/> has said no or <see cref="End"/> was called.</summary>
        public bool Over => _over;

        public int Done => _done;

        /// <summary>True if one more item may be done in this slice (always the first).</summary>
        public bool More() => More(false);

        /// <summary>As <see cref="More()"/>; a <paramref name="heavy"/> item is done only as the first of the slice.</summary>
        public bool More(bool heavy)
        {
            if (_over)
            {
                return false;
            }
            if (_done > 0 && (heavy || (_done % _lookEvery == 0 && Stopwatch.GetTimestamp() >= _until)))
            {
                _over = true;
                return false;
            }
            _done++;
            return true;
        }

        /// <summary>Ends the slice now: <see cref="More()"/> says no from here on.</summary>
        public void End()
        {
            _over = true;
        }
    }
}
