namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// The white box's own time on a page, for its self-check: only the time inside the box's calls, the outermost of nested
    /// ones, so the app's own work between Clear and Done is not blamed on the box. Game-free; checked offline
    /// (ContractCheck BoxClockChecks). Two comparisons and an addition a call.
    /// </summary>
    public sealed class BoxClock
    {
        private int _depth;
        private long _began, _entered, _own;

        public void Begin(long now)
        {
            _began = now;
            _own = 0;
            _depth = 0;
        }

        /// <summary>A call of the box's begins; one inside another counts with it.</summary>
        public void Enter(long now)
        {
            if (_depth++ == 0)
            {
                _entered = now;
            }
        }

        /// <summary>A call of the box's ends; the outermost one adds its time.</summary>
        public void Leave(long now)
        {
            if (_depth > 0 && --_depth == 0)
            {
                _own += now - _entered;
            }
        }

        /// <summary>The box's own time on the page so far, in the timestamps' units, a call under way counted to now.</summary>
        public long Own(long now)
        {
            return _depth > 0 ? _own + (now - _entered) : _own;
        }

        /// <summary>The page's whole time since it began: the box's and the app's work between its calls.</summary>
        public long Page(long now)
        {
            return now - _began;
        }
    }
}
