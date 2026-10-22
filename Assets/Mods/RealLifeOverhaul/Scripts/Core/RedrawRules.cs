using System;
using System.Collections.Generic;

namespace RealLifeOverhaul
{
    /// <summary>
    /// How a business's list of the day's customers stood after a draw: the list, its length and its last customer. A
    /// redraw replaces the unserved customers and appends the new ones, and serving changes none of the three, so while
    /// all three are the same nobody has drawn it again.
    /// </summary>
    public struct DrawPrint
    {
        public object List;
        public int Count;
        public object Last;

        public static DrawPrint Of(object list, int count, object last)
        {
            return new DrawPrint { List = list, Count = count, Last = last };
        }

        public bool Same(DrawPrint other)
        {
            return List != null && ReferenceEquals(List, other.List) && Count == other.Count && ReferenceEquals(Last, other.Last);
        }
    }

    /// <summary>
    /// The redraws of today's remaining customers at the player's businesses (<see cref="CustomerDraw"/>). Several
    /// features change a business's footfall figure in the frame a day starts; a business waits once with the figure of
    /// the last asking, and one whose list stands drawn with that figure is not drawn again. A wish "always" (the list is
    /// out of date whatever the figure) is never left out. Waiting businesses come out in the order first asked.
    /// Game-free; a dictionary look-up an asking, nothing allocated once the pool has grown.
    /// </summary>
    public sealed class RedrawQueue<T> where T : class
    {
        private sealed class Wish
        {
            public T Key;
            public int Figure;     // the figure at the last asking: the one it is drawn with
            public bool Always;    // asked for whatever the figure
            public int Hour;       // the game hour (day * 24 + hour) of the first asking: it is drawn within it
            public object List;    // the list it was asked against (the game makes a new one for its day's draw)
            public bool Live;      // false once drawn or dropped
        }

        private struct Drawn
        {
            public int Figure;
            public DrawPrint Print;
        }

        private readonly List<Wish> _order = new List<Wish>(64);          // first asked first, dropped ones passed over
        private readonly Dictionary<T, Wish> _waiting = new Dictionary<T, Wish>(64, ReferenceComparer<T>.Instance);
        private readonly Dictionary<T, Drawn> _drawn = new Dictionary<T, Drawn>(64, ReferenceComparer<T>.Instance);
        private readonly List<Wish> _pool = new List<Wish>(64);
        private int _head;   // the first place in _order that may still be live

        public int Waiting => _waiting.Count;

        /// <summary>Asks for a redraw with the figure now: true if it waits; false if the list stands drawn with it.</summary>
        public bool Ask(T key, int figure, bool always, int hour, DrawPrint now)
        {
            if (key == null)
            {
                return false;
            }
            bool drawnSo = !always && Stands(key, figure, now);
            if (_waiting.TryGetValue(key, out Wish wish))
            {
                if (drawnSo && !wish.Always)
                {
                    Drop(wish);
                    return false;
                }
                wish.Figure = figure;
                wish.Always |= always;
                wish.List = now.List;
                return true;
            }
            if (drawnSo)
            {
                return false;
            }
            wish = _pool.Count > 0 ? Pop() : new Wish();
            wish.Key = key;
            wish.Figure = figure;
            wish.Always = always;
            wish.Hour = hour;
            wish.List = now.List;
            wish.Live = true;
            _order.Add(wish);
            _waiting[key] = wish;
            return true;
        }

        /// <summary>Takes out the first-asked waiting business to be drawn; false if none waits.</summary>
        public bool Next(out T key, out int figure, out int hour)
        {
            while (_head < _order.Count)
            {
                Wish wish = _order[_head++];
                if (wish.Live)
                {
                    key = wish.Key;
                    figure = wish.Figure;
                    hour = wish.Hour;
                    Drop(wish);
                    return true;
                }
            }
            Compact();
            key = null;
            figure = hour = 0;
            return false;
        }

        /// <summary>Takes one business out to be drawn now, if it waits (its list is about to be read).</summary>
        public bool Take(T key, out int figure, out int hour)
        {
            if (key != null && _waiting.TryGetValue(key, out Wish wish))
            {
                figure = wish.Figure;
                hour = wish.Hour;
                Drop(wish);
                return true;
            }
            figure = hour = 0;
            return false;
        }

        public bool Waits(T key)
        {
            return key != null && _waiting.ContainsKey(key);
        }

        /// <summary>The game made the business a new list: a wish asked against the old one is dropped; true if one was.</summary>
        public bool Replaced(T key, object listNow)
        {
            if (key != null && _waiting.TryGetValue(key, out Wish wish) && !ReferenceEquals(wish.List, listNow))
            {
                Drop(wish);
                return true;
            }
            return false;
        }

        /// <summary>The game hour the longest waiting business was first asked for in; false if none waits.</summary>
        public bool Oldest(out int hour)
        {
            for (int i = _head; i < _order.Count; i++)
            {
                if (_order[i].Live)
                {
                    hour = _order[i].Hour;
                    return true;
                }
            }
            hour = 0;
            return false;
        }

