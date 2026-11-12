using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The sheet's state: which sheet is up and the figures being written, by field id. Game-free so the checks step it as
    /// the player does. A stepper keeps a number, a choice its place, a switch 0 or 1.
    /// </summary>
    public sealed class StocksSheetState
    {
        public const int Slots = 40;
        public byte Kind;       // the screen's own numbering; 0 none
        public int Key;         // the row it is about
        public int Armed = -1;  // an extra waiting for its second press
        public readonly long[] V = new long[Slots];

        public bool Open => Kind != 0;

        public void Close()
        {
            Kind = 0;
            Key = 0;
            Armed = -1;
            Array.Clear(V, 0, V.Length);
        }

        /// <summary>A stepper moved by its step, held between its ends.</summary>
        public static long Stepped(long value, in StocksFieldModel f, int dir)
        {
            long step = f.Step < 1 ? 1 : f.Step;
            long next = value + dir * step;
            if (f.Max >= f.Min)
            {
                next = Math.Max(f.Min, Math.Min(f.Max, next));
            }
            return next;
        }

        /// <summary>A choice moved on, round.</summary>
        public static long Cycled(long value, in StocksFieldModel f, int dir)
        {
            int n = f.Count < 1 ? 1 : f.Count;
            return ((value + dir) % n + n) % n;
        }

        /// <summary>
        /// A price typed in dollars ("56.4", "56,40") as cents, kept between its ends; an empty or unreadable text keeps <paramref name="keep"/>.
        /// A comma is a decimal point. Never more than two decimals are read.
        /// </summary>
        public static long TypedPrice(string text, long least, long most, long keep)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return keep;
            }
            long whole = 0, cents = 0;
            int decimals = -1;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if ((c == '.' || c == ',') && decimals < 0)
                {
                    decimals = 0;
                }
                else if (c >= '0' && c <= '9')
                {
                    if (decimals < 0)
                    {
                        whole = whole < 10000000000L ? whole * 10L + (c - '0') : whole;
                    }
                    else if (decimals < 2)
                    {
                        cents = cents * 10L + (c - '0');
                        decimals++;
                    }
                }
            }
            if (decimals == 1)
            {
                cents *= 10L;
            }
            long total = whole * 100L + cents;
            if (most >= least && total > most)
            {
                total = most;
            }
            return total < least ? least : total;
        }

        /// <summary>Digits typed into a stepper, kept to its upper end.</summary>
        public static long Typed(string text, long least, long most)
        {
            long n = 0;
            if (text != null)
            {
                for (int i = 0; i < text.Length; i++)
                {
                    char c = text[i];
                    if (c >= '0' && c <= '9' && n < 100000000000000L)
                    {
                        n = n * 10L + (c - '0');
                    }
                }
            }
            if (most >= least && n > most)
            {
                n = most;
            }
            return n < least ? least : n;
        }
    }
}
