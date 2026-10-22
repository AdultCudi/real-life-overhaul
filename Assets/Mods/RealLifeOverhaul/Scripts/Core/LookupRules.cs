using System;
using System.Collections.Generic;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The place of the first item of a list with a key, as a walk along it would find it, kept in a dictionary and made
    /// again when the list, its length, its last item or the caller's stamp changes. Only for keys an item never changes
    /// (an address); an item with a null key is not listed. Game-free, checked by EconomyCheck.
    /// COST: one walk when the list changes, then a dictionary look-up an asking; nothing allocated once grown.
    /// </summary>
    public sealed class ListIndex<TItem, TKey> where TItem : class where TKey : class
    {
        private readonly Dictionary<TKey, int> _first;
        private readonly Func<TItem, TKey> _keyOf;   // the key an item is listed under; null for one that is not listed
        private object _list;
        private int _count = -1;
        private TItem _last;
        private int _stamp;

        /// <summary>How many times the index was made, for the checks.</summary>
        public int Built { get; private set; }

        public ListIndex(Func<TItem, TKey> keyOf, IEqualityComparer<TKey> comparer = null)
        {
            _keyOf = keyOf;
            _first = new Dictionary<TKey, int>(64, comparer ?? EqualityComparer<TKey>.Default);
        }

        /// <summary>Where in the list the first item with this key is; -1 if none.</summary>
        public int At(List<TItem> list, TKey key, int stamp)
        {
            if (list == null || key == null)
            {
                return -1;
            }
            int count = list.Count;
            TItem last = count > 0 ? list[count - 1] : null;
            if (!ReferenceEquals(list, _list) || count != _count || !ReferenceEquals(last, _last) || stamp != _stamp)
            {
                Build(list, stamp);
            }
            return _first.TryGetValue(key, out int at) ? at : -1;
        }

        /// <summary>The first item of the list with this key; null if none.</summary>
        public TItem First(List<TItem> list, TKey key, int stamp)
        {
            int at = At(list, key, stamp);
            return at >= 0 ? list[at] : null;
        }

        /// <summary>Made again at the next asking, for items changed in place.</summary>
        public void Reset()
        {
            _list = null;
            _last = null;
            _count = -1;
            _first.Clear();
        }

        private void Build(List<TItem> list, int stamp)
        {
            _first.Clear();
            for (int i = 0; i < list.Count; i++)
            {
                TItem item = list[i];
                TKey key = item != null ? _keyOf(item) : null;
                if (key != null && !_first.ContainsKey(key))
                {
                    _first.Add(key, i);
                }
            }
            _list = list;
            _count = list.Count;
            _last = _count > 0 ? list[_count - 1] : null;
            _stamp = stamp;
            Built++;
        }
    }

    /// <summary>
    /// Sums by key, keys in the order their first figure came, each sum added in the order its figures came: the same
    /// to the last bit as a walk along a list of keys. For the day's figures of the city's economy by address and
    /// category, and the brands' staff by business. Game-free, checked by EconomyCheck.
    /// </summary>
    public sealed class KeyedSums<TKey>
    {
        private readonly Dictionary<TKey, int> _at;

        public readonly List<TKey> Keys = new List<TKey>();
        public readonly List<float> Sums = new List<float>();
        public readonly List<int> Counts = new List<int>();

        public KeyedSums(IEqualityComparer<TKey> comparer = null)
        {
            _at = new Dictionary<TKey, int>(64, comparer ?? EqualityComparer<TKey>.Default);
        }

        public int Count => Keys.Count;

        /// <summary>Adds a figure to its key's sum, a new key at the end; returns the key's place.</summary>
        public int Add(TKey key, float figure)
        {
            if (!_at.TryGetValue(key, out int at))
            {
                at = Keys.Count;
                _at.Add(key, at);
                Keys.Add(key);
                Sums.Add(0f);
                Counts.Add(0);
            }
            Sums[at] += figure;
            Counts[at]++;
            return at;
        }

        /// <summary>The place of a key's sum; -1 if no figure came for it.</summary>
        public int At(TKey key)
        {
            return _at.TryGetValue(key, out int at) ? at : -1;
        }

        public void Clear()
        {
            _at.Clear();
            Keys.Clear();
            Sums.Clear();
            Counts.Clear();
        }
    }

    /// <summary>
    /// One day's items of a queue the game adds to at its end and drops from at its head (its transactions, kept a
    /// week; 25,000 to 35,000 of them), in its order: walked in full once a day, then only past the last item seen, so
    /// every sum over them is the same to the last bit as a full walk. The game drops days older than its day less six
    /// (GameManager.ReduceTransactionQueue): such a day is walked in full each time. Two days are kept: the morning's
    /// and the one before. Game-free, checked by EconomyCheck. COST: nothing allocated once grown to a day's items.
    /// </summary>
    public sealed class DayCut<T> where T : class
    {
        private sealed class Slice
        {
            public int Day = int.MinValue;
            public readonly List<T> Items = new List<T>(256);
            public Queue<T> Queue;
            public int Count;      // the queue's length at the last asking
            public T Head, Last;   // and its first and last items
            public int Asked;      // when last asked for: the older of the two is made again
        }

        private readonly Func<T, int> _dayOf;
        private readonly Slice[] _slices = { new Slice(), new Slice() };
        private int _asked;

        /// <summary>Items walked over in all, for the checks.</summary>
        public long Walked { get; private set; }

        /// <summary>Items whose day was looked at, for the checks.</summary>
        public long Opened { get; private set; }

        public DayCut(Func<T, int> dayOf)
        {
            _dayOf = dayOf;
        }

        /// <summary>
        /// The queue's items of a day, in its order: a list kept here, to read but never keep or change.
        /// <paramref name="dropsBefore"/>: the oldest day the queue may still drop items of.
        /// </summary>
        public List<T> Of(Queue<T> queue, int day, int dropsBefore)
        {
            Slice s = _slices[0].Day == day && ReferenceEquals(_slices[0].Queue, queue) ? _slices[0]
                : _slices[1].Day == day && ReferenceEquals(_slices[1].Queue, queue) ? _slices[1] : null;
            if (queue == null)
            {
                Forget();
                return _slices[0].Items;
            }
            if (s == null)
            {
                s = _slices[0].Asked <= _slices[1].Asked ? _slices[0] : _slices[1];
                Build(s, queue, day);
            }
            else if (queue.Count != s.Count || (queue.Count > 0 && !ReferenceEquals(queue.Peek(), s.Head)))
            {
                if (day < dropsBefore || !Follow(s, queue))
                {
                    Build(s, queue, day);
                }
            }
            s.Asked = ++_asked;
            return s.Items;
        }

        /// <summary>Forgets everything: another save, or the city unloaded.</summary>
        public void Forget()
        {
            for (int i = 0; i < _slices.Length; i++)
            {
                _slices[i].Day = int.MinValue;
                _slices[i].Items.Clear();
                _slices[i].Queue = null;
                _slices[i].Head = _slices[i].Last = null;
                _slices[i].Count = 0;
            }
        }

        private void Build(Slice s, Queue<T> queue, int day)
        {
            s.Items.Clear();
            T last = null;
            foreach (T item in queue)
            {
                Walked++;
                Opened++;
                if (item != null && _dayOf(item) == day)
                {
                    s.Items.Add(item);
                }
                last = item;
            }
            s.Day = day;
            s.Queue = queue;
            s.Count = queue.Count;
            s.Head = queue.Count > 0 ? queue.Peek() : null;
            s.Last = last;
        }

        // Adds the items after the last one seen; false if that one is gone (the queue emptied or replaced in place).
        private bool Follow(Slice s, Queue<T> queue)
        {
            bool past = s.Count == 0 || s.Last == null;
            T last = null;
            foreach (T item in queue)
            {
                Walked++;
                last = item;
                if (!past)
                {
                    past = ReferenceEquals(item, s.Last);
                    continue;
                }
                Opened++;
                if (item != null && _dayOf(item) == s.Day)
                {
                    s.Items.Add(item);
                }
            }
            if (!past)
            {
                return false;
            }
            s.Count = queue.Count;
            s.Head = queue.Count > 0 ? queue.Peek() : null;
            s.Last = last;
            return true;
        }
    }

    /// <summary>
    /// Runs of profit by business over the last days of the game's statements, newest day first, in one pass: a loss or
    /// nothing on a day ends a business's run; its first statement of a day counts. Game-free, checked by EconomyCheck.
    /// </summary>
    public sealed class ProfitRuns<TKey>
    {
        private sealed class Run
        {
            public bool Lost;
            public float Total;
            public int Days, Group;
        }

        private readonly Dictionary<TKey, Run> _runs;
        private readonly List<Run> _pool = new List<Run>(64);
        private readonly List<Run> _used = new List<Run>(64);
        private int _group;

        public ProfitRuns(IEqualityComparer<TKey> comparer = null)
        {
            _runs = new Dictionary<TKey, Run>(64, comparer ?? EqualityComparer<TKey>.Default);
        }

        /// <summary>Starts afresh.</summary>
        public void Begin()
        {
            _runs.Clear();
            for (int i = 0; i < _used.Count; i++)
            {
                _pool.Add(_used[i]);
            }
            _used.Clear();
            _group = 0;
        }

        /// <summary>A new day's statements follow (newest first).</summary>
        public void Day()
        {
            _group++;
        }

        /// <summary>A business's profit on a statement of the day; only its first of the day counts.</summary>
        public void Add(TKey key, float profit)
        {
            if (key == null)
            {
                return;
            }
            if (!_runs.TryGetValue(key, out Run run))
            {
                run = _pool.Count > 0 ? _pool[_pool.Count - 1] : new Run();
                if (_pool.Count > 0)
                {
                    _pool.RemoveAt(_pool.Count - 1);
                }
                run.Lost = false;
                run.Total = 0f;
                run.Days = 0;
                run.Group = 0;
                _runs.Add(key, run);
                _used.Add(run);
            }
            if (run.Group == _group)
            {
                return;
            }
            run.Group = _group;
            if (run.Lost)
            {
                return;
            }
            if (profit <= 0f)
            {
                run.Lost = true;
                return;
            }
            run.Total += profit;
            run.Days++;
        }

        /// <summary>True if the business made a profit on each of its days, at least <paramref name="days"/> of them; its total then.</summary>
        public bool Ran(TKey key, int days, out float total)
        {
            total = 0f;
            if (key == null || !_runs.TryGetValue(key, out Run run) || run.Lost)
            {
                return false;
            }
            total = run.Total;
            return run.Days >= days;
        }
    }

    /// <summary>Equality by reference, for keys whose own Equals is not wanted or not cheap. Game-free.</summary>
    public sealed class ReferenceComparer<T> : IEqualityComparer<T> where T : class
    {
        public static readonly ReferenceComparer<T> Instance = new ReferenceComparer<T>();

        public bool Equals(T a, T b)
        {
            return ReferenceEquals(a, b);
        }

        public int GetHashCode(T value)
        {
            return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value);
        }
    }
}
