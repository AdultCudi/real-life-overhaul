using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Flare
{
    /// <summary>
    /// Which of Flare's items (a post by its number, an answer by its key) had their words asked: once, then left while the
    /// request waits or the words are kept; a request handed back without words frees it to be asked again on Flare's next
    /// event, up to <see cref="Most"/> times. Game-free. Cost: a set and a count an item asked.
    /// </summary>
    public sealed class FlareTextsAsked<T>
    {
        public const int Most = 3;   // asks of one item at most; a third failure keeps the written line

        private readonly HashSet<T> _waiting = new HashSet<T>();
        private readonly Dictionary<T, int> _asks = new Dictionary<T, int>();
        private readonly List<T> _gone = new List<T>();

        /// <summary>True if the item may be asked now: not waiting, not worded, and asked fewer than <see cref="Most"/> times.</summary>
        public bool May(T item)
        {
            return !_waiting.Contains(item) && (!_asks.TryGetValue(item, out int n) || n < Most);
        }

        /// <summary>The item was asked (the hub took the request).</summary>
        public void Asked(T item)
        {
            _waiting.Add(item);
            _asks[item] = _asks.TryGetValue(item, out int n) ? n + 1 : 1;
        }

        /// <summary>Its request came back: with words it stays asked (kept, drawn from the store); without, it may be asked again.</summary>
        public void Ended(T item, bool words)
        {
            if (!words)
            {
                _waiting.Remove(item);
            }
        }

        /// <summary>A new account or a stop: nothing asked.</summary>
        public void Clear()
        {
            _waiting.Clear();
            _asks.Clear();
        }

        /// <summary>Lets go of the items that are gone (a post pruned from the feed). How many went.</summary>
        public int Prune(Predicate<T> keep)
        {
            _gone.Clear();
            foreach (T item in _asks.Keys)
            {
                if (!keep(item))
                {
                    _gone.Add(item);
                }
            }
            for (int i = 0; i < _gone.Count; i++)
            {
                _asks.Remove(_gone[i]);
                _waiting.Remove(_gone[i]);
            }
            int gone = _gone.Count;
            _gone.Clear();
            return gone;
        }
    }
}
