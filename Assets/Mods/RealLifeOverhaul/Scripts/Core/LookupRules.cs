using System;
using System.Collections.Generic;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The place of the first item of a list with a key, as a walk along the list would find it, kept in a dictionary
    /// and made again whenever the list is another one, has another length or another last item, or the caller's stamp
    /// (a day, a version) has moved. For the economy's look-ups by an address, a place or a key, which walked the save's
    /// lists for every business asked about: every answer the same, a look-up each.
    ///
    /// Only for keys an item never changes (an address, a place). An item its key function gives no key for (null) is not
    /// listed. No Unity or game types: checked offline (EconomyCheck, MorningChecks.cs). Cost: one walk of the list when it
    /// changes, then a dictionary look-up and three comparisons an asking; nothing allocated once its dictionary has grown.
    /// </summary>
    public sealed class ListIndex<TItem, TKey> where TItem : class where TKey : class
    {
        private readonly Dictionary<TKey, int> _first;
        private readonly Func<TItem, TKey> _keyOf;   // the key an item is listed under; null for one that is not listed
        private object _list;
        private int _count = -1;
        private TItem _last;
        private int _stamp;

        /// <summary>How many times the index was made: for the checks (made again only when its list changed).</summary>
        public int Built { get; private set; }

        public ListIndex(Func<TItem, TKey> keyOf, IEqualityComparer<TKey> comparer = null)
        {
            _keyOf = keyOf;
            _first = new Dictionary<TKey, int>(64, comparer ?? EqualityComparer<TKey>.Default);
        }

        /// <summary>Where in the list the first item with this key is; -1 if none. A null key is never listed.</summary>
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

        /// <summary>Made again at the next asking, whatever the list looks like (its items changed in place).</summary>
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
    /// Sums by key, each key once, in the order its first figure came in, each sum added up in the order its figures came
    /// (so the same to the last bit as a walk along a list of keys that adds to the first it finds, or a new one at its end).
    /// For the day's rent, wages and takings of the city's economy by address, category and deduction, which
    /// were found by walking every line so far for every one of the day's thousands of transactions, and for the brands'
    /// staff by business. No Unity or game types: checked offline (EconomyCheck, MorningChecks.cs).
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

        /// <summary>A figure added to its key's sum (a new key at the end). Returns the key's place.</summary>
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
    /// One day's items of a queue the game only adds to at its end and drops from at its head (its transactions, kept a
    /// week), in the queue's order, kept up to date by walking only past what it saw at the last asking. Every reader of a
    /// morning (the city's day figures, each business's statement brought up to date, the dealerships' deductions, what
    /// leaves tomorrow) read the whole queue of 25,000 to 35,000 for the one day it wanted, four or five times.
    /// Here the queue is walked in full once a day, and after that only compared item by item (no item is opened)
    /// until the last one seen, with the day of each item added since looked at. The same items in the same order as a
    /// walk of the queue that keeps the day's, so every sum over them is the same to the last bit.
    ///
    /// The game drops only days older than its own day less six from the head (GameManager.ReduceTransactionQueue): a day
    /// as old as that, or older, is walked in full each time it is asked for. Two days are kept (the morning's and
    /// yesterday's: the hour that opens the day asks for the one before). No Unity or game types: checked offline
    /// (EconomyCheck, MorningChecks.cs). Cost: nothing allocated once its lists have grown to a day's items.
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
            public int Asked;      // when this slice was last asked for (for which of two is made again)
        }

        private readonly Func<T, int> _dayOf;
        private readonly Slice[] _slices = { new Slice(), new Slice() };
        private int _asked;

        /// <summary>Items walked over in all, for the checks: a day is walked in full once, then only past what was seen.</summary>
        public long Walked { get; private set; }

        /// <summary>Items whose day was looked at, for the checks.</summary>
        public long Opened { get; private set; }

        public DayCut(Func<T, int> dayOf)
        {
            _dayOf = dayOf;
        }

        /// <summary>
        /// The queue's items of a day, in its order (a list kept by this: read it, never keep or change it).
        /// <paramref name="dropsBefore"/>: the oldest day the queue may still drop items of from its head.
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

        /// <summary>Nothing kept (another save, a city gone).</summary>
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

        // The items added since the last asking, after the last one seen then: false if it is no longer there (the queue
        // was emptied or replaced in place), and the slice is made again.
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
    /// Runs of profit by business over the last days of the game's statements, newest day first (the city's
    /// rivals): a business that made a loss or nothing on one of them has no run; the first statement of a
    /// business in a day counts, as a walk along that day's statements finds it. One pass over the days' statements
    /// instead of a walk of every day's statements for every business. No Unity or game types: checked offline
    /// (EconomyCheck, MorningChecks.cs).
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

        /// <summary>A business's statement of the day: its profit. Only its first of the day counts.</summary>
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

        /// <summary>True if the business made a profit on every one of its days, on at least so many; its total then.</summary>
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

    /// <summary>
    /// Equality by reference, for keys whose own Equals is not wanted or not cheap: a business's registration, a day's
    /// statements, a list. No Unity or game types.
    /// </summary>
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
