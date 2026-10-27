using System.Collections.Generic;
using System.Threading;
using BAModAPI;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The mod's Info lines of the frame the game turns the day in, held and written in the frames after it. With Developer
    /// diagnostics on, every day handler writes its "check" lines in that frame (a player's log: 108 to 126 of the mod's lines
    /// between the first and the last day handler), and each line is a call into the game's log with its record kept in
    /// memory. The lines are diagnostics: written a few a frame, in their order, a frame or three late, with nothing else
    /// changed. A warning or an error, a save and the end of the game write what is held first, so the log is in order and
    /// complete. Info lines from another thread are written at once. The day is seen turning when the save's day moves on
    /// within the same save (the game's NewDay moves it before it calls any handler). COST: with diagnostics off nothing (the
    /// logger drops the line before it gets here); on, a comparison of the save and its day a line, and a queue entry in the
    /// frame the day turns.
    /// </summary>
    public static class DayLines
    {
        /// <summary>Milliseconds of lines the player writes in a frame.</summary>
        public const double SliceMs = 0.5;

        private static readonly Queue<string> Held = new Queue<string>(256);
        private static IModLogger _inner;
        private static int _main = -1;          // the game's main thread
        private static GameInstance _save;      // the save the day below belongs to
        private static int _day = int.MinValue;
        private static int _holdFrame = -1;     // the frame the day turned in
        private static int _writing;            // lines being written (a line written must not be held again)

        /// <summary>Called when a line is held on an empty queue: whatever writes them starts (Core/DayPump).</summary>
        public static System.Action Wake;

        private static int _heldNow;

        /// <summary>Lines held since the day last turned, for the session record.</summary>
        public static int HeldToday => _heldNow;

        /// <summary>Lines waiting.</summary>
        public static int Waiting => Held.Count;

        /// <summary>Names the game's main thread: the one that may hold lines. Called from a component's Awake.</summary>
        public static void Begin()
        {
            _main = Thread.CurrentThread.ManagedThreadId;
        }

        /// <summary>
        /// True if the line was taken to be written later (the caller writes nothing); false to write it now. Lines are
        /// taken in the frame the day turns in, and in the frames after it while any earlier one is still held.
        /// </summary>
        public static bool Hold(IModLogger inner, string message)
        {
            if (_writing > 0 || _main < 0 || Wake == null || Thread.CurrentThread.ManagedThreadId != _main)
            {
                return false;
            }
            int frame = Time.frameCount;
            Notice(frame);
            if (frame != _holdFrame && Held.Count == 0)
            {
                return false;
            }
            _inner = inner;
            bool wake = Held.Count == 0;
            Held.Enqueue(message);
            _heldNow++;
            if (wake)
            {
                Wake();
            }
            return true;
        }

        // The day has turned if the save is the one last seen and its day moved on.
        private static void Notice(int frame)
        {
            GameInstance save = SaveGameManager.Current;
            if (!ReferenceEquals(save, _save))
            {
                _save = save;
                _day = save != null ? save.Day : int.MinValue;
                return;
            }
            if (save != null && save.Day != _day)
            {
                _day = save.Day;
                _holdFrame = frame;
                _heldNow = 0;
            }
        }

        /// <summary>Writes the lines that fit in a slice, at least one, in order; true while any wait.</summary>
        public static bool Write(ref WorkSlice slice)
        {
            _writing++;
            try
            {
                while (Held.Count > 0 && slice.More())
                {
                    _inner?.Info(Held.Dequeue());
                }
            }
            finally
            {
                _writing--;
            }
            return Held.Count > 0;
        }

        /// <summary>Writes every line held, now: before a warning or an error, a save, the end of the game.</summary>
        public static void Flush()
        {
            if (Held.Count == 0 || Thread.CurrentThread.ManagedThreadId != _main)
            {
                return;   // (another thread leaves the lines to the main one)
            }
            WorkSlice whole = WorkSlice.Whole;
            Write(ref whole);
        }

        /// <summary>Forgets the lines held and the save seen (the city is going).</summary>
        public static void Reset()
        {
            Flush();
            _save = null;
            _day = int.MinValue;
            _holdFrame = -1;
        }
    }
}
