namespace RealLifeOverhaul
{
    /// <summary>
    /// A fingerprint of everything a piece of work is made from, so that what it made can be kept and used again when the
    /// same inputs come round (the minimap's two pictures for a city loaded again in the same session: Navigation/NavGraph,
    /// NavBase). FNV-1a over the inputs' bytes, 64 bits, in the order they are added: any value changed, added or moved
    /// gives another print. string.GetHashCode is not used (it differs between runs).
    ///
    /// A struct: nothing is allocated. Cost: a few nanoseconds a value (some 0.3 ms for a city's road graph). No Unity or
    /// game types: checked offline (NavCheck, LoadPaceCheck.cs).
    /// </summary>
    public struct InputPrint
    {
        private const ulong Offset = 14695981039346656037UL;
        private const ulong Prime = 1099511628211UL;

        private ulong _hash;
        private bool _begun;

        /// <summary>The print of what has been added (the print of nothing is the FNV offset).</summary>
        public ulong Value => _begun ? _hash : Offset;

        public void Add(int value)
        {
            if (!_begun)
            {
                _hash = Offset;
                _begun = true;
            }
            unchecked
            {
                uint v = (uint)value;
                _hash = (_hash ^ (v & 0xFF)) * Prime;
                _hash = (_hash ^ ((v >> 8) & 0xFF)) * Prime;
                _hash = (_hash ^ ((v >> 16) & 0xFF)) * Prime;
                _hash = (_hash ^ (v >> 24)) * Prime;
            }
        }

        public void Add(float value)
        {
            Add(Bits(value));
        }

        public void Add(bool value)
        {
            Add(value ? 1 : 0);
        }

        /// <summary>An array's length and then each value in turn; a null array counts as length -1.</summary>
        public void Add(int[] values)
        {
            Add(values != null ? values.Length : -1);
            for (int i = 0; values != null && i < values.Length; i++)
            {
                Add(values[i]);
            }
        }

        public void Add(float[] values)
        {
            Add(values != null ? values.Length : -1);
            for (int i = 0; values != null && i < values.Length; i++)
            {
                Add(Bits(values[i]));
            }
        }

        public void Add(bool[] values)
        {
            Add(values != null ? values.Length : -1);
            for (int i = 0; values != null && i < values.Length; i++)
            {
                Add(values[i] ? 1 : 0);
            }
        }

        public void Add(byte[] values)
        {
            Add(values != null ? values.Length : -1);
            if (values == null)
            {
                return;
            }
            unchecked
            {
                for (int i = 0; i < values.Length; i++)
                {
                    _hash = (_hash ^ values[i]) * Prime;
                }
            }
        }

        // The float's own bits (0 and -0 differ, as do two NaNs of different payloads: a print, not a comparison of values).
        private static int Bits(float value)
        {
            return System.BitConverter.SingleToInt32Bits(value);
        }
    }
}
