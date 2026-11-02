using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Ui
{
    /// <summary>A kind of work a kit screen does, for its line.</summary>
    public enum UiWork : byte
    {
        Frame = 0,   // its host's frame (the input read, a layout on a change), or its watcher's
        Open,        // shown: laid out fresh, arriving
        Press,       // a click, a key's or the pad's press, the layout after it in
        Pointer,     // a hover, the wheel, a drag
        Layout       // one layout and write, wherever it runs (inside the others)
    }

    /// <summary>
    /// What one kit screen costs while it shows, by name, for the session record (Developer diagnostics on, so Core/HotSpots
    /// runs): its frames' work, its open, its layouts and its presses, the game's own work a press hands to the game kept
    /// apart (HotSpots.Game) and the mod's other scripts the game runs inside it named apart, and the heap's growth under
    /// it in the sampled frames. A screen gives one Verbose line as it closes (<see cref="Line"/>), judged by the budgets
    /// here; the HUD's pieces (the phone, the notices) give none, and the interface's share of every frame, paused ones in
    /// (the full menu pauses the game), goes into the minute's session line (<see cref="MinuteLine"/>). Game-free: checked
    /// by NavCheck. COST: as shipped, one flag test a scope; while the record runs, two clock reads and a few adds a scope;
    /// nothing allocated but the lines.
    /// </summary>
    public sealed class UiCost
    {
        public const double FrameBudgetMs = 0.5;    // a screen's own work a frame while it shows, on average
        public const double ClickBudgetMs = 8.0;    // its open, and each press: the kit's own work in it
        public const double SteadyBudgetMs = 0.1;   // the interface a frame with none of its screens showing, on average

        /// <summary>The screen's name in the lines.</summary>
        public readonly string Name;

        /// <summary>A piece of the HUD, shown while the player walks the city: no line of its own, its share in the minute's.</summary>
        public readonly bool Hud;

        private int _depth;          // this screen's scopes open
        private int _pressDepth;     // of them presses: a press inside a press (a click's own press) is one
        private bool _shown;
        private int _shownFrame;
        private double _shownAt;
        private int _workFrame = int.MinValue;
        private long _frameWork;     // its work in _workFrame
        private long _work, _worstFrame, _open, _bytes;
        private bool _opened;
        private int _layouts, _presses;
        private long _layoutWorst, _pressWorst, _pressInside, _pressGame, _pressGameAll;

        // The interface as a whole: every screen's scopes, each counted once however they sit inside each other.
        private static int _uiDepth;
        private static bool _framed;          // a frame of the interface's has been seen since the record started
        private static int _frame;            // that frame, once _framed
        private static long _frameTicks;
        private static bool _frameScreen;     // a screen (not a piece of the HUD) worked or showed in the frame
        private static int _screensShown;
        private static UiCost _frameTop;
        private static int _minuteFrom;       // the minute's first frame, once _framed
        private static long _minuteTicks, _minuteScreenTicks, _minuteWorst, _minuteBytes, _minuteGame;
        private static int _minuteScreenFrames;
        private static string _minuteWorstName;

        /// <summary>The kit's own upkeep outside any screen: its motion, and its looks at the game once a second.</summary>
        public static readonly UiCost Kit = new UiCost("the kit's upkeep", true);

        public UiCost(string name, bool hud)
        {
            Name = string.IsNullOrEmpty(name) ? "a screen" : name;
            Hud = hud;
        }

        private static double Ms(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;

        private static int FrameNow() => HotSpots.FrameSource != null ? HotSpots.FrameSource() : 0;

        /// <summary>True while the screen shows (between <see cref="Shown"/> and <see cref="Hidden"/>).</summary>
        public bool Showing => _shown;

        /// <summary>The last showing's figures (kept after it closes, for the checks): presses, layouts, and milliseconds.</summary>
        public int Presses => _presses;
        public int Layouts => _layouts;
        public double WorkMs => Ms(_work);
        public double OpenMs => Ms(_open);
        public double PressMs => Ms(_pressWorst);           // the kit's own in its costliest press
        public double PressInsideMs => Ms(_pressInside);    // the mod's other scripts the game ran inside a press, at most
        public double PressGameMs => Ms(_pressGame);        // the game's own work a press handed to it, at most

        /// <summary>The screen comes up: its figures start from nothing. <paramref name="time"/> is the engine's unscaled time.</summary>
        public void Shown(double time)
        {
            if (_shown)
            {
                return;
            }
            RollTo(FrameNow());
            _shown = true;
            _shownFrame = FrameNow();
            _shownAt = time;
            _work = _worstFrame = _open = _bytes = 0L;
            _frameWork = 0L;
            _workFrame = int.MinValue;
            _opened = false;
            _layouts = _presses = 0;
            _layoutWorst = _pressWorst = _pressInside = _pressGame = _pressGameAll = 0L;
            if (!Hud)
            {
                _screensShown++;
                _frameScreen = true;
            }
        }

        /// <summary>
        /// The screen goes: its line, or null for a piece of the HUD, a screen shown while nothing was measured, or one
        /// that did no work. <paramref name="time"/> is the engine's unscaled time.
        /// </summary>
        public string Hidden(double time)
        {
            if (!_shown)
            {
                return null;
            }
            RollTo(FrameNow());
            _shown = false;
            if (!Hud)
            {
                _screensShown = Math.Max(0, _screensShown - 1);
            }
            CloseFrame();
            if (Hud || !HotSpots.On || _work <= 0L)
            {
                return null;
            }
            return Line(Math.Max(1, FrameNow() - _shownFrame), time - _shownAt);
        }

        /// <summary>A scope round a piece of the screen's work; while nothing is measured, an empty value that does nothing.</summary>
        public Scope Time(UiWork work)
        {
            if (!HotSpots.On)
            {
                return default;
            }
            int frame = FrameNow();
            if (!_framed || frame != _frame)
            {
                Roll(frame);
            }
            int every = HotSpots.SampleEvery;
            long heap = (every <= 1 || frame % every == 0) && HotSpots.HeapSource != null ? HotSpots.HeapSource() : -1L;
            _uiDepth++;
            _depth++;
            if (work == UiWork.Press)
            {
                _pressDepth++;
            }
            long began = Stopwatch.GetTimestamp();
            return new Scope(this, work, began == 0L ? 1L : began, HotSpots.GameTicks, HotSpots.InsideGameTicks, heap, frame);
        }

        /// <summary>What <see cref="Time"/> returns, closed at the end of its using block; a struct, so nothing is allocated.</summary>
        public readonly struct Scope : IDisposable
        {
            private readonly UiCost _cost;
            private readonly UiWork _work;
            private readonly long _began, _gameAt, _insideAt, _heap;
            private readonly int _frame;

            internal Scope(UiCost cost, UiWork work, long began, long gameAt, long insideAt, long heap, int frame)
            {
                _cost = cost;
                _work = work;
                _began = began;
                _gameAt = gameAt;
                _insideAt = insideAt;
                _heap = heap;
                _frame = frame;
            }

            public void Dispose()
            {
                if (_began != 0L && _cost != null)
                {
                    _cost.End(_work, _began, _gameAt, _insideAt, _heap, _frame);
                }
            }
        }

        private void End(UiWork work, long began, long gameAt, long insideAt, long heap, int frame)
        {
            long ended = Stopwatch.GetTimestamp();
            long game = Math.Max(0L, HotSpots.GameTicks - gameAt);         // the game's own work it handed on: not the mod's
            long inside = Math.Max(0L, HotSpots.InsideGameTicks - insideAt);   // the mod's other scripts the game ran meanwhile
            long own = Math.Max(1L, ended - began - game);
            long grown = heap >= 0L && HotSpots.HeapSource != null ? HotSpots.HeapSource() - heap : 0L;
            _uiDepth = Math.Max(0, _uiDepth - 1);
            _depth = Math.Max(0, _depth - 1);
            switch (work)
            {
                case UiWork.Layout:
                    _layouts++;
                    _layoutWorst = Math.Max(_layoutWorst, own);
                    break;
                case UiWork.Open:
                    if (!_opened && _shown)
                    {
                        _opened = true;
                        _open = Math.Max(1L, own - inside);
                    }
                    break;
                case UiWork.Press:
                    _pressDepth = Math.Max(0, _pressDepth - 1);
                    if (_pressDepth == 0)
                    {
                        _presses++;
                        _pressWorst = Math.Max(_pressWorst, Math.Max(1L, own - inside));
                        _pressInside = Math.Max(_pressInside, inside);
                        _pressGame = Math.Max(_pressGame, game);
                        _pressGameAll += game;
                    }
                    break;
            }
            if (_depth == 0)
            {
                if (frame != _workFrame)
                {
                    CloseFrame();
                    _workFrame = frame;
                }
                _frameWork += own;
                if (_shown)
                {
                    _work += own;
                    _bytes += Math.Max(0L, grown);
                }
            }
            if (_uiDepth == 0)
            {
                _frameTicks += own;
                _minuteBytes += Math.Max(0L, grown);
                _minuteGame += game;
                if (_frameTop == null || _frameWork > _frameTop._frameWork)
                {
                    _frameTop = this;
                }
            }
            if (!Hud)
            {
                _frameScreen = true;
            }
        }

        // The screen's frame before is over: its most in a frame kept while it shows.
        private void CloseFrame()
        {
            if (_shown && _frameWork > _worstFrame)
            {
                _worstFrame = _frameWork;
            }
            _frameWork = 0L;
        }

        // The frame before goes into the minute before a screen comes or goes in this one.
        private static void RollTo(int frame)
        {
            if (HotSpots.On && (!_framed || frame != _frame))
            {
                Roll(frame);
            }
        }

        // A new frame for the interface: the one before goes into the minute.
        private static void Roll(int frame)
        {
            if (!_framed)
            {
                _framed = true;
                _minuteFrom = frame;
            }
            else
            {
                _minuteTicks += _frameTicks;
                if (_frameScreen || _screensShown > 0)
                {
                    _minuteScreenTicks += _frameTicks;
                    _minuteScreenFrames++;
                }
                if (_screensShown > 0 && frame - _frame > 1)
                {
                    _minuteScreenFrames += frame - _frame - 1;   // frames a screen showed in with no work of the interface's (held under a box)
                }
                if (_frameTicks > _minuteWorst)
                {
                    _minuteWorst = _frameTicks;
                    _minuteWorstName = _frameTop != null ? _frameTop.Name : null;
                }
            }
            _frame = frame;
            _frameTicks = 0L;
            _frameScreen = _screensShown > 0;
            _frameTop = null;
        }

        /// <summary>The screen's line as it closes: its own work a frame, its open, its layouts, its presses, the heap, the verdict.</summary>
        public string Line(int frames, double seconds)
        {
            double perFrame = Ms(_work) / Math.Max(1, frames);
            double open = Ms(_open), press = Ms(_pressWorst);
            bool within = perFrame < FrameBudgetMs && open < ClickBudgetMs && press < ClickBudgetMs;
            var text = new StringBuilder(320);
            text.Append("UI check (").Append(Name).Append(", open ").Append(Fixed(seconds, 1)).Append(" s, ").Append(frames).Append(" frames): the kit's own work ")
                .Append(Fixed(perFrame, 3)).Append(" ms a frame on average and ").Append(Fixed(Ms(_worstFrame), 1)).Append(" ms at most in a frame; its open ")
                .Append(_opened ? Fixed(open, 1) + " ms" : "not measured").Append("; ").Append(_layouts).Append(_layouts == 1 ? " layout" : " layouts");
            if (_layouts > 0)
            {
                text.Append(", ").Append(Fixed(Ms(_layoutWorst), 1)).Append(" ms at most");
            }
            text.Append("; ").Append(_presses).Append(_presses == 1 ? " press" : " presses");
            if (_presses > 0)
            {
                text.Append(", the kit's own ").Append(Fixed(press, 1)).Append(" ms at most");
                if (_pressInside > 0L || _pressGame > 0L)
                {
                    text.Append(" (in one of them at most ").Append(Fixed(Ms(_pressInside), 1)).Append(" ms of the mod's other scripts the game ran, named in the session record, and ")
                        .Append(Fixed(Ms(_pressGame), 1)).Append(" ms of the game's own work the press handed to it, ").Append(Fixed(Ms(_pressGameAll), 1))
                        .Append(" ms in all, which is the game's and not in the mod's figures)");
                }
            }
            if (seconds > 0.0)
            {
                int every = Math.Max(1, HotSpots.SampleEvery);
                text.Append("; the heap grew under it by ").Append(every > 1 ? "about " : string.Empty).Append(Fixed(_bytes * (double)every / 1024.0 / seconds, 1))
                    .Append(" KB a second").Append(every > 1 ? " (one frame in " + every + " sampled)" : string.Empty);
            }
            text.Append("; wanted under ").Append(Fixed(FrameBudgetMs, 1)).Append(" ms a frame and under ").Append(Fixed(ClickBudgetMs, 0))
                .Append(" ms for its open and each press: ").Append(within ? "PASS." : "OVER.");
            return text.ToString();
        }

        /// <summary>
        /// The interface's share of the minute added to the session line, then cleared: its work a frame (paused frames in),
        /// with a screen of it showing and without, its most in a frame and whose, the heap's growth under it, and the game's
        /// own work its presses handed to the game.
        /// </summary>
        public static void MinuteLine(StringBuilder into, double seconds)
        {
            int now = FrameNow();
            if (_framed && now != _frame)
            {
                Roll(now);
            }
            int frames = _framed ? Math.Max(0, _frame - _minuteFrom) : 0;
            if (frames > 0)
            {
                int without = Math.Max(0, frames - _minuteScreenFrames);
                double steady = without > 0 ? Ms(_minuteTicks - _minuteScreenTicks) / without : 0.0;
                into.Append("; the RLO interface: ").Append(Fixed(Ms(_minuteTicks) / frames, 3)).Append(" ms a frame over the minute's ").Append(frames)
                    .Append(" frames, paused ones in (").Append(Fixed(steady, 3)).Append(" ms a frame in the ").Append(without).Append(" with none of its screens showing, wanted under ")
                    .Append(Fixed(SteadyBudgetMs, 1)).Append(": ").Append(steady < SteadyBudgetMs ? "PASS" : "OVER");
                if (_minuteScreenFrames > 0)
                {
                    into.Append("; ").Append(Fixed(Ms(_minuteScreenTicks) / _minuteScreenFrames, 3)).Append(" ms a frame in the ").Append(_minuteScreenFrames).Append(" with one showing");
                }
                into.Append("), ").Append(Fixed(Ms(_minuteWorst), 1)).Append(" ms at most in a frame");
                if (_minuteWorstName != null)
                {
                    into.Append(" (").Append(_minuteWorstName).Append(')');
                }
                into.Append(", the heap grew under it by ").Append(HotSpots.SampleEvery > 1 ? "about " : string.Empty)
                    .Append(Fixed(_minuteBytes * (double)Math.Max(1, HotSpots.SampleEvery) / 1024.0 / Math.Max(0.001, seconds), 1)).Append(" KB a second");
                if (_minuteGame > 0L)
                {
                    into.Append("; the game's own work its presses handed to the game: ").Append(Fixed(Ms(_minuteGame), 1)).Append(" ms, the game's and not in the mod's figures");
                }
            }
            _minuteFrom = _frame;
            _minuteTicks = _minuteScreenTicks = _minuteWorst = _minuteBytes = _minuteGame = 0L;
            _minuteScreenFrames = 0;
            _minuteWorstName = null;
        }

        /// <summary>The interface's figures start again (the record starts or stops); each screen's own are left to its next showing.</summary>
        public static void Reset()
        {
            _uiDepth = 0;
            _framed = false;
            _frame = _minuteFrom = 0;
            _frameTicks = 0L;
            _frameTop = null;
            _minuteTicks = _minuteScreenTicks = _minuteWorst = _minuteBytes = _minuteGame = 0L;
            _minuteScreenFrames = 0;
            _minuteWorstName = null;
        }

        private static string Fixed(double value, int places) => value.ToString("F" + places, CultureInfo.InvariantCulture);
    }
}
