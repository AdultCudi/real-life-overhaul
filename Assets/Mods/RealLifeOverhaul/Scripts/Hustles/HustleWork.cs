using System;
using UI.Elements;

namespace RealLifeOverhaul.Hustles
{
    /// <summary>
    /// A piece of the Marketplace's work left to the game's clock on its own panel (Jobs/ShiftSkip, as the game's employer job
    /// does it): a repair, a clean, the wait at a repair shop or a client's visit. What it says is made once, when it is
    /// offered; what it does is the director's, called once when it is over, whole or stopped. Costs nothing a frame.
    /// </summary>
    public sealed class HustleWork : ISkippableShift
    {
        private readonly string _title;
        private readonly int _minutes;
        private readonly LabelInfo[] _labels;
        private Action<int, bool> _ended;

        /// <param name="title">The panel's headline, in final words.</param>
        /// <param name="minutes">Game minutes the work takes.</param>
        /// <param name="lines">What the panel says before the start, in final words.</param>
        /// <param name="ended">Minutes worked and whether it was all done; called once.</param>
        public HustleWork(string title, int minutes, string[] lines, Action<int, bool> ended)
        {
            _title = title;
            _minutes = Math.Max(1, minutes);
            _ended = ended;
            _labels = new LabelInfo[lines != null ? lines.Length : 0];
            for (int i = 0; i < _labels.Length; i++)
            {
                _labels[i] = new LabelInfo(lines[i], GameUi.WorldPanel.White(), false);
            }
        }

        /// <summary>True from the start of the clock until it is over.</summary>
        public bool Working { get; private set; }

        public string SkipTitle => _title;

        public int SkipMinutes => _minutes;

        public LabelInfo[] SkipLabels()
        {
            return _labels;
        }

        public void SkipStarted()
        {
            Working = true;
        }

        public void SkipWorked(int minutes)
        {
        }

        public void SkipEnded(int minutes, bool whole)
        {
            Working = false;
            Action<int, bool> ended = _ended;
            _ended = null;
            ended?.Invoke(minutes, whole);
        }

        /// <summary>The panel was closed before the clock started, or the work is called off: nothing more is done.</summary>
        public void Drop()
        {
            Working = false;
            _ended = null;
        }
    }
}
