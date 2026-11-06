namespace RealLifeOverhaul
{
    /// <summary>
    /// The last large pieces of work the mod named (SessionWatch.Mark), with the frame and time each began in and, once
    /// closed, how long it took. A fixed ring: naming is seven writes, closing two; read only for a long frame's line,
    /// which names the pieces begun by that frame's end. The frame after a long one names more before the line is
    /// written (the bus stops' tick that fell due puts out up to six), so the ring holds far more than the line lists.
    /// Game-free.
    /// </summary>
    public sealed class MarkRing
    {
        public const int Kept = 16;    // pieces of work remembered: those named after a long frame, before its line, leave the frame's own held
        public const int Listed = 3;   // pieces a long frame's line lists: the newest begun by its end

        private readonly string[] _text = new string[Kept];
        private readonly int[] _frame = new int[Kept];
        private readonly float[] _at = new float[Kept];
        private readonly long[] _began = new long[Kept];
        private readonly long[] _spent = new long[Kept];
        private readonly int[] _serial = new int[Kept];
        private int _last;                    // the newest piece's number; 0 while there is none
        private int _gone = int.MaxValue;     // the frame of the newest piece that has left the ring; none has yet

        /// <summary>How many pieces are remembered (at most <see cref="Kept"/>).</summary>
        public int Count => _last < Kept ? _last : Kept;

        /// <summary>Names a piece of work beginning now. The number it returns closes it (<see cref="Close"/>).</summary>
        public int Add(string text, int frame, float at, long ticks)
        {
            int serial = ++_last;
            int slot = serial % Kept;
            if (serial > Kept)
            {
                _gone = _frame[slot];   // the oldest piece leaves the ring
            }
            _text[slot] = text;
            _frame[slot] = frame;
            _at[slot] = at;
            _began[slot] = ticks;
            _spent[slot] = -1;
            _serial[slot] = serial;
            return serial;
        }

        /// <summary>The piece is done; a number no longer in the ring is ignored.</summary>
        public void Close(int serial, long ticks)
        {
            int slot = serial % Kept;
            if (serial > 0 && _serial[slot] == serial && _spent[slot] < 0)
            {
                _spent[slot] = ticks - _began[slot];
            }
        }

        /// <summary>The piece named <paramref name="back"/> ago (0 the newest); <paramref name="spent"/> negative if never closed.</summary>
        public bool Get(int back, out string text, out int frame, out float at, out long spent)
        {
            if (back < 0 || back >= Count)
            {
                text = null;
                frame = 0;
                at = 0f;
                spent = -1;
                return false;
            }
            int slot = (_last - back) % Kept;
            text = _text[slot];
            frame = _frame[slot];
            at = _at[slot];
            spent = _spent[slot];
            return true;
        }

        /// <summary>
        /// The costliest closed piece begun in <paramref name="frame"/>, as a back; else the newest begun by its end
        /// (<see cref="NewestBy"/>); -1 when there is none. A piece begun after the frame never made it long.
        /// </summary>
        public int Costliest(int frame)
        {
            int pick = -1;
            long most = -1;
            for (int back = 0; back < Count; back++)
            {
                int slot = (_last - back) % Kept;
                if (_frame[slot] == frame && _spent[slot] >= 0 && _spent[slot] > most)
                {
                    most = _spent[slot];
                    pick = back;
                }
            }
            return pick >= 0 ? pick : NewestBy(frame, 0);
        }

        /// <summary>The <paramref name="nth"/> newest piece begun in <paramref name="frame"/> or before it (0 the newest of them), as a back; -1 when there is none.</summary>
        public int NewestBy(int frame, int nth)
        {
            int seen = 0;
            for (int back = 0; back < Count; back++)
            {
                if (_frame[(_last - back) % Kept] <= frame && seen++ == nth)
                {
                    return back;
                }
            }
            return -1;
        }

        /// <summary>How many of the pieces held began after <paramref name="frame"/>: named in the frames after it, before its line.</summary>
        public int After(int frame)
        {
            int after = 0;
            for (int back = 0; back < Count; back++)
            {
                if (_frame[(_last - back) % Kept] > frame)
                {
                    after++;
                }
            }
            return after;
        }

        /// <summary>
        /// Whether the pieces begun by the end of <paramref name="frame"/> have all left the ring, pushed out by pieces named
        /// after it: then what the mod did up to that frame is no longer known.
        /// </summary>
        public bool LostBy(int frame)
        {
            return _gone <= frame && NewestBy(frame, 0) < 0;
        }

        /// <summary>Ticks spent in the closed pieces that began in a frame: the mod's named share of that frame.</summary>
        public long SpentIn(int frame)
        {
            long total = 0;
            for (int back = 0; back < Count; back++)
            {
                int slot = (_last - back) % Kept;
                if (_frame[slot] == frame && _spent[slot] > 0)
                {
                    total += _spent[slot];
                }
            }
            return total;
        }
    }

    /// <summary>
    /// Counts of something at noted times (the navigation obstacles the mod changed), to be added up over a stretch of
    /// time afterwards. A fixed ring; a note is three writes. Game-free.
    /// </summary>
    public sealed class CountRing
    {
        private readonly float[] _times;
        private readonly int[] _counts;
        private int _next;

        public CountRing(int kept)
        {
            _times = new float[kept < 1 ? 1 : kept];
            _counts = new int[_times.Length];
        }

        /// <summary>Everything ever noted, held or not.</summary>
        public int Total { get; private set; }

        public void Note(float time, int count)
        {
            if (count <= 0)
            {
                return;
            }
            _times[_next] = time;
            _counts[_next] = count;
            _next = (_next + 1) % _times.Length;
            Total += count;
        }

        /// <summary>What was noted between two times, both ends counted, of the notes the ring still holds.</summary>
        public int Between(float from, float to)
        {
            int sum = 0;
            for (int i = 0; i < _times.Length; i++)
            {
                if (_counts[i] > 0 && _times[i] >= from && _times[i] <= to)
                {
                    sum += _counts[i];
                }
            }
            return sum;
        }
    }
}
