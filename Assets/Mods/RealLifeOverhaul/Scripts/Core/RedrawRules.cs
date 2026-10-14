using System;
using System.Collections.Generic;

namespace RealLifeOverhaul
{
    /// <summary>
    /// How a business's list of the day's customers stood after a draw: the list itself, how many it held and the last of
    /// them. The game's own redraw (anyone's) takes every customer not yet served out of the list and puts the new ones at
    /// its end, so while the list, its length and its last customer are all the same, nobody has drawn it again since.
    /// Serving a customer (the game's simulator, its spawner) changes none of the three.
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
    /// The redraws of the customers still to come today at the player's businesses (Core/CustomerDraw), as rules: who
    /// waits, with which figure, and which list is already drawn with the figure its business has.
    ///
    /// Why: in the frame a game day starts the game has just drawn every business's customers itself, and then the city's
    /// economy, the company, the brands, the interiors and business pressure each wrote their part of a business's footfall
    /// figure and had the day drawn again at once, so one business was drawn three or four times in that frame and hundreds
    /// of businesses in all. Here a business waits once, however often it is asked for, with
    /// the figure of the last asking (as the last of those redraws had it); and a business whose list was last drawn with
    /// the figure it has now, by the game's day or by the mod, nobody having drawn it since, is not drawn at all (a draw is
    /// random: a second one with the same figure changes nothing but which customers come).
    ///
    /// A wish "always" (the rota planner's and the brands' opening hours: the list is out of date whatever the figure) is
    /// never left out. Waiting businesses come out in the order they were first asked for, the same on every run.
    ///
    /// No Unity or game types: the business is any key compared by reference, the list a print of objects. Checked
    /// offline (EconomyCheck, MorningChecks.cs). Cost: a dictionary look-up an asking, nothing allocated once the pool
    /// of wishes has grown to the most that ever waited at once.
    /// </summary>
    public sealed class RedrawQueue<T> where T : class
    {
        private sealed class Wish
        {
            public T Key;
            public int Figure;     // the business's figure at the last asking: the one it is drawn with
            public bool Always;    // asked for whatever the figure (the list is out of date)
            public int Hour;       // the game hour (day * 24 + hour) of the first asking: it is drawn within it
            public object List;    // the list it was asked against (the game makes a new one for its day's draw)
            public bool Live;      // false once drawn or dropped: its place in the order is passed over
        }

        private struct Drawn
        {
            public int Figure;
            public DrawPrint Print;
        }

        private readonly List<Wish> _order = new List<Wish>(64);          // first asked first, with dropped ones passed over
        private readonly Dictionary<T, Wish> _waiting = new Dictionary<T, Wish>(64, ReferenceComparer<T>.Instance);
        private readonly Dictionary<T, Drawn> _drawn = new Dictionary<T, Drawn>(64, ReferenceComparer<T>.Instance);
        private readonly List<Wish> _pool = new List<Wish>(64);
        private int _head;   // the first place in _order that may still be live

        /// <summary>How many businesses wait.</summary>
        public int Waiting => _waiting.Count;

        /// <summary>
        /// A business is asked for with the figure it has now. True if it waits for its draw (asked now, or its wish brought
        /// up to this figure); false if nothing is to be drawn: its list stands drawn with this very figure (a wish of it that
        /// waited without "always" is dropped, as the redraw it stood for would have been undone by this one).
        /// </summary>
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

        /// <summary>The business first asked for of those that wait, taken out to be drawn. False if none waits.</summary>
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

        /// <summary>One business taken out to be drawn now, if it waits (its list is about to be read).</summary>
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

        /// <summary>
        /// The game has made the business a new list and drawn it (its day's draw): a wish asked against the list before
        /// is dropped, as the redraw made at its asking was, wiped by the game's own. True if one was.
        /// </summary>
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

        /// <summary>A business's list was drawn with a figure (by the mod, or by the game's own day) and stands so now.</summary>
        public void Drew(T key, int figure, DrawPrint print)
        {
            if (key != null)
            {
                _drawn[key] = new Drawn { Figure = figure, Print = print };
            }
        }

        /// <summary>
        /// The mod added to a business's list itself (an office's extra clients): if the list stood as last drawn just
        /// before, it stands drawn with the same figure now.
        /// </summary>
        public void Added(T key, DrawPrint before, DrawPrint now)
        {
            if (key != null && _drawn.TryGetValue(key, out Drawn d) && d.Print.Same(before))
            {
                _drawn[key] = new Drawn { Figure = d.Figure, Print = now };
            }
        }

