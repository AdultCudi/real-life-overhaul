using System;
using System.Collections.Generic;
using System.Text;

namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// Candles written small for the save: each as variable-length changes from the one before, in whole cents so a run is
    /// exact, in base 64's safe letters; a volume kept to about 6% in a byte. Game-free. A candle is the close and open less
    /// the close before, high over and low under the body, the volume's code, then the day as a change.
    /// </summary>
    public static class MarketCodec
    {
        private const string Letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";

        private static readonly byte[] Buffer = new byte[64 * 1024];
        private static int _length;
        private static readonly int[] Index = MakeIndex();

        private static int[] MakeIndex()
        {
            var index = new int[128];
            for (int i = 0; i < index.Length; i++)
            {
                index[i] = -1;
            }
            for (int i = 0; i < Letters.Length; i++)
            {
                index[Letters[i]] = i;
            }
            return index;
        }

        /// <summary>Candles, oldest first, as text with none of the save's separators in it ('|', ';', '~', ',').</summary>
        public static void Write(StringBuilder into, List<Candle> candles)
        {
            Write(into, candles, null, candles.Count);
        }

        public static void Write(StringBuilder into, Candle[] bars, int count)
        {
            Write(into, null, bars, count);
        }

        private static void Write(StringBuilder into, List<Candle> list, Candle[] array, int count)
        {
            _length = 0;
            Var((uint)count);
            int prevClose = 0, prevDay = 0;
            for (int i = 0; i < count; i++)
            {
                Candle c = list != null ? list[i] : array[i];
                Signed(c.Day - prevDay);
                Signed(c.C - prevClose);
                Signed(c.O - prevClose);
                Var((uint)Math.Max(0, c.H - Math.Max(c.O, c.C)));
                Var((uint)Math.Max(0, Math.Min(c.O, c.C) - c.L));
                Byte(VolumeCode(c.V));
                prevClose = c.C;
                prevDay = c.Day;
                if (_length > Buffer.Length - 64)
                {
                    break;   // never with the counts kept: a hundred candles take under a kilobyte
                }
            }
            Encode(into);
        }

        /// <summary>Whole numbers, oldest first, as the changes from one to the next.</summary>
        public static void WriteInts(StringBuilder into, List<int> values)
        {
            _length = 0;
            Var((uint)values.Count);
            int prev = 0;
            for (int i = 0; i < values.Count && _length < Buffer.Length - 16; i++)
            {
                Signed(values[i] - prev);
                prev = values[i];
            }
            Encode(into);
        }

        /// <summary>Whole numbers read back, at most <paramref name="most"/>; false if the text is damaged.</summary>
        public static bool ReadInts(string text, List<int> into, int most)
        {
            into.Clear();
            if (!Decode(text))
            {
                return false;
            }
            int at = 0;
            if (!ReadVar(ref at, out uint count))
            {
                return false;
            }
            int prev = 0;
            for (uint i = 0; i < count; i++)
            {
                if (!ReadSigned(ref at, out int change))
                {
                    into.Clear();
                    return false;
                }
                prev += change;
                if (into.Count < most)
                {
                    into.Add(prev);
                }
            }
            return true;
        }

        /// <summary>A volume as a byte, about 6% apart, up to a hundred billion shares.</summary>
        public static byte VolumeCode(long v)
        {
            if (v <= 0)
            {
                return 0;
            }
            double code = Math.Log(v) * 10.0;
            return (byte)Math.Max(1, Math.Min(255, (int)Math.Round(code)));
        }

        public static long VolumeOf(byte code)
        {
            return code == 0 ? 0 : (long)Math.Round(Math.Exp(code / 10.0));
        }

        private static void Signed(int value)
        {
            Var((uint)((value << 1) ^ (value >> 31)));
        }

        private static void Var(uint value)
        {
            while (value >= 0x80)
            {
                Byte((byte)(value | 0x80));
                value >>= 7;
            }
            Byte((byte)value);
        }

        private static void Byte(byte b)
        {
            if (_length < Buffer.Length)
            {
                Buffer[_length++] = b;
            }
        }

        private static void Encode(StringBuilder into)
        {
            int bits = 0, value = 0;
            for (int i = 0; i < _length; i++)
            {
                value = (value << 8) | Buffer[i];
                bits += 8;
                while (bits >= 6)
                {
                    bits -= 6;
                    into.Append(Letters[(value >> bits) & 63]);
                }
            }
            if (bits > 0)
            {
                into.Append(Letters[(value << (6 - bits)) & 63]);
            }
        }

        /// <summary>Candles read back, at most <paramref name="most"/>; false and empty if the text is damaged.</summary>
        public static bool Read(string text, List<Candle> into, int most)
        {
            into.Clear();
            if (!Decode(text))
            {
                return false;
            }
            int at = 0;
            if (!ReadVar(ref at, out uint count))
            {
                return false;
            }
            int prevClose = 0, prevDay = 0;
            for (uint i = 0; i < count; i++)
            {
                if (!ReadSigned(ref at, out int dDay) || !ReadSigned(ref at, out int dClose) || !ReadSigned(ref at, out int dOpen) || !ReadVar(ref at, out uint up)
                    || !ReadVar(ref at, out uint down) || at >= _length)
                {
                    into.Clear();
                    return false;
                }
                byte v = Buffer[at++];
                int close = prevClose + dClose, open = prevClose + dOpen;
                int day = prevDay + dDay;
                if (close <= 0 || open <= 0 || day < -1000000)   // a market's history goes back before the game's first day
                {
                    into.Clear();
                    return false;
                }
                int high = Math.Max(open, close) + (int)Math.Min(up, int.MaxValue / 4);
                int low = Math.Max(1, Math.Min(open, close) - (int)Math.Min(down, int.MaxValue / 4));
                if (into.Count < most)
                {
                    into.Add(new Candle(day, open, high, low, close, VolumeOf(v)));
                }
                prevClose = close;
                prevDay = day;
            }
            return true;
        }

        /// <summary>The same into an array; the count read, 0 if the text is damaged.</summary>
        public static int Read(string text, Candle[] into)
        {
            var list = new List<Candle>(into.Length);
            if (!Read(text, list, into.Length))
            {
                return 0;
            }
            for (int i = 0; i < list.Count; i++)
            {
                into[i] = list[i];
            }
            return list.Count;
        }

        private static bool Decode(string text)
        {
            _length = 0;
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }
            int bits = 0, value = 0;
            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                int six = ch < 128 ? Index[ch] : -1;
                if (six < 0)
                {
                    return false;
                }
                value = (value << 6) | six;
                bits += 6;
                if (bits >= 8)
                {
                    bits -= 8;
                    if (_length >= Buffer.Length)
                    {
                        return false;
                    }
                    Buffer[_length++] = (byte)((value >> bits) & 0xFF);
                }
            }
            return true;
        }

        private static bool ReadVar(ref int at, out uint value)
        {
            value = 0;
            int shift = 0;
            while (at < _length && shift < 35)
            {
                byte b = Buffer[at++];
                value |= (uint)(b & 0x7F) << shift;
                if ((b & 0x80) == 0)
                {
                    return true;
                }
                shift += 7;
            }
            return false;
        }

        private static bool ReadSigned(ref int at, out int value)
        {
            if (!ReadVar(ref at, out uint raw))
            {
                value = 0;
                return false;
            }
            value = (int)(raw >> 1) ^ -(int)(raw & 1);
            return true;
        }
    }
}
