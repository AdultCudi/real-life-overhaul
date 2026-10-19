namespace RealLifeOverhaul
{
    /// <summary>
    /// The last few large pieces of work the mod named (Diagnostics/SessionWatch.Mark), each with the frame and the
    /// time it began in and, where the place that named it also said when it was done, how long it took. A fixed
    /// ring: naming a piece of work is six writes, closing it two; nothing is allocated after the ring is made, and
    /// it is read only for the line of a long frame. No Unity or game types: checked offline (NavCheck, HitchCheck.cs).
    /// </summary>
    public sealed class MarkRing
    {
        public const int Kept = 3;   // pieces of work remembered: what the long-frame line lists

        private readonly string[] _text = new string[Kept];
        private readonly int[] _frame = new int[Kept];
        private readonly float[] _at = new float[Kept];
        private readonly long[] _began = new long[Kept];
        private readonly long[] _spent = new long[Kept];
        private readonly int[] _serial = new int[Kept];
        private int _last;   // the newest piece's number; 0 while there is none

        /// <summary>How many pieces are remembered (at most <see cref="Kept"/>).</summary>
        public int Count => _last < Kept ? _last : Kept;

        /// <summary>Names a piece of work beginning now. The number it returns closes it (<see cref="Close"/>).</summary>
        public int Add(string text, int frame, float at, long ticks)
        {
            int serial = ++_last;
            int slot = serial % Kept;
            _text[slot] = text;
            _frame[slot] = frame;
            _at[slot] = at;
            _began[slot] = ticks;
            _spent[slot] = -1;
            _serial[slot] = serial;
            return serial;
        }

        /// <summary>The piece is done. A number that is no longer in the ring (three newer pieces since) is ignored.</summary>
        public void Close(int serial, long ticks)
        {
            int slot = serial % Kept;
            if (serial > 0 && _serial[slot] == serial && _spent[slot] < 0)
            {
                _spent[slot] = ticks - _began[slot];
            }
        }

        /// <summary>
        /// The piece named <paramref name="back"/> pieces ago (0 the newest). <paramref name="spent"/> is in the
        /// ticks it was given, or negative for a piece nobody closed. False when there is no such piece.
        /// </summary>
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
        /// Which piece explains a long frame, as <see cref="Get"/>'s back: the costliest closed piece that began in
        /// <paramref name="frame"/>, the newest of equals; the newest (0) when none closed in that frame; -1 when the ring
        /// is empty.
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
            return pick >= 0 ? pick : Count > 0 ? 0 : -1;
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
    /// Counts of something that happened at noted times (the navigation obstacles the mod changed: Core/MeshCuts),
    /// to be added up over a stretch of time afterwards. A fixed ring; a note is three writes. No Unity or game
    /// types: checked offline (NavCheck, HitchCheck.cs).
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

        /// <summary>Everything ever noted, whether or not the ring still holds it.</summary>
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