        /// <summary>True if the business's list stands drawn with this figure: last drawn with it, and nobody drew it since.</summary>
        public bool Stands(T key, int figure, DrawPrint now)
        {
            return key != null && _drawn.TryGetValue(key, out Drawn d) && d.Figure == figure && d.Print.Same(now);
        }

        /// <summary>What is known of a business's list is forgotten (another session, another save).</summary>
        public void Forget(T key)
        {
            if (key != null)
            {
                _drawn.Remove(key);
            }
        }

        /// <summary>Nothing waits and nothing is known: a city is going.</summary>
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

        // Every wish in the order goes back to the pool once nothing waits: the order starts afresh, so it never grows past
        // the most that waited at once.
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
    /// When the redraws are made (Core/CustomerDraw): spread over the frames that follow, a little time each, or at once.
    ///
    /// A redraw asked for in a game hour must be made within it, before the game's own simulation of that hour reads the
    /// list at the hour's end (BusinessSimulatorHelper.RunHourly), as the redraws made at once always were. So they are
    /// spread only while the clock is known to take small steps: alone (no Going Public session, whose clock can be moved
    /// on by an hour in a frame), at the game's normal speed (not fast forward), not in the game's time skip (sleep, the
    /// time machine), and while the hour has at least <see cref="SettleMinutes"/> game minutes left, many times the
    /// largest step one frame can make: the engine gives no frame more than its largest step of time
    /// (Time.maximumDeltaTime, a third of a second), and the game moves its clock by that times its minutes a second. Out
    /// of those, whatever waits is made at once. Each frame they run, they take <see cref="BudgetMs"/> of it at most, and
    /// always at least one, so that a morning's few hundred end in a few seconds of play, well inside the first game minutes.
    ///
    /// No Unity or game types: checked offline (EconomyCheck, MorningChecks.cs).
    /// </summary>
    public static class RedrawRules
    {
        public const double BudgetMs = 1.0;        // milliseconds of redraws a frame while they are spread (the game's own spread work takes two: DistributedWorkBudget)
        public const float SettleMinutes = 10f;    // game minutes before the hour turns by which every redraw asked in it has been made
        public const int StepsKept = 3;            // the margin holds the clock's largest step of a frame this many times over, or nothing is spread
        public const float MinutesInHour = 60f;    // the game's clock: minutes an hour

        /// <summary>
        /// The most game minutes one frame can move the clock on: the engine's largest step of a frame (seconds), at the
        /// game's speed (the time scale, never counted under one), times the game's minutes a second of game time.
        /// </summary>
        public static float StepMost(float maximumDeltaTime, float timeScale, float minutesASecond)
        {
            return Math.Max(0f, maximumDeltaTime) * Math.Max(1f, timeScale) * Math.Max(0f, minutesASecond);
        }

        /// <summary>
        /// The game minutes a second of game time the clock was seen to run at between two looks a frame apart (the larger of
        /// the two frames' steps taken, so that a long frame before a short one is not read as a fast clock). Nothing if it
        /// did not move forward within the hour's span.
        /// </summary>
        public static float Seen(double clockBefore, double clockNow, float stepBefore, float stepNow)
        {
            double moved = clockNow - clockBefore;
            float step = Math.Max(stepBefore, stepNow);
            return moved > 0.0 && moved < MinutesInHour && step > 0f ? (float)(moved / step) : 0f;
        }

        /// <summary>
        /// True if the redraws may wait for the frames to come: alone, the game's time skip not running, at no more than
        /// the normal speed, with a clock whose largest step the margin holds <see cref="StepsKept"/> times over, and the
        /// hour's last <see cref="SettleMinutes"/> not begun. Otherwise everything waiting is drawn at once.
        /// </summary>
        public static bool Spread(bool alone, bool skipping, float timeScale, float stepMost, float minute)
        {
            return alone && !skipping && timeScale <= 1.001f && stepMost * StepsKept < SettleMinutes && minute >= 0f && minute < MinutesInHour - SettleMinutes;
        }

        /// <summary>True if another redraw fits into this frame: always the first, then while the frame's share is under the budget.</summary>
        public static bool More(int done, double ms)
        {
            return done == 0 || ms < BudgetMs;
        }

        /// <summary>True if a redraw asked for in one game hour (day * 24 + hour) is being made in a later one: the hour turned first.</summary>
        public static bool Late(int askedHour, int hourNow)
        {
            return hourNow != askedHour;
        }
    }
}
