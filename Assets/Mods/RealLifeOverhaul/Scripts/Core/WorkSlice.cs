using System.Diagnostics;

namespace RealLifeOverhaul
{
    /// <summary>
    /// How much of a piece of work spread over frames one frame may do: as much as fits in a budget of milliseconds,
    /// with the clock looked at every few items, and always the first item (so the work is never stuck). An item known to
    /// be heavy (a picture not in its cache, a renderer list not yet sorted) is done only as the first of a slice, so a
    /// frame never takes a heavy one after others. Used by the work of the first minute after a load that cannot be done
    /// behind the loading screen: the contacts' pictures (Phone/Contacts/ContactPainter), the bus stops' searches and
    /// dressings (Transport/TransitMap, TransitDirector), and the performance tweaks' looks (Performance/Tweaks).
    ///
    /// A struct made at the start of each slice: nothing is allocated. Cost of <see cref="More()"/>: one count, and one
    /// read of the clock every <c>lookEvery</c> items. No Unity or game types: checked offline (NavCheck, LoadPaceCheck.cs).
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

        /// <summary>A slice of <paramref name="milliseconds"/> from now, the clock looked at every <paramref name="lookEvery"/> items. Zero or less: one item a slice.</summary>
        public static WorkSlice Of(double milliseconds, int lookEvery)
        {
            long now = Stopwatch.GetTimestamp();
            double ticks = milliseconds > 0.0 ? milliseconds * Stopwatch.Frequency / 1000.0 : 0.0;
            long until = ticks >= long.MaxValue - (double)now ? long.MaxValue : now + (long)ticks;
            return new WorkSlice(until, milliseconds > 0.0 ? lookEvery : 1);
        }

        /// <summary>A slice with no end: all the work at once (behind the loading screen, where a long frame is not seen).</summary>
        public static WorkSlice Whole => new WorkSlice(long.MaxValue, int.MaxValue);

        /// <summary>True once <see cref="More()"/> has said no (or <see cref="End"/> was called): the slice's time was spent with work left.</summary>
        public bool Over => _over;

        /// <summary>Items handed out in this slice so far.</summary>
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

        /// <summary>Ends the slice now (the item just done was a heavy one, or the work must wait): <see cref="More()"/> says no from here on.</summary>
        public void End()
        {
            _over = true;
        }
    }
}
