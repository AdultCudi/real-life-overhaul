namespace RealLifeOverhaul
{
    /// <summary>
    /// A fingerprint of the inputs of a piece of work, so its result can be used again when the same inputs come round
    /// (the minimap's pictures for a city loaded again). 64-bit FNV-1a over the bytes in the order added; not
    /// string.GetHashCode, which differs between runs. Game-free, checked by NavCheck.
    /// COST: nothing allocated; a few nanoseconds a value.
    /// </summary>
    public struct InputPrint
    {
        private const ulong Offset = 14695981039346656037UL;
        private const ulong Prime = 1099511628211UL;

        private ulong _hash;
        private bool _begun;

        /// <summary>The print of what has been added; of nothing, the FNV offset.</summary>
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

        // The float's own bits: 0 and -0 differ, as do NaN payloads; a print, not a comparison of values.
        private static int Bits(float value)
        {
            return System.BitConverter.SingleToInt32Bits(value);
        }
    }
}
