namespace RealLifeOverhaul
{
    /// <summary>
    /// A curve of the game's (an AnimationCurve of plain keys) as the engine evaluates it: cubic Hermite between keys, held
    /// beyond the ends. Game-free, so the model and NavCheck read the curves as the car does; evaluating allocates nothing.
    /// </summary>
    public sealed class CurveKeys
    {
        private float[] _time = new float[24];
        private float[] _value = new float[24];
        private float[] _in = new float[24];
        private float[] _out = new float[24];

        public int Count { get; private set; }

        public float Time(int i) => _time[i];
        public float Value(int i) => _value[i];
        public float InSlope(int i) => _in[i];
        public float OutSlope(int i) => _out[i];

        public void Clear()
        {
            Count = 0;
        }

        /// <summary>A key after the last one (keys go in order of their time).</summary>
        public void Add(float time, float value, float inSlope, float outSlope)
        {
            if (Count == _time.Length)
            {
                int size = Count * 2;
                System.Array.Resize(ref _time, size);
                System.Array.Resize(ref _value, size);
                System.Array.Resize(ref _in, size);
                System.Array.Resize(ref _out, size);
            }
            _time[Count] = time;
            _value[Count] = value;
            _in[Count] = inSlope;
            _out[Count] = outSlope;
            Count++;
        }

        public void CopyFrom(CurveKeys other)
        {
            Clear();
            for (int i = 0; i < other.Count; i++)
            {
                Add(other._time[i], other._value[i], other._in[i], other._out[i]);
            }
        }

        /// <summary>The straight line from 0 to 1 the game's linear springs and pedals have.</summary>
        public void Straight()
        {
            Clear();
            Add(0f, 0f, 1f, 1f);
            Add(1f, 1f, 1f, 1f);
        }

        /// <summary>The curve's value at x, as the engine evaluates it.</summary>
        public float Evaluate(float x)
        {
            int n = Count;
            if (n == 0)
            {
                return 0f;
            }
            if (n == 1 || x <= _time[0])
            {
                return _value[0];
            }
            if (x >= _time[n - 1])
            {
                return _value[n - 1];
            }
            int i = Segment(x);
            float dt = _time[i + 1] - _time[i];
            float s = (x - _time[i]) / dt;
            float s2 = s * s;
            float s3 = s2 * s;
            return (2f * s3 - 3f * s2 + 1f) * _value[i] + (s3 - 2f * s2 + s) * dt * _out[i] + (-2f * s3 + 3f * s2) * _value[i + 1] + (s3 - s2) * dt * _in[i + 1];
        }

        /// <summary>The curve's slope at x (0 beyond its ends, where the engine holds the end values).</summary>
        public float Slope(float x)
        {
            int n = Count;
            if (n < 2 || x < _time[0] || x > _time[n - 1])
            {
                return 0f;
            }
            int i = Segment(x);
            float dt = _time[i + 1] - _time[i];
            float s = (x - _time[i]) / dt;
            float s2 = s * s;
            return ((6f * s2 - 6f * s) * _value[i] + (3f * s2 - 4f * s + 1f) * dt * _out[i] + (-6f * s2 + 6f * s) * _value[i + 1] + (3f * s2 - 2f * s) * dt * _in[i + 1]) / dt;
        }

        /// <summary>The most the curve reaches between 0 and its last key, sampled at the keys and 16 steps between each.</summary>
        public float Most()
        {
            float most = Evaluate(0f);
            for (int i = 0; i + 1 < Count; i++)
            {
                for (int k = 0; k <= 16; k++)
                {
                    float v = Evaluate(_time[i] + (_time[i + 1] - _time[i]) * k / 16f);
                    most = v > most ? v : most;
                }
            }
            return most;
        }

        // The key before x, between the first and last keys.
        private int Segment(float x)
        {
            int low = 0;
            int high = Count - 1;
            while (high - low > 1)
            {
                int middle = (low + high) >> 1;
                if (_time[middle] <= x)
                {
                    low = middle;
                }
                else
                {
                    high = middle;
                }
            }
            return low;
        }
    }
}
