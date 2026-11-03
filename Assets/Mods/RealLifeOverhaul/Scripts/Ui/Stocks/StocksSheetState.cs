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
