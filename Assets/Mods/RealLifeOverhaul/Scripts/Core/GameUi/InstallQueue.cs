using System.Collections.Generic;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// The mod's apps waiting to go on the phone, in the order they asked: each frame's slice takes from the front
    /// (WorkSlice, RLO-PERF-024), so the frame the phone can first take them never holds some 35 installs. One taken out
    /// of turn (opened before its turn, or taken off) leaves the others in their order. Game-free; nothing allocated once
    /// the list has grown, one count a frame while it holds apps and nothing while it is empty.
    /// </summary>
    public sealed class InstallQueue<T> where T : class
    {
        /// <summary>
        /// One frame's slice, in milliseconds: none, so one app a frame. An install copies two of the game's buttons and, the first
        /// time in a session, reads its glyph back from the graphics card, which waits for the card: one is a frame's work.
        /// </summary>
        public const double SliceMs = 0.0;

        private readonly List<T> _waiting = new List<T>(40);

        public int Count => _waiting.Count;

        /// <summary>The app at that place in the line, from 0 at the front.</summary>
        public T this[int index] => _waiting[index];

        /// <summary>Puts an app at the end of the line; false if it is null or already waits.</summary>
        public bool Add(T item)
        {
            if (item == null || _waiting.Contains(item))
            {
                return false;
            }
            _waiting.Add(item);
            return true;
        }

        /// <summary>Takes an app out of the line wherever it stands; false if it was not waiting.</summary>
        public bool Take(T item)
        {
            return item != null && _waiting.Remove(item);
        }

        /// <summary>The front of the line, if this frame's slice has room for one more; false when none waits or the slice is over.</summary>
        public bool Next(ref WorkSlice slice, out T item)
        {
            item = null;
            if (_waiting.Count == 0 || !slice.More())
            {
                return false;
            }
            item = _waiting[0];
            _waiting.RemoveAt(0);
            return true;
        }

        public void Clear()
        {
            _waiting.Clear();
        }
    }
}
