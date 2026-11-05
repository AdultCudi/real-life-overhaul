using System;

namespace RealLifeOverhaul.Flare
{
    /// <summary>
    /// Which of the day's steps are still owed (FlareDirector.DayStep). A step is taken one at a time; the save, the next day and standing down
    /// finish what is owed at once (<see cref="Finish"/>). A step that takes long (the walk over the city's premises) is not one of them: it is
    /// queued by its step and walked sliced elsewhere (FlareToLetWalk), so finishing the round never pays for it.
    /// </summary>
    public sealed class FlareDayRound
    {
        private int _next = -1;

        /// <summary>The day the steps are for.</summary>
        public int Day { get; private set; }

        /// <summary>A day's steps are still owed.</summary>
        public bool Owed => _next >= 0;

        /// <summary>The steps of a day are owed, from the first.</summary>
        public void Begin(int day)
        {
            Day = day;
            _next = 0;
        }

        /// <summary>The next step to run, moving on; -1 when none is owed.</summary>
        public int Take()
        {
            return _next < 0 ? -1 : _next++;
        }

        /// <summary>A step wants another go: the next to run is this one again.</summary>
        public void Back(int step)
        {
            _next = step;
        }

        /// <summary>The round is over, or dropped.</summary>
        public void End()
        {
            _next = -1;
        }

        /// <summary>Everything still owed, at once, each through <paramref name="step"/> (which takes the steps).</summary>
        public void Finish(Action step)
        {
            while (_next >= 0)
            {
                step();
            }
        }
    }
}
