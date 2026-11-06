namespace RealLifeOverhaul.Flare
{
    /// <summary>
    /// Flare's work on the game's hour, in steps of a frame each (FlareDirector.HourStep): the city's posts, the City
    /// Office's cases, invitations and the patrols watched, the count of unread, the words asked ahead, and the player's
    /// own milestones. The whole hour in one frame was a frame of 53 to 63 ms in the session record, the first hour after
    /// a load; a step a frame is the same work in the same order, a few frames later. Hours that come
    /// faster than their steps (a time skip) wait in turn; anything that needs the hour done first (the save, the day,
    /// standing down) runs what is left at once (<see cref="Next"/> until false). No Unity or game types: checked
    /// offline (NavCheck, FlareHourCheck.cs).
    /// </summary>
    public sealed class FlareHourSteps
    {
        public const int Steps = 6;    // the hour's steps, in their order
        public const int Most = 48;    // hours waiting at the most: two days of a skip; then the oldest is finished at once

        public const int City = 0;         // the Ledger's headlines, the followed notice, viral shares sold
        public const int Cases = 1;        // the City Office's answers due this hour and the works done
        public const int Out = 2;          // the invitations starting now, and the patrols and cleaners Flare has sent
        public const int Unread = 3;       // the count of what the player has not read
        public const int Words = 4;        // the words of the hour's answers and replies asked now
        public const int Milestones = 5;   // a promotion, a degree, a vote or an endorsement to post about

        private readonly int[] _days = new int[Most];
        private readonly int[] _hours = new int[Most];
        private int _head, _count, _step;

        /// <summary>True while an hour's steps are waiting.</summary>
        public bool Busy => _count > 0;

        /// <summary>Hours waiting, the one under way among them.</summary>
        public int Waiting => _count;

        /// <summary>
        /// An hour of the game began. False if the queue is full: the caller runs what is left of the oldest at once
        /// (<see cref="Next"/>) and adds again.
        /// </summary>
        public bool Add(int day, int hour)
        {
            if (_count == Most)
            {
                return false;
            }
            int at = (_head + _count) % Most;
            _days[at] = day;
            _hours[at] = hour;
            _count++;
            return true;
        }

        /// <summary>The next step to run, of the oldest hour waiting; false when none waits.</summary>
        public bool Next(out int day, out int hour, out int step)
        {
            if (_count == 0)
            {
                day = hour = step = -1;
                return false;
            }
            day = _days[_head];
            hour = _hours[_head];
            step = _step;
            _step++;
            if (_step == Steps)
            {
                _step = 0;
                _head = (_head + 1) % Most;
                _count--;
            }
            return true;
        }

        /// <summary>Nothing waits any more: Flare stopped.</summary>
        public void Clear()
        {
            _head = _count = _step = 0;
        }
    }
}