        /// <summary>A business's list was drawn with a figure, by the mod or the game's day, and stands so now.</summary>
        public void Drew(T key, int figure, DrawPrint print)
        {
            if (key != null)
            {
                _drawn[key] = new Drawn { Figure = figure, Print = print };
            }
        }

        /// <summary>The mod added to a list itself (an office's extra clients): if it stood as drawn before, it still does.</summary>
        public void Added(T key, DrawPrint before, DrawPrint now)
        {
            if (key != null && _drawn.TryGetValue(key, out Drawn d) && d.Print.Same(before))
            {
                _drawn[key] = new Drawn { Figure = d.Figure, Print = now };
            }
        }

        /// <summary>True if the list was last drawn with this figure and nobody drew it since.</summary>
        public bool Stands(T key, int figure, DrawPrint now)
        {
            return key != null && _drawn.TryGetValue(key, out Drawn d) && d.Figure == figure && d.Print.Same(now);
        }

        /// <summary>Forgets what is known of a business's list (another session, another save).</summary>
        public void Forget(T key)
        {
            if (key != null)
            {
                _drawn.Remove(key);
            }
        }

        /// <summary>A city is going: nothing waits and nothing is known.</summary>
        public void Clear()
        {
            for (int i = _head; i < _order.Count; i++)
            {
                if (_order[i].Live)
                {
                    _order[i].Live = false;
                    _order[i].Key = null;
                    _order[i].List = null;
                }
            }
            _waiting.Clear();
            _drawn.Clear();
            Compact();
        }

        private void Drop(Wish wish)
        {
            wish.Live = false;
            _waiting.Remove(wish.Key);
            wish.Key = null;
            wish.List = null;
            if (_waiting.Count == 0)
            {
                Compact();
            }
        }

        // Once nothing waits every wish goes back to the pool, so the order never grows past the most that waited at once.
        private void Compact()
        {
            if (_waiting.Count > 0)
            {
                return;
            }
            for (int i = 0; i < _order.Count; i++)
            {
                _order[i].Key = null;
                _order[i].List = null;
                _pool.Add(_order[i]);
            }
            _order.Clear();
            _head = 0;
        }

        private Wish Pop()
        {
            Wish wish = _pool[_pool.Count - 1];
            _pool.RemoveAt(_pool.Count - 1);
            return wish;
        }
    }

    /// <summary>
    /// When the redraws are made: spread over the frames that follow, or at once. A redraw asked in a game hour must be
    /// made before BusinessSimulatorHelper.RunHourly reads the list at its end, so they are spread only while the clock
    /// takes small steps: alone, at normal speed, not in a time skip, and with at least <see cref="SettleMinutes"/> left
    /// in the hour, many times the largest step a frame can make (Time.maximumDeltaTime). Each frame takes at most
    /// <see cref="BudgetMs"/>, and always at least one. Game-free.
    /// </summary>
    public static class RedrawRules
    {
        public const double BudgetMs = 1.0;        // milliseconds of redraws a frame while they are spread (the game's own spread work takes two: DistributedWorkBudget)
        public const float SettleMinutes = 10f;    // game minutes before the hour turns by which every redraw asked in it has been made
        public const int StepsKept = 3;            // the margin holds the clock's largest step of a frame this many times over, or nothing is spread
        public const float MinutesInHour = 60f;    // the game's clock: minutes an hour

        /// <summary>The most game minutes one frame can move the clock: largest step times time scale (at least 1) times minutes a second.</summary>
        public static float StepMost(float maximumDeltaTime, float timeScale, float minutesASecond)
        {
            return Math.Max(0f, maximumDeltaTime) * Math.Max(1f, timeScale) * Math.Max(0f, minutesASecond);
        }

        /// <summary>The game minutes a second the clock ran at between two looks, over the larger step; 0 if it did not move.</summary>
        public static float Seen(double clockBefore, double clockNow, float stepBefore, float stepNow)
        {
            double moved = clockNow - clockBefore;
            float step = Math.Max(stepBefore, stepNow);
            return moved > 0.0 && moved < MinutesInHour && step > 0f ? (float)(moved / step) : 0f;
        }

        /// <summary>True if the redraws may wait for the frames to come; otherwise everything waiting is drawn at once.</summary>
        public static bool Spread(bool alone, bool skipping, float timeScale, float stepMost, float minute)
        {
            return alone && !skipping && timeScale <= 1.001f && stepMost * StepsKept < SettleMinutes && minute >= 0f && minute < MinutesInHour - SettleMinutes;
        }

        /// <summary>True if another redraw fits into this frame: always the first, then while under the budget.</summary>
        public static bool More(int done, double ms)
        {
            return done == 0 || ms < BudgetMs;
        }

        /// <summary>True if a redraw asked in one game hour (day * 24 + hour) is being made in a later one.</summary>
        public static bool Late(int askedHour, int hourNow)
        {
            return hourNow != askedHour;
        }
    }
}
