using System.Diagnostics;
using System.Globalization;
using System.Text;

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

    /// <summary>
    /// The frames a piece of work spread over frames took, for its self-check line: how many, the time in all, the slowest
    /// frame, the slowest frame that held more than one item, and the slowest item, against <see cref="WantedMs"/>
    /// (docs/standards/performance.md, RLO-PERF-024). Kept by its owner; a few counts and clock reads a frame, nothing
    /// allocated but the line. Game-free.
    /// </summary>
    public sealed class SpreadWatch
    {
        /// <summary>No frame of the mod's spread work over this many milliseconds.</summary>
        public const double WantedMs = 2.0;

        private readonly StringBuilder _line = new StringBuilder(160);
        private long _ticks, _worst, _worstShared, _slowestTicks;
        private int _frames, _items, _frameItems, _slowest = -1;

        public int Frames => _frames;
        public int Items => _items;
        public double TotalMs => Ms(_ticks);

        /// <summary>The slowest frame of the work.</summary>
        public double WorstMs => Ms(_worst);

        /// <summary>The slowest frame that held more than one item: what the slicing itself let through.</summary>
        public double WorstSharedMs => Ms(_worstShared);

        /// <summary>The slowest single item, and which (the owner's number for it; -1 before any).</summary>
        public double SlowestItemMs => Ms(_slowestTicks);
        public int SlowestItem => _slowest;

        /// <summary>Everything forgotten: a new piece of work.</summary>
        public void Begin()
        {
            _ticks = _worst = _worstShared = _slowestTicks = 0;
            _frames = _items = _frameItems = 0;
            _slowest = -1;
        }

        /// <summary>An item done, begun at <paramref name="began"/> (a Stopwatch reading); true if it is the slowest so far.</summary>
        public bool Item(int item, long began)
        {
            long spent = Stopwatch.GetTimestamp() - began;
            _items++;
            _frameItems++;
            if (spent <= _slowestTicks)
            {
                return false;
            }
            _slowestTicks = spent;
            _slowest = item;
            return true;
        }

        /// <summary>A frame's slice over, the frame's work begun at <paramref name="began"/>.</summary>
        public void Frame(long began)
        {
            long spent = Stopwatch.GetTimestamp() - began;
            _frames++;
            _ticks += spent;
            _worst = spent > _worst ? spent : _worst;
            if (_frameItems > 1 && spent > _worstShared)
            {
                _worstShared = spent;
            }
            _frameItems = 0;
        }

        /// <summary>
        /// PASS while no frame went over <see cref="WantedMs"/>; with <paramref name="lonesAllowed"/>, a frame that held one
        /// item alone (one of the game's own calls that cannot be cut) is not held against it.
        /// </summary>
        public string Verdict(bool lonesAllowed)
        {
            return Verdict(lonesAllowed ? WorstSharedMs : WorstMs);
        }

        /// <summary>The verdict on a slowest frame of <paramref name="worstMs"/>.</summary>
        public static string Verdict(double worstMs)
        {
            return worstMs <= WantedMs ? "PASS" : "FAIL, a frame of " + worstMs.ToString("F1", CultureInfo.InvariantCulture) + " ms";
        }

        /// <summary>The figures for the line: the frames, the time in all, the slowest frame and item (named from <paramref name="names"/>).</summary>
        public string Said(string[] names)
        {
            return Said(names != null && _slowest >= 0 && _slowest < names.Length ? names[_slowest] : null);
        }

        /// <summary>As <see cref="Said(string[])"/>, the slowest item named <paramref name="slowestName"/>.</summary>
        public string Said(string slowestName)
        {
            CultureInfo c = CultureInfo.InvariantCulture;
            _line.Clear();
            _line.Append(_items).Append(" item(s) over ").Append(_frames).Append(" frame(s), ").Append(TotalMs.ToString("F1", c)).Append(" ms in all; the slowest frame ")
                .Append(WorstMs.ToString("F2", c)).Append(" ms");
            if (_worstShared > 0)
            {
                _line.Append(" (").Append(WorstSharedMs.ToString("F2", c)).Append(" ms of those with more than one item)");
            }
            if (_slowest >= 0)
            {
                _line.Append(", the slowest item ").Append(SlowestItemMs.ToString("F2", c)).Append(" ms (").Append(slowestName ?? _slowest.ToString(c)).Append(')');
            }
            _line.Append("; wanted: no frame over ").Append(WantedMs.ToString("F0", c)).Append(" ms");
            return _line.ToString();
        }

        private static double Ms(long ticks)
        {
            return ticks * 1000.0 / Stopwatch.Frequency;
        }
    }
}
